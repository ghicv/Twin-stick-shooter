using UnityEngine;
using UnityEngine.InputSystem;

// Basic gun: hold Left Mouse to shoot projectiles from the fire point, limited by Fire Rate.
// All projectile stats live here and are handed to each projectile when it is fired.
//
// Recoil: bullets leave at a random angle inside the current spread. Every shot opens the spread a bit
// (up to Max Spread) and it closes again over time, so tapping is accurate and spraying is not.
// Each shot also plays a sound, kicks the gun sprite, flashes the muzzle and shakes the camera a little.
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(AudioSource))]
public class PlayerWeapon : MonoBehaviour
{
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
    [SerializeField] private float fireRate = 5f;

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
    [SerializeField] private float shootVolume = 0.7f;

    [Tooltip("Random pitch change per shot, so rapid fire doesn't sound identical.")]
    [Range(0f, 0.3f)]
    [SerializeField] private float shootPitchVariation = 0.08f;

    private InputAction attackAction;
    private Rigidbody2D playerRigidbody;
    private AudioSource audioSource;
    private CameraShake cameraShake;
    private Vector3 gunRestPosition;
    private Quaternion gunRestRotation;
    private float currentSpread;
    private float nextFireTime;
    private float muzzleFlashTimer;

    private void Awake()
    {
        attackAction = InputSystem.actions.FindAction("Player/Attack");
        playerRigidbody = GetComponent<Rigidbody2D>();
        audioSource = GetComponent<AudioSource>();
        cameraShake = Camera.main.GetComponent<CameraShake>(); // null if the camera has none → no shake
        gunRestPosition = gunVisual.localPosition;
        gunRestRotation = gunVisual.localRotation;
        currentSpread = minSpread;
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
        // New click: may fire right away, but never before the cooldown from the last shot is over.
        if (attackAction.WasPressedThisFrame())
            nextFireTime = Mathf.Max(nextFireTime, Time.time);

        if (attackAction.IsPressed() && Time.time >= nextFireTime)
        {
            Shoot();

            // "+=" instead of "Time.time + interval": a late frame doesn't push the next shot back,
            // so holding the button gives exactly fireRate shots per second at any FPS.
            nextFireTime += 1f / fireRate;
        }

        currentSpread = Mathf.MoveTowards(currentSpread, minSpread, spreadRecovery * Time.deltaTime);
        UpdateShotFeedback();
    }

    private void Shoot()
    {
        // Random angle inside the current spread, then open the spread a bit more.
        float spreadAngle = Random.Range(-currentSpread, currentSpread);
        currentSpread = Mathf.Min(currentSpread + spreadPerShot, maxSpread);

        Quaternion shotRotation = firePoint.rotation * Quaternion.Euler(0f, 0f, spreadAngle);
        Vector2 direction = shotRotation * Vector3.right;

        Projectile projectile = Instantiate(projectilePrefab, firePoint.position, shotRotation);
        projectile.Launch(direction, projectileSpeed, projectileDamage, projectileLifetime, playerRigidbody);

        // Feedback
        gunVisual.localPosition = gunRestPosition - Vector3.right * gunKickDistance;           // gun's local -X = backwards
        gunVisual.localRotation = gunRestRotation * Quaternion.Euler(0f, 0f, spreadAngle);  // gun jerks toward where the bullet went
        muzzleFlash.SetActive(true);
        muzzleFlashTimer = muzzleFlashDuration;

        if (cameraShake != null)
            cameraShake.Shake(cameraShakeStrength, cameraShakeDuration);

        if (shootSound != null)
        {
            audioSource.pitch = 1f + Random.Range(-shootPitchVariation, shootPitchVariation);
            audioSource.PlayOneShot(shootSound, shootVolume);
        }
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
