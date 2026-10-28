using UnityEngine;

// Behind the main menu: one of the game's maps, blurred (a post-processing volume) so the menu stays easy to read.
// On while the menu shows (this object turns on). When a game starts (the menu turns it off) the blur goes away and
// the camera keeps post-processing only if the EFFECTS setting is on (EffectsSetting).
public class MenuBackdrop : MonoBehaviour
{
    [SerializeField] private MapManager maps;

    [Tooltip("Index of the map shown behind the menu.")]
    [SerializeField] private int map = 0;

    [Tooltip("A post-processing volume that blurs the scene (the menu itself stays sharp).")]
    [SerializeField] private UnityEngine.Rendering.Volume blur;

    private void OnEnable()
    {
        maps.SetMap(map);
        SetBlur(true);
    }

    private void OnDisable()
    {
        SetBlur(false);
    }

    private void SetBlur(bool on)
    {
        if (blur != null)
            blur.enabled = on;
        EffectsSetting.Apply(on);
    }
}
