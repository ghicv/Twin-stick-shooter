using System.Collections.Generic;
using UnityEngine;

// What the single cores do. They stand alone (no stacking rules). Only CoreBridge calls this.
// It lives on the player (each player has its own cores). "Enemies" are the targets: enemies in single player,
// the other players on LAN. Health and damage only count on the host; the gun and movement cores work on the
// machine that controls the player.
[RequireComponent(typeof(CoreInventory))]
public class SingleCores : MonoBehaviour
{
    // Effects the host shows on the other machines too (PlayerNetwork.ShowEffect).
    public const int DustEffectId = 0;
    public const int BlastEffectId = 1;

    [Header("PLAYER · Ground Pound")]
    [Tooltip("Targets within this distance of the player's feet are hit by a hard landing (units).")]
    [SerializeField] private float groundPoundRadius = 2.5f;

    [SerializeField] private float groundPoundDamage = 20f;

    [Tooltip("Knockback multiplier of the shockwave (targets are thrown away and up).")]
    [SerializeField] private float groundPoundKnockback = 3f;

    [Tooltip("Spawned at the feet (Ground Pound) and where targets crash (Crash), e.g. DustPuff. It should destroy itself.")]
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
    [Tooltip("Health the player gets back for each target it knocked into the void.")]
    [SerializeField] private float voidFeastHeal = 10f;

    [Header("ENEMY · Death Blast")]
    [Tooltip("Targets this player kills explode, hurting the targets within this distance (units).")]
    [SerializeField] private float deathBlastRadius = 2f;

    [SerializeField] private float deathBlastDamage = 20f;
    [SerializeField] private float deathBlastKnockback = 2f;

    [Tooltip("Spawned at the blast. It should destroy itself.")]
    [SerializeField] private ParticleSystem deathBlastEffect;

    [Header("ENEMY · Crash")]
    [Tooltip("Damage to a target knocked into a wall or another enemy (both enemies get it).")]
    [SerializeField] private float crashDamage = 12f;

    [Tooltip("The same target can't crash again for this long (seconds).")]
    [SerializeField] private float crashCooldown = 0.5f;

    [Header("ENEMY · Vampire")]
    [Tooltip("Health the player gets back for each target it kills (not fallen off).")]
    [SerializeField] private float vampireHeal = 3f;

    private CoreInventory inventory;
    private CoreBridge bridge;
    private PlayerHealth playerHealth;
    private PlayerMovement playerMovement;
    private PlayerNetwork net;
    private HitTarget self;
    private CameraShake cameraShake;
    private float lastShotTime = -100f;
    private bool extraLifeUsed;
    private readonly List<HitTarget> poundHits = new List<HitTarget>();
    private readonly Dictionary<HitTarget, float> lastCrash = new Dictionary<HitTarget, float>();

    private void Awake()
    {
        inventory = GetComponent<CoreInventory>();
        bridge = GetComponent<CoreBridge>();
        playerHealth = GetComponent<PlayerHealth>();
        playerMovement = GetComponent<PlayerMovement>();
        net = GetComponent<PlayerNetwork>();
        self = GetComponent<HitTarget>();
        cameraShake = Camera.main.GetComponent<CameraShake>(); // null → no shake
        inventory.Added += OnAdded;
    }

    private bool Has(CoreType type) => inventory.Has(type);

    private bool OnHost => net.IsHostSide;

    private void OnAdded(CoreType type)
    {
        if (type == CoreType.ThickSkin && OnHost)
            playerHealth.AddMaxHealth(thickSkinHealth);
    }

    private void Update()
    {
        if (OnHost && Has(CoreType.Regeneration) && Time.time - playerHealth.LastDamageTime >= regenerationDelay)
            playerHealth.Heal(regenerationRate * Time.deltaTime);
    }

    // ---------- Player ----------

    public int ExtraAirJumps => Has(CoreType.ExtraJump) ? extraJumps : 0;

