using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class OrbitCameraRig : MonoBehaviour
{
    [Header("Target (Pivot)")]
    [Tooltip("פיבוט/מרכז המודל (רצוי ילד ריק במרכז המודל)")]
    public Transform target;
    public Vector3 targetOffset = Vector3.zero;

    [Header("Distance / Zoom")]
    public float distance = 6f;
    public float minDistance = 2f;
    public float maxDistance = 20f;
    [Tooltip("מהירות זום (גלגלת/+/−)")]
    public float zoomSpeed = 1.2f;
    public bool zoomWithPlusMinus = true;

    [Header("Rotation")]
    [Tooltip("רגישות לגרירת עכבר (כשהכפתור לחוץ)")]
    public float mouseSensitivity = 0.2f;
    [Tooltip("מהירות סיבוב עם חיצים/WASD (מעלות/שניה)")]
    public float keysSpeedDegPerSec = 90f;
    public bool invertY = false;
    public float yMinLimit = -20f;
    public float yMaxLimit = 80f;

    [Header("Smoothing")]
    public float rotateDamp = 12f;
    public float zoomDamp = 12f;

    [Header("Input")]
    [Tooltip("0=שמאלי, 1=ימני, 2=אמצעי")]
    public int rotateMouseButton = 0;
    [Tooltip("אל תזיז כשהסמן מעל UI")]
    public bool lockWhenOverUI = true;

    [Header("View Presets")]
    public Transform[] viewpoints;
    public float viewBlendTime = 0.5f;

    [Header("Reset View")]
    public bool enableResetKey = true;
    public KeyCode resetKey = KeyCode.R;

    [Header("Collision (No Penetration)")]
    public bool collisionEnabled = true;

    [Tooltip("רדיוס ‘גוף המצלמה’ לחישובי פגיעה/סוויפ")]
    public float cameraRadius = 0.35f;

    [Tooltip("מרחק ביטחון מהמשטח. לא יקטן מ-0.5f (מאוכף אוטומטית)")]
    public float cameraClearance = 0.6f;

    [Tooltip("שכבות שחוסמות את המצלמה")]
    public LayerMask collisionLayers = ~0;

    [Tooltip("להתעלם מקוליידרים תחת ה-Target (אם המודל ילד של ה-pivot, השאר FALSE)")]
    public bool ignoreTargetColliders = false;

    [Tooltip("Roots להתעלמות (אם המודל לא תחת ה-Target)")]
    public Transform[] collisionIgnoreRoots;

    [Header("Lateral Block → Auto Zoom-Out")]
    [Tooltip("הפעל זום-אאוט אוטומטי כשיש חסימה לרוחב בזמן תנועה/סיבוב")]
    public bool autoZoomOutOnLateralBlock = true;

    [Tooltip("מהירות פתיחת זום כשחסום לרוחב (יח'/שניה)")]
    public float lateralZoomOutSpeed = 8f;

    [Header("Floor Clamp (Optional)")]
    public bool clampToFloor = false;
    public float floorY = 0f;
    public float floorPadding = 0.05f;

    // ---- Internal state ----
    const float kMinClearance = 0.5f; // לא נרד מזה
    float _yaw, _pitch, _yawVel, _pitchVel, _distVel;
    float _targetYaw, _targetPitch, _targetDist;

    float _startYaw, _startPitch, _startDist;

    Vector3 _lastCamPos;

    void OnValidate()
    {
        // אכיפה: clearance לא יקטן מ-0.5f
        if (cameraClearance < kMinClearance) cameraClearance = kMinClearance;

        // הגנות בסיס
        if (minDistance < 0.01f) minDistance = 0.01f;
        if (maxDistance < minDistance) maxDistance = minDistance;
        yMaxLimit = Mathf.Max(yMaxLimit, yMinLimit);
    }

    void Start()
    {
        if (target == null)
        {
            Debug.LogWarning($"{nameof(OrbitCameraRig)}: No target assigned.");
            enabled = false;
            return;
        }

        var e = transform.rotation.eulerAngles;
        _targetYaw = _yaw = e.y;
        _targetPitch = _pitch = ClampAngle(e.x, yMinLimit, yMaxLimit);

        _targetDist = Mathf.Clamp(distance, minDistance, maxDistance);
        distance = _targetDist;

        _startYaw = _targetYaw;
        _startPitch = _targetPitch;
        _startDist = _targetDist;

        UpdateCameraImmediate();
        _lastCamPos = transform.position;

        // התאמת מרחק מיידית אם מתחילים קרוב מדי (אל תאפשר חדירה מהפריים הראשון)
        if (collisionEnabled)
        {
            var rot0 = Quaternion.Euler(_targetPitch, _targetYaw, 0f);
            Vector3 focus0 = target.position + targetOffset;
            float minAllowed = ComputeMinAllowedDistance(rot0, focus0, distance);
            if (distance < minAllowed)
            {
                distance = _targetDist = Mathf.Clamp(minAllowed, minDistance, maxDistance);
                UpdateCameraImmediate();
                _lastCamPos = transform.position;
            }
        }
    }

    void Update()
    {
        if (target == null) return;

        if (enableResetKey && Input.GetKeyDown(resetKey))
            ResetView();

        bool overUI = IsPointerOverUI();
        float scroll = 0f;
        if (!overUI)
        {
            scroll = Input.mouseScrollDelta.y;   // גלגלת פועלת רק אם לא על UI
        }

        // Mouse drag (button held & not over UI)
        if (!overUI && Input.GetMouseButton(rotateMouseButton))
        {
            float mx = Input.GetAxis("Mouse X");
            float my = Input.GetAxis("Mouse Y");
            _targetYaw += mx * mouseSensitivity * 180f * Time.deltaTime;
            _targetPitch += (invertY ? my : -my) * mouseSensitivity * 180f * Time.deltaTime;
        }

        // Arrows/WASD
        float kx = 0f, ky = 0f;
        if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) kx += 1f;
        if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) kx -= 1f;
        if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) ky += 1f;
        if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) ky -= 1f;

        if (kx != 0f) _targetYaw += kx * keysSpeedDegPerSec * Time.deltaTime;
        if (ky != 0f) _targetPitch += (invertY ? -ky : ky) * keysSpeedDegPerSec * Time.deltaTime;

        _targetPitch = Mathf.Clamp(_targetPitch, yMinLimit, yMaxLimit);

        // Zoom (mouse wheel / +/-)
        if (zoomWithPlusMinus)
        {
            if (Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.KeypadPlus)) scroll += 1f;
            if (Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus)) scroll -= 1f;
        }
        if (Mathf.Abs(scroll) > 0.0001f)
            _targetDist = Mathf.Clamp(_targetDist - scroll * zoomSpeed, minDistance, maxDistance);

        // Floor clamp (מגביל Pitch לפי גובה הרצפה)
        if (clampToFloor)
        {
            Vector3 focus = target.position + targetOffset;
            float minCamY = floorY + floorPadding;
            float requiredSin = (minCamY - focus.y) / Mathf.Max(_targetDist, 0.0001f);
            requiredSin = Mathf.Clamp(requiredSin, -1f, 1f);
            float minPitchFromFloor = Mathf.Rad2Deg * Mathf.Asin(requiredSin);
            _targetPitch = Mathf.Clamp(_targetPitch, Mathf.Max(yMinLimit, minPitchFromFloor), yMaxLimit);
        }

        // Damping
        _yaw = Mathf.SmoothDampAngle(_yaw, _targetYaw, ref _yawVel, 1f / Mathf.Max(1f, rotateDamp));
        _pitch = Mathf.SmoothDampAngle(_pitch, _targetPitch, ref _pitchVel, 1f / Mathf.Max(1f, rotateDamp));
        distance = Mathf.SmoothDamp(distance, _targetDist, ref _distVel, 1f / Mathf.Max(1f, zoomDamp));

        // Apply (כולל מניעת חדירה מוחלטת)
        ApplyTransform();
    }

    void ApplyTransform()
    {
        Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 focus = target.position + targetOffset;

        float usedDist = distance;

        // 1) רדיאלי: אל תתקרב מעבר לנקודת המגע הראשונה + Clearance
        if (collisionEnabled)
        {
            float minAllowed = ComputeMinAllowedDistance(rot, focus, usedDist);
            if (usedDist < minAllowed) usedDist = minAllowed;
        }

        Vector3 desiredCamPos = focus + rot * new Vector3(0f, 0f, -usedDist);

        // 2) Sweep: מניעת "נגיחה" צדית בין המיקום הקודם לחדש
        bool lateralBlocked = false;
        if (collisionEnabled)
        {
            Vector3 sweepDir = desiredCamPos - _lastCamPos;
            float sweepLen = sweepDir.magnitude;
            if (sweepLen > 1e-4f)
            {
                sweepDir /= sweepLen;
                if (Physics.SphereCast(_lastCamPos, cameraRadius, sweepDir, out RaycastHit sweepHit, sweepLen, collisionLayers, QueryTriggerInteraction.Ignore))
                {
                    if (!IsIgnoredHit(sweepHit.transform))
                    {
                        desiredCamPos = sweepHit.point + sweepHit.normal * Mathf.Max(cameraClearance, kMinClearance);
                        lateralBlocked = true;

                        // יישור מרחק רדיאלי לפיבוט לאחר התיקון
                        usedDist = Vector3.Distance(focus, desiredCamPos);
                        float minAfter = ComputeMinAllowedDistance(rot, focus, usedDist);
                        if (usedDist < minAfter)
                        {
                            usedDist = minAfter;
                            Vector3 dirFromFocus = (desiredCamPos - focus).normalized;
                            desiredCamPos = focus + dirFromFocus * usedDist;
                        }
                    }
                }
            }
        }

        // 3) רצפה
        if (clampToFloor && desiredCamPos.y < floorY + floorPadding)
            desiredCamPos.y = floorY + floorPadding;

        // 4) Depenetration אמיתי: אם עדיין יש חפיפה—דחוף החוצה עד שאין חפיפה
        if (collisionEnabled)
            DepenetrateFallback(ref desiredCamPos);

        // 5) הצבה
        transform.SetPositionAndRotation(desiredCamPos, rot);

        // 6) Zoom-out אוטומטי רק כשחסום לרוחב (רגישות מוגדרת)
        if (collisionEnabled && autoZoomOutOnLateralBlock && lateralBlocked)
            _targetDist = Mathf.Clamp(_targetDist + lateralZoomOutSpeed * Time.deltaTime, minDistance, maxDistance);

        _lastCamPos = desiredCamPos;
    }

    // ===== Collision helpers =====

    // מרחק מינימלי מותר מן הפיבוט לכיוון המצלמה (ללא חדירה), כולל Clearance (>=0.5f).
    float ComputeMinAllowedDistance(Quaternion rot, Vector3 focus, float checkDistance)
    {
        Vector3 dir = rot * Vector3.back; // מהפיבוט אל המצלמה
        float minAllowed = minDistance;

        // A) פני השטח הראשונים בדרך החוצה
        if (Physics.SphereCast(focus, cameraRadius, dir, out RaycastHit hit, checkDistance, collisionLayers, QueryTriggerInteraction.Ignore))
        {
            if (!IsIgnoredHit(hit.transform))
                minAllowed = Mathf.Max(minAllowed, hit.distance + Mathf.Max(cameraClearance, kMinClearance));
        }

        // B) אם המצלמה כבר בתוך משהו: נבדוק חזרה לפיבוט
        Vector3 camPos = focus + dir * checkDistance;
        Vector3 backDir = (focus - camPos).normalized;
        if (Physics.Raycast(camPos, backDir, out RaycastHit backHit, checkDistance, collisionLayers, QueryTriggerInteraction.Ignore))
        {
            if (!IsIgnoredHit(backHit.transform))
            {
                // כמה עלינו להתרחק ביחס לפיבוט כדי להיות אחרי המחסום + Clearance
                float allowed = Mathf.Max(minDistance, (checkDistance - backHit.distance) + Mathf.Max(cameraClearance, kMinClearance));
                if (allowed > minAllowed) minAllowed = allowed;
            }
        }

        return minAllowed;
    }

    // דה-פנטרציה סופית בלי קוליידר פיזי על המצלמה: OverlapSphere + ClosestPoint
    void DepenetrateFallback(ref Vector3 camPos)
    {
        const int MaxIters = 3;
        Collider[] overlaps = new Collider[16];
        float r = cameraRadius;
        float clearance = Mathf.Max(cameraClearance, kMinClearance);

        for (int iter = 0; iter < MaxIters; iter++)
        {
            int count = Physics.OverlapSphereNonAlloc(camPos, r, overlaps, collisionLayers, QueryTriggerInteraction.Ignore);
            bool any = false;

            for (int i = 0; i < count; i++)
            {
                var col = overlaps[i];
                if (col == null || IsIgnoredHit(col.transform)) continue;

                Vector3 closest = col.ClosestPoint(camPos);
                Vector3 delta = camPos - closest;
                float d = delta.magnitude;

                if (d < r - 1e-5f) // בתוך הרדיוס
                {
                    Vector3 pushDir = (d > 1e-6f) ? (delta / d) : Vector3.up;
                    float pushDist = (r - d) + clearance;
                    camPos += pushDir * pushDist;
                    any = true;
                }
            }
            if (!any) break;
        }
    }

    bool IsIgnoredHit(Transform hitT)
    {
        if (hitT == null) return false;

        if (ignoreTargetColliders && target != null && hitT.IsChildOf(target))
            return true;

        if (collisionIgnoreRoots != null)
        {
            for (int i = 0; i < collisionIgnoreRoots.Length; i++)
            {
                var r = collisionIgnoreRoots[i];
                if (r != null && hitT.IsChildOf(r))
                    return true;
            }
        }
        return false;
    }

    // ===== Presets / Utils =====

    void UpdateCameraImmediate()
    {
        _yaw = _targetYaw;
        _pitch = _targetPitch;
        distance = _targetDist;

        Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 focus = target.position + targetOffset;
        Vector3 camPos = focus + rot * new Vector3(0f, 0f, -distance);
        transform.SetPositionAndRotation(camPos, rot);
    }

    public void SetViewIndex(int index)
    {
        if (viewpoints == null || viewpoints.Length == 0) return;
        index = Mathf.Clamp(index, 0, viewpoints.Length - 1);
        SetView(viewpoints[index], viewBlendTime);
    }

    public void NextView()
    {
        if (viewpoints == null || viewpoints.Length == 0) return;
        int current = FindClosestViewIndex();
        SetViewIndex((current + 1) % viewpoints.Length);
    }

    public void PrevView()
    {
        if (viewpoints == null || viewpoints.Length == 0) return;
        int current = FindClosestViewIndex();
        int next = current - 1; if (next < 0) next = viewpoints.Length - 1;
        SetViewIndex(next);
    }

    public void SetView(Transform view, float blendTime = 0.5f)
    {
        if (view == null) return;
        StopAllCoroutines();
        StartCoroutine(BlendToView(view, Mathf.Max(0f, blendTime)));
    }

    IEnumerator BlendToView(Transform view, float t)
    {
        Vector3 focus = target.position + targetOffset;

        Vector3 toCam = view.position - focus;
        float targetDist = Mathf.Clamp(toCam.magnitude, minDistance, maxDistance);

        Quaternion lookRot = Quaternion.LookRotation(focus - view.position, Vector3.up);
        Vector3 eul = lookRot.eulerAngles;
        float destPitch = ClampAngle(eul.x, yMinLimit, yMaxLimit);
        float destYaw = eul.y;

        float startYaw = _targetYaw, startPitch = _targetPitch, startDist = _targetDist;

        float elapsed = 0f;
        while (elapsed < t)
        {
            float a = (t <= 0f) ? 1f : (elapsed / t);
            _targetYaw = Mathf.LerpAngle(startYaw, destYaw, a);
            _targetPitch = Mathf.LerpAngle(startPitch, destPitch, a);
            _targetDist = Mathf.Lerp(startDist, targetDist, a);
            UpdateCameraImmediate();
            elapsed += Time.deltaTime;
            yield return null;
        }

        _targetYaw = destYaw;
        _targetPitch = destPitch;
        _targetDist = targetDist;
        UpdateCameraImmediate();
    }

    int FindClosestViewIndex()
    {
        if (viewpoints == null || viewpoints.Length == 0) return 0;
        float best = float.MaxValue; int bestIdx = 0;
        for (int i = 0; i < viewpoints.Length; i++)
        {
            if (viewpoints[i] == null) continue;
            float d = Vector3.Distance(transform.position, viewpoints[i].position);
            if (d < best) { best = d; bestIdx = i; }
        }
        return bestIdx;
    }

    [ContextMenu("Reset View Now")]
    public void ResetView()
    {
        _targetYaw = _startYaw;
        _targetPitch = _startPitch;
        _targetDist = _startDist;
        UpdateCameraImmediate();
        _lastCamPos = transform.position;
    }

    static float ClampAngle(float angle, float min, float max)
    {
        angle = Mathf.Repeat(angle + 180f, 360f) - 180f;
        return Mathf.Clamp(angle, min, max);
    }

    // API להחלפת פיבוט בזמן ריצה
    public void AssignTarget(Transform newTarget, Vector3? newOffset = null)
    {
        target = newTarget;
        if (newOffset.HasValue) targetOffset = newOffset.Value;
        UpdateCameraImmediate();
        _lastCamPos = transform.position;
    }
    
    bool IsPointerOverUI()
    {
        if (!lockWhenOverUI) return false;    // נשתמש גם בדגל הקיים שלך
        if (EventSystem.current == null) return false;

        // עכבר
        if (EventSystem.current.IsPointerOverGameObject())
            return true;

        // מגע (אנדרואיד/iOS)
        for (int i = 0; i < Input.touchCount; i++)
            if (EventSystem.current.IsPointerOverGameObject(Input.GetTouch(i).fingerId))
                return true;

        return false;
    }
}