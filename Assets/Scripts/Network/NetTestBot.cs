using UnityEngine;
using UnityEngine.UI;

// Test helper for LAN builds (command line -bot): drives this machine's player by itself (runs left and right,
// jumps, shoots at the nearest other player, picks the first card when one is offered) and logs every player's
// position and health once a second, so a session can be checked without anyone at the keyboard. In the lobby it
// walks into the READY zone, so the match starts the normal way (3-2-1).
// With -shots <folder> it also saves pictures (mid-round, round end, map roulette, dive, results), and with
// -cores A,B,... it takes those cores when the match starts (testing cores over the network).
// MainMenu adds it.
public class NetTestBot : MonoBehaviour
{
    private float logTimer;
    private int framesSinceLog;
    private float shootTimer;
    private string shotFolder;
    private float roundTimer;
    private MatchManager.Phase lastPhase;
    private bool rouletteSeen;
    private string[] startCores;
    private readonly System.Collections.Generic.List<(float time, string name)> shots = new System.Collections.Generic.List<(float, string)>();

    // Every test copy on this PC gets its own name (they share the saved one, and a room refuses a name twice).
    // Put -bot before -autohost / -autojoin on the command line, so the name is set before entering.
    private void Awake()
    {
        PlayerProfile.Name = "BOT" + System.Diagnostics.Process.GetCurrentProcess().Id % 1000;
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++)
        {
            if (args[i] == "-shots")
                shotFolder = args[i + 1];
            if (args[i] == "-cores") // cores taken when the match starts, e.g. -cores Thunder,BlackHole (testing them)
                startCores = args[i + 1].Split(',');
            if (args[i] == "-scale") // the RESOLUTION setting, e.g. -scale 0.5 (measuring speed)
            {
                PlayerPrefs.SetFloat("RenderScale", float.Parse(args[i + 1], System.Globalization.CultureInfo.InvariantCulture));
                RenderScaleSetting.Apply();
            }
        }
    }

    private void Update()
    {
        PlayerNetwork local = PlayerNetwork.Local;
        if (local == null)
        {
            // LAN: enter the room with the saved look.
            CharacterPanel character = FindAnyObjectByType<CharacterPanel>();
            if (character != null)
                character.Enter();
            return;
        }

        var movement = local.GetComponent<PlayerMovement>();
        if (movement.enabled)
            movement.enabled = false; // the bot moves the body itself

        var rb = local.GetComponent<Rigidbody2D>();
        MatchManager room = MatchManager.Instance;
        roundTimer = room != null && room.CurrentPhase == MatchManager.Phase.Round && !local.Dead ? roundTimer + Time.unscaledDeltaTime : 0f;
        bool lobby = room != null && !room.InMatch;
        if (startCores != null && room != null && room.InMatch)
        {
            foreach (string name in startCores)
                if (System.Enum.TryParse(name, out CoreType type))
                    local.PickCore(type);
            Debug.Log("[BOT] start cores " + string.Join(",", startCores));
            startCores = null;
        }
        if (rb.simulated && lobby && ReadyZone.Instance != null)
        {
            // Lobby: walk into the READY zone like a player would (everybody there starts the match).
            float toZone = ReadyZone.Instance.transform.position.x - local.transform.position.x;
            rb.linearVelocity = new Vector2(Mathf.Abs(toZone) > 0.5f ? Mathf.Sign(toZone) * 4f : 0f, rb.linearVelocity.y);
        }
        else if (rb.simulated && roundTimer > 15f && room != null && room.CurrentPhase == MatchManager.Phase.Round)
        {
            rb.position = new Vector2(rb.position.x, -30f); // stuck behind cover too long: fall off so the match goes on
            roundTimer = 0f;
        }
        else if (rb.simulated)
        {
            float vy = rb.linearVelocity.y;
            if (Mathf.Repeat(Time.time, 2f) < Time.deltaTime && Mathf.Abs(vy) < 0.1f)
                vy = 14f; // hop every 2 seconds
            rb.linearVelocity = new Vector2(Mathf.Sin(Time.time * 1.5f) * 3f, vy);
        }

        // Shoot at the nearest other player.
        PlayerNetwork aimAt = null;
        foreach (PlayerNetwork player in PlayerNetwork.All)
            if (player != local && player.InPlay && (aimAt == null || Vector2.Distance(player.transform.position, local.transform.position) <
                                                                      Vector2.Distance(aimAt.transform.position, local.transform.position)))
                aimAt = player;
        Transform pivot = local.transform.Find("GunPivot");
        if (aimAt != null)
        {
            Vector2 to = aimAt.transform.position - pivot.position;
            pivot.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg);
            shootTimer -= Time.deltaTime;
            bool matchOn = MatchManager.Instance != null && MatchManager.Instance.InMatch; // no shooting in the lobby
            if (shootTimer <= 0f && local.InPlay && matchOn)
            {
                shootTimer = 0.25f;
                Transform firePoint = pivot.Find("FirePoint");
                local.Cores.FireShot(local.GetComponent<PlayerWeapon>().ProjectilePrefab, firePoint.position, firePoint.rotation,
                                     30f, 10f, 2f, rb);
            }
        }

        // A card offer (after dying): take the first card.
        GameObject cards = GameObject.Find("HUD/CoreCards/Panel");
        if (cards != null && cards.activeInHierarchy)
        {
            Button first = cards.transform.Find("Card0").GetComponent<Button>();
            first.onClick.Invoke();
            Debug.Log("[BOT] picked a card, cores now " + local.Cores.OwnedCount);
        }

        // -shots <folder>: pictures of the round (twice), its end, the map roulette and the dive into the map, the
        // results (on every machine given a folder, so the host's and a client's pictures can be compared).
        MatchManager current = MatchManager.Instance;
        if (shotFolder != null && current != null && current.IsSpawned)
            TakeShots(current);

        framesSinceLog++;
        logTimer -= Time.unscaledDeltaTime;
        if (logTimer <= 0f)
        {
            logTimer = 1f;
            int fps = framesSinceLog;
            framesSinceLog = 0;
            MatchManager match = MatchManager.Instance;
            MapManager maps = FindAnyObjectByType<MapManager>();
            var line = new System.Text.StringBuilder("[BOT] t=" + Time.time.ToString("F1") + " fps=" + fps + " players=" + PlayerNetwork.All.Count +
                (match != null ? " phase=" + match.CurrentPhase + " round=" + match.Round : " no-match") +
                " map=" + (maps != null ? maps.CurrentMap.DisplayName : "?"));
            foreach (PlayerNetwork player in PlayerNetwork.All)
                line.Append(" | P" + (player.Slot + 1) + (player == local ? "(me)" : "") + " pos=" + player.transform.position.ToString("F1") +
                            " hp=" + player.Health.ToString("F0") + (player.Dead ? " DEAD" : "") + " cores=" + player.Cores.OwnedCount);
            Debug.Log(line.ToString());
        }
    }

    // Plans pictures when a show starts, takes them when their time comes (real time).
    private void TakeShots(MatchManager match)
    {
        float now = Time.unscaledTime;
        if (match.CurrentPhase != lastPhase)
        {
            if (match.CurrentPhase == MatchManager.Phase.BetweenRounds && match.Round > 0)
                shots.Add((now + 0.6f, "end_r" + match.Round));
            if (match.CurrentPhase == MatchManager.Phase.Round)
            {
                shots.Add((now + 3f, "mid_r" + match.Round));
                shots.Add((now + 6f, "round_r" + match.Round));
            }
            if (match.CurrentPhase == MatchManager.Phase.Results)
                shots.Add((now + 2.5f, "results"));
            lastPhase = match.CurrentPhase;
        }
        MapRoulette roulette = FindAnyObjectByType<MapRoulette>();
        bool spinning = roulette != null && roulette.Playing;
        if (spinning && !rouletteSeen)
        {
            shots.Add((now + 2f, "roulette_r" + (match.Round + 1)));
            shots.Add((now + roulette.Duration + 0.7f, "dive_r" + (match.Round + 1)));
        }
        rouletteSeen = spinning;

        for (int i = shots.Count - 1; i >= 0; i--)
            if (now >= shots[i].time)
            {
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(shotFolder, shots[i].name + ".png"));
                shots.RemoveAt(i);
            }
    }
}
