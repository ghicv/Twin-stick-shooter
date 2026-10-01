using System.Collections.Generic;
using UnityEngine;

// Anything bullets and cores can hurt: an enemy / dummy (Dummy) or a player (PlayerHealth, LAN PvP).
// The bullets and the cores only talk to this, so every core works the same against both.
// Damage only counts on the host; on LAN the other machines just show what happens.
public class HitTarget : MonoBehaviour
{
    // Every target in the game (homing, chain lightning, pulls and mines pick from here).
    public static readonly List<HitTarget> Active = new List<HitTarget>();

    private Dummy dummy;
    private PlayerHealth player;

    public bool IsPlayer => player != null;

    // Dying / dead / away (a player waiting to come back): not a target.
    public bool IsDying => dummy != null ? dummy.IsDying : player.IsDead;

    public float HealthFraction => dummy != null ? dummy.HealthFraction : player.Health / player.MaxHealth;

    // A player's own cores (its bullets never hurt it). Null for enemies.
    public CoreBridge OwnCores { get; private set; }

    private void Awake()
    {
        dummy = GetComponent<Dummy>();
        player = GetComponent<PlayerHealth>();
        OwnCores = GetComponent<CoreBridge>();
    }

    private void OnEnable()
    {
        Active.Add(this);
    }

    private void OnDisable()
    {
        Active.Remove(this);
    }

    // normal points out of the target's surface at the hit point (back toward where the hit came from).
    // source = the cores of whoever caused it (kill credit for Vampire, Death Blast, ...), null for enemies.
    public void TakeDamage(float amount, Vector2 point, Vector2 normal, float knockback, CoreBridge source)
    {
        if (dummy != null)
            dummy.TakeDamage(amount, point, normal, knockback, source);
        else
            player.TakeDamage(amount, -normal, knockback, source);
    }

    // Damage without a hit reaction (burning).
    public void TakeTickDamage(float amount, CoreBridge source)
    {
        if (dummy != null)
            dummy.TakeTickDamage(amount, source);
        else
            player.TakeTickDamage(amount, source);
    }

    public void Knock(Vector2 impulse, CoreBridge source)
    {
        if (dummy != null)
            dummy.Knock(impulse, source);
        else
            player.Knock(impulse, source);
    }

    // Moves (and for enemies attacks) slower for a while (factor 0.5 = half speed).
    public void Slow(float factor, float duration)
    {
        if (dummy != null)
            dummy.Slow(factor, duration);
        else
            player.Slow(factor, duration);
    }
}
