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
/// 메인 타이틀 씬을 만들고, 게임 씬(NetworkDemo)에 자동 시작 스크립트를 붙이고,
/// Build Settings 순서를 [MainTitle, NetworkDemo, Stage2]로 맞춘다.
/// 메뉴: Tools > Title > Setup Main Title
/// </summary>
public static class TitleSceneSetup
{
    private const string TitlePath = "Assets/Scenes/MainTitle.unity";
    private const string DemoPath = "Assets/Scenes/NetworkDemo.unity";
    private const string Stage2Path = "Assets/Scenes/Stage2_Campground.unity";

    private static Font uiFont;

    [MenuItem("Tools/Title/Setup Main Title")]
    public static void SetupMainTitle()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        // 1) 게임 씬에 NetworkAutoLaunch 부착
        var demo = EditorSceneManager.OpenScene(DemoPath, OpenSceneMode.Single);
        GameObject bootstrap = GameObject.Find("NetworkBootstrap");
        if (bootstrap == null)
        {
            EditorUtility.DisplayDialog("NetworkBootstrap 없음", "NetworkDemo에 NetworkBootstrap이 없습니다. Phase 0을 먼저 실행하세요.", "OK");
            return;
        }
        if (bootstrap.GetComponent<NetworkAutoLaunch>() == null)
            bootstrap.AddComponent<NetworkAutoLaunch>();
        EditorSceneManager.MarkSceneDirty(demo);
        EditorSceneManager.SaveScene(demo);

        // 2) 타이틀 씬 생성
        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var title = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        BuildTitleUI();
        EditorSceneManager.SaveScene(title, TitlePath);

        // 3) Build Settings 순서: 타이틀이 첫 씬 (Stage2는 존재할 때만 포함)
        var order = new List<string> { TitlePath, DemoPath };
        if (System.IO.File.Exists(Stage2Path))
            order.Add(Stage2Path);
        SetBuildOrder(order.ToArray());

        Debug.Log("[Title] 메인 타이틀 씬 생성 완료. Build Settings 첫 씬 = MainTitle. 재생하면 타이틀부터 시작합니다.");
    }

    private static void BuildTitleUI()
    {
        // 배경 카메라 (화면을 어둡게 클리어)
        var camGo = new GameObject("Main Camera");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.07f, 0.08f, 0.11f);
        camGo.AddComponent<AudioListener>();
        camGo.tag = "MainCamera";

        // EventSystem (새 Input System용 모듈)
        var esGo = new GameObject("EventSystem");
        esGo.AddComponent<EventSystem>();
        var uiModule = esGo.AddComponent<InputSystemUIInputModule>();
        uiModule.AssignDefaultActions();

        // Canvas
        var canvasGo = new GameObject("MainMenuCanvas", typeof(RectTransform));
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        canvasGo.AddComponent<MainMenu>();
        Transform root = canvasGo.transform;

        // 장식용 카드 패널
        var panel = MakeImage(root, "Panel", new Vector2(0f, 0f), new Vector2(720f, 820f), new Color(0f, 0f, 0f, 0.35f));

        // 타이틀 / 부제
        MakeText(root, "Title", "마검 탈출", new Vector2(0f, 300f), new Vector2(1200f, 160f), 96, new Color(1f, 0.85f, 0.4f), true);
        MakeText(root, "Subtitle", "캠핑 대소동", new Vector2(0f, 210f), new Vector2(1000f, 80f), 40, new Color(0.9f, 0.9f, 0.95f), false);
        MakeText(root, "Tagline", "친구를 믿지 마세요. 마검은 하나뿐입니다.", new Vector2(0f, 150f), new Vector2(1000f, 40f), 22, new Color(0.7f, 0.7f, 0.75f), false);

        // 접속 주소 입력
        MakeText(root, "AddressLabel", "접속 주소", new Vector2(0f, 60f), new Vector2(360f, 30f), 20, new Color(0.7f, 0.7f, 0.75f), false);
        MakeInputField(root, "AddressField", "127.0.0.1", new Vector2(0f, 20f), new Vector2(360f, 52f));

        // 버튼들
        MakeButton(root, "HostButton", "게임 시작 (호스트)", new Vector2(0f, -60f), new Vector2(360f, 66f), new Color(0.78f, 0.32f, 0.24f));
        MakeButton(root, "JoinButton", "참가하기", new Vector2(0f, -140f), new Vector2(360f, 66f), new Color(0.24f, 0.42f, 0.58f));
        MakeButton(root, "QuitButton", "나가기", new Vector2(0f, -220f), new Vector2(360f, 56f), new Color(0.28f, 0.28f, 0.32f));

        // 상태 텍스트
        MakeText(root, "Status", string.Empty, new Vector2(0f, -300f), new Vector2(900f, 40f), 22, new Color(0.85f, 0.85f, 0.5f), false);
    }

    // ---------------------------------------------------------------- UI 헬퍼

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
        var rt = NewRect(parent, name, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private static Text MakeText(Transform parent, string name, string text, Vector2 pos, Vector2 size, int fontSize, Color color, bool bold)
    {
        var rt = NewRect(parent, name, pos, size);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = uiFont;
        t.text = text;
        t.fontSize = fontSize;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = color;
        t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    private static void MakeButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, Color bg)
    {
        var rt = NewRect(parent, name, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = bg;
        var btn = rt.gameObject.AddComponent<Button>();

        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.fadeDuration = 0.08f;
        btn.colors = colors;

        var labelT = MakeText(rt, "Label", label, Vector2.zero, size, 26, Color.white, true);
        StretchToParent(labelT.rectTransform);
    }

    private static void MakeInputField(Transform parent, string name, string defaultText, Vector2 pos, Vector2 size)
    {
        var rt = NewRect(parent, name, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.12f);
        var field = rt.gameObject.AddComponent<InputField>();

        var placeholder = MakeText(rt, "Placeholder", defaultText, Vector2.zero, size, 22, new Color(1f, 1f, 1f, 0.4f), false);
        placeholder.alignment = TextAnchor.MiddleCenter;
        StretchToParent(placeholder.rectTransform, 12f);

        var textT = MakeText(rt, "Text", string.Empty, Vector2.zero, size, 22, Color.white, false);
        textT.alignment = TextAnchor.MiddleCenter;
        textT.supportRichText = false;
        StretchToParent(textT.rectTransform, 12f);

        field.textComponent = textT;
        field.placeholder = placeholder;
        field.text = defaultText;
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

    private static void SetBuildOrder(params string[] firstScenes)
    {
        var ordered = new List<EditorBuildSettingsScene>();
        foreach (string path in firstScenes)
            ordered.Add(new EditorBuildSettingsScene(path, true));

        // 기존에 있던 다른 씬들은 뒤에 유지
        foreach (var s in EditorBuildSettings.scenes)
            if (!firstScenes.Contains(s.path))
                ordered.Add(s);

        EditorBuildSettings.scenes = ordered.ToArray();
    }
}
#endif
