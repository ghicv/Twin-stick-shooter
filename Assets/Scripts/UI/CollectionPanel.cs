using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Main menu: COLLECTION shows every core in the game. Left: a scrolling grid of tiles (sorted silver, gold, diamond), with
// tabs to show one rarity only. Right: the core under the mouse (or the last one clicked) as a big card, like the
// card offer, with TRY under it (single player with that core, against the dummies). Clicking a tile selects it: the
// card keeps it while the mouse passes over other tiles (they show only while pointed at). ESC or BACK closes it.
public class CollectionPanel : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Button backButton;

    [Tooltip("TRY: play single player with the shown core, shooting the dummies (MainMenu listens to Try).")]
    [SerializeField] private Button tryButton;

    [Tooltip("Hidden while the collection is open (the title screen behind it).")]
    [SerializeField] private GameObject hideWhileOpen;

    [Tooltip("The cores come from here (the player prefab's CoreInventory).")]
    [SerializeField] private CoreInventory catalogSource;

    [Tooltip("One tile per core is made from this (kept off itself), under the same parent. Its child Frame is " +
             "tinted by rarity and its child Icon shows the core.")]
    [SerializeField] private Button tileTemplate;

    [Tooltip("ALL, SILVER, GOLD, DIAMOND.")]
    [SerializeField] private Button[] filterButtons;

    [Tooltip("\"38 CORES\" under the tabs.")]
    [SerializeField] private Text countText;

    [Header("Big card")]
    [SerializeField] private Image cardFrame;
    [SerializeField] private Image cardGlow;
    [SerializeField] private Image cardEmblem;
    [SerializeField] private Image cardIcon;
    [SerializeField] private Text cardRarity;
    [SerializeField] private Text cardTitle;
    [SerializeField] private Text cardTag;
    [SerializeField] private Text cardDescription;

    [Header("Look")]
    [Tooltip("Silver, gold, diamond (the same as the card offer).")]
    [SerializeField] private Color[] rarityColors =
    {
        new Color(0.82f, 0.85f, 0.92f), new Color(1f, 0.8f, 0.32f), new Color(0.5f, 0.95f, 1f)
    };

    [SerializeField] private Color bulletTagColor = new Color(1f, 0.9f, 0.43f);
    [SerializeField] private Color singleTagColor = new Color(0.31f, 0.8f, 0.77f);
    [SerializeField] private Color tabOnColor = new Color(1f, 0.902f, 0.427f);
    [SerializeField] private Color tabOffColor = new Color(1f, 1f, 1f, 0.45f);

    [Tooltip("Diamond frames run through the rainbow at this speed (turns per second).")]
    [SerializeField] private float diamondHueSpeed = 0.35f;

    private class Tile
    {
        public CoreInventory.CoreInfo info;
        public GameObject root;
        public Button button;
        public Image frame;
        public Image mark; // the blinking white frame of the selected tile
    }

    private readonly List<Tile> tiles = new List<Tile>();
    private CoreInventory.CoreInfo shown;
    private Tile selected; // clicked: the card comes back to it after pointing at others, TRY uses it

    public bool IsOpen => panel.activeSelf;

    // TRY was pressed on this core.
    public event System.Action<CoreType> Try;

    private void Awake()
    {
        tileTemplate.gameObject.SetActive(false);
        backButton.onClick.AddListener(Close);
        tryButton.onClick.AddListener(() =>
        {
            if (selected != null)
                Try?.Invoke(selected.info.type);
        });
        for (int i = 0; i < filterButtons.Length; i++)
        {
            int rarity = i - 1; // captured by the click
            filterButtons[i].onClick.AddListener(() => SetFilter(rarity));
        }
        Lang.Changed += ShowCard;
    }

    private void OnDestroy()
    {
        Lang.Changed -= ShowCard;
    }

    public void Open()
    {
        if (tiles.Count == 0)
            MakeTiles();
        panel.SetActive(true);
        hideWhileOpen.SetActive(false);
        SetFilter(-1);
    }

    // Opened on that core (back from trying it).
    public void Open(CoreType type)
    {
        Open();
        foreach (Tile tile in tiles)
            if (tile.info.type == type)
                Select(tile);
    }

    public void Close()
    {
        panel.SetActive(false);
        hideWhileOpen.SetActive(true);
    }

    private void Update()
    {
        if (!panel.activeSelf)
            return;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Close();
            return;
        }

        Color rainbow = Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime * diamondHueSpeed, 1f), 0.45f, 1f);
        foreach (Tile tile in tiles)
            if (tile.info.rarity == CoreInventory.Rarity.Diamond)
                tile.frame.color = rainbow;
        if (shown != null && shown.rarity == CoreInventory.Rarity.Diamond)
            cardFrame.color = rainbow;
        if (selected != null)
            selected.mark.color = Mathf.Repeat(Time.unscaledTime * 2f, 1f) < 0.5f ? Color.white : new Color(1f, 1f, 1f, 0.45f); // blinks
    }

    private void MakeTiles()
    {
        // Only the template may be there (tiles left in the scene by an editor rebuild would show twice).
        foreach (Transform child in tileTemplate.transform.parent)
            if (child != tileTemplate.transform)
                Destroy(child.gameObject);

        var sorted = new List<CoreInventory.CoreInfo>(catalogSource.Catalog);
        // Stable sort by rarity (the catalog's own order inside each rarity).
        var order = new Dictionary<CoreInventory.CoreInfo, int>();
        for (int i = 0; i < sorted.Count; i++)
            order[sorted[i]] = i;
        sorted.Sort((a, b) => a.rarity != b.rarity ? a.rarity.CompareTo(b.rarity) : order[a].CompareTo(order[b]));

        foreach (CoreInventory.CoreInfo info in sorted)
        {
            Button button = Instantiate(tileTemplate, tileTemplate.transform.parent);
            button.name = info.type.ToString();
            button.gameObject.SetActive(true);
            var tile = new Tile
            {
                info = info,
                root = button.gameObject,
                button = button,
                frame = button.transform.Find("Frame").GetComponent<Image>(),
            };
            tile.frame.color = rarityColors[(int)info.rarity];
            tile.mark = Instantiate(tile.frame, button.transform); // a white frame just outside the tile's own
            tile.mark.name = "Selected";
            tile.mark.rectTransform.sizeDelta += new Vector2(10f, 10f);
            tile.mark.raycastTarget = false;
            tile.mark.gameObject.SetActive(false);
            button.transform.Find("Icon").GetComponent<Image>().sprite = info.icon;
            button.onClick.AddListener(() => Select(tile));
            // Pointing at a tile shows it for a moment; leaving it shows the selected one again.
            var trigger = button.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var enter = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => Show(info));
            trigger.triggers.Add(enter);
            var exit = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => Show(selected != null ? selected.info : info));
            trigger.triggers.Add(exit);
            tiles.Add(tile);
        }
    }

    private void SetFilter(int rarity)
    {
        Tile first = null;
        foreach (Tile tile in tiles)
        {
            bool visible = rarity < 0 || (int)tile.info.rarity == rarity;
            tile.root.SetActive(visible);
            if (visible && first == null)
                first = tile;
        }
        for (int i = 0; i < filterButtons.Length; i++)
            filterButtons[i].GetComponentInChildren<Text>().color = i - 1 == rarity ? tabOnColor : tabOffColor;
        ScrollRect scroll = tileTemplate.GetComponentInParent<ScrollRect>(true);
        if (scroll != null)
            scroll.verticalNormalizedPosition = 1f; // back to the top
        Select(first);
    }

    // Clicked: this core stays on the card (and is the one TRY plays); its tile stands out.
    private void Select(Tile tile)
    {
        selected = tile;
        foreach (Tile other in tiles)
        {
            bool on = other == tile;
            other.mark.gameObject.SetActive(on);
            ColorBlock colors = other.button.colors;
            colors.normalColor = on ? Color.white : new Color(0.6f, 0.6f, 0.6f);
            colors.selectedColor = colors.normalColor;
            other.button.colors = colors;
        }
        Show(tile != null ? tile.info : null);
    }

    private void Show(CoreInventory.CoreInfo info)
    {
        shown = info;
        ShowCard();
    }

    private void ShowCard()
    {
        if (shown == null)
            return;
        Color color = rarityColors[(int)shown.rarity];
        cardFrame.color = color;
        cardGlow.color = new Color(color.r, color.g, color.b, cardGlow.color.a);
        cardEmblem.color = Color.Lerp(color, Color.black, 0.55f);
        cardIcon.sprite = shown.icon;
        cardRarity.text = CoreCards.RarityName(shown.rarity);
        cardRarity.color = color;
        cardTitle.text = Lang.T(shown.title, shown.titleVi);
        cardTitle.color = color;
        cardTag.text = Lang.T(shown.tag, shown.tagVi);
        cardTag.color = shown.bulletCore ? bulletTagColor : singleTagColor;
        cardDescription.text = Lang.T(shown.description, shown.descriptionVi);

        int count = 0;
        foreach (Tile tile in tiles)
            if (tile.root.activeSelf)
                count++;
        countText.text = Lang.T(count + " CORES", count + " LÕI");
    }
}
