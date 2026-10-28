using UnityEngine;
using UnityEngine.UI;

// LAN, when a round is decided (MatchManager): a moment of slow motion, the camera closes in on the winner, the
// winner's name pops in big and blinks in their color over a burst of pixel confetti, and a row of pips shows their
// round wins (the new one flashing in). A draw just shows DRAW. Real time throughout.
public class RoundEndShow : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Text nameText;
    [SerializeField] private Text subtitleText;

    [Tooltip("The round-win pips go here, made from Pip Template (kept off itself): one per round of the match.")]
    [SerializeField] private RectTransform pips;
    [SerializeField] private Image pipTemplate;

    [SerializeField] private PixelConfetti confetti;

    [Header("Timing")]
    [Tooltip("Game speed right after the round is decided.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float slowMotion = 0.3f;

    [Tooltip("Real seconds the slow motion lasts.")]
    [SerializeField] private float slowTime = 0.8f;

    [Tooltip("The camera closes in on the winner to this size (orthographic).")]
    [SerializeField] private float focusSize = 5f;

    [Header("Look and sound")]
    [SerializeField] private Color pipEmptyColor = new Color(1f, 1f, 1f, 0.2f);
    [SerializeField] private AudioClip winSound;
    [SerializeField] private AudioClip drawSound;
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.6f;

    private readonly System.Collections.Generic.List<Image> pipImages = new System.Collections.Generic.List<Image>();
    private float timer = -1f; // < 0: not showing
    private float duration;
    private Color color;
    private int wins;
    private bool slowed;
    private CameraFollow cameraRig;
    private HitStop hitStop;

    private void Awake()
    {
        pipTemplate.gameObject.SetActive(false);
        panel.SetActive(false);
        NameTag.HideAll = false; // a new scene (a show cut short by leaving must not keep them hidden)
    }

    // Winner = null for a draw. Wins = the winner's round wins now; Rounds = rounds in the match (the pips);
    // Seconds = how long until the next part of the match.
    public void Play(PlayerNetwork winner, int winnerWins, int rounds, float seconds)
    {
        if (cameraRig == null)
        {
            cameraRig = FindAnyObjectByType<CameraFollow>();
            hitStop = Camera.main.GetComponent<HitStop>();
        }
        duration = seconds;
        wins = winnerWins;
        color = winner != null ? winner.Color : Color.white;
        nameText.text = winner != null ? winner.Name : Lang.T("DRAW!", "HÒA!");
        nameText.color = color;
        subtitleText.text = winner != null ? Lang.T("WINS THE ROUND!", "THẮNG VÒNG NÀY!") : "";

        while (pipImages.Count < rounds)
        {
            Image pip = Instantiate(pipTemplate, pips);
            pipImages.Add(pip);
        }
        for (int i = 0; i < pipImages.Count; i++)
        {
            pipImages[i].gameObject.SetActive(winner != null && i < rounds);
            pipImages[i].color = i < wins - 1 ? color : pipEmptyColor;
            pipImages[i].rectTransform.localScale = Vector3.one;
        }

        if (winner != null)
        {
            cameraRig.Focus(winner.transform, focusSize);
            confetti.Burst(((RectTransform)nameText.transform).anchoredPosition, 70, new[] { color, Color.white, new Color(1f, 0.85f, 0.3f) }, 1300f);
        }
        if (hitStop != null)
            hitStop.SetSlowMotion(slowMotion);
        slowed = true;
        GameAudio.Play(winner != null ? winSound : drawSound, volume);
        timer = 0f;
        panel.SetActive(true);
        NameTag.HideAll = true;
    }

    private void Update()
    {
        if (timer < 0f)
            return;
        timer += Time.unscaledDeltaTime;

        // The name pops in big and settles in steps, blinking white and its color at first.
        float scale = timer < 0.06f ? 2f : timer < 0.12f ? 1.6f : timer < 0.18f ? 1.25f : 1f;
        nameText.rectTransform.localScale = Vector3.one * scale;
        nameText.color = timer < 0.6f && Mathf.Repeat(timer * 12f, 1f) < 0.5f ? Color.white : color;
        subtitleText.enabled = timer > 0.2f;

        // The new win's pip flashes in.
        pips.gameObject.SetActive(timer > 0.3f);
        if (wins > 0 && wins <= pipImages.Count)
        {
            Image newest = pipImages[wins - 1];
            bool lit = timer > 0.5f && (timer > 1f || Mathf.Repeat(timer * 10f, 1f) < 0.5f);
            newest.color = lit ? color : pipEmptyColor;
            newest.rectTransform.localScale = Vector3.one * (timer > 0.5f && timer < 0.65f ? 1.5f : 1f);
        }

        if (slowed && timer > slowTime)
        {
            slowed = false;
            if (hitStop != null)
                hitStop.SetSlowMotion(1f);
        }
        if (timer > duration)
            Hide();
    }

    public void Hide()
    {
        if (timer < 0f)
            return;
        timer = -1f;
        panel.SetActive(false);
        NameTag.HideAll = false;
        cameraRig.ClearFocus();
        if (slowed && hitStop != null)
            hitStop.SetSlowMotion(1f);
        slowed = false;
    }
}
