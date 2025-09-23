using UnityEngine;

public enum PreferredView
{
    AutoBroadside,      // ברירת מחדל: הצד הרחב של ה-Bounds
    LocalXPos, LocalXNeg,
    LocalYPos, LocalYNeg,
    LocalZPos, LocalZNeg
}

[DisallowMultipleComponent]
[AddComponentMenu("Camera/View Hint (Preferred View)")]
public class ViewHint : MonoBehaviour
{
    public PreferredView preferred = PreferredView.AutoBroadside;

    [Header("Editor Gizmo")]
    public bool showGizmo = true;

    void OnDrawGizmosSelected()
    {
        if (!showGizmo) return;

        Vector3 dirLocal = Vector3.back;
        switch (preferred)
        {
            case PreferredView.LocalXPos: dirLocal = Vector3.right;  break;
            case PreferredView.LocalXNeg: dirLocal = Vector3.left;   break;
            case PreferredView.LocalYPos: dirLocal = Vector3.up;     break;
            case PreferredView.LocalYNeg: dirLocal = Vector3.down;   break;
            case PreferredView.LocalZPos: dirLocal = Vector3.forward;break;
            case PreferredView.LocalZNeg: dirLocal = Vector3.back;   break;
        }

        Vector3 p = transform.position;
        Vector3 d = transform.TransformDirection(dirLocal).normalized * 0.8f;
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.9f);
        Gizmos.DrawLine(p, p + d);
        Vector3 right = Vector3.Cross(d.normalized, Vector3.up);
        Gizmos.DrawLine(p + d, p + d - (d.normalized * 0.15f) + right * 0.08f);
        Gizmos.DrawLine(p + d, p + d - (d.normalized * 0.15f) - right * 0.08f);
    }
}