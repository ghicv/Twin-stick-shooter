using UnityEngine;

// This PC's player: the name typed in the main menu and the look made in the character screen, both remembered.
// Every machine sends them when it joins a room; the host cleans the name up again (Clean) before showing it.
public static class PlayerProfile
{
    public const int MaxNameLength = 12;

    private const string NameKey = "PlayerName";
    private const string LookKey = "PlayerLook";

    // The saved look (a random one the first time).
    public static Look Look
    {
        get
        {
            if (PlayerPrefs.HasKey(LookKey))
                return Look.Unpack(PlayerPrefs.GetInt(LookKey));
            Look first = Wardrobe.Instance != null ? Wardrobe.Instance.Random() : new Look(0, 0, 0);
            PlayerPrefs.SetInt(LookKey, first.Pack());
            return first;
        }
        set => PlayerPrefs.SetInt(LookKey, value.Pack());
    }

    public static string Name
    {
        get
        {
            string saved = Clean(PlayerPrefs.GetString(NameKey, ""));
            if (saved.Length == 0)
            {
                saved = "PLAYER" + Random.Range(10, 100); // a first name until one is typed
                PlayerPrefs.SetString(NameKey, saved);
            }
            return saved;
        }
        set => PlayerPrefs.SetString(NameKey, Clean(value));
    }

    public const int MaxRoomNameLength = 24;

    // Upper case, no line breaks, "|" (room announcements use it) or rich text, at most Max Length letters.
    public static string Clean(string name, int maxLength = MaxNameLength)
    {
        if (string.IsNullOrEmpty(name))
            return "";
        name = name.Replace("|", "").Replace("\n", "").Replace("\r", "").Replace("<", "").Replace(">", "").Trim().ToUpperInvariant();
        return name.Length > maxLength ? name.Substring(0, maxLength) : name;
    }
}
