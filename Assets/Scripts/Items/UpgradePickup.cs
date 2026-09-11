using UnityEngine;

// A floating upgrade item (spawned by ItemSpawner). Touching it gives the player its upgrade (PlayerUpgrades).
// It bobs up and down, blinks near the end of its life, and disappears if nobody takes it.
[RequireComponent(typeof(Collider2D))]
public class UpgradePickup : MonoBehaviour
{
    [SerializeField] private UpgradeType type;

    [Tooltip("Icon sprite (a child): it bobs, and blinks near the end.")]
    [SerializeField] private SpriteRenderer icon;

    [Tooltip("Seconds before the item disappears if nobody picks it up.")]
    [SerializeField] private float lifetime = 12f;

    [Tooltip("The item blinks during these last seconds of its life.")]
    [SerializeField] private float blinkTime = 3f;

    [SerializeField] private float blinkInterval = 0.1f;

    [Tooltip("How far the icon moves up and down (units).")]
    [SerializeField] private float bobHeight = 0.12f;

    [Tooltip("Up-and-down cycles per second.")]
    [SerializeField] private float bobSpeed = 1.5f;

    [Tooltip("Spawned where the item is picked up. Optional.")]
    [SerializeField] private ParticleSystem pickupEffect;

    private Vector3 iconRestPosition;
    private float age;

    private void Awake()
    {
        iconRestPosition = icon.transform.localPosition;
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        icon.transform.localPosition = iconRestPosition + Vector3.up * (Mathf.Sin(age * bobSpeed * 2f * Mathf.PI) * bobHeight);
        icon.enabled = age < lifetime - blinkTime || Mathf.Repeat(age, blinkInterval * 2f) < blinkInterval;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerUpgrades upgrades = other.GetComponentInParent<PlayerUpgrades>();
        if (upgrades == null)
            return; // only the player picks items up

        upgrades.Add(type);
        if (pickupEffect != null)
            Instantiate(pickupEffect, transform.position, Quaternion.identity);
        Destroy(gameObject);
    }
}
