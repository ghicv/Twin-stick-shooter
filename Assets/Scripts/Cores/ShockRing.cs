using UnityEngine;

// A shock wave: a ring that grows from a point and fades out in steps (retro). Made by CoreEffects.Ring.
public class ShockRing : MonoBehaviour
{
    private const int Points = 28;

    private LineRenderer line;
    private float radius;
    private float duration;
    private float width;
    private float age;
    private Color color;

    public void Setup(Material material, Color color, float radius, float width, float duration)
    {
        this.radius = radius;
        this.duration = duration;
        this.width = width;
        this.color = color;
        line = gameObject.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.loop = true;
        line.useWorldSpace = false;
        line.positionCount = Points;
        line.sortingOrder = 18;
        Draw(0f);
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= duration)
        {
            Destroy(gameObject);
            return;
        }
        Draw(age / duration);
    }

    private void Draw(float t)
    {
        float eased = 1f - (1f - t) * (1f - t); // fast out, slow at the end
        float r = Mathf.Lerp(radius * 0.15f, radius, eased);
        for (int i = 0; i < Points; i++)
        {
            float a = i * Mathf.PI * 2f / Points;
            line.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * r);
        }
        Color c = color;
        c.a *= Mathf.Ceil((1f - t) * 4f) / 4f;
        line.startColor = line.endColor = c;
        line.widthMultiplier = width * (1f - t * 0.6f);
    }
}
