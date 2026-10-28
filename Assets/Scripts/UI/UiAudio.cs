using UnityEngine;

// The menus' sounds (one per scene, on the HUD): hover and click for buttons, and a soft pop for chat lines.
// Plays even while the game is paused.
[RequireComponent(typeof(AudioSource))]
public class UiAudio : MonoBehaviour
{
    public enum Sound { Hover, Click, Chat }

    [SerializeField] private AudioClip hover;
    [SerializeField] private AudioClip click;
    [SerializeField] private AudioClip chat;

    [Range(0f, 1f)]
    [SerializeField] private float hoverVolume = 0.25f;

    [Range(0f, 1f)]
    [SerializeField] private float clickVolume = 0.5f;

    [Range(0f, 1f)]
    [SerializeField] private float chatVolume = 0.35f;

    private static UiAudio instance;
    private AudioSource source;

    private void Awake()
    {
        instance = this;
        source = GetComponent<AudioSource>();
        source.ignoreListenerPause = true;
    }

    public static void Play(Sound sound)
    {
        if (instance == null)
            return;
        AudioClip clip = sound == Sound.Hover ? instance.hover : sound == Sound.Click ? instance.click : instance.chat;
        float volume = sound == Sound.Hover ? instance.hoverVolume : sound == Sound.Click ? instance.clickVolume : instance.chatVolume;
        if (clip != null)
            instance.source.PlayOneShot(clip, volume);
    }
}
