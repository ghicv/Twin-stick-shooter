using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using Unity.Netcode;
using UnityEngine;

// Finds LAN rooms by itself, so nobody has to type an IP.
// - A host announces its room every second: a small UDP broadcast on the Discovery Port (not the game port)
//   with the room's name, player count, whether a match is on and whether it has a password.
// - The main menu listens and keeps a list of every room it heard lately (Rooms); a room that goes quiet is dropped.
// Joining a room still goes through the normal LAN port (MainMenu.JoinLan with the room's address).
public class LanDiscovery : MonoBehaviour
{
    public class Room
    {
        public string address; // where the announcement came from = the host's IP
        public string name;
        public int players;
        public int maxPlayers;
        public bool playing;
        public bool locked; // needs a password
        public float lastHeard; // real time

        public bool CanJoin => !playing && players < maxPlayers;
    }

    [Tooltip("UDP port the rooms are announced on. Must be the same on every PC (and differ from the game port).")]
    [SerializeField] private int discoveryPort = 47777;

    [Tooltip("How often a host announces its room (seconds).")]
    [SerializeField] private float announceInterval = 1f;

    [Tooltip("A room not heard from for this long leaves the list (seconds).")]
    [SerializeField] private float roomTimeout = 3.5f;

    // Only messages starting with this count (other programs may use the same port).
    private const string MessageTag = "PIXELFIGHT-ROOM";

    private readonly List<Room> rooms = new List<Room>();
    private UdpClient listener;
    private UdpClient announcer;
    private int announceMaxPlayers;
    private string announceName;
    private bool announceLocked;
    private float announceTimer;

    public IReadOnlyList<Room> Rooms => rooms;
    public bool Listening => listener != null;

    // ---------- Menu: listen for rooms ----------

    public void StartListening()
    {
        if (listener != null)
            return;
        try
        {
            listener = new UdpClient();
            // Several copies of the game on one PC can all listen (testing).
            listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            listener.Client.Bind(new IPEndPoint(IPAddress.Any, discoveryPort));
        }
        catch (SocketException e)
        {
            Debug.LogWarning("LAN discovery can't listen on port " + discoveryPort + ": " + e.Message);
            StopListening();
        }
    }

    public void StopListening()
    {
        if (listener != null)
            listener.Close();
        listener = null;
        rooms.Clear();
    }

    // ---------- Host: announce the room ----------

    public void StartAnnouncing(string roomName, bool locked, int maxPlayers)
    {
        if (announcer != null)
            return;
        announceName = roomName.Replace("|", "");
        announceLocked = locked;
        announceMaxPlayers = maxPlayers;
        announcer = new UdpClient { EnableBroadcast = true };
        announceTimer = 0f; // right away
    }

    public void StopAnnouncing()
    {
        if (announcer != null)
            announcer.Close();
        announcer = null;
    }

    private void OnDestroy()
    {
        StopListening();
        StopAnnouncing();
    }

    private void Update()
    {
        if (listener != null)
            Listen();

        if (announcer != null)
        {
            announceTimer -= Time.unscaledDeltaTime;
            if (announceTimer <= 0f)
            {
                announceTimer = announceInterval;
                Announce();
            }
        }
    }

    private void Listen()
    {
        try
        {
            while (listener.Available > 0)
            {
                var from = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = listener.Receive(ref from);
                Heard(Encoding.UTF8.GetString(data), from.Address.ToString());
            }
        }
        catch (SocketException)
        {
            // A bad packet or the socket closing: just keep the list as it is.
        }

        rooms.RemoveAll(room => Time.unscaledTime - room.lastHeard > roomTimeout);
    }

    // Message: tag|name|players|maxPlayers|playing(0/1)|locked(0/1)
    private void Heard(string message, string address)
    {
        string[] parts = message.Split('|');
        if (parts.Length != 6 || parts[0] != MessageTag)
            return;

        Room room = rooms.Find(r => r.address == address);
        if (room == null)
        {
            room = new Room { address = address };
            rooms.Add(room);
        }
        room.name = parts[1];
        int.TryParse(parts[2], out room.players);
        int.TryParse(parts[3], out room.maxPlayers);
        room.playing = parts[4] == "1";
        room.locked = parts[5] == "1";
        room.lastHeard = Time.unscaledTime;
    }

    private void Announce()
    {
        NetworkManager network = NetworkManager.Singleton;
        int players = network != null && network.IsServer ? network.ConnectedClientsIds.Count : 1;
        bool playing = MatchManager.Instance != null && MatchManager.Instance.InMatch;
        string message = MessageTag + "|" + announceName + "|" + players + "|" + announceMaxPlayers + "|" +
                         (playing ? "1" : "0") + "|" + (announceLocked ? "1" : "0");
        byte[] data = Encoding.UTF8.GetBytes(message);

        foreach (IPAddress address in BroadcastAddresses())
        {
            try
            {
                announcer.Send(data, data.Length, new IPEndPoint(address, discoveryPort));
            }
            catch (SocketException)
            {
                // That network card can't send now: the others still can.
            }
        }
    }

    // Everyone on every local network this PC is on (a PC with several network cards would
    // otherwise only announce on one of them).
    private static List<IPAddress> BroadcastAddresses()
    {
        var list = new List<IPAddress> { IPAddress.Broadcast };
        foreach (NetworkInterface card in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (card.OperationalStatus != OperationalStatus.Up || card.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;
            foreach (UnicastIPAddressInformation info in card.GetIPProperties().UnicastAddresses)
            {
                if (info.Address.AddressFamily != AddressFamily.InterNetwork || info.IPv4Mask == null)
                    continue;
                byte[] ip = info.Address.GetAddressBytes();
                byte[] mask = info.IPv4Mask.GetAddressBytes();
                for (int i = 0; i < 4; i++)
                    ip[i] = (byte)(ip[i] | ~mask[i]);
                list.Add(new IPAddress(ip));
            }
        }
        return list;
    }
}
