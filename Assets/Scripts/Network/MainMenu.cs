using System.Net.NetworkInformation;
using System.Net.Sockets;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;

// Main menu: SINGLE PLAYER (waves, no network at all), HOST LAN or JOIN LAN (by IP, up to 4 players),
// and the language (ENGLISH / TIẾNG VIỆT, see Lang).
// After hosting or joining, the player picks a skin (SkinPanel) and appears in the lobby map.
// A small lobby panel lists the players (between matches); the host starts the match
// (MatchManager). Nobody can join while a match is on. Losing the connection goes back to the menu.
// Command line (for test builds): -autohost, -autojoin <ip>, -bot (NetTestBot plays by itself).
public class MainMenu : MonoBehaviour
{
    [SerializeField] private NetworkManager network;
    [SerializeField] private UnityTransport transport;

    [Tooltip("Things that only exist in single player (waves, practice dummies, cores). Kept off until it starts.")]
    [SerializeField] private GameObject singleModeRoot;

    [Tooltip("LAN games use this port (open it in the firewall).")]
    [SerializeField] private ushort port = 7777;

    [SerializeField] private MapManager maps;

    [Tooltip("Spawned by the host of a LAN game: runs the match (rounds, wins, results).")]
    [SerializeField] private NetworkObject matchManagerPrefab;

    [Tooltip("LAN: shown after hosting or joining; the player spawns once a skin is picked.")]
    [SerializeField] private SkinPanel skinPanel;

    [Min(1)]
    [SerializeField] private int maxPlayers = 4;

