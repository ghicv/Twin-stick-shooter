using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Card offer: 3 different cores of one rarity (silver, gold or diamond) the player doesn't own yet, two of them
// bullet cores while that rarity has any left. The cards take the rarity's color. Clicking a card gives that core
// to the player. Offers come at milestones (CoreBridge: after some waves; MatchManager: before some LAN rounds, where
// a random card is taken when the time runs out). If the rarity runs short, the nearest rarity fills the offer.
// Every card can be swapped (reroll) once for another core.
public class CoreCards : MonoBehaviour
{
    [System.Serializable]
    private class CardView
    {
        public Button button;
        public Image icon;
        public Text title;
        public Text tag;
        public Text description;

        [Tooltip("The card's border, in its rarity's color.")]
        public Image frame;

        [Tooltip("Soft light behind the card, in its rarity's color.")]
        public Image glow;

        [Tooltip("The badge the icon sits on.")]
        public Image emblem;

        public Text rarityText;

        [Tooltip("Swaps this card for another of the same rarity (Rerolls Per Offer in all).")]
        public Button reroll;
    }

    private CoreBridge cores; // the player who is choosing (this machine's)
    private CoreInventory inventory;

    [Tooltip("Shown while choosing: dims the game and holds the cards.")]
    [SerializeField] private GameObject panel;

    [Tooltip("One per card on screen (3).")]
    [SerializeField] private CardView[] cards;

    [SerializeField] private Color bulletTagColor = new Color(1f, 0.9f, 0.43f);
    [SerializeField] private Color singleTagColor = new Color(0.31f, 0.8f, 0.77f);

    [Tooltip("Card color per rarity: silver, gold, diamond.")]
    [SerializeField] private Color[] rarityColors =
    {
        new Color(0.82f, 0.85f, 0.92f), new Color(1f, 0.8f, 0.32f), new Color(0.5f, 0.95f, 1f)
    };

    [Tooltip("\"CHOOSE A SILVER CARD\" above the cards.")]
    [SerializeField] private Text headerText;


    [Tooltip("How many of the cards are bullet cores (while the offered rarity has that many left).")]
    [Range(0, 3)]
    [SerializeField] private int bulletCards = 2;

    [Tooltip("Diamond borders run through the rainbow at this speed (turns per second).")]
    [SerializeField] private float diamondHueSpeed = 0.35f;

    [SerializeField] private AudioClip pickSound;

    [Range(0f, 1f)]
    [SerializeField] private float pickVolume = 0.6f;

    [Tooltip("Shows the seconds left when the offer has a time limit (LAN). Optional.")]
    [SerializeField] private Text timerText;

    [Tooltip("LAN: the panel's dim background is this see-through, so players can still see the map while choosing.")]
    [Range(0f, 1f)]
    [SerializeField] private float lanDimAlpha = 0.35f;

    private readonly List<CoreType> offered = new List<CoreType>();
    private readonly List<CoreType> spare = new List<CoreType>(); // what rerolls swap in
    private bool[] rerolled = new bool[0]; // every card can be swapped once
    private System.Action onPicked;
    private Image dim;
    private float dimAlpha;
    private float timeLeft; // > 0: a random card is taken when it runs out

    public bool IsOpen => panel.activeSelf;

    public static string RarityName(CoreInventory.Rarity rarity) =>
        rarity == CoreInventory.Rarity.Silver ? Lang.T("SILVER", "BẠC")
        : rarity == CoreInventory.Rarity.Gold ? Lang.T("GOLD", "VÀNG")
        : Lang.T("DIAMOND", "KIM CƯƠNG");

    private void Awake()
    {
        dim = panel.GetComponent<Image>();
        dimAlpha = dim != null ? dim.color.a : 1f;
        panel.SetActive(false);
        for (int i = 0; i < cards.Length; i++)
        {
            int index = i; // captured by the click
            cards[i].button.onClick.AddListener(() => Pick(index));
            cards[i].reroll.onClick.AddListener(() => Reroll(index));
        }
    }

