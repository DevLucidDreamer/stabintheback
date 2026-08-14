using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 손에 든 캠핑 체크리스트 클립보드.
///
/// - E키: 화면 왼쪽 아래에서 클립보드를 들어올려 목록 확인 (다시 누르면 내림)
/// - 재료를 챙기면 잠깐 올라와 체크된 것을 보여 준다
///
/// 화면 구석의 요약은 HUD(<see cref="CampHud"/>)가 늘 띄우고 있고,
/// 이 클립보드는 "직접 들여다보는" 맛을 위한 물건이다.
/// 글자는 TMP라 한글이 또렷하게 나온다.
/// </summary>
public class ItemChecklist : MonoBehaviour
{
    [Header("클립보드 연출")]
    [Tooltip("올리고 내리는 데 걸리는 시간(초)")]
    [SerializeField] private float animTime = 0.22f;

    [Tooltip("재료를 챙겼을 때 자동으로 보여주는 시간(초)")]
    [SerializeField] private float peekDuration = 1.6f;

    private Camera cam;
    private Transform clipboard;
    private TextMeshPro listText;
    private bool shown;       // E키로 고정해서 보는 중인지
    private float t;          // 0 = 내려감, 1 = 올라옴
    private float peekTimer;  // 재료 획득 시 잠깐 보여주기
    private bool inputEnabled = true;

    private readonly StringBuilder sb = new StringBuilder(200);

    // 카메라 기준 클립보드 위치/회전 (왼쪽 아래에서 올라오는 연출)
    private static readonly Vector3 ShownPos = new Vector3(-0.22f, -0.16f, 0.5f);
    private static readonly Vector3 HiddenPos = new Vector3(-0.3f, -0.75f, 0.55f);
    private static readonly Quaternion ShownRot = Quaternion.Euler(-18f, 12f, 4f);
    private static readonly Quaternion HiddenRot = Quaternion.Euler(50f, 12f, 4f);

    private void OnEnable() => CampGameManager.OnChanged += HandleChanged;

    private void OnDisable() => CampGameManager.OnChanged -= HandleChanged;

    private void Start()
    {
        cam = GetComponentInChildren<Camera>(true);
        if (cam != null)
            BuildClipboard();

        RefreshText();
    }

    /// <summary>클립보드를 잠깐 올려 보여준다.</summary>
    public void Peek()
    {
        if (!shown)
            peekTimer = peekDuration;
    }

    /// <summary>
    /// 진행 상황이 바뀌면 글자를 고친다.
    /// 재료를 모으는 동안에만 저절로 들어 올린다 — 굽는 중에는 칸을 올리고 내릴 때마다
    /// 클립보드가 튀어나와 시야를 가린다.
    /// </summary>
    private void HandleChanged()
    {
        RefreshText();

        CampGameManager game = CampGameManager.Instance;
        if (game == null || game.IsGathering)
            Peek();
    }

    private void Update()
    {
        if (clipboard == null)
            return;

        if (inputEnabled && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame
            && Cursor.lockState == CursorLockMode.Locked)
        {
            shown = !shown;
            peekTimer = 0f;
        }

        if (peekTimer > 0f)
            peekTimer -= Time.deltaTime;

        float target = (shown || peekTimer > 0f) && inputEnabled ? 1f : 0f;
        if (!Mathf.Approximately(t, target))
        {
            t = Mathf.MoveTowards(t, target, Time.deltaTime / Mathf.Max(0.01f, animTime));
            float s = Mathf.SmoothStep(0f, 1f, t);
            clipboard.localPosition = Vector3.Lerp(HiddenPos, ShownPos, s);
            clipboard.localRotation = Quaternion.Slerp(HiddenRot, ShownRot, s);
        }

        bool visible = t > 0.001f;
        if (clipboard.gameObject.activeSelf != visible)
            clipboard.gameObject.SetActive(visible);
    }

    // ---- 글자 -------------------------------------------------------------

