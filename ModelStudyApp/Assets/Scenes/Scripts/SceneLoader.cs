using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneLoader : MonoBehaviour
{
    [Header("Optional Fade")]
    public CanvasGroup fadeGroup;     // גרור CanvasGroup שחור מלא-מסך (Image שחור)
    public float fadeTime = 0.35f;

    bool _busy;

    void Awake()
    {
        if (fadeGroup)
        {
            fadeGroup.alpha = 1f;
            StartCoroutine(Fade(0f, fadeTime));  // פייד-אין בכניסה לתפריט
        }
    }

    public void LoadByName(string sceneName)
    {
        if (_busy) return;
        StartCoroutine(LoadRoutine(sceneName));
    }

    public void LoadByIndex(int buildIndex)
    {
        if (_busy) return;
        StartCoroutine(LoadRoutine(buildIndex));
    }

    IEnumerator LoadRoutine(string sceneName)
    {
        _busy = true;
        if (fadeGroup) yield return Fade(1f, fadeTime);
        var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        yield return op; // המעבר יקרה בסוף ה־yield
    }

    IEnumerator LoadRoutine(int buildIndex)
    {
        _busy = true;
        if (fadeGroup) yield return Fade(1f, fadeTime);
        var op = SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single);
        yield return op;
    }

    IEnumerator Fade(float target, float t)
    {
        float start = fadeGroup.alpha;
        float e = 0f;
        while (e < t)
        {
            e += Time.unscaledDeltaTime;
            fadeGroup.alpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0,1,e/t));
            yield return null;
        }
        fadeGroup.alpha = target;
    }
}