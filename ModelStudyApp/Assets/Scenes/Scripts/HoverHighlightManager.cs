using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Camera))]
public class HoverHighlightManager : MonoBehaviour
{
    [Header("Scope")]
    public Transform modelRoot;          // אופציונלי: שורש המודל לסינון
    public LayerMask hitLayers = ~0;
    public float maxDistance = 1000f;

    [Header("Input / UI")]
    public bool blockWhenPointerOverUI = true;
    public int selectMouseButton = 0;    // 0=שמאלי
    public KeyCode clearSelectionKey = KeyCode.Escape;

    Camera _cam;
    Highlightable _currentHover;
    Highlightable _currentSelected;

    // לשימוש ה-UI/קוד חיצוני
    public System.Action<Highlightable> OnSelectionChanged;

    void Awake()
    {
        _cam = GetComponent<Camera>();
    }

    void Update()
    {
        bool overUI = blockWhenPointerOverUI &&
                      EventSystem.current != null &&
                      EventSystem.current.IsPointerOverGameObject();

        UpdateHover(overUI);
        UpdateSelection(overUI);
    }

    void UpdateHover(bool overUI)
    {
        if (overUI)
        {
            SetHover(null);
            return;
        }

        var ray = _cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, hitLayers, QueryTriggerInteraction.Ignore))
        {
            var h = FindOrAddHighlightable(hit.transform);
            SetHover(h);
        }
        else
        {
            SetHover(null);
        }
    }

    void UpdateSelection(bool overUI)
    {
        if (Input.GetKeyDown(clearSelectionKey))
        {
            SetSelected(null);
            return;
        }

        if (!overUI && Input.GetMouseButtonDown(selectMouseButton))
        {
            SetSelected(_currentHover); // בוחר את מה שמתחת לסמן כרגע
        }
    }

    void SetHover(Highlightable h)
    {
        if (_currentHover == h) return;
        if (_currentHover != null) _currentHover.SetHover(false);
        _currentHover = h;
        if (_currentHover != null) _currentHover.SetHover(true);
    }

    void SetSelected(Highlightable h)
    {
        if (_currentSelected == h) return;

        if (_currentSelected != null)
            _currentSelected.SetSelected(false);

        _currentSelected = h;

        if (_currentSelected != null)
            _currentSelected.SetSelected(true);

        OnSelectionChanged?.Invoke(_currentSelected);
    }

    public Highlightable GetSelected() => _currentSelected;

    // ——— עזר: מאתר Highlightable נכון לאובייקט שנפגענו בו ———
    Highlightable FindOrAddHighlightable(Transform hit)
    {
        // קודם נעלה באבות עד modelRoot (אם הוגדר), ונעדיף Highlightable על אב
        Transform p = hit;
        while (p != null && (modelRoot == null || p != modelRoot.parent))
        {
            var h = p.GetComponent<Highlightable>();
            if (h != null) return h;
            p = p.parent;
        }

        // אחרת נבחר יחידה הגיונית (אב שיש עליו Renderer/Filter), ונוסיף עליו Highlightable
        p = hit;
        while (p != null && (modelRoot == null || p != modelRoot.parent))
        {
            if (p.GetComponent<MeshRenderer>() != null || p.GetComponent<MeshFilter>() != null)
                break;
            p = p.parent;
        }
        if (p == null) p = hit;

        var hh = p.GetComponent<Highlightable>();
        if (hh == null) hh = p.gameObject.AddComponent<Highlightable>();
        return hh;
    }
}
