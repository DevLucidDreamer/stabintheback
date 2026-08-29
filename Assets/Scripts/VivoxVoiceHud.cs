using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// 로컬 Vivox 연결/마이크 상태를 화면 오른쪽 아래에 표시하고 음소거를 제어한다.
/// 오픈 마이크는 M 또는 버튼으로 토글하고, PTT 모드는 지정 키 안내만 표시한다.
/// </summary>
public sealed class VivoxVoiceHud : MonoBehaviour
{
    private static VivoxVoiceHud instance;

    public static VivoxVoiceHud Current => instance;

    public static VivoxVoiceHud Ensure()
    {
        if (instance != null)
            return instance;

        instance = FindFirstObjectByType<VivoxVoiceHud>(FindObjectsInactive.Include);
        if (instance != null)
            return instance;

        return new GameObject("VivoxVoiceHud").AddComponent<VivoxVoiceHud>();
    }

    private readonly Color connectedColor = new Color(0.28f, 0.86f, 0.55f);
    private readonly Color mutedColor = new Color(1f, 0.48f, 0.36f);
    private readonly Color waitingColor = new Color(1f, 0.79f, 0.32f);

    private VivoxProximityVoice voice;
    private Button microphoneButton;
    private Image buttonBackground;
    private TextMeshProUGUI stateLabel;
    private string lastState;

    public void Bind(VivoxProximityVoice target)
    {
        voice = target;
        gameObject.SetActive(true);
        Refresh(force: true);
    }

    public void Unbind(VivoxProximityVoice target)
    {
        if (voice != target)
            return;

        voice = null;
        gameObject.SetActive(false);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        Build();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Update() => Refresh();

    private void ToggleMute()
    {
        if (voice == null || !voice.IsConnected || voice.IsPushToTalk)
            return;

        voice.ToggleMicrophoneMute();
        Refresh(force: true);
    }

    private void Refresh(bool force = false)
    {
        if (voice == null || stateLabel == null)
            return;

        string state;
        Color color;
        bool interactable = false;

        if (voice.IsPushToTalk)
        {
            state = voice.IsConnected
                ? $"마이크  PTT · {voice.PushToTalkKey} 누르는 동안 송신"
                : "음성 채팅 연결 중...";
            color = voice.IsConnected ? connectedColor : waitingColor;
        }
        else if (voice.ConnectionFailed)
        {
            state = "음성 채팅 연결 실패";
            color = mutedColor;
        }
        else if (!voice.IsConnected)
        {
            state = voice.IsConnecting ? "음성 채팅 연결 중..." : "음성 채팅 연결 안 됨";
            color = waitingColor;
        }
        else if (voice.IsMicrophoneMuted)
        {
            state = $"마이크 꺼짐 · {voice.MuteToggleKey}로 켜기";
            color = mutedColor;
            interactable = true;
        }
        else
        {
            state = $"마이크 켜짐 · {voice.MuteToggleKey}로 끄기";
            color = connectedColor;
            interactable = true;
        }

        if (!force && state == lastState)
            return;

        lastState = state;
        stateLabel.text = state;
        stateLabel.color = color;
        microphoneButton.interactable = interactable;
        buttonBackground.color = new Color(color.r * 0.22f, color.g * 0.22f, color.b * 0.22f, 0.9f);
    }

    private void Build()
    {
        EnsureEventSystem();

        var canvasObject = new GameObject("VoiceCanvas", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;
        canvasObject.AddComponent<GraphicRaycaster>();

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var buttonObject = new GameObject("MicrophoneButton", typeof(RectTransform));
        buttonObject.transform.SetParent(canvasObject.transform, false);
        var rect = (RectTransform)buttonObject.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-28f, 28f);
        rect.sizeDelta = new Vector2(390f, 58f);

        buttonBackground = buttonObject.AddComponent<Image>();
        microphoneButton = buttonObject.AddComponent<Button>();
        microphoneButton.targetGraphic = buttonBackground;
        microphoneButton.onClick.AddListener(ToggleMute);

        var labelObject = new GameObject("State", typeof(RectTransform));
        labelObject.transform.SetParent(buttonObject.transform, false);
        stateLabel = labelObject.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset font = GameHud.ResolveFont();
        if (font != null)
            stateLabel.font = font;
        stateLabel.fontSize = 23f;
        stateLabel.alignment = TextAlignmentOptions.Center;
        stateLabel.raycastTarget = false;

        var labelRect = (RectTransform)labelObject.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(14f, 6f);
        labelRect.offsetMax = new Vector2(-14f, -6f);
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }
}
