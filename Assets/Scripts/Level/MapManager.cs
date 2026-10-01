using UnityEngine;

// Holds every map of the scene and keeps exactly one of them on. The run starts on Start Map;
// after each cleared wave the WaveManager asks for a new one (random, never the same one twice in a row).
public class MapManager : MonoBehaviour
{
    [Tooltip("Every map (each a LevelMap with its blocks and spawn points under it).")]
    [SerializeField] private LevelMap[] maps;

    [Tooltip("Index of the map the run starts on (the waiting screen with the practice dummies).")]
    [SerializeField] private int startMap = 0;

    public LevelMap CurrentMap { get; private set; }

    private void Awake()
    {
        SetMap(startMap);
    }

    public void ChangeToRandomMap()
    {
        if (maps.Length < 2)
            return;

        int current = System.Array.IndexOf(maps, CurrentMap);
        int index = (current + 1 + Random.Range(0, maps.Length - 1)) % maps.Length; // any map but the current one
        SetMap(index);
    }

    private void SetMap(int index)
    {
        for (int i = 0; i < maps.Length; i++)
            maps[i].gameObject.SetActive(i == index);
        CurrentMap = maps[index];
    }
}
