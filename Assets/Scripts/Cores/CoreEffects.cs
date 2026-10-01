using System.Collections.Generic;
using UnityEngine;

// Small helpers shared by the cores: a blast that hurts every enemy around a point, and a lightning line.
public static class CoreEffects
{
    private static readonly List<Dummy> blastHits = new List<Dummy>();

    // Hurts every enemy within the radius (once each, except Skip), pushing them away from the blast.
    // The effect (optional) is scaled by Effect Scale.
    public static void Blast(ParticleSystem effect, float effectScale, Vector2 position, float radius, float damage,
                             float knockback, Dummy skip)
    {
        if (effect != null)
        {
            ParticleSystem instance = Object.Instantiate(effect, position, Quaternion.identity);
            instance.transform.localScale = Vector3.one * effectScale;
        }

        blastHits.Clear();
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(position, radius))
        {
            Dummy dummy = hit.GetComponentInParent<Dummy>();
            if (dummy == null || dummy == skip || blastHits.Contains(dummy))
                continue;
            blastHits.Add(dummy);

            Vector2 closestPoint = hit.ClosestPoint(position);
            Vector2 towardBlast = (position - closestPoint).normalized; // TakeDamage pushes the other way
            if (towardBlast == Vector2.zero)
                towardBlast = Vector2.up;
            dummy.TakeDamage(damage, closestPoint, towardBlast, knockback);
        }
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
