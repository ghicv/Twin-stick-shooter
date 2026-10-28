using Unity.Netcode;
using UnityEngine;

// A crate that drops from the sky during a round (MapProps). The host decides what is inside when it drops: one of
// the special guns (PlayerWeapon's Special Guns) or a shield; its picture (the gun, or the shield) is shown on the
// crate on every machine. The first player to touch it (noticed by that player's own machine) gets it. Real physics
// on the host.
[RequireComponent(typeof(Rigidbody2D))]
public class WeaponCrate : NetworkBehaviour
{
    public const int Shield = -1; // the crate's content: a shield instead of a gun

    [SerializeField] private AudioClip pickupSound;
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.6f;

    [Tooltip("The picture of what is inside, floating just above the crate.")]
    [SerializeField] private SpriteRenderer icon;

    [SerializeField] private Sprite shieldIcon;

    [Tooltip("The icon bobs up and down a little (stepped).")]
    [SerializeField] private float bobHeight = 0.0625f;

    private readonly NetworkVariable<int> content = new NetworkVariable<int>(0); // 1.. = a special gun, -1 = shield
    private float askedTimer;
    private bool taken;
    private MapManager maps;
    private int shownContent;
    private Vector3 iconBase;

    // Host, before spawning it: what is inside.
    public int Content { set => content.Value = value; }

    private void Awake()
    {
        maps = FindAnyObjectByType<MapManager>();
        iconBase = icon.transform.localPosition;
    }

    private void Update()
    {
        askedTimer -= Time.deltaTime;
        if (content.Value != shownContent)
            ShowContent();
        // Above the crate and upright, however the crate tumbles.
        float bob = Mathf.Repeat(Time.time * 2f, 1f) < 0.5f ? bobHeight : 0f;
        icon.transform.SetPositionAndRotation(transform.position + Vector3.up * (iconBase.y + bob), Quaternion.identity);
    }

    private void ShowContent()
    {
        shownContent = content.Value;
        if (shownContent == Shield)
        {
            icon.sprite = shieldIcon;
            return;
        }
        PlayerWeapon weapon = FindAnyObjectByType<PlayerWeapon>();
        icon.sprite = weapon != null ? weapon.GunSprite(shownContent) : null;
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
        if (content.Value == Shield)
            player.GiveShield();
        else
            player.GiveGun(content.Value > 0 ? content.Value : Random.Range(1, player.GetComponent<PlayerWeapon>().SpecialGunCount + 1));
        TakenRpc();
        NetworkObject.Despawn();
    }

    [Rpc(SendTo.Everyone)]
    private void TakenRpc() => GameAudio.PlayAt(pickupSound, transform.position, volume, 0.05f);
}
