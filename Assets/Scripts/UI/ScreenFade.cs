using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// A black picture over the whole game screen that fades in and out (real time), used to hide map changes.
// It never blocks clicks.
public class ScreenFade : MonoBehaviour
{
    [Tooltip("Full-screen black image.")]
    [SerializeField] private Image image;

    [Tooltip("Seconds a fade to black (or back) takes.")]
    [SerializeField] private float fadeTime = 0.3f;

    public static ScreenFade Instance { get; private set; }

    public float FadeTime => fadeTime;

    private void Awake()
    {
        Instance = this;
        image.raycastTarget = false;
        SetAlpha(0f);
    }

    public IEnumerator FadeOut() => FadeTo(1f);
    public IEnumerator FadeIn() => FadeTo(0f);

    private IEnumerator FadeTo(float alpha)
    {
        float from = image.color.a;
        for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
        {
            SetAlpha(Mathf.Lerp(from, alpha, t / fadeTime));
            yield return null;
        }
        SetAlpha(alpha);
    }

    private void SetAlpha(float alpha)
    {
        Color color = image.color;
        image.color = new Color(color.r, color.g, color.b, alpha);
    }
}
