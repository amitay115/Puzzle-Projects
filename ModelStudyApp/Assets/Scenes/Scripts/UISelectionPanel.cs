using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UISelectionPanel : MonoBehaviour
{

    Vector3  _originalOrbitTargetOffset;
    bool     _orbitOriginalSaved;

    Transform _originalOrbitTarget;           // נשמור את הטארגט המקורי
    Transform _isoPivot;                      // פיווט של האיזולייט

    [SerializeField] private Transform modelRoot; // גרור באינספקטור את root של המודל (למשל spyderMR_example)

    [Header("Isolate Placement")]
    public bool snapAboveFloor = true;
    public float floorY = 0f;          // גובה הרצפה שלך (לרוב 0)
    public float placePadding = 0.02f; // מרווח קטן מעל הרצפה


    [Header("Refs")]
    public HoverHighlightManager manager;     // גרור את המנהל שעל המצלמה
    //public CameraFocusHelper focusHelper;     // גרור את ה-Helper שעל המצלמה (לא חובה אבל מומלץ)
    public OrbitCameraRig orbitRig;           // אם יש לך Rig עם ResetView()

    [Header("UI")]
    public TMPro.TMP_Text selectedNameText;             // אם אתה עם TMP: החלף ל-TMPro.TMP_Text
    public Button btnFull;
    public Button btnTransparent;
    public Button btnIsolate;

    [Header("Transparency")]
    [Range(0f, 1f)] public float transparentAlpha = 0.15f;

    // --- State ---
    Transform _originalRoot;          // modelRoot המקורי
    bool _isolated;
    GameObject _isolatedClone;

    // קאש של חומרים פר Renderer עבור Toggle Transparency
    class FadeCache
    {
        public Renderer r;
        public Material[] originalShared;
        public Material[] transparentInstanced;
        public bool isTransparent;
    }
    readonly Dictionary<Renderer, FadeCache> _fadeMap = new();

    void Start()
    {
        if (!manager) manager = Camera.main.GetComponent<HoverHighlightManager>();

        if (manager && !_originalRoot) _originalRoot = modelRoot;

        if (manager) manager.OnSelectionChanged += OnSelectionChanged;

        if (orbitRig && !_originalOrbitTarget) _originalOrbitTarget = orbitRig.target;

        if (orbitRig && !_orbitOriginalSaved)
        {
            _originalOrbitTarget        = orbitRig.target;
            _originalOrbitTargetOffset  = orbitRig.targetOffset;
            _orbitOriginalSaved         = true;
        }


        btnFull.onClick.AddListener(OnFullClicked);
        btnTransparent.onClick.AddListener(OnTransparentClicked);
        btnIsolate.onClick.AddListener(OnIsolateClicked);

        RefreshUI(manager ? manager.GetSelected() : null);
    }

    void OnDestroy()
    {
        if (manager) manager.OnSelectionChanged -= OnSelectionChanged;
    }

    void OnSelectionChanged(Highlightable h) => RefreshUI(h);

    void RefreshUI(Highlightable h)
    {
        bool hasSel = h != null;
        if (selectedNameText) selectedNameText.text = hasSel ? h.name : "(no selection)";
        btnTransparent.interactable = hasSel;
        btnIsolate.interactable = hasSel;
        // Full תמיד פעיל
    }

    // ========================= Full =========================
    void OnFullClicked()
    {
        if (_isolated)
        {
            if (_isolatedClone) Destroy(_isolatedClone);
            _isolatedClone = null;
            _isoPivot = null;

            if (_originalRoot) _originalRoot.gameObject.SetActive(true);
            if (manager) modelRoot = _originalRoot;

            // החזר את טארגט המצלמה המקורי
            if (orbitRig) orbitRig.target = _originalOrbitTarget;

            _isolated = false;
        }
        if (orbitRig && _orbitOriginalSaved)
        {
            orbitRig.target       = _originalOrbitTarget;
            orbitRig.targetOffset = _originalOrbitTargetOffset;  // ← זה הפרט החסר
            orbitRig.ResetView();
        }


        // ... (שאר הניקויים: שקיפות, בחירה, ResetCameraView)
        manager?.ClearSelection();
        ResetCameraView();
        RefreshUI(null);
    }


    void ResetCameraView()
    {
        if (orbitRig) { orbitRig.ResetView(); return; }

    }

    // ====================== Transparent ======================
    void OnTransparentClicked()
    {
        var sel = manager?.GetSelected();
        if (!sel) return;

        var rends = sel.GetComponentsInChildren<Renderer>(includeInactive: true);
        foreach (var r in rends)
        {
            if (!r || r is ParticleSystemRenderer) continue;
            ToggleRendererTransparency(r, transparentAlpha);
        }
    }

    void ToggleRendererTransparency(Renderer r, float alpha)
    {
        if (!_fadeMap.TryGetValue(r, out var cache))
        {
            cache = new FadeCache { r = r, originalShared = r.sharedMaterials };
            var mats = r.materials; // instanced
            for (int i = 0; i < mats.Length; i++)
                MakeURPLitTransparent(mats[i], alpha);
            cache.transparentInstanced = mats;
            _fadeMap[r] = cache;
        }

        cache.isTransparent = !cache.isTransparent;
        if (cache.isTransparent) r.materials = cache.transparentInstanced;
        else r.sharedMaterials = cache.originalShared;
    }

    static void MakeURPLitTransparent(Material m, float alpha)
    {
        if (!m) return;
        // URP/Lit טיפוסי
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f); // Transparent
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 1f); // כתיבת עומק גם בשקוף
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        if (m.HasProperty("_BaseColor"))
        {
            var c = m.GetColor("_BaseColor"); c.a = Mathf.Clamp01(alpha); m.SetColor("_BaseColor", c);
        }
        else if (m.HasProperty("_Color"))
        {
            var c = m.GetColor("_Color"); c.a = Mathf.Clamp01(alpha); m.SetColor("_Color", c);
        }
    }

    // ========================= Isolate ======================
    void OnIsolateClicked()
    {
        var sel = manager?.GetSelected();
        if (!sel || _isolated) return;

        Transform src = sel.transform;

        // 1) ודא שיש מה לרנדר
        var srcRenderers = src.GetComponentsInChildren<Renderer>(true);
        if (srcRenderers.Length == 0)
        {
            Debug.LogError($"[Isolate] '{src.name}' לא מכיל Renderer. בחר הורה עם רנדררים.");
            return;
        }

        // 2) כבה את המודל המקורי
        if (_originalRoot) _originalRoot.gameObject.SetActive(false);

        // 3) בנה קונטיינר שמחליף את ההורה המקורי (כולל world-scale של ההורה)
        Transform p = src.parent;
        var container = new GameObject(src.name + "__IsolatedRoot").transform;
        if (p == null)
        {
            container.position   = Vector3.zero;
            container.rotation   = Quaternion.identity;
            container.localScale = Vector3.one;
        }
        else
        {
            container.position   = p.position;
            container.rotation   = p.rotation;
            container.localScale = p.lossyScale;   // ← משמר את world-scale של הענף
        }

        // 4) צור Pivot בתוך הקונטיינר (יהיה נק' המבט של ה-OrbitRig)
        _isoPivot = new GameObject(src.name + "_Pivot").transform;
        _isoPivot.SetParent(container, false);
        _isoPivot.localPosition = Vector3.zero;
        _isoPivot.localRotation = Quaternion.identity;

        // 5) שכפל את האובייקט והחזר את ה-LocalTransform המקורי שלו תחת הקונטיינר
        _isolatedClone = Instantiate(src.gameObject);
        var cloneT = _isolatedClone.transform;
        cloneT.SetParent(container, worldPositionStays: false);
        cloneT.localPosition = src.localPosition;   // ← כמו במקור
        cloneT.localRotation = src.localRotation;   // ← כמו במקור
        cloneT.localScale    = src.localScale;      // ← הקונטיינר נושא את סקייל ההורה

        // 6) נקה לגמרי Outline/Highlight וחזֵר חומרים ל-Opaque
        StripOutlineAndHighlightable(container);       // מוחק ילדים בשם ".__Outline" ומסיר Highlightable
        EnsureRenderersVisibleOpaque(container);       // מחזיר ל-Opaque + alpha=1
        SetLayerRecursively(container, LayerMask.NameToLayer("Default"));
        container.gameObject.SetActive(true);

        // 7) מֵרכֵּז את האובייקט לאפס:
        //    מחשבים Bounds בעולם, מזיזים את הקונטיינר כך שמרכז ה-Bounds יהיה (0,0,0)
        bool hasR; 
        var b = ComputeWorldBounds(container, out hasR);
        if (hasR)
        {
            // (א) מרכז את מרכז ה-Bounds לאפס
            container.position -= b.center;

            // (ב) הרם כך שהתחתית של ה-Bounds תהיה מעל הרצפה
            if (snapAboveFloor)
            {
                b = ComputeWorldBounds(container, out hasR);       // עדכון bounds אחרי ההזזה
                float lift = (floorY + placePadding) - b.min.y;    // כמה חסר כדי להיות מעל הרצפה
                if (lift > 0f)
                {
                    container.position += new Vector3(0f, lift, 0f);
                    b = ComputeWorldBounds(container, out hasR);   // עדכון נוסף (לא חובה, אבל נחמד לדיבוג)
                }
            }

            // (ג) עדכן את הפיבוט כך שישב בדיוק באפס עולמי
            // בסיום כל ההזזות – הצב את הפיבוט במרכז האמיתי של האובייקט
            SetIsoPivotToBoundsCenter(container);

        }


        // 3) אחרי כל ההזזות – מיקום מצלמה לראות טוב
        modelRoot = container;
        if (orbitRig != null)
        {
            orbitRig.target = _isoPivot;
            orbitRig.targetOffset = Vector3.zero;
            orbitRig.ResetView();
        }
        else
        {
            PlaceCameraToSee(container);
        }


        // 8) עדכן את המנהל והמצלמה
        modelRoot = container;

        if (orbitRig != null)
        {
            orbitRig.target = _isoPivot;
            orbitRig.targetOffset = Vector3.zero;
            orbitRig.ResetView();             // מבט התחלתי נקי סביב האפס
        }
        else
        {
            // fallback אם אין OrbitRig
            PlaceCameraToSee(container);
        }

        // 9) אין בחירה/Outline במצב בידוד (כדי שלא “יחזור” השיידר)
        manager.ClearSelection();

        _isolated = true;
        RefreshUI(null); // מעדכן טקסט ל-(no selection)
    }





    static void CenterToOrigin(Transform root)
    {
        var rends = root.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0) { root.position = Vector3.zero; root.rotation = Quaternion.identity; return; }

        Bounds b = new Bounds(rends[0].bounds.center, Vector3.zero);
        for (int i = 0; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

        Vector3 center = b.center;
        root.position += -center;          // מזיז כך שמרכז ה-Bounds באפס
        root.rotation = Quaternion.identity;
    }

    static Vector3 CenterToOriginAndReturnWorldCenter(Transform root)
    {
        var rends = root.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0)
        {
            root.position = Vector3.zero;
            root.rotation = Quaternion.identity;
            return Vector3.zero;
        }

        Bounds b = new Bounds(rends[0].bounds.center, Vector3.zero);
        for (int i = 0; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

        Vector3 worldCenter = b.center;
        // הזזה כך שמרכז ה-Bounds ישב באפס עולמי
        root.position += -worldCenter;
        root.rotation = Quaternion.identity;
        return worldCenter;
    }

    static void StripOutlineAndHighlightable(Transform root)
    {
        // 1) מחיקת ילדים שנוצרו ע"י Highlightable (שם נגמר ב ".__Outline")
        var toDelete = new List<GameObject>();
        var rends = root.GetComponentsInChildren<MeshRenderer>(true);
        foreach (var r in rends)
            if (r.gameObject.name.EndsWith(".__Outline"))
                toDelete.Add(r.gameObject);
        for (int i = 0; i < toDelete.Count; i++)
            Destroy(toDelete[i]);

        // 2) הורדת Highlightable מהשכפול (לא צריך בזמן בידוד)
        var highs = root.GetComponentsInChildren<Highlightable>(true);
        for (int i = 0; i < highs.Length; i++)
            Destroy(highs[i]);
    }

    // מחזיר חומרים ל־Opaque + Alpha=1 ומוודא Renderer.enabled
    static void EnsureRenderersVisibleOpaque(Transform root)
    {
        var rrs = root.GetComponentsInChildren<Renderer>(true);
        foreach (var r in rrs)
        {
            if (!r) continue;
            r.enabled = true;

            var mats = r.materials; // instanced (מותר כאן)
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
    
    static void SetLayerRecursively(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursively(t.GetChild(i), layer);
    }

    static void EnsureRenderersVisible(Transform root)
    {
        var rrs = root.GetComponentsInChildren<Renderer>(true);
        foreach (var r in rrs)
        {
            if (!r) continue;
            r.enabled = true;

            var mats = r.materials; // instanced
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (!m) continue;

                // החזר ל־Opaque עם אלפא 1 (URP/Lit טיפוסי)
                if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 0f);
                if (m.HasProperty("_ZWrite"))  m.SetFloat("_ZWrite", 1f);
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

    void PlaceCameraToSee(Transform root)
    {
        var cam = Camera.main;
        if (!cam) return;

        bool hasR; var b = ComputeWorldBounds(root, out hasR);
        var center = b.center;
        float radius = Mathf.Max(b.extents.magnitude, 0.5f);

        // אם יש OrbitCameraRig – ננסה Reset ואז נרחיק מעט
        if (orbitRig != null && orbitRig.target != null)
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

        bool hasR;
        var b = ComputeWorldBounds(container, out hasR);
        if (!hasR) return;

        // מציב את הפיבוט במרכז המדויק של האובייקט (בעולם), כולל בציר ה־Y
        _isoPivot.position = b.center;
        _isoPivot.rotation = Quaternion.identity; // אופציונלי: לאפס גלגול/נטייה של הפיבוט
    }


    #if UNITY_EDITOR
    // לעזור לך למסגר מיד ב-SceneView
    static void FrameInSceneView(Transform t)
    {
        var sv = UnityEditor.SceneView.lastActiveSceneView;
        if (sv != null)
        {
            sv.FrameSelected();
        }
    }
    #endif



}