    // Opens an offer of that rarity for that player's cores; onPicked (optional) runs once a card is clicked.
    // Returns false (and never calls onPicked) when the player already owns every core.
    // timeLimit > 0: after that many seconds a random card is taken.
    public bool Show(CoreBridge cores, CoreInventory.Rarity rarity, System.Action onPicked, float timeLimit = 0f)
    {
        timeLeft = timeLimit;
        if (timerText != null)
            timerText.text = "";
        if (dim != null)
            dim.color = new Color(dim.color.r, dim.color.g, dim.color.b, GameMode.IsLan ? lanDimAlpha : dimAlpha);
        this.cores = cores;
        inventory = cores.Inventory;
        ChooseOffer(rarity);
        if (offered.Count == 0)
            return false;

        this.onPicked = onPicked;
        rerolled = new bool[cards.Length];
        if (headerText != null)
        {
            headerText.text = Lang.T("CHOOSE A " + RarityName(rarity) + " CARD", "CHỌN THẺ " + RarityName(rarity));
            headerText.color = rarityColors[(int)rarity];
        }
        for (int i = 0; i < cards.Length; i++)
        {
            cards[i].button.gameObject.SetActive(i < offered.Count);
            if (i < offered.Count)
                ShowCard(i);
        }
        panel.SetActive(true);
        return true;
    }

