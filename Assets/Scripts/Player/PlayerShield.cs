using UnityEngine;

// A shield from a shield crate (LAN): a glowing bubble around the player that takes the damage first (Strength), and
// lasts Duration seconds at most. Hits on it flash it; when it runs out it shatters into pieces with a glassy crash.
// The host keeps the strength (PlayerNetwork.Shield) and the time; every machine shows the bubble from the strength.
public class PlayerShield : MonoBehaviour
{
    [SerializeField] private float strength = 50f;
    [SerializeField] private float duration = 8f;

    [Header("Look")]
    [Tooltip("A pixel bubble (ring with a see-through middle).")]
    [SerializeField] private Sprite bubbleSprite;
    [SerializeField] private Color color = new Color(0.45f, 0.9f, 1f, 0.75f);
    [Tooltip("Scale of the bubble sprite (2 units wide at 1): just around the body.")]
    [SerializeField] private float size = 0.9f;

    [Tooltip("A small square, for the pieces flying off when it shatters.")]
    [SerializeField] private Sprite pieceSprite;

    [SerializeField] private AudioClip raiseSound;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private AudioClip breakSound;
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.6f;

    private PlayerNetwork net;
    private SpriteRenderer bubble;
    private float timeLeft;   // host
    private float shown;      // the strength last shown here
    private float flash;

    private void Awake()
    {
        net = GetComponent<PlayerNetwork>();
        bubble = new GameObject("ShieldBubble").AddComponent<SpriteRenderer>();
        bubble.transform.SetParent(transform, false);
        bubble.transform.localPosition = Vector3.up * 0.1f;
        bubble.transform.localScale = Vector3.one * size;
        bubble.sprite = bubbleSprite;
        bubble.sortingOrder = 7;
        bubble.enabled = false;
    }

    // Host: a fresh shield.
    public void RaiseOnHost()
    {
        timeLeft = duration;
        net.SetShield(strength);
    }

    private void Update()
    {
        if (net.IsHostSide && net.Shield > 0f)
        {
            timeLeft -= Time.deltaTime;
            if (timeLeft <= 0f || net.Dead)
                net.SetShield(0f);
        }

        float now = net.Shield;
        if (now > shown && shown <= 0f)
            GameAudio.PlayAt(raiseSound, transform.position, volume, 0.05f);
        else if (now < shown && now > 0f)
        {
            flash = 0.12f;
            GameAudio.PlayAt(hitSound, transform.position, volume * 0.7f, 0.1f);
        }
        else if (now <= 0f && shown > 0f)
            Shatter();
        shown = now;

        bubble.enabled = now > 0f && net.InPlay;
        if (!bubble.enabled)
            return;
        flash -= Time.deltaTime;
        // Pulses in steps, flashes white when hit, fades as it weakens; blinks in its last 2 seconds (host's time
        // is not shared, so the others blink when it is nearly broken instead).
        bool pulse = Mathf.Repeat(Time.time * 3f, 1f) < 0.5f;
        Color c = flash > 0f ? Color.white : color;
        c.a *= Mathf.Lerp(0.45f, 1f, now / strength) * (pulse ? 1f : 0.8f);
        bool ending = net.IsHostSide ? timeLeft < 2f : now < strength * 0.25f;
        if (ending && Mathf.Repeat(Time.time * 8f, 1f) < 0.5f)
            c.a *= 0.3f;
        bubble.color = c;
        bubble.transform.localScale = Vector3.one * size * (flash > 0f ? 1.08f : 1f);
    }

    // Gone: pieces of the bubble fly off and fade.
    private void Shatter()
    {
        GameAudio.PlayAt(breakSound, transform.position, volume, 0.05f);
        for (int i = 0; i < 12; i++)
        {
            var piece = new GameObject("ShieldPiece").AddComponent<SpriteRenderer>();
            float a = i * Mathf.PI * 2f / 12f;
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            piece.transform.position = (Vector2)bubble.transform.position + dir * size * 0.45f;
            piece.transform.localScale = Vector3.one * 0.25f;
            piece.sprite = pieceSprite;
            piece.color = color;
            piece.sortingOrder = 8;
            var body = piece.gameObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 2f;
            body.linearVelocity = dir * Random.Range(4f, 7f) + Vector2.up * 2f;
            body.angularVelocity = Random.Range(-400f, 400f);
            Destroy(piece.gameObject, 0.6f);
        }
    }
}
