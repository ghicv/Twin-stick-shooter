using UnityEngine;
using UnityEngine.UI;

// On-screen info: the player's health (bar + number), the active item upgrades (icons), the score and the wave.
// Reads the values every frame.
public class GameHUD : MonoBehaviour
{
    [System.Serializable]
    private class UpgradeSlot
    {
        [Tooltip("The slot (icon + bar): moved along the row, hidden while the upgrade is off.")]
        public RectTransform root;

        public Image icon;

        [Tooltip("Filled image (Horizontal): shrinks as the upgrade runs out.")]
        public Image timeBar;

        [Tooltip("Shows \"x2\" when the upgrade is stacked. Optional.")]
        public Text stackText;
    }

    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerUpgrades playerUpgrades;
    [SerializeField] private WaveManager waveManager;

    [Header("UI")]
    [Tooltip("Image with Image Type = Filled (Horizontal): its fill shows the player's health.")]
    [SerializeField] private Image healthFill;

    [SerializeField] private Text healthText;
    [SerializeField] private Text scoreText;
    [SerializeField] private Text waveText;

    [Header("Upgrade Icons")]
    [Tooltip("One slot per upgrade, in UpgradeType order: Extra Bullet, Homing, Explosive, Bounce. " +
             "The active ones are lined up from the left.")]
    [SerializeField] private UpgradeSlot[] upgradeSlots;

    [Tooltip("Distance between two icons in the row (UI pixels).")]
    [SerializeField] private float slotSpacing = 60f;

    [Tooltip("An icon blinks during its upgrade's last seconds.")]
    [SerializeField] private float blinkWhenSecondsLeft = 3f;

    [SerializeField] private float blinkInterval = 0.15f;

    private void Update()
    {
        healthFill.fillAmount = playerHealth.Health / playerHealth.MaxHealth;
        healthText.text = "HP " + Mathf.CeilToInt(playerHealth.Health);

        UpdateUpgradeIcons();

        scoreText.text = "SCORE " + waveManager.Score;
        waveText.text = waveManager.WaveRunning
            ? "WAVE " + waveManager.Wave + "   ENEMIES " + waveManager.EnemiesLeft
            : "WAVE " + waveManager.Wave;
    }

    private void UpdateUpgradeIcons()
    {
        float x = 0f;
        for (int i = 0; i < upgradeSlots.Length; i++)
        {
            UpgradeType type = (UpgradeType)i;
            UpgradeSlot slot = upgradeSlots[i];
            float secondsLeft = playerUpgrades.TimeLeft(type);

            bool active = secondsLeft > 0f;
            slot.root.gameObject.SetActive(active);
            if (!active)
                continue;

            slot.root.anchoredPosition = new Vector2(x, 0f);
            x += slotSpacing;

            slot.timeBar.fillAmount = secondsLeft / playerUpgrades.Duration;
            slot.icon.enabled = secondsLeft > blinkWhenSecondsLeft || Mathf.Repeat(Time.unscaledTime, blinkInterval * 2f) < blinkInterval;

            int stacks = type == UpgradeType.ExtraBullet ? playerUpgrades.ExtraBullets
                       : type == UpgradeType.Bounce ? playerUpgrades.Bounces
                       : 1;
            if (slot.stackText != null)
                slot.stackText.text = stacks > 1 ? "x" + stacks : "";
        }
    }
}
