using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


public class ModelMenuPanel : MonoBehaviour
{
    [Header("Refs")]
    public HoverHighlightManager selectionManager;   // גרור את המצלמה
    public Transform modelRoot;                      // השורש של המודל המקורי (ה־parent העליון)
    public RectTransform content;                    // ScrollView/Viewport/Content
    public Button headerButton;                      // הכפתור העליון ("Menu")
    public TextMeshProUGUI headerLabel;              // הטקסט של ההדר
    public TextMeshProUGUI iconLabel;                // טקסט קטן "▾/▸"
    public CanvasGroup canvasGroup;                  // של הפאנל

    [Header("Open/Close")]
    [Range(0.3f, 1f)]
    public float openMaxScreenFrac = 0.9f;   // עד כמה מהמסך מותר לתיבה למלא
    

    [Tooltip("RectTransform של ה-Header (אופציונלי). אם לא הוגדר נשתמש ב-closedHeight.")]
    public RectTransform headerRect;

    [Header("Prefabs")]
    public ModelMenuItem itemPrefab;

    [Header("Open/Close")]
    public bool startOpen = true;
    public float animTime = 0.15f;
    public float closedHeight = 40f;   // גובה סגור
    public RectTransform panelRect;    // RectTransform של הפאנל (זה)

    Dictionary<Highlightable, ModelMenuItem> _map = new();
    Highlightable _lastSel;
    bool _open = true;
    float _hVel;

    void Reset()
    {
        panelRect = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
    }

    void Awake()
    {
        if (!panelRect) panelRect = GetComponent<RectTransform>();
        if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();
        if (!canvasGroup) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (headerButton) headerButton.onClick.AddListener(ToggleOpen);
        if (headerLabel) headerLabel.text = "Menu";
        SetIcon(_open);

        BuildList();
        SetOpen(startOpen, instant: true);

        // ודא עוגנים תקינים גם אם השתבשו:
        var rt = panelRect;
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        SetOpen(startOpen, instant: true);
    }

    void Update()
    {
        // סינכרון בחירה ↔ רשימה
        if (!selectionManager) return;

        var sel = selectionManager.GetSelected();
        if (sel != _lastSel)
        {
            _lastSel = sel;
            UpdateVisualSelection(sel);
        }

        // אנימציה לגובה
        float targetH = _open ? Mathf.Clamp(GetPreferredHeight(), 160f, Screen.height) : closedHeight;
        var sd = panelRect.sizeDelta;
        sd.y = Mathf.SmoothDamp(sd.y, targetH, ref _hVel, animTime);
        panelRect.sizeDelta = sd;
    }

    // בונה את הרשימה מתוך כל Highlightable תחת modelRoot
    public void BuildList()
    {
        // נקה ישנים
        foreach (Transform c in content) Destroy(c.gameObject);
        _map.Clear();

        if (!modelRoot) return;
        var parts = modelRoot.GetComponentsInChildren<Highlightable>(true);
        foreach (var h in parts)
        {
            // דלג על דברים שלא נרצה ברשימה
            if (!h || !h.gameObject.activeInHierarchy) continue;

            var item = Instantiate(itemPrefab, content);

            // לוודא אנקרים לרוחב מלא
            var rt = (RectTransform)item.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0f, rt.offsetMin.y);
            rt.offsetMax = new Vector2(0f, rt.offsetMax.y);

            item.SetText(h.name);
            item.SetSelected(false);

            // אירוע לחיצה: לבחור את החלק במנהל
            item.button.onClick.AddListener(() =>
            {
                // אם יש לך פונקציה מפורשת במנהל - קרא לה; אחרת אפשר דרך API קיים
                if (selectionManager) selectionManager.SelectFromUI(h); // תראה הרחבה למטה
            });

            // Highlight UI בזמן hover (אופציונלי)
            var trigger = item.button.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var entEnter = new UnityEngine.EventSystems.EventTrigger.Entry
            { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
            entEnter.callback.AddListener(_ => item.SetHighlighted(true));
            trigger.triggers.Add(entEnter);

            var entExit = new UnityEngine.EventSystems.EventTrigger.Entry
            { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
            entExit.callback.AddListener(_ => item.SetHighlighted(false));
            trigger.triggers.Add(entExit);

            _map[h] = item;
        }
    }

    void UpdateVisualSelection(Highlightable sel)
    {
        foreach (var kv in _map)
            kv.Value.SetSelected(kv.Key == sel);
        // גלול אל הנבחר (אופציונלי)
        if (sel && _map.TryGetValue(sel, out var item))
            ScrollTo(item.transform as RectTransform);
    }

    void ScrollTo(RectTransform item)
    {
        var scroll = content.parent.parent.GetComponent<ScrollRect>();
        var vp = content.parent as RectTransform;
        if (!scroll || !vp) return;

        var itemWorld = item.TransformPoint(item.rect.center);
        var viewWorld = vp.TransformPoint(vp.rect.center);
        float signed = (itemWorld.y - viewWorld.y) / vp.rect.height;
        scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + signed);
    }

    float GetPreferredHeight()
    {
        if (!content) return closedHeight + 200f;

        // לעדכן את חישובי ה-Layout לפני מדידה
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        float headerH = CalcClosedHeight(); // גובה ההדר
        float contentPreferred = LayoutUtility.GetPreferredHeight(content); // גובה רשימה “אמיתי”

        // תקרה: לא לחרוג מגובה המסך (לפי openMaxScreenFrac)
        float cap = Screen.height * openMaxScreenFrac;

        return Mathf.Min(headerH + contentPreferred, cap);
    }

    float CalcClosedHeight()
    {
        // אם יש רפרנס ל-Header – חשב גובה מועדף שלו + padding של ה-Panel
        if (headerRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(headerRect);
            float headerH = LayoutUtility.GetPreferredHeight(headerRect);

            var vlg = GetComponent<VerticalLayoutGroup>();
            float pad = vlg ? (vlg.padding.top + vlg.padding.bottom) : 0f;

            return Mathf.Ceil(headerH + pad);
        }

        // אחרת – נשתמש בגובה דיפולטי קבוע
        return closedHeight;
    }


    public void ToggleOpen()
    {
        bool newState = !content.gameObject.activeSelf;
        SetOpen(newState, instant: false);
    }


    void SetOpen(bool open, bool instant)
    {
        _open = open;
        SetIcon(open); // "▾/▸"

        // הפעלה/כיבוי התוכן כדי שלא ידחוף את ה-Header
        if (content) content.gameObject.SetActive(open);

        float target = open
            ? Mathf.Clamp(GetPreferredHeight(), 160f, Screen.height * 0.9f)
            : closedHeight;

        if (instant)
        {
            var sd = panelRect.sizeDelta;
            sd.y = target;
            panelRect.sizeDelta = sd;
        }
        // אחרת, Update עושה SmoothDamp לגובה (כבר כתבת)
    }



    void SetIcon(bool open)
    {
        if (iconLabel) iconLabel.text = open ? "▾" : "▸";
    }
    
    
}
