using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Card offer: shows up to 3 cores the player doesn't own yet, always at least one bullet core while any is left.
// Clicking a card gives that core to the player. CoreBridge opens the offer (between waves, or after dying in a
// LAN round, where a random card is taken when the time runs out).
[RequireComponent(typeof(AudioSource))]
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
    }

    private CoreBridge cores; // the player who is choosing (this machine's)
    private CoreInventory inventory;

    [Tooltip("Shown while choosing: dims the game and holds the cards.")]
    [SerializeField] private GameObject panel;

    [Tooltip("One per card on screen (3).")]
    [SerializeField] private CardView[] cards;

    [SerializeField] private Color bulletTagColor = new Color(1f, 0.9f, 0.43f);
    [SerializeField] private Color singleTagColor = new Color(0.31f, 0.8f, 0.77f);

    [SerializeField] private AudioClip pickSound;

    [Range(0f, 1f)]
    [SerializeField] private float pickVolume = 0.6f;

    private readonly List<CoreType> offered = new List<CoreType>();
    [Tooltip("Shows the seconds left when the offer has a time limit (LAN). Optional.")]
    [SerializeField] private Text timerText;

    [Tooltip("LAN: the panel's dim background is this see-through, so a dead player can watch the fight while choosing.")]
    [Range(0f, 1f)]
    [SerializeField] private float lanDimAlpha = 0.35f;

    private AudioSource audioSource;
    private System.Action onPicked;
    private Image dim;
    private float dimAlpha;
    private float timeLeft; // > 0: a random card is taken when it runs out

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        dim = panel.GetComponent<Image>();
        dimAlpha = dim != null ? dim.color.a : 1f;
        panel.SetActive(false);
        for (int i = 0; i < cards.Length; i++)
        {
            int index = i; // captured by the click
            cards[i].button.onClick.AddListener(() => Pick(index));
        }
    }

    // Opens an offer for that player's cores; onPicked (optional) runs once a card is clicked.
    // Returns false (and never calls onPicked) when the player already owns every core.
    // timeLimit > 0: after that many seconds a random card is taken.
    public bool Show(CoreBridge cores, System.Action onPicked, float timeLimit = 0f)
    {
        timeLeft = timeLimit;
        if (timerText != null)
            timerText.text = "";
        if (dim != null)
            dim.color = new Color(dim.color.r, dim.color.g, dim.color.b, GameMode.IsLan ? lanDimAlpha : dimAlpha);
        this.cores = cores;
        inventory = cores.Inventory;
        ChooseOffer();
        if (offered.Count == 0)
            return false;

        this.onPicked = onPicked;
        for (int i = 0; i < cards.Length; i++)
        {
            CardView card = cards[i];
            card.button.gameObject.SetActive(i < offered.Count);
            if (i >= offered.Count)
                continue;

            CoreInventory.CoreInfo info = inventory.Info(offered[i]);
            card.icon.sprite = info.icon;
            card.title.text = Lang.T(info.title, info.titleVi);
            card.tag.text = Lang.T(info.tag, info.tagVi);
            card.tag.color = info.bulletCore ? bulletTagColor : singleTagColor;
            card.description.text = Lang.T(info.description, info.descriptionVi);
        }
        panel.SetActive(true);
        return true;
    }

    // Up to one card per view, never an owned core: one bullet core for sure, the rest from everything left.
    private void ChooseOffer()
    {
        offered.Clear();
        var bulletCores = new List<CoreType>();
        var allCores = new List<CoreType>();
        foreach (CoreInventory.CoreInfo info in inventory.Catalog)
        {
            if (inventory.Has(info.type))
                continue;
            allCores.Add(info.type);
            if (info.bulletCore)
                bulletCores.Add(info.type);
        }

        if (bulletCores.Count > 0)
        {
            CoreType sure = bulletCores[Random.Range(0, bulletCores.Count)];
            offered.Add(sure);
            allCores.Remove(sure);
        }
        while (offered.Count < cards.Length && allCores.Count > 0)
        {
            int index = Random.Range(0, allCores.Count);
            offered.Add(allCores[index]);
            allCores.RemoveAt(index);
        }

        // Shuffle, so the sure bullet core isn't always the first card.
        for (int i = offered.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (offered[i], offered[j]) = (offered[j], offered[i]);
        }
    }

    private void Update()
    {
        if (timeLeft <= 0f || !panel.activeSelf)
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
        if (pickSound != null)
            audioSource.PlayOneShot(pickSound, pickVolume);

        System.Action done = onPicked;
        onPicked = null;
        done?.Invoke();
    }
}
