using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;

// The menu screens before a game.
// - Title: your name (shown above your head in the lobby and the match), PLAY LAN, SINGLE PLAYER, COLLECTION (every
//   core, CollectionPanel), SETTINGS, QUIT.
// - LAN: the rooms on the local network (found by themselves, LanDiscovery: click one to join),
//   CREATE ROOM and JOIN BY IP.
// - Create room: name, password (optional), max players. Join by IP: address and password. A locked room from
//   the list opens the same box with its address filled in.
// After hosting or joining, the player makes its character (CharacterPanel) and appears in the lobby map, where the room is set
// up and started (MatchManager, LobbyStation, ReadyZone). Losing the connection comes back here with the reason.
// Command line (for test builds): -port <n> (before -autohost / -autojoin), -autohost, -autojoin <ip>, -bot (NetTestBot).
public class MainMenu : MonoBehaviour
{
    // Why the host sent a player back (sent as a code, shown in the player's own language).
    public const string ReasonFull = "full";
    public const string ReasonPlaying = "playing";
    public const string ReasonPassword = "password";
    public const string ReasonKicked = "kicked";
    public const string ReasonClosed = "closed";

    [SerializeField] private NetworkManager network;
    [SerializeField] private UnityTransport transport;

    [Tooltip("Things that only exist in single player (waves, practice dummies, cores). Kept off until it starts.")]
    [SerializeField] private GameObject singleModeRoot;

    [Tooltip("LAN games use this port (open it in the firewall).")]
    [SerializeField] private ushort port = 7777;

    [SerializeField] private MapManager maps;

    [Tooltip("Spawned by the host of a LAN game: runs the match (rounds, wins, results).")]
    [SerializeField] private NetworkObject matchManagerPrefab;

    [Tooltip("LAN: shown after hosting or joining; the player spawns once the character is made.")]
    [SerializeField] private CharacterPanel characterPanel;

    [SerializeField] private SettingsMenu settingsMenu;
    [SerializeField] private LanDiscovery discovery;

    [Tooltip("The blurred map behind the menu screens (hidden in a game).")]
    [SerializeField] private GameObject menuScene;

    [Header("Title screen")]
    [SerializeField] private GameObject titlePanel;
    [SerializeField] private InputField nameInput;
    [SerializeField] private Button lanButton;
    [SerializeField] private Button singleButton;
    [SerializeField] private Button collectionButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;

    [Tooltip("Every core in the game (opened by Collection Button).")]
    [SerializeField] private CollectionPanel collectionPanel;

    [Header("LAN screen")]
    [SerializeField] private GameObject lanPanel;
    [SerializeField] private Button createButton;
    [SerializeField] private Button joinIpButton;
    [SerializeField] private Button backButton;

    [Tooltip("One button per room found is made from this (kept off itself), under the same parent.")]
    [SerializeField] private Button roomButtonTemplate;

    [Tooltip("\"Searching for rooms...\" while the list is empty.")]
    [SerializeField] private Text roomsStatusText;

    [Tooltip("Connecting..., or why the last game ended (kicked, room closed...).")]
    [SerializeField] private Text statusText;

    [Header("Create room box")]
    [SerializeField] private GameObject createPanel;
    [SerializeField] private InputField roomNameInput;
    [SerializeField] private InputField createPasswordInput;

    [Tooltip("2, 3 and 4 players.")]
    [SerializeField] private Button[] maxPlayerButtons;

    [SerializeField] private Button createConfirmButton;
    [SerializeField] private Button createCancelButton;

    [Header("Join box (by IP, or a locked room)")]
    [SerializeField] private GameObject joinPanel;
    [SerializeField] private InputField ipInput;
    [SerializeField] private InputField joinPasswordInput;
    [SerializeField] private Button joinConfirmButton;
    [SerializeField] private Button joinCancelButton;

    [Header("Look")]
    [SerializeField] private Color choiceOnColor = new Color(1f, 0.902f, 0.427f);
    [SerializeField] private Color choiceOffColor = new Color(1f, 1f, 1f, 0.45f);

    private const string LastIpKey = "LastJoinIp";

    // Shown on the LAN screen after the scene was reloaded (why the connection was lost).
    private static string pendingMessage;

    // Host: the room as it was created.
    private string roomPassword = "";
    private int maxPlayers = 4;

