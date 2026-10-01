using UnityEngine;
using UnityEngine.UI;

// LAN: right after hosting or joining, the player picks a skin here; the host then spawns the player in the lobby.
// Skins other players already wear can't be picked. The panel closes once this machine's player exists.
public class SkinPanel : MonoBehaviour
{
    [System.Serializable]
    private class SkinButton
    {
        public Button button;
        public Image body;
        public Image accessory;
        public Text label;
    }

    [SerializeField] private GameObject panel;

    [Tooltip("One per skin, in the catalog's order.")]
    [SerializeField] private SkinButton[] buttons;

    [SerializeField] private Text statusText;

    private bool waiting; // a skin was asked for; the host hasn't answered yet

    private void Awake()
    {
        panel.SetActive(false);
        for (int i = 0; i < buttons.Length; i++)
        {
            int skin = i; // captured by the click
            buttons[i].button.onClick.AddListener(() => Choose(skin));
        }
    }

    public void Show()
    {
        SkinCatalog catalog = SkinCatalog.Instance;
        for (int i = 0; i < buttons.Length; i++)
        {
            SkinCatalog.Skin skin = catalog.Get(i);
            buttons[i].button.gameObject.SetActive(skin != null);
            if (skin == null)
                continue;
            buttons[i].body.color = skin.color;
            buttons[i].accessory.sprite = skin.accessory;
            buttons[i].accessory.enabled = skin.accessory != null;
        }
        waiting = false;
        statusText.text = "";
        panel.SetActive(true);
    }

    // The host said no (someone took it first): pick again.
    public void Refused()
    {
        waiting = false;
        statusText.text = Lang.T("THAT SKIN WAS JUST TAKEN", "SKIN NÀY VỪA CÓ NGƯỜI CHỌN");
    }

    private void Choose(int skin)
    {
        MatchManager match = MatchManager.Instance;
        if (waiting || match == null || !match.IsSpawned)
            return;
        waiting = true;
        statusText.text = Lang.T("JOINING...", "ĐANG VÀO...");
        match.ChooseSkin(skin);
    }

    private void Update()
    {
        if (!panel.activeSelf)
            return;
        if (PlayerNetwork.Local != null)
        {
            panel.SetActive(false); // spawned
            return;
        }

        // Skins worn by the others can't be picked (also not before the match info has arrived).
        bool ready = MatchManager.Instance != null && MatchManager.Instance.IsSpawned;
        SkinCatalog catalog = SkinCatalog.Instance;
        for (int i = 0; i < buttons.Length; i++)
        {
            if (catalog.Get(i) == null)
                continue;
            bool taken = PlayerNetwork.SkinTaken(i);
            buttons[i].button.interactable = ready && !taken && !waiting;
            buttons[i].label.text = taken ? Lang.T("TAKEN", "ĐÃ CHỌN") : catalog.Get(i).name;
        }
    }
}
