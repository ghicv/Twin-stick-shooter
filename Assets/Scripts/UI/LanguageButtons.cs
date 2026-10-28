using UnityEngine;
using UnityEngine.UI;

// Two buttons, ENGLISH and TIẾNG VIỆT, that pick the game's language (Lang). The one in use is bright,
// the other dimmed. Used in the main menu and in the ESC menu.
public class LanguageButtons : MonoBehaviour
{
    [SerializeField] private Button englishButton;
    [SerializeField] private Button vietnameseButton;

    [Tooltip("Label color of the language in use.")]
    [SerializeField] private Color onColor = Color.white;

    [Tooltip("Label color of the other language.")]
    [SerializeField] private Color offColor = new Color(1f, 1f, 1f, 0.35f);

    private void Awake()
    {
        englishButton.onClick.AddListener(() => Lang.Set(Lang.Language.English));
        vietnameseButton.onClick.AddListener(() => Lang.Set(Lang.Language.Vietnamese));
    }

    private void OnEnable()
    {
        Lang.Changed += Show;
        Show();
    }

    private void OnDisable()
    {
        Lang.Changed -= Show;
    }

    private void Show()
    {
        bool vietnamese = Lang.Current == Lang.Language.Vietnamese;
        englishButton.GetComponentInChildren<Text>().color = vietnamese ? offColor : onColor;
        vietnameseButton.GetComponentInChildren<Text>().color = vietnamese ? onColor : offColor;
    }
}
