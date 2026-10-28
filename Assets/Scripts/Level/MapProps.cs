using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Host: the networked things on the current map. The MatchManager calls Spawn For on every map change: the last
// map's things go away and the new map's appear at its spots (LevelMap): crates, explosive barrels, health packs.
// During a round a taken health pack comes back after Health Respawn Time, and weapon crates drop from the sky now
// and then. Sits next to the MapManager.
public class MapProps : MonoBehaviour
{
    [SerializeField] private Crate cratePrefab;
    [SerializeField] private ExplosiveBarrel barrelPrefab;
    [SerializeField] private HealthPack healthPackPrefab;
    [SerializeField] private WeaponCrate weaponCratePrefab;

    [Tooltip("Seconds before a taken health pack comes back on its spot.")]
    [SerializeField] private float healthRespawnTime = 12f;

    [Tooltip("Seconds between two weapon crate drops (random in this range).")]
    [SerializeField] private Vector2 weaponDropDelay = new Vector2(8f, 15f);

    [Tooltip("At most this many weapon crates at a time.")]
    [SerializeField] private int maxWeaponCrates = 2;

    [Tooltip("Share of the crates that hold a shield instead of a gun.")]
    [Range(0f, 1f)]
    [SerializeField] private float shieldChance = 0.25f;

    [Tooltip("Weapon crates drop from this height, anywhere this far left or right of the middle (units).")]
    [SerializeField] private float dropHeight = 10.5f;
    [SerializeField] private float dropHalfWidth = 13f;

    private readonly List<NetworkObject> spawned = new List<NetworkObject>();
    private readonly List<WeaponCrate> weaponCrates = new List<WeaponCrate>();
    private LevelMap map;
    private HealthPack[] healthPacks = new HealthPack[0];
    private float[] healthTimers = new float[0];
    private float dropTimer;

    public void SpawnFor(int mapIndex, LevelMap newMap)
    {
        Clear();
        map = newMap;
        foreach (Transform spot in map.CrateSpots)
        {
            Crate crate = Spawn(cratePrefab, spot.position);
            crate.SetMap(mapIndex);
            crate.NetworkObject.Spawn(true);
        }
        foreach (Transform spot in map.BarrelSpots)
            Spawn(barrelPrefab, spot.position).NetworkObject.Spawn(true);
        healthPacks = new HealthPack[map.HealthSpots.Length];
        healthTimers = new float[map.HealthSpots.Length];
        for (int i = 0; i < healthPacks.Length; i++)
            SpawnHealthPack(i);
        dropTimer = Random.Range(weaponDropDelay.x, weaponDropDelay.y);
    }

    public void Clear()
    {
        foreach (NetworkObject thing in spawned)
            if (thing != null && thing.IsSpawned)
                thing.Despawn();
        spawned.Clear();
        weaponCrates.Clear();
    }

    private T Spawn<T>(T prefab, Vector2 position) where T : NetworkBehaviour
    {
        T thing = Instantiate(prefab, position, Quaternion.identity);
        spawned.Add(thing.GetComponent<NetworkObject>());
        return thing;
    }

    private void SpawnHealthPack(int spot)
    {
        healthPacks[spot] = Spawn(healthPackPrefab, map.HealthSpots[spot].position);
        healthPacks[spot].NetworkObject.Spawn(true);
        healthTimers[spot] = 0f;
    }

    private void Update()
    {
        NetworkManager network = NetworkManager.Singleton;
        MatchManager match = MatchManager.Instance;
        if (network == null || !network.IsServer || map == null || match == null || match.CurrentPhase != MatchManager.Phase.Round)
            return;

        for (int i = 0; i < healthPacks.Length; i++)
        {
            if (healthPacks[i] != null)
                continue; // still there (a taken one is destroyed)
            healthTimers[i] += Time.deltaTime;
            if (healthTimers[i] >= healthRespawnTime)
                SpawnHealthPack(i);
        }

        weaponCrates.RemoveAll(crate => crate == null);
        dropTimer -= Time.deltaTime;
        if (dropTimer <= 0f)
        {
            dropTimer = Random.Range(weaponDropDelay.x, weaponDropDelay.y);
            if (weaponCrates.Count < maxWeaponCrates)
            {
                WeaponCrate crate = Spawn(weaponCratePrefab, new Vector2(Random.Range(-dropHalfWidth, dropHalfWidth), dropHeight));
                crate.NetworkObject.Spawn(true);
                int guns = PlayerNetwork.All.Count > 0 ? PlayerNetwork.All[0].GetComponent<PlayerWeapon>().SpecialGunCount : 1;
                crate.Content = Random.value < shieldChance ? WeaponCrate.Shield : Random.Range(1, guns + 1);
                weaponCrates.Add(crate);
            }
        }
    }
}
