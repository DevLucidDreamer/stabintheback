using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 캠핑 준비물 체크리스트.
/// - 씬에 있는 모든 CollectibleItem을 시작 시 수집 목록으로 등록
/// - E키: 화면 왼쪽 아래에서 3D 클립보드를 들어올려 목록 확인 (다시 누르면 내림)
/// - 아이템을 챙기면 잠깐 클립보드가 올라와 체크된 것을 보여준다
/// 클립보드는 프리미티브 + TextMesh로 런타임에 생성한다.
/// </summary>
public class ItemChecklist : MonoBehaviour
{
    [Header("클립보드 연출")]
    [Tooltip("올리고 내리는 데 걸리는 시간(초)")]
    [SerializeField] private float animTime = 0.22f;
    [Tooltip("아이템을 챙겼을 때 자동으로 보여주는 시간(초)")]
    [SerializeField] private float peekDuration = 1.6f;

    // 아이템 이름 → 필요 개수 / 챙긴 개수
    private readonly List<string> order = new List<string>();
    private readonly Dictionary<string, int> required = new Dictionary<string, int>();
    private readonly Dictionary<string, int> collected = new Dictionary<string, int>();

    private Camera cam;
    private Transform clipboard;
    private TextMesh listText;
    private bool shown;       // E키로 고정해서 보는 중인지
    private float t;          // 0 = 내려감, 1 = 올라옴
    private float peekTimer;  // 아이템 획득 시 잠깐 보여주기
    private bool inputEnabled = true;

    // 카메라 기준 클립보드 위치/회전 (왼쪽 아래에서 올라오는 연출)
    private static readonly Vector3 ShownPos = new Vector3(-0.22f, -0.16f, 0.5f);
    private static readonly Vector3 HiddenPos = new Vector3(-0.3f, -0.75f, 0.55f);
    private static readonly Quaternion ShownRot = Quaternion.Euler(-18f, 12f, 4f);
    private static readonly Quaternion HiddenRot = Quaternion.Euler(50f, 12f, 4f);

    private void OnEnable()
    {
        // 멀티플레이 공용 진행도가 바뀌면 UI를 갱신한다.
        CampChecklistManager.OnChanged += RefreshText;
    }

    private void OnDisable()
    {
        CampChecklistManager.OnChanged -= RefreshText;
    }

    private void Start()
    {
        cam = GetComponentInChildren<Camera>(true);
        if (cam != null)
            BuildClipboard();

        // 싱글플레이 폴백용: 씬에 배치된 준비물을 전부 로컬 목록에 등록한다.
        foreach (var item in Object.FindObjectsByType<CollectibleItem>(FindObjectsSortMode.None))
            Register(item.DisplayName);
        order.Sort();
        RefreshText();
    }

    /// <summary>클립보드를 잠깐 올려 보여준다(아이템 획득 피드백).</summary>
    public void Peek()
    {
        if (!shown)
            peekTimer = peekDuration;
    }

    private void Register(string itemName)
    {
        if (!required.ContainsKey(itemName))
        {
            required[itemName] = 0;
            collected[itemName] = 0;
            order.Add(itemName);
        }
        required[itemName]++;
    }

    /// <summary>아이템을 챙겼을 때 CollectibleItem이 호출한다.</summary>
    public void Collect(string itemName)
    {
        if (!required.ContainsKey(itemName))
            Register(itemName); // 목록에 없던 아이템 안전 처리

        collected[itemName] = Mathf.Min(collected[itemName] + 1, required[itemName]);
        RefreshText();

        if (!shown)
            peekTimer = peekDuration; // 잠깐 올려서 체크된 걸 보여준다
    }

    private void Update()
    {
        if (!inputEnabled || clipboard == null)
            return;

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame
            && Cursor.lockState == CursorLockMode.Locked)
        {
            shown = !shown;
            peekTimer = 0f;
        }

        if (peekTimer > 0f)
            peekTimer -= Time.deltaTime;

        float target = (shown || peekTimer > 0f) ? 1f : 0f;
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

