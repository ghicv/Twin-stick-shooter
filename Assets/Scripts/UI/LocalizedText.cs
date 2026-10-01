using UnityEngine;
using UnityEngine.UI;

// A fixed text in the scene (a button label, a title) in both languages. Put it next to the Text;
// it shows the current language and switches as soon as another one is picked (Lang).
[RequireComponent(typeof(Text))]
public class LocalizedText : MonoBehaviour
{
    [TextArea(1, 3)]
    [SerializeField] private string english;

    [TextArea(1, 3)]
    [SerializeField] private string vietnamese;

    private void OnEnable()
    {
        Lang.Changed += Apply;
        Apply();
    }

    private void OnDisable()
    {
        Lang.Changed -= Apply;
    }

    private void Apply()
    {
        GetComponent<Text>().text = Lang.T(english, vietnamese);
    }
}
