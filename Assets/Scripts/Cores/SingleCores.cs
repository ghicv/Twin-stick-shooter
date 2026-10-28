using System.Collections.Generic;
using UnityEngine;

// What the single cores do. They stand alone (no stacking rules). Only CoreBridge calls this.
// It lives on the player (each player has its own cores). "Enemies" are the targets: enemies in single player,
// the other players on LAN. Health and damage only count on the host; the gun and movement cores work on the
// machine that controls the player.
[RequireComponent(typeof(CoreInventory))]
public class SingleCores : MonoBehaviour
{
    // Effects the host (or the player's own machine) shows on the other machines too (PlayerNetwork.ShowEffect).
    public const int DustEffectId = 0;
    public const int BlastEffectId = 1;
    public const int VampireEffectId = 2;   // position = where the blood comes from, scale = health gained
    public const int VoidFeastEffectId = 3; // scale = health gained
    public const int MedicEffectId = 4;
    public const int ExtraJumpEffectId = 5; // position = the feet
    public const int CrashEffectId = 6;

    [Header("Looks")]
    [Tooltip("The pixel font of the words popping up in the world (CHÍ MẠNG!, +30...).")]
    [SerializeField] private Font popFont;

    [Tooltip("A small white square: the pixel specks of effects.")]
    [SerializeField] private Sprite speckSprite;

    [Tooltip("A pixel wing (pointing left): Extra Jump's wings.")]
    [SerializeField] private Sprite wingSprite;

    [SerializeField] private Material ringMaterial;

    [Tooltip("A bright chime: health from Vampire, Void Feast and Medic.")]
    [SerializeField] private AudioClip healSound;
    [SerializeField] private AudioClip whooshSound;

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

    [Header("PLAYER · Orbit (flying swords)")]
    [Tooltip("Flying swords circle the player at this distance (degrees per second), cutting other players' bullets...")]
    [SerializeField] private float orbitRadius = 1.3f;
    [SerializeField] private float orbitSpeed = 160f;

    [Tooltip("...and fly off to a target within reach, hit it for this much and fly back...")]
    [SerializeField] private float orbitDamage = 15f;

    [Tooltip("...then rest this long before flying off again (seconds, each sword on its own).")]
    [SerializeField] private float orbitHitCooldown = 1.2f;

    [Tooltip("A small sword pointing right (the flying swords).")]
    [SerializeField] private Sprite orbSprite;
    [SerializeField] private Color orbColor = new Color(0.5f, 0.9f, 1f);

    [Tooltip("The flying swords' light trails.")]
    [SerializeField] private Material orbitTrailMaterial;

    [Header("PLAYER · Anchor")]
    [Tooltip("Knockback this player takes is multiplied by this.")]
    [Range(0f, 1f)]
    [SerializeField] private float anchorKnockback = 0.6f;

    [Header("PLAYER · Medic")]
    [Tooltip("Health packs heal this many times more...")]
    [SerializeField] private float medicHeal = 2f;

    [Tooltip("...and protect for this many seconds.")]
    [SerializeField] private float medicProtection = 1f;

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

    [Header("GUN · Demolition")]
    [Tooltip("Bullet damage to barrels and the push on crates are multiplied by this.")]
    [SerializeField] private float demolitionMultiplier = 3f;

    [Header("GUN · Gun Nut")]
    [Tooltip("Ammo of the special guns from weapon crates is multiplied by this.")]
    [SerializeField] private float gunNutAmmo = 2f;

    [Header("TARGETS · Void Feast")]
    [Tooltip("Health the player gets back for each target it knocked into the void (and the dash comes back).")]
    [SerializeField] private float voidFeastHeal = 30f;

    [Header("TARGETS · Death Blast")]
    [Tooltip("Targets this player kills explode, hurting the targets within this distance (units).")]
    [SerializeField] private float deathBlastRadius = 2f;

    [SerializeField] private float deathBlastDamage = 20f;
    [SerializeField] private float deathBlastKnockback = 2f;

    [Tooltip("Spawned at the blast. It should destroy itself.")]
    [SerializeField] private ParticleSystem deathBlastEffect;

    [Header("TARGETS · Crash")]
    [Tooltip("Damage to a target knocked into a wall or another enemy (both enemies get it).")]
    [SerializeField] private float crashDamage = 12f;

