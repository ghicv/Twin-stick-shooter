using UnityEngine;

// A spring on the ground: whatever touches it is thrown straight up. Each player is thrown by its own machine (the
// only one where it moves by physics), loose things (crates, barrels) by the host, so nothing is sent. Every machine
// squashes the spring when a player is on it.
[RequireComponent(typeof(Collider2D))]
public class JumpPad : MonoBehaviour
{
    [Tooltip("Upward speed it throws things at (units/sec).")]
    [SerializeField] private float launchSpeed = 24f;

    [Tooltip("The spring's picture (pivot at its bottom): squashed for a moment when used.")]
    [SerializeField] private Transform spring;

    [SerializeField] private AudioClip sound;
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.5f;

    private Collider2D area;
    private float squash; // 1 = just used, back to 0

    private void Awake()
    {
        area = GetComponent<Collider2D>();
        area.isTrigger = true;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        Rigidbody2D body = other.attachedRigidbody;
        if (body == null || body.bodyType != RigidbodyType2D.Dynamic || body.linearVelocity.y > launchSpeed * 0.5f)
            return; // not moved by physics on this machine, or already on its way up
        body.linearVelocity = new Vector2(body.linearVelocity.x, launchSpeed);
        Bounce();
    }

    private void Update()
    {
        // Another machine's player on it: only squash (it throws itself on its own machine).
        foreach (PlayerNetwork player in PlayerNetwork.All)
            if (!player.IsMine && player.InPlay && area.OverlapPoint(player.transform.position + Vector3.down * 0.6f) && squash <= 0f)
                Bounce();

        squash = Mathf.Max(0f, squash - Time.deltaTime * 5f);
        float height = squash > 0.6f ? 0.4f : squash > 0.2f ? 1.3f : 1f; // squash, overshoot, rest (stepped)
        spring.localScale = new Vector3(1f, height, 1f);
    }

    private void Bounce()
    {
        if (squash > 0.5f)
            return;
        squash = 1f;
        GameAudio.PlayAt(sound, transform.position, volume, 0.08f);
    }
}
