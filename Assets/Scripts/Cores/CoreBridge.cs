using UnityEngine;

// The only class the rest of the game talks to about cores (augments). It sits on the player: every player has
// its own cores. The gun, the bullets, the player, the targets and the wave manager call these methods at the
// right moments; the core system behind it (CoreInventory, BulletCores, SingleCores, CoreCards) decides what the
// owned cores do. Network: shots, picks and landings go through PlayerNetwork so every machine agrees.
[RequireComponent(typeof(CoreInventory))]
[RequireComponent(typeof(BulletCores))]
[RequireComponent(typeof(SingleCores))]
public class CoreBridge : MonoBehaviour
{
    [Tooltip("Single player: the waves after which a card offer comes.")]
    [SerializeField] private int[] cardWaves = { 1, 3, 5, 8 };

    [Tooltip("Single player: after the last of those, an offer every this many waves.")]
    [Min(1)]
    [SerializeField] private int cardEveryWavesAfter = 3;

    [Tooltip("Each offer's rarity (RarityRoll): two silvers in a row make the next gold at least; the diamond chance " +
             "starts at the first value and grows by the second with every gold since the last diamond; otherwise silver " +
             "the third value's share of the time.")]
    [SerializeField] private Vector3 rarityLuck = new Vector3(0.08f, 0.25f, 0.5f);

    private RarityRoll rarityRoll; // one per run (the scene loads again for a new one)

    [Tooltip("Single player: card offers before the first wave (0 = none). Handy for trying cores on the dummies.")]
    [Min(0)]
    [SerializeField] private int cardsAtStart = 0;

    private CoreInventory inventory;
    private BulletCores bulletCores;
    private SingleCores singleCores;
    private PlayerNetwork net;
    private Rigidbody2D body;
    private Projectile projectilePrefab;
    private CoreCards cards;

    public CoreInventory Inventory => inventory;

    private void Awake()
    {
        inventory = GetComponent<CoreInventory>();
        bulletCores = GetComponent<BulletCores>();
        singleCores = GetComponent<SingleCores>();
        net = GetComponent<PlayerNetwork>();
        body = GetComponent<Rigidbody2D>();
    }

    // The card UI is part of the scene (the player is spawned over the network).
    private CoreCards Cards
    {
        get
        {
            if (cards == null)
                cards = FindAnyObjectByType<CoreCards>(FindObjectsInactive.Include);
            return cards;
        }
    }

    // ---------- Gun (PlayerWeapon, on the machine that controls the player) ----------

    // holdTime = seconds the fire button has been held without a break.
    public float FireRateMultiplier(float holdTime) => bulletCores.FireRateMultiplier() * singleCores.FireRateMultiplier(holdTime);

    // Multiplier for the gun's random spread.
    public float SpreadMultiplier => singleCores.SpreadMultiplier;

    // Fires one shot: the cores decide how many bullets, in which directions, and how strong. gun = what a special
    // gun's bullets do besides (rockets, grenades...).
    public void FireShot(Projectile prefab, Vector2 position, Quaternion aim, float speed, float damage, float lifetime, Rigidbody2D shooter,
                         PlayerWeapon.GunEffect gun = PlayerWeapon.GunEffect.None)
    {
        projectilePrefab = prefab;
        float size;
        float damageMultiplier = singleCores.ShotDamage(out size);
        net.Fire(position, aim.eulerAngles.z, speed, damage * damageMultiplier, lifetime, size, (int)gun);
    }

    // Spawns a shot's bullets on this machine (PlayerNetwork): the real ones on the host, look-alikes elsewhere.
    public void SpawnShot(Vector2 position, float angle, float speed, float damage, float lifetime, float size, int seed, bool lookAlike, int gun)
    {
        if (projectilePrefab == null)
            projectilePrefab = GetComponent<PlayerWeapon>().ProjectilePrefab;
        bulletCores.FireShot(projectilePrefab, position, Quaternion.Euler(0f, 0f, angle), speed, damage, lifetime, body, size, seed, lookAlike,
                             (PlayerWeapon.GunEffect)gun);
    }

    // ---------- This player's bullets (Projectile, only bullets fired through FireShot) ----------

    public void OnBulletFly(Projectile bullet) => bulletCores.OnFly(bullet);

    // Multiplier for the knockback of the bullet's hit.
    public float BulletKnockback => bulletCores.Knockback();

    // The bullet hit a target (a real one has already damaged it). True = it keeps flying.
    public bool OnBulletHitTarget(Projectile bullet, HitTarget target, Vector2 point, Vector2 normal) =>
        bulletCores.OnHitTarget(bullet, target, point, normal);

