using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The Draw the Sword core (diamond): the gun becomes a sword. Left Mouse slashes toward the aim, a wide arc in front:
// a heavy hit with a big push on whatever is in it, crates and barrels too, and every bullet in it that isn't this
// player's is cut to pieces. Pure melee: no shots (the bullet cores wait). A special gun picked up is used instead
// until its ammo runs out.
// The owner swings at once; the host decides the hits (single player: this machine). Every machine shows the swing
// and cuts its own bullets (the host's real ones, the others' look-alikes).
public class PlayerSword : MonoBehaviour
{
    [Header("Look")]
    [Tooltip("GunPivot: turns with the aim. The sword is held there.")]
    [SerializeField] private Transform pivot;

    [Tooltip("The gun's sprite, hidden while the sword is out.")]
    [SerializeField] private SpriteRenderer gunSprite;

    [SerializeField] private Sprite swordSprite;

    [Tooltip("The white crescent left by a slash.")]
    [SerializeField] private Sprite slashSprite;

    [Tooltip("Sparks where a bullet is cut.")]
    [SerializeField] private ParticleSystem cutEffect;

    [Header("Slash")]
    [SerializeField] private float damage = 25f;

    [Tooltip("Push on whoever is hit, as a multiplier of a bullet's.")]
    [SerializeField] private float knockback = 1.8f;

    [Tooltip("Reach from the hands (units) and how wide the arc is (degrees).")]
    [SerializeField] private float range = 1.9f;
    [SerializeField] private float arc = 150f;

    [Tooltip("Seconds from one slash to the next (holding Left Mouse keeps slashing).")]
    [SerializeField] private float cooldown = 0.25f;

    [Tooltip("Seconds the swing of the blade takes, and the crescent stays.")]
    [SerializeField] private float swingTime = 0.1f;
    [SerializeField] private float slashLife = 0.16f;

    [Tooltip("Push on crates hit by a slash.")]
    [SerializeField] private float propPush = 6f;

    [Header("Sound")]
    [SerializeField] private AudioClip swooshSound;
    [Range(0f, 1f)]
    [SerializeField] private float swooshVolume = 0.5f;

    [Tooltip("A bullet cut in two.")]
    [SerializeField] private AudioClip clangSound;
    [Range(0f, 1f)]
    [SerializeField] private float clangVolume = 0.55f;

    [Tooltip("The sword coming out (the core was taken, or a special gun ran out).")]
    [SerializeField] private AudioClip drawSound;
    [Range(0f, 1f)]
    [SerializeField] private float drawVolume = 0.5f;

    private PlayerNetwork net;
    private Rigidbody2D rb;
    private HitTarget self;
    private CameraShake cameraShake;
    private SpriteRenderer sword;
    private float nextSlash;
    private float swingTimer;
    private int swingSide = 1;
    private bool shown;
    private readonly List<HitTarget> targets = new List<HitTarget>();
    private readonly List<Collider2D> nearby = new List<Collider2D>();
    private static readonly ContactFilter2D Solid = new ContactFilter2D { useTriggers = false };

    // The sword is out: the core is owned and no special gun is held.
    public bool Active => net != null && net.Cores != null && net.Cores.Inventory.Has(CoreType.DrawSword) && net.Gun == 0;

    private Vector2 Hands => pivot.position;

    private void Awake()
    {
        net = GetComponent<PlayerNetwork>();
        rb = GetComponent<Rigidbody2D>();
        self = GetComponent<HitTarget>();
        cameraShake = Camera.main != null ? Camera.main.GetComponent<CameraShake>() : null;
        sword = new GameObject("Sword").AddComponent<SpriteRenderer>();
        sword.transform.SetParent(pivot, false);
        sword.transform.localPosition = gunSprite.transform.parent == pivot ? gunSprite.transform.localPosition
                                                                            : gunSprite.transform.parent.localPosition;
        sword.sprite = swordSprite;
        sword.sortingLayerID = gunSprite.sortingLayerID;
        sword.sortingOrder = gunSprite.sortingOrder;
        sword.enabled = false;
    }

