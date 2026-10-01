using System.Net.NetworkInformation;
using System.Net.Sockets;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;

// Main menu: SINGLE PLAYER (waves, a local host nobody can join), HOST LAN or JOIN LAN (by IP, up to 4 players).
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

    [Tooltip("Single player runs a local host on this port, so it never clashes with a LAN game on the same PC.")]
    [SerializeField] private ushort singlePort = 7779;

    [Min(1)]
    [SerializeField] private int maxPlayers = 4;

    [Header("Menu")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private Button singleButton;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private InputField ipInput;
    [SerializeField] private Text statusText;

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

    private void StartSingle()
    {
        GameMode.Current = GameMode.Mode.Single;
        singleModeRoot.SetActive(true); // before the player spawns, so it finds the cores
        transport.SetConnectionData("127.0.0.1", singlePort, "127.0.0.1"); // only this machine can connect
        if (!network.StartHost())
        {
            singleModeRoot.SetActive(false);
            Fail("COULD NOT START THE GAME");
            return;
        }
        menuPanel.SetActive(false);
    }

    private void HostLan()
    {
        GameMode.Current = GameMode.Mode.Lan;
        maps.SetMap(maps.LobbyMap); // LAN players arrive in the lobby
        transport.SetConnectionData("127.0.0.1", port, "0.0.0.0"); // listen on every network card
        if (!network.StartHost())
        {
            Fail("COULD NOT HOST (PORT " + port + " IN USE?)");
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
            Fail("COULD NOT CONNECT TO " + ip);
            return;
        }
        statusText.text = "CONNECTING TO " + ip + "...";
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

    // Host side: at most Max Players; single player takes nobody else.
    private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        bool hostItself = request.ClientNetworkId == NetworkManager.ServerClientId;
        bool full = network.ConnectedClientsIds.Count >= maxPlayers;
        bool single = GameMode.Current == GameMode.Mode.Single;
        bool playing = MatchManager.Instance != null && MatchManager.Instance.InMatch;

        response.Approved = hostItself || (!full && !single && !playing);
        response.CreatePlayerObject = response.Approved && single; // LAN players spawn after picking a skin
        response.Reason = single ? "THAT GAME IS SINGLE PLAYER"
                        : full ? "ROOM IS FULL (" + maxPlayers + " PLAYERS)"
                        : playing ? "A MATCH IS ALREADY ON - TRY AGAIN LATER" : "";
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

        pendingMessage = string.IsNullOrEmpty(network.DisconnectReason) ? "DISCONNECTED" : network.DisconnectReason;
        GameMode.Restart(false);
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
            ? "HOSTING - YOUR IP: " + LocalIp() + "\nFRIENDS JOIN WITH THIS IP"
            : "WAITING FOR THE HOST TO START";
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
        lines.AppendLine("PLAYERS " + PlayerNetwork.All.Count + "/" + maxPlayers);
        for (int slot = 0; slot < maxPlayers; slot++)
        {
            foreach (PlayerNetwork player in PlayerNetwork.All)
            {
                if (player.Slot != slot)
                    continue;
                string you = player == PlayerNetwork.Local ? "  (YOU)" : "";
                string host = player.OwnerClientId == NetworkManager.ServerClientId ? "  HOST" : "";
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
