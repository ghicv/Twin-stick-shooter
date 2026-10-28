using System.Collections;
using Unity.Netcode;
using UnityEngine;

// A barrel the host puts on a map spot (MapProps): real physics on the host, like a crate. Real bullets (on the host)
// wear it down; at zero it blows up: everybody near is hurt and thrown (whoever shot it too), loose things fly, and
// other barrels near go off a moment later. Every machine shows the blast.
[RequireComponent(typeof(Rigidbody2D))]
public class ExplosiveBarrel : NetworkBehaviour
{
    [SerializeField] private float health = 30f;
    [SerializeField] private float radius = 2.6f;
    [SerializeField] private float damage = 45f;

    [Tooltip("Knockback multiplier for the players it hits.")]
    [SerializeField] private float knockback = 2f;

    [Tooltip("Push given to loose things (crates, other barrels) in the blast (impulse).")]
    [SerializeField] private float pushImpulse = 14f;

    [Tooltip("Seconds before another barrel caught in the blast goes off.")]
    [SerializeField] private float chainDelay = 0.15f;

    [Tooltip("Every hit makes it blink white (on the host only it counts; the blink shows everywhere).")]
    [SerializeField] private SpriteRenderer sprite;

    [Header("Blast")]
    [SerializeField] private ParticleSystem blastEffect;
    [SerializeField] private float effectScale = 2.5f;
    [SerializeField] private AudioClip blastSound;
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.8f;
    [SerializeField] private float shakeStrength = 0.35f;

    private bool exploded;
    private float blinkTimer;
    private MapManager maps;

    private void Awake()
    {
        maps = FindAnyObjectByType<MapManager>();
    }

    // Host: a real bullet hit it.
    public void Hit(float amount)
    {
        if (!IsServer || exploded)
            return;
        health -= amount;
        if (health <= 0f)
            Explode();
        else
            BlinkRpc();
    }

    private void Update()
    {
        blinkTimer -= Time.deltaTime;
        sprite.color = blinkTimer > 0f ? new Color(1f, 1f, 1f, 0.5f) : Color.white;
    }

    private void FixedUpdate()
    {
        if (IsServer && IsSpawned && !exploded && transform.position.y < maps.CurrentMap.FallLimitY)
            NetworkObject.Despawn();
    }

    [Rpc(SendTo.Everyone)]
    private void BlinkRpc() => blinkTimer = 0.08f;

    private void Explode()
    {
        exploded = true;
        Vector2 center = transform.position;
        BlastRpc(center);
        CoreEffects.Blast(null, 1f, center, radius, damage, knockback, null, true, null);
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(center, radius))
        {
            if (hit.TryGetComponent(out ExplosiveBarrel other) && other != this && !other.exploded)
                other.StartCoroutine(other.ExplodeSoon(chainDelay));
            Rigidbody2D loose = hit.attachedRigidbody;
            if (loose != null && loose.bodyType == RigidbodyType2D.Dynamic && hit.GetComponentInParent<PlayerNetwork>() == null)
                loose.AddForce(((Vector2)loose.position - center).normalized * pushImpulse + Vector2.up * pushImpulse * 0.5f, ForceMode2D.Impulse);
        }
        NetworkObject.Despawn();
    }

    private IEnumerator ExplodeSoon(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!exploded && IsSpawned)
            Explode();
    }

    [Rpc(SendTo.Everyone)]
    private void BlastRpc(Vector2 center)
    {
        CoreEffects.Blast(blastEffect, effectScale, center, 0f, 0f, 0f, null, false, null); // just the effect
        GameAudio.PlayAt(blastSound, center, volume, 0.05f);
        CameraShake shake = Camera.main.GetComponent<CameraShake>();
        if (shake != null)
            shake.Shake(shakeStrength, 0.25f);
    }
}
