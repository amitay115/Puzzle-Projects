
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class Highlightable : MonoBehaviour
{
    [Header("Menu Hierarchy Tags")]
    [Tooltip("מזהה ייחודי לאובייקט בתפריט")]
    public string menuId;

    [Tooltip("הטקסט שיוצג בתפריט")]
    public string menuLabel;

    [Tooltip("הרמה בתפריט (0=ראשי, 1=ילד, 2=נכד וכו')")]
    public int menuLevel = 0;

    [Tooltip("מזהה ההורה בתפריט (אם ריק – זה פריט עליון)")]
    public string parentMenuId;
    
    [Header("Outline")]
    public bool useOutline = true;
    [Tooltip("מטרייל משיידר Custom/UnlitOutlineURP")]
    public Material outlineMaterial;
    [Tooltip("להשאיר 1.0 כשמשתמשים בשיידר שמרחיב וריטקסים")]
    [Min(1f)] public float outlineShellScale = 1.0f;

    [Header("Hover Style")]
    public Color hoverColor = new(0f, 1f, 1f, 1f);
    public float hoverThickness = 0.006f;
    [Range(0f,1f)] public float hoverAlpha = 1f;
    public float hoverZBias = -1.5f;

    [Header("Selected Style")]
    public Color selectedColor = new(1f, 0.65f, 0f, 1f);
    public float selectedThickness = 0.008f;
    [Range(0f,1f)] public float selectedAlpha = 1f;
    public float selectedZBias = -1.5f;

    [Header("Optional: Scale Pop")]
    [Min(1f)] public float hoverScale = 1f;      // השאר 1 לביטול קפיצה
    public float scaleInLerp = 12f;
    public float scaleOutLerp = 10f;

    // --- מצב פנימי ---
    bool _hover, _selected;
    Vector3 _baseScale;
    float _curScale = 1f, _goalScale = 1f;
    readonly List<GameObject> _outlineClones = new();
    MaterialPropertyBlock _mpb;
    bool _built;

    void Awake()
    {
        _baseScale = transform.localScale;
        _mpb = new MaterialPropertyBlock();
    }

    void Update()
    {
        float lerp = (_hover || _selected) ? scaleInLerp : scaleOutLerp;
        _curScale = Mathf.Lerp(_curScale, _goalScale, 1f - Mathf.Exp(-lerp * Time.unscaledDeltaTime));
        transform.localScale = _baseScale * _curScale;
    }

    public void SetHover(bool on)
    {
        if (_hover == on) return;
        _hover = on;
        Refresh();
    }

    public void SetSelected(bool on)
    {
        if (_selected == on) return;
        _selected = on;
        Refresh();
    }

    public bool IsSelected => _selected;

    void Refresh()
    {
        bool active = (_hover || _selected) && useOutline && outlineMaterial != null;

        _goalScale = active ? Mathf.Max(1f, hoverScale) : 1f;

        if (!active)
        {
            SetClonesActive(false);
            return;
        }

        if (!_built) BuildClones();

        // סטייל: Selected גובר על Hover
        Color col; float thick; float a; float zb;
        if (_selected)
        {
            col = selectedColor; thick = selectedThickness; a = selectedAlpha; zb = selectedZBias;
        }
        else
        {
            col = hoverColor; thick = hoverThickness; a = hoverAlpha; zb = hoverZBias;
        }

        for (int i = 0; i < _outlineClones.Count; i++)
        {
            var shell = _outlineClones[i];
            if (!shell) continue;
            var r = shell.GetComponent<MeshRenderer>();
            if (!r) continue;

            r.GetPropertyBlock(_mpb);
            _mpb.SetColor("_OutlineColor", col);
            _mpb.SetFloat("_OutlineThickness", Mathf.Max(0f, thick));
            _mpb.SetFloat("_Alpha", Mathf.Clamp01(a));
            _mpb.SetFloat("_ZBias", zb);
            r.SetPropertyBlock(_mpb);
        }

        SetClonesActive(true);
    }

    void BuildClones()
    {
        var renderers = GetComponentsInChildren<MeshRenderer>(includeInactive: true);
        foreach (var r in renderers)
        {
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            var shell = new GameObject(r.gameObject.name + ".__Outline");
            shell.transform.SetParent(r.transform, false);
            shell.transform.localPosition = Vector3.zero;
            shell.transform.localRotation = Quaternion.identity;
            shell.transform.localScale = Vector3.one * outlineShellScale; // 1.0 עם השיידר שלנו

            var smf = shell.AddComponent<MeshFilter>();
            smf.sharedMesh = mf.sharedMesh;

            var srd = shell.AddComponent<MeshRenderer>();
            srd.sharedMaterial = outlineMaterial;
            srd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            srd.receiveShadows = false;
            srd.allowOcclusionWhenDynamic = false;

            shell.SetActive(false);
            _outlineClones.Add(shell);
        }
        _built = true;
    }

    void SetClonesActive(bool on)
    {
        for (int i = 0; i < _outlineClones.Count; i++)
        {
            var go = _outlineClones[i];
            if (go) go.SetActive(on);
        }
    }

    void OnDisable()
    {
        _hover = _selected = false;
        transform.localScale = _baseScale;
        SetClonesActive(false);
    }
}
