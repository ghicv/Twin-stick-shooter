using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The ESC menu during a game (also the main menu's SETTINGS): resume, music and sound effects volumes, full screen,
// language, and leave
// (back to the main menu).
// On LAN the match keeps running behind it (the others keep playing); only this player's controls stop
// (PlayerMovement / PlayerWeapon check IsOpen). Single player pauses.
// Both volumes are remembered on this PC (GameAudio).
public class SettingsMenu : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Button resumeButton;

    [SerializeField] private Slider musicSlider;
    [SerializeField] private Text musicValueText;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Text sfxValueText;

    [SerializeField] private Button fullScreenButton;

    [Tooltip("Back to the main menu. On LAN it leaves the room (the host's leaving closes it for everyone).")]
    [SerializeField] private Button leaveButton;

    // This player's controls are off while the menu is open.
    public static bool IsOpen { get; private set; }

    private void Awake()
    {
        IsOpen = false;
        panel.SetActive(false);

        AudioListener.volume = 1f; // the two volumes below do it all
        musicSlider.SetValueWithoutNotify(GameAudio.MusicVolume);
        musicSlider.onValueChanged.AddListener(value => GameAudio.MusicVolume = value);
        sfxSlider.SetValueWithoutNotify(GameAudio.SfxVolume);
        sfxSlider.onValueChanged.AddListener(value => GameAudio.SfxVolume = value);

        resumeButton.onClick.AddListener(Close);
        fullScreenButton.onClick.AddListener(() => Screen.fullScreen = !Screen.fullScreen);
        leaveButton.onClick.AddListener(Leave);
    }

    private void OnDestroy()
    {
        IsOpen = false; // the scene reloads (left the game): controls back for the next one
    }

    private void Update()
    {
        bool escape = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        if (escape && !ChatBox.Typing && !ChatBox.ClosedThisFrame) // ESC in the chat only closes the chat
        {
            if (IsOpen)
                Close();
            else if (GameMode.Current != GameMode.Mode.None) // in a game (the main menu has its own button)
                Open();
        }

        if (IsOpen)
            ShowLabels(); // full screen and the leave text can change while open (window mode, language)
    }

    // ESC in a game, or the main menu's SETTINGS button (there is nothing to leave there).
    public void Open()
    {
        IsOpen = true;
        panel.SetActive(true);
        leaveButton.gameObject.SetActive(GameMode.Current != GameMode.Mode.None);
        if (GameMode.Current == GameMode.Mode.Single)
            Time.timeScale = 0f;
    }

    private void Close()
    {
        IsOpen = false;
        panel.SetActive(false);
        if (GameMode.Current == GameMode.Mode.Single)
            Time.timeScale = 1f;
    }

    private void Leave()
    {
        IsOpen = false;
        GameMode.Restart(false); // ends the session (if any) and goes back to the main menu
    }

    private void ShowLabels()
    {
        musicValueText.text = Mathf.RoundToInt(musicSlider.value * 100f) + "%";
        sfxValueText.text = Mathf.RoundToInt(sfxSlider.value * 100f) + "%";
        resumeButton.GetComponentInChildren<Text>().text = GameMode.Current == GameMode.Mode.None ? Lang.T("BACK", "QUAY LẠI")
                                                                                                   : Lang.T("RESUME", "TIẾP TỤC");
        fullScreenButton.GetComponentInChildren<Text>().text = Screen.fullScreen
            ? Lang.T("FULL SCREEN: ON", "TOÀN MÀN HÌNH: BẬT")
            : Lang.T("FULL SCREEN: OFF", "TOÀN MÀN HÌNH: TẮT");

        bool host = GameMode.IsLan && Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsServer;
        leaveButton.GetComponentInChildren<Text>().text = !GameMode.IsLan ? Lang.T("MAIN MENU", "VỀ MENU")
                                                        : host ? Lang.T("CLOSE ROOM", "ĐÓNG PHÒNG")
                                                        : Lang.T("LEAVE ROOM", "RỜI PHÒNG");
    }
}
