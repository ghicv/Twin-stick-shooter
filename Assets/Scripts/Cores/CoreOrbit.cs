using System.Collections.Generic;
using UnityEngine;

// Flying swords (Orbit core, made by SingleCores), like a cultivator's: three small glowing swords circle the player,
// points outward, cutting the bullets of others they touch. When a target comes within reach the swords fly off one
// after another, chase it, strike it and fly back to their place, then rest a moment. Every machine flies them (each
// picks the nearest target, the same everywhere); only the host's strikes hurt. Hidden while the player is out of
// play.
public class CoreOrbit : MonoBehaviour
{
    private const int SwordCount = 3;
    private const float SwordSize = 1f;
    private const float Reach = 7f;           // targets this close draw a sword
    private const float FlySpeed = 24f;
    private const float Turn = 900f;          // degrees per second while chasing
    private const float StrikeDistance = 0.45f;
    private const float MaxFlight = 1.2f;     // seconds chasing before giving up
    private const float LaunchGap = 0.25f;    // seconds between two swords flying off

    private enum Mode { Circling, Striking, Returning }

    private class Sword
    {
        public Transform body;
        public TrailRenderer trail;
        public Mode mode;
        public HitTarget target;
        public Vector2 velocity;
        public float rest;     // > 0: resting after a strike
        public float flight;   // seconds in the air
    }

    private SingleCores owner;
    private PlayerNetwork player;
    private HitTarget self;
    private Rigidbody2D shooter;
    private float radius;
    private float speed;
    private float damage;
    private float restTime;
    private Material trailMaterial;
    private float angle;
    private float launchTimer;
    private readonly List<Sword> swords = new List<Sword>();
    private readonly List<Collider2D> touching = new List<Collider2D>();
    private static readonly ContactFilter2D AnyCollider = new ContactFilter2D().NoFilter(); // triggers too

    public void Setup(SingleCores owner, PlayerNetwork player, HitTarget self, Sprite sprite, Color color, Material trailMaterial,
                      float radius, float speed, float damage, float restTime)
    {
        this.owner = owner;
        this.player = player;
        this.self = self;
        shooter = owner.GetComponent<Rigidbody2D>();
        this.radius = radius;
        this.speed = speed;
        this.damage = damage;
        this.restTime = restTime;
        this.trailMaterial = trailMaterial;
        for (int i = 0; i < SwordCount; i++)
        {
            var body = new GameObject("FlyingSword").AddComponent<SpriteRenderer>();
            body.transform.localScale = Vector3.one * SwordSize;
            body.sprite = sprite;
            body.color = color;
            body.sortingOrder = 6;
            var trail = body.gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = trailMaterial;
            trail.time = 0.18f;
            trail.startWidth = 0.14f;
            trail.endWidth = 0f;
            trail.startColor = new Color(color.r, color.g, color.b, 0.8f);
            trail.endColor = new Color(color.r, color.g, color.b, 0f);
            trail.sortingOrder = 5;
            trail.emitting = false;
            var sword = new Sword { body = body.transform, trail = trail };
            body.transform.position = Slot(i);
            swords.Add(sword);
        }
    }

    private void OnDestroy()
    {
        foreach (Sword sword in swords)
            if (sword.body != null)
                Destroy(sword.body.gameObject);
    }

    // Where sword i circles right now.
    private Vector2 Slot(int i) => (Vector2)transform.position + Direction(angle + i * 360f / SwordCount) * radius;

