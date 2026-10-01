using UnityEngine;
using UnityEngine.UI;

// The LAN match's on-screen parts (part of the scene). The MatchManager is spawned over the network when a LAN
// game is hosted, so it finds these here instead of holding them itself.
public class MatchUI : MonoBehaviour
{
    [Tooltip("Big text in the middle: ROUND 3, P2 WINS THE ROUND!, ...")]
    [SerializeField] private Text bannerText;

    [Tooltip("Small text at the top: ROUND 3 / 8.")]
    [SerializeField] private Text roundText;

    [SerializeField] private GameObject resultsPanel;
    [SerializeField] private Text resultsTitle;
    [SerializeField] private Text resultsList;
    [SerializeField] private Button lobbyButton;
    [SerializeField] private Text resultsWaitText;

    public Text BannerText => bannerText;
    public Text RoundText => roundText;
    public GameObject ResultsPanel => resultsPanel;
    public Text ResultsTitle => resultsTitle;
    public Text ResultsList => resultsList;
    public Button LobbyButton => lobbyButton;
    public Text ResultsWaitText => resultsWaitText;

    private void Awake()
    {
        resultsPanel.SetActive(false);
        bannerText.text = "";
        roundText.text = "";
    }
}
