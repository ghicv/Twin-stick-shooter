using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Network side of a player: everything about it that has to reach the other machines goes through here,
// so the gameplay scripts stay plain MonoBehaviours.
// - Each machine runs only its own player (movement, aim, input); the others see its position through the
//   NetworkTransform (owner authority) and its aim from here.
// - The host decides what counts: health, death, cores. They are sent to every machine.
// - Shots: the host spawns the real bullets (they deal the damage); every other machine spawns look-alike
//   bullets from the same random seed, so they fly the same way but never hurt anyone.
// - The host gives every player a slot (0-3) that sets its color and where it appears.
public class PlayerNetwork : NetworkBehaviour
{
    [Tooltip("Player colors by slot (P1-P4).")]
    [SerializeField] private Color[] slotColors =
    {
        new Color(1f, 0.42f, 0.21f),   // orange
        new Color(0.31f, 0.8f, 0.77f), // teal
        new Color(1f, 0.9f, 0.43f),    // yellow
        new Color(0.78f, 0.49f, 1f),   // purple
    };

    [Tooltip("Rotates toward the aim; its rotation is sent to the other machines.")]
    [SerializeField] private Transform gunPivot;

    [Tooltip("The owner only sends a new aim when it turned at least this much (degrees).")]
    [SerializeField] private float aimSendThreshold = 0.5f;

    // The player this machine controls (null before it has spawned).
    public static PlayerNetwork Local { get; private set; }

    // Every player in the session, on every machine.
    public static readonly List<PlayerNetwork> All = new List<PlayerNetwork>();

    private readonly NetworkVariable<int> slot = new NetworkVariable<int>(-1);
    private readonly NetworkVariable<float> aimAngle = new NetworkVariable<float>(0f,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private readonly NetworkVariable<float> health = new NetworkVariable<float>(100f);
    private readonly NetworkVariable<float> maxHealth = new NetworkVariable<float>(100f);
    private readonly NetworkVariable<bool> dead = new NetworkVariable<bool>(false);
    private NetworkList<int> cores;

    private PlayerHealth playerHealth;
    private PlayerRespawn respawn;
    private PlayerWeapon weapon;
    private HitTarget target;
    private CoreBridge coreBridge;
    private CoreInventory inventory;
    private bool placed;

    public int Slot => slot.Value;
    public Color Color => slot.Value >= 0 ? slotColors[slot.Value % slotColors.Length] : Color.white;
    public CoreBridge Cores => coreBridge;

    // Health and death are written by the host only.
    public float Health { get => health.Value; set => health.Value = value; }
    public float MaxHealth { get => maxHealth.Value; set => maxHealth.Value = value; }
    public bool Dead { get => dead.Value; set => dead.Value = value; }

    // Alive and on screen (not exploded / waiting to come back / traveling).
    public bool InPlay => !dead.Value && !respawn.IsTraveling;

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        respawn = GetComponent<PlayerRespawn>();
        weapon = GetComponent<PlayerWeapon>();
        target = GetComponent<HitTarget>();
        coreBridge = GetComponent<CoreBridge>();
        inventory = GetComponent<CoreInventory>();
        cores = new NetworkList<int>();
    }

    public override void OnNetworkSpawn()
    {
        All.Add(this);
        if (IsServer)
        {
            slot.Value = FreeSlot();
            maxHealth.Value = playerHealth.StartMaxHealth;
            health.Value = maxHealth.Value;
        }
        slot.OnValueChanged += OnSlotChanged;
        ApplyColor();

        // Cores this player already has (joining late), then every new one.
        foreach (int core in cores)
            inventory.Add((CoreType)core);
        cores.OnListChanged += OnCoresChanged;

        // Only the owner runs the controls; the others just show where this player is.
        bool mine = IsOwner;
        GetComponent<PlayerMovement>().enabled = mine;
        GetComponent<PlayerAim>().enabled = mine;

        if (mine)
        {
            Local = this;
            PlaceAtSpawnPoint();
        }
    }

    public override void OnNetworkDespawn()
    {
        All.Remove(this);
        slot.OnValueChanged -= OnSlotChanged;
        cores.OnListChanged -= OnCoresChanged;
        if (Local == this)
            Local = null;
    }

    private void OnSlotChanged(int previous, int current)
    {
        ApplyColor();
        if (IsOwner)
            PlaceAtSpawnPoint();
    }

    private void OnCoresChanged(NetworkListEvent<int> change)
    {
        if (change.Type == NetworkListEvent<int>.EventType.Add)
            inventory.Add((CoreType)change.Value);
    }

    private void Update()
    {
        if (!IsSpawned)
            return;

        if (IsOwner)
        {
            float angle = gunPivot.eulerAngles.z;
            if (Mathf.Abs(Mathf.DeltaAngle(angle, aimAngle.Value)) >= aimSendThreshold)
                aimAngle.Value = angle;
        }
        else
        {
            gunPivot.rotation = Quaternion.Euler(0f, 0f, aimAngle.Value);
        }
    }

    // ---------- Shots ----------

    // Owner: fires one shot. The host spawns the real bullets, every other machine look-alikes (same seed).
    public void Fire(Vector2 position, float angle, float speed, float damage, float lifetime, float size)
    {
        int seed = Random.Range(1, int.MaxValue);
        if (IsServer)
        {
            coreBridge.SpawnShot(position, angle, speed, damage, lifetime, size, seed, false);
            ShotLookAlikeRpc(position, angle, speed, damage, lifetime, size, seed);
        }
        else
        {
            coreBridge.SpawnShot(position, angle, speed, damage, lifetime, size, seed, true); // no waiting for the host
            FireRpc(position, angle, speed, damage, lifetime, size, seed);
        }
    }

