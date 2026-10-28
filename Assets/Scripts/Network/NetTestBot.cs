using UnityEngine;
using UnityEngine.UI;

// Test helper for LAN builds (command line -bot): drives this machine's player by itself (runs left and right,
// jumps, shoots at the nearest other player, picks the first card when one is offered) and logs every player's
// position and health once a second, so a session can be checked without anyone at the keyboard. In the lobby it
// walks into the READY zone, so the match starts the normal way (3-2-1).
// With -shots <folder> the host also saves a picture of every round map. MainMenu adds it.
public class NetTestBot : MonoBehaviour
{
    private float logTimer;
    private int framesSinceLog;
    private float shootTimer;
    private string shotFolder;
    private float roundTime;
    private readonly System.Collections.Generic.HashSet<int> shotMaps = new System.Collections.Generic.HashSet<int>();

    // Every test copy on this PC gets its own name (they share the saved one, and a room refuses a name twice).
    // Put -bot before -autohost / -autojoin on the command line, so the name is set before entering.
    private void Awake()
    {
        PlayerProfile.Name = "BOT" + System.Diagnostics.Process.GetCurrentProcess().Id % 1000;
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++)
            if (args[i] == "-shots")
                shotFolder = args[i + 1];
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
        bool lobby = room != null && !room.InMatch;
        if (rb.simulated && lobby && ReadyZone.Instance != null)
        {
            // Lobby: walk into the READY zone like a player would (everybody there starts the match).
            float toZone = ReadyZone.Instance.transform.position.x - local.transform.position.x;
            rb.linearVelocity = new Vector2(Mathf.Abs(toZone) > 0.5f ? Mathf.Sign(toZone) * 4f : 0f, rb.linearVelocity.y);
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

        // -shots <folder>: the host saves one picture of every round map, a few seconds into the round.
        MatchManager current = MatchManager.Instance;
        MapManager currentMaps = FindAnyObjectByType<MapManager>();
        if (shotFolder != null && current != null && current.IsServer && currentMaps != null &&
            current.CurrentPhase == MatchManager.Phase.Round && !shotMaps.Contains(currentMaps.CurrentIndex))
        {
            roundTime += Time.deltaTime;
            if (roundTime > 2.5f)
            {
                shotMaps.Add(currentMaps.CurrentIndex);
                roundTime = 0f;
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(shotFolder, currentMaps.CurrentMap.DisplayName + ".png"));
            }
        }

        framesSinceLog++;
        logTimer -= Time.deltaTime;
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
}
