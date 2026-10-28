using UnityEngine;

// A word or number that pops up in the world and floats away ("CHÍ MẠNG!", "+30", "KẾT LIỄU!"): it jumps in big,
// settles, rises and fades in steps. Made by CoreEffects.Pop.
public class PopText : MonoBehaviour
{
    private const float Life = 0.9f;

    private TextMesh text;
    private Color color;
    private float age;
    private float size;

    public void Setup(Font font, string words, Color color, float size)
    {
        this.color = color;
        this.size = size;
        text = gameObject.AddComponent<TextMesh>();
        text.font = font;
        text.text = words;
        text.fontSize = 64;
        text.characterSize = 0.06f;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = color;
        var mesh = GetComponent<MeshRenderer>();
        mesh.sharedMaterial = font.material;
        mesh.sortingOrder = 40;
        Update();
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= Life)
        {
            Destroy(gameObject);
            return;
        }
        float t = age / Life;
        float pop = t < 0.12f ? 1.4f : 1f; // jumps in big
        transform.localScale = Vector3.one * size * pop;
        transform.position += Vector3.up * 1.2f * Time.deltaTime;
        Color c = color;
        c.a = t < 0.6f ? 1f : Mathf.Ceil((1f - t) / 0.4f * 3f) / 3f;
        text.color = c;
    }
}
