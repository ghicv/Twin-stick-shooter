using UnityEngine;

// A black hole a bullet left where it stopped (Black Hole core, made by BulletCores): a dark core in a pulsing purple
// ring that pulls targets and loose things (crates, barrels) in for a moment, then collapses with a small blast.
// It spins and pulses on every machine; only the host's pulls and blast count (a look-alike just shows them).
public class CoreBlackHole : MonoBehaviour
{
    private BulletCores owner;
    private HitTarget ownerTarget;
    private Transform ring;
    private float timeLeft;
    private float lifetime;
    private float radius;
    private float pull;
    private float pullTimer;
    private bool lookAlike;

    private const float PullInterval = 0.1f;

    public void Setup(BulletCores owner, HitTarget ownerTarget, Transform ring, float lifetime, float radius, float pull, bool lookAlike)
    {
        this.owner = owner;
        this.ownerTarget = ownerTarget;
        this.ring = ring;
        this.lifetime = lifetime;
        timeLeft = lifetime;
        this.radius = radius;
        this.pull = pull;
        this.lookAlike = lookAlike;
    }

    private void Update()
    {
        timeLeft -= Time.deltaTime;

        // Spins in 45° steps and pulses; shrinks at the very end.
        float age = lifetime - timeLeft;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Floor(age * 16f) * 45f);
        float grow = Mathf.Min(1f, age * 6f) * Mathf.Min(1f, timeLeft * 5f);
        ring.localScale = Vector3.one * (Mathf.Repeat(age * 6f, 1f) < 0.5f ? 1f : 1.15f);
        transform.localScale = Vector3.one * grow;

        pullTimer -= Time.deltaTime;
        if (!lookAlike && pullTimer <= 0f)
        {
            pullTimer = PullInterval;
            Pull();
        }
        if (timeLeft <= 0f)
        {
            owner.BlackHoleCollapse(transform.position, lookAlike);
            Destroy(gameObject);
        }
    }

    // Host: everything in range gets a tug toward the middle (stronger nearer).
    private void Pull()
    {
        Vector2 center = transform.position;
        foreach (HitTarget target in HitTarget.Active)
        {
            if (target == ownerTarget || target.IsDying)
                continue;
            Vector2 toCenter = center - (Vector2)target.transform.position;
            float distance = toCenter.magnitude;
            if (distance < radius && distance > 0.2f)
                target.Knock(toCenter.normalized * pull * (1f - distance / radius * 0.5f), owner.Bridge);
        }
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(center, radius))
        {
            Rigidbody2D loose = hit.attachedRigidbody;
            if (loose != null && loose.bodyType == RigidbodyType2D.Dynamic && hit.GetComponentInParent<HitTarget>() == null)
                loose.AddForce((center - loose.position).normalized * pull * loose.mass * 0.5f, ForceMode2D.Impulse);
        }
    }
}
