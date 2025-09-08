using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UISelectionPanel : MonoBehaviour
{
    // ===== Refs =====
    [Header("Refs")]
    [Tooltip("גרור את HoverHighlightManager שעל המצלמה")]
    [SerializeField] public HoverHighlightManager manager;
    [SerializeField] private Transform modelRoot;      // שורש המודל המקורי (למשל spyderMR_example)
    public OrbitCameraRig orbitRig;

    [Header("UI")]
    public TMP_Text selectedNameText;
    public Button btnFull;
    public Button btnTransparent;
    public Button btnIsolate;



    // ===== Transparency =====
    
    [SerializeField] bool keepOutlineOnTransparent = true;   // להשאיר מסגרת גם כששקוף
    [SerializeField] bool writeDepthOnTransparent = true;    // המודל השקוף יכתוב עומק (ZWrite=1)

    [Header("Transparency")]
    [Range(0f, 1f)] public float transparentAlpha = 0.15f;

    [Tooltip("ילד אווטליין שנוצר ע\"י ה-Highlightable")]
    [SerializeField] string outlineChildSuffix = ".__Outline";
    [Tooltip("מחרוזת לזיהוי שיידר אווטליין (לסינון במצב Transparent)")]
    [SerializeField] string outlineShaderNameContains = "UnlitOutlineURP";

    // ===== Isolate / Placement =====
    [Header("Isolate Placement")]
    public bool snapAboveFloor = true;
    public float floorY = 0f;
    public float placePadding = 0.02f;

    // ===== Internal state =====
    Highlightable _currentSel;          // צילום מצב הנבחר
    Transform _originalRoot;        // שמירת שורש המודל המקורי
    bool _isolated;
    GameObject _isolatedClone;
    Transform _isoPivot;

    Transform _originalOrbitTarget;
    Vector3 _originalOrbitTargetOffset;
    bool _orbitOriginalSaved;

    // קאש שקיפות
    class FadeCache
    {
        public Renderer r;
        public Material[] originalShared;
        public Material[] transparentInstanced;
        public bool isTransparent;
    }
    readonly Dictionary<Renderer, FadeCache> _fadeMap = new();

    // ===== Lifecycle =====
    void OnEnable()
    {
        if (!manager)
        {
            var cam = Camera.main;
            if (cam) manager = cam.GetComponent<HoverHighlightManager>();
            if (!manager) manager = FindObjectOfType<HoverHighlightManager>(true);
        }

        if (manager)
        {
            manager.OnSelectionChanged += OnSelectionChanged;
            _currentSel = manager.GetSelected();
        }

        if (!_originalRoot) _originalRoot = modelRoot;

        // שמירות מצלמה
        if (orbitRig && !_orbitOriginalSaved)
        {
            _originalOrbitTarget = orbitRig.target;
            _originalOrbitTargetOffset = orbitRig.targetOffset;
            _orbitOriginalSaved = true;
        }

        // חיבור מאזינים לכפתורים (בלי כפילויות)
        if (btnFull)
        {
            btnFull.onClick.RemoveAllListeners();
            btnFull.onClick.AddListener(OnFullClicked);
        }
        if (btnTransparent)
        {
            btnTransparent.onClick.RemoveAllListeners();
            btnTransparent.onClick.AddListener(OnTransparentClicked);
        }
        if (btnIsolate)
        {
            btnIsolate.onClick.RemoveAllListeners();
            btnIsolate.onClick.AddListener(OnIsolateClicked);
        }

        RefreshUI(_currentSel);
    }

    void OnDisable()
    {
        if (manager) manager.OnSelectionChanged -= OnSelectionChanged;
    }

    void Update() // גיבוי: אם משום מה האירוע לא הגיע
    {
        if (!manager) return;
        var sel = manager.GetSelected();
        if (sel != _currentSel)
        {
            _currentSel = sel;
            RefreshUI(sel);
        }
    }

    // ===== Selection sync =====
    void OnSelectionChanged(Highlightable h)
    {
        _currentSel = h;
        RefreshUI(h);
    }

    void RefreshUI(Highlightable h)
    {
        bool hasSel = h != null;
        if (selectedNameText) selectedNameText.text = hasSel ? h.name : "(no selection)";
        if (btnTransparent) btnTransparent.interactable = hasSel;
        if (btnIsolate) btnIsolate.interactable = hasSel;
        // Full תמיד פעיל
    }

    // ===== FULL =====
    public void OnFullClicked()
    {
        // בטל שקיפויות שיצרנו
        RestoreAllTransparency();

        // בטל בידוד אם פעיל
        if (_isolated)
        {
            if (_isolatedClone) Destroy(_isolatedClone);
            _isolatedClone = null;
            _isoPivot = null;

            if (_originalRoot) _originalRoot.gameObject.SetActive(true);
            modelRoot = _originalRoot;

            if (orbitRig && _orbitOriginalSaved)
            {
                orbitRig.target = _originalOrbitTarget;
                orbitRig.targetOffset = _originalOrbitTargetOffset;
                orbitRig.ResetView();
            }
            _isolated = false;
        }

        // נקה בחירה ואפס UI
        manager?.ClearSelection();
        RefreshUI(null);
        //SetOutlinesActive(_originalRoot, true);   // ודא שההילות חוזרות אחרי Full
    }

    // ===== TRANSPARENT =====
    void OnTransparentClicked()
    {
        var sel = manager?.GetSelected();
        if (!sel) return;

        var all = sel.GetComponentsInChildren<Renderer>(includeInactive: true);
        int touched = 0;
        foreach (var r in all)
        {
            if (!IsRenderableCandidate(r)) continue;
            if (IsOutlineRenderer(r)) continue; // לא נוגעים בחומרי ה-outline עצמם
            if (!TryToggleRendererTransparency(r, transparentAlpha)) continue;
            touched++;
        }

        // האם להציג מסגרת גם כשהמודל שקוף?
        SetOutlineEnabled(sel.transform, keepOutlineOnTransparent);
        // Debug.Log($"[Transparent] changed={touched}");
    }

    bool IsRenderableCandidate(Renderer r)
    {
        if (!r || !r.enabled) return false;
        if (r is ParticleSystemRenderer) return false;
        if (r is TrailRenderer) return false;
        if (r is LineRenderer) return false;
        return true;
    }
    void SetOutlineEnabled(Transform root, bool enable)
    {
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            if (IsOutlineRenderer(r))
                r.enabled = enable;
    }

    bool IsOutlineRenderer(Renderer r)
    {
        if (!string.IsNullOrEmpty(outlineChildSuffix) && r.gameObject.name.EndsWith(outlineChildSuffix))
            return true;

        var mats = r.sharedMaterials;
        if (mats != null)
        {
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (!m) continue;
                var s = m.shader ? m.shader.name : "";
                if (!string.IsNullOrEmpty(outlineShaderNameContains) && s.Contains(outlineShaderNameContains))
                    return true;
            }
        }
        return false;
    }

    bool TryToggleRendererTransparency(Renderer r, float alpha)
    {
        // אם יש קאש — החלפה הלוך/חזור
        if (_fadeMap.TryGetValue(r, out var cache))
        {
            cache.isTransparent = !cache.isTransparent;
            if (cache.isTransparent) r.materials = cache.transparentInstanced;
            else r.sharedMaterials = cache.originalShared;
            return true;
        }

        var shared = r.sharedMaterials;
        if (shared == null || shared.Length == 0) return false;

        var instanced = r.materials; // יוצר עותקים
        bool changedAny = false;

        for (int i = 0; i < instanced.Length; i++)
        {
            var m = instanced[i];
            if (!m) continue;

            // דלג על חומרים חשודים כאווטליין
            var shaderName = m.shader ? m.shader.name : "";
            if (!string.IsNullOrEmpty(outlineShaderNameContains) && shaderName.Contains(outlineShaderNameContains))
                continue;

            if (MakeURPLitTransparentSafe(m, transparentAlpha, writeDepthOnTransparent))
                changedAny = true;
        }

        if (!changedAny)
        {
            // לא שינינו כלום—אל תשאיר instanced
            r.sharedMaterials = shared;
            return false;
        }

        // שמור קאש והחל שקיפות
        _fadeMap[r] = new FadeCache
        {
            r = r,
            originalShared = shared,
            transparentInstanced = instanced,
            isTransparent = true
        };
        r.materials = instanced;
        return true;
    }

    bool MakeURPLitTransparentSafe(Material m, float alpha, bool forceZWrite)
    {
        bool hasSurface = m.HasProperty("_Surface");
        bool hasBase    = m.HasProperty("_BaseColor");
        bool hasColor   = m.HasProperty("_Color");
        if (!hasSurface && !hasBase && !hasColor) return false;

        if (hasSurface) m.SetFloat("_Surface", 1f); // Transparent
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

        // הטריק: גם בשקיפות משאירים כתיבת עומק כדי שה-outline (עם ZTest) לא ימלא את פני השטח
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", forceZWrite ? 1f : 0f);

        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        if (hasBase) { var c = m.GetColor("_BaseColor"); c.a = Mathf.Clamp01(alpha); m.SetColor("_BaseColor", c); }
        else if (hasColor) { var c = m.GetColor("_Color"); c.a = Mathf.Clamp01(alpha); m.SetColor("_Color", c); }

        return true;
    }

    void RestoreAllTransparency()
    {
        foreach (var kv in _fadeMap)
        {
            var r = kv.Key; var cache = kv.Value;
            if (!r) continue;

            // החזר למקורי
            try { r.sharedMaterials = cache.originalShared; } catch { }

            // נקה עותקים
            if (cache.transparentInstanced != null)
            {
                for (int i = 0; i < cache.transparentInstanced.Length; i++)
                {
                    var m = cache.transparentInstanced[i];
                    if (m) Destroy(m);
                }
            }
        }
        _fadeMap.Clear();
    }

    // ===== ISOLATE =====
    public void OnIsolateClicked()
    {
        var sel = _currentSel ?? (manager ? manager.GetSelected() : null);
        if (!sel || _isolated) return;

        var src = sel.transform;

        // ודא שיש מה לרנדר
        var srcRenderers = src.GetComponentsInChildren<Renderer>(true);
        if (srcRenderers.Length == 0)
        {
            Debug.LogError($"[Isolate] '{src.name}' לא מכיל Renderer");
            return;
        }

        // כבה מודל מקורי
        if (_originalRoot) _originalRoot.gameObject.SetActive(false);

        // קונטיינר בעולם (שומר world-scale של ההורה)
        Transform p = src.parent;
        var container = new GameObject(src.name + "__IsolatedRoot").transform;
        if (p == null)
        {
            container.position = Vector3.zero;
            container.rotation = Quaternion.identity;
            container.localScale = Vector3.one;
        }
        else
        {
            container.position = p.position;
            container.rotation = p.rotation;
            container.localScale = p.lossyScale;
        }

        // Pivot
        _isoPivot = new GameObject(src.name + "_Pivot").transform;
        _isoPivot.SetParent(container, false);
        _isoPivot.localPosition = Vector3.zero;
        _isoPivot.localRotation = Quaternion.identity;

        // שכפול הענף
        _isolatedClone = Instantiate(src.gameObject);
        var cloneT = _isolatedClone.transform;
        cloneT.SetParent(container, worldPositionStays: false);
        cloneT.localPosition = src.localPosition;
        cloneT.localRotation = src.localRotation;
        cloneT.localScale = src.localScale;

        // נקה אווטליין ושקיפויות, ודא רנדרים גלויים ואטומים
        StripOutlineAndHighlightable(container);
        EnsureRenderersVisibleOpaque(container);
        SetLayerRecursively(container, LayerMask.NameToLayer("Default"));
        container.gameObject.SetActive(true);

        // מרכז לאפס והרם מעל הרצפה
        bool hasR; var b = ComputeWorldBounds(container, out hasR);
        if (hasR)
        {
            container.position -= b.center;

            if (snapAboveFloor)
            {
                b = ComputeWorldBounds(container, out hasR);
                float lift = (floorY + placePadding) - b.min.y;
                if (lift > 0f) container.position += new Vector3(0f, lift, 0f);
            }
            SetIsoPivotToBoundsCenter(container);
        }

        // כוון מצלמה
        modelRoot = container;
        if (orbitRig)
        {
            orbitRig.target = _isoPivot;
            orbitRig.targetOffset = Vector3.zero;
            orbitRig.ResetView();
        }
        else
        {
            PlaceCameraToSee(container);
        }

        // בטל בחירה (שלא יחזור השיידר)
        manager?.ClearSelection();

        _isolated = true;
        RefreshUI(null);
    }

    // ===== Helpers (renderers/materials/bounds/camera) =====
    static void StripOutlineAndHighlightable(Transform root)
    {
        var toDelete = new List<GameObject>();
        var rends = root.GetComponentsInChildren<MeshRenderer>(true);
        foreach (var r in rends)
            if (r.gameObject.name.EndsWith(".__Outline"))
                toDelete.Add(r.gameObject);
        for (int i = 0; i < toDelete.Count; i++)
            Destroy(toDelete[i]);

        var highs = root.GetComponentsInChildren<Highlightable>(true);
        for (int i = 0; i < highs.Length; i++)
            Destroy(highs[i]);
    }

    static void EnsureRenderersVisibleOpaque(Transform root)
    {
        var rrs = root.GetComponentsInChildren<Renderer>(true);
        foreach (var r in rrs)
        {
            if (!r) continue;
            r.enabled = true;

            var mats = r.materials; // instanced
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i]; if (!m) continue;
                if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 0f); // Opaque
                if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 1f);
                m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;

                if (m.HasProperty("_BaseColor"))
                { var c = m.GetColor("_BaseColor"); c.a = 1f; m.SetColor("_BaseColor", c); }
                else if (m.HasProperty("_Color"))
                { var c = m.GetColor("_Color"); c.a = 1f; m.SetColor("_Color", c); }
            }
        }
    }

    static Bounds ComputeWorldBounds(Transform root, out bool hasRenderers)
    {
        var rrs = root.GetComponentsInChildren<Renderer>(true);
        hasRenderers = rrs.Length > 0;
        if (!hasRenderers) return new Bounds(root.position, Vector3.one);

        Bounds b = new Bounds(rrs[0].bounds.center, Vector3.zero);
        for (int i = 0; i < rrs.Length; i++) b.Encapsulate(rrs[i].bounds);
        return b;
    }

    static void SetLayerRecursively(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursively(t.GetChild(i), layer);
    }

    void PlaceCameraToSee(Transform root)
    {
        var cam = Camera.main;
        if (!cam) return;

        bool hasR; var b = ComputeWorldBounds(root, out hasR);
        var center = b.center;
        float radius = Mathf.Max(b.extents.magnitude, 0.5f);

        if (orbitRig && orbitRig.target)
        {
            orbitRig.ResetView();
            var pos = center - cam.transform.forward * (radius * 2.5f);
            cam.transform.position = pos;
            cam.transform.rotation = Quaternion.LookRotation(center - pos, Vector3.up);
        }
        else
        {
            var pos = center + new Vector3(0, 0, -radius * 2.5f);
            cam.transform.position = pos;
            cam.transform.rotation = Quaternion.LookRotation(center - pos, Vector3.up);
        }
    }

    void SetIsoPivotToBoundsCenter(Transform container)
    {
        if (_isoPivot == null || container == null) return;
        bool hasR; var b = ComputeWorldBounds(container, out hasR);
        if (!hasR) return;

        _isoPivot.position = b.center;
        _isoPivot.rotation = Quaternion.identity;
    }

    // בודק אם מתחת ל-root יש לפחות Renderer אחד ששקוף כרגע (לפי ה-fade cache)
    bool IsAnyTransparentUnder(Transform root)
    {
        if (!root) return false;
        var rends = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rends.Length; i++)
        {
            var r = rends[i];
            if (!r) continue;
            if (_fadeMap.TryGetValue(r, out var cache) && cache.isTransparent)
                return true;
        }
        return false;
    }
    
    
    // מדליק/מכבה את כל רנדררי האווטליין (הילדים שה-Highlightable יוצר)
    void SetOutlinesActive(Transform root, bool active)
    {
        if (!root) return;

        // לפי שם ילד מיוחד
        if (!string.IsNullOrEmpty(outlineChildSuffix))
        {
            var trs = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < trs.Length; i++)
            {
                var t = trs[i];
                if (t.gameObject.name.EndsWith(outlineChildSuffix))
                    t.gameObject.SetActive(active);
            }
        }

        // בנוסף: אם במקרה יש Outline כשיידר על אותו Renderer (לא רק ילד נפרד)
        var rends = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rends.Length; i++)
        {
            var r = rends[i];
            if (!r) continue;
            var mats = r.sharedMaterials;
            if (mats == null) continue;
            bool looksLikeOutline = false;
            for (int m = 0; m < mats.Length; m++)
            {
                var mm = mats[m];
                if (!mm) continue;
                var s = mm.shader ? mm.shader.name : "";
                if (!string.IsNullOrEmpty(outlineShaderNameContains) && s.Contains(outlineShaderNameContains))
                {
                    looksLikeOutline = true; break;
                }
            }
            if (looksLikeOutline) r.enabled = active;
        }
    }
}