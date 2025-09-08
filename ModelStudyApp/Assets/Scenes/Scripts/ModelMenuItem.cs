using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ModelMenuItem : MonoBehaviour
{
    public Button button;
    public TextMeshProUGUI label;
    public Image background;

    [Header("Colors")]
    public Color normal = new Color(1,1,1,0.08f);
    public Color highlighted = new Color(1,1,1,0.15f);
    public Color selected = new Color(0.10f, 0.35f, 1f, 0.9f); // כחול BF

    public void SetText(string t)
    {
        if (label) label.text = t;
    }

    public void SetSelected(bool on)
    {
        if (!background) return;
        background.color = on ? selected : normal;
    }

    public void SetHighlighted(bool on)
    {
        if (!background) return;
        if (background.color == selected) return;
        background.color = on ? highlighted : normal;
    }
}
