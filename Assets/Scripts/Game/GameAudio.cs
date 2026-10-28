using System.Collections.Generic;
using UnityEngine;

// Every game sound goes through here (the menus' clicks too, see UiAudio):
// - Two volumes, remembered on this PC: sound effects and music (the settings menu changes them).
// - Nothing piles up: one sound plays at most Max Repeats times within Repeat Window seconds (many bullets hitting
//   walls at once make a few clicks, not a buzz).
// - Sounds have a place: panned left / right by where they happen on screen, and quieter the farther they are from
//   the middle of the camera.
// Made by itself the first time a sound plays, and kept across scene reloads.
public class GameAudio : MonoBehaviour
{
    private const int Voices = 16;
    private const int MaxRepeats = 3;
    private const float RepeatWindow = 0.1f;

    // Distance from the camera's middle (units): full volume up to Near, Far Volume from Far on.
    private const float Near = 6f;
    private const float Far = 22f;
    private const float FarVolume = 0.35f;

    // How far left / right a sound at the screen's edge is panned (0 = no panning, 1 = fully).
    private const float PanAmount = 0.7f;

    private const string SfxKey = "SfxVolume";
    private const string MusicKey = "MusicVolume";

    private static GameAudio instance;

    private AudioSource[] voices;
    private int nextVoice;
    private readonly Dictionary<AudioClip, List<float>> recent = new Dictionary<AudioClip, List<float>>();

    // Music players listen to this to follow the music volume.
    public static event System.Action MusicVolumeChanged;

    public static float SfxVolume
    {
        get => PlayerPrefs.GetFloat(SfxKey, 0.8f);
        set => PlayerPrefs.SetFloat(SfxKey, Mathf.Clamp01(value));
    }

    public static float MusicVolume
    {
        get => PlayerPrefs.GetFloat(MusicKey, 0.6f);
        set
        {
            PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value));
            MusicVolumeChanged?.Invoke();
        }
    }

    private static GameAudio Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("GameAudio");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<GameAudio>();
            }
            return instance;
        }
    }

    private void Awake()
    {
        voices = new AudioSource[Voices];
        for (int i = 0; i < Voices; i++)
        {
            voices[i] = gameObject.AddComponent<AudioSource>();
            voices[i].playOnAwake = false;
            voices[i].spatialBlend = 0f;
        }
    }

    // A sound with no place (it is about this player: its own jump, its own hit landing...).
    public static void Play(AudioClip clip, float volume, float pitchVariation = 0f, float pitch = 1f)
    {
        if (clip != null)
            Instance.Emit(clip, volume, pitch, pitchVariation, 0f);
    }

    // A sound that happens somewhere in the world.
    public static void PlayAt(AudioClip clip, Vector2 position, float volume, float pitchVariation = 0f, float pitch = 1f)
    {
        if (clip == null)
            return;
        Camera camera = Camera.main;
        float pan = 0f;
        if (camera != null)
        {
            float x = camera.WorldToViewportPoint(position).x;
            pan = Mathf.Clamp((x - 0.5f) * 2f, -1f, 1f) * PanAmount;
            float distance = Vector2.Distance(position, camera.transform.position);
            volume *= Mathf.Lerp(1f, FarVolume, Mathf.InverseLerp(Near, Far, distance));
        }
        Instance.Emit(clip, volume, pitch, pitchVariation, pan);
    }

    private void Emit(AudioClip clip, float volume, float pitch, float pitchVariation, float pan)
    {
        // At most Max Repeats of one clip in a short while.
        if (!recent.TryGetValue(clip, out List<float> times))
        {
            times = new List<float>();
            recent[clip] = times;
        }
        float now = Time.unscaledTime;
        times.RemoveAll(t => now - t > RepeatWindow);
        if (times.Count >= MaxRepeats)
            return;
        times.Add(now);

        AudioSource voice = voices[nextVoice];
        nextVoice = (nextVoice + 1) % voices.Length; // the oldest sound gives way when all voices are busy
        voice.clip = clip;
        voice.volume = volume * SfxVolume;
        voice.pitch = pitch + Random.Range(-pitchVariation, pitchVariation);
        voice.panStereo = pan;
        voice.Play();
    }
}
