using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using K = UiKit.ButtonKind;

// Builds the game's menus and HUD in the open scene (Tools > Rebuild UI), in UiKit's look.
// Each part clears what it built before, so it can be run again after a layout change.
// Positions are in the 1920 x 1080 reference resolution, measured from the anchor to the middle of each piece.
public static class UiBuilder
{
    private static readonly Vector2 C = new Vector2(0.5f, 0.5f);
    private static readonly Vector2 Top = new Vector2(0.5f, 1f);
    private static readonly Vector2 Bottom = new Vector2(0.5f, 0f);
    private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
    private static readonly Vector2 TopRight = new Vector2(1f, 1f);
    private static readonly Vector2 BottomLeft = new Vector2(0f, 0f);
    private static readonly Vector2 BottomRight = new Vector2(1f, 0f);
    private static readonly Vector2 Left = new Vector2(0f, 0.5f);
    private static readonly Vector2 Right = new Vector2(1f, 0.5f);
    private const TextAnchor Mid = TextAnchor.MiddleCenter;
    private const TextAnchor MidLeft = TextAnchor.MiddleLeft;
    private const TextAnchor MidRight = TextAnchor.MiddleRight;

    [MenuItem("Tools/Rebuild UI")]
    public static void BuildAll()
    {
        BuildMainMenu();
        BuildCharacterPanel();
        BuildMatchUi();
        BuildSettings();
        EditorSceneManager.MarkSceneDirty(Hud.gameObject.scene);
    }

    private static Transform Hud => GameObject.Find("HUD").transform;

    // ---------- Main menu ----------

