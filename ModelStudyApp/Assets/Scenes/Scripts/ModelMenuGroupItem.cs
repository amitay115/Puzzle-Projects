using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class ModelMenuGroupItem : MonoBehaviour,
    IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler
{
    // נקרא ע"י ModelMenuPanel כדי לבצע בחירה + פתיחה באותה לחיצה
    public System.Action onHeaderPointerDown;

    [Header("Hookup (Row prefab)")]
    public Button headerButton;                 // ה-Button שיושב על Row
    public Image headerBackground;              // ה-Image של Row (הרקע המעוצב)
    public TextMeshProUGUI titleLabel;         // Row/Label
    public TextMeshProUGUI arrowLabel;         // Row/Arrow  (טקסט "▸/▾")
    public RectTransform childrenContainer;    // ChildrenContainer (VLG + CSF)

    [Header("Visuals")]
    public Color normalBg  = new Color(1,1,1, 0.08f);
    public Color selectedBg= new Color(0.10f, 0.35f, 1f, 0.9f);
    public Color hoverBg   = new Color(1,1,1, 0.15f);

    bool _expanded = false;
    bool _selected = false;

    void Reset()
    {
        // התאמה לשמות בפריפאב: Row/Label, Row/Arrow, ChildrenContainer
        var row = transform.Find("Row");
        if (!headerButton && row)        headerButton = row.GetComponent<Button>();
        if (!headerBackground && row)    headerBackground = row.GetComponent<Image>();
        if (!titleLabel && row)          titleLabel = row.Find("Label")?.GetComponent<TextMeshProUGUI>();
        if (!arrowLabel && row)          arrowLabel = row.Find("Arrow")?.GetComponent<TextMeshProUGUI>();
        if (!childrenContainer)          childrenContainer = transform.Find("ChildrenContainer") as RectTransform;
    }

    void Awake()
    {
        if (childrenContainer) childrenContainer.gameObject.SetActive(_expanded);
        UpdateArrow();
        ApplyBg();
    }

    // ===== API שהפאנל משתמש בו =====
    public void SetTitle(string s)
    {
        if (titleLabel) titleLabel.text = s;
    }

    public void SetSelected(bool on)
    {
        _selected = on;
        ApplyBg();
    }

    public void ToggleExpanded() => SetExpanded(!_expanded);

    public void SetExpanded(bool on)
    {
        _expanded = on;
        if (childrenContainer) childrenContainer.gameObject.SetActive(_expanded);
        UpdateArrow();
    }

    // ===== אירועי עכבר (מאפשרים לחיצה/הובר גם אם לא חיברת OnClick ידנית) =====
    public void OnPointerDown(PointerEventData eventData)
    {
        onHeaderPointerDown?.Invoke();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_selected) { ApplyBg(); return; }
        if (headerBackground) headerBackground.color = hoverBg;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_selected) { ApplyBg(); return; }
        if (headerBackground) headerBackground.color = normalBg;
    }

    // ===== עזרים =====
    void UpdateArrow()
    {
        if (arrowLabel) arrowLabel.text = _expanded ? "▾" : "▸";
    }

    void ApplyBg()
    {
        if (!headerBackground) return;
        headerBackground.color = _selected ? selectedBg : normalBg;
    }
}