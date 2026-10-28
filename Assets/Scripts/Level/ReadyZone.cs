using UnityEngine;
using UnityEngine.UI;

// The lobby's READY zone: when everybody in the room stands in it (at least 2 players), the host counts down 3-2-1
// and the match starts (MatchManager). Its sign shows how many are ready; the pad lights up while someone is in it.
// The zone is the area of Size above this object (its position is the middle of the bottom edge).
public class ReadyZone : MonoBehaviour
{
    [SerializeField] private Vector2 size = new Vector2(6f, 3f);

    [Tooltip("World-space text above the pad.")]
    [SerializeField] private Text label;

    [Tooltip("The pad's glow (its color shows the state).")]
    [SerializeField] private SpriteRenderer glow;

    [SerializeField] private Color idleColor = new Color(0.4f, 0.45f, 0.65f, 0.5f);
    [SerializeField] private Color readyColor = new Color(0.4f, 1f, 0.55f, 0.9f);

    [Tooltip("How fast the glow pulses while someone stands in it (per second).")]
    [SerializeField] private float pulseSpeed = 3f;

    // The lobby map is the only one with a zone, and it is only on in the lobby.
    public static ReadyZone Instance { get; private set; }

    private void OnEnable()
    {
        Instance = this;
    }

    private void OnDisable()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool Contains(Vector2 point)
    {
        Vector2 bottom = transform.position;
        return Mathf.Abs(point.x - bottom.x) <= size.x * 0.5f && point.y >= bottom.y && point.y <= bottom.y + size.y;
    }

    private void Update()
    {
        MatchManager match = MatchManager.Instance;
        if (match == null)
        {
            label.text = "";
            return;
        }

        int total = PlayerNetwork.All.Count;
        int ready = match.ReadyCount;
        if (match.Countdown > 0)
            label.text = Lang.T("STARTING IN ", "BẮT ĐẦU SAU ") + match.Countdown;
        else if (total < 2)
            label.text = Lang.T("READY ", "SẴN SÀNG ") + ready + "/" + total + "\n" +
                         Lang.T("WAITING FOR PLAYERS", "ĐANG CHỜ NGƯỜI CHƠI");
        else
            label.text = Lang.T("READY ", "SẴN SÀNG ") + ready + "/" + total + "\n" +
                         Lang.T("EVERYONE STAND HERE TO START", "TẤT CẢ ĐỨNG VÀO ĐÂY ĐỂ BẮT ĐẦU");

        float pulse = ready > 0 ? 0.75f + 0.25f * Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f) : 1f;
        Color color = ready > 0 ? readyColor : idleColor;
        glow.color = new Color(color.r, color.g, color.b, color.a * pulse);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position + Vector3.up * size.y * 0.5f, size);
    }
}
