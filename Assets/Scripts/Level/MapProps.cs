using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Host: the networked things that belong to the current map (crates). The MatchManager calls Spawn For on every
// map change: the last map's things go away and the new map's appear at its spots (LevelMap.CrateSpots).
// Sits next to the MapManager.
public class MapProps : MonoBehaviour
{
    [SerializeField] private Crate cratePrefab;

    private readonly List<NetworkObject> spawned = new List<NetworkObject>();

    public void SpawnFor(int mapIndex, LevelMap map)
    {
        Clear();
        foreach (Transform spot in map.CrateSpots)
        {
            Crate crate = Instantiate(cratePrefab, spot.position, Quaternion.identity);
            crate.SetMap(mapIndex);
            crate.NetworkObject.Spawn(true);
            spawned.Add(crate.NetworkObject);
        }
    }

    public void Clear()
    {
        foreach (NetworkObject thing in spawned)
            if (thing != null && thing.IsSpawned)
                thing.Despawn();
        spawned.Clear();
    }
}
