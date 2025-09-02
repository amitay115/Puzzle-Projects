using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SelectionInfoPanel : MonoBehaviour
{
    [Header("Refs")]
    public HoverHighlightManager selectionManager;   // גרור את המצלמה (עם HoverHighlightManager)
    public RectTransform panel;                      // TopRightInfoPanel RectTransform
    public CanvasGroup canvasGroup;                  // ← חדש: CanvasGroup על אותו אובייקט
    public Button headerButton;
    public TextMeshProUGUI headerLabel;
    public RectTransform content;
    public TMP_InputField descriptionInput;

    [Header("Sizes")]
    public float collapsedHeight = 44f;
    public float expandedMinHeight = 220f;
    public float animTime = 0.15f;

    Highlightable _lastSelected;
    bool _expanded;
    float _animVel;

    void Reset()
    {
        panel = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
    }

    void Awake()
    {
        if (!panel) panel = GetComponent<RectTransform>();
        if (!canvasGroup) canvasGroup = gameObject.GetComponent<CanvasGroup>();
        if (!canvasGroup) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (headerButton) headerButton.onClick.AddListener(ToggleExpand);

        // לא מכבים את האובייקט! רק מסתירים עם CanvasGroup
        Show(false);
        SetHeightInstant(collapsedHeight);
    }

    void Update()
    {
        if (!selectionManager) return;

        var sel = selectionManager.GetSelected();   // מנהל הבחירה שלך
        if (sel != _lastSelected)
        {
            _lastSelected = sel;

            if (sel == null)
            {
                Show(false);
                _expanded = false;
                SetHeightInstant(collapsedHeight);
            }
            else
            {
                Show(true);
                if (headerLabel) headerLabel.text = sel.name;
                _expanded = false;
                SetHeightInstant(collapsedHeight);
            }
        }

        // אנימציית גובה חלקה
        float targetH = _expanded ? Mathf.Max(expandedMinHeight, CalcPreferredHeight()) : collapsedHeight;
        float h = Mathf.SmoothDamp(panel.sizeDelta.y, targetH, ref _animVel, animTime);
        SetHeight(h);
    }

    void ToggleExpand()
    {
        if (_lastSelected == null) return;
        _expanded = !_expanded;
    }

    float CalcPreferredHeight()
    {
        if (!content) return expandedMinHeight;
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        return Mathf.Max(expandedMinHeight, collapsedHeight + content.sizeDelta.y + 16f);
    }

    void SetHeightInstant(float h)
    {
        var sd = panel.sizeDelta;
        sd.y = h;
        panel.sizeDelta = sd;
        if (content) content.gameObject.SetActive(h > collapsedHeight + 1f);
    }

    void SetHeight(float h)
    {
        var sd = panel.sizeDelta;
        sd.y = h;
        panel.sizeDelta = sd;
        if (content) content.gameObject.SetActive(h > collapsedHeight + 1f);
    }

    void Show(bool visible)
    {
        // לא נוגעים ב-SetActive של האובייקט עם הסקריפט!
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }
}
