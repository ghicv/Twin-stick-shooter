using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The host sets the room up in the lobby ([E] anywhere there opens it; the lobby's signs show the values): the match
// length, the seconds to pick a card and how often everybody picks one, the same choices as the create room box.
// A click applies at once. MAPS switches to the map board.
public class RoomBoard : MonoBehaviour
{
    [SerializeField] private GameObject panel;

    [Tooltip("Labelled from the match's Round Choices.")]
    [SerializeField] private Button[] roundButtons;

    [Tooltip("Labelled from the match's Card Time Choices.")]
    [SerializeField] private Button[] cardTimeButtons;

    [Tooltip("A card pick every round, every other round.")]
    [SerializeField] private Button[] cardEveryButtons;

    [SerializeField] private Button mapsButton;
    [SerializeField] private MapBoard mapBoard;
    [SerializeField] private Button closeButton;

    [SerializeField] private Color onColor = new Color(1f, 0.902f, 0.427f);
    [SerializeField] private Color offColor = new Color(1f, 1f, 1f, 0.45f);

    public bool IsOpen => panel.activeSelf;

    private void Awake()
    {
        panel.SetActive(false);
        closeButton.onClick.AddListener(Close);
        mapsButton.onClick.AddListener(() =>
        {
            Close();
            mapBoard.Toggle();
        });
        for (int i = 0; i < roundButtons.Length; i++)
        {
            int index = i; // captured by the click
            roundButtons[i].onClick.AddListener(() => Match()?.SetRounds(Match().RoundChoices[index]));
        }
        for (int i = 0; i < cardTimeButtons.Length; i++)
        {
            int index = i;
            cardTimeButtons[i].onClick.AddListener(() => Match()?.SetCardTime(Match().CardTimeChoices[index]));
        }
        for (int i = 0; i < cardEveryButtons.Length; i++)
        {
            int every = i + 1;
            cardEveryButtons[i].onClick.AddListener(() => Match()?.SetCardEvery(every));
        }
    }

    private static MatchManager Match() => MatchManager.Instance;

    public void Toggle()
    {
        if (IsOpen)
            Close();
        else
            panel.SetActive(true);
    }

    public void Close()
    {
        panel.SetActive(false);
    }

    // [E] anywhere in the lobby opens / closes it (host only). The buttons show the choices, the current ones lit.
    private void Update()
    {
        MatchManager match = Match();
        if (IsOpen && (match == null || match.InMatch))
            Close();
        bool inLobby = match != null && match.IsSpawned && !match.InMatch && match.IsServer && PlayerNetwork.Local != null;
        if (inLobby && !InputLock.Blocked && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (mapBoard.IsOpen)
                mapBoard.Close(); // E closes the map board too (back to nothing)
            else
                Toggle();
        }
        if (!IsOpen)
            return;

        for (int i = 0; i < roundButtons.Length; i++)
            Show(roundButtons[i], match.RoundChoices[i].ToString(), match.RoundChoices[i] == match.Rounds);
        for (int i = 0; i < cardTimeButtons.Length; i++)
            Show(cardTimeButtons[i], match.CardTimeChoices[i] + "s", match.CardTimeChoices[i] == Mathf.RoundToInt(match.CardTime));
        for (int i = 0; i < cardEveryButtons.Length; i++)
            cardEveryButtons[i].GetComponentInChildren<Text>().color = i + 1 == match.CardEvery ? onColor : offColor;
    }

    private void Show(Button button, string text, bool on)
    {
        Text label = button.GetComponentInChildren<Text>();
        label.text = text;
        label.color = on ? onColor : offColor;
    }
}