    [Header("Menu")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private Button singleButton;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private InputField ipInput;
    [SerializeField] private Text statusText;

    [Header("Language")]
    [SerializeField] private Button englishButton;
    [SerializeField] private Button vietnameseButton;

    [Tooltip("Label color of the language in use (the other one is dimmed).")]
    [SerializeField] private Color languageOnColor = Color.white;

    [SerializeField] private Color languageOffColor = new Color(1f, 1f, 1f, 0.35f);

    [Header("Lobby")]
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private Text lobbyPlayersText;
    [SerializeField] private Text lobbyInfoText;
    [SerializeField] private Button leaveButton;

    [Tooltip("Host only: starts the match (needs at least 2 players).")]
    [SerializeField] private Button startMatchButton;

    [Tooltip("Shrinks the lobby panel to its title bar (and back).")]
    [SerializeField] private Button collapseButton;

    [Tooltip("Everything under the title: hidden while the panel is collapsed.")]
    [SerializeField] private GameObject lobbyBody;

    [Tooltip("Panel height while collapsed (UI pixels).")]
    [SerializeField] private float collapsedHeight = 76f;

    private const string LastIpKey = "LastJoinIp";

    // Shown on the menu after the scene was reloaded (e.g. why the connection was lost).
    private static string pendingMessage;

    private bool inLobby; // connected on LAN (the lobby panel itself hides during a match)
    private float lobbyPanelHeight;

    private void Awake()
    {
        PlayerNetwork.All.Clear();
        singleModeRoot.SetActive(false);
        menuPanel.SetActive(true);
        lobbyPanel.SetActive(false);
        statusText.text = pendingMessage ?? "";
        pendingMessage = null;
        ipInput.text = PlayerPrefs.GetString(LastIpKey, "127.0.0.1");

        singleButton.onClick.AddListener(StartSingle);
        hostButton.onClick.AddListener(HostLan);
        joinButton.onClick.AddListener(() => JoinLan(ipInput.text.Trim()));
        leaveButton.onClick.AddListener(() => GameMode.Restart(false));
        startMatchButton.onClick.AddListener(() => MatchManager.Instance.StartMatch());
        collapseButton.onClick.AddListener(ToggleLobbyPanel);
        englishButton.onClick.AddListener(() => SetLanguage(Lang.Language.English));
        vietnameseButton.onClick.AddListener(() => SetLanguage(Lang.Language.Vietnamese));
        ShowLanguage();
        lobbyPanelHeight = ((RectTransform)lobbyPanel.transform).sizeDelta.y;

        network.ConnectionApprovalCallback = ApproveConnection;
        network.OnClientConnectedCallback += OnClientConnected;
        network.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void OnDestroy()
    {
        if (network == null)
            return;
        network.ConnectionApprovalCallback = null;
        network.OnClientConnectedCallback -= OnClientConnected;
        network.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    private void Start()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-autohost")
                HostLan();
            else if (args[i] == "-autojoin" && i + 1 < args.Length)
                JoinLan(args[i + 1]);
            else if (args[i] == "-bot")
                gameObject.AddComponent<NetTestBot>();
        }

        if (GameMode.AutoStartSingle)
        {
            GameMode.AutoStartSingle = false;
            StartSingle();
        }
    }

    // No network in single player: the player is simply made here.
    private void StartSingle()
    {
        GameMode.Current = GameMode.Mode.Single;
        singleModeRoot.SetActive(true); // before the player appears, so it finds the cores
        Instantiate(network.NetworkConfig.PlayerPrefab).GetComponent<PlayerNetwork>().StartOffline();
        menuPanel.SetActive(false);
    }

    private void HostLan()
    {
        GameMode.Current = GameMode.Mode.Lan;
        maps.SetMap(maps.LobbyMap); // LAN players arrive in the lobby
        transport.SetConnectionData("127.0.0.1", port, "0.0.0.0"); // listen on every network card
        if (!network.StartHost())
        {
            Fail(Lang.T("COULD NOT HOST (PORT " + port + " IN USE?)", "KHÔNG THỂ TẠO PHÒNG (CỔNG " + port + " ĐANG BẬN?)"));
            return;
        }
        Instantiate(matchManagerPrefab).Spawn(); // every machine (also the ones joining later) gets the match
        ShowLobby();
    }

    private void JoinLan(string ip)
    {
        if (string.IsNullOrEmpty(ip))
            return;

        PlayerPrefs.SetString(LastIpKey, ip);
        GameMode.Current = GameMode.Mode.Lan;
        maps.SetMap(maps.LobbyMap); // LAN players arrive in the lobby
        transport.SetConnectionData(ip, port);
        if (!network.StartClient())
        {
            Fail(Lang.T("COULD NOT CONNECT TO ", "KHÔNG KẾT NỐI ĐƯỢC TỚI ") + ip);
            return;
        }
        statusText.text = Lang.T("CONNECTING TO ", "ĐANG KẾT NỐI TỚI ") + ip + "...";
        SetMenuButtons(false);
    }

    private void Fail(string message)
    {
        GameMode.Current = GameMode.Mode.None;
        statusText.text = message;
        SetMenuButtons(true);
    }

    private void SetMenuButtons(bool interactable)
    {
        singleButton.interactable = interactable;
        hostButton.interactable = interactable;
        joinButton.interactable = interactable;
    }

    // Host side: at most Max Players, and nobody joins during a match.
    private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        bool hostItself = request.ClientNetworkId == NetworkManager.ServerClientId;
        bool full = network.ConnectedClientsIds.Count >= maxPlayers;
        bool playing = MatchManager.Instance != null && MatchManager.Instance.InMatch;

        response.Approved = hostItself || (!full && !playing);
        response.CreatePlayerObject = false; // LAN players spawn after picking a skin
        response.Reason = full ? ReasonFull : playing ? ReasonPlaying : ""; // shown in the joining player's language
    }

    private const string ReasonFull = "full";
    private const string ReasonPlaying = "playing";

    // Why this machine was sent back to the menu, in its own language.
    private string ReasonText(string reason)
    {
        if (reason == ReasonFull)
            return Lang.T("ROOM IS FULL (" + maxPlayers + " PLAYERS)", "PHÒNG ĐÃ ĐỦ NGƯỜI (" + maxPlayers + " NGƯỜI)");
        if (reason == ReasonPlaying)
            return Lang.T("A MATCH IS ALREADY ON - TRY AGAIN LATER", "TRẬN ĐẤU ĐANG DIỄN RA - HÃY THỬ LẠI SAU");
        return Lang.T("DISCONNECTED", "MẤT KẾT NỐI");
    }

