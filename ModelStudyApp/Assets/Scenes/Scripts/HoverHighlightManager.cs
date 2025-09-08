// HoverHighlightManager.cs
using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class HoverHighlightManager : MonoBehaviour
{
    [Header("Picking")]
    public Camera pickCamera;                 // אם ריק נשתמש ב-Camera.main
    public LayerMask pickLayers = ~0;         // אילו שכבות ניתנות לבחירה
    public float rayMaxDistance = 500f;
    public bool blockWhenPointerOverUI = true;

    [Header("Mouse")]
    public int selectMouseButton = 0;         // 0=שמאלי

    // האירוע למאזינים (UI וכו')
    public event Action<Highlightable> OnSelectionChanged;

    

    // מצב פנימי
    Highlightable _hover;     // מה שמתחת לסמן כרגע
    Highlightable _current;   // מה שנבחר בפועל

    // API חיצוני
    public Highlightable GetSelected() => _current;

    void Awake()
    {
        if (!pickCamera) pickCamera = Camera.main;
    }

    void Update()
    {
        // 1) עדכון Hover
        UpdateHover();

        // 2) בחירה בעכבר (לחיצה)
        bool pointerOnUI = blockWhenPointerOverUI &&
                           EventSystem.current != null &&
                           EventSystem.current.IsPointerOverGameObject();

        if (!pointerOnUI && Input.GetMouseButtonDown(selectMouseButton))
        {
            // בחר את מה שמתחת לסמן (אם יש)
            Select(_hover);
        }

        // 3) ניקוי בחירה (Esc)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ClearSelection();
        }
    }

    void UpdateHover()
    {
        Highlightable found = RaycastForHighlightable();

        if (_hover == found) return;

        // כבה hover קודם
        if (_hover) SafeSetHover(_hover, false);

        _hover = found;

        // הדלק hover חדש (לא אם הוא כבר נבחר? לרוב כן – משאירים hover גם על נבחר)
        if (_hover) SafeSetHover(_hover, true);
    }

    Highlightable RaycastForHighlightable()
    {
        if (!pickCamera) return null;

        var ray = pickCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, rayMaxDistance, pickLayers, QueryTriggerInteraction.Ignore))
        {
            // מחפש Highlightable על האובייקט או אחד מההורים
            return hit.collider.GetComponentInParent<Highlightable>();
        }
        return null;
    }

    /// <summary>
    /// בחירה ממקור UI (לחיצה על פריט בתפריט)
    /// </summary>
    public void SelectFromUI(Highlightable h)
    {
        Select(h);
    }

    /// <summary>
    /// בחירה כללית (עכבר/תפריט)
    /// </summary>
    public void Select(Highlightable h)
    {
        if (_current == h) return;

        // נקה נבחר קודם
        if (_current) SafeSetSelected(_current, false);

        _current = h;

        // הדלק נבחר חדש
        if (_current) SafeSetSelected(_current, true);

        // עדכן UI/מאזינים
        OnSelectionChanged?.Invoke(_current);
    }
    
    public void ClearSelection()
    {
        if (_current)
        {
            SafeSetSelected(_current, false);
            _current = null;
            OnSelectionChanged?.Invoke(null);
        }
    }

    // === עוזרים בטוחים: קוראים למתודות אם קיימות ===
    void SafeSetHover(Highlightable h, bool on)
    {
        if (!h) return;
        // נניח שב-Highlightable קיימת SetHover(bool). אם קוראים לה בשם אחר, שנה כאן.
        try { h.SetHover(on); } catch { /* התעלם אם אין */ }
    }

    void SafeSetSelected(Highlightable h, bool on)
    {
        if (!h) return;
        // נניח שב-Highlightable קיימת SetSelected(bool). אם השם שונה – עדכן כאן.
        try { h.SetSelected(on); } catch { /* התעלם אם אין */ }
    }
}
