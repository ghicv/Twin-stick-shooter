using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// LAN: hold TAB to see everybody in the room: name (in the player's color, the host marked), round wins,
// the cores they own (small icons), and their ping. The host can kick a player from here.
public class Scoreboard : MonoBehaviour
{
    [System.Serializable]
    private class Row
    {
        public GameObject root;
        public Image colorBar;
        public Text name;

        [Tooltip("Shown on the host's row.")]
        public GameObject hostBadge;

        public Text wins;

        [Tooltip("The player's core icons are lined up here, from the left.")]
        public RectTransform icons;

        public Text ping;

        [Tooltip("Host only, on the other players' rows.")]
        public Button kickButton;

        [HideInInspector] public List<Image> iconImages = new List<Image>();
        [HideInInspector] public PlayerNetwork player;
    }

    [SerializeField] private GameObject panel;

    [Tooltip("Room name and what is going on (lobby, round 3 / 8).")]
    [SerializeField] private Text titleText;

    [SerializeField] private Text subtitleText;

    [Tooltip("One row per player (4).")]
    [SerializeField] private Row[] rows;

    [Tooltip("Copied once per core. Keep it disabled.")]
    [SerializeField] private Image iconTemplate;

    [SerializeField] private float iconSpacing = 30f;

    [Tooltip("At most this many icons per row (the rest is written as +N).")]
    [SerializeField] private int maxIcons = 12;

    [Header("Ping colors")]
    [SerializeField] private Color goodPing = new Color(0.4f, 0.9f, 0.5f);
    [SerializeField] private Color okPing = new Color(1f, 0.85f, 0.3f);
    [SerializeField] private Color badPing = new Color(1f, 0.35f, 0.35f);

    private void Awake()
    {
        panel.SetActive(false);
        iconTemplate.gameObject.SetActive(false);
        foreach (Row row in rows)
        {
            Row current = row; // captured by the click
            row.kickButton.onClick.AddListener(() =>
            {
                if (MatchManager.Instance != null)
                    MatchManager.Instance.Kick(current.player);
            });
        }
    }

    private void Update()
    {
        bool show = GameMode.IsLan && MatchManager.Instance != null && !InputLock.Blocked &&
                    Keyboard.current != null && Keyboard.current.tabKey.isPressed;
        if (panel.activeSelf != show)
            panel.SetActive(show);
        if (show)
            Fill();
    }

    private void Fill()
    {
        MatchManager match = MatchManager.Instance;
        titleText.text = match.RoomName;
        subtitleText.text = !match.InMatch ? Lang.T("LOBBY", "PHÒNG CHỜ")
                          : Lang.T("ROUND ", "VÒNG ") + Mathf.Max(1, match.Round) + " / " + match.Rounds;

        // Most round wins first, then by slot.
        var players = new List<PlayerNetwork>(PlayerNetwork.All);
        players.Sort((a, b) => match.Wins(b.Slot) != match.Wins(a.Slot) ? match.Wins(b.Slot).CompareTo(match.Wins(a.Slot))
                                                                       : a.Slot.CompareTo(b.Slot));
        bool host = Unity.Netcode.NetworkManager.Singleton.IsServer;
        for (int i = 0; i < rows.Length; i++)
        {
            Row row = rows[i];
            row.player = i < players.Count ? players[i] : null;
            row.root.SetActive(row.player != null);
            if (row.player == null)
                continue;

            PlayerNetwork player = row.player;
            row.colorBar.color = player.Color;
            row.name.text = player.Name + (player == PlayerNetwork.Local ? Lang.T(" (YOU)", " (BẠN)") : "") +
                            (player.Dead && match.InMatch ? Lang.T("  [OUT]", "  [LOẠI]") : "");
            row.name.color = player.Color;
            row.hostBadge.SetActive(player.IsHostPlayer);
            row.wins.text = match.Wins(player.Slot).ToString();
            row.ping.text = player.IsHostPlayer ? "HOST" : player.Ping + " ms";
            row.ping.color = player.IsHostPlayer || player.Ping < 60 ? goodPing : player.Ping < 150 ? okPing : badPing;
            row.kickButton.gameObject.SetActive(host && !player.IsHostPlayer);
            UpdateIcons(row, player.Cores);
        }
    }

    private void UpdateIcons(Row row, CoreBridge cores)
    {
        int shown = Mathf.Min(cores.OwnedCount, maxIcons);
        bool same = row.iconImages.Count == shown;
        for (int i = 0; same && i < shown; i++)
            same = row.iconImages[i].sprite == cores.OwnedIcon(i);
        if (same)
            return;

        foreach (Image old in row.iconImages)
            Destroy(old.gameObject);
        row.iconImages.Clear();
        for (int i = 0; i < shown; i++)
        {
            Image icon = Instantiate(iconTemplate, row.icons);
            icon.sprite = cores.OwnedIcon(i);
            icon.rectTransform.anchoredPosition = new Vector2(i * iconSpacing, 0f);
            icon.gameObject.SetActive(true);
            row.iconImages.Add(icon);
        }
    }
}
