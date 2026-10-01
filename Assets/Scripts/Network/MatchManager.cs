using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

// LAN match (players against each other). The host runs it; every machine shows it.
// Lobby: players run around the start map; the host presses START (at least Min Players).
// Rounds: every round is on another map. Everybody arrives with full health (the travel sequence also brings the
// dead back). A player who dies stays out until the next round and picks a core right away (Card Time seconds,
// then a random card); the last one standing wins the round. The next round waits until everyone has picked.
// After Rounds rounds the most round wins takes the match; a tie plays tiebreak rounds (at most Max Tiebreaks).
// Results: the ranking; the host goes back to the lobby, where everybody starts again without cores.
// The host spawns it (a network prefab) when it starts a LAN game; its on-screen parts are the scene's MatchUI.
public class MatchManager : NetworkBehaviour
{
    public enum Phase { Lobby = 0, Round = 1, BetweenRounds = 2, Results = 3 }

    [Min(1)]
    [SerializeField] private int rounds = 8;

    [Min(1)]
    [SerializeField] private int minPlayers = 2;

    [Tooltip("Seconds a dead player has to pick a card; then a random one is taken.")]
    [SerializeField] private float cardTime = 10f;

    [Tooltip("Seconds between the end of a round (one player left) and the travel to the next map.")]
    [SerializeField] private float roundEndDelay = 2f;

    [Tooltip("The next round never waits longer than this for card picks (seconds).")]
    [SerializeField] private float pickWaitLimit = 12f;

    [SerializeField] private int maxTiebreaks = 3;

    public static MatchManager Instance { get; private set; }

    // Part of the scene (this object is spawned over the network, so it looks them up).
    private MapManager maps;
    private MatchUI ui;
    private Text bannerText;
    private Text roundText;
    private GameObject resultsPanel;
    private Text resultsTitle;
    private Text resultsList;
    private Button lobbyButton;
    private Text resultsWaitText;

    private readonly NetworkVariable<int> phase = new NetworkVariable<int>((int)Phase.Lobby);
    private readonly NetworkVariable<int> round = new NetworkVariable<int>(0);
    private readonly NetworkVariable<bool> tiebreak = new NetworkVariable<bool>(false);
    private NetworkList<int> wins; // round wins by slot (0-3)

    // Host only.
    private readonly HashSet<PlayerNetwork> picking = new HashSet<PlayerNetwork>();
    private readonly List<int> mapBag = new List<int>();
    private int tiebreaks;
    private bool busy; // between the end of a round and the start of the next one
    private float bannerTimer;

    public Phase CurrentPhase => (Phase)phase.Value;
    public bool InMatch => IsSpawned && CurrentPhase != Phase.Lobby;
    public bool RoundOn => IsSpawned && CurrentPhase == Phase.Round;
    public int Round => round.Value;
    public int Rounds => rounds;
    public float CardTime => cardTime;
    public int Wins(int slot) => slot >= 0 && slot < wins.Count ? wins[slot] : 0;

    private void Awake()
    {
        Instance = this;
        wins = new NetworkList<int>();
        maps = FindAnyObjectByType<MapManager>();
        ui = FindAnyObjectByType<MatchUI>(FindObjectsInactive.Include);
        bannerText = ui.BannerText;
        roundText = ui.RoundText;
        resultsPanel = ui.ResultsPanel;
        resultsTitle = ui.ResultsTitle;
        resultsList = ui.ResultsList;
        lobbyButton = ui.LobbyButton;
        resultsWaitText = ui.ResultsWaitText;
        lobbyButton.onClick.AddListener(BackToLobby);
    }

