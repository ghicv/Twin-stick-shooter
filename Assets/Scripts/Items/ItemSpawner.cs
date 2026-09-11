using System.Collections.Generic;
using UnityEngine;

// Upgrade items:
// - On the waiting screen (before the first wave) one appears every Practice Interval seconds on a random free
//   spawn point (at most Max Items at a time), so the items can be tried out.
// - After that, only killed enemies drop them (the WaveManager calls TryDrop), with Drop Chance.
// Starting a wave clears the map (the WaveManager calls ClearItems).
public class ItemSpawner : MonoBehaviour
{
    [Tooltip("One prefab per item type; each item is one of them at random.")]
    [SerializeField] private UpgradePickup[] itemPrefabs;

    [Tooltip("Where waiting-screen items appear.")]
    [SerializeField] private Transform[] spawnPoints;

    [Tooltip("Seconds between two items on the waiting screen (before the first wave).")]
    [SerializeField] private float practiceInterval = 10f;

    [Tooltip("At most this many waiting-screen items on the map at once.")]
    [SerializeField] private int maxItems = 1;

    [Tooltip("Chance that a killed enemy drops an item (0 = never, 1 = always).")]
    [Range(0f, 1f)]
    [SerializeField] private float dropChance = 0.4f;

    [Tooltip("Tells when the waiting screen is over (the first wave has started).")]
    [SerializeField] private WaveManager waveManager;

    [Tooltip("Played where an item appears. Optional.")]
    [SerializeField] private ParticleSystem spawnEffect;

    private readonly List<UpgradePickup> items = new List<UpgradePickup>();
    private float timer;

    private void Update()
    {
        items.RemoveAll(item => item == null); // picked up or expired

        if (waveManager != null && waveManager.Wave > 0)
            return; // past the waiting screen: items only come from enemies

        timer += Time.deltaTime;
        if (timer < practiceInterval)
            return;

        timer = 0f;
        if (items.Count < maxItems)
            SpawnAtFreePoint();
    }

    // An enemy was killed here: maybe drop an item.
    public void TryDrop(Vector2 position)
    {
        if (Random.value < dropChance)
            SpawnAt(position);
    }

    // Removes every item on the map and restarts the clock (the WaveManager does this when a wave starts).
    public void ClearItems()
    {
        foreach (UpgradePickup item in items)
            if (item != null)
                Destroy(item.gameObject);
        items.Clear();
        timer = 0f;
    }

    private void SpawnAtFreePoint()
    {
        // Free spawn points: no item on them yet.
        var freePoints = new List<Transform>();
        foreach (Transform point in spawnPoints)
        {
            bool taken = false;
            foreach (UpgradePickup item in items)
                if (Vector2.Distance(item.transform.position, point.position) < 0.5f)
                    taken = true;
            if (!taken)
                freePoints.Add(point);
        }
        if (freePoints.Count > 0)
            SpawnAt(freePoints[Random.Range(0, freePoints.Count)].position);
    }

    private void SpawnAt(Vector2 position)
    {
        UpgradePickup prefab = itemPrefabs[Random.Range(0, itemPrefabs.Length)];
        items.Add(Instantiate(prefab, position, Quaternion.identity));
        if (spawnEffect != null)
            Instantiate(spawnEffect, position, Quaternion.identity);
    }
}
