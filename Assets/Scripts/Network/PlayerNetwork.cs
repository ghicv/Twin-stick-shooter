using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

// Network side of a player: everything about it that has to reach the other machines goes through here,
// so the gameplay scripts stay plain MonoBehaviours.
// - Single player has no network at all: MainMenu just makes the player and calls StartOffline. This machine is
//   then both the host and the owner, and every message below goes straight to it instead of over the network.
// - Each machine runs only its own player (movement, aim, input); the others see its position through the
//   NetworkTransform (owner authority) and its aim from here.
// - The host decides what counts: health, death, cores. They are sent to every machine.
// - Shots: the host spawns the real bullets (they deal the damage); every other machine spawns look-alike
//   bullets from the same random seed, so they fly the same way but never hurt anyone.
// - The host gives every player a slot (0-3) that sets where it appears (and its color in single player).
//   On LAN the player has a skin (SkinCatalog): its color and a head accessory.
public class PlayerNetwork : NetworkBehaviour
{
    // Player colors by slot (P1-P4).
    private static readonly Color[] SlotColors =
    {
        new Color(1f, 0.42f, 0.21f),   // orange
        new Color(0.31f, 0.8f, 0.77f), // teal
        new Color(1f, 0.9f, 0.43f),    // yellow
        new Color(0.78f, 0.49f, 1f),   // purple
    };

    public static Color SlotColor(int slot) => SlotColors[slot % SlotColors.Length];

    [Tooltip("Rotates toward the aim; its rotation is sent to the other machines.")]
    [SerializeField] private Transform gunPivot;

    [Tooltip("The owner only sends a new aim when it turned at least this much (degrees).")]
    [SerializeField] private float aimSendThreshold = 0.5f;

    [Tooltip("Shows the skin's accessory (a child of the body, so it squashes with it).")]
    [SerializeField] private SpriteRenderer accessory;

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
    private readonly NetworkVariable<bool> ready = new NetworkVariable<bool>(false);
    private readonly NetworkVariable<int> skin = new NetworkVariable<int>(-1); // -1 = no skin (single player)
    private NetworkList<int> cores;

    private PlayerHealth playerHealth;
    private PlayerRespawn respawn;
    private PlayerWeapon weapon;
    private HitTarget target;
    private CoreBridge coreBridge;
    private CoreInventory inventory;
    private bool placed;
    private int pendingSkin = -1;

    // Single player (no network).
    public bool Offline { get; private set; }

    // This machine decides what counts for this player (health, death, cores): the host, or single player.
    public bool IsHostSide => Offline || IsServer;

    // This machine controls this player: its owner, or single player.
    public bool IsMine => Offline || IsOwner;

    // Single player keeps these here instead (a NetworkVariable must not be written without a network).
    private float offlineHealth;
    private float offlineMaxHealth;
    private bool offlineDead;

    public int Slot => Offline ? 0 : slot.Value;
    public Color Color => skin.Value >= 0 && SkinCatalog.Instance != null ? SkinCatalog.Instance.Get(skin.Value).color
                        : Slot >= 0 ? SlotColor(Slot) : Color.white;
    public CoreBridge Cores => coreBridge;
    public PlayerRespawn Respawn => respawn;

    // Health and death are written by the host only.
    public float Health
    {
        get => Offline ? offlineHealth : health.Value;
        set { if (Offline) offlineHealth = value; else health.Value = value; }
    }

    public float MaxHealth
    {
        get => Offline ? offlineMaxHealth : maxHealth.Value;
        set { if (Offline) offlineMaxHealth = value; else maxHealth.Value = value; }
    }

    public bool Dead
    {
        get => Offline ? offlineDead : dead.Value;
        set { if (Offline) offlineDead = value; else dead.Value = value; }
    }

    // Its machine has finished joining (it gets every message from now on). A match only starts when all are ready.
    public bool Ready => ready.Value;