    private void RefreshText()
    {
        if (listText == null)
            return;

        // 멀티플레이면 서버가 관리하는 공용 진행도를, 아니면 로컬 목록을 렌더한다.
        var mgr = CampChecklistManager.Instance;
        listText.text = mgr != null
            ? BuildText(mgr.Names, mgr.Need, mgr.Have)
            : BuildLocalText();
    }

    private string BuildLocalText()
    {
        var sb = new StringBuilder("< 캠핑 준비물 >\n\n");
        bool all = order.Count > 0;
        foreach (string itemName in order)
        {
            bool done = collected[itemName] >= required[itemName];
            all &= done;
            sb.Append(done ? "■ " : "□ ").Append(itemName);
            if (required[itemName] > 1)
                sb.Append($" ({collected[itemName]}/{required[itemName]})");
            sb.Append('\n');
        }
        if (all)
            sb.Append("\n출발 준비 완료!");
        return sb.ToString();
    }

    private static string BuildText(string[] names, int[] need, int[] have)
    {
        var sb = new StringBuilder("< 캠핑 준비물 >\n\n");
        int n = Mathf.Min(names.Length, Mathf.Min(need.Length, have.Length));
        bool all = n > 0;
        for (int i = 0; i < n; i++)
        {
            bool done = have[i] >= need[i];
            all &= done;
            sb.Append(done ? "■ " : "□ ").Append(names[i]);
            if (need[i] > 1)
                sb.Append($" ({have[i]}/{need[i]})");
            sb.Append('\n');
        }
        if (all)
            sb.Append("\n출발 준비 완료!");
        return sb.ToString();
    }

    // ------------------------------------------------------------ 클립보드 생성

    private void BuildClipboard()
    {
        clipboard = new GameObject("Clipboard").transform;
        clipboard.SetParent(cam.transform, false);
        clipboard.localPosition = HiddenPos;
        clipboard.localRotation = HiddenRot;

        Part("Board", new Vector3(0f, 0f, 0f), new Vector3(0.30f, 0.42f, 0.014f), new Color(0.45f, 0.30f, 0.18f));
        Part("Paper", new Vector3(0f, -0.005f, -0.009f), new Vector3(0.26f, 0.37f, 0.004f), new Color(0.96f, 0.95f, 0.90f));
        Part("Clip", new Vector3(0f, 0.19f, -0.014f), new Vector3(0.10f, 0.035f, 0.02f), new Color(0.65f, 0.66f, 0.68f));

        var textGo = new GameObject("ListText");
        textGo.transform.SetParent(clipboard, false);
        textGo.transform.localPosition = new Vector3(-0.11f, 0.155f, -0.013f);
        listText = textGo.AddComponent<TextMesh>();
        listText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        textGo.GetComponent<MeshRenderer>().material = listText.font.material;
        listText.anchor = TextAnchor.UpperLeft;
        listText.alignment = TextAlignment.Left;
        listText.characterSize = 0.0065f;
        listText.fontSize = 48;
        listText.lineSpacing = 1.05f;
        listText.color = new Color(0.12f, 0.10f, 0.10f);

        clipboard.gameObject.SetActive(false);
    }

    /// <summary>클립보드 부품 큐브 하나 생성. 콜라이더는 제거해 상호작용 레이캐스트를 막지 않는다.</summary>
    private void Part(string partName, Vector3 pos, Vector3 size, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = partName;
        go.transform.SetParent(clipboard, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        Object.Destroy(go.GetComponent<Collider>());

        var mr = go.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var mat = new Material(shader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        else mat.color = color;
        mr.material = mat;
    }

    private void OnGUI()
    {
        if (!inputEnabled)
            return;

        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        var style = new GUIStyle(GUI.skin.label) { fontSize = 14 };
        style.normal.textColor = new Color(1f, 1f, 1f, 0.75f);
        GUI.Label(new Rect(16f, Screen.height - 30f, 300f, 24f), "[E] 준비물 목록", style);
    }

    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
        if (!enabled && clipboard != null)
            clipboard.gameObject.SetActive(false);
    }
}
