using UnityEngine;

// A platform that is there for Solid Time, blinks for its last Warn Time, then is gone (a faint outline, nothing to
// stand on) for Gone Time, again and again. It follows the MapClock, so it comes and goes at the same moment on
// every machine.
public class GhostPlatform : MonoBehaviour
{
    [Min(0.1f)]
    [SerializeField] private float solidTime = 4f;

    [Min(0.1f)]
    [SerializeField] private float goneTime = 2f;

    [Tooltip("The last seconds of Solid Time, when it blinks.")]
    [Min(0f)]
    [SerializeField] private float warnTime = 1f;

    [Tooltip("Where in the cycle it starts (0..1). Two groups half a cycle apart take turns.")]
    [Range(0f, 1f)]
    [SerializeField] private float phase;

    [Tooltip("How see-through it is while gone.")]
    [Range(0f, 1f)]
    [SerializeField] private float goneAlpha = 0.15f;

    [SerializeField] private float blinkSpeed = 10f;

    private Collider2D solid;
    private SpriteRenderer[] sprites;

    private void Awake()
    {
        solid = GetComponent<Collider2D>();
        sprites = GetComponentsInChildren<SpriteRenderer>();
    }

    private void FixedUpdate()
    {
        float cycle = solidTime + goneTime;
        float time = Mathf.Repeat(MapClock.Time + phase * cycle, cycle);
        bool there = time < solidTime;
        solid.enabled = there;

        float alpha = there ? 1f : goneAlpha;
        if (there && time > solidTime - warnTime)
            alpha = Mathf.Repeat(time * blinkSpeed, 1f) < 0.5f ? 1f : 0.4f;
        foreach (SpriteRenderer sprite in sprites)
        {
            Color color = sprite.color;
            color.a = alpha;
            sprite.color = color;
        }
    }
}
