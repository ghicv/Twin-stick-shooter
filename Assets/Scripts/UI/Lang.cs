using UnityEngine;

// The game's language: English or Vietnamese, picked in the main menu and remembered on this PC.
// Texts made in code: Lang.T("ENGLISH TEXT", "CHỮ TIẾNG VIỆT"). Fixed texts in the scene: a LocalizedText on the Text.
public static class Lang
{
    public enum Language { English = 0, Vietnamese = 1 }

    private const string Key = "Language";

    private static Language current;
    private static bool loaded;

    // Fixed texts listen to this to switch right away.
    public static event System.Action Changed;

    public static Language Current
    {
        get
        {
            if (!loaded)
            {
                current = (Language)PlayerPrefs.GetInt(Key, (int)Language.English);
                loaded = true;
            }
            return current;
        }
    }

    public static void Set(Language language)
    {
        current = language;
        loaded = true;
        PlayerPrefs.SetInt(Key, (int)language);
        Changed?.Invoke();
    }

    // The text in the current language.
    public static string T(string english, string vietnamese) => Current == Language.Vietnamese ? vietnamese : english;
}
