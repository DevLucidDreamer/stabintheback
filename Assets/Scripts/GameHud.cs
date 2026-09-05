using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 게임 화면 UI를 한 곳에서 그리는 HUD.
///
/// 예전에는 각 스크립트가 저마다 OnGUI로 글자를 찍었다. IMGUI는 내장 폰트라
/// 한글이 흐릿하고, 화면 크기가 바뀌면 글자만 그대로여서 배치가 무너지고,
/// 무엇보다 어디서 무엇을 그리는지 흩어져 있어 손대기 어려웠다.
/// 이제 전부 TMP 캔버스 하나로 모은다.
///
/// 캔버스는 필요할 때 <see cref="Ensure"/>가 런타임에 만든다 — 씬마다 UI를 심어 둘
/// 필요가 없고, 씬을 다시 구워도 UI가 사라지지 않는다.
///
/// 글꼴은 TMP 기본 폰트(Jalnan2 SDF)를 쓴다.
/// 'Tools > Fonts > Set Jalnan2 SDF as TMP Default'가 지정해 두며,
/// 혹시 지정이 풀렸으면 씬에 이미 있는 TMP 글자(팻말 등)에서 폰트를 빌려 온다.
/// </summary>
public class GameHud : MonoBehaviour
{
    private static GameHud instance;

    /// <summary>없으면 만들어서 돌려준다.</summary>
    public static GameHud Ensure()
    {
        if (instance != null)
            return instance;

        instance = FindFirstObjectByType<GameHud>(FindObjectsInactive.Include);
        if (instance != null)
            return instance;

        var go = new GameObject("GameHud");
        instance = go.AddComponent<GameHud>();
        return instance;
    }

    /// <summary>이미 만들어져 있으면 돌려준다(없으면 만들지 않는다).</summary>
    public static GameHud Current => instance;

    // 색
    private static readonly Color Ink = new Color(0.96f, 0.96f, 0.98f);
    private static readonly Color Gold = new Color(1f, 0.85f, 0.42f);
    private static readonly Color Panel = new Color(0.05f, 0.05f, 0.07f, 0.55f);

    private TMP_FontAsset font;

    private RectTransform crosshair;
    private TextMeshProUGUI goalLabel;
    private TextMeshProUGUI promptLabel;
    private TextMeshProUGUI topLeftLabel;
    private TextMeshProUGUI topRightLabel;
    private TextMeshProUGUI bottomLabel;
    private TextMeshProUGUI toastLabel;
    private TextMeshProUGUI bannerLabel;
    private TextMeshProUGUI deathLabel;
    private bool deathScreen;
    private RectTransform staminaBar;
    private Image staminaFill;
    private TextMeshProUGUI weaponLabel;
    private float weaponLabelUntil;

    private RectTransform goalPanel;
    private RectTransform topLeftPanel;
    private RectTransform topRightPanel;

    private float toastUntil;
    private float bannerUntil;

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

    private void LateUpdate()
    {
        // 중복 인스턴스는 Awake에서 스스로를 파괴한다. 그 프레임에는 아직 여기가 돌 수 있다.
        if (toastLabel == null || bannerLabel == null)
            return;

        if (toastLabel.gameObject.activeSelf && Time.time >= toastUntil)
            toastLabel.gameObject.SetActive(false);

        if (bannerLabel.gameObject.activeSelf && Time.time >= bannerUntil)
            bannerLabel.gameObject.SetActive(false);
        if (weaponLabel != null && Time.time >= weaponLabelUntil) weaponLabel.gameObject.SetActive(false);
    }

    // ---- 바깥에서 쓰는 창구 -------------------------------------------------

    /// <summary>조준점 표시. 커서가 풀려 있으면(메뉴 중) 감춘다.</summary>
    public void SetCrosshair(bool visible)
    {
        if (crosshair != null && crosshair.gameObject.activeSelf != visible)
            crosshair.gameObject.SetActive(visible);
    }

    /// <summary>조준한 대상 안내. 빈 문자열이면 감춘다.</summary>
    public void SetPrompt(string text) => Set(promptLabel, text);

    /// <summary>화면 위 가운데의 현재 목표.</summary>
    public void SetGoal(string text)
    {
        Set(goalLabel, text);
        if (goalPanel != null)
            goalPanel.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }

    /// <summary>왼쪽 위 정보(방 코드, 재료 목록 등). 여러 줄 가능.</summary>
    public void SetTopLeft(string text)
    {
        Set(topLeftLabel, text);
        if (topLeftPanel != null)
            topLeftPanel.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }

    /// <summary>오른쪽 위 정보(인원 등).</summary>
    public void SetTopRight(string text)
    {
        Set(topRightLabel, text);
        if (topRightPanel != null)
            topRightPanel.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }

