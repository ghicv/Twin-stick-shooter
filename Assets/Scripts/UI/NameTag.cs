using UnityEngine;
using UnityEngine.UI;

// LAN: above every player's head a big green "YOU" over this machine's own player, the others' names (lobby only),
// and a small health bar (match only).
// A world-space canvas under the player; hidden in single player and while the player is out of play.
public class NameTag : MonoBehaviour
{
    [SerializeField] private PlayerNetwork player;
    [SerializeField] private GameObject content;
    [SerializeField] private Text nameText;

    [Tooltip("Filled image (horizontal) showing health.")]
    [SerializeField] private Image healthFill;

    [Tooltip("The health bar only shows in a match (not in the lobby).")]
    [SerializeField] private GameObject healthBar;

    [Tooltip("\"YOU\" over this machine's own player: its color and size.")]
    [SerializeField] private Color youColor = new Color(0.3f, 1f, 0.3f); // bright green
    [SerializeField] private int youFontSize = 96;

    private Vector3 baseScale;
    private int baseFontSize;

    // Every name tag hides while true (the round's end show puts big text above the winner).
    public static bool HideAll { get; set; }

    private void Awake()
    {
        baseScale = transform.localScale;
        baseFontSize = nameText.fontSize;
    }

    private void LateUpdate()
    {
        bool show = GameMode.IsLan && player.IsSpawned && player.InPlay && !HideAll;
        if (content.activeSelf != show)
            content.SetActive(show);
        if (!show)
            return;

        // This machine's own player: a big bright "YOU", easy to spot. The others: their name in their color in the
        // lobby, no name in a match (only the health bar).
        MatchManager match = MatchManager.Instance;
        bool inMatch = match != null && match.InMatch;
        nameText.text = player.IsMine ? "YOU" : inMatch ? "" : player.Name;
        nameText.color = player.IsMine ? youColor : player.Color;
        nameText.fontSize = player.IsMine ? youFontSize : baseFontSize;
        healthBar.SetActive(inMatch);
        healthFill.fillAmount = player.MaxHealth > 0f ? player.Health / player.MaxHealth : 0f;

        // Never mirrored or squashed with the player.
        Vector3 parent = transform.parent.lossyScale;
        transform.localScale = new Vector3(baseScale.x / parent.x, baseScale.y / parent.y, baseScale.z);
        transform.rotation = Quaternion.identity;
    }
}
