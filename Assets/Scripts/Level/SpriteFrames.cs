using UnityEngine;

// Plays a loop of sprites on this SpriteRenderer, on the MapClock (e.g. a conveyor belt's moving tread).
// A negative speed plays the loop backwards.
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteFrames : MonoBehaviour
{
    [SerializeField] private Sprite[] frames;

    [Tooltip("Frames per second (negative = backwards).")]
    [SerializeField] private float framesPerSecond = 12f;

    private SpriteRenderer sprite;

    private void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        int frame = Mathf.FloorToInt(MapClock.Time * framesPerSecond);
        sprite.sprite = frames[(frame % frames.Length + frames.Length) % frames.Length];
    }
}
