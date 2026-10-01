using System.Collections.Generic;
using UnityEngine;

// What the single cores do. They stand alone (no stacking rules). Only CoreBridge calls this.
[RequireComponent(typeof(CoreInventory))]
public class SingleCores : MonoBehaviour
{
    [Header("PLAYER · Ground Pound")]
    [Tooltip("Enemies within this distance of the player's feet are hit by a hard landing (units).")]
    [SerializeField] private float groundPoundRadius = 2.5f;

    [SerializeField] private float groundPoundDamage = 20f;

    [Tooltip("Knockback multiplier of the shockwave (enemies are thrown away and up).")]
    [SerializeField] private float groundPoundKnockback = 3f;

    [Tooltip("Spawned at the feet (Ground Pound) and where enemies crash (Crash), e.g. DustPuff. It should destroy itself.")]
    [SerializeField] private ParticleSystem dustEffect;

    [SerializeField] private float groundPoundEffectScale = 2.5f;
    [SerializeField] private float groundPoundShakeStrength = 0.3f;
    [SerializeField] private float groundPoundShakeDuration = 0.2f;

    [Header("PLAYER · Extra Jump")]
    [SerializeField] private int extraJumps = 1;

    [Header("PLAYER · Thick Skin")]
    [Tooltip("Max health added (and given right away) when the core is picked.")]
    [SerializeField] private float thickSkinHealth = 40f;

    [Header("PLAYER · Regeneration")]
    [Tooltip("Health per second while not hurt for Regeneration Delay.")]
    [SerializeField] private float regenerationRate = 2f;

    [SerializeField] private float regenerationDelay = 3f;

    [Header("GUN · Hot Barrel")]
    [Tooltip("Fire rate multiplier after holding fire for Hot Barrel Ramp Time.")]
    [SerializeField] private float hotBarrelMaxRate = 1.6f;

    [Tooltip("Seconds of non-stop fire to reach the max rate. Letting go cools the gun down at once.")]
    [SerializeField] private float hotBarrelRampTime = 1.5f;

    [Header("GUN · Steady Aim")]
    [Tooltip("Spread multiplier (0 = perfectly straight).")]
    [Range(0f, 1f)]
    [SerializeField] private float steadyAimSpread = 0.2f;

    [Header("GUN · Air Gunner")]
    [Tooltip("Damage multiplier of shots fired while in the air.")]
    [SerializeField] private float airGunnerDamage = 1.35f;

    [Header("GUN · Adrenaline")]
    [Tooltip("Below this share of max health...")]
    [Range(0f, 1f)]
    [SerializeField] private float adrenalineHealth = 0.35f;

    [Tooltip("...the fire rate is multiplied by this.")]
    [SerializeField] private float adrenalineRate = 1.5f;

    [Header("GUN · Opener")]
    [Tooltip("A shot after not firing for this long (seconds)...")]
    [SerializeField] private float openerPause = 0.5f;

    [Tooltip("...deals this much more damage...")]
    [SerializeField] private float openerDamage = 3f;

    [Tooltip("...and its bullets are this much bigger.")]
    [SerializeField] private float openerSize = 1.5f;

    [Header("ENEMY · Void Feast")]
    [Tooltip("Health the player gets back for each enemy that falls into the void.")]
    [SerializeField] private float voidFeastHeal = 10f;

    [Header("ENEMY · Death Blast")]
    [Tooltip("Killed enemies explode, hurting the enemies within this distance (units).")]
    [SerializeField] private float deathBlastRadius = 2f;

    [SerializeField] private float deathBlastDamage = 20f;
    [SerializeField] private float deathBlastKnockback = 2f;

    [Tooltip("Spawned at the blast. It should destroy itself.")]
    [SerializeField] private ParticleSystem deathBlastEffect;

    [Header("ENEMY · Crash")]
    [Tooltip("Damage to an enemy knocked into a wall or another enemy (both enemies get it).")]
    [SerializeField] private float crashDamage = 12f;

    [Tooltip("The same enemy can't crash again for this long (seconds).")]
    [SerializeField] private float crashCooldown = 0.5f;

    [Header("ENEMY · Vampire")]
    [Tooltip("Health the player gets back for each enemy killed (not fallen off).")]
    [SerializeField] private float vampireHeal = 3f;

    private CoreInventory inventory;
    private PlayerHealth playerHealth;
    private PlayerMovement playerMovement;
    private CameraShake cameraShake;
    private float lastShotTime = -100f;
    private bool extraLifeUsed;
    private readonly List<Dummy> poundHits = new List<Dummy>();
    private readonly Dictionary<Dummy, float> lastCrash = new Dictionary<Dummy, float>();

    private void Awake()
    {
        inventory = GetComponent<CoreInventory>();
        GameObject player = GameObject.FindWithTag("Player");
        playerHealth = player.GetComponent<PlayerHealth>();
        playerMovement = player.GetComponent<PlayerMovement>();
        cameraShake = Camera.main.GetComponent<CameraShake>(); // null → no shake
        inventory.Added += OnAdded;
    }