    /// <summary>화면 아래 조작 힌트.</summary>
    public void SetBottom(string text) => Set(bottomLabel, text);

    /// <summary>화면 가운데에 잠깐 크게 띄우는 알림(피격, 획득 등).</summary>
    public void ShowToast(string text, float seconds = 1.8f, Color? color = null)
    {
        if (deathScreen) return;
        if (string.IsNullOrEmpty(text))
            return;

        toastLabel.text = text;
        toastLabel.color = color ?? Gold;
        toastLabel.gameObject.SetActive(true);
        toastUntil = Time.time + seconds;
    }

    /// <summary>페이즈 전환처럼 판을 가르는 큰 알림.</summary>
    public void ShowBanner(string text, float seconds = 3.5f, Color? color = null)
    {
        if (deathScreen) return;
        if (string.IsNullOrEmpty(text))
            return;

        bannerLabel.text = text;
        bannerLabel.color = color ?? Gold;
        bannerLabel.gameObject.SetActive(true);
        bannerUntil = Time.time + seconds;
    }

    /// <summary>씬을 옮길 때처럼 화면을 한 번에 비운다.</summary>
    public void Clear()
    {
        SetDeathScreen(false);
        SetPrompt(string.Empty);
        SetGoal(string.Empty);
        SetTopLeft(string.Empty);
        SetTopRight(string.Empty);
        SetBottom(string.Empty);
        toastLabel.gameObject.SetActive(false);
        bannerLabel.gameObject.SetActive(false);
    }

    // ---- 캔버스 만들기 -----------------------------------------------------

    public void SetDeathScreen(bool visible)
    {
        deathScreen = visible;
        if (deathLabel != null) deathLabel.gameObject.SetActive(visible);
        if (visible)
        {
            if (staminaBar != null) staminaBar.gameObject.SetActive(false);
            if (weaponLabel != null) weaponLabel.gameObject.SetActive(false);
            toastLabel.gameObject.SetActive(false);
            bannerLabel.gameObject.SetActive(false);
            SetCrosshair(false);
            SetPrompt(string.Empty);
        }
    }

    private void Build()
    {
        font = ResolveFont();

        var canvasGo = new GameObject("HudCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // GraphicRaycaster를 붙이지 않는다 — 이 캔버스는 보여 주기만 하고
        // 클릭을 가로채면 안 된다(조준·상호작용이 막힌다).

        var root = (RectTransform)canvasGo.transform;

        BuildCrosshair(root);
        staminaBar = MakePanel(root, "StaminaBar", new Vector2(.5f, 0), new Vector2(0, 28), new Vector2(320, 8));
        var fill = new GameObject("StaminaFill", typeof(RectTransform)); fill.transform.SetParent(staminaBar, false);
        staminaFill = fill.AddComponent<Image>(); staminaFill.raycastTarget = false;
        staminaFill.rectTransform.anchorMin = Vector2.zero; staminaFill.rectTransform.anchorMax = Vector2.one;
        staminaFill.rectTransform.offsetMin = staminaFill.rectTransform.offsetMax = Vector2.zero;
        staminaBar.gameObject.SetActive(false);
        weaponLabel = MakeLabel(root, "EquippedWeapon", 24f, Ink, TextAlignmentOptions.Center);
        Anchor(weaponLabel.rectTransform, new Vector2(.5f, 0), new Vector2(0, 65), new Vector2(440, 40));
        weaponLabel.gameObject.SetActive(false);

        // 위 가운데: 현재 목표
        goalPanel = MakePanel(root, "GoalPanel", new Vector2(0.5f, 1f), new Vector2(0f, -58f), new Vector2(760f, 64f));
        goalLabel = MakeLabel(goalPanel, "Goal", 38f, Gold, TextAlignmentOptions.Center);

        // 왼쪽 위: 재료/방 정보
        topLeftPanel = MakePanel(root, "TopLeftPanel", new Vector2(0f, 1f), new Vector2(228f, -132f), new Vector2(420f, 220f));
        topLeftLabel = MakeLabel(topLeftPanel, "TopLeft", 27f, Ink, TextAlignmentOptions.TopLeft, new Vector4(22f, 14f, 22f, 14f));

        // 오른쪽 위: 인원 등
        topRightPanel = MakePanel(root, "TopRightPanel", new Vector2(1f, 1f), new Vector2(-160f, -56f), new Vector2(280f, 60f));
        topRightLabel = MakeLabel(topRightPanel, "TopRight", 28f, Ink, TextAlignmentOptions.Center);

        // 가운데 아래: 조준 대상 안내
        promptLabel = MakeLabel(root, "Prompt", 28f, Ink, TextAlignmentOptions.Center);
        Anchor(promptLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -92f), new Vector2(900f, 76f));

        // 화면 아래: 조작 힌트
        bottomLabel = MakeLabel(root, "Bottom", 24f, new Color(0.82f, 0.84f, 0.9f, 0.9f), TextAlignmentOptions.Center);
        Anchor(bottomLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 52f), new Vector2(1200f, 68f));

