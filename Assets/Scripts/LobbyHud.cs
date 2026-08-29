using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// 대기실 화면 UI. <see cref="LobbyManager"/>와 같은 오브젝트에 붙는다.
///
/// 늘 보이는 정보(방 코드·인원·카운트다운)는 공용 <see cref="GameHud"/>에 얹고,
/// 호스트만 여는 '방 옵션' 창은 여기서 직접 만든다 —
/// 버튼을 눌러야 하므로 클릭을 받는 캔버스가 따로 필요하기 때문이다.
///
/// 창은 처음 열 때 한 번만 만들고, 그다음부터는 켜고 끄기만 한다.
/// </summary>
[RequireComponent(typeof(LobbyManager))]
public class LobbyHud : MonoBehaviour
{
    private static readonly Color Ink = new Color(0.95f, 0.95f, 0.97f);
    private static readonly Color Gold = new Color(1f, 0.88f, 0.5f);
    private static readonly Color Dim = new Color(0.75f, 0.78f, 0.85f);
    private static readonly Color PanelBg = new Color(0.09f, 0.09f, 0.12f, 0.96f);
    private static readonly Color ButtonBg = new Color(0.22f, 0.23f, 0.28f, 1f);

    private LobbyManager lobby;
    private TMP_FontAsset font;

    private GameObject optionsRoot;
    private TextMeshProUGUI valueLabel;
    private TextMeshProUGUI noteLabel;
    private TextMeshProUGUI stageLabel;
    private Button minusButton;
    private Button plusButton;

    private int shownCountdown = -2;

    private void Awake()
    {
        lobby = GetComponent<LobbyManager>();
        font = GameHud.ResolveFont();
    }

    private void OnDisable()
    {
        // 대기실을 떠날 때 문구가 다음 씬에 남지 않게 지운다.
        GameHud hud = GameHud.Current;
        if (hud != null)
        {
            hud.SetTopLeft(string.Empty);
            hud.SetTopRight(string.Empty);
            hud.SetGoal(string.Empty);
        }
    }

    private void Update()
    {
        if (lobby == null)
            return;

        GameHud hud = GameHud.Ensure();

        string code = lobby.RoomCodeText();
        hud.SetTopLeft(string.IsNullOrEmpty(code) ? string.Empty : $"방 코드\n<size=140%><b>{code}</b></size>");
        hud.SetTopRight($"{lobby.SelectedStageName}\n인원  {lobby.PlayerCount} / {lobby.TargetPlayers}");

        int left = lobby.CountdownSecondsLeft;
        if (left >= 0)
        {
            hud.SetGoal($"출발까지  {left}");

            // 남은 초가 바뀔 때마다 한 번씩 크게 알린다.
            if (left != shownCountdown && left <= 3)
                hud.ShowToast(left > 0 ? left.ToString() : "출발!", 0.9f, Gold);
            shownCountdown = left;
        }
        else
        {
            shownCountdown = -2;
            int missing = Mathf.Max(0, lobby.TargetPlayers - lobby.PlayerCount);
            hud.SetGoal(missing > 0 ? $"{missing}명 더 모이면 출발" : "곧 출발합니다");
        }

        if (optionsRoot != null && optionsRoot.activeSelf)
            RefreshOptions();
    }

    // ---- 방 옵션 창 --------------------------------------------------------

    /// <summary>호스트가 Tab을 눌렀을 때 LobbyManager가 호출한다.</summary>
    public void SetOptionsVisible(bool visible)
    {
        if (visible && optionsRoot == null)
            BuildOptions();

        if (optionsRoot == null)
            return;

        optionsRoot.SetActive(visible);
        if (visible)
            RefreshOptions();
    }

    private void RefreshOptions()
    {
        if (valueLabel == null)
            return;

        valueLabel.text = lobby.TargetPlayers.ToString();
        if (stageLabel != null)
            stageLabel.text = lobby.SelectedStageName;

        // 이미 들어와 있는 인원보다 적게는 내릴 수 없다.
        int lowest = Mathf.Max(lobby.MinPlayers, lobby.PlayerCount);
        minusButton.interactable = lobby.TargetPlayers > lowest;
        plusButton.interactable = lobby.TargetPlayers < lobby.MaxPlayers;

        noteLabel.text =
            $"{lobby.TargetPlayers}명이 모이면 자동으로 출발합니다.\n" +
            $"조절 범위 {lobby.MinPlayers}~{lobby.MaxPlayers}명 · 현재 접속 {lobby.PlayerCount}명";
    }