    [Tooltip("The same target can't crash again for this long (seconds).")]
    [SerializeField] private float crashCooldown = 0.5f;

    [Header("TARGETS · Vampire")]
    [Tooltip("Share of the damage this player deals that comes back as health.")]
    [Range(0f, 1f)]
    [SerializeField] private float vampireLifesteal = 0.15f;

    private CoreInventory inventory;
    private CoreBridge bridge;
    private PlayerHealth playerHealth;
    private PlayerMovement playerMovement;
    private PlayerNetwork net;
    private HitTarget self;
    private CameraShake cameraShake;
    private float lastShotTime = -100f;
    private bool extraLifeUsed;
    private CoreOrbit orbit;
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
        if (popFont != null)
            CoreEffects.PopFont = popFont;
        if (speckSprite != null)
            CoreEffects.Speck = speckSprite;
    }

    private bool Has(CoreType type) => inventory.Has(type);

    private bool OnHost => net.IsHostSide;

    private void OnAdded(CoreType type)
    {
        if (type == CoreType.ThickSkin && OnHost)
            playerHealth.AddMaxHealth(thickSkinHealth);
        if (type == CoreType.ExtraLife)
            extraLifeUsed = false; // a new run or match
    }

    // The orbs come and go with the core (every machine shows them).
    private void Update()
    {
        if (Has(CoreType.Orbit) != (orbit != null))
        {
            if (orbit == null)
            {
                orbit = new GameObject("Orbit").AddComponent<CoreOrbit>();
                orbit.transform.SetParent(transform, false);
                orbit.Setup(this, net, self, orbSprite, orbColor, orbitTrailMaterial, orbitRadius, orbitSpeed, orbitDamage, orbitHitCooldown);
            }
            else
            {
                Destroy(orbit.gameObject);
            }
        }
    }

    // ---------- Player ----------

    public int ExtraAirJumps => Has(CoreType.ExtraJump) ? extraJumps : 0;

    public bool DashUpgraded => Has(CoreType.Dash); // Dash+: faster dashes that can't be hurt

    public float KnockbackTaken => Has(CoreType.Anchor) ? anchorKnockback : 1f;

    // Health packs (Medic): how many times more they heal, and the seconds of protection they give.
    public float HealthPackHeal => Has(CoreType.Medic) ? medicHeal : 1f;
    public float HealthPackProtection => Has(CoreType.Medic) ? medicProtection : 0f;

    public CoreBridge Bridge => bridge;

    // Host: Extra Life → true (once per run; on LAN once per match) = the player comes back right away.
    public bool TryUseExtraLife()
    {
        if (!Has(CoreType.ExtraLife) || extraLifeUsed)
            return false;
        extraLifeUsed = true;
        return true;
    }

    // LAN: a new round.
    // (Extra Life is once per match on LAN, so nothing comes back here now.)
    public void ResetForRound()
    {
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

    // Demolition: bullet damage to barrels and the push on crates.
    public float PropMultiplier => Has(CoreType.Demolition) ? demolitionMultiplier : 1f;

    // Gun Nut: ammo of the special guns.
    public float SpecialAmmoMultiplier => Has(CoreType.GunNut) ? gunNutAmmo : 1f;

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
            {
                playerHealth.Heal(voidFeastHeal);
                net.ResetDash();
                ShowEffect(VoidFeastEffectId, transform.position, voidFeastHeal);
            }
            return;
        }

        if (Has(CoreType.DeathBlast))
        {
            ShowEffect(BlastEffectId, position, deathBlastRadius);
            CoreEffects.Blast(null, 0f, position, deathBlastRadius, deathBlastDamage, deathBlastKnockback, bridge, true, victim, self);
        }
    }

    // Host: this player's bullets or cores dealt that much damage there (Vampire heals a share of it, the blood flies
    // over).
    public void OnDamageDealt(float amount, Vector2 at)
    {
        if (!Has(CoreType.Vampire) || amount <= 0f)
            return;
        playerHealth.Heal(amount * vampireLifesteal);
        ShowEffect(VampireEffectId, at, amount * vampireLifesteal);
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
            ShowEffect(CrashEffectId, point, 1f);
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
    public void ShowEffect(int effect, Vector2 position, float scale)
    {
        PlayEffect(effect, position, scale);
        if (OnHost)
            net.ShowEffect(effect, position, scale);
    }

    public void PlayEffect(int effect, Vector2 position, float scale)
    {
        Vector2 above = (Vector2)transform.position + Vector2.up * 1.4f;
        Color green = new Color(0.45f, 1f, 0.5f);
        switch (effect)
        {
            case DustEffectId:
            case BlastEffectId:
                ParticleSystem prefab = effect == DustEffectId ? dustEffect : deathBlastEffect;
                if (prefab != null)
                    Instantiate(prefab, position, Quaternion.identity).transform.localScale = Vector3.one * scale;
                if (effect == BlastEffectId) // Death Blast: a big orange shock wave
                {
                    CoreEffects.Ring(ringMaterial, new Color(1f, 0.55f, 0.2f), position, scale * 1.3f, 0.4f, 0.4f);
                    CoreEffects.Burst(position, new Color(1f, 0.7f, 0.3f), 16, 10f);
                    CRTScreen.Blur(position, 0.8f);
                }
                if (cameraShake != null)
                    cameraShake.Shake(effect == DustEffectId && scale > 1.5f ? groundPoundShakeStrength : 0.15f,
                                      effect == DustEffectId && scale > 1.5f ? groundPoundShakeDuration : 0.12f);
                break;

            case VampireEffectId: // drops of blood fly from the victim into the player
                for (int i = 0; i < 6; i++)
                    FxSpeck.Spawn(speckSprite, new Color(0.85f, 0.05f, 0.15f), position + Random.insideUnitCircle * 0.3f,
                                  Random.insideUnitCircle * 6f, 0f, 0.8f, 0.12f, 0f, transform);
                CoreEffects.Pop("+" + Mathf.Max(1, Mathf.RoundToInt(scale)), above, green, 0.8f);
                break;

            case VoidFeastEffectId: // a green burst of life
                CoreEffects.Ring(ringMaterial, green, transform.position, 2.2f, 0.35f, 0.4f);
                CoreEffects.Burst(transform.position, green, 14, 7f);
                CoreEffects.Pop("+" + Mathf.RoundToInt(scale), above, green, 1.2f);
                GameAudio.PlayAt(healSound, transform.position, 0.5f, 0.05f);
                break;

            case MedicEffectId: // green crosses floating up, and a shimmer
                for (int i = 0; i < 4; i++)
                    CoreEffects.Pop("+", (Vector2)transform.position + new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(0.2f, 1.2f)), green, 0.9f);
                CoreEffects.Ring(ringMaterial, green, transform.position, 1.6f, 0.2f, 0.35f);
                GameAudio.PlayAt(healSound, transform.position, 0.45f, 0.05f);
                break;

            case ExtraJumpEffectId: // a ring of wind under the feet and a flap of pixel wings
                CoreEffects.Ring(ringMaterial, Color.white, position, 1.1f, 0.15f, 0.25f);
                CoreEffects.Burst(position, new Color(1f, 1f, 1f, 0.8f), 6, 5f, Vector2.down, 0.08f);
                for (int side = -1; side <= 1; side += 2)
                {
                    FxSpeck wing = FxSpeck.Spawn(wingSprite, Color.white, (Vector2)transform.position + new Vector2(side * 0.55f, 0.3f),
                                                 new Vector2(side * 1.5f, -1f), 0f, 0.3f, 1f);
                    wing.GetComponent<SpriteRenderer>().flipX = side > 0;
                }
                GameAudio.PlayAt(whooshSound, position, 0.35f, 0.1f);
                break;

            case CrashEffectId: // dust and a crack of a shock wave where it hit the wall
                if (dustEffect != null)
                    Instantiate(dustEffect, position, Quaternion.identity);
                CoreEffects.Ring(ringMaterial, new Color(0.9f, 0.85f, 0.7f), position, 1.4f, 0.3f, 0.25f);
                CoreEffects.Burst(position, new Color(0.65f, 0.6f, 0.55f), 10, 8f);
                if (cameraShake != null)
                    cameraShake.Shake(0.15f, 0.12f);
                break;
        }
    }
}
