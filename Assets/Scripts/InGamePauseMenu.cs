using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 로컬 플레이어 전용 ESC 메뉴. 네트워크 시간은 멈추지 않고 이 플레이어의 입력만 잠시 멈춘다.
/// 음성·사운드 설정과 조작법을 한 화면에 모아 플레이 HUD를 단순하게 유지한다.
/// </summary>
public sealed class InGamePauseMenu : MonoBehaviour
{
    private static InGamePauseMenu instance;

    public static InGamePauseMenu Current => instance;
    public static bool IsOpen => instance != null && instance.open;

    public static InGamePauseMenu Ensure()
    {
        if (instance != null)
            return instance;

        instance = FindFirstObjectByType<InGamePauseMenu>(FindObjectsInactive.Include);
        if (instance != null)
            return instance;

        return new GameObject("InGamePauseMenu").AddComponent<InGamePauseMenu>();
    }

    private static readonly Color Ink = new Color(0.95f, 0.96f, 0.98f);
    private static readonly Color Dim = new Color(0.68f, 0.72f, 0.8f);
    private static readonly Color Accent = new Color(0.34f, 0.78f, 0.66f);
    private static readonly Color PanelColor = new Color(0.055f, 0.06f, 0.08f, 0.98f);
    private static readonly Color ButtonColor = new Color(0.16f, 0.18f, 0.23f, 1f);
    private static readonly Color SelectedColor = new Color(0.18f, 0.48f, 0.42f, 1f);

    private NetworkPlayerSetup owner;
    private PlayerController controller;
    private VivoxProximityVoice voice;
    private TMP_FontAsset font;

    private GameObject menuRoot;
    private GameObject settingsPage;
    private GameObject controlsPage;
    private bool open;

    private TextMeshProUGUI voiceState;
    private TextMeshProUGUI muteButtonLabel;
    private TextMeshProUGUI masterValue;
    private TextMeshProUGUI sfxValue;
    private Button muteButton;
    private Image pttBackground;
    private Image openMicBackground;

    public void Bind(NetworkPlayerSetup targetOwner, PlayerController targetController, VivoxProximityVoice targetVoice)
    {
        owner = targetOwner;
        controller = targetController;
        voice = targetVoice;
        SetOpen(false);
        RefreshVoice();
    }

    public void Unbind(NetworkPlayerSetup targetOwner)
    {
        if (owner != targetOwner)
            return;

        SetOpen(false);
        owner = null;
        controller = null;
        voice = null;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        font = GameHud.ResolveFont();
        Build();
        SetOpen(false);
    }

    private void OnDestroy()
    {
        if (open && controller != null)
            controller.SetInputPaused(false);
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            LobbyManager lobby = LobbyManager.Instance;
            if (lobby != null && lobby.OptionsOpen)
            {
                lobby.CloseOptions();
                return;
            }

            SetOpen(!open);
        }