    public static void BuildMainMenu()
    {
        MainMenu menu = Object.FindAnyObjectByType<MainMenu>(FindObjectsInactive.Include);
        Transform root = menu.transform;
        var rect = (RectTransform)root;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        UiKit.Clear(root);

        // Behind every menu screen: a map of the game, blurred and a little darker.
        BuildMenuScene(root);

        // Title screen
        RectTransform title = UiKit.Stretch(root, "TitlePanel");
        // A soft dark glow behind the middle column, so the menu reads well over the fight.
        Image shade = UiKit.Icon(title, "Shade", AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/Spotlight.png"), C, new Vector2(0, -20), new Vector2(1500, 1300));
        shade.preserveAspect = false;
        shade.color = new Color(0.02f, 0.02f, 0.05f, 0.8f);

        // In the middle: the pixel logo, then one column of buttons (the main one first and biggest).
        Image logo = UiKit.Icon(title, "Logo", MenuArt("Logo"), C, new Vector2(0, 285), new Vector2(97 * 6, 59 * 6));
        UiKit.Label(title, "Tagline", "LAN PARTY BRAWLER", "ĐẠI CHIẾN PIXEL CÙNG BẠN BÈ", 44, UiKit.Gold, Mid, C, new Vector2(0, 80), new Vector2(900, 56));
        Button lan = UiKit.Button(title, "LanButton", "PLAY LAN", "CHƠI LAN", C, new Vector2(0, -30), new Vector2(520, 104), K.Primary, 40);
        Button single = UiKit.Button(title, "SingleButton", "SINGLE PLAYER", "CHƠI ĐƠN", C, new Vector2(0, -145), new Vector2(520, 84), K.Normal, 30);
        Button settings = UiKit.Button(title, "SettingsButton", "SETTINGS", "CÀI ĐẶT", C, new Vector2(0, -240), new Vector2(520, 84), K.Normal, 30);
        Button quit = UiKit.Button(title, "QuitButton", "QUIT", "THOÁT", C, new Vector2(0, -335), new Vector2(520, 84), K.Subtle, 30);

        Image pointer = UiKit.Icon(title, "Pointer", AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/Arrow.png"), C, Vector2.zero, new Vector2(30, 55));

        var animator = title.gameObject.AddComponent<MenuAnimator>();
        UiKit.Wire(animator, "logo", logo.rectTransform);
        UiKit.WireArray(animator, "buttons", new Object[] { lan.transform, single.transform, settings.transform, quit.transform });
        UiKit.Wire(animator, "pointer", pointer.rectTransform);

        // Top right: the player's card (name).
        Transform profile = UiKit.Panel(title, "Profile", TopRight, new Vector2(-300, -80), new Vector2(520, 120)).transform;
        UiKit.Label(profile, "NameLabel", "YOUR NAME", "TÊN CỦA BẠN", 30, UiKit.Muted, MidLeft, TopLeft, new Vector2(250, -26), new Vector2(460, 34));
        InputField nameInput = UiKit.Input(profile, "NameInput", "TYPE YOUR NAME", "NHẬP TÊN CỦA BẠN", Bottom, new Vector2(0, 42), new Vector2(472, 62), 42);

        // Bottom corners, small: language and version.
        LanguageRow(title, BottomLeft, new Vector2(230, 64), 180, 22);
        UiKit.Label(title, "Version", "v0.6  LAN", null, 30, UiKit.Muted, MidRight, BottomRight, new Vector2(-150, 50), new Vector2(260, 40));

        // LAN screen (the same scene behind, darker so the lists read well)
        RectTransform lanPanel = UiKit.Stretch(root, "LanPanel");
        UiKit.Dim(lanPanel, "Backdrop", 0.6f).raycastTarget = true;
        UiKit.Label(lanPanel, "Header", "PLAY LAN", "CHƠI LAN", 90, UiKit.Orange, Mid, Top, new Vector2(0, -90), new Vector2(1200, 120), true);
        UiKit.Label(lanPanel, "Subheader", "JOIN A ROOM ON YOUR NETWORK, OR CREATE ONE", "VÀO MỘT PHÒNG TRONG MẠNG LAN, HOẶC TẠO PHÒNG MỚI",
                    40, UiKit.Muted, Mid, Top, new Vector2(0, -190), new Vector2(1600, 50));

        Transform actions = UiKit.Panel(lanPanel, "ActionsCard", C, new Vector2(-540, -50), new Vector2(500, 640)).transform;
        UiKit.Label(actions, "Title", "NEW ROOM", "PHÒNG MỚI", 36, UiKit.Gold, Mid, Top, new Vector2(0, -60), new Vector2(440, 60), true);
        UiKit.Label(actions, "Hint", "FRIENDS ON THE SAME NETWORK CAN SEE AND JOIN IT", "BẠN BÈ CÙNG MẠNG LAN SẼ THẤY VÀ VÀO ĐƯỢC",
                    32, UiKit.Muted, Mid, Top, new Vector2(0, -140), new Vector2(420, 90));
        Button create = UiKit.Button(actions, "CreateButton", "CREATE ROOM", "TẠO PHÒNG", C, new Vector2(0, 50), new Vector2(420, 96), K.Primary, 34);
        UiKit.Label(actions, "Or", "- OR -", "- HOẶC -", 32, UiKit.Muted, Mid, C, new Vector2(0, -38), new Vector2(420, 40));
        Button joinIp = UiKit.Button(actions, "JoinIpButton", "JOIN BY IP", "VÀO BẰNG IP", C, new Vector2(0, -120), new Vector2(420, 84), K.Normal, 30);
        Button back = UiKit.Button(actions, "BackButton", "BACK", "QUAY LẠI", Bottom, new Vector2(0, 70), new Vector2(420, 76), K.Subtle, 28);

        Transform rooms = UiKit.Panel(lanPanel, "RoomsCard", C, new Vector2(250, -50), new Vector2(1000, 640)).transform;
        UiKit.Label(rooms, "Title", "OPEN ROOMS", "PHÒNG ĐANG MỞ", 36, UiKit.Gold, MidLeft, TopLeft, new Vector2(340, -60), new Vector2(600, 60), true);
        UiKit.Label(rooms, "ColName", "ROOM", "TÊN PHÒNG", 30, UiKit.Muted, MidLeft, TopLeft, new Vector2(270, -118), new Vector2(440, 40));
        UiKit.Label(rooms, "ColPlayers", "PLAYERS", "NGƯỜI CHƠI", 30, UiKit.Muted, MidRight, TopRight, new Vector2(-180, -118), new Vector2(280, 40));
        RectTransform list = UiKit.Rect(rooms, "List", Top, Top, new Vector2(0, -150), new Vector2(920, 450));
        var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 12;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.UpperCenter;
        Button row = UiKit.Button(list, "RoomButtonTemplate", "", null, Top, Vector2.zero, new Vector2(920, 80), K.Normal, 30);
        Text rowName = row.transform.Find("Label").GetComponent<Text>();
        rowName.name = "NameText";
        rowName.font = UiKit.PixelFont;
        rowName.fontSize = 46;
        rowName.alignment = MidLeft;
        var rowNameRect = (RectTransform)rowName.transform;
        rowNameRect.anchorMin = Vector2.zero;
        rowNameRect.anchorMax = Vector2.one;
        rowNameRect.offsetMin = new Vector2(28, 4);
        rowNameRect.offsetMax = new Vector2(-220, -4);
        UiKit.Label(row.transform, "PlayersText", "2/4", null, 42, UiKit.Gold, MidRight, Right, new Vector2(-130, 3), new Vector2(220, 60));
        Text roomsStatus = UiKit.Label(rooms, "SearchingText", "SEARCHING FOR ROOMS...", null, 40, UiKit.Muted, Mid, C, new Vector2(0, -30), new Vector2(900, 120));
        Text status = UiKit.Label(lanPanel, "StatusText", "", null, 42, UiKit.Gold, Mid, Bottom, new Vector2(0, 60), new Vector2(1600, 60));

        // Create room box (fields stacked with Field: label, then its input, the same gaps everywhere)
        RectTransform createPanel = UiKit.Stretch(root, "CreatePanel");
        UiKit.Dim(createPanel, "Backdrop", 0.75f).raycastTarget = true;
        Transform createCard = UiKit.Panel(createPanel, "Card", C, Vector2.zero, new Vector2(760, 680)).transform;
        UiKit.Label(createCard, "Title", "CREATE ROOM", "TẠO PHÒNG", 56, UiKit.Orange, Mid, Top, new Vector2(0, -62), new Vector2(700, 80), true);
        float y = 200f;
        InputField roomName = Field(createCard, "RoomName", "ROOM NAME", "TÊN PHÒNG", "ROOM NAME", "TÊN PHÒNG", ref y);
        roomName.characterLimit = 24;
        InputField createPassword = Field(createCard, "Password", "PASSWORD (EMPTY = OPEN ROOM)", "MẬT KHẨU (ĐỂ TRỐNG = PHÒNG MỞ)",
                                          "NO PASSWORD", "KHÔNG CÓ MẬT KHẨU", ref y);
        createPassword.contentType = InputField.ContentType.Password;
        createPassword.characterLimit = 16;
        UiKit.Label(createCard, "PlayersLabel", "MAX PLAYERS", "SỐ NGƯỜI TỐI ĐA", 34, UiKit.Muted, MidLeft, C, new Vector2(0, y), new Vector2(640, FieldLabelHeight));
        float buttonsY = y - FieldLabelHeight * 0.5f - FieldLabelGap - 36f;
        Button players2 = UiKit.Button(createCard, "Players2", "2", null, C, new Vector2(-220, buttonsY), new Vector2(200, 72), K.Subtle, 34);
        Button players3 = UiKit.Button(createCard, "Players3", "3", null, C, new Vector2(0, buttonsY), new Vector2(200, 72), K.Subtle, 34);
        Button players4 = UiKit.Button(createCard, "Players4", "4", null, C, new Vector2(220, buttonsY), new Vector2(200, 72), K.Subtle, 34);
        Button createCancel = UiKit.Button(createCard, "CancelButton", "CANCEL", "HỦY", Bottom, new Vector2(-165, 76), new Vector2(300, 84), K.Subtle, 30);
        Button createOk = UiKit.Button(createCard, "CreateButton", "CREATE", "TẠO", Bottom, new Vector2(165, 76), new Vector2(300, 84), K.Primary, 32);

        // Join box
        RectTransform joinPanel = UiKit.Stretch(root, "JoinPanel");
        UiKit.Dim(joinPanel, "Backdrop", 0.75f).raycastTarget = true;
        Transform joinCard = UiKit.Panel(joinPanel, "Card", C, Vector2.zero, new Vector2(760, 560)).transform;
        UiKit.Label(joinCard, "Title", "JOIN ROOM", "VÀO PHÒNG", 56, UiKit.Orange, Mid, Top, new Vector2(0, -62), new Vector2(700, 80), true);
        y = 140f;
        InputField ip = Field(joinCard, "Ip", "HOST'S IP", "IP CỦA CHỦ PHÒNG", "E.G. 192.168.1.5", "VD: 192.168.1.5", ref y);
        ip.characterLimit = 40;
        InputField joinPassword = Field(joinCard, "Password", "PASSWORD (IF ANY)", "MẬT KHẨU (NẾU CÓ)", "NO PASSWORD", "KHÔNG CÓ MẬT KHẨU", ref y);
        joinPassword.contentType = InputField.ContentType.Password;
        joinPassword.characterLimit = 16;
        Button joinCancel = UiKit.Button(joinCard, "CancelButton", "CANCEL", "HỦY", Bottom, new Vector2(-165, 76), new Vector2(300, 84), K.Subtle, 30);
        Button joinOk = UiKit.Button(joinCard, "JoinButton", "JOIN", "VÀO", Bottom, new Vector2(165, 76), new Vector2(300, 84), K.Primary, 32);

        UiKit.Wire(menu, "titlePanel", title.gameObject);
        UiKit.Wire(menu, "nameInput", nameInput);
        UiKit.Wire(menu, "lanButton", lan);
        UiKit.Wire(menu, "singleButton", single);
        UiKit.Wire(menu, "settingsButton", settings);
        UiKit.Wire(menu, "quitButton", quit);
        UiKit.Wire(menu, "lanPanel", lanPanel.gameObject);
        UiKit.Wire(menu, "createButton", create);
        UiKit.Wire(menu, "joinIpButton", joinIp);
        UiKit.Wire(menu, "backButton", back);
        UiKit.Wire(menu, "roomButtonTemplate", row);
        UiKit.Wire(menu, "roomsStatusText", roomsStatus);
        UiKit.Wire(menu, "statusText", status);
        UiKit.Wire(menu, "createPanel", createPanel.gameObject);
        UiKit.Wire(menu, "roomNameInput", roomName);
        UiKit.Wire(menu, "createPasswordInput", createPassword);
        UiKit.WireArray(menu, "maxPlayerButtons", new Object[] { players2, players3, players4 });
        UiKit.Wire(menu, "createConfirmButton", createOk);
        UiKit.Wire(menu, "createCancelButton", createCancel);
        UiKit.Wire(menu, "joinPanel", joinPanel.gameObject);
        UiKit.Wire(menu, "ipInput", ip);
        UiKit.Wire(menu, "joinPasswordInput", joinPassword);
        UiKit.Wire(menu, "joinConfirmButton", joinOk);
        UiKit.Wire(menu, "joinCancelButton", joinCancel);
        UiKit.Wire(menu, "discovery", menu.GetComponent<LanDiscovery>());
        UiKit.Wire(menu, "settingsMenu", Object.FindAnyObjectByType<SettingsMenu>(FindObjectsInactive.Include));
        lanPanel.gameObject.SetActive(false);
        createPanel.gameObject.SetActive(false);
        joinPanel.gameObject.SetActive(false);
    }

    private static Sprite MenuArt(string name) => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/Menu/" + name + ".png");

    // Behind every menu: map 0 (MenuBackdrop), blurred by a post-processing volume and darkened a bit.
    private static void BuildMenuScene(Transform root)
    {
        RectTransform scene = UiKit.Stretch(root, "MenuScene");
        UiKit.Dim(scene, "Darken", 0.35f).raycastTarget = false;

        var backdrop = scene.gameObject.AddComponent<MenuBackdrop>();
        UiKit.Wire(backdrop, "maps", Object.FindAnyObjectByType<MapManager>(FindObjectsInactive.Include));
        UiKit.Wire(backdrop, "blur", MenuBlurVolume());
        UiKit.Wire(Object.FindAnyObjectByType<MainMenu>(FindObjectsInactive.Include), "menuScene", scene.gameObject);
    }

    // A global volume with a Gaussian depth of field that blurs everything the camera sees (the UI is drawn on top,
    // sharp). Made once; MenuBackdrop turns it on and off.
    private static UnityEngine.Rendering.Volume MenuBlurVolume()
    {
        const string profilePath = "Assets/Settings/MenuBlurProfile.asset";
        var profile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(profilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
        }
        if (!profile.TryGet(out UnityEngine.Rendering.Universal.DepthOfField dof))
            dof = profile.Add<UnityEngine.Rendering.Universal.DepthOfField>(true);
        dof.mode.Override(UnityEngine.Rendering.Universal.DepthOfFieldMode.Gaussian);
        dof.gaussianStart.Override(0f);
        dof.gaussianEnd.Override(1f);
        dof.gaussianMaxRadius.Override(1.5f);
        dof.highQualitySampling.Override(true);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        GameObject found = GameObject.Find("MenuBlur");
        var volume = found != null ? found.GetComponent<UnityEngine.Rendering.Volume>()
                                   : new GameObject("MenuBlur").AddComponent<UnityEngine.Rendering.Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;
        volume.sharedProfile = profile;

        return volume;
    }

    private const float FieldLabelHeight = 40f;
    private const float FieldLabelGap = 6f;   // label to its input
    private const float FieldInputHeight = 76f;
    private const float FieldGroupGap = 26f;  // input to the next label

    // A label with its input under it, 640 wide, starting at labelY (the label's middle). Moves labelY down to where
    // the next label goes, so stacked fields always have the same gaps.
    private static InputField Field(Transform card, string name, string english, string vietnamese,
                                    string placeholderEnglish, string placeholderVietnamese, ref float labelY)
    {
        UiKit.Label(card, name + "Label", english, vietnamese, 34, UiKit.Muted, MidLeft, C, new Vector2(0, labelY), new Vector2(640, FieldLabelHeight));
        float inputY = labelY - FieldLabelHeight * 0.5f - FieldLabelGap - FieldInputHeight * 0.5f;
        InputField input = UiKit.Input(card, name + "Input", placeholderEnglish, placeholderVietnamese, C, new Vector2(0, inputY),
                                       new Vector2(640, FieldInputHeight), 42);
        labelY = inputY - FieldInputHeight * 0.5f - FieldGroupGap - FieldLabelHeight * 0.5f;
        return input;
    }

    // ENGLISH / TIẾNG VIỆT pair with a LanguageButtons.
    private static void LanguageRow(Transform parent, Vector2 anchor, Vector2 position, float buttonWidth, int fontSize)
    {
        RectTransform row = UiKit.Rect(parent, "Language", anchor, position, new Vector2(buttonWidth * 2f + 12f, 64));
        Button english = UiKit.Button(row, "EnglishButton", "ENGLISH", null, C, new Vector2(-buttonWidth * 0.5f - 6f, 0), new Vector2(buttonWidth, 60), K.Subtle, fontSize);
        Button vietnamese = UiKit.Button(row, "VietnameseButton", "TIẾNG VIỆT", null, C, new Vector2(buttonWidth * 0.5f + 6f, 0), new Vector2(buttonWidth, 60), K.Subtle, fontSize);
        var buttons = row.gameObject.AddComponent<LanguageButtons>();
        UiKit.Wire(buttons, "englishButton", english);
        UiKit.Wire(buttons, "vietnameseButton", vietnamese);
    }

    // ---------- Character screen (after hosting or joining) ----------

    public static void BuildCharacterPanel()
    {
        Transform hud = Hud;
        int index = hud.childCount;
        foreach (string old in new[] { "SkinPanel", "CharacterPanel" })
        {
            Transform found = hud.Find(old);
            if (found != null)
            {
                index = found.GetSiblingIndex();
                Object.DestroyImmediate(found.gameObject);
            }
        }
        RectTransform root = UiKit.Stretch(hud, "CharacterPanel");
        root.SetSiblingIndex(Mathf.Min(index, hud.childCount - 1));
        var characterPanel = root.gameObject.AddComponent<CharacterPanel>();
        RectTransform panel = UiKit.Stretch(root, "Panel");
        UiKit.Dim(panel, "Backdrop", 0.85f).raycastTarget = true;
        UiKit.Label(panel, "Title", "MAKE YOUR FIGHTER", "TẠO NHÂN VẬT", 64, UiKit.Orange, Mid, Top, new Vector2(0, -100), new Vector2(1400, 100), true);

        // Left: the preview on a little stage, and the name (changeable here too).
        Transform left = UiKit.Panel(panel, "PreviewCard", C, new Vector2(-370, -30), new Vector2(560, 660)).transform;
        Image stage = UiKit.Panel(left, "Stage", C, new Vector2(0, -130), new Vector2(300, 40));
        stage.color = new Color(0.8f, 0.85f, 1f);
        RectTransform preview = UiKit.Rect(left, "Preview", C, new Vector2(0, -110), new Vector2(168, 308));
        preview.pivot = new Vector2(0.5f, 0f);
        Sprite bodySprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Pixel/Capsule.png");
        Image body = UiKit.Icon(preview, "Body", bodySprite, C, Vector2.zero, new Vector2(168, 308)); // 12 x 22 pixels, x14
        Image outfit = UiKit.Icon(body.transform, "Outfit", null, C, Vector2.zero, new Vector2(168, 308));
        Image hat = UiKit.Icon(body.transform, "Hat", null, C, Vector2.zero, new Vector2(196, 224));  // 14 x 16 pixels, x14
        hat.rectTransform.pivot = new Vector2(0.5f, 0f); // its pivot is the body's middle
        hat.rectTransform.anchoredPosition = Vector2.zero;
        UiKit.Shadow(body, new Vector2(8, -8));
        UiKit.Label(left, "NameLabel", "NAME", "TÊN", 32, UiKit.Muted, Mid, Bottom, new Vector2(0, 128), new Vector2(440, 40));
        InputField nameInput = UiKit.Input(left, "NameInput", "TYPE YOUR NAME", "NHẬP TÊN", Bottom, new Vector2(0, 74), new Vector2(440, 68), 44);
        nameInput.textComponent.alignment = Mid;
        ((Text)nameInput.placeholder).alignment = Mid;

        // Right: one row per part, each with arrows.
        Transform right = UiKit.Panel(panel, "PartsCard", C, new Vector2(330, -30), new Vector2(680, 660)).transform;
        PartRow(right, characterPanel, "hatRow", "HAT", "MŨ", 190);
        PartRow(right, characterPanel, "outfitRow", "OUTFIT", "QUẦN ÁO", 45);
        PartRow(right, characterPanel, "colorRow", "SKIN COLOR", "MÀU DA", -100);
        Button random = UiKit.Button(right, "RandomButton", "RANDOM", "NGẪU NHIÊN", Bottom, new Vector2(-160, 80), new Vector2(290, 84), K.Normal, 28);
        Button enter = UiKit.Button(right, "EnterButton", "ENTER ROOM", "VÀO PHÒNG", Bottom, new Vector2(160, 80), new Vector2(290, 92), K.Primary, 30);
        Text status = UiKit.Label(panel, "StatusText", "", null, 40, UiKit.Gold, Mid, Bottom, new Vector2(0, 60), new Vector2(1200, 56));

        UiKit.Wire(characterPanel, "panel", panel.gameObject);
        UiKit.Wire(characterPanel, "preview", preview);
        UiKit.Wire(characterPanel, "previewBody", body);
        UiKit.Wire(characterPanel, "previewOutfit", outfit);
        UiKit.Wire(characterPanel, "previewHat", hat);
        UiKit.Wire(characterPanel, "nameInput", nameInput);
        UiKit.Wire(characterPanel, "randomButton", random);
        UiKit.Wire(characterPanel, "enterButton", enter);
        UiKit.Wire(characterPanel, "statusText", status);
        UiKit.Wire(Object.FindAnyObjectByType<MainMenu>(FindObjectsInactive.Include), "characterPanel", characterPanel);
        panel.gameObject.SetActive(false);
    }

    // "HAT" above "<  CROWN  >".
    private static void PartRow(Transform card, CharacterPanel characterPanel, string field, string english, string vietnamese, float y)
    {
        UiKit.Label(card, field + "Label", english, vietnamese, 36, UiKit.Muted, Mid, C, new Vector2(0, y + 50), new Vector2(560, 44));
        Button previous = UiKit.Button(card, field + "Previous", "<", null, C, new Vector2(-240, y - 10), new Vector2(96, 84), K.Normal, 40);
        Button next = UiKit.Button(card, field + "Next", ">", null, C, new Vector2(240, y - 10), new Vector2(96, 84), K.Normal, 40);
        Text value = UiKit.Label(card, field + "Value", "NONE", null, 50, Color.white, Mid, C, new Vector2(0, y - 10), new Vector2(370, 70));
        UiKit.Shadow(value, new Vector2(3, -3));
        var so = new SerializedObject(characterPanel);
        SerializedProperty row = so.FindProperty(field);
        row.FindPropertyRelative("previous").objectReferenceValue = previous;
        row.FindPropertyRelative("next").objectReferenceValue = next;
        row.FindPropertyRelative("value").objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------- In a LAN game ----------

    public static void BuildMatchUi()
    {
        Transform hud = Hud;
        foreach (string old in new[] { "RoundText", "Scoreboard", "Results", "MatchHud", "KillFeed", "Chat", "Countdown", "MapBoard", "UiAudio" })
        {
            Transform found = hud.Find(old);
            if (found != null)
                Object.DestroyImmediate(found.gameObject);
        }
        MatchUI matchUi = hud.GetComponent<MatchUI>();

        Text banner = hud.Find("BannerText").GetComponent<Text>();
        Outline outline = banner.GetComponent<Outline>();
        if (outline != null)
            Object.DestroyImmediate(outline);
        banner.font = UiKit.TitleFont;
        banner.fontSize = 116;
        if (banner.GetComponent<Shadow>() == null)
            UiKit.Shadow(banner, new Vector2(6, -6));
        ((RectTransform)banner.transform).anchoredPosition = new Vector2(0, 250);
        ((RectTransform)banner.transform).sizeDelta = new Vector2(1800, 130);
        int index = banner.transform.GetSiblingIndex();

        // Top bar: round and one chip per player
        RectTransform hudRoot = UiKit.Stretch(hud, "MatchHud");
        hudRoot.SetSiblingIndex(index);
        var matchHud = hudRoot.gameObject.AddComponent<MatchHud>();
        RectTransform group = UiKit.Rect(hudRoot, "MatchGroup", Top, Top, new Vector2(0, -16), new Vector2(1400, 140));
        Text round = UiKit.Label(group, "RoundText", "ROUND 1 / 8", null, 34, UiKit.Gold, Mid, Top, new Vector2(0, -24), new Vector2(600, 44), true);
        RectTransform chips = UiKit.Rect(group, "Chips", Top, Top, new Vector2(0, -60), new Vector2(1300, 60));
        var chipLayout = chips.gameObject.AddComponent<HorizontalLayoutGroup>();
        chipLayout.spacing = 14;
        chipLayout.childAlignment = TextAnchor.MiddleCenter;
        chipLayout.childControlWidth = false;
        chipLayout.childControlHeight = false;
        chipLayout.childForceExpandWidth = false;
        chipLayout.childForceExpandHeight = false;
        for (int i = 0; i < 4; i++)
        {
            Image chip = UiKit.Panel(chips, "Chip" + (i + 1), C, Vector2.zero, new Vector2(300, 56));
            Image bar = UiKit.Box(chip.transform, "ColorBar", Left, new Vector2(16, 0), new Vector2(10, 34), Color.white);
            Text name = UiKit.Label(chip.transform, "Name", "PLAYER", null, 40, Color.white, MidLeft, Left, new Vector2(128, 2), new Vector2(196, 50));
            Text wins = UiKit.Label(chip.transform, "Wins", "0", null, 34, UiKit.Gold, MidRight, Right, new Vector2(-42, 2), new Vector2(60, 50), true);
            wins.GetComponent<Shadow>().effectDistance = new Vector2(2, -2);
            UiKit.WireRow(matchHud, "chips", i, "root", chip.gameObject);
            UiKit.WireRow(matchHud, "chips", i, "colorBar", bar);
            UiKit.WireRow(matchHud, "chips", i, "name", name);
            UiKit.WireRow(matchHud, "chips", i, "wins", wins);
        }
        Text hint = UiKit.Label(hudRoot, "LobbyHint", "[TAB] PLAYERS", null, 34, UiKit.Muted, Mid, Bottom, new Vector2(0, 40), new Vector2(1600, 44));
        UiKit.Shadow(hint, new Vector2(2, -2));
        UiKit.Wire(matchHud, "matchGroup", group.gameObject);
        UiKit.Wire(matchHud, "roundText", round);
        UiKit.Wire(matchHud, "lobbyHintText", hint);

        // Kill feed (top right)
        RectTransform feed = UiKit.Rect(hud, "KillFeed", TopRight, TopRight, new Vector2(-30, -30), new Vector2(760, 260));
        feed.SetSiblingIndex(index + 1);
        Text feedText = UiKit.Label(feed, "Text", "", null, 36, Color.white, TextAnchor.UpperRight, C, Vector2.zero, new Vector2(760, 260));
        UiKit.Shadow(feedText, new Vector2(2, -2));
        var killFeed = feed.gameObject.AddComponent<KillFeed>();
        UiKit.Wire(killFeed, "text", feedText);

        // Chat (bottom left)
        RectTransform chat = UiKit.Rect(hud, "Chat", BottomLeft, BottomLeft, new Vector2(30, 30), new Vector2(780, 420));
        chat.SetSiblingIndex(index + 2);
        Image chatBack = UiKit.Box(chat, "Background", BottomLeft, Vector2.zero, new Vector2(780, 340), new Color(0.03f, 0.03f, 0.06f, 0.6f));
        ((RectTransform)chatBack.transform).pivot = BottomLeft;
        ((RectTransform)chatBack.transform).anchoredPosition = new Vector2(0, 76);
        Text lines = UiKit.Label(chat, "Lines", "", null, 34, Color.white, TextAnchor.LowerLeft, BottomLeft, Vector2.zero, new Vector2(750, 320));
        ((RectTransform)lines.transform).pivot = BottomLeft;
        ((RectTransform)lines.transform).anchoredPosition = new Vector2(14, 86);
        UiKit.Shadow(lines, new Vector2(2, -2));
        InputField chatInput = UiKit.Input(chat, "Input", "TYPE A MESSAGE  (ENTER SEND, ESC CANCEL)", "NHẬP TIN NHẮN  (ENTER GỬI, ESC HỦY)",
                                           BottomLeft, Vector2.zero, new Vector2(780, 66), 34);
        ((RectTransform)chatInput.transform).pivot = BottomLeft;
        ((RectTransform)chatInput.transform).anchoredPosition = Vector2.zero;
        var chatBox = chat.gameObject.AddComponent<ChatBox>();
        UiKit.Wire(chatBox, "input", chatInput);
        UiKit.Wire(chatBox, "linesText", lines);
        UiKit.Wire(chatBox, "background", chatBack.gameObject);

        // 3-2-1
        Text countdown = UiKit.Label(hud, "Countdown", "", null, 260, UiKit.Gold, Mid, C, new Vector2(0, 120), new Vector2(600, 320), true);
        countdown.GetComponent<Shadow>().effectDistance = new Vector2(10, -10);
        countdown.transform.SetSiblingIndex(index + 3);

        // Results
        RectTransform results = UiKit.Stretch(hud, "Results");
        results.SetSiblingIndex(hud.Find("CoreCards").GetSiblingIndex() + 1);
        UiKit.Dim(results, "Backdrop", 0.8f).raycastTarget = true;
        Transform card = UiKit.Panel(results, "Card", C, Vector2.zero, new Vector2(1060, 760)).transform;
        UiKit.Label(card, "Header", "MATCH OVER", "KẾT THÚC TRẬN", 36, UiKit.Muted, Mid, Top, new Vector2(0, -56), new Vector2(900, 50), true);
        Text resultsTitle = UiKit.Label(card, "Title", "NAME WINS THE MATCH!", null, 60, UiKit.Gold, Mid, Top, new Vector2(0, -130), new Vector2(1000, 90), true);
        for (int i = 0; i < 4; i++)
        {
            Image row = UiKit.Panel(card, "Row" + (i + 1), Top, new Vector2(0, -235 - i * 98), new Vector2(900, 86));
            Text rank = UiKit.Label(row.transform, "Rank", "#" + (i + 1), null, 40, i == 0 ? UiKit.Gold : UiKit.Muted, Mid, Left, new Vector2(60, 2), new Vector2(100, 60), true);
            Text name = UiKit.Label(row.transform, "Name", "PLAYER", null, 50, Color.white, MidLeft, Left, new Vector2(330, 2), new Vector2(420, 70));
            Text wins = UiKit.Label(row.transform, "Wins", "0 WINS", null, 42, UiKit.Gold, MidRight, Right, new Vector2(-260, 2), new Vector2(200, 60));
            Text cores = UiKit.Label(row.transform, "Cores", "0 CORE", null, 38, UiKit.Muted, MidRight, Right, new Vector2(-90, 2), new Vector2(150, 60));
            UiKit.WireRow(matchUi, "resultRows", i, "root", row.gameObject);
            UiKit.WireRow(matchUi, "resultRows", i, "rank", rank);
            UiKit.WireRow(matchUi, "resultRows", i, "name", name);
            UiKit.WireRow(matchUi, "resultRows", i, "wins", wins);
            UiKit.WireRow(matchUi, "resultRows", i, "cores", cores);
        }
        Button lobby = UiKit.Button(card, "LobbyButton", "BACK TO LOBBY", "VỀ PHÒNG CHỜ", Bottom, new Vector2(0, 80), new Vector2(460, 92), K.Primary, 34);
        Text wait = UiKit.Label(card, "WaitText", "", null, 40, UiKit.Muted, Mid, Bottom, new Vector2(0, 80), new Vector2(900, 60));
        UiKit.Wire(matchUi, "bannerText", banner);
        UiKit.Wire(matchUi, "countdownText", countdown);
        UiKit.Wire(matchUi, "chat", chatBox);
        UiKit.Wire(matchUi, "killFeed", killFeed);
        UiKit.Wire(matchUi, "resultsPanel", results.gameObject);
        UiKit.Wire(matchUi, "resultsTitle", resultsTitle);
        UiKit.Wire(matchUi, "lobbyButton", lobby);
        UiKit.Wire(matchUi, "resultsWaitText", wait);
        results.gameObject.SetActive(false);

        BuildScoreboard(hud);
        BuildMapBoard(hud);
        BuildUiAudio(hud);
    }

    private static void BuildScoreboard(Transform hud)
    {
        RectTransform root = UiKit.Stretch(hud, "Scoreboard");
        root.SetSiblingIndex(hud.Find("MainMenu").GetSiblingIndex());
        var scoreboard = root.gameObject.AddComponent<Scoreboard>();
        RectTransform panel = UiKit.Stretch(root, "Panel");
        UiKit.Dim(panel, "Backdrop", 0.45f);
        Transform card = UiKit.Panel(panel, "Card", C, new Vector2(0, 20), new Vector2(1280, 640)).transform;
        Text title = UiKit.Label(card, "Title", "ROOM", null, 46, UiKit.Orange, MidLeft, TopLeft, new Vector2(500, -60), new Vector2(920, 70), true);
        Text subtitle = UiKit.Label(card, "Subtitle", "LOBBY", null, 36, UiKit.Gold, MidRight, TopRight, new Vector2(-200, -62), new Vector2(340, 50));
        UiKit.Label(card, "ColPlayer", "PLAYER", "NGƯỜI CHƠI", 30, UiKit.Muted, MidLeft, TopLeft, new Vector2(250, -128), new Vector2(400, 40));
        UiKit.Label(card, "ColWins", "WINS", "THẮNG", 30, UiKit.Muted, Mid, TopLeft, new Vector2(600, -128), new Vector2(140, 40));
        UiKit.Label(card, "ColCores", "CORES", "CORE", 30, UiKit.Muted, MidLeft, TopLeft, new Vector2(840, -128), new Vector2(300, 40));
        UiKit.Label(card, "ColPing", "PING", "PING", 30, UiKit.Muted, Mid, TopRight, new Vector2(-250, -128), new Vector2(140, 40));
        Image iconTemplate = UiKit.Icon(card, "IconTemplate", null, TopLeft, Vector2.zero, new Vector2(28, 28));
        for (int i = 0; i < 4; i++)
        {
            Transform row = UiKit.Panel(card, "Row" + (i + 1), Top, new Vector2(0, -200 - i * 100), new Vector2(1200, 88)).transform;
            Image bar = UiKit.Box(row, "ColorBar", Left, new Vector2(16, 0), new Vector2(12, 56), Color.white);
            Text name = UiKit.Label(row, "Name", "PLAYER", null, 46, Color.white, MidLeft, Left, new Vector2(230, 2), new Vector2(380, 70));
            Image crown = UiKit.Icon(row, "HostBadge", UiKit.Crown, Left, new Vector2(445, 4), new Vector2(44, 32));
            Text wins = UiKit.Label(row, "Wins", "0", null, 44, UiKit.Gold, Mid, Left, new Vector2(560, 2), new Vector2(120, 60), true);
            RectTransform icons = UiKit.Rect(row, "Icons", Left, Left, new Vector2(660, 0), new Vector2(360, 30));
            Text ping = UiKit.Label(row, "Ping", "0 ms", null, 40, UiKit.Good, Mid, Right, new Vector2(-210, 2), new Vector2(140, 60));
            Button kick = UiKit.Button(row, "KickButton", "KICK", "ĐUỔI", Right, new Vector2(-75, 0), new Vector2(120, 60), K.Danger, 22);
            UiKit.WireRow(scoreboard, "rows", i, "root", row.gameObject);
            UiKit.WireRow(scoreboard, "rows", i, "colorBar", bar);
            UiKit.WireRow(scoreboard, "rows", i, "name", name);
            UiKit.WireRow(scoreboard, "rows", i, "hostBadge", crown.gameObject);
            UiKit.WireRow(scoreboard, "rows", i, "wins", wins);
            UiKit.WireRow(scoreboard, "rows", i, "icons", icons);
            UiKit.WireRow(scoreboard, "rows", i, "ping", ping);
            UiKit.WireRow(scoreboard, "rows", i, "kickButton", kick);
        }
        UiKit.Label(card, "Hint", "RELEASE TAB TO CLOSE", "THẢ TAB ĐỂ ĐÓNG", 30, UiKit.Muted, Mid, Bottom, new Vector2(0, 30), new Vector2(800, 40));
        UiKit.Wire(scoreboard, "panel", panel.gameObject);
        UiKit.Wire(scoreboard, "titleText", title);
        UiKit.Wire(scoreboard, "subtitleText", subtitle);
        UiKit.Wire(scoreboard, "iconTemplate", iconTemplate);
        panel.gameObject.SetActive(false);
    }

    private static void BuildMapBoard(Transform hud)
    {
        RectTransform root = UiKit.Stretch(hud, "MapBoard");
        root.SetSiblingIndex(hud.Find("MainMenu").GetSiblingIndex());
        var board = root.gameObject.AddComponent<MapBoard>();
        RectTransform panel = UiKit.Stretch(root, "Panel");
        UiKit.Dim(panel, "Backdrop", 0.6f).raycastTarget = true;
        Transform card = UiKit.Panel(panel, "Card", C, Vector2.zero, new Vector2(820, 720)).transform;
        UiKit.Label(card, "Title", "MAPS", "CHỌN MAP", 56, UiKit.Orange, Mid, Top, new Vector2(0, -66), new Vector2(760, 80), true);
        UiKit.Label(card, "Hint", "CLICK A MAP TO TURN IT ON OR OFF", "BẤM VÀO MAP ĐỂ BẬT / TẮT", 34, UiKit.Muted, Mid, Top, new Vector2(0, -130), new Vector2(760, 44));
        RectTransform grid = UiKit.Rect(card, "Grid", Top, Top, new Vector2(0, -170), new Vector2(700, 380));
        var gridLayout = grid.gameObject.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(340, 80);
        gridLayout.spacing = new Vector2(20, 16);
        gridLayout.childAlignment = TextAnchor.UpperCenter;
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = 2;
        Button mapButton = UiKit.Button(grid, "MapButtonTemplate", "[X]  MAP", null, Top, Vector2.zero, new Vector2(340, 80), K.Subtle, 30);
        Text mapLabel = mapButton.transform.Find("Label").GetComponent<Text>();
        mapLabel.font = UiKit.PixelFont;
        mapLabel.fontSize = 42;
        mapLabel.alignment = MidLeft;
        Button done = UiKit.Button(card, "CloseButton", "DONE", "XONG", Bottom, new Vector2(0, 75), new Vector2(360, 84), K.Primary, 32);
        UiKit.Wire(board, "panel", panel.gameObject);
        UiKit.Wire(board, "mapButtonTemplate", mapButton);
        UiKit.Wire(board, "closeButton", done);
        panel.gameObject.SetActive(false);

        // The lobby's MAPS station opens this board.
        foreach (LobbyStation station in Object.FindObjectsByType<LobbyStation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (station.name == "Station_Maps")
                UiKit.Wire(station, "mapBoard", board);
    }

    private static void BuildUiAudio(Transform hud)
    {
        var go = new GameObject("UiAudio", typeof(RectTransform));
        go.transform.SetParent(hud, false);
        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        var audio = go.AddComponent<UiAudio>();
        UiKit.Wire(audio, "hover", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/UI/Hover.wav"));
        UiKit.Wire(audio, "click", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/UI/Click.wav"));
        UiKit.Wire(audio, "chat", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/UI/Chat.wav"));
    }

    // ---------- ESC menu ----------

    public static void BuildSettings()
    {
        Transform settings = Hud.Find("Settings");
        settings.SetAsLastSibling();
        var menu = settings.GetComponent<SettingsMenu>();
        UiKit.Clear(settings);
        RectTransform panel = UiKit.Stretch(settings, "Panel");
        UiKit.Dim(panel, "Backdrop", 0.7f).raycastTarget = true;
        Transform card = UiKit.Panel(panel, "Card", C, Vector2.zero, new Vector2(700, 820)).transform;
        UiKit.Label(card, "Title", "SETTINGS", "CÀI ĐẶT", 60, UiKit.Orange, Mid, Top, new Vector2(0, -70), new Vector2(640, 80), true);
        Button resume = UiKit.Button(card, "ResumeButton", "RESUME", null, Top, new Vector2(0, -175), new Vector2(520, 88), K.Primary, 32);
        UiKit.Label(card, "VolumeLabel", "VOLUME", "ÂM LƯỢNG", 40, Color.white, MidLeft, Top, new Vector2(-60, -265), new Vector2(400, 50));
        Text volumeValue = UiKit.Label(card, "VolumeValue", "100%", null, 40, UiKit.Gold, MidRight, Top, new Vector2(160, -265), new Vector2(200, 50));
        Slider slider = PixelSlider(card, new Vector2(0, -320), new Vector2(520, 28));
        Button fullScreen = UiKit.Button(card, "FullScreenButton", "FULL SCREEN: ON", null, Top, new Vector2(0, -420), new Vector2(520, 80), K.Normal, 28);
        UiKit.Label(card, "LanguageLabel", "LANGUAGE", "NGÔN NGỮ", 40, Color.white, MidLeft, Top, new Vector2(-60, -500), new Vector2(400, 50));
        LanguageRow(card, Top, new Vector2(0, -565), 254, 24);
        Button leave = UiKit.Button(card, "LeaveButton", "LEAVE ROOM", null, Bottom, new Vector2(0, 80), new Vector2(520, 84), K.Danger, 28);
        UiKit.Wire(menu, "panel", panel.gameObject);
        UiKit.Wire(menu, "resumeButton", resume);
        UiKit.Wire(menu, "volumeSlider", slider);
        UiKit.Wire(menu, "volumeValueText", volumeValue);
        UiKit.Wire(menu, "fullScreenButton", fullScreen);
        UiKit.Wire(menu, "leaveButton", leave);
        panel.gameObject.SetActive(false);
    }

    private static Slider PixelSlider(Transform parent, Vector2 position, Vector2 size)
    {
        RectTransform root = UiKit.Rect(parent, "VolumeSlider", Top, position, size);
        Image back = UiKit.Panel(root, "Background", C, Vector2.zero, size);
        var backRect = (RectTransform)back.transform;
        backRect.anchorMin = Vector2.zero;
        backRect.anchorMax = Vector2.one;
        backRect.offsetMin = backRect.offsetMax = Vector2.zero;
        RectTransform fillArea = UiKit.Stretch(root, "Fill Area");
        fillArea.offsetMin = new Vector2(6, 6);
        fillArea.offsetMax = new Vector2(-6, -6);
        Image fill = UiKit.Box(fillArea, "Fill", C, Vector2.zero, Vector2.zero, UiKit.Orange);
        var fillRect = (RectTransform)fill.transform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
        RectTransform handleArea = UiKit.Stretch(root, "Handle Slide Area");
        handleArea.offsetMin = new Vector2(10, 0);
        handleArea.offsetMax = new Vector2(-10, 0);
        RectTransform handle = UiKit.Rect(handleArea, "Handle", C, Vector2.zero, new Vector2(24, 20));
        handle.anchorMin = new Vector2(0, 0);
        handle.anchorMax = new Vector2(0, 1);
        Image handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.sprite = UiKit.ButtonSprite;
        handleImage.type = Image.Type.Sliced;
        handleImage.pixelsPerUnitMultiplier = 0.8f;
        Slider slider = root.gameObject.AddComponent<Slider>();
        slider.fillRect = fillRect;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        Navigation navigation = slider.navigation;
        navigation.mode = Navigation.Mode.None;
        slider.navigation = navigation;
        return slider;
    }
}
