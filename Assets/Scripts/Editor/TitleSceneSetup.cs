#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// 메인 타이틀 씬을 만든다. 메뉴: Tools > Title > Setup Main Title
///
/// 화면 구성
///   TitlePanel      : 제목 + Game Start / Options / Quit
///   GameStartPanel  : 방 코드 입력 + Host Game / Join
///   OptionsPanel    : Sounds / Language / Credit
/// 실제 동작은 MainMenu.cs가 이름으로 계층을 찾아 연결한다 —
/// 오브젝트 이름을 바꾸면 MainMenu의 문자열도 같이 고쳐야 한다.
///
/// Build Settings 순서도 [MainTitle, Lobby, NetworkDemo, Stage2]로 맞춘다.
/// </summary>
public static class TitleSceneSetup
{
    private const string TitlePath = "Assets/Scenes/MainTitle.unity";
    private const string LobbyPath = "Assets/Scenes/Lobby.unity";
    private const string DemoPath = "Assets/Scenes/NetworkDemo.unity";
    private const string Stage2Path = "Assets/Scenes/Stage2_Campground.unity";

    // 목업 색상
    private static readonly Color Red = new Color(0.85f, 0.31f, 0.29f);
    private static readonly Color Green = new Color(0.56f, 0.84f, 0.62f);
    private static readonly Color Neutral = new Color(0.26f, 0.27f, 0.32f);
    private static readonly Color Ink = new Color(0.93f, 0.93f, 0.95f);
    private static readonly Color Dim = new Color(0.62f, 0.63f, 0.68f);

    private static Font uiFont;

    [MenuItem("Tools/Title/Setup Main Title")]
    public static void SetupMainTitle()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        // 타이틀에서 넘어온 의도(호스트/참가)를 실행할 씬들에 자동 시작 스크립트를 보장한다.
        EnsureAutoLaunch(LobbyPath);
        EnsureAutoLaunch(DemoPath);

        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var title = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        BuildTitleUI();
        EditorSceneManager.SaveScene(title, TitlePath);

        SetBuildOrder();

