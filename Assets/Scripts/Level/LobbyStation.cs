using UnityEngine;
using UnityEngine.UI;

// A sign in the lobby map showing one room setting to everybody: Rounds (the match length), Card Time (seconds to
// pick a card), Core Picks (a pick every round or every other round), Maps (how many maps are on). The host changes
// them on the room board ([E] anywhere in the lobby, RoomBoard).
public class LobbyStation : MonoBehaviour
{
    public enum Kind { Rounds, CardTime, Maps, CorePicks }

    [SerializeField] private Kind kind;

    [Tooltip("World-space texts on the sign (the title has its own LocalizedText).")]
    [SerializeField] private Text valueText;

    [Tooltip("Under the sign (left empty now: the board opens from anywhere).")]
    [SerializeField] private Text promptText;

    private MapManager maps;

    private void Awake()
    {
        maps = FindAnyObjectByType<MapManager>();
    }

    private void Update()
    {
        MatchManager match = MatchManager.Instance;
        if (match == null)
            return;

        valueText.text = kind == Kind.Rounds ? match.Rounds.ToString()
                       : kind == Kind.CardTime ? match.CardTime + "s"
                       : kind == Kind.CorePicks ? match.CardEvery.ToString() // "CORES EVERY" 1 / 2 (rounds)
                       : MapCount(match, maps);

        promptText.text = "";
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
