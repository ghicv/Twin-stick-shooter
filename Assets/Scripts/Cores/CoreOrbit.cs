using System.Collections.Generic;
using UnityEngine;

// Two glowing orbs circling the player (Orbit core, made by SingleCores). They destroy other players' bullets they
// touch (every machine, its own bullets: the host's real ones, the others' look-alikes) and hurt whoever they touch
// (the host decides). Hidden while the player is out of play.
public class CoreOrbit : MonoBehaviour
{
    private const int OrbCount = 2;
    private const float OrbSize = 0.5f;

    private SingleCores owner;
    private PlayerNetwork player;
    private HitTarget self;
    private float radius;
    private float speed;
    private float damage;
    private float hitCooldown;
    private float angle;
    private readonly List<Transform> orbs = new List<Transform>();
    private readonly Dictionary<HitTarget, float> lastHit = new Dictionary<HitTarget, float>();

    public void Setup(SingleCores owner, PlayerNetwork player, HitTarget self, Sprite sprite, Color color,
                      float radius, float speed, float damage, float hitCooldown)
    {
        this.owner = owner;
        this.player = player;
        this.self = self;
        this.radius = radius;
        this.speed = speed;
        this.damage = damage;
        this.hitCooldown = hitCooldown;
        for (int i = 0; i < OrbCount; i++)
        {
            var orb = new GameObject("Orb").AddComponent<SpriteRenderer>();
            orb.transform.SetParent(transform, false);
            orb.transform.localScale = Vector3.one * OrbSize;
            orb.sprite = sprite;
            orb.color = color;
            orb.sortingOrder = 6;
            orbs.Add(orb.transform);
        }
    }

    private void Update()
    {
        bool shown = player == null || player.InPlay;
        angle += speed * Time.deltaTime;
        for (int i = 0; i < orbs.Count; i++)
        {
            orbs[i].gameObject.SetActive(shown);
            float a = (angle + i * 360f / orbs.Count) * Mathf.Deg2Rad;
            orbs[i].position = (Vector2)transform.position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
        }
    }

    private void FixedUpdate()
    {
        if (player != null && !player.InPlay)
            return;
        Rigidbody2D shooter = owner.GetComponent<Rigidbody2D>();
        foreach (Transform orb in orbs)
            foreach (Collider2D hit in Physics2D.OverlapCircleAll(orb.position, OrbSize * 0.5f))
            {
                Projectile bullet = hit.GetComponent<Projectile>();
                if (bullet != null && bullet.Shooter != shooter)
                {
                    bullet.Finish(); // blocked
                    continue;
                }
                HitTarget target = hit.GetComponentInParent<HitTarget>();
                if (target == null || target == self || target.IsDying || player == null || !player.IsHostSide)
                    continue;
                if (lastHit.TryGetValue(target, out float last) && Time.time - last < hitCooldown)
                    continue;
                lastHit[target] = Time.time;
                Vector2 away = ((Vector2)target.transform.position - (Vector2)orb.position).normalized;
                target.TakeDamage(damage, orb.position, -away, 0.6f, owner.Bridge);
            }
    }
}
