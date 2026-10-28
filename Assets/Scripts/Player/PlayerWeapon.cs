using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Basic gun: hold Left Mouse to shoot projectiles from the fire point, limited by Fire Rate. Unlimited ammo.
// Clicks on UI buttons don't shoot.
// Only the machine that controls this player reads the mouse; the shot feedback plays on every machine.
// Cores (augments): with a CoreBridge on the player, each shot is handed to it (it decides how many bullets and
// what they do, and sends the shot over the network) and it can change the fire rate and spread.
// Without one, the gun fires one plain bullet per shot.
// All projectile stats live here and are handed to each projectile when it is fired.
//
// Recoil: bullets leave at a random angle inside the current spread. Every shot opens the spread a bit
// (up to Max Spread) and it closes again over time, so tapping is accurate and spraying is not.
// Each shot also plays a sound, kicks the gun sprite, flashes the muzzle and shakes the camera a little.
//
// Special guns (LAN, from weapon crates): while one is held (PlayerNetwork.Gun) its own fire rate, damage, bullets
// and spread are used, with limited ammo (kept by the owner); when it runs out, the normal gun is back. Cores still
// work on its shots.
// With the Draw the Sword core (PlayerSword) Left Mouse slashes instead, unless a special gun is held.
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerWeapon : MonoBehaviour
{
    [System.Serializable]
    public class SpecialGun
    {
        public string name = "GUN";

        [Tooltip("Shots per second.")]
        [Min(0.1f)]
        public float fireRate = 2f;

        public float damage = 10f;
        public float speed = 30f;

        [Tooltip("Seconds a bullet flies (short = short range).")]
        public float lifetime = 2f;

        [Tooltip("Bullets per shot, fanned out over Spread.")]
        [Min(1)]
        public int pellets = 1;

        [Tooltip("Degrees to each side (one bullet: a random angle inside it).")]
        public float spread;

        [Tooltip("Shots before it is used up.")]
        [Min(1)]
        public int ammo = 10;

        [Tooltip("How the gun sprite looks while it is held.")]
        public Vector2 gunScale = Vector2.one;
        public Color gunColor = Color.white;
    }

    [Header("References")]
    [SerializeField] private Projectile projectilePrefab;

    [Tooltip("Muzzle. Projectiles spawn here and fly along its right (red) axis.")]
    [SerializeField] private Transform firePoint;

    [Tooltip("Gun sprite that kicks on each shot.")]
    [SerializeField] private Transform gunVisual;

    [Tooltip("Shown at the muzzle for a moment on each shot. Keep it disabled in the prefab.")]
    [SerializeField] private GameObject muzzleFlash;

    [Header("Gun")]
    [Tooltip("Max shots per second while Left Mouse is held.")]
    [Min(0.1f)]
    [SerializeField] private float fireRate = 3f;

    [Header("Recoil (spread)")]
    [Tooltip("Spread of an accurate shot: max random angle (degrees) each side of the aim.")]
    [SerializeField] private float minSpread = 1.5f;

    [Tooltip("Spread never grows beyond this (degrees).")]
    [SerializeField] private float maxSpread = 12f;

    [Tooltip("Spread added by every shot (degrees).")]
    [SerializeField] private float spreadPerShot = 3f;

    [Tooltip("How fast spread shrinks back to Min Spread (degrees/sec).")]
    [SerializeField] private float spreadRecovery = 10f;

    [Header("Projectile")]
    [Tooltip("Units/sec.")]
    [SerializeField] private float projectileSpeed = 30f;

    [SerializeField] private float projectileDamage = 10f;

    [Tooltip("Seconds before the projectile is destroyed.")]
    [SerializeField] private float projectileLifetime = 2f;

    [Header("Feedback")]
    [Tooltip("How far the gun sprite kicks back on a shot (units).")]
    [SerializeField] private float gunKickDistance = 0.12f;

    [Tooltip("How quickly the gun sprite settles back after a kick (higher = snappier).")]
    [SerializeField] private float gunKickRecovery = 15f;

    [Tooltip("Seconds the muzzle flash stays visible.")]
    [SerializeField] private float muzzleFlashDuration = 0.05f;

    [Tooltip("Camera shake per shot (units). Needs a CameraShake on the main camera.")]
    [SerializeField] private float cameraShakeStrength = 0.06f;

    [SerializeField] private float cameraShakeDuration = 0.08f;

    [Header("Sound")]
    [SerializeField] private AudioClip shootSound;

    [Range(0f, 1f)]
    [SerializeField] private float shootVolume = 0.35f;

    [Tooltip("Other players' shots play at this part of Shoot Volume.")]
    [Range(0f, 1f)]
    [SerializeField] private float otherShotsVolume = 0.5f;

    [Tooltip("This player's bullet hit another player: a short tick, so a hit is felt.")]
    [SerializeField] private AudioClip hitConfirmSound;

    [Range(0f, 1f)]
    [SerializeField] private float hitConfirmVolume = 0.5f;

    [Header("Hit feedback (PvP, on the shooter's screen)")]
    [Tooltip("Pops up where another player was hit, with the damage.")]
    [SerializeField] private DamageNumber damageNumberPrefab;

    [Tooltip("The last hit on a player: its number in this color, a short freeze, a jolt and this sound.")]
    [SerializeField] private Color knockoutColor = new Color(1f, 0.3f, 0.25f);
    [SerializeField] private float knockoutFreeze = 0.07f;
    [SerializeField] private AudioClip knockoutSound;
    [Range(0f, 1f)]
    [SerializeField] private float knockoutVolume = 0.6f;

    [Tooltip("A hit doing at least this much damage shakes the screen a little.")]
    [SerializeField] private float bigHitDamage = 25f;

    [Header("Shells")]
    [Tooltip("Spent shells fly out of the gun on every shot (ShellCasings). Their material and color.")]
    [SerializeField] private Material shellMaterial;
    [SerializeField] private Color shellColor = new Color(0.9f, 0.7f, 0.25f);

    [Tooltip("Random pitch change per shot, so rapid fire doesn't sound identical.")]
    [Range(0f, 0.3f)]
    [SerializeField] private float shootPitchVariation = 0.08f;

    [Header("Special guns")]
    [SerializeField] private SpecialGun[] specialGuns =
    {
        new SpecialGun { name = "SHOTGUN", fireRate = 1.4f, damage = 8f, speed = 26f, lifetime = 0.35f, pellets = 6, spread = 14f,
                         ammo = 6, gunScale = new Vector2(1.25f, 1.4f), gunColor = new Color(0.75f, 0.5f, 0.25f) },
        new SpecialGun { name = "SNIPER", fireRate = 0.9f, damage = 40f, speed = 60f, lifetime = 2f, pellets = 1, spread = 0f,
                         ammo = 4, gunScale = new Vector2(1.7f, 1f), gunColor = new Color(0.4f, 0.65f, 0.4f) },
        new SpecialGun { name = "MINIGUN", fireRate = 14f, damage = 5f, speed = 32f, lifetime = 1.2f, pellets = 1, spread = 6f,
                         ammo = 50, gunScale = new Vector2(1.4f, 1.5f), gunColor = new Color(0.65f, 0.68f, 0.75f) },
    };

    public Projectile ProjectilePrefab => projectilePrefab;
    public int SpecialGunCount => specialGuns.Length;

    // The owner's special gun and its shots left (null / 0 = the normal gun).
    public SpecialGun HeldGun => net != null && net.Gun > 0 && net.Gun <= specialGuns.Length && ammo > 0 ? specialGuns[net.Gun - 1] : null;
    public int Ammo => ammo;

    private InputAction attackAction;
    private Rigidbody2D playerRigidbody;
    private CameraShake cameraShake;
    private CoreBridge cores;
    private PlayerNetwork net;
    private PlayerSword sword;
    private float holdTime; // seconds the fire button has been held without a break
    private Vector3 gunRestPosition;
    private Quaternion gunRestRotation;
    private float currentSpread;
    private float nextFireTime;
    private float muzzleFlashTimer;
    private int ammo;
    private int shownGun = -1;
    private Vector3 gunBaseScale;
    private SpriteRenderer gunSprite;
    private Color gunBaseColor;

    private void Awake()
    {
        attackAction = InputSystem.actions.FindAction("Player/Attack");
        playerRigidbody = GetComponent<Rigidbody2D>();
        cameraShake = Camera.main.GetComponent<CameraShake>(); // null if the camera has none → no shake
        cores = GetComponent<CoreBridge>(); // this player's own cores (null → plain gun)
        net = GetComponent<PlayerNetwork>();
        sword = GetComponent<PlayerSword>(); // null → never a sword
        gunRestPosition = gunVisual.localPosition;
        gunRestRotation = gunVisual.localRotation;
        gunBaseScale = gunVisual.localScale;
        gunSprite = gunVisual.GetComponentInChildren<SpriteRenderer>();
        gunBaseColor = gunSprite != null ? gunSprite.color : Color.white;
        currentSpread = minSpread;
    }

    // Owner: a special gun was handed over (PlayerNetwork, from a weapon crate).
    public void EquipSpecial(int type)
    {
        if (type > 0 && type <= specialGuns.Length)
            ammo = Mathf.RoundToInt(specialGuns[type - 1].ammo * (cores != null ? cores.SpecialAmmoMultiplier : 1f)); // Gun Nut: more
    }

    // Every machine: the gun looks like the one held.
    private void ShowHeldGun()
    {
        int held = net != null ? net.Gun : 0;
        if (held == shownGun)
            return;
        shownGun = held;
        SpecialGun special = held > 0 && held <= specialGuns.Length ? specialGuns[held - 1] : null;
        gunVisual.localScale = special != null ? Vector3.Scale(gunBaseScale, new Vector3(special.gunScale.x, special.gunScale.y, 1f)) : gunBaseScale;
        if (gunSprite != null)
            gunSprite.color = special != null ? special.gunColor : gunBaseColor;
        if (held == 0)
            ammo = 0;
    }

    // Turned off while the player is dead (PlayerRespawn). When turned back on, holding Left Mouse
    // must not fire all the shots "missed" while it was off.
    private void OnEnable()
    {
        nextFireTime = Mathf.Max(nextFireTime, Time.time);
    }

    private void OnDisable()
    {
        muzzleFlash.SetActive(false);
        muzzleFlashTimer = 0f;
    }

    private void Update()
    {
        ShowHeldGun();
        UpdateShotFeedback();
        if (net != null && !net.IsMine)
            return; // another machine's player: only its shot feedback plays here (PlayShotFeedback)
        if (InputLock.Blocked || InputLock.Frozen)
        {
            holdTime = 0f;
            return; // no shooting while a menu or the chat is open, or between rounds
        }

        // New click: may fire right away, but never before the cooldown from the last shot is over.
        if (attackAction.WasPressedThisFrame())
            nextFireTime = Mathf.Max(nextFireTime, Time.time);

        bool pointerOnUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        bool holding = attackAction.IsPressed() && !pointerOnUI;
        holdTime = holding ? holdTime + Time.deltaTime : 0f;

        // Draw the Sword: slashes instead of shots (a special gun still shoots).
        if (sword != null && sword.Active)
        {
            if (holding)
                sword.Attack();
            return;
        }

        if (holding && Time.time >= nextFireTime)
        {
            // The rate of the gun this shot comes from (its last bullet may switch back to the normal gun).
            SpecialGun special = HeldGun;
            float rate = (special != null ? special.fireRate : fireRate) * (cores != null ? cores.FireRateMultiplier(holdTime) : 1f);
            Shoot();

            // "+=" instead of "Time.time + interval": a late frame doesn't push the next shot back,
            // so holding the button gives exactly fireRate shots per second at any FPS.
            nextFireTime += 1f / rate;
        }

        currentSpread = Mathf.MoveTowards(currentSpread, minSpread, spreadRecovery * Time.deltaTime);
    }

    private void Shoot()
    {
        SpecialGun special = HeldGun;
        if (special != null && cores != null)
        {
            ShootSpecial(special);
            return;
        }

        // Random angle inside the current spread, then open the spread a bit more.
        // Cores can make the gun steadier (less spread).
        float spreadAngle = Random.Range(-currentSpread, currentSpread) * (cores != null ? cores.SpreadMultiplier : 1f);
        currentSpread = Mathf.Min(currentSpread + spreadPerShot, maxSpread);

        Quaternion shotRotation = firePoint.rotation * Quaternion.Euler(0f, 0f, spreadAngle);
        if (cores != null)
        {
            cores.FireShot(projectilePrefab, firePoint.position, shotRotation, projectileSpeed, projectileDamage, projectileLifetime, playerRigidbody);
        }
        else
        {
            Projectile projectile = Instantiate(projectilePrefab, firePoint.position, shotRotation);
            projectile.Launch(shotRotation * Vector3.right, projectileSpeed, projectileDamage, projectileLifetime, playerRigidbody);
        }

        PlayShotFeedback(spreadAngle, true);
    }

    // A special gun's shot: its bullets fanned out over its spread (one bullet: a random angle inside it), one ammo.
    private void ShootSpecial(SpecialGun special)
    {
        float spread = special.spread * cores.SpreadMultiplier;
        for (int i = 0; i < special.pellets; i++)
        {
            float angle = special.pellets > 1 ? Mathf.Lerp(-spread, spread, i / (special.pellets - 1f)) + Random.Range(-2f, 2f)
                                              : Random.Range(-spread, spread);
            Quaternion rotation = firePoint.rotation * Quaternion.Euler(0f, 0f, angle);
            cores.FireShot(projectilePrefab, firePoint.position, rotation, special.speed, special.damage, special.lifetime, playerRigidbody);
        }
        PlayShotFeedback(0f, true);

        ammo--;
        if (ammo <= 0)
            net.DropGun(); // back to the normal gun
    }

    // Gun kick, muzzle flash and sound (also played for other machines' players when they shoot).
    // shake = shake this screen (only for the player's own shots).
    // This player's bullet hit another player (the host tells this player's machine).
    // Damage 0 = a hit that hurts nobody (the lobby): just the tick.
    public void PlayHitConfirm(Vector2 point, float damage, bool knockout)
    {
        GameAudio.Play(hitConfirmSound, hitConfirmVolume, 0.05f, knockout ? 0.75f : 1f);
        if (damage > 0f && damageNumberPrefab != null)
            Instantiate(damageNumberPrefab, point, Quaternion.identity).Show(damage, knockout ? knockoutColor : Color.white, knockout ? 1.6f : 1f);

        if (knockout)
        {
            GameAudio.Play(knockoutSound, knockoutVolume);
            HitStop hitStop = Camera.main.GetComponent<HitStop>();
            if (hitStop != null)
                hitStop.Freeze(knockoutFreeze);
            if (cameraShake != null)
                cameraShake.Shake(0.3f, 0.2f);
            CRTScreen.Blur(point, 0.5f);
        }
        else if (damage >= bigHitDamage && cameraShake != null)
        {
            cameraShake.Shake(0.15f, 0.12f); // a big hit (a sniper shot) is felt
        }
    }

    public void PlayShotFeedback(float spreadAngle, bool shake)
    {
        gunVisual.localPosition = gunRestPosition - Vector3.right * gunKickDistance;           // gun's local -X = backwards
        gunVisual.localRotation = gunRestRotation * Quaternion.Euler(0f, 0f, spreadAngle);  // gun jerks toward where the bullet went
        muzzleFlash.SetActive(true);
        muzzleFlashTimer = muzzleFlashDuration;
        // Every flash a little different: a quarter turn and a size step.
        muzzleFlash.transform.localRotation = Quaternion.Euler(0f, 0f, 90f * Random.Range(0, 4));
        muzzleFlash.transform.localScale = Vector3.one * (Random.Range(0, 2) == 0 ? 1.1f : 1.4f);
        ShellCasings.Eject(gunVisual.position, -(Vector2)firePoint.right, shellMaterial, shellColor);

        if (shake && cameraShake != null)
            cameraShake.Shake(cameraShakeStrength, cameraShakeDuration);

        // Other players' shots are quieter than this player's own.
        bool mine = net == null || net.IsMine;
        GameAudio.PlayAt(shootSound, muzzleFlash.transform.position, shootVolume * (mine ? 1f : otherShotsVolume), shootPitchVariation);
    }

    // The gun eases back to rest; the muzzle flash hides after a moment.
    private void UpdateShotFeedback()
    {
        float t = 1f - Mathf.Exp(-gunKickRecovery * Time.deltaTime); // same easing speed at any FPS
        gunVisual.localPosition = Vector3.Lerp(gunVisual.localPosition, gunRestPosition, t);
        gunVisual.localRotation = Quaternion.Slerp(gunVisual.localRotation, gunRestRotation, t);

        if (muzzleFlashTimer > 0f)
        {
            muzzleFlashTimer -= Time.deltaTime;
            if (muzzleFlashTimer <= 0f)
                muzzleFlash.SetActive(false);
        }
    }
}
