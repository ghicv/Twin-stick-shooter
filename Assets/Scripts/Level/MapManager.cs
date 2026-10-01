using UnityEngine;

// Holds every map of the scene and keeps exactly one of them on. Single player starts on Start Map (the waiting
// screen) and after each cleared wave the WaveManager asks for a random new wave map. LAN starts on the Lobby Map,
// and the MatchManager picks the map of every round (SetMap, a PvP map) on all machines.
public class MapManager : MonoBehaviour
{
    [Tooltip("Every map (each a LevelMap with its blocks and spawn points under it).")]
    [SerializeField] private LevelMap[] maps;

    [Tooltip("Index of the map single player starts on (the waiting screen with the practice dummies).")]
    [SerializeField] private int startMap = 0;

    [Tooltip("Index of the LAN lobby map (closed: nobody can fall out). Never used for waves or rounds. -1 = none.")]
    [SerializeField] private int lobbyMap = -1;

    public LevelMap CurrentMap { get; private set; }
    public int CurrentIndex { get; private set; }
    public int Count => maps.Length;
    public int StartMap => startMap;
    public int LobbyMap => lobbyMap >= 0 ? lobbyMap : startMap;

    private void Awake()
    {
        SetMap(startMap);
    }

    // Single player: any wave map but the current one (never the lobby or a LAN round map).
    public void ChangeToRandomMap()
    {
        var choices = new System.Collections.Generic.List<int>();
        for (int i = 0; i < maps.Length; i++)
            if (i != CurrentIndex && i != lobbyMap && !maps[i].Pvp)
                choices.Add(i);
        if (choices.Count > 0)
            SetMap(choices[Random.Range(0, choices.Count)]);
    }

    // A LAN round can be played on this map (a PvP map; or, if there are none, any map but the lobby).
    public bool IsRoundMap(int index)
    {
        if (index == lobbyMap)
            return false;
        foreach (LevelMap map in maps)
            if (map.Pvp)
                return maps[index].Pvp;
        return true;
    }

    public void SetMap(int index)
    {
        for (int i = 0; i < maps.Length; i++)
            maps[i].gameObject.SetActive(i == index);
        CurrentMap = maps[index];
        CurrentIndex = index;
    }

    // Where the player in a slot (0-3) starts on a map: the spawn points spread between the slots.
    public Vector2 SlotSpawn(int mapIndex, int slot)
    {
        Transform[] points = maps[mapIndex].SpawnPoints;
        return points[(slot * points.Length / 4) % points.Length].position;
    }
}
