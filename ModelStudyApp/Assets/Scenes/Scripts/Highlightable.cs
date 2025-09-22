using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class Highlightable : MonoBehaviour
{
    // ---------- NEW: רישום גלובלי ----------
    static readonly HashSet<Highlightable> s_all = new();
    void OnEnable()  { s_all.Add(this); }
    void OnDestroy() { s_all.Remove(this); }

    /// <summary>ניקוי כל מצבי ה-hover בכל הסצנה.</summary>
    public static void ClearAllHovers()
    {
        foreach (var h in s_all) { if (h) h.SetHover(false); }
    }
    /// <summary>ניקוי כל מצבי ה-selected בכל הסצנה.</summary>
    public static void ClearAllSelected()
    {
        foreach (var h in s_all) { if (h) h.SetSelected(false); }
    }
    /// <summary>כיבוי מוחלט: גם hover וגם selected וגם כיבוי אובייקטי outline.</summary>
    public static void ClearAllOutlinesAndFlags()
    {
        foreach (var h in s_all)
        {
            if (!h) continue;
            h._hover = h._selected = false;
            h.SetPairsActive(false);
        }
    }

    // ---------- השדות שלך (השארתי כמו אצלך) ----------
    [Header("Menu Hierarchy Tags")]
    public string menuId;
    public string menuLabel;
    public int menuLevel = 0;
    public string parentMenuId;

    [Header("Outline")]
    public bool useOutline = true;
    public Material outlineMaterial;
    [Min(1f)] public float outlineShellScale = 1.0f;

    [Header("Smoothing")]
    public bool smoothOutlineNormals = true;
    public float smoothPositionEpsilon = 1e-4f;

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

    // ---------- NEW: חסם אווטליין ----------
    // כשtrue – גם אם יש hover/selected לא נדליק את המעטפת.
    bool _outlineBlocked;

    // מאפשר ל-UI לכפות חסימה/שחרור של האווטליין (למשל בשקיפות)
    public void SetOutlineBlocked(bool blocked)
    {
        _outlineBlocked = blocked;
        if (blocked) SetPairsActive(false);
        else Refresh(); // יפעיל לפי ה-hover/selected הקיימים
    }

    // IDs
    static readonly int _OutlineColorID = Shader.PropertyToID("_OutlineColor");
    static readonly int _OutlineThickID = Shader.PropertyToID("_ThicknessWorld");
    static readonly int _AlphaID        = Shader.PropertyToID("_Alpha");

    // URP DepthOnly
    static Material _depthOnlyMat;

    // Cache לרשת חלקה
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
                _depthOnlyMat = new Material(sh) { renderQueue = 2450 };
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
        bool wantsOutline = (_hover || _selected);
        bool active = wantsOutline && useOutline && outlineMaterial != null && !_outlineBlocked;

        _goalScale = wantsOutline ? Mathf.Max(1f, hoverScale) : 1f;

        if (!active) { SetPairsActive(false); return; }
        if (!_built) BuildPairs();

        // Selected > Hover
        Color col; float thick; float a;
        if (_selected) { col = selectedColor; thick = selectedThickness; a = selectedAlpha; }
        else           { col = hoverColor;    thick = hoverThickness;  a = hoverAlpha; }

        foreach (var p in _clones)
        {
            if (!p.outline) continue;
            var r = p.outline.GetComponent<MeshRenderer>();
            if (!r) continue;

            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(_OutlineColorID, col);
            _mpb.SetFloat(_OutlineThickID, Mathf.Max(0f, thick));
            _mpb.SetFloat(_AlphaID, Mathf.Clamp01(a));
            r.SetPropertyBlock(_mpb);
        }

        SetPairsActive(true);
    }

    void BuildPairs()
    {
        if (_built) return;

        // 1) MeshRenderer רגיל
        var meshRenderers = GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < meshRenderers.Length; i++)
        {
            var r = meshRenderers[i];
            if (!r) continue;
            var mf = r.GetComponent<MeshFilter>();
            if (!mf || !mf.sharedMesh) continue;

            GameObject depthGO = null;
            if (_depthOnlyMat != null)
            {
                depthGO = new GameObject(r.gameObject.name + ".__DepthOnly");
                depthGO.layer = r.gameObject.layer;
                depthGO.transform.SetParent(r.transform, false);

                var mfDepth = depthGO.AddComponent<MeshFilter>();
                mfDepth.sharedMesh = mf.sharedMesh;

                var mrDepth = depthGO.AddComponent<MeshRenderer>();
                mrDepth.sharedMaterial = _depthOnlyMat;
                mrDepth.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mrDepth.receiveShadows = false;
                mrDepth.allowOcclusionWhenDynamic = false;

                depthGO.SetActive(false);
            }

            var outlineGO = new GameObject(r.gameObject.name + ".__Outline");
            outlineGO.layer = r.gameObject.layer;
            outlineGO.transform.SetParent(r.transform, false);
            outlineGO.transform.localScale = Vector3.one * outlineShellScale;

            var mfOutline = outlineGO.AddComponent<MeshFilter>();
            mfOutline.sharedMesh = smoothOutlineNormals
                ? GetSmoothMesh180(mf.sharedMesh, smoothPositionEpsilon)
                : mf.sharedMesh;

            var mrOutline = outlineGO.AddComponent<MeshRenderer>();
            if (outlineMaterial != null)
            {
                mrOutline.material = new Material(outlineMaterial);
                mrOutline.material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 10;
            }
            mrOutline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mrOutline.receiveShadows = false;
            mrOutline.allowOcclusionWhenDynamic = false;

            outlineGO.SetActive(false);
            _clones.Add(new Pair { depth = depthGO, outline = outlineGO });
        }

        // 2) SkinnedMeshRenderer (אם יש)
        var skinned = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < skinned.Length; i++)
        {
            var src = skinned[i];
            if (!src || !src.sharedMesh) continue;

            GameObject depthGO = null;
            if (_depthOnlyMat != null)
            {
                depthGO = new GameObject(src.gameObject.name + ".__DepthOnly");
                depthGO.layer = src.gameObject.layer;
                depthGO.transform.SetParent(src.transform, false);

                // DepthOnly אפשר גם כ-Skinned כדי לשמר עצמות
                var sDepth = depthGO.AddComponent<SkinnedMeshRenderer>();
                sDepth.sharedMesh  = src.sharedMesh;
                sDepth.bones       = src.bones;
                sDepth.rootBone    = src.rootBone;
                sDepth.sharedMaterial = _depthOnlyMat;
                sDepth.updateWhenOffscreen = true;
                sDepth.shadowCastingMode   = UnityEngine.Rendering.ShadowCastingMode.Off;
                sDepth.receiveShadows      = false;
                sDepth.allowOcclusionWhenDynamic = false;

                depthGO.SetActive(false);
            }

            var outlineGO = new GameObject(src.gameObject.name + ".__Outline");
            outlineGO.layer = src.gameObject.layer;
            outlineGO.transform.SetParent(src.transform, false);
            outlineGO.transform.localScale = Vector3.one * outlineShellScale;

            var sOutline = outlineGO.AddComponent<SkinnedMeshRenderer>();
            // שים לב: על Skinned לא נוכל להחליק נורמלים בלי Mesh קריא; נשתמש במקור ישר
            sOutline.sharedMesh = (smoothOutlineNormals && src.sharedMesh.isReadable)
                ? GetSmoothMesh180(src.sharedMesh, smoothPositionEpsilon)
                : src.sharedMesh;

            if (outlineMaterial != null)
            {
                sOutline.material = new Material(outlineMaterial);
                sOutline.material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 10;
            }

            sOutline.bones     = src.bones;
            sOutline.rootBone  = src.rootBone;
            sOutline.updateWhenOffscreen = true;
            sOutline.shadowCastingMode   = UnityEngine.Rendering.ShadowCastingMode.Off;
            sOutline.receiveShadows      = false;
            sOutline.allowOcclusionWhenDynamic = false;

            outlineGO.SetActive(false);
            _clones.Add(new Pair { depth = depthGO, outline = outlineGO });
        }

        _built = true;
    }

    static Mesh GetSmoothMesh180(Mesh src, float posEps)
    {
        if (!src) return null;

        // אם mesh לא קריא ב-Build – אל תנסה להעתיק/לקרוא אותו
        if (!src.isReadable)
        {
            // נשתמש ישירות במקור, בלי החלקת נורמלים, ונזהיר פעם אחת בלוג
            #if UNITY_EDITOR
            Debug.LogWarning($"[Highlightable] Mesh '{src.name}' is not Read/Write enabled. " +
                            "Outline will work but without 180° smoothing. " +
                            "To enable: Select the model asset → Model tab → Read/Write = Enabled → Apply.");
            #endif
            return src;
        }

        if (_smoothMeshCache.TryGetValue(src, out var cached)) return cached;

        var dup = Object.Instantiate(src);

        var verts = dup.vertices;
        var tris  = dup.triangles;

        var accum  = new Vector3[verts.Length];
        var counts = new int[verts.Length];

        for (int t = 0; t < tris.Length; t += 3)
        {
            int i0 = tris[t], i1 = tris[t+1], i2 = tris[t+2];
            Vector3 v0 = verts[i0], v1 = verts[i1], v2 = verts[i2];
            Vector3 n = Vector3.Cross(v1 - v0, v2 - v0);
            float area2 = n.magnitude;
            if (area2 > 1e-12f) n /= area2;
            accum[i0] += n; counts[i0]++;
            accum[i1] += n; counts[i1]++;
            accum[i2] += n; counts[i2]++;
        }

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