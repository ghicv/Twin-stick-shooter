using UnityEngine;

// One ghost left behind by a dashing player (PlayerBodyAnimator): a copy of the body, outfit and hat as they were,
// in one flat color, fading out in steps, then gone.
public class PlayerAfterimage : MonoBehaviour
{
    private SpriteRenderer[] parts;
    private float life;
    private float age;
    private Color color;

    // Copies these sprites (with their place, size and order) in Color, for Life seconds.
    public static void Leave(SpriteRenderer[] source, Color color, float life)
    {
        var ghost = new GameObject("Afterimage").AddComponent<PlayerAfterimage>();
        ghost.parts = new SpriteRenderer[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            SpriteRenderer from = source[i];
            var part = new GameObject(from.name).AddComponent<SpriteRenderer>();
            part.transform.SetParent(ghost.transform, false);
            part.transform.SetPositionAndRotation(from.transform.position, from.transform.rotation);
            part.transform.localScale = from.transform.lossyScale;
            part.sprite = from.enabled && from.gameObject.activeInHierarchy ? from.sprite : null; // a hidden hat stays hidden
            part.flipX = from.flipX;
            part.sharedMaterial = from.sharedMaterial;
            part.sortingLayerID = from.sortingLayerID;
            part.sortingOrder = from.sortingOrder - 10; // behind the player
            ghost.parts[i] = part;
        }
        ghost.color = color;
        ghost.life = life;
        ghost.Paint(1f);
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= life)
        {
            Destroy(gameObject);
            return;
        }
        Paint(1f - age / life);
    }

    private void Paint(float left)
    {
        Color tint = color;
        tint.a *= Mathf.Ceil(left * 4f) / 4f; // fades in steps (retro)
        foreach (SpriteRenderer part in parts)
            part.color = tint;
    }
}
