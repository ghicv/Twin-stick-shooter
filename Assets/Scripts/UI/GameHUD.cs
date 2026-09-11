using UnityEngine;
using UnityEngine.UI;

// On-screen info: the player's health (bar + number), the score and the wave. Reads the values every frame.
public class GameHUD : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private WaveManager waveManager;

    [Header("UI")]
    [Tooltip("Image with Image Type = Filled (Horizontal): its fill shows the player's health.")]
    [SerializeField] private Image healthFill;

    [SerializeField] private Text healthText;
    [SerializeField] private Text scoreText;
    [SerializeField] private Text waveText;

    private void Update()
    {
        healthFill.fillAmount = playerHealth.Health / playerHealth.MaxHealth;
        healthText.text = "HP " + Mathf.CeilToInt(playerHealth.Health);

        scoreText.text = "SCORE " + waveManager.Score;
        waveText.text = waveManager.WaveRunning
            ? "WAVE " + waveManager.Wave + "   ENEMIES " + waveManager.EnemiesLeft
            : "WAVE " + waveManager.Wave;
    }
}
