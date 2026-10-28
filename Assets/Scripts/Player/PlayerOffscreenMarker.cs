using UnityEngine;

// While a player is outside what the camera shows (knocked off the map, launched high), an arrow in that player's
// color waits at the edge of the screen, pointing at them, so everyone (that player first) knows where they are and
// which way to go back. It blinks a little, in steps (retro). Runs on every machine.
public class PlayerOffscreenMarker : MonoBehaviour
{
    [Tooltip("A pixel arrow pointing right (white, tinted with the player's color).")]
    [SerializeField] private Sprite arrowSprite;

    [Tooltip("How far inside the screen edge the arrow sits (units)...")]
    [SerializeField] private float edgeInset = 0.9f;

    [Tooltip("...and from the top edge, below the health bar.")]
    [SerializeField] private float topInset = 1.6f;

    [Tooltip("Size of the arrow (it pulses to a quarter bigger).")]
    [SerializeField] private float size = 1.6f;

    [Tooltip("The arrow pulses this many times a second.")]
    [SerializeField] private float pulseSpeed = 4f;

    [SerializeField] private int sortingOrder = 50;

    private PlayerNetwork player;
    private SpriteRenderer arrow;

    private void Awake()
    {
        player = GetComponent<PlayerNetwork>();
        arrow = new GameObject("Offscreen Marker").AddComponent<SpriteRenderer>();
        arrow.sprite = arrowSprite;
        arrow.sortingOrder = sortingOrder;
        arrow.enabled = false;
    }

    private void OnDestroy()
    {
        if (arrow != null)
            Destroy(arrow.gameObject);
    }

    private void LateUpdate()
    {
        Camera view = Camera.main;
        arrow.enabled = false;
        if (view == null || !view.orthographic || (player != null && !player.InPlay))
            return;

        Vector2 center = view.transform.position;
        var half = new Vector2(view.orthographicSize * view.aspect, view.orthographicSize);
        Vector2 position = transform.position;
        Vector2 offset = position - center;
        if (Mathf.Abs(offset.x) < half.x && Mathf.Abs(offset.y) < half.y)
            return; // on screen

        var spot = new Vector2(Mathf.Clamp(offset.x, -half.x + edgeInset, half.x - edgeInset),
                               Mathf.Clamp(offset.y, -half.y + edgeInset, half.y - topInset));
        Vector2 toPlayer = offset - spot;
        arrow.transform.SetPositionAndRotation(center + spot, Quaternion.Euler(0f, 0f, Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg));
        bool big = Mathf.Repeat(Time.time * pulseSpeed, 1f) < 0.5f;
        arrow.transform.localScale = Vector3.one * size * (big ? 1.25f : 1f);
        arrow.color = player != null ? player.Color : Color.white;
        arrow.enabled = true;
    }
}
