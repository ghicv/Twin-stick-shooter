using UnityEngine;

// Plays a sound where this object appears (an effect prefab, e.g. an explosion), through GameAudio so the sound
// effects volume and the no-pile-up rule apply.
public class SoundOnSpawn : MonoBehaviour
{
    [SerializeField] private AudioClip clip;

    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.6f;

    [SerializeField] private float pitchVariation = 0.08f;

    private void Start()
    {
        GameAudio.PlayAt(clip, transform.position, volume, pitchVariation);
    }
}