    private void BuildOptions()
    {
        EnsureEventSystem();

        optionsRoot = new GameObject("LobbyOptionsCanvas", typeof(RectTransform));
        optionsRoot.transform.SetParent(transform, false);

        var canvas = optionsRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200; // HUD보다 위

        var scaler = optionsRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        optionsRoot.AddComponent<GraphicRaycaster>();

        var root = (RectTransform)optionsRoot.transform;

        // 뒤를 어둡게 깔아 창에 눈이 가게 한다.
        var backdrop = Panel(root, "Backdrop", Vector2.zero, new Vector2(0f, 0f), new Color(0f, 0f, 0f, 0.55f));
        backdrop.anchorMin = Vector2.zero;
        backdrop.anchorMax = Vector2.one;
        backdrop.offsetMin = Vector2.zero;
        backdrop.offsetMax = Vector2.zero;

        RectTransform panel = Panel(root, "OptionsPanel", new Vector2(680f, 500f), Vector2.zero, PanelBg);

        Label(panel, "Title", "방 옵션", 44f, Gold, TextAlignmentOptions.Center,
            new Vector2(620f, 60f), new Vector2(0f, 190f));

        Label(panel, "StageTitle", "플레이할 스테이지", 26f, Dim, TextAlignmentOptions.Left,
            new Vector2(260f, 44f), new Vector2(-160f, 120f));
        stageLabel = Label(panel, "StageValue", lobby.SelectedStageName, 28f, Ink, TextAlignmentOptions.Center,
            new Vector2(270f, 52f), new Vector2(90f, 120f));
        MakeButton(panel, "ChangeStage", "변경", new Vector2(110f, 52f), new Vector2(255f, 120f),
            () => lobby.RequestCycleStage());

        Label(panel, "TargetLabel", "정원 (명)", 30f, Ink, TextAlignmentOptions.Left,
            new Vector2(240f, 50f), new Vector2(-160f, 38f));

        minusButton = MakeButton(panel, "Minus", "−", new Vector2(70f, 70f), new Vector2(30f, 38f),
            () => lobby.RequestTargetPlayers(lobby.TargetPlayers - 1));

        valueLabel = Label(panel, "Value", "4", 46f, Color.white, TextAlignmentOptions.Center,
            new Vector2(110f, 70f), new Vector2(125f, 38f));

        plusButton = MakeButton(panel, "Plus", "+", new Vector2(70f, 70f), new Vector2(220f, 38f),
            () => lobby.RequestTargetPlayers(lobby.TargetPlayers + 1));

        noteLabel = Label(panel, "Note", string.Empty, 22f, Dim, TextAlignmentOptions.Center,
            new Vector2(600f, 80f), new Vector2(0f, -72f));

        MakeButton(panel, "Close", "닫기", new Vector2(180f, 58f), new Vector2(0f, -190f),
            () => lobby.CloseOptions());
    }

    /// <summary>버튼을 누르려면 EventSystem이 있어야 한다. 대기실 씬에는 없으므로 만들어 준다.</summary>
    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        // 이 프로젝트는 새 Input System만 쓴다. 예전 StandaloneInputModule은 동작하지 않는다.
        go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    // ---- 부품 만들기 -------------------------------------------------------

    private static RectTransform Panel(RectTransform parent, string name, Vector2 size, Vector2 pos, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var image = go.AddComponent<Image>();
        image.color = color;

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        return rt;
    }

    private TextMeshProUGUI Label(RectTransform parent, string name, string text, float size, Color color,
        TextAlignmentOptions align, Vector2 rectSize, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var label = go.AddComponent<TextMeshProUGUI>();
        if (font != null)
            label.font = font;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = align;
        label.raycastTarget = false;

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = rectSize;
        rt.anchoredPosition = pos;
        return label;
    }

    private Button MakeButton(RectTransform parent, string name, string text, Vector2 size, Vector2 pos,
        UnityEngine.Events.UnityAction onClick)
    {
        RectTransform rt = Panel(parent, name, size, pos, ButtonBg);

        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = rt.GetComponent<Image>();
        button.onClick.AddListener(onClick);

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.25f, 1.25f, 1.3f, 1f);
        colors.pressedColor = new Color(0.75f, 0.75f, 0.8f, 1f);
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        button.colors = colors;

        Label(rt, "Text", text, size.y * 0.5f, Ink, TextAlignmentOptions.Center, size, Vector2.zero);
        return button;
    }
}
