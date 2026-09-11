using UnityEngine;

public enum UpgradeType
{
    ExtraBullet, // +1 bullet per shot
    Homing,      // bullets steer toward an enemy ahead
    Explosive,   // bullets explode when they hit a wall or an enemy
    Bounce,      // bullets bounce off a wall
}

// Upgrades picked up from items (UpgradePickup). PlayerWeapon reads them for every shot.
// Each upgrade lasts Upgrade Duration seconds. Picking the same item again resets its timer and, for
// Extra Bullet / Bounce, adds one more (up to their max). Everything is lost when the player dies
// (PlayerRespawn calls Clear).
[RequireComponent(typeof(AudioSource))]
public class PlayerUpgrades : MonoBehaviour
{
    [Tooltip("Seconds an upgrade lasts after its item is picked up.")]
    [SerializeField] private float upgradeDuration = 12f;

    [Tooltip("Most extra bullets per shot (Extra Bullet stacks up to this).")]
    [SerializeField] private int maxExtraBullets = 2;

    [Tooltip("Most wall bounces per bullet (Bounce stacks up to this).")]
    [SerializeField] private int maxBounces = 1;

    [SerializeField] private AudioClip pickupSound;

    [Range(0f, 1f)]
    [SerializeField] private float pickupVolume = 0.6f;

    public float Duration => upgradeDuration;
    public int ExtraBullets { get; private set; }
    public int Bounces { get; private set; }
    public bool Homing => TimeLeft(UpgradeType.Homing) > 0f;
    public bool Explosive => TimeLeft(UpgradeType.Explosive) > 0f;

    // Seconds left per upgrade, indexed by UpgradeType (0 = not active).
    private readonly float[] timeLeft = new float[System.Enum.GetValues(typeof(UpgradeType)).Length];
    private AudioSource audioSource;

    public float TimeLeft(UpgradeType type) => timeLeft[(int)type];

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    public void Add(UpgradeType type)
    {
        timeLeft[(int)type] = upgradeDuration;
        if (type == UpgradeType.ExtraBullet)
            ExtraBullets = Mathf.Min(ExtraBullets + 1, maxExtraBullets);
        if (type == UpgradeType.Bounce)
            Bounces = Mathf.Min(Bounces + 1, maxBounces);

        if (pickupSound != null)
        {
            audioSource.pitch = 1f; // PlayerWeapon changes the pitch for every shot
            audioSource.PlayOneShot(pickupSound, pickupVolume);
        }
    }

    public void Clear()
    {
        System.Array.Clear(timeLeft, 0, timeLeft.Length);
        ExtraBullets = 0;
        Bounces = 0;
    }

    private void Update()
    {
        for (int i = 0; i < timeLeft.Length; i++)
        {
            if (timeLeft[i] <= 0f)
                continue;

            timeLeft[i] -= Time.deltaTime;
            if (timeLeft[i] <= 0f)
            {
                timeLeft[i] = 0f; // expired
                if ((UpgradeType)i == UpgradeType.ExtraBullet)
                    ExtraBullets = 0;
                if ((UpgradeType)i == UpgradeType.Bounce)
                    Bounces = 0;
            }
        }
    }
}
