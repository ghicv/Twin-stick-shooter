using UnityEngine;

// Old-TV (CRT) look over the whole screen: curved glass, scanlines, red/green/blue phosphor columns,
// color fringing and darker corners. The drawing is done by the "CRT Screen" Full Screen Pass
// renderer feature on the URP renderer (shader Assets/Shaders/CRTScreen.shader); this component only
// sends the values below to it. Intensity 0 = normal picture (the default); PlayerRespawn turns it up
// during the respawn transition. Put it on the main camera.
// It also plays the impact blur (Blur: a short radial smear from a point), drawn by the same pass.
public class CRTScreen : MonoBehaviour
{
    [Tooltip("0 = normal picture, 1 = full CRT effect. PlayerRespawn drives it; drag it in Play Mode to preview the look.")]
    [Range(0f, 1f)]
    [SerializeField] private float intensity;

    [Header("Look (at full intensity)")]
    [Tooltip("How much the picture bulges like curved glass. The area outside the tube is black.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float curvature = 0.12f;

    [Tooltip("Number of scanlines from the top to the bottom of the screen.")]
    [SerializeField] private float scanlineCount = 360f;

    [Tooltip("How dark the gaps between scanlines are (0 = no scanlines, 1 = black gaps).")]
    [Range(0f, 1f)]
    [SerializeField] private float scanlineDarkness = 0.5f;

    [Tooltip("Strength of the red/green/blue phosphor columns.")]
    [Range(0f, 1f)]
    [SerializeField] private float maskStrength = 0.18f;

    [Tooltip("How far red and blue are shifted sideways (share of the screen width).")]
    [Range(0f, 0.01f)]
    [SerializeField] private float colorFringe = 0.0016f;

    [Tooltip("Darkening toward the corners.")]
    [Range(0f, 1f)]
    [SerializeField] private float vignette = 0.55f;

    [Header("Impact blur")]
    [Tooltip("Seconds an impact blur takes to fade out.")]
    [SerializeField] private float blurTime = 0.25f;

    [Tooltip("Impact blurs are this strong at most (share of the way to the center).")]
    [Range(0f, 0.3f)]
    [SerializeField] private float maxBlur = 0.08f;

    private static CRTScreen instance;
    private float blur;
    private Vector2 blurCenter = new Vector2(0.5f, 0.5f);

    public float Intensity
    {
        get => intensity;
        set => intensity = Mathf.Clamp01(value);
    }

    // A short radial smear out of that point of the world (a blast, a death, a hard landing). Strength 1 = the
    // strongest; a weaker one doesn't cut a stronger one short.
    public static void Blur(Vector2 worldPosition, float strength)
    {
        if (instance == null || !EffectsSetting.On)
            return;
        float wanted = Mathf.Clamp01(strength) * instance.maxBlur;
        if (wanted < instance.blur)
            return;
        instance.blur = wanted;
        instance.blurCenter = Camera.main.WorldToViewportPoint(worldPosition);
    }

    private void Awake()
    {
        instance = this;
    }

    private static readonly int IntensityId = Shader.PropertyToID("_CRT_Intensity");
    private static readonly int CurvatureId = Shader.PropertyToID("_CRT_Curvature");
    private static readonly int ScanlineCountId = Shader.PropertyToID("_CRT_ScanlineCount");
    private static readonly int ScanlineDarknessId = Shader.PropertyToID("_CRT_ScanlineDarkness");
    private static readonly int MaskStrengthId = Shader.PropertyToID("_CRT_MaskStrength");
    private static readonly int ColorFringeId = Shader.PropertyToID("_CRT_ColorFringe");
    private static readonly int VignetteId = Shader.PropertyToID("_CRT_Vignette");
    private static readonly int BlurStrengthId = Shader.PropertyToID("_Blur_Strength");
    private static readonly int BlurCenterId = Shader.PropertyToID("_Blur_Center");

    private void LateUpdate()
    {
        blur = Mathf.MoveTowards(blur, 0f, maxBlur / blurTime * Time.unscaledDeltaTime);
        Shader.SetGlobalFloat(BlurStrengthId, blur);
        Shader.SetGlobalVector(BlurCenterId, blurCenter);

        Shader.SetGlobalFloat(IntensityId, intensity);
        Shader.SetGlobalFloat(CurvatureId, curvature);
        Shader.SetGlobalFloat(ScanlineCountId, scanlineCount);
        Shader.SetGlobalFloat(ScanlineDarknessId, scanlineDarkness);
        Shader.SetGlobalFloat(MaskStrengthId, maskStrength);
        Shader.SetGlobalFloat(ColorFringeId, colorFringe);
        Shader.SetGlobalFloat(VignetteId, vignette);
    }

    // Leaving Play Mode (or removing the component) must not leave the screen stuck in CRT mode.
    private void OnDisable()
    {
        Shader.SetGlobalFloat(IntensityId, 0f);
        Shader.SetGlobalFloat(BlurStrengthId, 0f);
    }
}
