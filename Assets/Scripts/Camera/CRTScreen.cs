using UnityEngine;

// Old-TV (CRT) look over the whole screen: curved glass, scanlines, red/green/blue phosphor columns,
// color fringing and darker corners. The drawing is done by the "CRT Screen" Full Screen Pass
// renderer feature on the URP renderer (shader Assets/Shaders/CRTScreen.shader); this component only
// sends the values below to it. Intensity 0 = normal picture (the default); PlayerRespawn turns it up
// during the respawn transition. Put it on the main camera.
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

    public float Intensity
    {
        get => intensity;
        set => intensity = Mathf.Clamp01(value);
    }

    private static readonly int IntensityId = Shader.PropertyToID("_CRT_Intensity");
    private static readonly int CurvatureId = Shader.PropertyToID("_CRT_Curvature");
    private static readonly int ScanlineCountId = Shader.PropertyToID("_CRT_ScanlineCount");
    private static readonly int ScanlineDarknessId = Shader.PropertyToID("_CRT_ScanlineDarkness");
    private static readonly int MaskStrengthId = Shader.PropertyToID("_CRT_MaskStrength");
    private static readonly int ColorFringeId = Shader.PropertyToID("_CRT_ColorFringe");
    private static readonly int VignetteId = Shader.PropertyToID("_CRT_Vignette");

    private void LateUpdate()
    {
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
    }
}
