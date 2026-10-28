using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The LAN match's on-screen parts (part of the scene). The MatchManager is spawned over the network when a LAN
// game is hosted, so it finds these here instead of holding them itself.
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

    [Tooltip("One per place (1st to 4th).")]
    [SerializeField] private ResultRow[] resultRows;

    [SerializeField] private Button lobbyButton;
    [SerializeField] private Text resultsWaitText;

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
    }

    // ranking: most round wins first.
    public void ShowResults(string winnerName, Color winnerColor, List<PlayerNetwork> ranking, bool host)
    {
        MatchManager match = MatchManager.Instance;
        resultsTitle.text = winnerName.Length > 0 ? winnerName + Lang.T(" WINS THE MATCH!", " THẮNG CẢ TRẬN!")
                                                  : Lang.T("IT'S A DRAW!", "HÒA CẢ TRẬN!");
        resultsTitle.color = winnerName.Length > 0 ? winnerColor : Color.white;

        for (int i = 0; i < resultRows.Length; i++)
        {
            ResultRow row = resultRows[i];
            row.root.SetActive(i < ranking.Count);
            if (i >= ranking.Count)
                continue;
            PlayerNetwork player = ranking[i];
            row.rank.text = "#" + (i + 1);
            row.name.text = player.Name + (player == PlayerNetwork.Local ? Lang.T(" (YOU)", " (BẠN)") : "");
            row.name.color = player.Color;
            row.wins.text = (match != null ? match.Wins(player.Slot) : 0) + Lang.T(" WINS", " THẮNG");
            row.cores.text = player.Cores.OwnedCount + " CORE";
        }

        lobbyButton.gameObject.SetActive(host);
        resultsWaitText.text = host ? "" : Lang.T("WAITING FOR THE HOST...", "ĐANG CHỜ CHỦ PHÒNG...");
        resultsPanel.SetActive(true);
    }

    public void HideResults()
    {
        resultsPanel.SetActive(false);
    }
}