    // Alive and on screen (not exploded / waiting to come back / traveling).
    public bool InPlay => !Dead && !respawn.IsTraveling;

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
        if (IsServer)
        {
            slot.Value = FreeSlot();
            skin.Value = pendingSkin;
            maxHealth.Value = playerHealth.StartMaxHealth;
            health.Value = maxHealth.Value;
        }
        Setup(IsOwner);
    }

    // Single player: right after MainMenu made the player (never spawned on a network).
    public void StartOffline()
    {
        Offline = true;
        // Nothing to sync: the body moves by itself (NetworkRigidbody2D had made it kinematic for the network).
        GetComponent<NetworkTransform>().enabled = false;
        GetComponent<NetworkRigidbody2D>().enabled = false;
        GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Dynamic;

        MaxHealth = playerHealth.StartMaxHealth;
        Health = MaxHealth;
        Setup(true);
    }

    private void Setup(bool mine)
    {
        All.Add(this);
        slot.OnValueChanged += OnSlotChanged;
        skin.OnValueChanged += OnSkinChanged;
        ApplyColor();

        // Cores this player already has (joining late), then every new one.
        foreach (int core in cores)
            inventory.Add((CoreType)core);
        cores.OnListChanged += OnCoresChanged;

        // Only the owner runs the controls; the others just show where this player is.
        GetComponent<PlayerMovement>().enabled = mine;
        GetComponent<PlayerAim>().enabled = mine;

        if (mine)
        {
            Local = this;
            if (!Offline)
                ReadyRpc(); // this machine is in sync now
            PlaceAtSpawnPoint();
        }
    }

    public override void OnNetworkDespawn() => Cleanup();

    public override void OnDestroy()
    {
        if (Offline)
            Cleanup(); // never despawned: it was never spawned
        base.OnDestroy();
    }

    private void Cleanup()
    {
        All.Remove(this);
        slot.OnValueChanged -= OnSlotChanged;
        skin.OnValueChanged -= OnSkinChanged;
        cores.OnListChanged -= OnCoresChanged;
        if (Local == this)
            Local = null;
    }

    private void OnSlotChanged(int previous, int current)
    {
        ApplyColor();
        if (IsMine)
            PlaceAtSpawnPoint();
    }

    private void OnSkinChanged(int previous, int current) => ApplyColor();

    private void OnCoresChanged(NetworkListEvent<int> change)
    {
        if (change.Type == NetworkListEvent<int>.EventType.Add)
            inventory.Add((CoreType)change.Value);
        else if (change.Type == NetworkListEvent<int>.EventType.Clear)
            inventory.Clear();
    }

    // Host: the player loses every core and its max health is back to normal (a new match).
    public void ClearCoresOnHost()
    {
        cores.Clear();
        inventory.Clear();
        maxHealth.Value = playerHealth.StartMaxHealth;
        health.Value = maxHealth.Value;
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
        if (IsHostSide)
        {
            coreBridge.SpawnShot(position, angle, speed, damage, lifetime, size, seed, false);
            if (!Offline)
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

    public void PlayHurt(float blinkTime)
    {
        if (Offline)
            playerHealth.PlayHurt(blinkTime);
        else
            HurtRpc(blinkTime);
    }

    [Rpc(SendTo.Everyone)]
    private void HurtRpc(float blinkTime) => playerHealth.PlayHurt(blinkTime);

    public void PlayHurtBlinkOnly(float blinkTime)
    {
        if (Offline)
            playerHealth.PlayBlink(blinkTime);
        else
            BlinkRpc(blinkTime);
    }

    [Rpc(SendTo.Everyone)]
    private void BlinkRpc(float blinkTime) => playerHealth.PlayBlink(blinkTime);

    public void PushOwner(Vector2 velocity, bool set)
    {
        if (Offline)
            playerHealth.ApplyPush(velocity, set);
        else
            PushRpc(velocity, set);
    }

    [Rpc(SendTo.Owner)]
    private void PushRpc(Vector2 velocity, bool set) => playerHealth.ApplyPush(velocity, set);

    public void SlowOwner(float factor, float duration)
    {
        if (Offline)
            playerHealth.ApplySlow(factor, duration);
        else
            SlowRpc(factor, duration);
    }

    [Rpc(SendTo.Owner)]
    private void SlowRpc(float factor, float duration) => playerHealth.ApplySlow(factor, duration);

    // ---------- Owner → host ----------

    public void RequestInvulnerable(float duration)
    {
        if (Offline)
            playerHealth.SetInvulnerable(duration);
        else
            InvulnerableRpc(duration);
    }

    [Rpc(SendTo.Server)]
    private void InvulnerableRpc(float duration) => playerHealth.SetInvulnerable(duration);

    public void ReportCrash(Vector2 point)
    {
        if (Offline)
            Crash(point);
        else
            CrashRpc(point);
    }

    [Rpc(SendTo.Server)]
    private void CrashRpc(Vector2 point) => Crash(point);

    private void Crash(Vector2 point)
    {
        if (playerHealth.LastAttacker != null)
            playerHealth.LastAttacker.OnEnemyCrash(target, null, point);
    }

    public void ReportFell()
    {
        if (Offline)
            respawn.Die(true);
        else
            FellRpc();
    }

    [Rpc(SendTo.Server)]
    private void FellRpc() => respawn.Die(true);

    public void ReportHardLanding(Vector2 feet)
    {
        if (Offline)
            coreBridge.OnHardLandingOnHost(feet);
        else
            HardLandingRpc(feet);
    }

    [Rpc(SendTo.Server)]
    private void HardLandingRpc(Vector2 feet) => coreBridge.OnHardLandingOnHost(feet);

    public void PickCore(CoreType type)
    {
        if (Offline)
            AddCore((int)type);
        else
            PickCoreRpc((int)type);
    }

    [Rpc(SendTo.Server)]
    private void PickCoreRpc(int type) => AddCore(type);

    private void AddCore(int type)
    {
        if (MatchManager.Instance != null)
            MatchManager.Instance.PickDone(this);
        if (inventory.Has((CoreType)type))
            return;
        inventory.Add((CoreType)type); // right away on the host (the list event adds it everywhere else)
        if (!Offline)
            cores.Add(type);
    }

    [Rpc(SendTo.Server)]
    private void ReadyRpc() => ready.Value = true;

    // Owner: nothing to pick (owns every core already).
    public void ReportPickSkipped()
    {
        if (!Offline)
            PickSkippedRpc(); // only a LAN match waits for the pick
    }

    [Rpc(SendTo.Server)]
    private void PickSkippedRpc()
    {
        if (MatchManager.Instance != null)
            MatchManager.Instance.PickDone(this);
    }

    // ---------- Death (host → machines) ----------

    // outcome: see PlayerRespawn.DeathOutcome.
    public void PlayDeath(Vector2 point, int outcome, Vector2 spawn)
    {
        if (Offline)
            respawn.PlayDeath(point, outcome, spawn);
        else
            DeathRpc(point, outcome, spawn);
    }

    [Rpc(SendTo.Everyone)]
    private void DeathRpc(Vector2 point, int outcome, Vector2 spawn) => respawn.PlayDeath(point, outcome, spawn);

    public void ReportRevived()
    {
        if (Offline)
            Revived();
        else
            RevivedRpc();
    }

    [Rpc(SendTo.Server)]
    private void RevivedRpc() => Revived();

    private void Revived()
    {
        Dead = false;
        playerHealth.Refill();
    }

    // ---------- Effects (host → the other machines) ----------

    public void ShowEffect(int effect, Vector2 position, float scale)
    {
        if (!Offline)
            EffectRpc(effect, position, scale); // single player: nobody else to show
    }

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

    // Body color (the skin's, else the slot's) and the skin's accessory.
    private void ApplyColor()
    {
        if (Slot >= 0 || skin.Value >= 0)
            playerHealth.SetBodyColor(Color);
        SkinCatalog.Skin chosen = SkinCatalog.Instance != null ? SkinCatalog.Instance.Get(skin.Value) : null;
        accessory.sprite = chosen != null ? chosen.accessory : null;
    }

    // Host, before spawning the player: the skin it picked (set on the network once spawned).
    public void SetSkinOnHost(int value)
    {
        pendingSkin = value;
    }

    // Somebody in the session already wears that skin.
    public static bool SkinTaken(int value)
    {
        foreach (PlayerNetwork player in All)
            if (player.skin.Value == value)
                return true;
        return false;
    }

    // Each slot starts at its own spawn point of the current map, spread over the list.
    private void PlaceAtSpawnPoint()
    {
        if (placed || Slot < 0)
            return;

        MapManager maps = FindAnyObjectByType<MapManager>();
        if (maps == null || maps.CurrentMap.SpawnPoints.Length == 0)
            return;

        Transform[] points = maps.CurrentMap.SpawnPoints;
        Vector3 position = points[Slot * points.Length / 4 % points.Length].position;
        var rb = GetComponent<Rigidbody2D>();
        rb.position = position;
        transform.position = position;
        rb.linearVelocity = Vector2.zero;
        placed = true;
    }
}