    private void Awake()
    {
        // A loaded scene always starts on the menu (in the editor the last play's mode would stay otherwise).
        GameMode.Current = GameMode.Mode.None;
        PlayerNetwork.All.Clear();
        singleModeRoot.SetActive(false);
        createPanel.SetActive(false);
        joinPanel.SetActive(false);
        roomButtonTemplate.gameObject.SetActive(false);

        // Back from a LAN game with a reason: straight to the LAN screen to try again.
        bool fromLan = pendingMessage != null;
        titlePanel.SetActive(!fromLan);
        lanPanel.SetActive(fromLan);
        statusText.text = pendingMessage ?? "";
        pendingMessage = null;

        nameInput.characterLimit = PlayerProfile.MaxNameLength;
        nameInput.text = PlayerProfile.Name;
        nameInput.onEndEdit.AddListener(SaveName);
        ipInput.text = PlayerPrefs.GetString(LastIpKey, "");

        lanButton.onClick.AddListener(() =>
        {
            SaveName(nameInput.text);
            ShowScreen(lanPanel);
        });
        singleButton.onClick.AddListener(() =>
        {
            SaveName(nameInput.text);
            StartSingle();
        });
        collectionButton.onClick.AddListener(collectionPanel.Open);
        settingsButton.onClick.AddListener(settingsMenu.Open);
        quitButton.onClick.AddListener(Application.Quit);
        backButton.onClick.AddListener(() => ShowScreen(titlePanel));
        createButton.onClick.AddListener(OpenCreateBox);
        joinIpButton.onClick.AddListener(() => OpenJoinBox(ipInput.text, false));
        createConfirmButton.onClick.AddListener(HostLan);
        createCancelButton.onClick.AddListener(() => createPanel.SetActive(false));
        joinConfirmButton.onClick.AddListener(() => JoinLan(ipInput.text.Trim(), joinPasswordInput.text));
        joinCancelButton.onClick.AddListener(() => joinPanel.SetActive(false));
        for (int i = 0; i < maxPlayerButtons.Length; i++)
        {
            int players = i + 2; // captured by the click
            maxPlayerButtons[i].onClick.AddListener(() => SetMaxPlayers(players));
        }
        Lang.Changed += ClearStatus;

        network.ConnectionApprovalCallback = ApproveConnection;
        network.OnClientConnectedCallback += OnClientConnected;
        network.OnClientDisconnectCallback += OnClientDisconnected;
        discovery.StartListening();
    }

    private void OnDestroy()
    {
        Lang.Changed -= ClearStatus;
        if (network == null)
            return;
        // Leaving play mode (or quitting) while in a room: close the connection, or the port stays taken.
        if (network.IsListening && !network.ShutdownInProgress)
            network.Shutdown(true);
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
                JoinLan(args[i + 1], "");
            else if (args[i] == "-port" && i + 1 < args.Length && ushort.TryParse(args[i + 1], out ushort testPort))
                port = testPort;
            else if (args[i] == "-bot")
                gameObject.AddComponent<NetTestBot>();
        }