    private void OnClientConnected(ulong clientId)
    {
        if (GameMode.IsLan && clientId == network.LocalClientId)
            ShowLobby();
    }

    // A client that lost the host (or was refused) goes back to the menu.
    private void OnClientDisconnected(ulong clientId)
    {
        if (GameMode.Current == GameMode.Mode.None || network.IsServer || clientId != network.LocalClientId)
            return; // already leaving, or the host / someone else

        pendingMessage = ReasonText(network.DisconnectReason);
        GameMode.Restart(false);
    }

    private void SetLanguage(Lang.Language language)
    {
        Lang.Set(language); // fixed texts switch by themselves (LocalizedText)
        statusText.text = "";
        ShowLanguage();
    }

    // The language in use is bright, the other one dimmed.
    private void ShowLanguage()
    {
        bool vietnamese = Lang.Current == Lang.Language.Vietnamese;
        englishButton.GetComponentInChildren<Text>().color = vietnamese ? languageOffColor : languageOnColor;
        vietnameseButton.GetComponentInChildren<Text>().color = vietnamese ? languageOnColor : languageOffColor;
    }

    // The lobby panel's "-" / "+" button: only the title bar, or everything.
    private void ToggleLobbyPanel()
    {
        bool collapse = lobbyBody.activeSelf;
        lobbyBody.SetActive(!collapse);
        var rect = (RectTransform)lobbyPanel.transform;
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, collapse ? collapsedHeight : lobbyPanelHeight);
        collapseButton.GetComponentInChildren<Text>().text = collapse ? "+" : "-";
    }

    private void ShowLobby()
    {
        menuPanel.SetActive(false);
        inLobby = true;
        lobbyInfoText.text = network.IsServer
            ? Lang.T("HOSTING - YOUR IP: ", "ĐANG LÀM CHỦ PHÒNG - IP CỦA BẠN: ") + LocalIp() +
              Lang.T("\nFRIENDS JOIN WITH THIS IP", "\nBẠN BÈ VÀO BẰNG IP NÀY")
            : Lang.T("WAITING FOR THE HOST TO START", "ĐANG CHỜ CHỦ PHÒNG BẮT ĐẦU");
        skinPanel.Show(); // the player appears in the lobby once it picked a skin
    }

    private void Update()
    {
        // The lobby panel is up between matches only.
        MatchManager match = MatchManager.Instance;
        bool show = inLobby && (match == null || !match.InMatch);
        if (lobbyPanel.activeSelf != show)
            lobbyPanel.SetActive(show);
        if (!show)
            return;

        startMatchButton.gameObject.SetActive(network.IsServer);
        startMatchButton.interactable = match != null && match.CanStart;

        var lines = new System.Text.StringBuilder();
        lines.AppendLine(Lang.T("PLAYERS ", "NGƯỜI CHƠI ") + PlayerNetwork.All.Count + "/" + maxPlayers);
        for (int slot = 0; slot < maxPlayers; slot++)
        {
            foreach (PlayerNetwork player in PlayerNetwork.All)
            {
                if (player.Slot != slot)
                    continue;
                string you = player == PlayerNetwork.Local ? Lang.T("  (YOU)", "  (BẠN)") : "";
                string host = player.OwnerClientId == NetworkManager.ServerClientId ? Lang.T("  HOST", "  CHỦ PHÒNG") : "";
                lines.AppendLine("<color=#" + ColorUtility.ToHtmlStringRGB(player.Color) + ">P" + (slot + 1) + "</color>" + host + you);
            }
        }
        lobbyPlayersText.text = lines.ToString();
    }

    // This machine's address on the local network (what the others type to join).
    private static string LocalIp()
    {
        foreach (NetworkInterface card in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (card.OperationalStatus != OperationalStatus.Up || card.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;
            foreach (UnicastIPAddressInformation address in card.GetIPProperties().UnicastAddresses)
                if (address.Address.AddressFamily == AddressFamily.InterNetwork)
                    return address.Address.ToString();
        }
        return "127.0.0.1";
    }
}
