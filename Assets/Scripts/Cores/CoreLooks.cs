using UnityEngine;

// What some cores look like on the player, on every machine (the cores' lists are the same everywhere):
// Hot Barrel: the gun reddens as it fires fast and steams. Adrenaline: below its health line, a red aura beats around
// the player (with a heartbeat for the player itself). Thick Skin: a grey armor outline; hits clink.
public class CoreLooks : MonoBehaviour
{
    [SerializeField] private Transform firePoint;
    [SerializeField] private SpriteRenderer gunSprite;
    [SerializeField] private SpriteRenderer body;

    [Tooltip("A soft ring (the Adrenaline aura).")]
    [SerializeField] private Sprite auraSprite;

    [SerializeField] private AudioClip heartbeatSound;
    [SerializeField] private AudioClip clinkSound;

    private PlayerNetwork net;
    private PlayerSword sword;
    private SpriteRenderer aura;
    private SpriteRenderer armor;
    private float heat;            // Hot Barrel: 0 cold .. 1 red hot
    private float steamTimer;
    private float heartTimer;
    private Color gunOwn = Color.white, gunSet = Color.white;

    private void Awake()
    {
        net = GetComponent<PlayerNetwork>();
        sword = GetComponent<PlayerSword>();

        aura = Part("AdrenalineAura", auraSprite, transform, -1);
        armor = Part("ArmorOutline", body.sprite, body.transform, body.sortingOrder - 1);
        armor.color = new Color(0.62f, 0.68f, 0.78f);
        armor.transform.localScale = new Vector3(1.18f, 1.1f, 1f);
    }

    private SpriteRenderer Part(string name, Sprite sprite, Transform parent, int order)
    {
        var part = new GameObject(name).AddComponent<SpriteRenderer>();
        part.transform.SetParent(parent, false);
        part.sprite = sprite;
        part.sortingOrder = order;
        part.enabled = false;
        return part;
    }

    private bool Has(CoreType type) => net != null && net.Cores != null && net.Cores.Inventory.Has(type);

    // Every machine: this player fired (PlayerWeapon's shot feedback).
    public void OnShot()
    {
        heat = Mathf.Min(1f, heat + 0.12f);
    }

    // Every machine: this player was hit (PlayerHealth.PlayHurt).
    public void OnHurt()
    {
        if (Has(CoreType.ThickSkin))
        {
            GameAudio.PlayAt(clinkSound, transform.position, 0.35f, 0.15f);
            CoreEffects.Burst(transform.position, Color.white, 5, 6f, default, 0.07f);
        }
    }

    private void LateUpdate()
    {
        bool inPlay = net == null || net.InPlay;
        bool swordOut = sword != null && sword.Active;
        float dt = Time.deltaTime;

        // Hot Barrel: red hot while firing fast, cooling down, steaming when very hot.
        heat = Mathf.Max(0f, heat - dt * 0.6f);
        if (gunSprite.color != gunSet)
            gunOwn = gunSprite.color; // the gun's own color changed (a special gun)
        float hot = Has(CoreType.HotBarrel) && !swordOut ? heat : 0f;
        gunSet = Color.Lerp(gunOwn, new Color(1f, 0.3f, 0.15f, gunOwn.a), hot);
        gunSprite.color = gunSet;
        steamTimer -= dt;
        if (hot > 0.55f && inPlay && steamTimer <= 0f)
        {
            steamTimer = 0.09f;
            FxSpeck.Spawn(CoreEffects.Speck, new Color(0.85f, 0.85f, 0.9f, 0.7f), firePoint.position,
                          new Vector2(Random.Range(-0.3f, 0.3f), 1.5f), -0.5f, 0.5f, 0.1f, 0f, null, 0.25f);
        }

        // Adrenaline: a red aura beating like a heart (the player itself hears it).
        bool pumped = inPlay && Has(CoreType.Adrenaline) && net.Health < net.MaxHealth * 0.35f;
        aura.enabled = pumped;
        if (pumped)
        {
            float beat = Mathf.Repeat(Time.time * 1.4f, 1f);
            bool big = beat < 0.12f || (beat > 0.22f && beat < 0.32f); // lub-dub
            aura.transform.localScale = Vector3.one * (big ? 0.95f : 0.85f); // the sprite is 2 units wide: hugs the body
            aura.color = new Color(1f, 0.15f, 0.2f, big ? 0.35f : 0.15f);
            heartTimer -= dt;
            if (net.IsMine && heartTimer <= 0f)
            {
                heartTimer = 1f / 1.4f;
                GameAudio.Play(heartbeatSound, 0.5f, 0f);
            }
        }

        // Thick Skin: the armor outline.
        armor.enabled = inPlay && Has(CoreType.ThickSkin) && body.enabled;
    }
}
