using System.Collections.Generic;
using UnityEngine;

// A black hole a bullet left where it stopped (Black Hole core, made by BulletCores): a dark core inside a whirling
// purple swirl (two layers turning opposite ways, in steps), with specks of light spiralling into it, that pulls
// targets and loose things (crates, barrels) in for a moment, then collapses with a small blast.
// It whirls on every machine. Each machine pulls its own player smoothly; enemies, loose things and the blast are
// the host's (a look-alike just shows them).
public class CoreBlackHole : MonoBehaviour
{
    private const float PullStep = 0.1f; // the pull's strength is given per this many seconds
    private const int SpeckCount = 14;
    private const float SpeckSize = 0.14f;
    private const float SwirlSize = 1.7f;

    private BulletCores owner;
    private HitTarget ownerTarget;
    private Transform swirl;
    private Transform innerSwirl;
    private Transform core;
    private float timeLeft;
    private float lifetime;
    private float radius;
    private float pull;
    private bool lookAlike;
    private readonly List<Transform> specks = new List<Transform>();
    private readonly List<Vector2> speckOrbits = new List<Vector2>(); // (angle in degrees, distance)
    private readonly List<Collider2D> nearby = new List<Collider2D>();
    private static readonly ContactFilter2D AnyCollider = new ContactFilter2D().NoFilter();

    public void Setup(BulletCores owner, HitTarget ownerTarget, Sprite swirlSprite, Sprite dotSprite, float lifetime,
                      float radius, float pull, bool lookAlike)
    {
        this.owner = owner;
        this.ownerTarget = ownerTarget;
        this.lifetime = lifetime;
        timeLeft = lifetime;
        this.radius = radius;
        this.pull = pull;
        this.lookAlike = lookAlike;

        swirl = Part("Swirl", swirlSprite, Color.white, SwirlSize, 4);
        innerSwirl = Part("Inner Swirl", swirlSprite, new Color(1f, 0.8f, 1f, 0.8f), SwirlSize * 0.55f, 5);
        core = Part("Core", dotSprite, new Color(0.04f, 0f, 0.08f), 0.9f, 6);
        for (int i = 0; i < SpeckCount; i++)
        {
            Color color = i % 3 == 0 ? Color.white : new Color(0.85f, 0.6f, 1f);
            specks.Add(Part("Speck", dotSprite, color, SpeckSize, 7));
            speckOrbits.Add(new Vector2(Random.Range(0f, 360f), Random.Range(0.3f, 1f) * radius * 0.8f));
        }
    }

    private Transform Part(string name, Sprite sprite, Color color, float size, int order)
    {
        var part = new GameObject(name).AddComponent<SpriteRenderer>();
        part.transform.SetParent(transform, false);
        part.transform.localScale = Vector3.one * size;
        part.sprite = sprite;
        part.color = color;
        part.sortingOrder = order;
        return part.transform;
    }

    private void Update()
    {
        timeLeft -= Time.deltaTime;
        float age = lifetime - timeLeft;

        // Grows in, whirls (in 15° steps: retro), the core throbs; everything shrinks away at the very end.
        float grow = Mathf.Min(1f, age * 6f) * Mathf.Min(1f, timeLeft * 5f);
        transform.localScale = Vector3.one * grow;
        swirl.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Floor(age * 24f) * 15f);
        innerSwirl.localRotation = Quaternion.Euler(0f, 0f, Mathf.Floor(age * 36f) * 15f + 60f);
        core.localScale = Vector3.one * (Mathf.Repeat(age * 6f, 1f) < 0.5f ? 0.9f : 1f);

        // Specks spiral in, faster near the middle, and start again at the edge.
        for (int i = 0; i < specks.Count; i++)
        {
            Vector2 orbit = speckOrbits[i];
            orbit.y -= Time.deltaTime * (1.2f + 2.5f / Mathf.Max(0.3f, orbit.y));
            orbit.x += Time.deltaTime * (200f + 500f / Mathf.Max(0.3f, orbit.y));
            if (orbit.y < 0.25f)
                orbit = new Vector2(Random.Range(0f, 360f), radius * 0.8f);
            speckOrbits[i] = orbit;
            float a = orbit.x * Mathf.Deg2Rad;
            specks[i].localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * orbit.y;
        }

        if (timeLeft <= 0f)
        {
            owner.BlackHoleCollapse(transform.position, lookAlike);
            Destroy(gameObject);
        }
    }

    // Every physics step, everything in range gets a small tug toward the middle (stronger nearer): smooth, not in
    // jerks. Players are pulled by their own machine (every machine has this hole), enemies and loose things by the
    // host.
    private void FixedUpdate()
    {
        float share = Time.fixedDeltaTime / PullStep; // Pull is per Pull Step seconds
        Vector2 center = transform.position;
        foreach (HitTarget target in HitTarget.Active)
        {
            if (target == ownerTarget || target.IsDying)
                continue;
            Vector2 toCenter = center - (Vector2)target.transform.position;
            float distance = toCenter.magnitude;
            if (distance >= radius || distance <= 0.2f)
                continue;
            Vector2 tug = toCenter / distance * pull * (1f - distance / radius * 0.5f) * share;
            if (target.IsPlayer)
                target.Pull(tug, owner.Bridge);
            else if (!lookAlike)
                target.Knock(tug, owner.Bridge);
        }
        if (lookAlike)
            return;
        Physics2D.OverlapCircle(center, radius, AnyCollider, nearby);
        foreach (Collider2D hit in nearby)
        {
            Rigidbody2D loose = hit.attachedRigidbody;
            if (loose != null && loose.bodyType == RigidbodyType2D.Dynamic && hit.GetComponentInParent<HitTarget>() == null)
                loose.AddForce((center - loose.position).normalized * pull * loose.mass * 0.5f * share, ForceMode2D.Impulse);
        }
    }
}
