using UnityEngine;

// The only class the rest of the game talks to about cores (augments).
// The gun, the player's bullets, the player, the enemies and the wave manager call these methods at the right
// moments; the core system behind it (CoreInventory, BulletCores, SingleCores, CoreCards) decides what the owned
// cores do. Without a CoreBridge in the scene the game simply plays with a plain gun and no card offers.
[RequireComponent(typeof(CoreInventory))]
[RequireComponent(typeof(BulletCores))]
[RequireComponent(typeof(SingleCores))]
public class CoreBridge : MonoBehaviour
{
    [Tooltip("Card offer UI.")]
    [SerializeField] private CoreCards cards;

    [Tooltip("A card offer comes after every this many cleared waves (1 = after each wave).")]
    [Min(1)]
    [SerializeField] private int cardEveryWaves = 1;

    [Tooltip("Card offers before the first wave (0 = none). Handy for trying cores on the practice dummies.")]
    [Min(0)]
    [SerializeField] private int cardsAtStart = 0;

    private CoreInventory inventory;
    private BulletCores bulletCores;
    private SingleCores singleCores;

    private void Awake()
    {
        inventory = GetComponent<CoreInventory>();
        bulletCores = GetComponent<BulletCores>();
        singleCores = GetComponent<SingleCores>();
    }

    // ---------- Gun (PlayerWeapon) ----------

    // holdTime = seconds the fire button has been held without a break.
    public float FireRateMultiplier(float holdTime) => bulletCores.FireRateMultiplier() * singleCores.FireRateMultiplier(holdTime);

    // Multiplier for the gun's random spread.
    public float SpreadMultiplier => singleCores.SpreadMultiplier;

    // Fires one shot: the cores decide how many bullets, in which directions, and how strong.
    public void FireShot(Projectile prefab, Vector2 position, Quaternion aim, float speed, float damage, float lifetime, Rigidbody2D shooter)
    {
        float size;
        float damageMultiplier = singleCores.ShotDamage(out size);
        bulletCores.FireShot(prefab, position, aim, speed, damage * damageMultiplier, lifetime, shooter, size);
    }

    // ---------- Player's bullets (Projectile, only bullets fired through FireShot) ----------

    public void OnBulletFly(Projectile bullet) => bulletCores.OnFly(bullet);

    // Multiplier for the knockback of the bullet's hit.
    public float BulletKnockback => bulletCores.Knockback();

    // The bullet has damaged an enemy. True = it keeps flying.
    public bool OnBulletHitEnemy(Projectile bullet, Dummy enemy, Vector2 point, Vector2 normal) =>
        bulletCores.OnHitEnemy(bullet, enemy, point, normal);

    // The bullet hit a wall or the floor. True = it keeps flying.
    public bool OnBulletHitWall(Projectile bullet, Vector2 point, Vector2 normal) =>
        bulletCores.OnHitWall(bullet, point, normal);

    // The bullet is gone. direction = the way things may fly out of that spot.
    public void OnBulletEnd(Projectile bullet, Vector2 position, Vector2 direction) =>
        bulletCores.OnEnd(bullet, position, direction);

    // ---------- Player (PlayerMovement, PlayerRespawn) ----------

    public int ExtraAirJumps => singleCores.ExtraAirJumps;

    public bool DashUnlocked => singleCores.DashUnlocked;

    public void OnHardLanding(Vector2 feet) => singleCores.OnHardLanding(feet);

    // The player died. True = a core brings it back (once), so the run goes on.
    public bool TryUseExtraLife() => singleCores.TryUseExtraLife();

    // ---------- Enemies (Dummy) ----------

    // An enemy exploded (killed, or fell into the void: see Dummy.FellOffMap).
    public void OnEnemyDied(Dummy enemy, Vector2 position) => singleCores.OnEnemyDied(enemy, position);

    // An enemy was knocked into a wall (other = null) or into another enemy.
    public void OnEnemyCrash(Dummy enemy, Dummy other, Vector2 point) => singleCores.OnEnemyCrash(enemy, other, point);

    // ---------- Waves (WaveManager) ----------

    // Card offers before the first wave. False = none (onDone is not called then).
    public bool OfferCardsAtStart(System.Action onDone) => OfferCards(cardsAtStart, onDone);

    // Called when a wave is cleared. False = no offer this time (onDone is not called then).
    public bool OfferCardsAfterWave(int wave, System.Action onDone) =>
        wave % cardEveryWaves == 0 && OfferCards(1, onDone);

    // The map was swapped (things the cores left on the old one go away).
    public void OnMapChanged() => bulletCores.ClearMines();

    // Several offers in a row; onDone runs after the last pick.
    private bool OfferCards(int offers, System.Action onDone)
    {
        if (offers <= 0 || cards == null)
            return false;
        return cards.Show(() =>
        {
            if (!OfferCards(offers - 1, onDone))
                onDone();
        });
    }

    // ---------- HUD (GameHUD) ----------

    public int OwnedCount => inventory.Owned.Count;

    public Sprite OwnedIcon(int index)
    {
        CoreInventory.CoreInfo info = inventory.Info(inventory.Owned[index]);
        return info != null ? info.icon : null;
    }
}