        if (GameMode.AutoStartSingle)
        {
            GameMode.AutoStartSingle = false;
            StartSingle();
        }
    }

    private void ShowScreen(GameObject screen)
    {
        menuScene.SetActive(true);
        titlePanel.SetActive(screen == titlePanel);
        lanPanel.SetActive(screen == lanPanel);
        createPanel.SetActive(false);
        joinPanel.SetActive(false);
        statusText.text = "";
    }

    private void HideMenu()
    {
        menuScene.SetActive(false);
        titlePanel.SetActive(false);
        lanPanel.SetActive(false);
        createPanel.SetActive(false);
        joinPanel.SetActive(false);
    }

    private void SaveName(string name)
    {
        PlayerProfile.Name = name;
        nameInput.text = PlayerProfile.Name; // cleaned (or a new one if it was left empty)
    }

    // The last message was in the other language.
    private void ClearStatus() => statusText.text = "";

    // ---------- Single player ----------

    // No network in single player: the player is simply made here.
    private void StartSingle()
    {
        GameMode.Current = GameMode.Mode.Single;
        HideMenu(); // ends the menu's blurred backdrop first
        maps.SetMap(maps.StartMap);
        singleModeRoot.SetActive(true); // before the player appears, so it finds the cores
        Instantiate(network.NetworkConfig.PlayerPrefab).GetComponent<PlayerNetwork>().StartOffline();
        discovery.StopListening();
    }

    // ---------- Host ----------

    private void OpenCreateBox()
    {
        SaveName(nameInput.text);
        roomNameInput.text = Lang.T(PlayerProfile.Name + "'S ROOM", "PHÒNG CỦA " + PlayerProfile.Name);
        createPasswordInput.text = "";
        SetMaxPlayers(4);
        joinPanel.SetActive(false);
        createPanel.SetActive(true);
    }

    private void SetMaxPlayers(int players)
    {
        maxPlayers = players;
        for (int i = 0; i < maxPlayerButtons.Length; i++)
            maxPlayerButtons[i].GetComponentInChildren<Text>().color = i + 2 == players ? choiceOnColor : choiceOffColor;
    }

    private void HostLan()
    {
        string roomName = PlayerProfile.Clean(roomNameInput.text, PlayerProfile.MaxRoomNameLength);
        if (roomName.Length == 0)
            roomName = Lang.T(PlayerProfile.Name + "'S ROOM", "PHÒNG CỦA " + PlayerProfile.Name);
        roomPassword = createPasswordInput.text.Trim();

        GameMode.Current = GameMode.Mode.Lan;
        transport.SetConnectionData("127.0.0.1", port, "0.0.0.0"); // listen on every network card
        if (!network.StartHost())
        {
            Fail(Lang.T("COULD NOT CREATE THE ROOM (PORT " + port + " IN USE?)", "KHÔNG TẠO ĐƯỢC PHÒNG (CỔNG " + port + " ĐANG BẬN?)"));
            return;
        }
        NetworkObject match = Instantiate(matchManagerPrefab);
        match.Spawn(); // every machine (also the ones joining later) gets the match
        match.GetComponent<MatchManager>().SetRoomOnHost(roomName);
        GameMode.RoomAddress = LocalIp();
        discovery.StopListening();
        discovery.StartAnnouncing(roomName, roomPassword.Length > 0, maxPlayers); // shows up on the other PCs
        EnterRoom();
    }

    // Host side: room full, match on, wrong password. The reason goes as a code (see ReasonText).
    private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        bool hostItself = request.ClientNetworkId == NetworkManager.ServerClientId;
        bool full = network.ConnectedClientsIds.Count >= maxPlayers;
        bool playing = MatchManager.Instance != null && MatchManager.Instance.InMatch;
        string password = request.Payload != null ? Encoding.UTF8.GetString(request.Payload) : "";
        bool wrongPassword = roomPassword.Length > 0 && password != roomPassword;

        response.Approved = hostItself || (!full && !playing && !wrongPassword);
        response.CreatePlayerObject = false; // LAN players spawn after picking a skin
        response.Reason = full ? ReasonFull : playing ? ReasonPlaying : wrongPassword ? ReasonPassword : "";
    }

    // ---------- Join ----------

    // A room from the list: open rooms join right away, locked ones ask for the password first.
    private void JoinRoom(int index)
    {
        if (index >= discovery.Rooms.Count)
            return;
        LanDiscovery.Room room = discovery.Rooms[index];
        if (room.locked)
            OpenJoinBox(room.address, true);
        else
            JoinLan(room.address, "");
    }

    private void OpenJoinBox(string ip, bool forLockedRoom)
    {
        SaveName(nameInput.text);
        ipInput.text = ip;
        joinPasswordInput.text = "";
        createPanel.SetActive(false);
        joinPanel.SetActive(true);
        (forLockedRoom ? joinPasswordInput : ipInput).ActivateInputField();
    }

    private void JoinLan(string ip, string password)
    {
        if (string.IsNullOrEmpty(ip))
        {
            statusText.text = Lang.T("TYPE THE HOST'S IP FIRST", "HÃY NHẬP IP CỦA CHỦ PHÒNG");
            return;
        }

        PlayerPrefs.SetString(LastIpKey, ip);
        GameMode.Current = GameMode.Mode.Lan;
        GameMode.RoomAddress = ip;
        transport.SetConnectionData(ip, port);
        network.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(password ?? "");
        if (!network.StartClient())
        {
            Fail(Lang.T("COULD NOT CONNECT TO ", "KHÔNG KẾT NỐI ĐƯỢC TỚI ") + ip);
            return;
        }
        createPanel.SetActive(false);
        joinPanel.SetActive(false);
        statusText.text = Lang.T("CONNECTING TO ", "ĐANG KẾT NỐI TỚI ") + ip + "...";
        SetButtons(false);
        discovery.StopListening();
    }

    private void Fail(string message)
    {
        GameMode.Current = GameMode.Mode.None;
        statusText.text = message;
        SetButtons(true);
        discovery.StartListening();
    }

    private void SetButtons(bool interactable)
    {
        createButton.interactable = interactable;
        joinIpButton.interactable = interactable;
        backButton.interactable = interactable;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (GameMode.IsLan && clientId == network.LocalClientId && !network.IsServer)
            EnterRoom();
    }

    // A client that lost the host (or was refused / kicked) goes back to the LAN screen with the reason.
    private void OnClientDisconnected(ulong clientId)
    {
        if (GameMode.Current == GameMode.Mode.None || network.IsServer || clientId != network.LocalClientId)
            return; // already leaving, or the host / someone else

        pendingMessage = ReasonText(network.DisconnectReason);
        GameMode.Restart(false);
    }

    // Why this machine was sent back to the menu, in its own language.
    private static string ReasonText(string reason)
    {
        switch (reason)
        {
            case ReasonFull: return Lang.T("THE ROOM IS FULL", "PHÒNG ĐÃ ĐỦ NGƯỜI");
            case ReasonPlaying: return Lang.T("A MATCH IS ON IN THAT ROOM - TRY AGAIN LATER", "PHÒNG ĐANG ĐẤU - HÃY THỬ LẠI SAU");
            case ReasonPassword: return Lang.T("WRONG PASSWORD", "SAI MẬT KHẨU");
            case ReasonKicked: return Lang.T("YOU WERE KICKED FROM THE ROOM", "BẠN ĐÃ BỊ ĐUỔI KHỎI PHÒNG");
            case ReasonClosed: return Lang.T("THE HOST CLOSED THE ROOM", "CHỦ PHÒNG ĐÃ ĐÓNG PHÒNG");
            default: return Lang.T("CONNECTION LOST", "MẤT KẾT NỐI");
        }
    }

    // In the room: make the character, then the player appears in the lobby.
    private void EnterRoom()
    {
        HideMenu(); // ends the menu's blurred backdrop first
        maps.SetMap(maps.LobbyMap); // LAN players arrive in the lobby
        statusText.text = "";
        characterPanel.Show();
    }

    // ---------- LAN rooms found by themselves ----------

    private void Update()
    {
        if (lanPanel.activeSelf)
            ShowRooms();
    }

    // One button per room heard (made from the template, reused while the list keeps its size).
    private void ShowRooms()
    {
        var buttons = new List<Button>();
        foreach (Transform child in roomButtonTemplate.transform.parent)
            if (child != roomButtonTemplate.transform && child.GetComponent<Button>() != null)
                buttons.Add(child.GetComponent<Button>());

        var rooms = discovery.Rooms;
        for (int i = buttons.Count; i < rooms.Count; i++)
        {
            Button made = Instantiate(roomButtonTemplate, roomButtonTemplate.transform.parent);
            made.gameObject.SetActive(true);
            int index = i; // the button joins whatever room is at its place in the list
            made.onClick.AddListener(() => JoinRoom(index));
            buttons.Add(made);
        }
        for (int i = 0; i < buttons.Count; i++)
        {
            buttons[i].gameObject.SetActive(i < rooms.Count);
            if (i >= rooms.Count)
                continue;
            LanDiscovery.Room room = rooms[i];
            Text[] texts = buttons[i].GetComponentsInChildren<Text>(true);
            texts[0].text = (room.locked ? "[" + Lang.T("LOCKED", "KHÓA") + "]  " : "") + room.name;
            texts[1].text = room.playing ? Lang.T("PLAYING", "ĐANG ĐẤU")
                          : room.players + "/" + room.maxPlayers + (room.players >= room.maxPlayers ? Lang.T("  FULL", "  ĐẦY") : "");
            buttons[i].interactable = room.CanJoin && createButton.interactable;
        }

        roomsStatusText.text = rooms.Count > 0 ? ""
                             : discovery.Listening ? Lang.T("SEARCHING FOR ROOMS ON YOUR NETWORK...", "ĐANG TÌM PHÒNG TRONG MẠNG LAN...")
                             : Lang.T("CAN'T SEARCH FOR ROOMS (PORT IN USE)", "KHÔNG TÌM ĐƯỢC PHÒNG (CỔNG ĐANG BẬN)");
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
