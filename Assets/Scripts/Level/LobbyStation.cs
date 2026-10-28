using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// A sign in the lobby map that sets the room up: walk to it and press E (host only; the others just see the value).
// Rounds: the match length. Card Time: seconds a dead player has to pick a card. Maps: opens the map board.
public class LobbyStation : MonoBehaviour
{
    public enum Kind { Rounds, CardTime, Maps }

    [SerializeField] private Kind kind;

    [Tooltip("The local player this close (units) can use it.")]
    [SerializeField] private float useDistance = 1.8f;

    [Tooltip("World-space texts on the sign (the title has its own LocalizedText).")]
    [SerializeField] private Text valueText;

    [SerializeField] private Text promptText;

    [Tooltip("Maps only: the board that opens.")]
    [SerializeField] private MapBoard mapBoard;

    [Tooltip("The sign brightens while the local player can use it.")]
    [SerializeField] private SpriteRenderer sign;

    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color nearColor = new Color(1f, 0.95f, 0.7f);

    private MapManager maps;

    private void Awake()
    {
        maps = FindAnyObjectByType<MapManager>();
    }

    private void Update()
    {
        MatchManager match = MatchManager.Instance;
        PlayerNetwork local = PlayerNetwork.Local;
        if (match == null)
            return;

        valueText.text = kind == Kind.Rounds ? match.Rounds.ToString()
                       : kind == Kind.CardTime ? match.CardTime + "s"
                       : MapCount(match, maps);

        bool near = local != null && local.InPlay &&
                    Vector2.Distance(local.transform.position, transform.position) <= useDistance;
        bool host = match.IsServer;
        sign.color = near ? nearColor : normalColor;
        promptText.text = !near ? ""
                        : !host ? Lang.T("HOST ONLY", "CHỈ CHỦ PHÒNG")
                        : kind == Kind.Maps ? Lang.T("[E] CHOOSE", "[E] CHỌN")
                        : Lang.T("[E] CHANGE", "[E] ĐỔI");

        if (near && host && !InputLock.Blocked && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (kind == Kind.Rounds)
                match.CycleRounds();
            else if (kind == Kind.CardTime)
                match.CycleCardTime();
            else
                mapBoard.Toggle();
        }
        if (kind == Kind.Maps && !near && mapBoard.IsOpen)
            mapBoard.Close(); // walked away
    }

    private static string MapCount(MatchManager match, MapManager maps)
    {
        int on = 0, all = 0;
        for (int i = 0; i < maps.Count; i++)
        {
            if (!maps.IsRoundMap(i))
                continue;
            all++;
            if (match.MapEnabled(i))
                on++;
        }
        return on + "/" + all;
    }
}
