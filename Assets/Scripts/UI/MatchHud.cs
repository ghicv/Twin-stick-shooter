using UnityEngine;
using UnityEngine.UI;

// Top of the screen during a LAN match: ROUND 3 / 8, and one chip per player with its name (in its color) and
// its round wins. In the lobby it shows the controls instead.
public class MatchHud : MonoBehaviour
{
    [System.Serializable]
    private class Chip
    {
        public GameObject root;
        public Image colorBar;
        public Text name;
        public Text wins;
    }

    [SerializeField] private GameObject matchGroup;
    [SerializeField] private Text roundText;

    [Tooltip("One per player (4).")]
    [SerializeField] private Chip[] chips;

    [Tooltip("Lobby: the keys (TAB, ENTER, ESC, E).")]
    [SerializeField] private Text lobbyHintText;

    private void Update()
    {
        MatchManager match = MatchManager.Instance;
        bool lan = GameMode.IsLan && match != null;
        bool inMatch = lan && match.InMatch && match.CurrentPhase != MatchManager.Phase.Results;
        matchGroup.SetActive(inMatch);
        lobbyHintText.gameObject.SetActive(lan && !match.InMatch && PlayerNetwork.Local != null);

        if (lobbyHintText.gameObject.activeSelf)
            lobbyHintText.text = Lang.T("[TAB] PLAYERS     [ENTER] CHAT     [E] USE     [ESC] MENU",
                                        "[TAB] NGƯỜI CHƠI     [ENTER] CHAT     [E] DÙNG     [ESC] MENU");
        if (!inMatch)
            return;

        roundText.text = Lang.T("ROUND ", "VÒNG ") + Mathf.Max(1, match.Round) + " / " + match.Rounds;
        for (int i = 0; i < chips.Length; i++)
        {
            PlayerNetwork player = i < PlayerNetwork.All.Count ? PlayerNetwork.All[i] : null;
            chips[i].root.SetActive(player != null);
            if (player == null)
                continue;
            chips[i].colorBar.color = player.Color;
            chips[i].name.text = player.Name;
            chips[i].name.color = player.Dead ? new Color(player.Color.r, player.Color.g, player.Color.b, 0.4f) : player.Color;
            chips[i].wins.text = match.Wins(player.Slot).ToString();
        }
    }
}
