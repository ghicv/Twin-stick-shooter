using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// On-screen info: the player's health (bar + number), the cores (augments) the player owns (icons),
// the score and the wave. Reads the values every frame.
public class GameHUD : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private WaveManager waveManager;

    [Tooltip("Where the owned cores come from. Optional.")]
    [SerializeField] private CoreBridge cores;

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
        healthFill.fillAmount = playerHealth.Health / playerHealth.MaxHealth;
        healthText.text = "HP " + Mathf.CeilToInt(playerHealth.Health);

        if (cores != null)
            UpdateCoreIcons();

        scoreText.text = "SCORE " + waveManager.Score;
        waveText.text = waveManager.WaveRunning
            ? "WAVE " + waveManager.Wave + "   ENEMIES " + waveManager.EnemiesLeft
            : "WAVE " + waveManager.Wave;
    }

    // Cores are only ever added during a run, so only the missing icons are made.
    private void UpdateCoreIcons()
    {
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
