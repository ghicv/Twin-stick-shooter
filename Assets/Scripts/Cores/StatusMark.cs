using UnityEngine;

// Shows a target is frozen (Chill: tinted icy blue, snowflakes drifting off) or burning (Ignite: tinted orange,
// pixel flames licking up) for a while. Added to the target by BulletCores on every machine; hitting it again starts
// it over. The target's look comes back when it ends.
public class StatusMark : MonoBehaviour
{
    public enum Kind { Frost, Flame }

    private Kind kind;
    private float timeLeft;
    private float puffTimer;
    private Sprite speck;
    private SpriteRenderer[] sprites;
    private Color[] baseColors; // each sprite's own color under the tint
    private Color[] tinted;     // what this mark set last frame

    // Starts (or starts over) that mark on the target.
    public static void Put(GameObject target, Kind kind, float duration, Sprite speck)
    {
        foreach (StatusMark mark in target.GetComponents<StatusMark>())
            if (mark.kind == kind)
            {
                mark.timeLeft = duration;
                return;
            }
        var added = target.AddComponent<StatusMark>();
        added.kind = kind;
        added.timeLeft = duration;
        added.speck = speck;
    }

    private void Start()
    {
        sprites = GetComponentsInChildren<SpriteRenderer>();
        baseColors = new Color[sprites.Length];
        tinted = new Color[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
            baseColors[i] = tinted[i] = sprites[i].color;
    }

    private Color Tint => kind == Kind.Frost ? new Color(0.55f, 0.85f, 1f) : new Color(1f, 0.6f, 0.3f);

    private void LateUpdate()
    {
        timeLeft -= Time.deltaTime;
        if (timeLeft <= 0f)
        {
            Restore();
            Destroy(this);
            return;
        }
        // Tints over the sprite's own color: a color someone else set this frame (a blink, a flash) is the new own
        // color; one still showing last frame's tint is not tinted again.
        Color tint = Tint;
        for (int i = 0; i < sprites.Length; i++)
        {
            SpriteRenderer sprite = sprites[i];
            if (sprite == null)
                continue;
            if (sprite.color != tinted[i])
                baseColors[i] = sprite.color;
            Color own = baseColors[i];
            tinted[i] = new Color(own.r * tint.r, own.g * tint.g, own.b * tint.b, own.a);
            sprite.color = tinted[i];
        }

        puffTimer -= Time.deltaTime;
        if (puffTimer > 0f)
            return;
        Vector2 at = (Vector2)transform.position + new Vector2(Random.Range(-0.35f, 0.35f), Random.Range(0f, 1f));
        if (kind == Kind.Frost)
        {
            puffTimer = 0.12f;
            FxSpeck.Spawn(speck, Color.white, at, new Vector2(Random.Range(-0.6f, 0.6f), -0.4f), 0.5f, 0.6f, 0.06f, 180f);
        }
        else
        {
            puffTimer = 0.05f;
            Color flame = Random.value < 0.5f ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.45f, 0.15f);
            FxSpeck.Spawn(speck, flame, at, new Vector2(Random.Range(-0.4f, 0.4f), 2.2f), -1f, 0.35f, 0.09f, 0f, null, -0.15f);
        }
    }

    private void Restore()
    {
        if (sprites == null)
            return;
        for (int i = 0; i < sprites.Length; i++)
            if (sprites[i] != null && sprites[i].color == tinted[i])
                sprites[i].color = baseColors[i];
    }
}