        // 가운데: 짧은 알림
        toastLabel = MakeLabel(root, "Toast", 54f, Gold, TextAlignmentOptions.Center);
        Anchor(toastLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(1400f, 90f));
        toastLabel.gameObject.SetActive(false);

        // 위쪽: 판을 가르는 큰 알림
        bannerLabel = MakeLabel(root, "Banner", 76f, Gold, TextAlignmentOptions.Center);
        Anchor(bannerLabel.rectTransform, new Vector2(0.5f, 0.72f), Vector2.zero, new Vector2(1600f, 130f));
        bannerLabel.gameObject.SetActive(false);

        deathLabel = MakeLabel(root, "Wasted", 132f, new Color(0.85f, 0.035f, 0.035f), TextAlignmentOptions.Center);
        deathLabel.text = "WASTED";
        deathLabel.fontStyle = FontStyles.Bold;
        deathLabel.characterSpacing = 8f;
        Anchor(deathLabel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1600f, 210f));
        deathLabel.gameObject.SetActive(false);

        Clear();
    }

    /// <summary>가운데 조준점. 밝은 배경에서도 보이도록 어두운 테두리를 깐다.</summary>
    public void SetStamina(float value, bool exhausted, bool visible)
    {
        if (staminaBar == null) return;
        staminaBar.gameObject.SetActive(visible && !deathScreen);
        staminaFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
        staminaFill.color = exhausted ? new Color(.72f, .38f, .28f) : new Color(.75f, .82f, .81f);
    }
    public void ShowWeaponName(string name)
    {
        if (weaponLabel == null || deathScreen) return;
        weaponLabel.text = name; weaponLabel.gameObject.SetActive(true); weaponLabelUntil = Time.time + 1.3f;
    }
    private void BuildCrosshair(RectTransform root)
    {
        var go = new GameObject("Crosshair", typeof(RectTransform));
        go.transform.SetParent(root, false);
        crosshair = (RectTransform)go.transform;
        Anchor(crosshair, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14f, 14f));

        Dot(crosshair, "Outline", 10f, new Color(0f, 0f, 0f, 0.55f));
        Dot(crosshair, "Dot", 5f, new Color(1f, 1f, 1f, 0.9f));
    }

    private static void Dot(RectTransform parent, string name, float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        Anchor((RectTransform)go.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
    }

    private RectTransform MakePanel(RectTransform parent, string name, Vector2 anchor, Vector2 offset, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var image = go.AddComponent<Image>();
        image.color = Panel;
        image.raycastTarget = false;

        var rt = (RectTransform)go.transform;
        Anchor(rt, anchor, offset, size);
        return rt;
    }

    private TextMeshProUGUI MakeLabel(RectTransform parent, string name, float size, Color color,
        TextAlignmentOptions align, Vector4 margin = default)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var label = go.AddComponent<TextMeshProUGUI>();
        if (font != null)
            label.font = font;
        label.fontSize = size;
        label.color = color;
        label.alignment = align;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.margin = margin;
        label.text = string.Empty;

        // 부모(패널)를 꽉 채우게 두면 패널 크기만 조절하면 된다.
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        return label;
    }

    private static void Anchor(RectTransform rt, Vector2 anchor, Vector2 offset, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = offset;
        rt.sizeDelta = size;
    }

    private static void Set(TextMeshProUGUI label, string text)
    {
        if (label == null)
            return;

        text ??= string.Empty;
        if (label.text != text)
            label.text = text;

        bool show = text.Length > 0;
        if (label.gameObject.activeSelf != show)
            label.gameObject.SetActive(show);
    }

    /// <summary>
    /// 런타임에 만드는 TMP 글자가 쓸 폰트를 정한다.
    /// TMP 기본 폰트가 한글을 못 그리는 것(LiberationSans)뿐이면,
    /// 씬에 이미 놓인 TMP 글자(팻말 등)에서 폰트를 빌려 온다.
    /// </summary>
    public static TMP_FontAsset ResolveFont()
    {
        TMP_FontAsset fallback = TMP_Settings.defaultFontAsset;
        if (fallback != null && fallback.name.IndexOf("Liberation", System.StringComparison.OrdinalIgnoreCase) < 0)
            return fallback;

        foreach (TMP_Text text in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (text.font != null && text.font.name.IndexOf("Liberation", System.StringComparison.OrdinalIgnoreCase) < 0)
                return text.font;
        }

        return fallback;
    }
}
