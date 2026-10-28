using System.Collections.Generic;
using UnityEngine;

// Small helpers shared by the cores: a blast that hurts every target around a point, and a lightning line.
public static class CoreEffects
{
    private static readonly List<HitTarget> blastHits = new List<HitTarget>();

    // Hurts every target within the radius (once each, never Skip / Skip Too), pushing them away from the blast.
    // The effect (optional) is scaled by Effect Scale. source = whose cores made it (kill credit; its own player
    // is never hurt). dealDamage = false: only the effect (a look-alike on a machine that is not the host).
    public static void Blast(ParticleSystem effect, float effectScale, Vector2 position, float radius, float damage,
                             float knockback, CoreBridge source, bool dealDamage, HitTarget skip, HitTarget skipToo = null)
    {
        if (effect != null)
        {
            ParticleSystem instance = Object.Instantiate(effect, position, Quaternion.identity);
            instance.transform.localScale = Vector3.one * effectScale;
        }
        if (!dealDamage)
            return;

        blastHits.Clear();
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(position, radius))
        {
            HitTarget target = hit.GetComponentInParent<HitTarget>();
            if (target == null || target == skip || target == skipToo || target.IsDying || blastHits.Contains(target))
                continue;
            if (source != null && target.OwnCores == source)
                continue; // never your own blast
            blastHits.Add(target);

            Vector2 closestPoint = hit.ClosestPoint(position);
            Vector2 towardBlast = (position - closestPoint).normalized; // TakeDamage pushes the other way
            if (towardBlast == Vector2.zero)
                towardBlast = Vector2.up;
            target.TakeDamage(damage, closestPoint, towardBlast, knockback, source);
        }
    }

    // A lightning bolt from high above down onto a point (Thunder): a wide glow and a bright core along the same
    // jagged path, gone after Duration.
    public static void Bolt(Material material, Color glow, Color core, Vector2 to, float height, float duration)
    {
        const int points = 10;
        Vector2 from = to + new Vector2(Random.Range(-1.5f, 1.5f), height);
        var path = new Vector3[points];
        for (int i = 0; i < points; i++)
        {
            float t = i / (points - 1f);
            float jitter = i == 0 || i == points - 1 ? 0f : Random.Range(-0.45f, 0.45f);
            path[i] = Vector2.Lerp(from, to, t) + Vector2.right * jitter;
        }
        Line(material, glow, path, 0.4f, duration, 19);
        Line(material, core, path, 0.12f, duration, 20);
    }

    private static void Line(Material material, Color color, Vector3[] path, float width, float duration, int order)
    {
        var line = new GameObject("Bolt").AddComponent<LineRenderer>();
        line.material = material;
        line.startColor = color;
        line.endColor = color;
        line.widthMultiplier = width;
        line.sortingOrder = order;
        line.positionCount = path.Length;
        line.SetPositions(path);
        Object.Destroy(line.gameObject, duration);
    }

    // A jagged line from one point to another that disappears after Duration (real seconds).
    public static void Zap(Material material, Color color, Vector2 from, Vector2 to, float width, float duration)
    {
        var line = new GameObject("Zap").AddComponent<LineRenderer>();
        line.material = material;
        line.startColor = color;
        line.endColor = color;
        line.widthMultiplier = width;
        line.sortingOrder = 20;

        const int points = 5;
        line.positionCount = points;
        Vector2 side = Vector2.Perpendicular(to - from).normalized;
        for (int i = 0; i < points; i++)
        {
            float t = i / (points - 1f);
            float jitter = i == 0 || i == points - 1 ? 0f : Random.Range(-0.25f, 0.25f);
            line.SetPosition(i, Vector2.Lerp(from, to, t) + side * jitter);
        }
        Object.Destroy(line.gameObject, duration);
    }
}
