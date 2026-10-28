using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Brings the title screen to life: the logo drops in and bobs, the buttons rise in one after another, an arrow
// points at the button under the mouse, the fighters bounce on their stage (now and then one jumps high),
// and pixel dust drifts up behind everything. Plays again every time the title screen opens.
public class MenuAnimator : MonoBehaviour
{
    [SerializeField] private RectTransform logo;

    [Tooltip("Rise in one after another, top to bottom.")]
    [SerializeField] private RectTransform[] buttons;

    [Tooltip("Points at the button under the mouse (same parent and anchor as the buttons).")]
    [SerializeField] private RectTransform pointer;

    [SerializeField] private RectTransform[] fighters;

    [Header("Dust")]
    [Tooltip("Fills the screen, behind the menu. Optional.")]
    [SerializeField] private RectTransform dustArea;

    [SerializeField] private int dustCount = 46;
    [SerializeField] private Vector2 dustSpeed = new Vector2(18f, 60f);

    [Header("Timing")]
    [SerializeField] private float logoDropTime = 0.7f;
    [SerializeField] private float buttonSlideTime = 0.4f;
    [SerializeField] private float buttonStagger = 0.08f;
    [SerializeField] private float bounceHeight = 10f;
    [SerializeField] private float bounceSpeed = 2.2f;

    [Tooltip("Seconds between two big jumps (one random fighter each time).")]
    [SerializeField] private Vector2 jumpEvery = new Vector2(1.5f, 3.5f);

    [SerializeField] private float jumpHeight = 120f;
    [SerializeField] private float jumpTime = 0.6f;

    private Vector2 logoBase;
    private Vector2[] buttonBase;
    private Vector2[] fighterBase;
    private RectTransform[] dust;
    private float[] dustRise;
    private float shownAt;
    private float nextJump;
    private int jumper = -1;
    private float jumpStart;

    private void Awake()
    {
        logoBase = logo.anchoredPosition;
        buttonBase = new Vector2[buttons.Length];
        for (int i = 0; i < buttons.Length; i++)
            buttonBase[i] = buttons[i].anchoredPosition;
        fighterBase = new Vector2[fighters.Length];
        for (int i = 0; i < fighters.Length; i++)
            fighterBase[i] = fighters[i].anchoredPosition;
        if (dustArea != null)
            MakeDust();
    }

    private void OnEnable()
    {
        shownAt = Time.unscaledTime;
        nextJump = Time.unscaledTime + 1.2f;
        Update();
    }

    private void MakeDust()
    {
        dust = new RectTransform[dustCount];
        dustRise = new float[dustCount];
        Vector2 area = dustArea.rect.size;
        for (int i = 0; i < dustCount; i++)
        {
            var image = new GameObject("Dust", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(dustArea, false);
            image.raycastTarget = false;
            float size = Random.Range(1, 4) * 5f; // whole UI pixels of the pixel grid
            image.color = new Color(1f, 0.85f, 0.6f, Random.Range(0.05f, 0.22f));
            dust[i] = image.rectTransform;
            dust[i].sizeDelta = new Vector2(size, size);
            dust[i].anchorMin = dust[i].anchorMax = Vector2.zero;
            dust[i].anchoredPosition = new Vector2(Random.Range(0f, area.x), Random.Range(0f, area.y));
            dustRise[i] = Random.Range(dustSpeed.x, dustSpeed.y);
        }
    }

    private void Update()
    {
        float t = Time.unscaledTime - shownAt;
        float dt = Time.unscaledDeltaTime;

        // Logo: drops in with a small overshoot, then bobs.
        float drop = EaseOutBack(Mathf.Clamp01(t / logoDropTime));
        logo.anchoredPosition = logoBase + new Vector2(0f, (1f - drop) * 420f + Mathf.Sin(Time.unscaledTime * 1.6f) * 6f);

        // Buttons: rise in one after another.
        for (int i = 0; i < buttons.Length; i++)
        {
            float k = EaseOutCubic(Mathf.Clamp01((t - 0.15f - i * buttonStagger) / buttonSlideTime));
            buttons[i].anchoredPosition = buttonBase[i] + new Vector2(0f, (1f - k) * -500f);
        }

        UpdatePointer();
        UpdateFighters();

        if (dust == null)
            return;

        // Dust drifts up and comes back at the bottom.
        float top = dustArea.rect.height;
        for (int i = 0; i < dust.Length; i++)
        {
            Vector2 p = dust[i].anchoredPosition + new Vector2(Mathf.Sin(Time.unscaledTime * 0.7f + i) * 6f * dt, dustRise[i] * dt);
            if (p.y > top + 20f)
                p = new Vector2(Random.Range(0f, dustArea.rect.width), -20f);
            dust[i].anchoredPosition = p;
        }
    }

    // The arrow sits left of the button under the mouse (hidden when there is none).
    private void UpdatePointer()
    {
        RectTransform hovered = null;
        Vector2 mouse = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        foreach (RectTransform button in buttons)
            if (RectTransformUtility.RectangleContainsScreenPoint(button, mouse, null))
                hovered = button;
        pointer.gameObject.SetActive(hovered != null);
        if (hovered == null)
            return;
        // Same parent and anchor as the buttons.
        pointer.anchoredPosition = hovered.anchoredPosition +
                                   new Vector2(-hovered.rect.width * 0.5f - 44f + Mathf.Sin(Time.unscaledTime * 8f) * 6f, 4f);
    }

    private void UpdateFighters()
    {
        if (fighters.Length == 0)
            return;
        if (Time.unscaledTime >= nextJump && jumper < 0)
        {
            jumper = Random.Range(0, fighters.Length);
            jumpStart = Time.unscaledTime;
            nextJump = Time.unscaledTime + jumpTime + Random.Range(jumpEvery.x, jumpEvery.y);
        }

        for (int i = 0; i < fighters.Length; i++)
        {
            float phase = Time.unscaledTime * bounceSpeed * Mathf.PI + i * 1.3f;
            float hop = Mathf.Abs(Mathf.Sin(phase)) * bounceHeight;
            float squash = 1f - Mathf.Clamp01(1f - Mathf.Abs(Mathf.Sin(phase)) * 4f) * 0.12f; // flatter when touching down
            if (i == jumper)
            {
                float k = (Time.unscaledTime - jumpStart) / jumpTime;
                if (k >= 1f)
                    jumper = -1;
                else
                {
                    hop = 4f * k * (1f - k) * jumpHeight;
                    squash = 1.06f;
                }
            }
            fighters[i].anchoredPosition = fighterBase[i] + new Vector2(0f, hop);
            fighters[i].localScale = new Vector3(2f - squash, squash, 1f);
        }
    }

    private static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);

    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
}
