using UnityEngine;

// One map: the level blocks under it, plus the spots where enemies appear and where the player arrives
// on a map change. MapManager turns one map on at a time.
public class LevelMap : MonoBehaviour
{
    [Tooltip("Shown when the player arrives on this map.")]
    [SerializeField] private string displayName = "MAP";

    [Tooltip("Enemies appear here and the player arrives at one of them. Keep them a bit above the surfaces.")]
    [SerializeField] private Transform[] spawnPoints;

    public string DisplayName => displayName;
    public Transform[] SpawnPoints => spawnPoints;
}
