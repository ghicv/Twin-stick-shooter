using Unity.Netcode;
using UnityEngine;

// A health pack the host puts on a map spot (MapProps brings a new one a while after it is taken). A hurt player who
// touches it (noticed by that player's own machine) asks the host for it; the first to ask gets Heal Amount.
public class HealthPack : NetworkBehaviour
{
    [SerializeField] private float healAmount = 35f;

    [Tooltip("The picture, bobbing up and down (stepped).")]
    [SerializeField] private Transform visual;

    [SerializeField] private float bobHeight = 0.125f;
    [SerializeField] private float bobSpeed = 2f;

    [SerializeField] private AudioClip pickupSound;
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.6f;

    private float askedTimer; // this machine already asked (asks again later if it is still here)
    private bool taken;

    private void Update()
    {
        bool up = Mathf.Repeat(Time.time * bobSpeed, 1f) < 0.5f;
        visual.localPosition = new Vector3(0f, up ? bobHeight : 0f, 0f);
        askedTimer -= Time.deltaTime;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        PlayerNetwork player = other.GetComponentInParent<PlayerNetwork>();
        if (player == null || !player.IsMine || player.Dead || player.Health >= player.MaxHealth || askedTimer > 0f || !IsSpawned)
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
        player.GetComponent<PlayerHealth>().Heal(healAmount);
        TakenRpc();
        NetworkObject.Despawn();
    }

    [Rpc(SendTo.Everyone)]
    private void TakenRpc() => GameAudio.PlayAt(pickupSound, transform.position, volume, 0.05f);
}
