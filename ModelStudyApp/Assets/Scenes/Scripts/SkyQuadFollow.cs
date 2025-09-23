using UnityEngine;

[ExecuteAlways]
public class SkyQuadFollow : MonoBehaviour
{
    public Camera cam;
    public float distance = 10f;   // מרחק מהמצלמה
    public float overscan = 1.05f; // קצת מעבר לשוליים כדי שלא ייחתך

    void LateUpdate()
    {
        if (!cam) cam = Camera.main;
        if (!cam) return;

        // מיקום וסיבוב זהים למצלמה, במרחק קבוע קדימה
        transform.position = cam.transform.position + cam.transform.forward * distance;
        transform.rotation = cam.transform.rotation;

        // סקלת הקוואד כדי למלא את הפריים
        float h = 2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float w = h * cam.aspect;
        transform.localScale = new Vector3(w * overscan, h * overscan, 1f);
    }
}