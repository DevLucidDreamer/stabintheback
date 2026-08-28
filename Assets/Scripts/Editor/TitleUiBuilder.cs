#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// 타이틀 화면의 UI를 만든다. TitleSceneSetup이 배경을 세운 뒤 호출한다.
///
/// 디자인 방향 — 게임이 저폴리라 UI도 '판때기'로 통일했다.
///   · 둥근 모서리·그라데이션 버튼 없음. 각진 판 + 아래로 떨어지는 단색 그림자.
///   · 버튼은 왼쪽 색 띠로 성격을 구분한다(빨강=진행, 초록=참가, 회색=보조).
///   · 로고는 왼쪽 정렬 2단 — STAB IN THE / BACK. 두 줄의 왼쪽 끝이 같은 x에 오도록
///     둘 다 화면 왼쪽 위 기준으로 같은 anchoredPosition.x를 쓴다.
///   · 3D 배경 위에 글씨가 얹히므로 왼쪽에 어두운 그라데이션을 깔아 가독성을 확보한다.
///
/// 오브젝트 이름은 MainMenu.cs가 찾아 쓰는 계약이다. 바꾸면 양쪽을 같이 고쳐야 한다.
/// </summary>
public static class TitleUiBuilder
{
    // ---- 색 ----
    private static readonly Color Ink = Color.white;

    /// <summary>BACK 전용 빨강. 형광에 가까운 순색(#FF0000)이 아닌 평범한 빨강.</summary>
    private static readonly Color TitleRed = new Color(0.878f, 0.192f, 0.192f);

    private static readonly Color Dim = new Color(0.72f, 0.74f, 0.78f);
    private static readonly Color Warm = new Color(1f, 0.86f, 0.60f);
    private static readonly Color PanelDark = new Color(0.07f, 0.075f, 0.095f, 0.97f);
    private static readonly Color Scrim = new Color(0.02f, 0.025f, 0.04f, 0.72f);
    private static readonly Color IdleFill = new Color(0.11f, 0.12f, 0.15f, 0.92f);
    private static readonly Color HoverFill = new Color(0.19f, 0.20f, 0.24f, 0.97f);
    private static readonly Color Green = new Color(0.33f, 0.76f, 0.47f);
    private static readonly Color Gray = new Color(0.52f, 0.55f, 0.60f);