    public bool DashUnlocked => Has(CoreType.Dash);

    // Host: Extra Life → true (once per run; on LAN once per round) = the player comes back right away.
    public bool TryUseExtraLife()
    {
        if (!Has(CoreType.ExtraLife) || extraLifeUsed)
            return false;
        extraLifeUsed = true;
        return true;
    }

    // LAN: a new round.
    public void ResetForRound()
    {
        extraLifeUsed = false;
    }

    // Host: Ground Pound: a hard landing throws the targets around the feet away and up.
    public void OnHardLanding(Vector2 feet)
    {
        if (!Has(CoreType.GroundPound))
            return;

        ShowEffect(DustEffectId, feet, groundPoundEffectScale);
        poundHits.Clear();
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(feet, groundPoundRadius))
        {
            HitTarget target = hit.GetComponentInParent<HitTarget>();
            if (target == null || target == self || target.IsDying || poundHits.Contains(target))
                continue;
            poundHits.Add(target);

            // Away from the feet and up. TakeDamage pushes against the hit normal.
            Vector2 away = (Vector2)target.transform.position - feet;
            away.y = Mathf.Max(away.y, 0f);
            Vector2 push = (away.normalized + Vector2.up).normalized;
            target.TakeDamage(groundPoundDamage, hit.ClosestPoint(feet), -push, groundPoundKnockback, bridge);
        }
    }

    // ---------- Gun (the machine that controls the player) ----------

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

    // ---------- Enemies (host) ----------

    // A target this player hurt last is gone: killed, or knocked into the void.
    public void OnEnemyDied(HitTarget victim, Vector2 position, bool fellOff)
    {
        if (fellOff)
        {
            if (Has(CoreType.VoidFeast))
                playerHealth.Heal(voidFeastHeal);
            return;
        }

        if (Has(CoreType.Vampire))
            playerHealth.Heal(vampireHeal);
        if (Has(CoreType.DeathBlast))
        {
            ShowEffect(BlastEffectId, position, deathBlastRadius);
            CoreEffects.Blast(null, 0f, position, deathBlastRadius, deathBlastDamage, deathBlastKnockback, bridge, true, victim, self);
        }
    }

    // A target this player hit was knocked into a wall (other = null) or into another enemy.
    public void OnEnemyCrash(HitTarget victim, HitTarget other, Vector2 point)
    {
        if (!Has(CoreType.Crash))
            return;

        bool hurt = Crash(victim, point);
        if (other != null && other != self)
            hurt |= Crash(other, point);
        if (hurt)
            ShowEffect(DustEffectId, point, 1f);
    }

    private bool Crash(HitTarget target, Vector2 point)
    {
        float last;
        if (target.IsDying || (lastCrash.TryGetValue(target, out last) && Time.time - last < crashCooldown))
            return false;

        lastCrash[target] = Time.time;
        Vector2 normal = (point - (Vector2)target.transform.position).normalized;
        target.TakeDamage(crashDamage, point, normal, 0f, bridge); // no extra push, so it doesn't bounce back and forth
        return true;
    }

    // ---------- Effects ----------

    // On this machine, and (from the host) on the other machines too.
    private void ShowEffect(int effect, Vector2 position, float scale)
    {
        PlayEffect(effect, position, scale);
        if (OnHost)
            net.ShowEffect(effect, position, scale);
    }

    public void PlayEffect(int effect, Vector2 position, float scale)
    {
        ParticleSystem prefab = effect == DustEffectId ? dustEffect : deathBlastEffect;
        if (prefab != null)
        {
            ParticleSystem instance = Instantiate(prefab, position, Quaternion.identity);
            instance.transform.localScale = Vector3.one * scale;
        }
        if (cameraShake != null)
            cameraShake.Shake(effect == DustEffectId && scale > 1.5f ? groundPoundShakeStrength : 0.15f,
                              effect == DustEffectId && scale > 1.5f ? groundPoundShakeDuration : 0.12f);
    }
}
