using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// LAN: one row per player (P1-P4 in their colors): round wins and the cores they own (small icons), so everybody
// can see the other players' builds. Hidden in single player.
public class Scoreboard : MonoBehaviour
{
    [System.Serializable]
    private class Row
    {
        public GameObject root;
        public Text label;

        [Tooltip("The player's core icons are lined up here, from the left.")]
        public RectTransform icons;

        [HideInInspector] public List<Image> iconImages = new List<Image>();
    }

    [Tooltip("One row per slot (P1-P4).")]
    [SerializeField] private Row[] rows;

    [Tooltip("Copied once per core. Keep it disabled.")]
    [SerializeField] private Image iconTemplate;

    [SerializeField] private float iconSpacing = 26f;

    [Tooltip("At most this many icons per row (the rest is written as +N).")]
    [SerializeField] private int maxIcons = 14;

    private void Awake()
    {
        iconTemplate.gameObject.SetActive(false);
        foreach (Row row in rows)
            row.root.SetActive(false);
    }

    private void Update()
    {
        MatchManager match = MatchManager.Instance;
        for (int slot = 0; slot < rows.Length; slot++)
        {
            Row row = rows[slot];
            PlayerNetwork player = GameMode.IsLan ? Find(slot) : null;
            row.root.SetActive(player != null);
            if (player == null)
                continue;

            int wins = match != null ? match.Wins(slot) : 0;
            int cores = player.Cores.OwnedCount;
            string you = player == PlayerNetwork.Local ? Lang.T(" (YOU)", " (BẠN)") : "";
            string more = cores > maxIcons ? "  +" + (cores - maxIcons) : "";
            string dead = player.Dead ? "  <color=#FF5555>X</color>" : "";
            row.label.text = "<color=#" + ColorUtility.ToHtmlStringRGB(player.Color) + ">P" + (slot + 1) + "</color>" + you +
                             "  " + wins + "W" + dead + more;
            UpdateIcons(row, player.Cores);
        }
    }

    private static PlayerNetwork Find(int slot)
    {
        foreach (PlayerNetwork player in PlayerNetwork.All)
            if (player.Slot == slot)
                return player;
        return null;
    }

    private void UpdateIcons(Row row, CoreBridge cores)
    {
        int shown = Mathf.Min(cores.OwnedCount, maxIcons);
        if (row.iconImages.Count == shown)
            return;

        foreach (Image old in row.iconImages)
            Destroy(old.gameObject);
        row.iconImages.Clear();
        for (int i = 0; i < shown; i++)
        {
            Image icon = Instantiate(iconTemplate, row.icons);
            icon.sprite = cores.OwnedIcon(i);
            icon.rectTransform.anchoredPosition = new Vector2(i * iconSpacing, 0f);
            icon.gameObject.SetActive(true);
            row.iconImages.Add(icon);
        }
    }
}