    private bool Has(CoreType type) => inventory.Has(type);

    private void OnAdded(CoreType type)
    {
        if (type == CoreType.ThickSkin)
            playerHealth.AddMaxHealth(thickSkinHealth);
    }

    private void Update()
    {
        if (Has(CoreType.Regeneration) && Time.time - playerHealth.LastDamageTime >= regenerationDelay)
            playerHealth.Heal(regenerationRate * Time.deltaTime);
    }

    // ---------- Player ----------

    public int ExtraAirJumps => Has(CoreType.ExtraJump) ? extraJumps : 0;

    public bool DashUnlocked => Has(CoreType.Dash);

    // Extra Life: true (once per run) = the player comes back instead of losing the run.
    public bool TryUseExtraLife()
    {
        if (!Has(CoreType.ExtraLife) || extraLifeUsed)
            return false;
        extraLifeUsed = true;
        return true;
    }

    // Ground Pound: a hard landing throws the enemies around the feet away and up.
    public void OnHardLanding(Vector2 feet)
    {
        if (!Has(CoreType.GroundPound))
            return;

        SpawnDust(feet, groundPoundEffectScale);
        if (cameraShake != null)
            cameraShake.Shake(groundPoundShakeStrength, groundPoundShakeDuration);

        poundHits.Clear();
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(feet, groundPoundRadius))
        {
            Dummy dummy = hit.GetComponentInParent<Dummy>();
            if (dummy == null || poundHits.Contains(dummy))
                continue;
            poundHits.Add(dummy);

            // Away from the feet and up. TakeDamage pushes against the hit normal.
            Vector2 away = (Vector2)dummy.transform.position - feet;
            away.y = Mathf.Max(away.y, 0f);
            Vector2 push = (away.normalized + Vector2.up).normalized;
            dummy.TakeDamage(groundPoundDamage, hit.ClosestPoint(feet), -push, groundPoundKnockback);
        }
    }

    // ---------- Gun ----------

    // holdTime = seconds the fire button has been held without a break.
    public float FireRateMultiplier(float holdTime)
    {
        float rate = 1f;
        if (Has(CoreType.HotBarrel))
            rate *= Mathf.Lerp(1f, hotBarrelMaxRate, holdTime / hotBarrelRampTime);
        if (Has(CoreType.Adrenaline) && playerHealth.Health < playerHealth.MaxHealth * adrenalineHealth)
            rate *= adrenalineRate;
        return rate;
    }

    public float SpreadMultiplier => Has(CoreType.SteadyAim) ? steadyAimSpread : 1f;

    // Called for every shot: damage and bullet size multipliers for it.
    public float ShotDamage(out float size)
    {
        float damage = 1f;
        size = 1f;
        if (Has(CoreType.Opener) && Time.time - lastShotTime >= openerPause)
        {
            damage *= openerDamage;
            size *= openerSize;
        }
        if (Has(CoreType.AirGunner) && !playerMovement.IsGrounded)
            damage *= airGunnerDamage;
        lastShotTime = Time.time;
        return damage;
    }

    // ---------- Enemies ----------

    // An enemy exploded: killed, or fell into the void.
    public void OnEnemyDied(Dummy enemy, Vector2 position)
    {
        if (enemy.FellOffMap)
        {
            if (Has(CoreType.VoidFeast))
                playerHealth.Heal(voidFeastHeal);
            return;
        }

        if (Has(CoreType.Vampire))
            playerHealth.Heal(vampireHeal);
        if (Has(CoreType.DeathBlast))
            CoreEffects.Blast(deathBlastEffect, deathBlastRadius, position, deathBlastRadius, deathBlastDamage, deathBlastKnockback, enemy);
    }

    // An enemy was knocked into a wall (other = null) or into another enemy.
    public void OnEnemyCrash(Dummy enemy, Dummy other, Vector2 point)
    {
        if (!Has(CoreType.Crash))
            return;

        bool hurt = Crash(enemy, point);
        if (other != null)
            hurt |= Crash(other, point);
        if (hurt)
        {
            SpawnDust(point, 1f);
            if (cameraShake != null)
                cameraShake.Shake(0.15f, 0.12f);
        }
    }

    private bool Crash(Dummy enemy, Vector2 point)
    {
        float last;
        if (enemy.IsDying || (lastCrash.TryGetValue(enemy, out last) && Time.time - last < crashCooldown))
            return false;

        lastCrash[enemy] = Time.time;
        Vector2 normal = (point - (Vector2)enemy.transform.position).normalized;
        enemy.TakeDamage(crashDamage, point, normal, 0f); // no extra push, so it doesn't bounce back and forth
        return true;
    }

    private void SpawnDust(Vector2 position, float scale)
    {
        if (dustEffect == null)
            return;
        ParticleSystem effect = Instantiate(dustEffect, position, Quaternion.identity);
        effect.transform.localScale = Vector3.one * scale;
    }
}