    // The bullet hit a wall or the floor. True = it keeps flying.
    public bool OnBulletHitWall(Projectile bullet, Vector2 point, Vector2 normal) =>
        bulletCores.OnHitWall(bullet, point, normal);

    // The bullet is gone. direction = the way things may fly out of that spot.
    public void OnBulletEnd(Projectile bullet, Vector2 position, Vector2 direction) =>
        bulletCores.OnEnd(bullet, position, direction);

    // ---------- Player (PlayerMovement, PlayerRespawn) ----------

    public int ExtraAirJumps => singleCores.ExtraAirJumps;

    public bool DashUpgraded => singleCores.DashUpgraded;

    // Host: knockback this player takes is multiplied by this (Anchor).
    public float KnockbackTaken => singleCores.KnockbackTaken;

    // Host: health packs heal this many times more and protect this long (Medic).
    public float HealthPackHeal => singleCores.HealthPackHeal;
    public float HealthPackProtection => singleCores.HealthPackProtection;

    // Bullet damage to barrels and the push on crates (Demolition).
    public float PropMultiplier => singleCores.PropMultiplier;

    // Ammo of the special guns from weapon crates (Gun Nut).
    public float SpecialAmmoMultiplier => singleCores.SpecialAmmoMultiplier;

    // Host: this player's bullets or cores dealt that much damage (Vampire).
    public void OnDamageDealt(float amount, Vector2 at) => singleCores.OnDamageDealt(amount, at);

    // The machine that controls the player landed hard; the shockwave counts on the host.
    public void OnHardLanding(Vector2 feet)
    {
        if (net.IsHostSide)
            singleCores.OnHardLanding(feet);
        else
            net.ReportHardLanding(feet);
    }

    public void OnHardLandingOnHost(Vector2 feet) => singleCores.OnHardLanding(feet);

    // Host: the player died. True = a core brings it back (once), so the run goes on.
    public bool TryUseExtraLife() => singleCores.TryUseExtraLife();

    // ---------- Targets (Dummy, PlayerHealth: on the host) ----------

    // A target this player hit last is gone (killed, or knocked into the void).
    public void OnEnemyDied(HitTarget target, Vector2 position, bool fellOff) => singleCores.OnEnemyDied(target, position, fellOff);

    // A target this player hit was knocked into a wall (other = null) or into another enemy.
    public void OnEnemyCrash(HitTarget target, HitTarget other, Vector2 point) => singleCores.OnEnemyCrash(target, other, point);

    // An effect the host showed (PlayerNetwork passes it on).
    public void PlayEffect(int effect, Vector2 position, float scale) => singleCores.PlayEffect(effect, position, scale);

    // ---------- Cards ----------

    // Picks a core (asks the host, which tells every machine).
    public void Pick(CoreType type) => net.PickCore(type);

    // LAN: a card offer of that rarity right now (a match milestone); after Time Limit seconds a random card is
    // taken. False = nothing to offer (this player already owns every core).
    public bool OfferCardsNow(CoreInventory.Rarity rarity, float timeLimit)
    {
        return Cards != null && Cards.Show(this, rarity, null, timeLimit);
    }

    public bool OwnsEverything => inventory.Owned.Count >= inventory.Catalog.Length;

    // Host, LAN: a new round starts (once-per-round cores like Extra Life are ready again).
    public void ResetForRound() => singleCores.ResetForRound();

    // Single player: card offers before the first wave. False = none (onDone is not called then).
    public bool OfferCardsAtStart(System.Action onDone) => OfferCards(cardsAtStart, CoreInventory.Rarity.Silver, onDone);

    // Single player: called when a wave is cleared. False = no offer this time (onDone is not called then).
    public bool OfferCardsAfterWave(int wave, System.Action onDone)
    {
        int last = cardWaves.Length > 0 ? cardWaves[cardWaves.Length - 1] : 0;
        bool milestone = System.Array.IndexOf(cardWaves, wave) >= 0 ||
                         (wave > last && (wave - last) % cardEveryWavesAfter == 0);
        if (!milestone)
            return false;
        rarityRoll ??= new RarityRoll(rarityLuck.x, rarityLuck.y, rarityLuck.z);
        return OfferCards(1, rarityRoll.Next(), onDone);
    }

    // The map was swapped (things the cores left on the old one go away).
    public void OnMapChanged() => bulletCores.ClearMines();

    // Several offers in a row; onDone runs after the last pick.
    private bool OfferCards(int offers, CoreInventory.Rarity rarity, System.Action onDone)
    {
        if (offers <= 0 || Cards == null)
            return false;
        return Cards.Show(this, rarity, () =>
        {
            if (!OfferCards(offers - 1, rarity, onDone))
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
