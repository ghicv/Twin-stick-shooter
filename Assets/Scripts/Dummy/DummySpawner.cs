using UnityEngine;

// Keeps one dummy at this spot: spawns it when the game starts, and when it's gone
// (exploded or fell off the map, i.e. disabled) replaces it with a fresh one after a short delay.
public class DummySpawner : MonoBehaviour
{
    [SerializeField] private Dummy dummyPrefab;

    [Tooltip("Seconds between a dummy disappearing and the new one appearing.")]
    [SerializeField] private float respawnDelay = 1.5f;

    private Dummy current;
    private float timer;

    private void Start()
    {
        Spawn();
    }

    private void Update()
    {
        if (current != null && current.gameObject.activeSelf)
            return; // still alive

        timer += Time.deltaTime;
        if (timer >= respawnDelay)
        {
            if (current != null)
                Destroy(current.gameObject); // clean up the disabled one
            Spawn();
        }
    }

    private void Spawn()
    {
        current = Instantiate(dummyPrefab, transform.position, Quaternion.identity);
        timer = 0f;
    }
}
