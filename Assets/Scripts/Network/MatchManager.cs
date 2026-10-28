using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

// LAN match (players against each other). The host runs it; every machine shows it.
// Lobby: players walk around the lobby map. The host sets the room up at the lobby's stations (rounds, card time,
// maps). Everybody standing in the READY zone (at least Min Players) starts a 3-2-1 countdown, then the match.
// Rounds: every round is on another map, shown first by the map roulette (MapRoulette). Everybody arrives with full
// health (the travel sequence also brings the dead back). A player who dies stays out until the next round; the last
// one standing wins the round.
// Card milestones (Card Rounds): before those rounds everybody picks one of 3 cards of the same rarity (silver, gold,
// diamond), Card Time seconds, then a random card.
// After Rounds rounds the most round wins takes the match; a tie plays tiebreak rounds (at most Max Tiebreaks).
// Results: the ranking; the host goes back to the lobby, where everybody starts again without cores.
// Also here: chat, the kill feed, "joined / left / kicked" notices, and kicking (host).
// The host spawns it (a network prefab) when it starts a LAN game; its on-screen parts are the scene's MatchUI.
public class MatchManager : NetworkBehaviour
{
    public enum Phase { Lobby = 0, Round = 1, BetweenRounds = 2, Results = 3 }

    private enum Notice { Joined = 0, Left = 1, Kicked = 2 }

    [Header("Room settings (the host changes them in the lobby)")]
    [Tooltip("Rounds per match at first, and the choices of the lobby's ROUNDS station.")]
    [SerializeField] private int[] roundChoices = { 3, 5, 8, 10, 15 };

    [SerializeField] private int defaultRounds = 8;

    [Tooltip("Seconds to pick a card at a milestone (then a random one is taken): the CARD TIME station's choices.")]
    [SerializeField] private int[] cardTimeChoices = { 15, 20, 30, 45 };

    [SerializeField] private int defaultCardTime = 30;

    [Tooltip("Before these rounds everybody picks a card.")]
    [SerializeField] private int[] cardRounds = { 1, 3, 5, 7 };

    [Tooltip("Each milestone rolls one rarity for everybody: chances of silver, gold and diamond.")]
    [SerializeField] private float[] rarityChances = { 40f, 45f, 15f };

    [Header("Match")]
    [Min(1)]
    [SerializeField] private int minPlayers = 2;

    [Tooltip("Everybody in the READY zone this long (seconds, shown as 3-2-1) starts the match.")]
    [SerializeField] private float countdownTime = 3f;

    [Tooltip("Seconds between the end of a round (one player left) and the travel to the next map.")]
    [SerializeField] private float roundEndDelay = 2f;

    [Tooltip("At a card milestone the round waits at most Card Time plus this for the picks (seconds).")]
    [SerializeField] private float pickWaitExtra = 2f;

    [SerializeField] private int maxTiebreaks = 3;

    [Tooltip("Longest chat message (letters).")]
    [SerializeField] private int maxChatLength = 80;

    public static MatchManager Instance { get; private set; }

    // Part of the scene (this object is spawned over the network, so it looks them up).
    private MapManager maps;
    private MatchUI ui;
    private MapRoulette roulette;
    private RoundEndShow roundShow;

    private readonly NetworkVariable<int> phase = new NetworkVariable<int>((int)Phase.Lobby);
    private readonly NetworkVariable<int> round = new NetworkVariable<int>(0);
    private readonly NetworkVariable<bool> tiebreak = new NetworkVariable<bool>(false);
    private readonly NetworkVariable<int> rounds = new NetworkVariable<int>(8);
    private readonly NetworkVariable<int> cardTime = new NetworkVariable<int>(30);
    private readonly NetworkVariable<int> mapMask = new NetworkVariable<int>(0); // bit i = map i can be played
    private readonly NetworkVariable<int> readyCount = new NetworkVariable<int>(0);
    private readonly NetworkVariable<int> countdown = new NetworkVariable<int>(0); // 3, 2, 1 (0 = not counting)
    private readonly NetworkVariable<int> picksLeft = new NetworkVariable<int>(0); // players still choosing a card
    private readonly NetworkVariable<Unity.Collections.FixedString64Bytes> roomName =
        new NetworkVariable<Unity.Collections.FixedString64Bytes>();
    private NetworkList<int> wins; // round wins by slot (0-3)