    private static Vector2 Direction(float degrees) => new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));

    private void Update()
    {
        bool shown = player == null || player.InPlay;
        float dt = Time.deltaTime;
        angle += speed * dt;
        launchTimer -= dt;
        HitTarget nearest = shown ? NearestTarget() : null;

        for (int i = 0; i < swords.Count; i++)
        {
            Sword sword = swords[i];
            sword.body.gameObject.SetActive(shown);
            sword.rest -= dt;
            if (!shown)
            {
                sword.mode = Mode.Circling;
                sword.body.position = Slot(i);
                continue;
            }

            switch (sword.mode)
            {
                case Mode.Circling:
                    float slotAngle = angle + i * 360f / SwordCount;
                    sword.body.SetPositionAndRotation(Slot(i), Quaternion.Euler(0f, 0f, slotAngle)); // point outward
                    sword.trail.emitting = false;
                    if (nearest != null && sword.rest <= 0f && launchTimer <= 0f)
                    {
                        launchTimer = LaunchGap;
                        sword.mode = Mode.Striking;
                        sword.target = nearest;
                        sword.flight = 0f;
                        sword.velocity = Direction(slotAngle) * FlySpeed * 0.5f; // leaves outward, then turns
                        sword.trail.Clear();
                        sword.trail.emitting = true;
                    }
                    break;

                case Mode.Striking:
                    sword.flight += dt;
                    if (sword.target == null || sword.target.IsDying || sword.flight > MaxFlight)
                    {
                        sword.mode = Mode.Returning;
                        break;
                    }
                    Vector2 aim = (Vector2)sword.target.transform.position + Vector2.up * 0.3f;
                    Fly(sword, aim, dt);
                    if (Vector2.Distance(sword.body.position, aim) < StrikeDistance)
                    {
                        Strike(sword);
                        sword.mode = Mode.Returning;
                        sword.rest = restTime;
                    }
                    break;

                case Mode.Returning:
                    Fly(sword, Slot(i), dt);
                    if (Vector2.Distance(sword.body.position, Slot(i)) < 0.3f)
                        sword.mode = Mode.Circling;
                    break;
            }
        }
    }

    // Turns toward the point at Turn degrees per second and flies on, pointing where it goes.
    private void Fly(Sword sword, Vector2 to, float dt)
    {
        Vector2 wanted = (to - (Vector2)sword.body.position).normalized * FlySpeed;
        sword.velocity = Vector3.RotateTowards(sword.velocity, wanted, Turn * Mathf.Deg2Rad * dt, FlySpeed * 4f * dt);
        sword.body.position += (Vector3)(sword.velocity * dt);
        if (sword.velocity.sqrMagnitude > 0.01f)
            sword.body.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(sword.velocity.y, sword.velocity.x) * Mathf.Rad2Deg);
    }

    private HitTarget NearestTarget()
    {
        HitTarget best = null;
        float bestDistance = Reach;
        foreach (HitTarget target in HitTarget.Active)
        {
            if (target == self || target.IsDying)
                continue;
            float distance = Vector2.Distance(target.transform.position, transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = target;
            }
        }
        return best;
    }

    // The strike: the host hurts the target (a push along the sword's flight); every machine shows the sparks.
    private void Strike(Sword sword)
    {
        Vector2 point = sword.body.position;
        Vector2 along = sword.velocity.normalized;
        for (int k = -1; k <= 1; k++) // a few white sparks flying on past the hit
            CoreEffects.Zap(trailMaterial, Color.white, point, point + (Vector2)(Quaternion.Euler(0f, 0f, k * 35f) * along) * 0.7f, 0.07f, 0.08f);
        if (player != null && !player.IsHostSide)
            return;
        sword.target.TakeDamage(damage, point, -sword.velocity.normalized, 0.8f, owner.Bridge);
    }

    // The swords cut the bullets of others they touch (each machine its own bullets).
    private void FixedUpdate()
    {
        if (player != null && !player.InPlay)
            return;
        foreach (Sword sword in swords)
        {
            Physics2D.OverlapCircle(sword.body.position, 0.3f, AnyCollider, touching);
            foreach (Collider2D hit in touching)
            {
                Projectile bullet = hit.GetComponent<Projectile>();
                if (bullet != null && bullet.Shooter != shooter)
                    bullet.Shatter();
            }
        }
    }
}