    public override void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        if (lobbyButton != null)
            lobbyButton.onClick.RemoveListener(BackToLobby);
        if (bannerText != null)
            bannerText.text = "";
        if (roundText != null)
            roundText.text = "";
        base.OnDestroy();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer && wins.Count == 0)
            for (int i = 0; i < 4; i++)
                wins.Add(0);
    }

    private void Update()
    {
        if (bannerTimer > 0f)
        {
            bannerTimer -= Time.unscaledDeltaTime;
            if (bannerTimer <= 0f)
                bannerText.text = "";
        }

        bool showRound = GameMode.IsLan && InMatch && CurrentPhase != Phase.Results && round.Value > 0;
        roundText.text = !showRound ? ""
            : tiebreak.Value ? Lang.T("TIEBREAK ROUND", "VÒNG PHÂN ĐỊNH")
            : Lang.T("ROUND ", "VÒNG ") + round.Value + " / " + rounds;

        if (IsServer && RoundOn && !busy)
            CheckRoundOver();
    }

    // ---------- Host ----------

    // Enough players, and every one of them has finished joining (else it could miss the start).
    public bool CanStart
    {
        get
        {
            if (!IsSpawned || CurrentPhase != Phase.Lobby || PlayerNetwork.All.Count < minPlayers)
                return false;
            foreach (PlayerNetwork player in PlayerNetwork.All)
                if (!player.Ready)
                    return false;
            return true;
        }
    }

    // The host's START button.
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

    // ---------- Skins (LAN players spawn once they picked one) ----------

    // Any machine: asks the host to spawn this machine's player with that skin.
    public void ChooseSkin(int skin) => ChooseSkinRpc(skin);

    [Rpc(SendTo.Server)]
    private void ChooseSkinRpc(int skin, RpcParams rpcParams = default)
    {
        ulong client = rpcParams.Receive.SenderClientId;
        if (NetworkManager.ConnectedClients[client].PlayerObject != null)
            return; // already playing
        if (PlayerNetwork.SkinTaken(skin))
        {
            SkinRefusedRpc(RpcTarget.Single(client, RpcTargetUse.Temp));
            return;
        }

        GameObject player = Instantiate(NetworkManager.NetworkConfig.PlayerPrefab);
        player.GetComponent<PlayerNetwork>().SetSkinOnHost(skin);
        player.GetComponent<NetworkObject>().SpawnAsPlayerObject(client);
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void SkinRefusedRpc(RpcParams rpcParams)
    {
        SkinPanel panel = FindAnyObjectByType<SkinPanel>();
        if (panel != null)
            panel.Refused();
    }

    // Host: a player died during a round. It stays out and picks a card (unless it owns every core).
    public void OnPlayerDiedInRound(PlayerNetwork player)
    {
        if (IsServer && !player.Cores.OwnsEverything)
            picking.Add(player);
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
        {
            wins[winner.Slot] = wins[winner.Slot] + 1;
            string player = "P" + (winner.Slot + 1);
            BannerRpc(player + " WINS THE ROUND!", player + " THẮNG VÒNG NÀY!", winner.Slot, roundEndDelay + 1f);
        }
        else
        {
            BannerRpc("DRAW!", "HÒA!", -1, roundEndDelay + 1f);
        }
        yield return new WaitForSecondsRealtime(roundEndDelay);

        // Everyone who died picks a card first (they have Card Time; never wait forever).
        for (float t = 0f; picking.Count > 0 && t < pickWaitLimit; t += Time.unscaledDeltaTime)
        {
            picking.RemoveWhere(player => player == null || !player.IsSpawned);
            yield return null;
        }
        picking.Clear();

        if (MatchDecided())
            yield return EndMatch();
        else
            yield return NextRound();
    }

    // After the last round: one leader = decided. A shared lead plays a tiebreak round (a few at most).
    private bool MatchDecided()
    {
        if (round.Value < rounds)
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

    private IEnumerator NextRound()
    {
        busy = true;
        phase.Value = (int)Phase.BetweenRounds;
        round.Value++;
        foreach (PlayerNetwork player in PlayerNetwork.All)
            player.Cores.ResetForRound();

        // Everybody travels to the next map (the dead come back too).
        TravelRpc(NextMap());
        yield return new WaitForSecondsRealtime(0.2f);
        float waited = 0f;
        while (waited < 10f && (AnyTraveling() || AnyDead()))
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (tiebreak.Value)
            BannerRpc("TIEBREAK!", "PHÂN ĐỊNH!", -1, 1.2f);
        else
            BannerRpc("ROUND " + round.Value, "VÒNG " + round.Value, -1, 1.2f);
        phase.Value = (int)Phase.Round;
        busy = false;
    }

    private IEnumerator EndMatch()
    {
        busy = true;
        phase.Value = (int)Phase.Results;
        int best = -1, winner = -1;
        foreach (PlayerNetwork player in PlayerNetwork.All)
        {
            int playerWins = Wins(player.Slot);
            if (playerWins > best)
            {
                best = playerWins;
                winner = player.Slot;
            }
            else if (playerWins == best)
            {
                winner = -1; // still shared after the tiebreaks
            }
        }
        ResultsRpc(winner);
        yield break;
    }

    // The host's button on the results: everybody back to the start map without cores.
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

    // A shuffled bag of maps: every map once before any comes again, never the same twice in a row.
    private int NextMap()
    {
        if (mapBag.Count == 0)
        {
            for (int i = 0; i < maps.Count; i++)
                if (maps.IsRoundMap(i))
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

    // ---------- Every machine ----------

    // Every player travels to that map: the players stop, the screen fades to black, the map is swapped, and
    // everybody appears at its spawn point while the screen fades back in.
    [Rpc(SendTo.Everyone)]
    private void TravelRpc(int map)
    {
        StartCoroutine(Travel(map));
    }

    private IEnumerator Travel(int map)
    {
        foreach (PlayerNetwork player in PlayerNetwork.All)
            player.Respawn.BeginRoundTravel();
        ScreenFade fade = ScreenFade.Instance;
        if (fade != null)
            yield return fade.FadeOut();

        maps.SetMap(map);
        foreach (PlayerNetwork player in PlayerNetwork.All)
        {
            player.Cores.OnMapChanged();
            player.Respawn.ArriveForRound(maps.SlotSpawn(map, player.Slot));
        }
        if (fade != null)
            yield return fade.FadeIn();
    }

    // The text in both languages (each machine shows its own). slot >= 0: the text takes that player's color.
    [Rpc(SendTo.Everyone)]
    private void BannerRpc(string english, string vietnamese, int slot, float seconds)
    {
        bannerText.text = Lang.T(english, vietnamese);
        bannerText.color = slot >= 0 ? PlayerNetwork.SlotColor(slot) : Color.white;
        bannerTimer = seconds;
    }

    [Rpc(SendTo.Everyone)]
    private void ResultsRpc(int winnerSlot)
    {
        bannerText.text = ""; // the results say it all
        bannerTimer = 0f;
        resultsTitle.text = winnerSlot >= 0 ? "P" + (winnerSlot + 1) + Lang.T(" WINS THE MATCH!", " THẮNG CẢ TRẬN!")
                                            : Lang.T("IT'S A DRAW!", "HÒA CẢ TRẬN!");
        resultsTitle.color = winnerSlot >= 0 ? PlayerNetwork.SlotColor(winnerSlot) : Color.white;

        // Ranking: most round wins first.
        var players = new List<PlayerNetwork>(PlayerNetwork.All);
        players.Sort((a, b) => Wins(b.Slot).CompareTo(Wins(a.Slot)));
        var lines = new System.Text.StringBuilder();
        for (int i = 0; i < players.Count; i++)
        {
            PlayerNetwork player = players[i];
            string you = player == PlayerNetwork.Local ? Lang.T("  (YOU)", "  (BẠN)") : "";
            lines.AppendLine((i + 1) + ".  <color=#" + ColorUtility.ToHtmlStringRGB(player.Color) + ">P" + (player.Slot + 1) + "</color>   " +
                             Wins(player.Slot) + Lang.T(" WINS   ", " THẮNG   ") + player.Cores.OwnedCount + Lang.T(" CORES", " CORE") + you);
        }
        resultsList.text = lines.ToString();

        bool host = NetworkManager.Singleton.IsServer;
        lobbyButton.gameObject.SetActive(host);
        resultsWaitText.text = host ? "" : Lang.T("WAITING FOR THE HOST...", "ĐANG CHỜ CHỦ PHÒNG...");
        resultsPanel.SetActive(true);
    }

    [Rpc(SendTo.Everyone)]
    private void HideResultsRpc()
    {
        resultsPanel.SetActive(false);
    }
}
