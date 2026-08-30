using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// 우측 하단에 마이크 상태만 작게 표시한다.
/// 자세한 음성 설정과 조작 설명은 ESC 메뉴가 담당한다.
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
        return instance != null ? instance : new GameObject("VivoxVoiceHud").AddComponent<VivoxVoiceHud>();
    }

    private static readonly Color Connected = new Color(0.3f, 0.88f, 0.58f);
    private static readonly Color Transmitting = new Color(0.35f, 0.82f, 1f);
    private static readonly Color Muted = new Color(1f, 0.4f, 0.34f);
    private static readonly Color Waiting = new Color(0.72f, 0.74f, 0.78f);

    private VivoxProximityVoice voice;
    private Button microphoneButton;
    private Image buttonBackground;
    private Image[] microphoneParts;
    private Image muteSlash;
    private int lastState = -1;

    public void Bind(VivoxProximityVoice target)
    {
        voice = target;
        gameObject.SetActive(true);
        Refresh(true);
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

    private void Update() => Refresh(false);

    private void ToggleMute()
    {
        if (voice == null || !voice.IsConnected)
            return;

        voice.ToggleMicrophoneMute();
        Refresh(true);
    }

    private void Refresh(bool force)
    {
        if (microphoneButton == null)
            return;

        bool connected = voice != null && voice.IsConnected;
        bool muted = voice == null || voice.ConnectionFailed || voice.IsUserMuted;
        bool transmitting = connected && !muted && voice.IsTransmitting;
        int state = voice != null && voice.ConnectionFailed ? 3 : muted ? 2 : transmitting ? 1 : connected ? 0 : 4;
        if (!force && state == lastState)
            return;

        lastState = state;
        Color color = state == 1 ? Transmitting : state == 0 ? Connected : state == 2 || state == 3 ? Muted : Waiting;
        microphoneButton.interactable = connected;
        buttonBackground.color = new Color(0.025f, 0.03f, 0.04f, connected ? 0.72f : 0.48f);
        foreach (Image part in microphoneParts)
            part.color = color;
        muteSlash.color = color;
        muteSlash.gameObject.SetActive(state == 2 || state == 3);
    }

    private void Build()
    {
        EnsureEventSystem();

        var canvasObject = new GameObject("VoiceCanvas", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;
        canvasObject.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var buttonObject = new GameObject("Microphone", typeof(RectTransform));
        buttonObject.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = (RectTransform)buttonObject.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-28f, 28f);
        rect.sizeDelta = new Vector2(52f, 52f);

        buttonBackground = buttonObject.AddComponent<Image>();
        microphoneButton = buttonObject.AddComponent<Button>();
        microphoneButton.targetGraphic = buttonBackground;
        microphoneButton.navigation = new Navigation { mode = Navigation.Mode.None };
        microphoneButton.onClick.AddListener(ToggleMute);

        microphoneParts = new[]
        {
            Part(rect, "Capsule", new Vector2(0f, 6f), new Vector2(12f, 24f)),
            Part(rect, "BracketLeft", new Vector2(-9f, 1f), new Vector2(3f, 17f)),
            Part(rect, "BracketRight", new Vector2(9f, 1f), new Vector2(3f, 17f)),
            Part(rect, "Stem", new Vector2(0f, -10f), new Vector2(3f, 10f)),
            Part(rect, "Base", new Vector2(0f, -16f), new Vector2(22f, 3f)),
        };
        muteSlash = Part(rect, "MutedSlash", Vector2.zero, new Vector2(34f, 3f));
        muteSlash.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -42f);
    }

    private static Image Part(RectTransform parent, string name, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = go.AddComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }
}