        if (open)
            RefreshVoice();
    }

    private void SetOpen(bool value)
    {
        open = value && owner != null;
        if (menuRoot != null)
            menuRoot.SetActive(open);
        if (controller != null)
            controller.SetInputPaused(open);
        else if (open)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (open)
        {
            ShowSettings();
            RefreshVoice();
        }
    }

    private void ShowSettings()
    {
        if (settingsPage != null) settingsPage.SetActive(true);
        if (controlsPage != null) controlsPage.SetActive(false);
    }

    private void ShowControls()
    {
        if (settingsPage != null) settingsPage.SetActive(false);
        if (controlsPage != null) controlsPage.SetActive(true);
    }

    private void SetVoiceMode(bool pushToTalk)
    {
        GameOptions.SetPushToTalk(pushToTalk);
        if (voice != null)
            voice.SetPushToTalk(pushToTalk);
        RefreshVoice();
    }

    private void ToggleMute()
    {
        if (voice == null || !voice.IsConnected)
            return;
        voice.ToggleMicrophoneMute();
        RefreshVoice();
    }

    private void RefreshVoice()
    {
        bool ptt = voice != null ? voice.IsPushToTalk : GameOptions.PushToTalk;
        if (pttBackground != null) pttBackground.color = ptt ? SelectedColor : ButtonColor;
        if (openMicBackground != null) openMicBackground.color = ptt ? ButtonColor : SelectedColor;

        if (voiceState == null)
            return;

        if (voice == null)
        {
            voiceState.text = "음성 채팅 준비 중";
            voiceState.color = Dim;
        }
        else if (voice.ConnectionFailed)
        {
            voiceState.text = "음성 채팅 연결 실패";
            voiceState.color = new Color(1f, 0.5f, 0.42f);
        }
        else if (!voice.IsConnected)
        {
            voiceState.text = "음성 채팅 연결 중";
            voiceState.color = Dim;
        }
        else
        {
            voiceState.text = voice.IsUserMuted
                ? "현재 마이크 음소거"
                : ptt ? "V를 누르는 동안 송신" : "항상 켜짐";
            voiceState.color = voice.IsUserMuted ? new Color(1f, 0.58f, 0.45f) : Accent;
        }

        bool connected = voice != null && voice.IsConnected;
        if (muteButton != null) muteButton.interactable = connected;
        if (muteButtonLabel != null)
            muteButtonLabel.text = connected && voice.IsUserMuted ? "음소거 해제" : "마이크 음소거";
    }

    private void ReturnToMainMenu()
    {
        SetOpen(false);

        if (NetworkManager.singleton != null)
        {
            if (NetworkServer.active)
                NetworkManager.singleton.StopHost();
            else if (NetworkClient.active)
                NetworkManager.singleton.StopClient();
        }

        if (SceneManager.GetActiveScene().name != "MainTitle")
            SceneManager.LoadScene("MainTitle");
    }

    private void Build()
    {
        EnsureEventSystem();

        menuRoot = new GameObject("PauseMenuCanvas", typeof(RectTransform));
        menuRoot.transform.SetParent(transform, false);
        Canvas canvas = menuRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;
        menuRoot.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = menuRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = (RectTransform)menuRoot.transform;
        RectTransform backdrop = Panel(root, "Backdrop", Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.7f));
        backdrop.anchorMin = Vector2.zero;
        backdrop.anchorMax = Vector2.one;
        backdrop.offsetMin = Vector2.zero;
        backdrop.offsetMax = Vector2.zero;

        RectTransform panel = Panel(root, "MenuPanel", new Vector2(820f, 820f), Vector2.zero, PanelColor);
        Label(panel, "Title", "일시정지", 48f, Ink, TextAlignmentOptions.Center,
            new Vector2(740f, 66f), new Vector2(0f, 350f));

        MakeButton(panel, "SettingsTab", "설정", new Vector2(250f, 54f), new Vector2(-135f, 287f), ShowSettings);
        MakeButton(panel, "ControlsTab", "조작방법", new Vector2(250f, 54f), new Vector2(135f, 287f), ShowControls);

        settingsPage = new GameObject("SettingsPage", typeof(RectTransform));
        settingsPage.transform.SetParent(panel, false);
        Stretch((RectTransform)settingsPage.transform, new Vector2(36f, 112f), new Vector2(-36f, -118f));
        BuildSettings((RectTransform)settingsPage.transform);

        controlsPage = new GameObject("ControlsPage", typeof(RectTransform));
        controlsPage.transform.SetParent(panel, false);
        Stretch((RectTransform)controlsPage.transform, new Vector2(36f, 112f), new Vector2(-36f, -118f));
        BuildControls((RectTransform)controlsPage.transform);

        MakeButton(panel, "Resume", "계속하기", new Vector2(250f, 58f), new Vector2(-135f, -356f), () => SetOpen(false));
        MakeButton(panel, "MainMenu", "메인 메뉴", new Vector2(250f, 58f), new Vector2(135f, -356f), ReturnToMainMenu,
            new Color(0.42f, 0.16f, 0.17f, 1f));
    }

    private void BuildSettings(RectTransform page)
    {
        Label(page, "VoiceHeader", "음성 채팅", 30f, Accent, TextAlignmentOptions.Left,
            new Vector2(680f, 42f), new Vector2(0f, 225f));
        voiceState = Label(page, "VoiceState", "음성 채팅 준비 중", 22f, Dim, TextAlignmentOptions.Left,
            new Vector2(680f, 36f), new Vector2(0f, 182f));

        Button ptt = MakeButton(page, "PushToTalk", "눌러 말하기  [V]", new Vector2(300f, 58f),
            new Vector2(-165f, 120f), () => SetVoiceMode(true));
        pttBackground = ptt.GetComponent<Image>();
        Button always = MakeButton(page, "OpenMic", "항상 켜기", new Vector2(300f, 58f),
            new Vector2(165f, 120f), () => SetVoiceMode(false));
        openMicBackground = always.GetComponent<Image>();

        muteButton = MakeButton(page, "Mute", "마이크 음소거", new Vector2(630f, 52f),
            new Vector2(0f, 50f), ToggleMute);
        muteButtonLabel = muteButton.GetComponentInChildren<TextMeshProUGUI>();

        Label(page, "SoundHeader", "사운드", 30f, Accent, TextAlignmentOptions.Left,
            new Vector2(680f, 42f), new Vector2(0f, -28f));

        Label(page, "MasterLabel", "전체 음량", 23f, Ink, TextAlignmentOptions.Left,
            new Vector2(190f, 36f), new Vector2(-245f, -88f));
        Slider master = MakeSlider(page, "MasterVolume", new Vector2(360f, 32f), new Vector2(55f, -88f));
        masterValue = Label(page, "MasterValue", "100%", 22f, Ink, TextAlignmentOptions.Right,
            new Vector2(90f, 36f), new Vector2(310f, -88f));
        master.SetValueWithoutNotify(GameOptions.MasterVolume);
        master.onValueChanged.AddListener(value =>
        {
            GameOptions.SetMasterVolume(value);
            masterValue.text = Mathf.RoundToInt(value * 100f) + "%";
        });
        masterValue.text = Mathf.RoundToInt(GameOptions.MasterVolume * 100f) + "%";

        Label(page, "SfxLabel", "효과음", 23f, Ink, TextAlignmentOptions.Left,
            new Vector2(190f, 36f), new Vector2(-245f, -150f));
        Slider sfx = MakeSlider(page, "SfxVolume", new Vector2(360f, 32f), new Vector2(55f, -150f));
        sfxValue = Label(page, "SfxValue", "100%", 22f, Ink, TextAlignmentOptions.Right,
            new Vector2(90f, 36f), new Vector2(310f, -150f));
        sfx.SetValueWithoutNotify(GameOptions.SfxVolume);
        sfx.onValueChanged.AddListener(value =>
        {
            GameOptions.SetSfxVolume(value);
            sfxValue.text = Mathf.RoundToInt(value * 100f) + "%";
        });
        sfxValue.text = Mathf.RoundToInt(GameOptions.SfxVolume * 100f) + "%";
    }

    private void BuildControls(RectTransform page)
    {
        const string controls =
            "<color=#59C7A8><b>이동</b></color>\n" +
            "WASD / 방향키  이동     Shift  달리기     Space  점프\n\n" +
            "<color=#59C7A8><b>상호작용</b></color>\n" +
            "좌클릭  줍기·사용·공격     우클릭  무기를 든 채 사용\n" +
            "G  들고 있는 무기 내려놓기     E  준비물 목록\n\n" +
            "<color=#59C7A8><b>음성 및 메뉴</b></color>\n" +
            "V  눌러 말하기     M  마이크 음소거\n" +
            "ESC  이 메뉴 열기·닫기     Tab  방장용 대기실 옵션\n\n" +
            "화면 중앙의 안내는 현재 조준한 대상에서 가능한 행동만 표시합니다.";

        TextMeshProUGUI label = Label(page, "Controls", controls, 25f, Ink, TextAlignmentOptions.TopLeft,
            new Vector2(680f, 500f), new Vector2(0f, 5f));
        label.lineSpacing = 7f;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;
        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    private RectTransform Panel(RectTransform parent, string name, Vector2 size, Vector2 pos, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.color = color;
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        return rt;
    }

    private TextMeshProUGUI Label(RectTransform parent, string name, string text, float size, Color color,
        TextAlignmentOptions alignment, Vector2 rectSize, Vector2 pos)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.Normal;
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = rectSize;
        rt.anchoredPosition = pos;
        return label;
    }

    private Button MakeButton(RectTransform parent, string name, string text, Vector2 size, Vector2 pos,
        UnityEngine.Events.UnityAction callback, Color? color = null)
    {
        RectTransform rt = Panel(parent, name, size, pos, color ?? ButtonColor);
        Button button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = rt.GetComponent<Image>();
        button.onClick.AddListener(() =>
        {
            GameAudio.PlayUi("ui_click", 0.24f);
            callback?.Invoke();
        });
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1.16f, 1.16f, 1.16f, 1f);
        colors.pressedColor = new Color(0.75f, 0.75f, 0.78f, 1f);
        colors.disabledColor = new Color(0.45f, 0.45f, 0.48f, 0.45f);
        button.colors = colors;
        Label(rt, "Text", text, 24f, Ink, TextAlignmentOptions.Center, size, Vector2.zero);
        return button;
    }

    private Slider MakeSlider(RectTransform parent, string name, Vector2 size, Vector2 pos)
    {
        RectTransform root = Panel(parent, name, size, pos, new Color(0.12f, 0.13f, 0.16f, 1f));
        Slider slider = root.gameObject.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;

        RectTransform fillArea = Panel(root, "Fill Area", Vector2.zero, Vector2.zero, Color.clear);
        Stretch(fillArea, new Vector2(5f, 8f), new Vector2(-5f, -8f));
        RectTransform fill = Panel(fillArea, "Fill", Vector2.zero, Vector2.zero, Accent);
        Stretch(fill, Vector2.zero, Vector2.zero);

        RectTransform handleArea = Panel(root, "Handle Slide Area", Vector2.zero, Vector2.zero, Color.clear);
        Stretch(handleArea, new Vector2(10f, 0f), new Vector2(-10f, 0f));
        RectTransform handle = Panel(handleArea, "Handle", new Vector2(24f, 38f), Vector2.zero, Ink);

        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }

    private static void Stretch(RectTransform rt, Vector2 minOffset, Vector2 maxOffset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = minOffset;
        rt.offsetMax = maxOffset;
    }
}