    private void RefreshText()
    {
        if (listText == null)
            return;

        CampGameManager game = CampGameManager.Instance;
        if (game == null)
        {
            // 대기실 등 목표가 없는 씬.
            listText.text = "< 캠핑 준비 >\n\n여기선 챙길 게 없다.\n출발하면 시작된다.";
            return;
        }

        sb.Clear();

        if (game.Phase == CampPhase.Gathering || game.Phase == CampPhase.Dusk)
        {
            sb.Append("< 캠핑 재료 >\n\n");
            Line("장작", game.FirewoodHave, game.FirewoodNeeded);
            Line("고기", game.MeatHave, game.MeatNeeded);
            Line("야채", game.VegetableHave, game.VegetableNeeded);

            sb.Append('\n');
            sb.Append(game.GatheringComplete
                ? "다 모았다!\n해가 넘어간다."
                : "해가 지기 전에\n전부 찾아내자.");
        }
        else
        {
            sb.Append("< 바베큐 >\n\n");
            Line("장작 투입", game.FirewoodLoaded, game.FirewoodNeeded);
            Line("구운 것", game.CookedCount, game.CookTarget);

            sb.Append('\n');
            sb.Append(game.FireLit
                ? $"남은 재료\n고기 {game.MeatAvailable} · 야채 {game.VegetableAvailable}"
                : "화로에 장작을\n전부 넣어야 한다.");
        }

        listText.text = sb.ToString();
    }

    private void Line(string label, int have, int need)
    {
        sb.Append(have >= need ? "■ " : "□ ")
          .Append(label)
          .Append("  ")
          .Append(have).Append('/').Append(need)
          .Append('\n');
    }

    // ---- 클립보드 생성 -----------------------------------------------------

    private void BuildClipboard()
    {
        clipboard = new GameObject("Clipboard").transform;
        clipboard.SetParent(cam.transform, false);
        clipboard.localPosition = HiddenPos;
        clipboard.localRotation = HiddenRot;

        Part("Board", new Vector3(0f, 0f, 0f), new Vector3(0.30f, 0.42f, 0.014f), new Color(0.45f, 0.30f, 0.18f));
        Part("Paper", new Vector3(0f, -0.005f, -0.009f), new Vector3(0.26f, 0.37f, 0.004f), new Color(0.96f, 0.95f, 0.90f));
        Part("Clip", new Vector3(0f, 0.19f, -0.014f), new Vector3(0.10f, 0.035f, 0.02f), new Color(0.65f, 0.66f, 0.68f));

        // TMP_Text는 RectTransform이 필요하다. 만들 때 같이 붙여야 한다.
        var textGo = new GameObject("ListText", typeof(RectTransform));
        textGo.transform.SetParent(clipboard, false);
        textGo.transform.localPosition = new Vector3(0f, 0f, -0.012f);

        var rt = textGo.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0.24f, 0.34f);

        listText = textGo.AddComponent<TextMeshPro>();
        TMP_FontAsset font = GameHud.ResolveFont();
        if (font != null)
            listText.font = font;

        listText.alignment = TextAlignmentOptions.TopLeft;
        listText.color = new Color(0.12f, 0.10f, 0.10f);
        listText.textWrappingMode = TextWrappingModes.Normal;
        listText.enableAutoSizing = true;
        listText.fontSizeMin = 0.06f;
        listText.fontSizeMax = 0.34f;

        clipboard.gameObject.SetActive(false);
    }

    /// <summary>클립보드 부품 큐브 하나. 콜라이더는 제거해 상호작용 레이캐스트를 막지 않는다.</summary>
    private void Part(string partName, Vector3 pos, Vector3 size, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = partName;
        go.transform.SetParent(clipboard, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        Destroy(go.GetComponent<Collider>());

        var mr = go.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.material = PipelineShaders.CreateLit(color);
    }

    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
        if (!enabled)
        {
            shown = false;
            peekTimer = 0f;
        }
    }
}
