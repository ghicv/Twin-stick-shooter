using UnityEngine;
using UnityEngine.UI;

// End of the run (roguelite): shows the wave reached and the score. Play Again starts a new single-player run
// by loading the scene again, so everything (cores, score, waves) starts over. PlayerRespawn calls Show when the player dies.
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
        waveText.text = Lang.T("WAVE ", "ĐỢT ") + waveManager.Wave;
        scoreText.text = Lang.T("SCORE ", "ĐIỂM ") + waveManager.Score;
        panel.SetActive(true);
    }

    private void PlayAgain()
    {
        GameMode.Restart(true); // reloads, and the menu starts single player right away
    }
}