        Debug.Log("[Title] 메인 타이틀 씬 생성 완료 (Title / Game Start / Options)." +
                  "\n- Build Settings 첫 씬 = MainTitle, 그 다음이 Lobby(대기실)입니다." +
                  "\n- 대기실 씬이 없다면 'Tools > Lobby > Build Lobby'를 실행하세요.");
    }

    /// <summary>해당 씬의 NetworkBootstrap에 NetworkAutoLaunch가 붙어 있게 한다.</summary>
    private static void EnsureAutoLaunch(string scenePath)
    {
        if (!System.IO.File.Exists(scenePath))
            return;

        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        GameObject bootstrap = GameObject.Find("NetworkBootstrap");
        if (bootstrap == null)
        {
            Debug.LogWarning($"[Title] {scenePath} 에 NetworkBootstrap이 없습니다. 자동 시작 연결을 건너뜁니다.");
            return;
        }

        if (bootstrap.GetComponent<NetworkAutoLaunch>() == null)
        {
            bootstrap.AddComponent<NetworkAutoLaunch>();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    // ---------------------------------------------------------------- 화면 구성

    private static void BuildTitleUI()
    {
        var camGo = new GameObject("Main Camera");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.07f, 0.08f, 0.11f);
        camGo.AddComponent<AudioListener>();
        camGo.tag = "MainCamera";

        var esGo = new GameObject("EventSystem");
        esGo.AddComponent<EventSystem>();
        esGo.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();

        var canvasGo = new GameObject("MainMenuCanvas", typeof(RectTransform));
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        canvasGo.AddComponent<MainMenu>();

        BuildTitlePanel(canvasGo.transform);
        BuildGameStartPanel(canvasGo.transform);
        BuildOptionsPanel(canvasGo.transform);
    }

    private static void BuildTitlePanel(Transform root)
    {
        RectTransform panel = Panel(root, "TitlePanel");

        MakeText(panel, "Title", "Stab in the back", new Vector2(0f, 300f), new Vector2(1700f, 200f), 118, new Color(1f, 0.85f, 0.4f), true);
        MakeText(panel, "Subtitle", "마검 탈출 · 캠핑 대소동", new Vector2(0f, 195f), new Vector2(1200f, 60f), 34, Ink, false);
        MakeText(panel, "Tagline", "친구를 믿지 마세요. 마검은 하나뿐입니다.", new Vector2(0f, 145f), new Vector2(1200f, 40f), 22, Dim, false);

        MakeButton(panel, "GameStartButton", "Game Start", new Vector2(-600f, -20f), new Vector2(400f, 72f), Red);
        MakeButton(panel, "OptionsButton", "Options", new Vector2(-600f, -108f), new Vector2(400f, 72f), Neutral);
        MakeButton(panel, "QuitButton", "Quit", new Vector2(-600f, -196f), new Vector2(400f, 72f), Neutral);
    }

    private static void BuildGameStartPanel(Transform root)
    {
        RectTransform panel = Panel(root, "GameStartPanel");

        MakeText(panel, "GameStartHeading", "Insert Code", new Vector2(0f, 250f), new Vector2(1000f, 90f), 56, Ink, true);
        MakeInputField(panel, "CodeField", "ex) AOQNDXK", new Vector2(0f, 110f), new Vector2(560f, 120f));

        MakeButton(panel, "HostButton", "Host Game", new Vector2(-145f, -50f), new Vector2(270f, 68f), Red);
        MakeButton(panel, "JoinButton", "Join", new Vector2(155f, -50f), new Vector2(210f, 68f), Green);

        MakeText(panel, "CodeHint", $"방을 열면 코드가 나옵니다. 참가할 땐 친구가 부른 영문 {RoomCode.Length}글자를 그대로 입력하세요.\n(같은 공유기에 있을 때 바로 연결됩니다. IP 주소를 직접 적어도 됩니다)",
            new Vector2(0f, -145f), new Vector2(1200f, 70f), 20, Dim, false);
        MakeText(panel, "Status", string.Empty, new Vector2(0f, -230f), new Vector2(1200f, 44f), 24, new Color(0.95f, 0.85f, 0.45f), false);

        MakeButton(panel, "BackFromGameStart", "뒤로", new Vector2(-700f, -400f), new Vector2(220f, 60f), Neutral);
    }

    private static void BuildOptionsPanel(Transform root)
    {
        RectTransform panel = Panel(root, "OptionsPanel");

        MakeText(panel, "OptionsHeading", "Options", new Vector2(0f, 320f), new Vector2(1000f, 90f), 56, Ink, true);

        MakeButton(panel, "SoundsButton", "Sounds", new Vector2(-480f, 90f), new Vector2(300f, 66f), Neutral);
        MakeButton(panel, "LanguageButton", "Language", new Vector2(-480f, 10f), new Vector2(300f, 66f), Neutral);
        MakeButton(panel, "CreditButton", "Credit", new Vector2(-480f, -70f), new Vector2(300f, 66f), Neutral);

        Image box = MakeImage(panel, "OptionContent", new Vector2(230f, 20f), new Vector2(760f, 400f), new Color(1f, 1f, 1f, 0.06f));
        Transform content = box.transform;

        // Sounds
        RectTransform sounds = Panel(content, "SoundsContent", new Vector2(760f, 400f));
        MakeText(sounds, "SoundsLabel", "마스터 볼륨", new Vector2(0f, 110f), new Vector2(600f, 50f), 30, Ink, true);
        MakeSlider(sounds, "VolumeSlider", new Vector2(0f, 30f), new Vector2(520f, 26f), 1f);
        MakeText(sounds, "VolumeValue", "100%", new Vector2(0f, -30f), new Vector2(300f, 40f), 24, Dim, false);

        // Language
        RectTransform language = Panel(content, "LanguageContent", new Vector2(760f, 400f));
        MakeText(language, "LanguageLabel", "표시 언어", new Vector2(0f, 130f), new Vector2(600f, 50f), 30, Ink, true);
        MakeText(language, "LanguageValue", "한국어", new Vector2(0f, 75f), new Vector2(600f, 50f), 30, new Color(1f, 0.85f, 0.4f), true);
        MakeButton(language, "KoreanButton", "한국어", new Vector2(-115f, 0f), new Vector2(210f, 60f), Neutral);
        MakeButton(language, "EnglishButton", "English", new Vector2(115f, 0f), new Vector2(210f, 60f), Neutral);
        MakeText(language, "LanguageNote", "선택은 저장되지만 문구 교체는 아직 준비 중입니다.", new Vector2(0f, -80f), new Vector2(700f, 40f), 19, Dim, false);

        // Credit
        RectTransform credit = Panel(content, "CreditContent", new Vector2(760f, 400f));
        MakeText(credit, "CreditText",
            "Stab in the back\n마검탈출맵 리메이크 · 병맛 파티게임\n\nMade with Unity 6 · Mirror\n원작 장르: 마인크래프트 마검탈출맵",
            new Vector2(0f, 0f), new Vector2(700f, 300f), 24, Ink, false);

        MakeButton(panel, "BackFromOptions", "뒤로", new Vector2(-700f, -400f), new Vector2(220f, 60f), Neutral);
    }

    // ---------------------------------------------------------------- UI 헬퍼

    /// <summary>화면 전체를 덮는 빈 패널(그래픽 없음). MainMenu가 켜고 끈다.</summary>
    private static RectTransform Panel(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    private static RectTransform Panel(Transform parent, string name, Vector2 size)
        => NewRect(parent, name, Vector2.zero, size);

    private static RectTransform NewRect(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    private static Image MakeImage(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
    {
        RectTransform rt = NewRect(parent, name, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private static Text MakeText(Transform parent, string name, string text, Vector2 pos, Vector2 size, int fontSize, Color color, bool bold)
    {
        RectTransform rt = NewRect(parent, name, pos, size);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = uiFont;
        t.text = text;
        t.fontSize = fontSize;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = color;
        t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    private static void MakeButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, Color bg)
    {
        RectTransform rt = NewRect(parent, name, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = bg;
        var btn = rt.gameObject.AddComponent<Button>();

        ColorBlock colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.fadeDuration = 0.08f;
        btn.colors = colors;

        Text labelText = MakeText(rt, name + "Label", label, Vector2.zero, size, 28, Color.white, true);
        StretchToParent(labelText.rectTransform);
    }

    private static void MakeInputField(Transform parent, string name, string placeholderText, Vector2 pos, Vector2 size)
    {
        RectTransform rt = NewRect(parent, name, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.1f);
        var field = rt.gameObject.AddComponent<InputField>();

        Text placeholder = MakeText(rt, name + "Placeholder", placeholderText, Vector2.zero, size, 34, new Color(1f, 1f, 1f, 0.35f), false);
        StretchToParent(placeholder.rectTransform, 16f);

        Text text = MakeText(rt, name + "Text", string.Empty, Vector2.zero, size, 34, Color.white, true);
        text.supportRichText = false;
        StretchToParent(text.rectTransform, 16f);

        field.textComponent = text;
        field.placeholder = placeholder;
        field.characterLimit = 15; // 코드 7자 또는 IP 주소(최대 15자)
        field.text = string.Empty;
    }

    private static void MakeSlider(Transform parent, string name, Vector2 pos, Vector2 size, float value)
    {
        RectTransform rt = NewRect(parent, name, pos, size);
        var slider = rt.gameObject.AddComponent<Slider>();

        Image bg = MakeImage(rt, "SliderBackground", Vector2.zero, size, new Color(1f, 1f, 1f, 0.14f));
        StretchToParent(bg.rectTransform);

        RectTransform fillArea = NewRect(rt, "Fill Area", Vector2.zero, size);
        StretchToParent(fillArea);

        RectTransform fill = NewRect(fillArea, "Fill", Vector2.zero, size);
        StretchToParent(fill);
        var fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.color = new Color(0.85f, 0.45f, 0.32f);

        slider.targetGraphic = bg;
        slider.fillRect = fill;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = value;
    }

    private static void StretchToParent(RectTransform rt, float padding = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(padding, padding);
        rt.offsetMax = new Vector2(-padding, -padding);
    }

    // ---------------------------------------------------------------- Build Settings

    private static void SetBuildOrder()
    {
        var order = new List<string>();
        order.Add(TitlePath);
        if (System.IO.File.Exists(LobbyPath)) order.Add(LobbyPath);
        if (System.IO.File.Exists(DemoPath)) order.Add(DemoPath);
        if (System.IO.File.Exists(Stage2Path)) order.Add(Stage2Path);

        var scenes = order.Select(path => new EditorBuildSettingsScene(path, true)).ToList();
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            if (!order.Contains(s.path))
                scenes.Add(s);

        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
#endif
