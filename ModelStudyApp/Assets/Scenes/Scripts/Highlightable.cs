using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class Highlightable : MonoBehaviour
{
    [Header("Menu Hierarchy Tags")]
    public string menuId;
    public string menuLabel;
    public int menuLevel = 0;
    public string parentMenuId;

    [Header("Outline")]
    public bool useOutline = true;
    [Tooltip("Material משיידר ה-Outline שלך (Custom/URP_OutlineOnly או Custom/UnlitOutlineURP)")]
    public Material outlineMaterial;
    [Min(1f)] public float outlineShellScale = 1.0f;

    [Header("Smoothing")]
    [Tooltip("מחליק נורמלים של מעטפת לפי מיקום קודקוד (180°). משמיד 'פירורי פנים'.")]
    public bool smoothOutlineNormals = true;
    [Tooltip("Tolerance לקיבוץ קודקודים לפי מיקום (ביח' עולם). 1e-4 לרוב מספיק.")]
    public float smoothPositionEpsilon = 1e-4f;

    [Header("Hover Style")]
    public Color hoverColor = new(0f, 1f, 1f, 1f);
    public float hoverThickness = 0.006f;     // world units
    [Range(0f,1f)] public float hoverAlpha = 1f;
    public float hoverZBias = -1.5f;

    [Header("Selected Style")]
    public Color selectedColor = new(1f, 0.65f, 0f, 1f);
    public float selectedThickness = 0.008f;
    [Range(0f,1f)] public float selectedAlpha = 1f;
    public float selectedZBias = -1.5f;

    [Header("Optional: Scale Pop")]
    [Min(1f)] public float hoverScale = 1f;
    public float scaleInLerp = 12f;
    public float scaleOutLerp = 10f;

    [Header("Isolate bundle")]
    public bool isolateIncludeSelf = true;
    public List<Transform> isolateAlsoTransforms = new();

    // --- מצב פנימי ---
    bool _hover, _selected;
    Vector3 _baseScale;
    float _curScale = 1f, _goalScale = 1f;

    struct Pair { public GameObject depth; public GameObject outline; }
    readonly List<Pair> _clones = new();

    MaterialPropertyBlock _mpb;
    bool _built;

    // IDs תואמים לשיידר
    static readonly int _OutlineColorID = Shader.PropertyToID("_OutlineColor");
    static readonly int _OutlineThickID = Shader.PropertyToID("_ThicknessWorld"); // חשוב!
    static readonly int _AlphaID        = Shader.PropertyToID("_Alpha");
    static readonly int _ZBiasID        = Shader.PropertyToID("_ZBias");

    // URP DepthOnly
    static Material _depthOnlyMat;

    // Cache לרשת חלקה (180°) לכל Mesh מקור
    static readonly Dictionary<Mesh, Mesh> _smoothMeshCache = new();

    void Awake()
    {
        _baseScale = transform.localScale;
        _mpb = new MaterialPropertyBlock();

        if (_depthOnlyMat == null)
        {
            var sh = Shader.Find("Hidden/Universal Render Pipeline/DepthOnly");
            if (sh != null)
            {
                _depthOnlyMat = new Material(sh) { renderQueue = 2450 }; // לפני Opaque/Outline
            }
        }
    }

    void Update()
    {
        float lerp = (_hover || _selected) ? scaleInLerp : scaleOutLerp;
        _curScale = Mathf.Lerp(_curScale, _goalScale, 1f - Mathf.Exp(-lerp * Time.unscaledDeltaTime));
        transform.localScale = _baseScale * _curScale;
    }

    public void SetHover(bool on)    { if (_hover    != on) { _hover = on;    Refresh(); } }
    public void SetSelected(bool on) { if (_selected != on) { _selected = on; Refresh(); } }
    public bool IsSelected => _selected;

    void Refresh()
    {
        bool active = (_hover || _selected) && useOutline && outlineMaterial != null;

        _goalScale = active ? Mathf.Max(1f, hoverScale) : 1f;

        if (!active) { SetPairsActive(false); return; }
        if (!_built) BuildPairs();

        // Selected > Hover
        Color col; float thick; float a; float zb;
        if (_selected) { col = selectedColor; thick = selectedThickness; a = selectedAlpha; zb = selectedZBias; }
        else           { col = hoverColor;    thick = hoverThickness;  a = hoverAlpha;    zb = hoverZBias; }

        foreach (var p in _clones)
        {
            if (!p.outline) continue;
            var r = p.outline.GetComponent<MeshRenderer>();
            if (!r) continue;

            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(_OutlineColorID, col);
            _mpb.SetFloat(_OutlineThickID, Mathf.Max(0f, thick));
            _mpb.SetFloat(_AlphaID, Mathf.Clamp01(a));
            _mpb.SetFloat(_ZBiasID, zb);
            r.SetPropertyBlock(_mpb);
        }

        SetPairsActive(true);
    }

    void BuildPairs()
    {
        var renderers = GetComponentsInChildren<MeshRenderer>(includeInactive: true);
        foreach (var r in renderers)
        {
            var mf = r.GetComponent<MeshFilter>();
            if (!mf || !mf.sharedMesh) continue;

            // 1) DepthOnly – ליציבות עם חומרים שקופים
            GameObject depthGO = null;
            if (_depthOnlyMat != null)
            {
                depthGO = new GameObject(r.gameObject.name + ".__DepthOnly");
                depthGO.transform.SetParent(r.transform, false);

                var mfDepth = depthGO.AddComponent<MeshFilter>();
                mfDepth.sharedMesh = mf.sharedMesh;

                var mrDepth = depthGO.AddComponent<MeshRenderer>();
                mrDepth.material = _depthOnlyMat;
                mrDepth.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mrDepth.receiveShadows = false;
                mrDepth.allowOcclusionWhenDynamic = false;

                depthGO.SetActive(false);
            }

            // 2) Outline – משתמשים ברשת משוכפלת עם נורמלים מוחלקים 180°
            var outlineGO = new GameObject(r.gameObject.name + ".__Outline");
            outlineGO.transform.SetParent(r.transform, false);
            outlineGO.transform.localScale = Vector3.one * outlineShellScale;

            var mfOutline = outlineGO.AddComponent<MeshFilter>();
            mfOutline.sharedMesh = smoothOutlineNormals
                ? GetSmoothMesh180(mf.sharedMesh, smoothPositionEpsilon)
                : mf.sharedMesh;

            var mrOutline = outlineGO.AddComponent<MeshRenderer>();
            mrOutline.material = new Material(outlineMaterial); // אינסטנס פר Renderer
            mrOutline.material.renderQueue =
                (int)UnityEngine.Rendering.RenderQueue.Transparent + 10; // ~3010

            mrOutline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mrOutline.receiveShadows = false;
            mrOutline.allowOcclusionWhenDynamic = false;

            outlineGO.SetActive(false);

            _clones.Add(new Pair { depth = depthGO, outline = outlineGO });
        }
        _built = true;
    }

    // === רשת חלקה 180°: מאחד נורמלים לפי מיקום קודקוד (מתעלם מתפרי UV) ===
    static Mesh GetSmoothMesh180(Mesh src, float posEps)
    {
        if (!src) return null;
        if (_smoothMeshCache.TryGetValue(src, out var cached)) return cached;

        var dup = Object.Instantiate(src);
        var verts = dup.vertices;
        var tris  = dup.triangles;

        var accum = new Vector3[verts.Length];   // סכום נורמלים לכל קודקוד
        var counts = new int[verts.Length];

        // חישוב נורמל משולש והוספה לקודקודיו (משוקלל לפי שטח)
        for (int t = 0; t < tris.Length; t += 3)
        {
            int i0 = tris[t], i1 = tris[t+1], i2 = tris[t+2];
            Vector3 v0 = verts[i0], v1 = verts[i1], v2 = verts[i2];
            Vector3 n = Vector3.Cross(v1 - v0, v2 - v0);
            float area2 = n.magnitude; // פרופורציונלי לשטח
            if (area2 > 1e-12f) n /= area2;      // נורמליזציה למניעת גדילה מוגזמת
            accum[i0] += n; counts[i0]++;
            accum[i1] += n; counts[i1]++;
            accum[i2] += n; counts[i2]++;
        }

        // קיבוץ לפי מיקום (כדי לאחד תפרי UV / דופליקטים)
        var groups = new Dictionary<Vector3Int, List<int>>(verts.Length);
        float inv = 1f / Mathf.Max(1e-9f, posEps);

        for (int i = 0; i < verts.Length; i++)
        {
            var v = verts[i];
            var q = new Vector3Int(
                Mathf.RoundToInt(v.x * inv),
                Mathf.RoundToInt(v.y * inv),
                Mathf.RoundToInt(v.z * inv)
            );
            if (!groups.TryGetValue(q, out var list))
            {
                list = new List<int>(4);
                groups.Add(q, list);
            }
            list.Add(i);
        }

        var newNormals = new Vector3[verts.Length];

        foreach (var kv in groups)
        {
            var idxs = kv.Value;
            Vector3 sum = Vector3.zero;
            for (int k = 0; k < idxs.Count; k++) sum += accum[idxs[k]];
            sum.Normalize();
            for (int k = 0; k < idxs.Count; k++) newNormals[idxs[k]] = sum;
        }

        dup.normals = newNormals;
        // טנג'נטים לא נחוצים לאוטליין; אם צריך:
        // try { dup.RecalculateTangents(); } catch {}

        _smoothMeshCache[src] = dup;
        return dup;
    }

    void SetPairsActive(bool on)
    {
        foreach (var p in _clones)
        {
            if (p.depth)   p.depth.SetActive(on);
            if (p.outline) p.outline.SetActive(on);
        }
    }

    void OnDisable()
    {
        _hover = _selected = false;
        transform.localScale = _baseScale;
        SetPairsActive(false);
    }
}