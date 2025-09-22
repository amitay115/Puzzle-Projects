using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UISelectionPanel : MonoBehaviour
{
    [Header("Panels")]
    public ModelMenuPanel modelMenuPanel;

    // ===== Refs =====
    [Header("Refs")]
    [Tooltip("גרור את HoverHighlightManager שעל המצלמה")]
    [SerializeField] public HoverHighlightManager manager;
    [SerializeField] private Transform modelRoot;      // שורש המודל המקורי
    public OrbitCameraRig orbitRig;

    [Header("UI")]
    public TMP_Text selectedNameText;
    public Button btnFull;
    public Button btnTransparent;
    public Button btnIsolate;

    // ===== Transparency =====
    [Header("Transparency")]
    [Range(0f, 1f)] public float transparentAlpha = 0.15f;
    [Tooltip("להשאיר Outline גם כשהאובייקט שקוף")]
    [SerializeField] bool keepOutlineOnTransparent = true;
    [Tooltip("כששקוף – לכתוב עומק כדי שה-outline לא יכסה את פני השטח")]
    [SerializeField] bool writeDepthOnTransparent = true;

    [Tooltip("ילד outline שנוצר ע\"י ה-Highlightable")]
    [SerializeField] string outlineChildSuffix = ".__Outline";

    [Tooltip("שמות השיידרים של האווטליין כמו שמופיעים בתוך הקבצים (Shader \"...\")")]
    [SerializeField] string[] outlineShaderNames = new[]
    {
        "Custom/URP_OutlineOnly",
        "Custom/UnlitOutlineURP"
    };

    // ===== Isolate / Placement =====
    [Header("Isolate Placement")]
    public bool  snapAboveFloor = true;
    public float floorY = 0f;
    public float placePadding = 0.02f;

    // ===== Isolate Framing =====
    [Header("Isolate Framing")]
    [Tooltip("כמה מגובה המסך האובייקט יתפוס אחרי בידוד (0–1)")]
    [Range(0.2f, 0.95f)] public float isolateScreenHeightFrac = 0.6f;

    [Tooltip("במצב Auto: לבחור תמיד זווית אופקית בלבד (לא מלמעלה/למטה)")]
    public bool autoHorizontalOnly = true;

    [Tooltip("ציר אנכי עולמי (ברירת מחדל: Y)")]
    public Vector3 worldUp = Vector3.up;

    [Tooltip("זמן בלנד למעבר לתצוגה הממוסגרת")]
    public float isolateViewBlend = 0.35f;

    [Tooltip("כיוון ברירת מחדל אם אין ViewHint על האובייקט")]
    public PreferredView defaultPreferredView = PreferredView.AutoBroadside;

    [Tooltip("לאפשר רמז-כיוון על אובייקט/הורה (ViewHint)")]
    public bool allowPerObjectViewHint = true;

    [Tooltip("מרחק מינימלי של המצלמה מהאובייקט בבידוד")]
    public float minIsolationDistance = 0.75f;

    [Tooltip("פקטור בטיחות נוסף על החישוב (1.0–1.2 מומלץ)")]
    [Range(1.0f, 1.5f)] public float distanceSafetyFactor = 1.1f;

    // ===== Internal state =====
    Highlightable _currentSel;
    Transform    _originalRoot;
    bool         _isolated;
    Transform    _isoPivot;

    Transform _originalOrbitTarget;
    Vector3   _originalOrbitTargetOffset;
    bool      _orbitOriginalSaved;

    readonly List<Transform> _isoRoots = new();

    // קאש שקיפות פר Renderer
    class FadeCache
    {
        public Renderer r;
        public Material[] originalShared;
        public Material[] transparentInstanced;
        public bool isTransparent;
    }
    readonly Dictionary<Renderer, FadeCache> _fadeMap = new();

    // ===== Gating colliders =====
    Highlightable _gatedRootHL;
    struct GateRecord { public Collider col; }
    readonly List<GateRecord> _gated = new();

    // אינדקס מהיר: menuId -> Highlightable
    readonly Dictionary<string, Highlightable> _hlByMenuId = new();

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

        if (orbitRig && !_orbitOriginalSaved)
        {
            _originalOrbitTarget       = orbitRig.target;
            _originalOrbitTargetOffset = orbitRig.targetOffset;
            _orbitOriginalSaved        = true;
        }

        if (btnFull)        { btnFull.onClick.RemoveAllListeners();        btnFull.onClick.AddListener(OnFullClicked); }
        if (btnTransparent) { btnTransparent.onClick.RemoveAllListeners(); btnTransparent.onClick.AddListener(OnTransparentClicked); }
        if (btnIsolate)     { btnIsolate.onClick.RemoveAllListeners();     btnIsolate.onClick.AddListener(OnIsolateClicked); }

        BuildHighlightableIndex();
        RefreshUI(_currentSel);
        UpdateColliderGateForSelection();
    }

    void OnDisable()
    {
        UngateColliders();
        if (manager) manager.OnSelectionChanged -= OnSelectionChanged;
    }

    void Update()
    {
        if (!manager) return;
        var sel = manager.GetSelected();
        if (sel != _currentSel)
        {
            _currentSel = sel;
            RefreshUI(sel);
            UpdateColliderGateForSelection();
        }
    }

    // ===== Selection sync =====
    void OnSelectionChanged(Highlightable h)
    {
        _currentSel = h;
        RefreshUI(h);
        UpdateColliderGateForSelection();
    }

    void RefreshUI(Highlightable h)
    {
        bool hasSel = h != null;
        if (selectedNameText) selectedNameText.text = hasSel ? h.name : "(no selection)";
        if (btnTransparent)   btnTransparent.interactable = hasSel;
        if (btnIsolate)       btnIsolate.interactable     = hasSel;
    }

    // ===== FULL =====
    public void OnFullClicked()
    {
        UngateColliders();
        _gatedRootHL = null;

        RestoreAllTransparency();
        DestroyAllIsoRoots();

        if (_originalRoot) _originalRoot.gameObject.SetActive(true);
        modelRoot = _originalRoot;

        if (modelMenuPanel) modelMenuPanel.SetModelRoot(_originalRoot, keepOpenState: true);

        if (orbitRig && _orbitOriginalSaved)
        {
            orbitRig.target = _originalOrbitTarget;
            orbitRig.targetOffset = _originalOrbitTargetOffset;
            orbitRig.ResetView();
        }

        manager?.ClearSelection();
        RefreshUI(null);
        BuildHighlightableIndex();
    }

    // ===== TRANSPARENT =====
    void OnTransparentClicked()
    {
        var sel = _currentSel ?? (manager ? manager.GetSelected() : null);
        if (!sel) return;

        Highlightable.ClearAllHovers();

        var all = sel.GetComponentsInChildren<Renderer>(includeInactive: true);
        foreach (var r in all)
        {
            if (!IsRenderableCandidate(r)) continue;
            if (IsOutlineRenderer(r))      continue;
            TryToggleRendererTransparency(r, transparentAlpha);
        }

        bool blockOutline = !keepOutlineOnTransparent;
        var highs = sel.GetComponentsInChildren<Highlightable>(true);
        for (int i = 0; i < highs.Length; i++)
            highs[i].SetOutlineBlocked(blockOutline);

        UpdateColliderGateForSelection();
    }

    // ===== ISOLATE =====
    public void OnIsolateClicked()
    {
        Highlightable.ClearAllHovers();
        Highlightable.ClearAllSelected();
        Highlightable.ClearAllOutlinesAndFlags();

        UngateColliders();
        _gatedRootHL = null;

        var sel = _currentSel ?? (manager ? manager.GetSelected() : null);
        if (!sel) return;

        if (_isolated) DestroyAllIsoRoots();

        // 1) מקורות (הנבחר + also)
        var sources = CollectIsolateSources(sel);
        if (sources == null || sources.Count == 0) return;

        bool anyRenderable = false;
        for (int i = 0; i < sources.Count; i++)
        {
            var s = sources[i];
            if (s && s.GetComponentsInChildren<Renderer>(true).Length > 0)
            { anyRenderable = true; break; }
        }
        if (!anyRenderable)
        {
            Debug.LogWarning("[Isolate] No renderers found in selection bundle.");
            return;
        }

        // 2) כיבוי המקור
        if (_originalRoot) _originalRoot.gameObject.SetActive(false);

        // 3) קונטיינר
        var first = sources[0];
        Transform p = first ? first.parent : null;

        var container = new GameObject(first.name + "__IsolatedRoot").transform;
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

        _isoRoots.Add(container);

        // 4) Pivot
        _isoPivot = new GameObject(first.name + "_Pivot").transform;
        _isoPivot.SetParent(container, false);
        _isoPivot.localPosition = Vector3.zero;
        _isoPivot.localRotation = Quaternion.identity;

        // 5) שכפול
        for (int i = 0; i < sources.Count; i++)
        {
            var src = sources[i];
            if (!src) continue;

            var clone = Instantiate(src.gameObject);
            var ct = clone.transform;
            ct.SetParent(container, worldPositionStays: false);
            ct.localPosition = src.localPosition;
            ct.localRotation = src.localRotation;
            ct.localScale    = src.localScale;

            StripOutlineChildrenOnly(ct);
            EnsureRenderersVisibleOpaque(ct);
        }

        // 6) שכבה+הפעלה
        SetLayerRecursively(container, LayerMask.NameToLayer("Default"));
        container.gameObject.SetActive(true);

        // 7) מרכז+רצפה
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

        // 8) פריימינג חכם + רמזים
        modelRoot = container;
        if (modelMenuPanel) modelMenuPanel.SetModelRoot(container, keepOpenState: true);

        if (orbitRig)
        {
            orbitRig.target = _isoPivot;
            orbitRig.targetOffset = Vector3.zero;

            bool hasR2; var worldB = ComputeWorldBounds(container, out hasR2);

            // מצא מקור לרמז (אם קיים) – מחפש על אחד המקורות או בהוריהם
            Transform hintSource = allowPerObjectViewHint ? FindHintSource(sources) : null;

            if (hasR2) FrameIsolated(container, worldB, hintSource);
            else       orbitRig.ResetView();
        }
        else
        {
            PlaceCameraToSee(container);
        }

        // 9) נקה
        manager?.ClearSelection();
        Highlightable.ClearAllHovers();
        Highlightable.ClearAllOutlinesAndFlags();

        _isolated = true;
        RefreshUI(null);
        BuildHighlightableIndex();
    }

    // ===== Colliders gating =====
    void BuildHighlightableIndex()
    {
        _hlByMenuId.Clear();
        if (!modelRoot) return;

        var highs = modelRoot.GetComponentsInChildren<Highlightable>(true);
        for (int i = 0; i < highs.Length; i++)
        {
            var h = highs[i]; if (!h) continue;
            var id = !string.IsNullOrEmpty(h.menuId) ? h.menuId : h.name;
            if (string.IsNullOrEmpty(id)) continue;
            if (!_hlByMenuId.ContainsKey(id))
                _hlByMenuId[id] = h;
        }
    }

    bool IsSameFamilyLogical(Highlightable parent, Highlightable other)
    {
        if (!parent || !other) return false;
        if (parent == other) return true;

        string pid = other.parentMenuId;
        int guard = 0;
        while (!string.IsNullOrEmpty(pid) && guard++ < 64)
        {
            if (_hlByMenuId.TryGetValue(pid, out var p))
            {
                if (p == parent) return true;
                pid = p.parentMenuId;
            }
            else break;
        }

        if (other.transform && parent.transform && other.transform.IsChildOf(parent.transform))
            return true;

        return false;
    }

    static void StripOutlineChildrenOnly(Transform root)
    {
        var toDelete = new List<GameObject>();
        var rends = root.GetComponentsInChildren<MeshRenderer>(true);
        foreach (var r in rends)
            if (r.gameObject.name.EndsWith(".__Outline"))
                toDelete.Add(r.gameObject);

        for (int i = 0; i < toDelete.Count; i++)
            if (toDelete[i]) Destroy(toDelete[i]);
    }

    void UpdateColliderGateForSelection()
    {
        var sel = _currentSel;

        if (_gatedRootHL)
        {
            bool rootStillTransparent = IsTransformTransparent(_gatedRootHL.transform);

            if (sel && rootStillTransparent && IsSameFamilyLogical(_gatedRootHL, sel))
            {
                // השאר כבוי
            }
            else
            {
                UngateColliders();
                _gatedRootHL = null;
            }
        }

        if (!_gatedRootHL && sel && IsTransformTransparent(sel.transform))
        {
            GateOwnColliders(sel.transform);
            _gatedRootHL = sel;
        }
    }

    void GateOwnColliders(Transform t)
    {
        if (!t) return;
        var cols = t.GetComponents<Collider>();
        for (int i = 0; i < cols.Length; i++)
        {
            var c = cols[i];
            if (!c || !c.enabled) continue;
            _gated.Add(new GateRecord { col = c });
            c.enabled = false;
        }
    }

    void UngateColliders()
    {
        for (int i = 0; i < _gated.Count; i++)
        {
            var rec = _gated[i];
            if (rec.col) rec.col.enabled = true;
        }
        _gated.Clear();
    }

    // ===== Transparency helpers =====
    bool IsRenderableCandidate(Renderer r)
    {
        if (!r || !r.enabled)           return false;
        if (r is ParticleSystemRenderer) return false;
        if (r is TrailRenderer)          return false;
        if (r is LineRenderer)           return false;
        return true;
    }

    bool MatchesOutlineShader(string shaderName)
    {
        if (string.IsNullOrEmpty(shaderName) || outlineShaderNames == null) return false;
        for (int i = 0; i < outlineShaderNames.Length; i++)
        {
            var pattern = outlineShaderNames[i];
            if (!string.IsNullOrEmpty(pattern) &&
                shaderName.IndexOf(pattern, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    bool IsOutlineRenderer(Renderer r)
    {
        if (!r) return false;

        if (!string.IsNullOrEmpty(outlineChildSuffix) && r.gameObject.name.EndsWith(outlineChildSuffix))
            return true;

        var mats = r.sharedMaterials;
        if (mats == null) return false;

        for (int i = 0; i < mats.Length; i++)
        {
            var m = mats[i];
            if (!m || !m.shader) continue;
            if (MatchesOutlineShader(m.shader.name))
                return true;
        }
        return false;
    }

    void SetOutlineEnabled(Transform root, bool enable)
    {
        if (!root) return;

        if (!string.IsNullOrEmpty(outlineChildSuffix))
        {
            var trs = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < trs.Length; i++)
                if (trs[i].gameObject.name.EndsWith(outlineChildSuffix))
                    trs[i].gameObject.SetActive(enable);
        }

        var rends = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rends.Length; i++)
        {
            var r = rends[i];
            if (!r) continue;
            var mats = r.sharedMaterials; if (mats == null) continue;

            bool looksLikeOutline = false;
            for (int m = 0; m < mats.Length; m++)
            {
                var mm = mats[m]; if (!mm || !mm.shader) continue;
                if (MatchesOutlineShader(mm.shader.name)) { looksLikeOutline = true; break; }
            }
            if (looksLikeOutline) r.enabled = enable;
        }
    }

    bool TryToggleRendererTransparency(Renderer r, float alpha)
    {
        if (_fadeMap.TryGetValue(r, out var cache))
        {
            cache.isTransparent = !cache.isTransparent;
            if (cache.isTransparent) r.materials = cache.transparentInstanced;
            else                     r.sharedMaterials = cache.originalShared;
            return true;
        }

        var shared = r.sharedMaterials;
        if (shared == null || shared.Length == 0) return false;

        var instanced   = r.materials; // יוצר עותקים
        bool changedAny = false;

        for (int i = 0; i < instanced.Length; i++)
        {
            var m = instanced[i];
            if (!m) continue;

            var shaderName = m.shader ? m.shader.name : "";
            if (MatchesOutlineShader(shaderName))
                continue;

            if (MakeURPLitTransparentSafe(m, alpha, writeDepthOnTransparent))
                changedAny = true;
        }

        if (!changedAny)
        {
            r.sharedMaterials = shared;
            return false;
        }

        _fadeMap[r] = new FadeCache
        {
            r = r,
            originalShared       = shared,
            transparentInstanced = instanced,
            isTransparent        = true
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
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend",  (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend",  (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_ZWrite"))   m.SetFloat("_ZWrite", forceZWrite ? 1f : 0f);

        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        if (hasBase)
        {
            var c = m.GetColor("_BaseColor"); c.a = Mathf.Clamp01(alpha); m.SetColor("_BaseColor", c);
        }
        else if (hasColor)
        {
            var c = m.GetColor("_Color");     c.a = Mathf.Clamp01(alpha); m.SetColor("_Color", c);
        }
        return true;
    }

    void RestoreAllTransparency()
    {
        UngateColliders();
        _gatedRootHL = null;

        foreach (var kv in _fadeMap)
        {
            var r = kv.Key; var cache = kv.Value;
            if (!r) continue;

            try { r.sharedMaterials = cache.originalShared; } catch {}

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

    bool IsTransformTransparent(Transform t)
    {
        if (!t) return false;
        var rends = t.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rends.Length; i++)
        {
            var r = rends[i];
            if (!r) continue;
            if (_fadeMap.TryGetValue(r, out var cache) && cache.isTransparent)
                return true;
        }
        return false;
    }

    // ===== Isolate helpers =====
    List<Transform> CollectIsolateSources(Highlightable root)
    {
        var list = new List<Transform>();
        var seen = new HashSet<Transform>();
        if (!root) return list;

        if (root.isolateIncludeSelf && root.transform && seen.Add(root.transform))
            list.Add(root.transform);

        if (root.isolateAlsoTransforms != null)
        {
            for (int i = 0; i < root.isolateAlsoTransforms.Count; i++)
            {
                var t = root.isolateAlsoTransforms[i];
                if (!t) continue;
                if (seen.Add(t)) list.Add(t);
            }
        }
        return list;
    }

    /// <summary>מעדיף מקור שיש עליו/על אביו ViewHint.</summary>
    Transform FindHintSource(List<Transform> sources)
    {
        if (sources == null) return null;

        // חפש קודם מקור שיש לו ViewHint בעצמו או על ההורה
        for (int i = 0; i < sources.Count; i++)
        {
            var t = sources[i];
            if (!t) continue;
            if (t.GetComponentInParent<ViewHint>()) return t;
        }
        // אם אין רמז מפורש – נחזיר את הראשון (לצורך Local* אם תבחר ידנית)
        return sources.Count > 0 ? sources[0] : null;
    }

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
                if (m.HasProperty("_Surface"))   m.SetFloat("_Surface", 0f); // Opaque
                if (m.HasProperty("_ZWrite"))    m.SetFloat("_ZWrite",  1f);
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

    void DestroyAllIsoRoots()
    {
        RestoreAllTransparency();

        for (int i = 0; i < _isoRoots.Count; i++)
            if (_isoRoots[i]) Destroy(_isoRoots[i].gameObject);

        _isoRoots.Clear();
        _isoPivot  = null;
        _isolated  = false;
    }

    // ===================== פריימינג חכם =====================
    Bounds ComputeLocalBounds(Transform root)
    {
        var inv = root.worldToLocalMatrix;
        bool init = false;
        Bounds lb = default;

        var rends = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rends.Length; i++)
        {
            var r = rends[i]; if (!r) continue;
            var b = r.bounds;
            Vector3 c = b.center;
            Vector3 e = b.extents;

            for (int k = 0; k < 8; k++)
            {
                var corner = new Vector3(
                    c.x + e.x * ((k & 1) != 0 ? 1f : -1f),
                    c.y + e.y * ((k & 2) != 0 ? 1f : -1f),
                    c.z + e.z * ((k & 4) != 0 ? 1f : -1f)
                );
                var local = inv.MultiplyPoint3x4(corner);
                if (!init) { lb = new Bounds(local, Vector3.zero); init = true; }
                else lb.Encapsulate(local);
            }
        }

        if (!init) lb = new Bounds(root.InverseTransformPoint(root.position), Vector3.one * 0.1f);
        return lb;
    }

    Vector3 FindPreferredViewDirWorld(Transform container, Transform source, Bounds localBounds)
    {
        PreferredView pref = defaultPreferredView;

        if (allowPerObjectViewHint && source)
        {
            var hint = source.GetComponentInParent<ViewHint>();
            if (hint) pref = hint.preferred;
        }

        if (pref == PreferredView.AutoBroadside)
        {
            if (autoHorizontalOnly)
                return AutoBroadsideHorizontal(container, localBounds, worldUp); // ← חדש: רק אופקי
            // אחרת: ההתנהגות הקודמת (גם Y מותר)
            Vector3 ext = localBounds.extents;
            float ax = ext.x, ay = ext.y, az = ext.z;

            Vector3 dirLocal;
            if      (ax <= ay && ax <= az) dirLocal = Vector3.left;   // -X
            else if (ay <= ax && ay <= az) dirLocal = Vector3.down;   // -Y
            else                           dirLocal = Vector3.back;   // -Z

            return container.TransformDirection(dirLocal).normalized;
        }
        else
        {
            Vector3 dirLocal = Vector3.back;
            switch (pref)
            {
                case PreferredView.LocalXPos: dirLocal = Vector3.right;   break;
                case PreferredView.LocalXNeg: dirLocal = Vector3.left;    break;
                case PreferredView.LocalYPos: dirLocal = Vector3.up;      break;
                case PreferredView.LocalYNeg: dirLocal = Vector3.down;    break;
                case PreferredView.LocalZPos: dirLocal = Vector3.forward; break;
                case PreferredView.LocalZNeg: dirLocal = Vector3.back;    break;
            }
            return container.TransformDirection(dirLocal).normalized;
        }
    }

    Vector3 AutoBroadsideHorizontal(Transform container, Bounds localBounds, Vector3 worldUpDir)
    {
        // נרמל worldUp
        if (worldUpDir.sqrMagnitude < 1e-6f) worldUpDir = Vector3.up;
        worldUpDir = worldUpDir.normalized;

        // בחר ציר “דק” רק בין X/Z (בלי Y בכלל)
        Vector3 ext = localBounds.extents;
        bool useX = ext.x <= ext.z;

        // כיוון אופקי של המצלמה (להקטין קפיצות כיוון)
        Vector3 camHoriz = Vector3.forward;
        if (Camera.main)
        {
            var cf = Vector3.ProjectOnPlane(Camera.main.transform.forward, worldUpDir);
            if (cf.sqrMagnitude > 1e-6f) camHoriz = cf.normalized;
        }

        // מועמדים: +/-X או +/-Z, מקרינים למישור האופקי ובוחרים את הדוט הכי גדול מול camHoriz
        Vector3[] candLocal = useX
            ? new[] { Vector3.right, Vector3.left }
            : new[] { Vector3.forward, Vector3.back };

        Vector3 best = Vector3.zero;
        float bestDot = -999f;

        for (int i = 0; i < candLocal.Length; i++)
        {
            var w = container.TransformDirection(candLocal[i]);
            w = Vector3.ProjectOnPlane(w, worldUpDir);
            float mag = w.magnitude;
            if (mag < 1e-6f) continue;
            w /= mag;

            float d = Vector3.Dot(w, camHoriz);
            if (d > bestDot) { bestDot = d; best = w; }
        }

        if (best.sqrMagnitude < 1e-6f)
        {
            // נפילה בטוחה: קח -forward של הקונטיינר, הקרן אופקי
            var w = Vector3.ProjectOnPlane(-container.forward, worldUpDir);
            if (w.sqrMagnitude < 1e-6f) w = Vector3.ProjectOnPlane(Vector3.forward, worldUpDir);
            return w.normalized;
        }

        return best.normalized;
    }

    float ComputeDistanceForFraming(Camera cam, Bounds worldBounds, float targetScreenHeightFrac, float minDistance, float safetyFactor)
    {
        // מגביל את טווח המילוי (גובה על המסך)
        targetScreenHeightFrac = Mathf.Clamp(targetScreenHeightFrac, 0.2f, 0.95f);

        // משתמשים ב"רדיוס" כולל של האובייקט (extents.magnitude) כדי להימנע מתת־הערכה
        float radius = Mathf.Max(1e-4f, worldBounds.extents.magnitude);

        // גיאומטריית FOV אנכית
        float halfTan = Mathf.Tan(0.5f * cam.fieldOfView * Mathf.Deg2Rad);

        // מרחק שמבטיח שהאובייקט יכנס בנוחות בהתאם ל־targetScreenHeightFrac
        float dist = radius / (halfTan * Mathf.Max(0.05f, targetScreenHeightFrac));

        // פקטור בטיחות קטן למנוע חיתוכים
        dist *= Mathf.Max(1.0f, safetyFactor);

        // מרחק מינימלי גלובלי/פר־אובייקט
        return Mathf.Max(dist, Mathf.Max(0.01f, minDistance));
    }

    void FrameIsolated(Transform container, Bounds worldBounds, Transform sourceForHint)
    {
        var cam = Camera.main; 
        if (!cam || !orbitRig) { orbitRig.ResetView(); return; }

        Bounds localB = ComputeLocalBounds(container);
        Vector3 dirW = FindPreferredViewDirWorld(container, sourceForHint, localB);
        if (dirW.sqrMagnitude < 1e-6f) dirW = -container.forward;

        Vector3 focus = worldBounds.center;

        // מינימום גלובלי
        float minDist = Mathf.Max(0.01f, minIsolationDistance);

        

        // חישוב מרחק יציב
        float dist = ComputeDistanceForFraming(
            cam, 
            worldBounds, 
            isolateScreenHeightFrac, 
            minDist, 
            distanceSafetyFactor
        );

        Vector3 camPos = focus - dirW * dist;
        Quaternion camRot = Quaternion.LookRotation(focus - camPos, Vector3.up);

        var temp = new GameObject("__TempIsoView").transform;
        temp.position = camPos;
        temp.rotation = camRot;

        orbitRig.SetView(temp, Mathf.Max(0f, isolateViewBlend));
        Destroy(temp.gameObject, Mathf.Max(0.1f, isolateViewBlend + 0.1f));
    }
}