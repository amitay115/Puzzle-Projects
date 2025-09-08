
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ModelMenuPanel : MonoBehaviour
{
    [Header("Refs")]
    public HoverHighlightManager selectionManager;   // מנהל בחירה (יש בו SelectFromUI + OnSelectionChanged)
    public Transform modelRoot;                      // שורש המודל
    public RectTransform content;                    // ScrollView/Viewport/Content
    public Button headerButton;                      // כפתור "Menu"
    public TextMeshProUGUI headerLabel;              // כיתוב "Menu"
    public TextMeshProUGUI iconLabel;                // "▾/▸"
    public CanvasGroup canvasGroup;                  // אופציונלי (שקיפות/אינטרקטיביות)
    public RectTransform panelRect;                  // RectTransform של הפאנל (זה)
    public RectTransform headerRect;                 // RectTransform של ההדר (למדידת גובה סגור)

    [Header("Prefabs")]
    public ModelMenuItem itemPrefab;                 // עלה (כפתור יחיד)
    public ModelMenuGroupItem groupPrefab;           // קבוצה (Header + Children)

    [Header("Open/Close")]
    [Range(0.3f, 1f)] public float openMaxScreenFrac = 0.9f;
    public bool startOpen = true;
    public float animTime = 0.15f;
    public float closedHeight = 40f;

    [Header("Hierarchy View")]
    public float indentPerLevel = 18f;       // הזחה לפי level
    public bool expandGroupsByDefault = false;
    public bool uniqueByMenuId = true;      // מאחד Highlightables עם אותו menuId לפריט אחד
    public bool requireCollider = false;    // צור כפתור רק למי שיש עליו Collider פעיל

    // ===== פנימי =====
    interface IMenuRow { void SetSelected(bool on); RectTransform RT { get; } }
    class ItemRowAdapter : IMenuRow
    {
        public ModelMenuItem row; public RectTransform RT => (RectTransform)row.transform;
        public ItemRowAdapter(ModelMenuItem r) { row = r; }
        public void SetSelected(bool on) => row.SetSelected(on);
    }
    class GroupRowAdapter : IMenuRow
    {
        public ModelMenuGroupItem row; public RectTransform RT => (RectTransform)row.transform;
        public GroupRowAdapter(ModelMenuGroupItem r) { row = r; }
        public void SetSelected(bool on) => row.SetSelected(on);
    }

    class Node
    {
        public string id, label, parentId;
        public int level;
        public Highlightable rep;          // נציג לבחירה (Highlightable)
        public List<Node> children = new();
    }

    Dictionary<Highlightable, IMenuRow> _map = new();
    Highlightable _lastSel;
    bool _open;
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

        if (headerButton) headerButton.onClick.AddListener(ToggleOpen);
        if (headerLabel) headerLabel.text = "Menu";

        if (selectionManager) selectionManager.OnSelectionChanged += OnSelectionChanged;

        _open = startOpen;
        SetIcon(_open);
        SetOpen(_open, instant: true);

        BuildList();
    }

    void OnDestroy()
    {
        if (selectionManager) selectionManager.OnSelectionChanged -= OnSelectionChanged;
    }

    void Update()
    {
        // סנכרון בחירה ↔ רשימה
        if (selectionManager)
        {
            var sel = selectionManager.GetSelected();
            if (sel != _lastSel)
            {
                _lastSel = sel;
                UpdateVisualSelection(sel);
            }
        }

        // אנימציה לגובה
        float targetH = _open ? Mathf.Clamp(GetPreferredHeight(), 160f, Screen.height) : closedHeight;
        var sd = panelRect.sizeDelta;
        sd.y = Mathf.SmoothDamp(sd.y, targetH, ref _hVel, animTime);
        panelRect.sizeDelta = sd;
    }

    // ===== בניית הרשימה ההיררכית =====
    public void BuildList()
    {
        // ניקוי
        foreach (Transform c in content) Destroy(c.gameObject);
        _map.Clear();

        if (!modelRoot || !itemPrefab || !groupPrefab) return;

        // 1) אסוף Highlightable-ים רלוונטיים
        var all = modelRoot.GetComponentsInChildren<Highlightable>(true)
                           .Where(h => h && h.gameObject.activeInHierarchy);

        if (requireCollider) all = all.Where(h => { var col = h.GetComponent<Collider>(); return col && col.enabled; });

        // 2) המר ל-Nodes לפי השדות בתפריט
        var byId = new Dictionary<string, Node>();
        var raw = new List<Node>();

        foreach (var h in all)
        {
            string id = !string.IsNullOrEmpty(h.menuId) ? h.menuId : h.name;
            string label = !string.IsNullOrEmpty(h.menuLabel) ? h.menuLabel : h.name;
            int lvl = Mathf.Max(0, h.menuLevel);
            string pid = h.parentMenuId ?? "";

            if (uniqueByMenuId)
            {
                if (!byId.TryGetValue(id, out var n))
                {
                    n = new Node { id = id, label = label, level = lvl, parentId = pid, rep = h };
                    byId[id] = n; raw.Add(n);
                }
                else
                {
                    // מאחדים לפי menuId: שומרים נציג ראשון, מעדכנים מידע משני
                    n.level = Mathf.Min(n.level, lvl);
                    if (string.IsNullOrEmpty(n.parentId) && !string.IsNullOrEmpty(pid)) n.parentId = pid;
                    if (string.IsNullOrEmpty(n.label)) n.label = label;
                }
            }
            else
            {
                raw.Add(new Node { id = id, label = label, level = lvl, parentId = pid, rep = h });
            }
        }

        if (raw.Count == 0) return;

        // 3) חבר הורים-ילדים לפי parentMenuId (לוגי, לא הייררכיה ב-Transform)
        var index = raw.GroupBy(n => n.id).ToDictionary(g => g.Key, g => g.First());
        var roots = new List<Node>();
        foreach (var n in raw)
        {
            if (string.IsNullOrEmpty(n.parentId) || !index.TryGetValue(n.parentId, out var parent))
                roots.Add(n);
            else
                parent.children.Add(n);
        }

        // 4) מיון נעים לעין
        void Sort(Node node)
        {
            node.children = node.children
                .OrderBy(c => c.level)
                .ThenBy(c => c.label, System.StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (var c in node.children) Sort(c);
        }
        foreach (var r in roots) Sort(r);

        // 5) בניית UI רקורסיבית (Group/Item)
        foreach (var r in roots) BuildUIRecursive(r, content);

        // רענון Layout כדי שגובה פתוח יחושב נכון
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        if (_open) ResizeToPreferred();
    }

    void BuildUIRecursive(Node n, RectTransform parent)
    {
        bool hasChildren = n.children.Count > 0;

        if (!hasChildren)
        {
            // עלה – כפתור בודד
            var item = Instantiate(itemPrefab, parent);

            var rt = (RectTransform)item.transform;
            StretchTop(rt);
            ApplyIndent(rt, n.level);

            item.SetText(string.IsNullOrEmpty(n.label) ? n.id : n.label);
            item.SetSelected(false);

            if (item.button)
            {
                item.button.onClick.RemoveAllListeners();
                item.button.onClick.AddListener(() =>
                {
                    if (selectionManager && n.rep)
                        selectionManager.SelectFromUI(n.rep);
                });
                // אופציונלי: מניעת ניווט מקלדת שמבלבל פוקוס
                item.button.navigation = new Navigation { mode = Navigation.Mode.None };
            }

            _map[n.rep] = new ItemRowAdapter(item);
            return;
        }

        // קבוצה – כפתור Header + Children מתקפלים
        var grp = Instantiate(groupPrefab, parent);
        var rtGrp = (RectTransform)grp.transform;
        StretchTop(rtGrp);
        ApplyIndent(rtGrp, n.level);

        grp.SetTitle(string.IsNullOrEmpty(n.label) ? n.id : n.label);
        grp.SetExpanded(expandGroupsByDefault);

        // נקה מאזינים שהוגדרו באינספקטור (אם יש) – שלא יהיה כפול
        if (grp.headerButton)
        {
            grp.headerButton.onClick.RemoveAllListeners();

            grp.headerButton.onClick.AddListener(() =>
            {
                // 1) פתח/סגור עכשיו (לפני הבחירה), כדי שלא ילך לאיבוד ברענון
                grp.ToggleExpanded();
                if (_open) ResizeToPreferred();

                // 2) בצע בחירה בסוף הפריים – אחרי שה־UI התעדכן
                if (selectionManager && n.rep)
                    StartCoroutine(SelectNextFrame(n.rep));
            });

            // אופציונלי: כבה ניווט מקלדת שמפריע לפוקוס
            grp.headerButton.navigation = new Navigation { mode = Navigation.Mode.None };
        }

        _map[n.rep] = new GroupRowAdapter(grp);

        // בנה את הילדים לתוך המכולה של הקבוצה
        var container = grp.childrenContainer ? grp.childrenContainer : (RectTransform)grp.transform;
        foreach (var c in n.children)
            BuildUIRecursive(c, container);
    }

    // ===== בחירה ← UI =====
    void OnSelectionChanged(Highlightable sel) => UpdateVisualSelection(sel);

    void UpdateVisualSelection(Highlightable sel)
    {
        foreach (var kv in _map) kv.Value.SetSelected(kv.Key == sel);

        if (sel && _map.TryGetValue(sel, out var row))
            ScrollTo(row.RT);
    }

    void ScrollTo(RectTransform item)
    {
        var scroll = content.parent?.parent?.GetComponent<ScrollRect>();
        var vp = content.parent as RectTransform;
        if (!scroll || !vp || !item) return;

        Canvas.ForceUpdateCanvases();
        var itemWorld = item.TransformPoint(item.rect.center);
        var viewWorld = vp.TransformPoint(vp.rect.center);
        float signed = (itemWorld.y - viewWorld.y) / Mathf.Max(1f, vp.rect.height);
        scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + signed);
    }

    // ===== Open / Close =====
    public void ToggleOpen()
    {
        bool newState = !content.gameObject.activeSelf;
        SetOpen(newState, instant: false);
    }

    void SetOpen(bool open, bool instant)
    {
        _open = open;
        SetIcon(open); // "▾/▸"

        if (content) content.gameObject.SetActive(open);

        float target = open
            ? Mathf.Clamp(GetPreferredHeight(), 160f, Screen.height * 0.9f)
            : closedHeight;

        if (instant)
        {
            ResizeToTargetHeight(target);
        }
        // אחרת, Update עושה SmoothDamp לגובה
    }

    void SetIcon(bool open)
    {
        if (iconLabel) iconLabel.text = open ? "▾" : "▸";
    }

    float GetPreferredHeight()
    {
        if (!content) return closedHeight + 200f;

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        float headerH = CalcClosedHeight();
        float contentPreferred = LayoutUtility.GetPreferredHeight(content);
        float cap = Screen.height * openMaxScreenFrac;

        return Mathf.Min(headerH + contentPreferred, cap);
    }

    float CalcClosedHeight()
    {
        if (headerRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(headerRect);
            float headerH = LayoutUtility.GetPreferredHeight(headerRect);

            var vlg = GetComponent<VerticalLayoutGroup>();
            float pad = vlg ? (vlg.padding.top + vlg.padding.bottom) : 0f;

            return Mathf.Ceil(headerH + pad);
        }
        return closedHeight;
    }

    void ResizeToPreferred() => ResizeToTargetHeight(GetPreferredHeight());

    void ResizeToTargetHeight(float h)
    {
        var sd = panelRect.sizeDelta; sd.y = h; panelRect.sizeDelta = sd;
    }

    // ===== עזרי Rect =====
    void StretchTop(RectTransform rt)
    {
        if (!rt) return;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(0f, rt.offsetMin.y);
        rt.offsetMax = new Vector2(0f, rt.offsetMax.y);
    }

    void ApplyIndent(RectTransform rt, int level)
    {
        if (!rt) return;
        float left = Mathf.Max(0, level) * indentPerLevel;
        rt.offsetMin = new Vector2(left, rt.offsetMin.y);
    }
    
    private System.Collections.IEnumerator SelectNextFrame(Highlightable h)
    {
        // תחכה פריים אחד כדי לתת ל־Layout/Content להתעדכן
        yield return null;

        if (selectionManager != null && h != null)
            selectionManager.SelectFromUI(h);
    }
}