    // ---- 앵커 프리셋 ----
    private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
    private static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
    private static readonly Vector2 Middle = new Vector2(0.5f, 0.5f);
    private static readonly Vector2 BottomLeft = new Vector2(0f, 0f);
    private static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);

    /// <summary>로고와 버튼이 공유하는 왼쪽 여백.</summary>
    private const float Margin = 150f;

    private static TMP_FontAsset font;

    public static void Build(TMP_FontAsset fontAsset)
    {
        font = fontAsset;
        if (font == null)
            Debug.LogWarning("[Title] Jalnan2 SDF 폰트를 찾지 못해 TMP 기본 폰트로 만듭니다. " +
                             "'Tools > Fonts > Create Jalnan2 TMP Font Asset'을 먼저 실행하세요.");

        var eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();

        var canvasGo = new GameObject("MainMenuCanvas", typeof(RectTransform));
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();
        canvasGo.AddComponent<MainMenu>();

        BuildBackdrop(canvasGo.transform);
        BuildTitlePanel(canvasGo.transform);
        BuildPlayPanel(canvasGo.transform);
        BuildOptionsPanel(canvasGo.transform);
    }

    // ---------------------------------------------------------------- 배경 위 가독성 레이어

    /// <summary>
    /// 3D 배경 위에 흰 글씨를 얹으려면 뒤가 어두워야 한다.
    /// 왼쪽에서 오른쪽으로 사라지는 그라데이션이라 캠핑장은 그대로 보인다.
    /// </summary>
    private static void BuildBackdrop(Transform root)
    {
        RectTransform panel = FullPanel(root, "Backdrop");

        RectTransform left = NewRect(panel, "LeftFade", new Vector2(0f, 0f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(1150f, 0f));
        left.anchorMax = new Vector2(0f, 1f); // 세로로만 늘어난다
        left.sizeDelta = new Vector2(1150f, 0f);
        left.anchoredPosition = Vector2.zero; // 앵커를 바꾸면 위치가 따라 틀어지므로 다시 잡는다
        var leftImage = left.gameObject.AddComponent<Image>();
        leftImage.raycastTarget = false;
        var leftGradient = left.gameObject.AddComponent<UIGradient>();
        leftGradient.direction = UIGradient.Direction.Horizontal;
        leftGradient.from = new Color(0.02f, 0.025f, 0.04f, 0.88f);
        leftGradient.to = new Color(0.02f, 0.025f, 0.04f, 0f);

        RectTransform bottom = NewRect(panel, "BottomFade", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 260f));
        bottom.anchorMin = new Vector2(0f, 0f);
        bottom.anchorMax = new Vector2(1f, 0f); // 가로로만 늘어난다
        bottom.sizeDelta = new Vector2(0f, 260f);
        bottom.anchoredPosition = Vector2.zero;
        var bottomImage = bottom.gameObject.AddComponent<Image>();
        bottomImage.raycastTarget = false;
        var bottomGradient = bottom.gameObject.AddComponent<UIGradient>();
        bottomGradient.direction = UIGradient.Direction.Vertical;
        bottomGradient.from = new Color(0.02f, 0.025f, 0.04f, 0.7f);
        bottomGradient.to = new Color(0.02f, 0.025f, 0.04f, 0f);
    }

    // ---------------------------------------------------------------- 타이틀

    private static void BuildTitlePanel(Transform root)
    {
        RectTransform panel = FullPanel(root, "TitlePanel");

        // 로고 2단. 두 줄 모두 왼쪽 위 기준 x = Margin 이라 BACK이 STAB 바로 아래에 온다.
        MakeText(panel, "TitleLine1", "STAB IN THE", TopLeft, TopLeft,
            new Vector2(Margin, -150f), new Vector2(1500f, 150f), 128f, Ink, TextAlignmentOptions.Left)
            .characterSpacing = 6f;

        MakeText(panel, "TitleLine2", "BACK", TopLeft, TopLeft,
            new Vector2(Margin, -250f), new Vector2(1500f, 250f), 208f, TitleRed, TextAlignmentOptions.Left)
            .characterSpacing = 2f;

        // 로고 밑줄. 설정 문구는 두지 않는다 — 세계관 설명 없이 로고와 메뉴만 보여 준다.
        MakeImage(panel, "TitleRule", TopLeft, TopLeft, new Vector2(Margin + 6f, -496f), new Vector2(460f, 9f), TitleRed);

        MakeButton(panel, "GameStartButton", "게임 시작", TopLeft, TopLeft,
            new Vector2(Margin, -580f), new Vector2(400f, 84f), TitleRed, 32f, primary: true, centerLabel: false);
        MakeButton(panel, "OptionsButton", "옵션", TopLeft, TopLeft,
            new Vector2(Margin, -684f), new Vector2(400f, 84f), Gray, 32f, primary: false, centerLabel: false);
        MakeButton(panel, "QuitButton", "종료", TopLeft, TopLeft,
            new Vector2(Margin, -788f), new Vector2(400f, 84f), Gray, 32f, primary: false, centerLabel: false);

        MakeText(panel, "BuildNote", "Made with Unity 6 · Mirror", BottomLeft, BottomLeft,
            new Vector2(Margin, 44f), new Vector2(700f, 36f), 20f, new Color(0.6f, 0.62f, 0.66f), TextAlignmentOptions.Left);
    }

    // ---------------------------------------------------------------- 게임 시작 (호스트 / 참가)

    private static void BuildPlayPanel(Transform root)
    {
        RectTransform panel = FullPanel(root, "PlayPanel");
        MakeScrim(panel);

        MakeText(panel, "PlayHeading", "게임 시작", TopCenter, TopCenter,
            new Vector2(0f, -110f), new Vector2(900f, 90f), 64f, Ink, TextAlignmentOptions.Center);
        MakeText(panel, "PlaySubheading", "방을 직접 열거나, 친구가 연 방에 들어가세요", TopCenter, TopCenter,
            new Vector2(0f, -200f), new Vector2(1100f, 44f), 24f, Dim, TextAlignmentOptions.Center);

        // ---- 왼쪽: 방 만들기 ----
        RectTransform hostCard = MakeCard(panel, "HostCard", new Vector2(-330f, -20f), new Vector2(600f, 560f), TitleRed);

        MakeText(hostCard, "HostTitle", "방 만들기", TopCenter, TopCenter,
            new Vector2(0f, -44f), new Vector2(520f, 62f), 42f, Ink, TextAlignmentOptions.Center);
        MakeText(hostCard, "HostDesc",
            "새 방을 열고 방장이 됩니다.\n\n" +
            "방을 열면 Unity Relay 코드가 나옵니다.\n" +
            "친구에게 코드를 알려주세요.\n\n" +
            "같은 공유기에 있다면 코드 없이\n'빠른 참가'로도 들어올 수 있습니다.",
            TopCenter, TopCenter, new Vector2(0f, -130f), new Vector2(500f, 300f), 24f, Dim, TextAlignmentOptions.Top, wrap: true);

        MakeButton(hostCard, "HostButton", "방 열기", BottomCenter, BottomCenter,
            new Vector2(0f, 44f), new Vector2(480f, 88f), TitleRed, 34f, primary: true, centerLabel: true);

        // ---- 오른쪽: 참가하기 ----
        RectTransform joinCard = MakeCard(panel, "JoinCard", new Vector2(330f, -20f), new Vector2(600f, 560f), Green);

        MakeText(joinCard, "JoinTitle", "참가하기", TopCenter, TopCenter,
            new Vector2(0f, -44f), new Vector2(520f, 62f), 42f, Ink, TextAlignmentOptions.Center);
        MakeText(joinCard, "CodeLabel", "방 코드", TopCenter, TopCenter,
            new Vector2(0f, -122f), new Vector2(480f, 40f), 24f, Dim, TextAlignmentOptions.Center);

        MakeInput(joinCard, "CodeField", "ABC123", TopCenter, TopCenter,
            new Vector2(0f, -166f), new Vector2(480f, 92f));

        MakeButton(joinCard, "JoinButton", "코드로 참가", TopCenter, TopCenter,
            new Vector2(0f, -272f), new Vector2(480f, 76f), Gray, 30f, primary: false, centerLabel: true);

        MakeText(joinCard, "JoinOr", "— 또는 —", TopCenter, TopCenter,
            new Vector2(0f, -362f), new Vector2(480f, 40f), 22f, new Color(0.55f, 0.57f, 0.62f), TextAlignmentOptions.Center);

        MakeText(joinCard, "QuickHint", "같은 공유기 안에서 열려 있는 방을 찾아 바로 들어갑니다",
            TopCenter, TopCenter, new Vector2(0f, -404f), new Vector2(500f, 60f), 20f, Dim, TextAlignmentOptions.Top, wrap: true);

        MakeButton(joinCard, "QuickJoinButton", "빠른 참가", BottomCenter, BottomCenter,
            new Vector2(0f, 44f), new Vector2(480f, 88f), Green, 34f, primary: true, centerLabel: true);

        MakeText(panel, "Status", string.Empty, Middle, Middle,
            new Vector2(0f, -336f), new Vector2(1400f, 50f), 26f, Warm, TextAlignmentOptions.Center);

        MakeButton(panel, "BackFromPlay", "뒤로", BottomCenter, BottomCenter,
            new Vector2(0f, 80f), new Vector2(260f, 72f), Gray, 28f, primary: false, centerLabel: true);
    }

    // ---------------------------------------------------------------- 옵션

    private static void BuildOptionsPanel(Transform root)
    {
        RectTransform panel = FullPanel(root, "OptionsPanel");
        MakeScrim(panel);

        MakeText(panel, "OptionsHeading", "옵션", TopCenter, TopCenter,
            new Vector2(0f, -110f), new Vector2(900f, 90f), 64f, Ink, TextAlignmentOptions.Center);

        MakeButton(panel, "SoundsButton", "소리", Middle, Middle,
            new Vector2(-500f, 120f), new Vector2(300f, 76f), TitleRed, 28f, primary: false, centerLabel: false);
        MakeButton(panel, "LanguageButton", "언어", Middle, Middle,
            new Vector2(-500f, 30f), new Vector2(300f, 76f), TitleRed, 28f, primary: false, centerLabel: false);
        MakeButton(panel, "CreditButton", "만든 사람", Middle, Middle,
            new Vector2(-500f, -60f), new Vector2(300f, 76f), TitleRed, 28f, primary: false, centerLabel: false);

        RectTransform card = MakeCard(panel, "OptionContent", new Vector2(230f, 10f), new Vector2(760f, 440f), Gray);

        // 소리
        RectTransform sounds = FullPanel(card, "SoundsContent");
        MakeText(sounds, "SoundsLabel", "마스터 볼륨", Middle, Middle,
            new Vector2(0f, 110f), new Vector2(600f, 50f), 32f, Ink, TextAlignmentOptions.Center);
        MakeSlider(sounds, "VolumeSlider", Middle, Middle, new Vector2(0f, 30f), new Vector2(520f, 26f), 1f);
        MakeText(sounds, "VolumeValue", "100%", Middle, Middle,
            new Vector2(0f, -30f), new Vector2(300f, 44f), 26f, Warm, TextAlignmentOptions.Center);

        // 언어
        RectTransform language = FullPanel(card, "LanguageContent");
        MakeText(language, "LanguageLabel", "표시 언어", Middle, Middle,
            new Vector2(0f, 130f), new Vector2(600f, 50f), 32f, Ink, TextAlignmentOptions.Center);
        MakeText(language, "LanguageValue", "한국어", Middle, Middle,
            new Vector2(0f, 78f), new Vector2(600f, 50f), 30f, Warm, TextAlignmentOptions.Center);
        MakeButton(language, "KoreanButton", "한국어", Middle, Middle,
            new Vector2(-118f, 0f), new Vector2(212f, 64f), Gray, 26f, primary: false, centerLabel: true);
        MakeButton(language, "EnglishButton", "English", Middle, Middle,
            new Vector2(118f, 0f), new Vector2(212f, 64f), Gray, 26f, primary: false, centerLabel: true);
        MakeText(language, "LanguageNote", "선택은 저장되지만 문구 교체는 아직 준비 중입니다.", Middle, Middle,
            new Vector2(0f, -90f), new Vector2(700f, 44f), 20f, Dim, TextAlignmentOptions.Center);

        // 만든 사람
        RectTransform credit = FullPanel(card, "CreditContent");
        MakeText(credit, "CreditText",
            "STAB IN THE BACK\n캠핑장에서 벌어지는 배신 파티게임\n\nMade with Unity 6 · Mirror",
            Middle, Middle, new Vector2(0f, 0f), new Vector2(700f, 320f), 26f, Ink, TextAlignmentOptions.Center, wrap: true);

        MakeButton(panel, "BackFromOptions", "뒤로", BottomCenter, BottomCenter,
            new Vector2(0f, 80f), new Vector2(260f, 72f), Gray, 28f, primary: false, centerLabel: true);
    }

    // ---------------------------------------------------------------- UI 헬퍼

    /// <summary>부모 전체를 덮는 빈 사각형.</summary>
    private static RectTransform FullPanel(Transform parent, string name)
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

    private static RectTransform NewRect(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    private static void MakeScrim(Transform parent)
    {
        RectTransform rt = FullPanel(parent, "Scrim");
        var image = rt.gameObject.AddComponent<Image>();
        image.color = Scrim;
        // 뒤쪽 패널로 클릭이 새지 않도록 레이캐스트는 켜 둔다.
    }

    private static Image MakeImage(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color color)
    {
        RectTransform rt = NewRect(parent, name, anchor, pivot, pos, size);
        var image = rt.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI MakeText(Transform parent, string name, string content,
        Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size,
        float fontSize, Color color, TextAlignmentOptions alignment, bool wrap = false)
    {
        RectTransform rt = NewRect(parent, name, anchor, pivot, pos, size);

        var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null)
            text.font = font;
        text.text = content;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }

    /// <summary>제목 띠가 달린 어두운 판. 호스트/참가/옵션 내용이 올라간다.</summary>
    private static RectTransform MakeCard(Transform parent, string name, Vector2 pos, Vector2 size, Color topBar)
    {
        RectTransform rt = NewRect(parent, name, Middle, Middle, pos, size);

        var background = rt.gameObject.AddComponent<Image>();
        background.color = PanelDark;

        RectTransform bar = NewRect(rt, "TopBar", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 10f));
        bar.anchorMin = new Vector2(0f, 1f);
        bar.anchorMax = new Vector2(1f, 1f);
        bar.sizeDelta = new Vector2(0f, 10f);
        bar.anchoredPosition = Vector2.zero;
        var barImage = bar.gameObject.AddComponent<Image>();
        barImage.color = topBar;
        barImage.raycastTarget = false;

        return rt;
    }

    /// <summary>
    /// 판때기 버튼. 실제 클릭 판정은 이 오브젝트(고정)가 받고,
    /// 눈에 보이는 판(Body)만 TitleButton이 움직인다 — 가장자리에서 떨지 않게 하려는 것.
    /// </summary>
    private static Button MakeButton(Transform parent, string name, string label,
        Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size,
        Color accentColor, float fontSize, bool primary, bool centerLabel)
    {
        RectTransform root = NewRect(parent, name, anchor, pivot, pos, size);

        var hit = root.gameObject.AddComponent<Image>();
        hit.color = new Color(1f, 1f, 1f, 0f); // 보이지 않지만 클릭은 받는다

        var button = root.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = hit;

        RectTransform body = FullPanel(root, "Body");

        Image shadow = StretchImage(body, "Shadow", new Color(0f, 0f, 0f, 0.45f));
        shadow.rectTransform.offsetMin = new Vector2(7f, -7f);
        shadow.rectTransform.offsetMax = new Vector2(7f, -7f);

        Color idle = primary ? accentColor : IdleFill;
        Color hover = primary ? Lighten(accentColor, 0.16f) : HoverFill;
        Image fill = StretchImage(body, "Fill", idle);

        RectTransform accent = NewRect(body, "Accent", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(8f, 0f));
        accent.anchorMin = new Vector2(0f, 0f);
        accent.anchorMax = new Vector2(0f, 1f);
        accent.sizeDelta = new Vector2(8f, 0f);
        accent.anchoredPosition = Vector2.zero;
        var accentImage = accent.gameObject.AddComponent<Image>();
        accentImage.color = primary ? new Color(1f, 1f, 1f, 0.8f) : accentColor;
        accentImage.raycastTarget = false;

        TextMeshProUGUI text = MakeText(body, name + "Label", label, Middle, Middle, Vector2.zero, size,
            fontSize, Ink, centerLabel ? TextAlignmentOptions.Center : TextAlignmentOptions.Left);
        Stretch(text.rectTransform, centerLabel ? 0f : 40f, centerLabel ? 0f : 20f);

        var motion = root.gameObject.AddComponent<TitleButton>();
        motion.body = body;
        motion.fill = fill;
        motion.accent = accent;
        motion.label = text;
        motion.idleFill = idle;
        motion.hoverFill = hover;
        motion.idleLabel = primary ? Ink : new Color(0.88f, 0.89f, 0.92f);
        motion.hoverLabel = Ink;

        return button;
    }

    private static void MakeInput(Transform parent, string name, string placeholderText,
        Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        RectTransform root = NewRect(parent, name, anchor, pivot, pos, size);

        var background = root.gameObject.AddComponent<Image>();
        background.color = new Color(1f, 1f, 1f, 0.08f);

        var field = root.gameObject.AddComponent<TMP_InputField>();

        RectTransform viewport = FullPanel(root, "Text Area");
        viewport.offsetMin = new Vector2(20f, 8f);
        viewport.offsetMax = new Vector2(-20f, -8f);
        viewport.gameObject.AddComponent<RectMask2D>();

        TextMeshProUGUI placeholder = MakeText(viewport, name + "Placeholder", placeholderText,
            Middle, Middle, Vector2.zero, size, 38f, new Color(1f, 1f, 1f, 0.28f), TextAlignmentOptions.Center);
        Stretch(placeholder.rectTransform, 0f, 0f);

        TextMeshProUGUI text = MakeText(viewport, name + "Text", string.Empty,
            Middle, Middle, Vector2.zero, size, 38f, Ink, TextAlignmentOptions.Center);
        Stretch(text.rectTransform, 0f, 0f);
        text.richText = false;

        field.textViewport = viewport;
        field.textComponent = text;
        field.placeholder = placeholder;
        field.richText = false;
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.characterLimit = 15; // Unity Relay 참가 코드
        field.onFocusSelectAll = true;
        field.customCaretColor = true;
        field.caretColor = Ink;
        field.selectionColor = new Color(0.878f, 0.192f, 0.192f, 0.5f);
        field.targetGraphic = background;
        field.text = string.Empty;
    }

    private static void MakeSlider(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, float value)
    {
        RectTransform root = NewRect(parent, name, anchor, pivot, pos, size);
        var slider = root.gameObject.AddComponent<Slider>();

        Image background = StretchImage(root, "SliderBackground", new Color(1f, 1f, 1f, 0.14f));
        background.raycastTarget = true;

        RectTransform fillArea = FullPanel(root, "Fill Area");
        RectTransform fill = FullPanel(fillArea, "Fill");
        var fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.color = TitleRed;
        fillImage.raycastTarget = false;

        slider.targetGraphic = background;
        slider.fillRect = fill;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = value;
    }

    private static Image StretchImage(Transform parent, string name, Color color)
    {
        RectTransform rt = FullPanel(parent, name);
        var image = rt.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>부모에 꽉 채우되 좌우 여백만 준다.</summary>
    private static void Stretch(RectTransform rt, float left, float right)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left, 0f);
        rt.offsetMax = new Vector2(-right, 0f);
    }

    private static Color Lighten(Color color, float amount)
        => new Color(
            Mathf.Clamp01(color.r + amount),
            Mathf.Clamp01(color.g + amount),
            Mathf.Clamp01(color.b + amount),
            color.a);
}
#endif
