using UnityEngine;
using UnityEngine.UI;

// LAN: the player's name (in its color) and a small health bar above its head, on every machine.
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

    private Vector3 baseScale;

    private void Awake()
    {
        baseScale = transform.localScale;
    }

    private void LateUpdate()
    {
        bool show = GameMode.IsLan && player.IsSpawned && player.InPlay;
        if (content.activeSelf != show)
            content.SetActive(show);
        if (!show)
            return;

        nameText.text = player.Name;
        nameText.color = player.Color;
        MatchManager match = MatchManager.Instance;
        healthBar.SetActive(match != null && match.InMatch);
        healthFill.fillAmount = player.MaxHealth > 0f ? player.Health / player.MaxHealth : 0f;

        // Never mirrored or squashed with the player.
        Vector3 parent = transform.parent.lossyScale;
        transform.localScale = new Vector3(baseScale.x / parent.x, baseScale.y / parent.y, baseScale.z);
        transform.rotation = Quaternion.identity;
    }
}
