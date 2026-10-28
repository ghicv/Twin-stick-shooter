using UnityEngine;

// Background music: the menu's track while on the menus, the game's track in a game (none = quiet). Changing
// track fades the old one out and the new one in. Follows the music volume of the settings (GameAudio).
[RequireComponent(typeof(AudioSource))]
public class MusicPlayer : MonoBehaviour
{
    [SerializeField] private AudioClip menuMusic;

    [Tooltip("Optional: plays in single player and LAN games.")]
    [SerializeField] private AudioClip gameMusic;

    [Tooltip("The music's own loudness, before the player's music volume.")]
    [Range(0f, 1f)]
    [SerializeField] private float level = 0.7f;

    [SerializeField] private float fadeTime = 1f;

    private AudioSource source;
    private float fade; // 0 = silent, 1 = full

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
    }

    private void Update()
    {
        AudioClip wanted = GameMode.Current == GameMode.Mode.None ? menuMusic : gameMusic;
        float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeTime);
        if (source.clip != wanted)
        {
            fade = Mathf.MoveTowards(fade, 0f, step); // the old track fades out first
            if (fade <= 0f || !source.isPlaying)
            {
                source.clip = wanted;
                if (wanted != null)
                    source.Play();
            }
        }
        else
        {
            fade = Mathf.MoveTowards(fade, 1f, step);
        }
        source.volume = level * GameAudio.MusicVolume * fade;
    }
}