    // Host only.
    private readonly HashSet<PlayerNetwork> picking = new HashSet<PlayerNetwork>();
    private readonly HashSet<ulong> kicked = new HashSet<ulong>();
    private readonly List<int> mapBag = new List<int>();
    private int tiebreaks;
    private bool busy; // between the end of a round and the start of the next one
    private float countdownTimer;

    private float bannerTimer;

    public Phase CurrentPhase => (Phase)phase.Value;
    public bool InMatch => IsSpawned && CurrentPhase != Phase.Lobby;
    public bool RoundOn => IsSpawned && CurrentPhase == Phase.Round;
    public int Round => round.Value;
    public int Rounds => rounds.Value;
    public float CardTime => cardTime.Value;
    public int ReadyCount => readyCount.Value;
    public int Countdown => countdown.Value;
    public int PicksLeft => picksLeft.Value;
    public string RoomName => roomName.Value.ToString();
    public int Wins(int slot) => slot >= 0 && slot < wins.Count ? wins[slot] : 0;
    public bool MapEnabled(int index) => (mapMask.Value & (1 << index)) != 0;

    private void Awake()
    {
        Instance = this;
        wins = new NetworkList<int>();
        maps = FindAnyObjectByType<MapManager>();
        ui = FindAnyObjectByType<MatchUI>(FindObjectsInactive.Include);
        roulette = FindAnyObjectByType<MapRoulette>(FindObjectsInactive.Include);
        roundShow = FindAnyObjectByType<RoundEndShow>(FindObjectsInactive.Include);
        ui.LobbyButton.onClick.AddListener(BackToLobby);
    }

