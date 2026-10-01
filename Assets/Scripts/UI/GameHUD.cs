using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// On-screen info: this machine's player's health (bar + number), the cores (augments) the player owns (icons),
// and in single player the score and the wave. Reads the values every frame.
public class GameHUD : MonoBehaviour
{
    [Tooltip("Score and wave are only shown while it is running (single player).")]
    [SerializeField] private WaveManager waveManager;

    [Header("UI")]
    [Tooltip("Image with Image Type = Filled (Horizontal): its fill shows the player's health.")]
    [SerializeField] private Image healthFill;

    [SerializeField] private Text healthText;
    [SerializeField] private Text scoreText;
    [SerializeField] private Text waveText;

    [Header("Core Icons")]
    [Tooltip("The owned cores are lined up here, from the top left.")]
    [SerializeField] private RectTransform coreIconRow;

    [Tooltip("Copied once per owned core. Keep it disabled.")]
    [SerializeField] private Image coreIconTemplate;

    [Tooltip("Distance between two icons, sideways and between rows (UI pixels).")]
    [SerializeField] private float coreIconSpacing = 44f;

    [Tooltip("Icons per row; more go on the next row down.")]
    [Min(1)]
    [SerializeField] private int coreIconsPerRow = 12;

    private readonly List<Image> coreIcons = new List<Image>();

    private void Awake()
    {
        if (coreIconTemplate != null)
            coreIconTemplate.gameObject.SetActive(false);
    }

    private void Update()
    {
        // The player spawns over the network once a game starts.
        PlayerNetwork local = PlayerNetwork.Local;
        // No health bar in the LAN lobby (nobody can get hurt there).
        MatchManager match = MatchManager.Instance;
        bool inLobby = GameMode.IsLan && (match == null || !match.InMatch);
        healthFill.transform.parent.gameObject.SetActive(local != null && !inLobby);
        if (local != null)
        {
            PlayerHealth playerHealth = local.GetComponent<PlayerHealth>();
            healthFill.fillAmount = playerHealth.Health / playerHealth.MaxHealth;
            healthText.text = "HP " + Mathf.CeilToInt(playerHealth.Health);
        }

        if (local != null && local.Cores != null)
            UpdateCoreIcons(local.Cores);

        bool waves = waveManager.isActiveAndEnabled;
        scoreText.text = waves ? Lang.T("SCORE ", "ĐIỂM ") + waveManager.Score : "";
        waveText.text = !waves ? ""
            : waveManager.WaveRunning ? Lang.T("WAVE ", "ĐỢT ") + waveManager.Wave + Lang.T("   ENEMIES ", "   KẺ ĐỊCH ") + waveManager.EnemiesLeft
            : Lang.T("WAVE ", "ĐỢT ") + waveManager.Wave;
    }

    // Cores are added one by one, so only the missing icons are made; when they were all lost (a new LAN match)
    // the row starts over.
    private void UpdateCoreIcons(CoreBridge cores)
    {
        if (coreIcons.Count > cores.OwnedCount)
        {
            foreach (Image old in coreIcons)
                Destroy(old.gameObject);
            coreIcons.Clear();
        }
        while (coreIcons.Count < cores.OwnedCount)
        {
            Image icon = Instantiate(coreIconTemplate, coreIconRow);
            icon.sprite = cores.OwnedIcon(coreIcons.Count);
            int column = coreIcons.Count % coreIconsPerRow;
            int row = coreIcons.Count / coreIconsPerRow;
            icon.rectTransform.anchoredPosition = new Vector2(column * coreIconSpacing, -row * coreIconSpacing);
            icon.gameObject.SetActive(true);
            coreIcons.Add(icon);
        }
    }
}
