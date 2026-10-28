using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The ESC menu during a game (also the main menu's SETTINGS): resume, music and sound effects volumes, full screen, VSync,
// effects (EffectsSetting),
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

    [Tooltip("Window size when leaving full screen.")]
    [SerializeField] private Vector2Int windowSize = new Vector2Int(1280, 720);

    [Tooltip("VSync on/off (remembered on this PC). On: frames in step with the screen, smooth and no tearing.")]
    [SerializeField] private Button vSyncButton;

    private const string VSyncKey = "VSync";

    [Tooltip("EFFECTS on/off (EffectsSetting): bloom, motion blur and impact blur in a game. Off for slow PCs.")]
    [SerializeField] private Button effectsButton;

    [Tooltip("Back to the main menu. On LAN it leaves the room (the host's leaving closes it for everyone).")]
    [SerializeField] private Button leaveButton;

    // This player's controls are off while the menu is open.
    public static bool IsOpen { get; private set; }

    private void Awake()
    {
        IsOpen = false;
        panel.SetActive(false);
        QualitySettings.vSyncCount = PlayerPrefs.GetInt(VSyncKey, 1);
        vSyncButton.onClick.AddListener(() =>
        {
            QualitySettings.vSyncCount = QualitySettings.vSyncCount > 0 ? 0 : 1;
            PlayerPrefs.SetInt(VSyncKey, QualitySettings.vSyncCount);
        });
        effectsButton.onClick.AddListener(() =>
        {
            EffectsSetting.On = !EffectsSetting.On;
            EffectsSetting.Apply(GameMode.Current == GameMode.Mode.None); // the menu keeps its blurred backdrop
        });

        AudioListener.volume = 1f; // the two volumes below do it all
        musicSlider.SetValueWithoutNotify(GameAudio.MusicVolume);
        musicSlider.onValueChanged.AddListener(value => GameAudio.MusicVolume = value);
        sfxSlider.SetValueWithoutNotify(GameAudio.SfxVolume);
        sfxSlider.onValueChanged.AddListener(value => GameAudio.SfxVolume = value);

        resumeButton.onClick.AddListener(Close);
        fullScreenButton.onClick.AddListener(ToggleFullScreen);
        leaveButton.onClick.AddListener(Leave);
    }

    // Full screen at the monitor's own resolution (keeping the window's would stretch a small picture: blurry).
    private void ToggleFullScreen()
    {
        if (Screen.fullScreen)
            Screen.SetResolution(windowSize.x, windowSize.y, FullScreenMode.Windowed);
        else
            Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, FullScreenMode.FullScreenWindow);
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
        vSyncButton.GetComponentInChildren<Text>().text = QualitySettings.vSyncCount > 0
            ? Lang.T("VSYNC: ON", "VSYNC: BẬT")
            : Lang.T("VSYNC: OFF", "VSYNC: TẮT");
        effectsButton.GetComponentInChildren<Text>().text = EffectsSetting.On
            ? Lang.T("EFFECTS: ON", "HIỆU ỨNG: BẬT")
            : Lang.T("EFFECTS: OFF", "HIỆU ỨNG: TẮT");

        bool host = GameMode.IsLan && Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsServer;
        leaveButton.GetComponentInChildren<Text>().text = !GameMode.IsLan ? Lang.T("MAIN MENU", "VỀ MENU")
                                                        : host ? Lang.T("CLOSE ROOM", "ĐÓNG PHÒNG")
                                                        : Lang.T("LEAVE ROOM", "RỜI PHÒNG");
    }
}