    public override void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        if (ui != null)
        {
            ui.LobbyButton.onClick.RemoveListener(BackToLobby);
            ui.BannerText.text = "";
            ui.CountdownText.text = "";
        }
        base.OnDestroy();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;
        if (wins.Count == 0)
            for (int i = 0; i < 4; i++)
                wins.Add(0);
        rounds.Value = defaultRounds;
        cardTime.Value = defaultCardTime;
        int mask = 0;
        for (int i = 0; i < maps.Count; i++)
            if (maps.IsRoundMap(i))
                mask |= 1 << i;
        mapMask.Value = mask;
    }

    private void Update()
    {
        if (bannerTimer > 0f)
        {
            bannerTimer -= Time.unscaledDeltaTime;
            if (bannerTimer <= 0f)
                ui.BannerText.text = "";
        }
        ui.CountdownText.text = CurrentPhase == Phase.Lobby && countdown.Value > 0 ? countdown.Value.ToString() : "";

        if (!IsServer)
            return;
        if (CurrentPhase == Phase.Lobby)
            UpdateReady();
        else if (RoundOn && !busy)
            CheckRoundOver();
    }

    // ---------- Host: lobby ----------

    // The host names the room (from the menu) right after spawning this.
    public void SetRoomOnHost(string name)
    {
        roomName.Value = name;
    }

    // Enough players, and every one has picked a skin and finished joining (else it could miss the start).
    public bool CanStart
    {
        get
        {
            if (!IsSpawned || CurrentPhase != Phase.Lobby || PlayerNetwork.All.Count < minPlayers ||
                PlayerNetwork.All.Count < NetworkManager.ConnectedClientsIds.Count)
                return false;
            foreach (PlayerNetwork player in PlayerNetwork.All)
                if (!player.Ready)
                    return false;
            return true;
        }
    }

    // Who stands in the READY zone; everybody there counts down, then the match starts.
    private void UpdateReady()
    {
        int ready = 0;
        foreach (PlayerNetwork player in PlayerNetwork.All)
            if (ReadyZone.Instance != null && ReadyZone.Instance.Contains(player.transform.position))
                ready++;
        readyCount.Value = ready;

        if (busy || !CanStart || ready < PlayerNetwork.All.Count)
        {
            countdownTimer = countdownTime;
            countdown.Value = 0;
            return;
        }

        countdownTimer -= Time.unscaledDeltaTime;
        countdown.Value = Mathf.Max(1, Mathf.CeilToInt(countdownTimer));
        if (countdownTimer <= 0f)
        {
            countdown.Value = 0;
            StartMatch();
        }
    }

    // Starts the match right away (the READY countdown, or a test bot).
    public void StartMatch()
    {
        if (!IsServer || !CanStart)
            return;

        for (int i = 0; i < wins.Count; i++)
            wins[i] = 0;
        foreach (PlayerNetwork player in PlayerNetwork.All)
            player.ClearCoresOnHost();
        round.Value = 0;
        tiebreak.Value = false;
        tiebreaks = 0;
        mapBag.Clear();
        StartCoroutine(NextRound());
    }

    // The lobby's ROUNDS and CARD TIME stations: the next choice (after the last one, the first again).
    public void CycleRounds()
    {
        if (IsServer && CurrentPhase == Phase.Lobby)
            rounds.Value = NextChoice(roundChoices, rounds.Value);
    }

    public void CycleCardTime()
    {
        if (IsServer && CurrentPhase == Phase.Lobby)
            cardTime.Value = NextChoice(cardTimeChoices, cardTime.Value);
    }

    private static int NextChoice(int[] choices, int current)
    {
        for (int i = 0; i < choices.Length; i++)
            if (choices[i] == current)
                return choices[(i + 1) % choices.Length];
        return choices[0];
    }

    // The map board: turns a map on or off for the rounds (at least one stays on).
    public void ToggleMap(int index)
    {
        if (!IsServer || CurrentPhase != Phase.Lobby || !maps.IsRoundMap(index))
            return;
        int mask = mapMask.Value ^ (1 << index);
        if (mask != 0)
            mapMask.Value = mask;
    }

    // ---------- Looks and names (LAN players spawn once they made their character) ----------

    // Any machine: asks the host to spawn this machine's player with its saved look and name.
    public void EnterWithLook() => EnterRpc(PlayerProfile.Look.Pack(), PlayerProfile.Name);

    [Rpc(SendTo.Server)]
    private void EnterRpc(int look, string name, RpcParams rpcParams = default)
    {
        ulong client = rpcParams.Receive.SenderClientId;
        if (NetworkManager.ConnectedClients[client].PlayerObject != null)
            return; // already playing

        name = PlayerProfile.Clean(name);
        if (name.Length == 0)
            name = "PLAYER";
        foreach (PlayerNetwork other in PlayerNetwork.All)
        {
            if (other.Name == name)
            {
                NameTakenRpc(RpcTarget.Single(client, RpcTargetUse.Temp)); // two players with one name: change it first
                return;
            }
        }
        GameObject player = Instantiate(NetworkManager.NetworkConfig.PlayerPrefab);
        player.GetComponent<PlayerNetwork>().SetLookOnHost(look, name);
        player.GetComponent<NetworkObject>().SpawnAsPlayerObject(client);
        NoticeRpc((int)Notice.Joined, name, Wardrobe.Instance.ColorAt(Look.Unpack(look).color).color);
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void NameTakenRpc(RpcParams rpcParams)
    {
        CharacterPanel panel = FindAnyObjectByType<CharacterPanel>();
        if (panel != null)
            panel.NameTaken();
    }

    // Host: a player's object went away (left, kicked or lost the connection).
    public void OnPlayerLeft(PlayerNetwork player)
    {
        if (!IsServer || !IsSpawned || NetworkManager.ShutdownInProgress)
            return;
        if (!kicked.Remove(player.OwnerClientId))
            NoticeRpc((int)Notice.Left, player.Name, player.Color);
    }

    // Host: sends that player back to the main menu ("you were kicked").
    public void Kick(PlayerNetwork player)
    {
        if (!IsServer || player == null || player.IsHostPlayer)
            return;
        kicked.Add(player.OwnerClientId);
        NoticeRpc((int)Notice.Kicked, player.Name, player.Color);
        NetworkManager.DisconnectClient(player.OwnerClientId, MainMenu.ReasonKicked);
    }

    // ---------- Host: rounds ----------

    // A player died during a round: the kill feed tells everybody. killer: whoever hurt it last (null: nobody).
    public void OnPlayerDiedInRound(PlayerNetwork player, PlayerNetwork killer, bool fellOff)
    {
        if (!IsServer)
            return;
        bool hasKiller = killer != null && killer != player;
        KillFeedRpc(hasKiller ? killer.Name : "", hasKiller ? killer.Color : Color.white, player.Name, player.Color, fellOff);
    }

    // Host: that player picked its card (or had nothing to pick).
    public void PickDone(PlayerNetwork player)
    {
        picking.Remove(player);
    }

    private void CheckRoundOver()
    {
        int alive = 0;
        PlayerNetwork last = null;
        foreach (PlayerNetwork player in PlayerNetwork.All)
        {
            if (!player.Dead)
            {
                alive++;
                last = player;
            }
        }

        if (PlayerNetwork.All.Count < 2)
            StartCoroutine(EndMatch()); // everybody else left
        else if (alive <= 1)
            StartCoroutine(EndRound(last));
    }

    private IEnumerator EndRound(PlayerNetwork winner)
    {
        busy = true;
        phase.Value = (int)Phase.BetweenRounds;
        if (winner != null)
            wins[winner.Slot] = wins[winner.Slot] + 1;
        RoundEndRpc(winner != null ? winner.Slot : -1, winner != null ? wins[winner.Slot] : 0);
        yield return new WaitForSecondsRealtime(roundEndDelay);

        if (MatchDecided())
            yield return EndMatch();
        else
            yield return NextRound();
    }

    // After the last round: one leader = decided. A shared lead plays a tiebreak round (a few at most).
    private bool MatchDecided()
    {
        if (round.Value < rounds.Value)
            return false;
        if (LeaderCount() == 1 || tiebreaks >= maxTiebreaks)
            return true;
        tiebreaks++;
        tiebreak.Value = true;
        return false;
    }

    private int LeaderCount()
    {
        int best = 0, count = 0;
        foreach (PlayerNetwork player in PlayerNetwork.All)
            best = Mathf.Max(best, Wins(player.Slot));
        foreach (PlayerNetwork player in PlayerNetwork.All)
            if (Wins(player.Slot) == best)
                count++;
        return count;
    }

    // A card milestone: everybody picks a card of that rarity, with
    // Card Time to choose; the next round waits for the picks (never forever).
    private IEnumerator PickCards(CoreInventory.Rarity rarity)
    {
        picking.Clear();
        foreach (PlayerNetwork player in PlayerNetwork.All)
        {
            if (player.Cores.OwnsEverything)
                continue;
            picking.Add(player);
            OfferCardsRpc((int)rarity, RpcTarget.Single(player.OwnerClientId, RpcTargetUse.Temp));
        }
        if (picking.Count == 0)
            yield break;

        string tier = rarity == CoreInventory.Rarity.Silver ? "SILVER" : rarity == CoreInventory.Rarity.Gold ? "GOLD" : "DIAMOND";
        string tierVi = rarity == CoreInventory.Rarity.Silver ? "BẠC" : rarity == CoreInventory.Rarity.Gold ? "VÀNG" : "KIM CƯƠNG";
        BannerRpc(tier + " CARDS!", "THẺ " + tierVi + "!", Color.white, 1.5f);

        // Nobody moves on until everybody has picked (a random card is taken when someone's time runs out; the
        // extra seconds only cover a player who lost the connection meanwhile).
        for (float t = 0f; picking.Count > 0 && t < cardTime.Value + pickWaitExtra; t += Time.unscaledDeltaTime)
        {
            picking.RemoveWhere(player => player == null || !player.IsSpawned);
            picksLeft.Value = picking.Count;
            yield return null;
        }
        picking.Clear();
        picksLeft.Value = 0;
    }

    // That player's machine: the card offer, with the room's card time.
    [Rpc(SendTo.SpecifiedInParams)]
    private void OfferCardsRpc(int rarity, RpcParams rpcParams)
    {
        PlayerNetwork local = PlayerNetwork.Local;
        if (local != null && !local.Cores.OfferCardsNow((CoreInventory.Rarity)rarity, cardTime.Value))
            local.ReportPickSkipped();
    }

    private IEnumerator NextRound()
    {
        busy = true;
        phase.Value = (int)Phase.BetweenRounds;
        round.Value++;
        foreach (PlayerNetwork player in PlayerNetwork.All)
        {
            player.Cores.ResetForRound();
            player.ClearGun(); // special guns don't carry over
        }

        // A card milestone: everybody picks a card before the round.
        if (System.Array.IndexOf(cardRounds, round.Value) >= 0 && !tiebreak.Value)
            yield return PickCards(CoreBridge.RandomRarity(rarityChances));

        // The map roulette shows which map is next, then everybody travels there (the dead come back too).
        int next = NextMap();
        if (roulette != null)
        {
            RouletteRpc(next, Random.Range(0, int.MaxValue));
            yield return new WaitForSecondsRealtime(roulette.Duration);
        }
        TravelRpc(next);
        yield return new WaitForSecondsRealtime(0.2f);
        float waited = 0f;
        while (waited < 10f && (AnyTraveling() || AnyDead()))
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (tiebreak.Value)
            BannerRpc("TIEBREAK!", "PHÂN ĐỊNH!", Color.white, 1.2f);
        else
            BannerRpc("ROUND " + round.Value, "VÒNG " + round.Value, Color.white, 1.2f);
        phase.Value = (int)Phase.Round;
        busy = false;
    }

    private IEnumerator EndMatch()
    {
        busy = true;
        phase.Value = (int)Phase.Results;
        int best = -1;
        PlayerNetwork winner = null;
        foreach (PlayerNetwork player in PlayerNetwork.All)
        {
            int playerWins = Wins(player.Slot);
            if (playerWins > best)
            {
                best = playerWins;
                winner = player;
            }
            else if (playerWins == best)
            {
                winner = null; // still shared after the tiebreaks
            }
        }
        ResultsRpc(winner != null ? winner.Name : "", winner != null ? winner.Color : Color.white);
        yield break;
    }

    // The host's button on the results: everybody back to the lobby without cores.
    private void BackToLobby()
    {
        if (!IsServer || CurrentPhase != Phase.Results)
            return;

        foreach (PlayerNetwork player in PlayerNetwork.All)
            player.ClearCoresOnHost();
        for (int i = 0; i < wins.Count; i++)
            wins[i] = 0;
        round.Value = 0;
        tiebreak.Value = false;
        HideResultsRpc();
        TravelRpc(maps.LobbyMap);
        phase.Value = (int)Phase.Lobby;
        busy = false;
    }

    // A shuffled bag of the maps turned on: every one once before any comes again, never the same twice in a row.
    private int NextMap()
    {
        if (mapBag.Count == 0)
        {
            for (int i = 0; i < maps.Count; i++)
                if (maps.IsRoundMap(i) && MapEnabled(i))
                    mapBag.Add(i); // PvP maps only, never the lobby
            for (int i = mapBag.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (mapBag[i], mapBag[j]) = (mapBag[j], mapBag[i]);
            }
            if (mapBag.Count > 1 && mapBag[0] == maps.CurrentIndex)
                (mapBag[0], mapBag[mapBag.Count - 1]) = (mapBag[mapBag.Count - 1], mapBag[0]);
        }
        int map = mapBag[0];
        mapBag.RemoveAt(0);
        return map;
    }

    private static bool AnyTraveling()
    {
        foreach (PlayerNetwork player in PlayerNetwork.All)
            if (player.Respawn.IsTraveling)
                return true;
        return false;
    }

    private static bool AnyDead()
    {
        foreach (PlayerNetwork player in PlayerNetwork.All)
            if (player.Dead)
                return true;
        return false;
    }

    // ---------- Chat ----------

    // Any machine: sends a chat line (the host adds who wrote it).
    public void SendChat(string text)
    {
        text = text.Replace("<", "").Replace(">", "").Trim();
        if (text.Length > maxChatLength)
            text = text.Substring(0, maxChatLength);
        if (text.Length > 0)
            SendChatRpc(text);
    }

    [Rpc(SendTo.Server)]
    private void SendChatRpc(string text, RpcParams rpcParams = default)
    {
        NetworkObject sender = NetworkManager.ConnectedClients[rpcParams.Receive.SenderClientId].PlayerObject;
        PlayerNetwork player = sender != null ? sender.GetComponent<PlayerNetwork>() : null;
        if (player == null)
            return; // not in the room yet (still picking a skin)
        if (text.Length > maxChatLength)
            text = text.Substring(0, maxChatLength);
        ChatRpc(player.Name, player.Color, text.Replace("<", "").Replace(">", ""));
    }

    [Rpc(SendTo.Everyone)]
    private void ChatRpc(string name, Color color, string text)
    {
        ui.Chat.AddLine(Colored(name, color) + ": " + text);
    }

    // The words are made on each machine, in its own language.
    [Rpc(SendTo.Everyone)]
    private void NoticeRpc(int notice, string name, Color color)
    {
        string who = Colored(name, color);
        string line = notice == (int)Notice.Joined ? who + Lang.T(" joined the room", " đã vào phòng")
                    : notice == (int)Notice.Left ? who + Lang.T(" left the room", " đã rời phòng")
                    : who + Lang.T(" was kicked", " đã bị đuổi khỏi phòng");
        ui.Chat.AddLine("<color=#9AA3C7>" + line + "</color>");
    }

    [Rpc(SendTo.Everyone)]
    private void KillFeedRpc(string killer, Color killerColor, string victim, Color victimColor, bool fellOff)
    {
        string v = Colored(victim, victimColor);
        string line;
        if (killer.Length > 0)
            line = Colored(killer, killerColor) + (fellOff ? Lang.T(" knocked ", " hất ") + v + Lang.T(" off", " xuống vực")
                                                           : Lang.T(" took down ", " hạ gục ") + v);
        else
            line = v + (fellOff ? Lang.T(" fell off", " rơi khỏi map") : Lang.T(" went down", " gục ngã"));
        ui.KillFeed.Add(line);
    }

    public static string Colored(string text, Color color) => "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + text + "</color>";

    // ---------- Every machine ----------

    // The roulette spins to that map; the other cards show every round map (the show; the map itself was picked from
    // the ones turned on).
    [Rpc(SendTo.Everyone)]
    private void RouletteRpc(int map, int seed)
    {
        var choices = new List<int>();
        for (int i = 0; i < maps.Count; i++)
            if (maps.IsRoundMap(i))
                choices.Add(i);
        roulette.Play(map, seed, choices);
    }

    // Every player travels to that map: the players stop, the screen fades to black, the map is swapped, and
    // everybody appears at its spawn point while the screen fades back in.
    [Rpc(SendTo.Everyone)]
    private void TravelRpc(int map)
    {
        StartCoroutine(Travel(map));
    }

    // After the map roulette there is no black fade: the map is put in place behind the roulette, and the camera flies
    // from the chosen card into it (MapRoulette.EnterMap).
    private IEnumerator Travel(int map)
    {
        foreach (PlayerNetwork player in PlayerNetwork.All)
            player.Respawn.BeginRoundTravel();
        while (roulette != null && roulette.Playing && !roulette.WaitingForMap)
            yield return null; // this machine's roulette may stop a moment after the host's
        bool fromRoulette = roulette != null && roulette.WaitingForMap;
        ScreenFade fade = fromRoulette ? null : ScreenFade.Instance;
        if (fade != null)
            yield return fade.FadeOut();

        maps.SetMap(map);
        if (IsServer && maps.TryGetComponent(out MapProps props))
            props.SpawnFor(map, maps.CurrentMap); // the new map's crates (the old map's go away)
        foreach (PlayerNetwork player in PlayerNetwork.All)
        {
            player.Cores.OnMapChanged();
            player.Respawn.ArriveForRound(maps.SlotSpawn(map, player.Slot));
        }
        if (fromRoulette)
            roulette.EnterMap();
        if (fade != null)
            yield return fade.FadeIn();
    }

    // The text in both languages (each machine shows its own), in that color.
    [Rpc(SendTo.Everyone)]
    private void BannerRpc(string english, string vietnamese, Color color, float seconds)
    {
        ui.BannerText.text = Lang.T(english, vietnamese);
        ui.BannerText.color = color;
        bannerTimer = seconds;
    }

    // The round's show: the winner (slot, -1 = a draw) closes it out with their new number of round wins.
    [Rpc(SendTo.Everyone)]
    private void RoundEndRpc(int winnerSlot, int winnerWins)
    {
        PlayerNetwork winner = PlayerNetwork.All.Find(player => player.Slot == winnerSlot);
        if (roundShow != null)
            roundShow.Play(winner, winnerWins, rounds.Value, roundEndDelay);
    }

    [Rpc(SendTo.Everyone)]
    private void ResultsRpc(string winnerName, Color winnerColor)
    {
        if (roundShow != null)
            roundShow.Hide();
        ui.BannerText.text = ""; // the results say it all
        bannerTimer = 0f;
        var ranking = new List<PlayerNetwork>(PlayerNetwork.All);
        ranking.Sort((a, b) => Wins(b.Slot).CompareTo(Wins(a.Slot)));
        ui.ShowResults(winnerName, winnerColor, ranking, NetworkManager.Singleton.IsServer);
    }

    [Rpc(SendTo.Everyone)]
    private void HideResultsRpc()
    {
        ui.HideResults();
    }
}