    [Rpc(SendTo.Server)]
    private void FireRpc(Vector2 position, float angle, float speed, float damage, float lifetime, float size, int seed)
    {
        coreBridge.SpawnShot(position, angle, speed, damage, lifetime, size, seed, false);
        ShotLookAlikeRpc(position, angle, speed, damage, lifetime, size, seed);
    }

    [Rpc(SendTo.NotServer)]
    private void ShotLookAlikeRpc(Vector2 position, float angle, float speed, float damage, float lifetime, float size, int seed)
    {
        if (IsOwner)
            return; // the shooter made its own already
        coreBridge.SpawnShot(position, angle, speed, damage, lifetime, size, seed, true);
        weapon.PlayShotFeedback(0f, false);
    }

    // ---------- Health (host → machines) ----------

    public void PlayHurt(float blinkTime) => HurtRpc(blinkTime);

    [Rpc(SendTo.Everyone)]
    private void HurtRpc(float blinkTime) => playerHealth.PlayHurt(blinkTime);

    public void PlayHurtBlinkOnly(float blinkTime) => BlinkRpc(blinkTime);

    [Rpc(SendTo.Everyone)]
    private void BlinkRpc(float blinkTime) => playerHealth.PlayBlink(blinkTime);

    public void PushOwner(Vector2 velocity, bool set) => PushRpc(velocity, set);

    [Rpc(SendTo.Owner)]
    private void PushRpc(Vector2 velocity, bool set) => playerHealth.ApplyPush(velocity, set);

    public void SlowOwner(float factor, float duration) => SlowRpc(factor, duration);

    [Rpc(SendTo.Owner)]
    private void SlowRpc(float factor, float duration) => playerHealth.ApplySlow(factor, duration);

    // ---------- Owner → host ----------

    public void RequestInvulnerable(float duration) => InvulnerableRpc(duration);

    [Rpc(SendTo.Server)]
    private void InvulnerableRpc(float duration) => playerHealth.SetInvulnerable(duration);

    public void ReportCrash(Vector2 point) => CrashRpc(point);

    [Rpc(SendTo.Server)]
    private void CrashRpc(Vector2 point)
    {
        if (playerHealth.LastAttacker != null)
            playerHealth.LastAttacker.OnEnemyCrash(target, null, point);
    }

    public void ReportFell() => FellRpc();

    [Rpc(SendTo.Server)]
    private void FellRpc() => respawn.Die(true);

    public void ReportHardLanding(Vector2 feet) => HardLandingRpc(feet);

    [Rpc(SendTo.Server)]
    private void HardLandingRpc(Vector2 feet) => coreBridge.OnHardLandingOnHost(feet);

    public void PickCore(CoreType type) => PickCoreRpc((int)type);

    [Rpc(SendTo.Server)]
    private void PickCoreRpc(int type)
    {
        if (inventory.Has((CoreType)type))
            return;
        inventory.Add((CoreType)type); // right away on the host (the list event adds it everywhere else)
        cores.Add(type);
    }

    // ---------- Death (host → machines) ----------

    // outcome: see PlayerRespawn.DeathOutcome.
    public void PlayDeath(Vector2 point, int outcome, Vector2 spawn) => DeathRpc(point, outcome, spawn);

    [Rpc(SendTo.Everyone)]
    private void DeathRpc(Vector2 point, int outcome, Vector2 spawn) => respawn.PlayDeath(point, outcome, spawn);

    public void ReportRevived() => RevivedRpc();

    [Rpc(SendTo.Server)]
    private void RevivedRpc()
    {
        dead.Value = false;
        playerHealth.Refill();
    }

    // ---------- Effects (host → the other machines) ----------

    public void ShowEffect(int effect, Vector2 position, float scale) => EffectRpc(effect, position, scale);

    [Rpc(SendTo.NotServer)]
    private void EffectRpc(int effect, Vector2 position, float scale) => coreBridge.PlayEffect(effect, position, scale);

    // ---------- Setup ----------

    // Lowest slot nobody uses (host side).
    private int FreeSlot()
    {
        for (int i = 0; i < 4; i++)
        {
            bool taken = false;
            foreach (PlayerNetwork other in All)
                if (other != this && other.slot.Value == i)
                    taken = true;
            if (!taken)
                return i;
        }
        return 0;
    }

    private void ApplyColor()
    {
        if (slot.Value >= 0)
            playerHealth.SetBodyColor(Color);
    }

    // Each slot starts at its own spawn point of the current map, spread over the list.
    private void PlaceAtSpawnPoint()
    {
        if (placed || slot.Value < 0)
            return;

        MapManager maps = FindAnyObjectByType<MapManager>();
        if (maps == null || maps.CurrentMap.SpawnPoints.Length == 0)
            return;

        Transform[] points = maps.CurrentMap.SpawnPoints;
        Vector3 position = points[slot.Value * points.Length / 4 % points.Length].position;
        var rb = GetComponent<Rigidbody2D>();
        rb.position = position;
        transform.position = position;
        rb.linearVelocity = Vector2.zero;
        placed = true;
    }
}
