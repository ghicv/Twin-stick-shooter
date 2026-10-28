using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// The RESOLUTION setting (SettingsMenu), remembered on this PC: the game picture is drawn at 100%, 75% or 50% of the
// screen and blown up with sharp (point) pixels, so pixel art stays crisp; a slow graphics card draws far fewer
// pixels. The UI is always drawn at full size. 50% of a 1080p screen is an exact 2x, the cleanest; 75% can make a few
// pixel columns a little wider than others.
public static class RenderScaleSetting
{
    private const string Key = "RenderScale";
    private static readonly float[] Levels = { 1f, 0.75f, 0.5f };

    public static float Scale => PlayerPrefs.GetFloat(Key, 1f);

    // 100% → 75% → 50% → 100%...
    public static void Next()
    {
        int current = System.Array.IndexOf(Levels, Scale);
        PlayerPrefs.SetFloat(Key, Levels[(current + 1) % Levels.Length]);
        Apply();
    }

    public static void Apply()
    {
        if (GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset pipeline)
            return;
        pipeline.upscalingFilter = UpscalingFilterSelection.Point;
        pipeline.renderScale = Scale;
#if UNITY_EDITOR
        // The pipeline is an asset: leaving Play Mode puts it back as saved.
        Application.quitting -= Restore;
        Application.quitting += Restore;
#endif
    }

#if UNITY_EDITOR
    private static void Restore()
    {
        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)
            pipeline.renderScale = 1f;
    }
#endif
}
