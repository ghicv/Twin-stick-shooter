using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The LAN match's on-screen parts (part of the scene). The MatchManager is spawned over the network when a LAN
// game is hosted, so it finds these here instead of holding them itself.
// The results at the end of a match are a little show: each player sees VICTORY! or DEFEAT (or DRAW), the ranking
// rows slide in one by one from the last place up, and the winner's row gets a crown and a burst of confetti.
public class MatchUI : MonoBehaviour
{
    [System.Serializable]
    private class ResultRow
    {
        public GameObject root;
        public Text rank;
        public Text name;
        public Text wins;
        public Text cores;
    }

    [Tooltip("Big text in the middle: ROUND 3, NAME WINS THE ROUND!, ...")]
    [SerializeField] private Text bannerText;

    [Tooltip("The lobby's 3-2-1 before a match.")]
    [SerializeField] private Text countdownText;

    [SerializeField] private ChatBox chat;
    [SerializeField] private KillFeed killFeed;

    [Header("Results")]
    [SerializeField] private GameObject resultsPanel;
    [SerializeField] private Text resultsTitle;

    [Tooltip("VICTORY! / DEFEAT / DRAW: what this player sees.")]
    [SerializeField] private Text outcomeText;

    [Tooltip("One per place (1st to 4th).")]
    [SerializeField] private ResultRow[] resultRows;

    [Tooltip("Sits on the winner's row.")]
    [SerializeField] private Image crown;

    [SerializeField] private PixelConfetti confetti;
    [SerializeField] private Button lobbyButton;
    [SerializeField] private Text resultsWaitText;

    [Header("Results show")]
    [Tooltip("Seconds between two rows sliding in.")]
    [SerializeField] private float rowDelay = 0.2f;

    [SerializeField] private float rowSlideTime = 0.25f;
    [SerializeField] private Color victoryColor = new Color(1f, 0.85f, 0.3f);
    [SerializeField] private Color defeatColor = new Color(0.75f, 0.4f, 0.45f);
    [SerializeField] private AudioClip victorySound;
    [SerializeField] private AudioClip defeatSound;
    [SerializeField] private AudioClip rowSound;
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.6f;

    private Vector2[] rowPositions;
    private int rowCount;
    private float resultsTimer = -1f;
    private bool won;
    private Color winnerColor;

    public Text BannerText => bannerText;
    public Text CountdownText => countdownText;
    public ChatBox Chat => chat;
    public KillFeed KillFeed => killFeed;
    public Button LobbyButton => lobbyButton;

    private void Awake()
    {
        resultsPanel.SetActive(false);
        bannerText.text = "";
        countdownText.text = "";
        rowPositions = new Vector2[resultRows.Length];
        for (int i = 0; i < resultRows.Length; i++)
            rowPositions[i] = ((RectTransform)resultRows[i].root.transform).anchoredPosition;
    }

    // ranking: most round wins first.
    public void ShowResults(string winnerName, Color winnerColor, List<PlayerNetwork> ranking, bool host)
    {
        MatchManager match = MatchManager.Instance;
        resultsTitle.text = winnerName.Length > 0 ? winnerName + Lang.T(" WINS THE MATCH!", " THẮNG CẢ TRẬN!")
                                                  : Lang.T("IT'S A DRAW!", "HÒA CẢ TRẬN!");
        resultsTitle.color = winnerName.Length > 0 ? winnerColor : Color.white;

        bool draw = winnerName.Length == 0;
        won = !draw && PlayerNetwork.Local != null && ranking.Count > 0 && ranking[0] == PlayerNetwork.Local;
        this.winnerColor = winnerColor;
        outcomeText.text = draw ? Lang.T("DRAW", "HÒA") : won ? Lang.T("VICTORY!", "CHIẾN THẮNG!") : Lang.T("DEFEAT", "THẤT BẠI");
        outcomeText.color = draw ? Color.white : won ? victoryColor : defeatColor;

        rowCount = Mathf.Min(ranking.Count, resultRows.Length);
        for (int i = 0; i < resultRows.Length; i++)
        {
            ResultRow row = resultRows[i];
            row.root.SetActive(false); // they slide in one by one
            if (i >= rowCount)
                continue;
            PlayerNetwork player = ranking[i];
            row.rank.text = "#" + (i + 1);
            row.name.text = player.Name + (player == PlayerNetwork.Local ? Lang.T(" (YOU)", " (BẠN)") : "");
            row.name.color = player.Color;
            row.wins.text = (match != null ? match.Wins(player.Slot) : 0) + Lang.T(" WINS", " THẮNG");
            row.cores.text = player.Cores.OwnedCount + " CORE";
        }
        crown.gameObject.SetActive(false);

        lobbyButton.gameObject.SetActive(host);
        resultsWaitText.text = host ? "" : Lang.T("WAITING FOR THE HOST...", "ĐANG CHỜ CHỦ PHÒNG...");
        resultsTimer = 0f;
        resultsPanel.SetActive(true);
        GameAudio.Play(won || draw ? victorySound : defeatSound, volume);
    }

    public void HideResults()
    {
        resultsPanel.SetActive(false);
        resultsTimer = -1f;
    }

    private void Update()
    {
        if (resultsTimer < 0f)
            return;
        resultsTimer += Time.unscaledDeltaTime;

        // The outcome pops in big and settles in steps.
        float scale = resultsTimer < 0.08f ? 1.8f : resultsTimer < 0.16f ? 1.4f : resultsTimer < 0.24f ? 1.15f : 1f;
        outcomeText.rectTransform.localScale = Vector3.one * scale;

        // Rows slide in from the right, last place first, the winner last.
        for (int place = rowCount - 1; place >= 0; place--)
        {
            int order = rowCount - 1 - place;
            float since = resultsTimer - 0.5f - order * rowDelay;
            if (since < 0f)
                continue;
            ResultRow row = resultRows[place];
            if (!row.root.activeSelf)
            {
                row.root.SetActive(true);
                GameAudio.Play(rowSound, volume * 0.6f, 0.05f, 1f + order * 0.1f);
                if (place == 0)
                    WinnerArrives(row);
            }
            float slide = Mathf.Clamp01(since / rowSlideTime);
            float eased = 1f - (1f - slide) * (1f - slide) * (1f - slide);
            ((RectTransform)row.root.transform).anchoredPosition = rowPositions[place] + new Vector2((1f - eased) * 1200f, 0f);
        }

        // The crown bobs a little on the winner's row.
        if (crown.gameObject.activeSelf)
            crown.rectTransform.localScale = Vector3.one * (Mathf.Repeat(resultsTimer * 2f, 1f) < 0.5f ? 1f : 1.12f);
    }

    private void WinnerArrives(ResultRow row)
    {
        crown.gameObject.SetActive(true); // it sits on the first row
        var position = (Vector2)confetti.transform.InverseTransformPoint(row.root.transform.position);
        confetti.Burst(position, 90, new[] { winnerColor, Color.white, victoryColor }, 1500f);
    }
}
