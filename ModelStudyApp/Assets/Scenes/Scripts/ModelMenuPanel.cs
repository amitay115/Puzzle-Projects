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
        if (!content) return 200f;
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        // 56 לערך לגובה Header+Padding, אפשר לכוון
        return 56f + content.rect.height + 12f;
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