    private void Update()
    {
        bool active = Active && net.InPlay;
        if (active != shown)
        {
            shown = active;
            sword.enabled = active;
            gunSprite.enabled = !active;
            if (active)
                GameAudio.PlayAt(drawSound, Hands, drawVolume, 0.05f);
        }

        // The swing: the blade sweeps across the arc in a few steps, then comes back to rest.
        if (swingTimer > 0f)
        {
            swingTimer -= Time.deltaTime;
            float t = 1f - Mathf.Clamp01(swingTimer / swingTime);
            t = Mathf.Floor(t * 4f) / 4f; // stepped (retro)
            sword.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(80f, -80f, t) * swingSide);
        }
        else
        {
            sword.transform.localRotation = Quaternion.Euler(0f, 0f, 20f * swingSide); // resting where the last swing ended
        }
    }

    // Owner, Left Mouse held: slash when the sword is ready.
    public void Attack()
    {
        if (Time.time < nextSlash)
            return;
        nextSlash = Time.time + cooldown;
        net.Slash(pivot.eulerAngles.z);
    }

    // Owner, a dash (Shift) with the sword out: a slash as it starts and another as it ends, the dash carrying the
    // blade through whoever is in the way.
    public void DashSlash(Vector2 direction, float dashTime)
    {
        StartCoroutine(DashSlashes(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, dashTime));
    }

    private IEnumerator DashSlashes(float angle, float dashTime)
    {
        net.Slash(angle);
        yield return new WaitForSeconds(dashTime);
        net.Slash(angle);
        nextSlash = Time.time + cooldown;
    }

    // Every machine: the swing, the crescent and the sound; this machine's bullets in the arc are cut.
    public void PlaySlash(float angle)
    {
        swingSide = -swingSide; // up, then down, then up...
        swingTimer = swingTime;
        StartCoroutine(Crescent(angle, swingSide));
        bool mine = net == null || net.IsMine;
        GameAudio.PlayAt(swooshSound, Hands, swooshVolume * (mine ? 1f : 0.6f), 0.12f);
        if (mine && cameraShake != null)
            cameraShake.Shake(0.06f, 0.06f);

        bool cut = false;
        foreach (Projectile bullet in FindObjectsByType<Projectile>(FindObjectsSortMode.None))
        {
            if (bullet.Shooter == rb || !InArc(bullet.Body.position, angle))
                continue;
            if (cutEffect != null)
                Destroy(Instantiate(cutEffect, bullet.Body.position, Quaternion.Euler(0f, 0f, angle)).gameObject, 2f);
            bullet.Shatter();
            cut = true;
        }
        if (cut)
        {
            GameAudio.PlayAt(clangSound, Hands, clangVolume, 0.1f);
            if (mine && cameraShake != null)
                cameraShake.Shake(0.12f, 0.08f);
        }
    }

    // Host (single player: this machine): whatever is in the arc is hit hard and pushed; crates fly, barrels go off.
    public void SlashHits(float angle)
    {
        Vector2 hands = Hands;
        targets.Clear();
        targets.AddRange(HitTarget.Active); // a kill takes a target off the list
        foreach (HitTarget target in targets)
        {
            if (target == null || target == self || target.IsDying)
                continue;
            Collider2D body = target.GetComponentInChildren<Collider2D>();
            Vector2 point = body != null ? body.ClosestPoint(hands) : (Vector2)target.transform.position;
            if (!InArc(point, angle))
                continue;
            Vector2 away = (point - hands).sqrMagnitude > 0.0001f ? (point - hands).normalized : (Vector2)(Quaternion.Euler(0f, 0f, angle) * Vector3.right);
            target.TakeDamage(damage, point, -away, knockback, net.Cores);
        }

        Physics2D.OverlapCircle(hands, range, Solid, nearby);
        foreach (Collider2D hit in nearby)
        {
            if (hit.GetComponentInParent<HitTarget>() != null || !InArc(hit.ClosestPoint(hands), angle))
                continue;
            if (hit.TryGetComponent(out ExplosiveBarrel barrel))
                barrel.Hit(damage);
            Rigidbody2D loose = hit.attachedRigidbody;
            if (loose != null && loose.bodyType == RigidbodyType2D.Dynamic)
                loose.AddForce(((Vector2)loose.position - hands).normalized * propPush * loose.mass, ForceMode2D.Impulse);
        }
    }

    // In reach and inside the arc around the aim (anything right at the hands counts).
    private bool InArc(Vector2 point, float angle)
    {
        Vector2 offset = point - Hands;
        float distance = offset.magnitude;
        if (distance > range)
            return false;
        if (distance < 0.5f)
            return true;
        return Mathf.Abs(Mathf.DeltaAngle(angle, Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg)) <= arc * 0.5f;
    }

    // The white crescent of the slash, tinted with the player's color, fading out in steps.
    private IEnumerator Crescent(float angle, int side)
    {
        var slash = new GameObject("Slash").AddComponent<SpriteRenderer>();
        slash.sprite = slashSprite;
        slash.sortingOrder = 8;
        slash.flipY = side < 0;
        slash.transform.SetPositionAndRotation(Hands, Quaternion.Euler(0f, 0f, angle));
        Color tint = Color.Lerp(Color.white, net != null ? net.Color : Color.white, 0.35f);
        Destroy(slash.gameObject, slashLife);
        for (float age = 0f; age < slashLife && slash != null; age += Time.deltaTime)
        {
            float t = age / slashLife;
            slash.transform.position = Hands;
            slash.transform.localScale = Vector3.one * range * 0.9f * (t < 0.25f ? 0.85f : 1f); // the crescent reaches about the blade's reach
            tint.a = Mathf.Ceil((1f - t) * 3f) / 3f;
            slash.color = tint;
            yield return null;
        }
    }
}
