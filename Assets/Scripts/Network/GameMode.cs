using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// Which way the game is being played, chosen in the main menu.
// Single: one player against the waves (no network: the NetworkManager is never started).
// Lan: up to 4 players on the local network (host or client).
public static class GameMode
{
    public enum Mode { None, Single, Lan }

    public static Mode Current { get; set; } = Mode.None;

    // Set before reloading the scene so the menu starts a new single-player run right away (Play Again).
    public static bool AutoStartSingle { get; set; }

    public static bool IsLan => Current == Mode.Lan;

    // Single player started by COLLECTION's TRY with this core (null = a normal run). It survives the scene reload:
    // Play Again tries it again, leaving goes back to the collection on it.
    public static CoreType? TryingCore { get; set; }

    // A new Play in the editor (no domain reload) starts from the menu as normal.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        TryingCore = null;
        AutoStartSingle = false;
    }

    // LAN: the host's address (this PC's own when hosting), shown in the lobby.
    public static string RoomAddress { get; set; } = "";

    // Ends the session (host or client) and loads the scene again. autoStartSingle = skip the menu, new single run.
    public static void Restart(bool autoStartSingle)
    {
        AutoStartSingle = autoStartSingle;
        Current = Mode.None;
        Time.timeScale = 1f; // a death may have left the game in slow motion

        var runner = new GameObject("Restart").AddComponent<Restarter>();
        Object.DontDestroyOnLoad(runner.gameObject);
        runner.StartCoroutine(runner.Run());
    }

    // The network must be fully shut down (its port free again) before the scene brings a fresh NetworkManager.
    private class Restarter : MonoBehaviour
    {
        public IEnumerator Run()
        {
            NetworkManager network = NetworkManager.Singleton;
            if (network != null && network.IsServer && network.IsListening)
            {
                // A host leaving tells everybody why they are sent back to the menu.
                foreach (ulong client in new System.Collections.Generic.List<ulong>(network.ConnectedClientsIds))
                    if (client != NetworkManager.ServerClientId)
                        network.DisconnectClient(client, MainMenu.ReasonClosed);
                yield return new WaitForSecondsRealtime(0.2f); // let the messages go out
            }
            if (network != null)
            {
                network.Shutdown();
                while (network != null && network.ShutdownInProgress)
                    yield return null;
                if (network != null)
                    Destroy(network.gameObject); // it survives scene loads
                yield return null;
            }
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            Destroy(gameObject);
        }
    }
}