    private void ShowCard(int index)
    {
        CardView card = cards[index];
        CoreInventory.CoreInfo info = inventory.Info(offered[index]);
        Color rarityColor = rarityColors[(int)info.rarity];
        card.frame.color = rarityColor;
        card.glow.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, card.glow.color.a);
        card.emblem.color = Color.Lerp(rarityColor, Color.black, 0.55f);
        card.icon.sprite = info.icon;
        card.title.text = Lang.T(info.title, info.titleVi);
        card.title.color = rarityColor;
        card.rarityText.text = RarityName(info.rarity);
        card.rarityText.color = rarityColor;
        card.tag.text = Lang.T(info.tag, info.tagVi);
        card.tag.color = info.bulletCore ? bulletTagColor : singleTagColor;
        card.description.text = Lang.T(info.description, info.descriptionVi);
        ShowRerolls();
    }

    private void ShowRerolls()
    {
        for (int i = 0; i < cards.Length; i++)
            cards[i].reroll.interactable = i < rerolled.Length && !rerolled[i] && spare.Count > 0;
    }

    // Swaps one card for the next spare core (same rarity first); every card once.
    private void Reroll(int index)
    {
        if (!panel.activeSelf || spare.Count == 0 || index >= offered.Count || index >= rerolled.Length || rerolled[index])
            return;
        rerolled[index] = true;
        offered[index] = spare[0];
        spare.RemoveAt(0);
        CountOffer(offered[index]);
        SaveOfferCounts();
        ShowCard(index);
        UiAudio.Play(UiAudio.Sound.Click);
    }

    // 3 different cores not owned yet, all of the rarity (the nearest rarity fills in when it runs short),
    // two bullet cores among them when there are. The cores offered least often on this PC come first (random among
    // equals), so the offers go round every core of a rarity before showing the same ones again.
    private void ChooseOffer(CoreInventory.Rarity rarity)
    {
        offered.Clear();
        var pool = new List<CoreType>();
        // The asked rarity first, then the others by how close they are.
        for (int distance = 0; distance < 3 && pool.Count < cards.Length; distance++)
        {
            var ring = new List<CoreType>();
            foreach (CoreInventory.CoreInfo info in inventory.Catalog)
                if (!inventory.Has(info.type) && Mathf.Abs((int)info.rarity - (int)rarity) == distance)
                    ring.Add(info.type);
            Shuffle(ring);
            LeastOfferedFirst(ring);
            pool.AddRange(ring);
        }

        // Bullet cores first (Bullet Cards of them, while the asked rarity has any), then the rest in order.
        for (int slot = 0; slot < bulletCards && slot < pool.Count; slot++)
        {
            int bullet = pool.FindIndex(slot, core => inventory.Info(core).bulletCore && inventory.Info(core).rarity == rarity);
            if (bullet < 0)
                break;
            (pool[slot], pool[bullet]) = (pool[bullet], pool[slot]);
        }
        for (int i = 0; i < pool.Count && offered.Count < cards.Length; i++)
            offered.Add(pool[i]);
        // What a reroll swaps in: the rest of the pool (the asked rarity first).
        spare.Clear();
        for (int i = offered.Count; i < pool.Count; i++)
            spare.Add(pool[i]);
        Shuffle(offered); // the bullet cores aren't always the first cards
        foreach (CoreType core in offered)
            CountOffer(core);
        SaveOfferCounts();
    }

    // ---------- What has been offered (kept on this PC between games) ----------

    private const string OfferCountsKey = "CoreOfferCounts";
    private static Dictionary<CoreType, int> offerCounts;

    private static Dictionary<CoreType, int> OfferCounts
    {
        get
        {
            if (offerCounts != null)
                return offerCounts;
            offerCounts = new Dictionary<CoreType, int>();
            foreach (string entry in PlayerPrefs.GetString(OfferCountsKey, "").Split(';'))
            {
                string[] parts = entry.Split(':');
                if (parts.Length == 2 && System.Enum.TryParse(parts[0], out CoreType core) && int.TryParse(parts[1], out int count))
                    offerCounts[core] = count;
            }
            return offerCounts;
        }
    }

    private static int TimesOffered(CoreType core) => OfferCounts.TryGetValue(core, out int count) ? count : 0;

    private static void CountOffer(CoreType core) => OfferCounts[core] = TimesOffered(core) + 1;

    private static void SaveOfferCounts()
    {
        var text = new System.Text.StringBuilder();
        foreach (var pair in OfferCounts)
            text.Append(pair.Key).Append(':').Append(pair.Value).Append(';');
        PlayerPrefs.SetString(OfferCountsKey, text.ToString());
        PlayerPrefs.Save();
    }

    // The least offered first; the shuffle before keeps equals in a random order (a stable sort).
    private static void LeastOfferedFirst(List<CoreType> cores)
    {
        var ordered = new List<CoreType>(cores);
        cores.Clear();
        for (int times = 0; ordered.Count > 0; times++)
            for (int i = 0; i < ordered.Count; i++)
                if (TimesOffered(ordered[i]) <= times)
                {
                    cores.Add(ordered[i]);
                    ordered.RemoveAt(i--);
                }
    }

    private static void Shuffle(List<CoreType> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private void Update()
    {
        if (!panel.activeSelf)
            return;

        // Diamond cards shimmer through the rainbow (like prismatic augments).
        for (int i = 0; i < offered.Count && i < cards.Length; i++)
        {
            if (inventory.Info(offered[i]).rarity != CoreInventory.Rarity.Diamond)
                continue;
            Color shimmer = Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime * diamondHueSpeed + i * 0.15f, 1f), 0.45f, 1f);
            cards[i].frame.color = shimmer;
            cards[i].glow.color = new Color(shimmer.r, shimmer.g, shimmer.b, cards[i].glow.color.a);
        }

        if (timeLeft <= 0f)
            return;

        timeLeft -= Time.unscaledDeltaTime;
        if (timerText != null)
            timerText.text = Mathf.CeilToInt(Mathf.Max(timeLeft, 0f)).ToString();
        if (timeLeft <= 0f)
            Pick(Random.Range(0, offered.Count)); // out of time: a random card
    }

    private void Pick(int index)
    {
        if (!panel.activeSelf || index >= offered.Count)
            return;

        cores.Pick(offered[index]); // the host adds it and tells every machine
        panel.SetActive(false);
        timeLeft = 0f;
        GameAudio.Play(pickSound, pickVolume);

        System.Action done = onPicked;
        onPicked = null;
        done?.Invoke();
    }
}
