using UnityEngine;
using UnityEngine.Rendering.Universal;

// The EFFECTS setting (SettingsMenu), remembered on this PC: on = the camera's post-processing in a game (bloom,
// motion blur from the Global Volume) and the impact blur (CRTScreen). Off = a plain picture for slow PCs. The menu
// always post-processes (its blurred backdrop, MenuBackdrop).
public static class EffectsSetting
{
    private const string Key = "Effects";

    public static bool On
    {
        get => PlayerPrefs.GetInt(Key, 1) == 1;
        set
        {
            PlayerPrefs.SetInt(Key, value ? 1 : 0);
            Apply(false);
        }
    }

    // Menu = the menu is showing (it needs post-processing for its blur whatever the setting).
    public static void Apply(bool menu)
    {
        Camera camera = Camera.main;
        UniversalAdditionalCameraData cameraData = camera != null ? camera.GetComponent<UniversalAdditionalCameraData>() : null;
        if (cameraData != null)
            cameraData.renderPostProcessing = menu || On;
    }
}
