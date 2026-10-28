using Unity.Netcode;
using UnityEngine;

// A crate that drops from the sky during a round (MapProps). The first player to touch it (noticed by that player's
// own machine) gets a random special gun with its own ammo (PlayerWeapon's Special Guns). Real physics on the host.
[RequireComponent(typeof(Rigidbody2D))]
public class WeaponCrate : NetworkBehaviour
{
    [SerializeField] private AudioClip pickupSound;
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.6f;

    private float askedTimer;
    private bool taken;
    private MapManager maps;

    private void Awake()
    {
        maps = FindAnyObjectByType<MapManager>();
    }

    private void Update()
    {
        askedTimer -= Time.deltaTime;
    }

    private void FixedUpdate()
    {
        if (IsServer && IsSpawned && transform.position.y < maps.CurrentMap.FallLimitY)
            NetworkObject.Despawn();
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        PlayerNetwork player = other.GetComponentInParent<PlayerNetwork>();
        if (player == null || !player.IsMine || player.Dead || askedTimer > 0f || !IsSpawned)
            return;
        askedTimer = 0.5f;
        TakeRpc();
    }

    [Rpc(SendTo.Server)]
    private void TakeRpc(RpcParams rpcParams = default)
    {
        if (taken)
            return;
        PlayerNetwork player = PlayerNetwork.All.Find(p => p.OwnerClientId == rpcParams.Receive.SenderClientId);
        if (player == null || player.Dead)
            return;
        taken = true;
        player.GiveGun(Random.Range(1, player.GetComponent<PlayerWeapon>().SpecialGunCount + 1));
        TakenRpc();
        NetworkObject.Despawn();
    }

    [Rpc(SendTo.Everyone)]
    private void TakenRpc() => GameAudio.PlayAt(pickupSound, transform.position, volume, 0.05f);
}
