using System.Net.NetworkInformation;
using System.Net.Sockets;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;

// Main menu: SINGLE PLAYER (waves, a local host nobody can join), HOST LAN or JOIN LAN (by IP, up to 4 players).
// After hosting or joining, a small lobby panel lists the players. Losing the connection goes back to the menu.
// Command line (for test builds): -autohost, -autojoin <ip>, -bot (NetTestBot plays by itself).
public class MainMenu : MonoBehaviour
{
    [SerializeField] private NetworkManager network;
    [SerializeField] private UnityTransport transport;

    [Tooltip("Things that only exist in single player (waves, practice dummies, cores). Kept off until it starts.")]
    [SerializeField] private GameObject singleModeRoot;

    [Tooltip("LAN games use this port (open it in the firewall).")]
    [SerializeField] private ushort port = 7777;

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

    private const string LastIpKey = "LastJoinIp";

    // Shown on the menu after the scene was reloaded (e.g. why the connection was lost).
    private static string pendingMessage;

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
        transport.SetConnectionData("127.0.0.1", port, "0.0.0.0"); // listen on every network card
        if (!network.StartHost())
        {
            Fail("COULD NOT HOST (PORT " + port + " IN USE?)");
            return;
        }
        ShowLobby();
    }

    private void JoinLan(string ip)
    {
        if (string.IsNullOrEmpty(ip))
            return;

        PlayerPrefs.SetString(LastIpKey, ip);
        GameMode.Current = GameMode.Mode.Lan;
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

        response.Approved = hostItself || (!full && !single);
        response.CreatePlayerObject = response.Approved;
        response.Reason = single ? "THAT GAME IS SINGLE PLAYER" : full ? "ROOM IS FULL (" + maxPlayers + " PLAYERS)" : "";
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

    private void ShowLobby()
    {
        menuPanel.SetActive(false);
        lobbyPanel.SetActive(true);
        lobbyInfoText.text = network.IsServer
            ? "HOSTING - YOUR IP: " + LocalIp() + "\nFRIENDS JOIN WITH THIS IP"
            : "CONNECTED";
    }

    private void Update()
    {
        if (!lobbyPanel.activeSelf)
            return;

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
