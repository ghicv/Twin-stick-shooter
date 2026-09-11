using UnityEngine;

// Retro floating damage number built from pixel digit sprites: pops in, rises in pixel steps while slowing down,
// blinks, then destroys itself.
public class DamageNumber : MonoBehaviour
{
    [Tooltip("Sprites for the digits 0-9, in order.")]
    [SerializeField] private Sprite[] digitSprites = new Sprite[10];

    [SerializeField] private Color color = Color.white;

    [SerializeField] private int sortingOrder = 20;

    [Tooltip("Distance between two digits (units).")]
    [SerializeField] private float digitSpacing = 0.25f;

    [Tooltip("Seconds the number stays on screen.")]
    [SerializeField] private float lifetime = 0.7f;

    [Tooltip("Starting upward speed (units/sec). It slows down to 0 over the lifetime.")]
    [SerializeField] private float riseSpeed = 2.5f;

    [Tooltip("Random sideways speed (units/sec), so numbers from fast hits don't stack on top of each other.")]
    [SerializeField] private float sideDrift = 0.6f;

    [Tooltip("Scale for the first moment (a pop), then 1.")]
    [SerializeField] private float popScale = 1.5f;

    [Tooltip("The number is drawn snapped to this grid (units), so it moves in pixel steps. 1/16 = one pixel.")]
    [SerializeField] private float pixelSize = 0.0625f;

    [Tooltip("From this point of its life (0..1) the number blinks before disappearing.")]
    [Range(0f, 1f)]
    [SerializeField] private float blinkStart = 0.7f;

    [Tooltip("Seconds each blink (on / off) lasts.")]
    [SerializeField] private float blinkInterval = 0.04f;

    private SpriteRenderer[] digits;
    private Vector3 exactPosition;
    private Vector3 velocity;
    private float age;

    public void Show(float damage)
    {
        // One sprite per digit, centered on the spawn point.
        string text = Mathf.RoundToInt(damage).ToString();
        digits = new SpriteRenderer[text.Length];
        float firstX = -(text.Length - 1) * digitSpacing * 0.5f;
        for (int i = 0; i < text.Length; i++)
        {
            var digit = new GameObject("Digit");
            digit.transform.SetParent(transform, false);
            digit.transform.localPosition = new Vector3(firstX + i * digitSpacing, 0f, 0f);
            var sr = digit.AddComponent<SpriteRenderer>();
            sr.sprite = digitSprites[text[i] - '0'];
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            digits[i] = sr;
        }

        exactPosition = transform.position;
        velocity = new Vector3(Random.Range(-sideDrift, sideDrift), riseSpeed, 0f);
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = age / lifetime; // 0 → 1
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        // Move smoothly in "exact" space, but draw snapped to the pixel grid.
        exactPosition += velocity * (1f - t) * Time.deltaTime;
        transform.position = new Vector3(Snap(exactPosition.x), Snap(exactPosition.y), exactPosition.z);

        // Pop: big for the first 10% of its life, then normal size (no smooth tween).
        transform.localScale = Vector3.one * (t < 0.1f ? popScale : 1f);

        // Blink on and off near the end.
        bool visible = t < blinkStart || Mathf.Repeat(age, blinkInterval * 2f) < blinkInterval;
        foreach (var digit in digits)
            digit.enabled = visible;
    }

    private float Snap(float value) => Mathf.Round(value / pixelSize) * pixelSize;
}
