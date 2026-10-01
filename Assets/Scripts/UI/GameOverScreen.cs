using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// End of the run (roguelite): shows the wave reached and the score. Play Again starts a new run by loading
// the scene again, so everything (cores, score, waves) starts over. PlayerRespawn calls Show when the player dies.
public class GameOverScreen : MonoBehaviour
{
    [SerializeField] private WaveManager waveManager;

    [Tooltip("Hidden until the player dies. Covers the screen, so nothing behind it can be clicked.")]
    [SerializeField] private GameObject panel;

    [SerializeField] private Text waveText;
    [SerializeField] private Text scoreText;
    [SerializeField] private Button playAgainButton;

    private void Awake()
    {
        panel.SetActive(false);
        playAgainButton.onClick.AddListener(PlayAgain);
    }

    public void Show()
    {
        waveText.text = "WAVE " + waveManager.Wave;
        scoreText.text = "SCORE " + waveManager.Score;
        panel.SetActive(true);
    }

    private void PlayAgain()
    {
        Time.timeScale = 1f; // the death left the game in slow motion (HitStop also resets it when it's destroyed)
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
