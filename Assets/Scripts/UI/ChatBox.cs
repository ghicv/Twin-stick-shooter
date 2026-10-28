using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// LAN chat at the bottom left. ENTER opens the input, ENTER sends, ESC closes it.
// The last lines stay a few seconds, then fade; while typing, every recent line shows again.
// Also shows the room's notices (joined, left, kicked), which MatchManager adds as lines.
// While typing, this player's controls are off (InputLock).
public class ChatBox : MonoBehaviour
{
    [SerializeField] private InputField input;

    [Tooltip("The chat lines, newest at the bottom.")]
    [SerializeField] private Text linesText;

    [Tooltip("Shown behind the lines while typing.")]
    [SerializeField] private GameObject background;

    [SerializeField] private int maxLines = 8;

    [Tooltip("A line stays this long (seconds), then fades out.")]
    [SerializeField] private float lineTime = 8f;

    [SerializeField] private float fadeTime = 1f;

    private class Line
    {
        public string text;
        public float time;
    }

    private readonly List<Line> lines = new List<Line>();
    private int closedFrame = -1;

    public static bool Typing { get; private set; }

    // ESC closed the chat this frame (the ESC menu must not open on the same press).
    public static bool ClosedThisFrame { get; private set; }

    private void Awake()
    {
        Typing = false;
        input.gameObject.SetActive(false);
        background.SetActive(false);
        input.characterLimit = 80;
    }

    private void OnDestroy()
    {
        Typing = false;
    }

    public void AddLine(string text)
    {
        lines.Add(new Line { text = text, time = Time.unscaledTime });
        if (lines.Count > maxLines)
            lines.RemoveAt(0);
        UiAudio.Play(UiAudio.Sound.Chat);
    }

    private void Update()
    {
        ClosedThisFrame = closedFrame == Time.frameCount;
        Keyboard keyboard = Keyboard.current;
        bool inRoom = GameMode.IsLan && MatchManager.Instance != null;
        if (keyboard != null && inRoom)
        {
            bool enter = keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
            if (!Typing && enter && !SettingsMenu.IsOpen)
                Open();
            else if (Typing && enter)
                Send();
            else if (Typing && keyboard.escapeKey.wasPressedThisFrame)
                Close();
        }
        else if (Typing)
        {
            Close(); // left the room
        }

        ShowLines();
    }

    private void Open()
    {
        Typing = true;
        input.gameObject.SetActive(true);
        background.SetActive(true);
        input.text = "";
        input.ActivateInputField();
        input.Select();
    }

    private void Send()
    {
        if (MatchManager.Instance != null)
            MatchManager.Instance.SendChat(input.text);
        Close();
    }

    private void Close()
    {
        Typing = false;
        closedFrame = Time.frameCount;
        ClosedThisFrame = true;
        input.DeactivateInputField();
        input.gameObject.SetActive(false);
        background.SetActive(false);
    }

    // Each line fades on its own; all of them show while typing.
    private void ShowLines()
    {
        var text = new System.Text.StringBuilder();
        foreach (Line line in lines)
        {
            float age = Time.unscaledTime - line.time;
            float alpha = Typing ? 1f : Mathf.Clamp01(1f - (age - lineTime) / fadeTime);
            if (alpha <= 0f)
                continue;
            // The fade goes into every color of the line (names have their own color).
            string a = Mathf.RoundToInt(alpha * 255f).ToString("X2");
            string faded = System.Text.RegularExpressions.Regex.Replace(line.text, "<color=#([0-9A-Fa-f]{6})>", "<color=#$1" + a + ">");
            text.Append("<color=#FFFFFF" + a + ">" + faded + "</color>\n");
        }
        linesText.text = text.ToString();
    }
}
