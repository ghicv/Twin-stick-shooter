using UnityEngine;
using UnityEngine.UI;

// The character screen, right after hosting or joining a LAN room: a big preview and three rows (hat, outfit,
// skin color), each with left / right arrows, plus RANDOM and the name (it must differ from the others in the room). ENTER ROOM asks the host to spawn the player in the lobby
// with this look. The look is saved on this PC, so next time it starts from the last one.
public class CharacterPanel : MonoBehaviour
{
    [System.Serializable]
    private class Row
    {
        public Button previous;
        public Button next;
        public Text value;
    }

    [SerializeField] private GameObject panel;

    [SerializeField] private Row hatRow;
    [SerializeField] private Row outfitRow;
    [SerializeField] private Row colorRow;

    [Header("Preview")]
    [SerializeField] private RectTransform preview;
    [SerializeField] private Image previewBody;
    [SerializeField] private Image previewOutfit;
    [SerializeField] private Image previewHat;

    [Tooltip("The player's name (the one from the main menu); a name somebody in the room already has must change.")]
    [SerializeField] private InputField nameInput;

    [SerializeField] private Button randomButton;
    [SerializeField] private Button enterButton;
    [SerializeField] private Text statusText;

    private enum Part { Hat, Outfit, Color }

    private Look look;
    private bool waiting; // asked the host to enter; no answer yet
    private Vector2 previewBase;

    private void Awake()
    {
        panel.SetActive(false);
        previewBase = preview.anchoredPosition;
        hatRow.previous.onClick.AddListener(() => Step(Part.Hat, -1));
        hatRow.next.onClick.AddListener(() => Step(Part.Hat, 1));
        outfitRow.previous.onClick.AddListener(() => Step(Part.Outfit, -1));
        outfitRow.next.onClick.AddListener(() => Step(Part.Outfit, 1));
        colorRow.previous.onClick.AddListener(() => Step(Part.Color, -1));
        colorRow.next.onClick.AddListener(() => Step(Part.Color, 1));
        randomButton.onClick.AddListener(() =>
        {
            look = Wardrobe.Instance.Random();
            Refresh();
        });
        enterButton.onClick.AddListener(Enter);
        nameInput.characterLimit = PlayerProfile.MaxNameLength;
        Lang.Changed += Refresh;
    }

    private void OnDestroy()
    {
        Lang.Changed -= Refresh;
    }

    public void Show()
    {
        look = PlayerProfile.Look;
        nameInput.text = PlayerProfile.Name;
        waiting = false;
        statusText.text = "";
        panel.SetActive(true);
        Refresh();
    }

    // Saves the look and asks the host for the player (also used by the test bot).
    public void Enter()
    {
        MatchManager match = MatchManager.Instance;
        if (waiting || match == null || !match.IsSpawned)
            return;
        PlayerProfile.Look = look;
        PlayerProfile.Name = nameInput.text;
        nameInput.text = PlayerProfile.Name; // cleaned (or a new one if left empty)
        waiting = true;
        statusText.text = Lang.T("ENTERING THE ROOM...", "ĐANG VÀO PHÒNG...");
        match.EnterWithLook();
    }

    // The host said someone in the room already has this name.
    public void NameTaken()
    {
        waiting = false;
        statusText.text = Lang.T("SOMEONE IN THE ROOM HAS THIS NAME - PLEASE CHANGE IT",
                                 "TÊN NÀY ĐÃ CÓ NGƯỜI DÙNG TRONG PHÒNG - HÃY ĐỔI TÊN KHÁC");
        nameInput.ActivateInputField();
    }

    private void Step(Part part, int direction)
    {
        Wardrobe wardrobe = Wardrobe.Instance;
        if (part == Part.Hat)
            look.hat = Wrap(look.hat + direction, wardrobe.HatCount);
        else if (part == Part.Outfit)
            look.outfit = Wrap(look.outfit + direction, wardrobe.OutfitCount);
        else
            look.color = Wrap(look.color + direction, wardrobe.ColorCount);
        Refresh();
    }

    private static int Wrap(int value, int count) => (value % count + count) % count;

    private void Refresh()
    {
        if (!panel.activeSelf)
            return;
        Wardrobe wardrobe = Wardrobe.Instance;
        Wardrobe.Item hat = wardrobe.Hat(look.hat);
        Wardrobe.Item outfit = wardrobe.Outfit(look.outfit);
        Wardrobe.SkinColor color = wardrobe.ColorAt(look.color);

        hatRow.value.text = hat.Name;
        outfitRow.value.text = outfit.Name;
        colorRow.value.text = color.Name;
        colorRow.value.color = color.color;

        previewBody.color = color.color;
        previewOutfit.sprite = outfit.sprite;
        previewOutfit.enabled = outfit.sprite != null;
        previewHat.sprite = hat.sprite;
        previewHat.enabled = hat.sprite != null;
    }

    private void Update()
    {
        if (!panel.activeSelf)
            return;
        if (PlayerNetwork.Local != null)
        {
            panel.SetActive(false); // in the lobby now
            return;
        }

        MatchManager match = MatchManager.Instance;
        enterButton.interactable = match != null && match.IsSpawned && !waiting;

        // The preview bounces a little, like the fighters waiting for a match.
        float bounce = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.4f)) * 14f;
        preview.anchoredPosition = previewBase + new Vector2(0f, bounce);
    }
}
