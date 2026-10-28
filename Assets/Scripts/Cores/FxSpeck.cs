using UnityEngine;

// One small flying pixel of an effect (debris, blood drops, steam, snow, flames, sparks): it flies with a speed and
// gravity (or homes in on a target), spins if asked, and fades out in steps (retro) over its life.
public class FxSpeck : MonoBehaviour
{
    private SpriteRenderer sprite;
    private Vector2 velocity;
    private float gravity;
    private float life;
    private float age;
    private float spin;
    private Color color;
    private Transform seek;     // flies to it (and is gone when it gets there)
    private float growth;       // size added per second

    // Makes one. seek = fly to this transform instead (e.g. blood drops to a vampire).
    public static FxSpeck Spawn(Sprite sprite, Color color, Vector2 position, Vector2 velocity, float gravity, float life,
                                float size, float spin = 0f, Transform seek = null, float growth = 0f)
    {
        var speck = new GameObject("Speck").AddComponent<FxSpeck>();
        speck.transform.position = position;
        speck.transform.localScale = Vector3.one * size;
        speck.sprite = speck.gameObject.AddComponent<SpriteRenderer>();
        speck.sprite.sprite = sprite;
        speck.sprite.color = color;
        speck.sprite.sortingOrder = 25;
        speck.color = color;
        speck.velocity = velocity;
        speck.gravity = gravity;
        speck.life = life;
        speck.spin = spin;
        speck.seek = seek;
        speck.growth = growth;
        return speck;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        if (age >= life)
        {
            Destroy(gameObject);
            return;
        }
        if (seek != null)
        {
            Vector2 to = (Vector2)seek.position + Vector2.up * 0.4f - (Vector2)transform.position;
            if (to.magnitude < 0.25f)
            {
                Destroy(gameObject);
                return;
            }
            velocity = Vector2.Lerp(velocity, to.normalized * 14f, 1f - Mathf.Exp(-10f * dt));
        }
        else
        {
            velocity.y -= gravity * dt;
        }
        transform.position += (Vector3)(velocity * dt);
        if (spin != 0f)
            transform.Rotate(0f, 0f, spin * dt);
        if (growth != 0f)
            transform.localScale += Vector3.one * growth * dt;
        Color c = color;
        c.a *= Mathf.Ceil((1f - age / life) * 3f) / 3f;
        sprite.color = c;
    }
}
