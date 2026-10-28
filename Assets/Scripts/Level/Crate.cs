using Unity.Netcode;
using UnityEngine;

// A crate the host spawns on a map (MapProps): real physics on the host (bullets knock it around, it tumbles and
// falls), copied to the other machines (NetworkTransform). Players can stand on it and hide behind it. It looks
// like its map's theme and is gone once it falls off the map.
[RequireComponent(typeof(Rigidbody2D))]
public class Crate : NetworkBehaviour
{
    private readonly NetworkVariable<int> mapIndex = new NetworkVariable<int>(-1);
    private MapManager maps;
    private Rigidbody2D body;

    // Host, before spawning: the map it belongs to (its look and fall limit).
    public void SetMap(int index) => mapIndex.Value = index;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        maps = FindAnyObjectByType<MapManager>();
    }

    public override void OnNetworkSpawn()
    {
        if (mapIndex.Value >= 0)
            GetComponentInChildren<SpriteRenderer>().sprite = maps.Map(mapIndex.Value).CrateSprite;
    }

    private void FixedUpdate()
    {
        if (IsServer && IsSpawned && body.position.y < maps.Map(mapIndex.Value).FallLimitY)
            NetworkObject.Despawn();
    }
}
