using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 대기실 진행 관리 (서버 권한). 씬에 하나 존재하는 NetworkIdentity 오브젝트.
///
/// - 호스트가 Tab을 눌러 '방 옵션'을 열고 정원(몇 명이 모이면 출발할지)을 정한다.
///   옵션 변경은 호스트만 가능하며, 서버가 다시 한 번 검사한다.
/// - 정한 인원이 다 모이면 카운트다운 후 게임 씬(캠핑장)으로 전환한다.
/// - 정원이 덜 찼더라도 접속자 전원이 출발 발판에 올라서면 바로 출발할 수 있다.
/// - 카운트다운 중 조건이 깨지면(누가 나가거나 준비를 풀면) 즉시 취소한다.
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
public class LobbyManager : NetworkBehaviour
{
    public static LobbyManager Instance { get; private set; }

    [Header("씬 전환")]
    [Tooltip("인원이 모이면 이동할 게임 씬 이름(Build Settings에 포함되어야 함)")]
    [SerializeField] private string firstStageScene = "Stage2_Campground";

    [Tooltip("출발 조건이 갖춰진 뒤 실제 전환까지의 카운트다운(초)")]
    [SerializeField] private float countdownSeconds = 5f;

    [Header("방 옵션")]
    [Tooltip("정원의 최소값. 1로 두면 호스트 혼자서도 바로 출발할 수 있어 테스트가 쉽다")]
    [SerializeField] private int minPlayers = 1;

    [Tooltip("정원의 최대값")]
    [SerializeField] private int maxPlayers = 8;

    [Tooltip("방 옵션 창을 여닫는 키(호스트 전용)")]
    [SerializeField] private Key optionsKey = Key.Tab;

    [Header("동작")]
    [Tooltip("서버가 준비 인원을 다시 세는 주기(초). 접속 종료 정리용")]
    [SerializeField] private float recountInterval = 0.5f;

    [SyncVar] private int readyCount;
    [SyncVar] private int playerCount;
    [SyncVar] private int targetPlayers = 4;                 // 이 인원이 모이면 출발
    [SyncVar] private double countdownEndsAt = -1d;          // NetworkTime.time 기준. 음수면 카운트다운 없음

    private readonly HashSet<uint> readyIds = new HashSet<uint>(); // 서버 전용
    private bool serverChangingScene;
    private bool localReady;
    private float recountTimer;
    private string cachedCode;
    private bool optionsOpen;

    /// <summary>이 클라이언트가 방을 연 사람인지. 옵션을 만질 수 있는 사람은 호스트뿐이다.</summary>
    private static bool IsHost => NetworkServer.active;

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// 옵션 창을 열어 둔 채로 게임 씬으로 넘어가면 플레이어 입력이 꺼진 채 시작된다.
    /// 대기실을 떠날 때 반드시 되돌려 놓는다.
    /// </summary>
    private void OnDisable()
    {
        if (optionsOpen)
            SetOptionsOpen(false);
    }

    public override void OnStartServer()
    {
        readyIds.Clear();
        serverChangingScene = false;
        countdownEndsAt = -1d;
        targetPlayers = Mathf.Clamp(targetPlayers, minPlayers, maxPlayers);
        ApplyMaxConnections();
    }

    private void Update()
    {
        UpdateOptionsInput();

        if (!isServer)
            return;

        recountTimer -= Time.deltaTime;
        if (recountTimer <= 0f)
        {
            recountTimer = Mathf.Max(0.1f, recountInterval);
            ServerRecount();
        }

        if (!serverChangingScene && countdownEndsAt >= 0d && NetworkTime.time >= countdownEndsAt
            && NetworkManager.singleton != null)
        {
            serverChangingScene = true;
            NetworkManager.singleton.ServerChangeScene(firstStageScene);
        }
    }

    // ------------------------------------------------------------ 방 옵션 (호스트 전용)

    private void UpdateOptionsInput()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null)
            return;

        if (optionsOpen && kb.escapeKey.wasPressedThisFrame)
        {
            SetOptionsOpen(false);
            return;
        }

        if (!kb[optionsKey].wasPressedThisFrame)
            return;

        if (!IsHost)
            return; // 참가자에게는 옵션 창이 열리지 않는다

        SetOptionsOpen(!optionsOpen);
    }

    private void SetOptionsOpen(bool open)
    {
        optionsOpen = open;

        // 창이 열려 있는 동안은 조작만 멈추고 커서를 풀어 버튼을 누를 수 있게 한다.
        // SetInputEnabled가 아니라 SetInputPaused여야 한다 — 조작을 통째로 끄면
        // CharacterController가 접지 판정을 잃고 캐릭터가 바닥 아래로 가라앉는다.
        var controller = LocalPlayerController();
        if (controller != null)
            controller.SetInputPaused(open);
        else
            SetCursorFree(open);
    }

    private static PlayerController LocalPlayerController()
    {
        if (NetworkClient.active && NetworkClient.localPlayer != null)
            return NetworkClient.localPlayer.GetComponent<PlayerController>();
        return null;
    }

    private static void SetCursorFree(bool free)
    {
        Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = free;
    }

    /// <summary>
    /// 호스트가 정원을 바꿨다.
    /// 옵션 창은 IsHost(=서버 실행 중)일 때만 열리므로 여기서는 늘 서버 위에서 돌고,
    /// 클라이언트가 정원을 바꿀 경로 자체가 없다. 그래도 한 번 더 확인한다.
    /// </summary>
    private void RequestTargetPlayers(int value)
    {
        if (!NetworkServer.active)
            return;

        value = Mathf.Clamp(value, minPlayers, maxPlayers);
        if (value == targetPlayers)
            return;

        targetPlayers = value;
        ApplyMaxConnections();
        ServerRecount();
    }

    /// <summary>
    /// 정원을 넘겨 들어오지 못하게 접속 상한도 같이 바꾼다.
    /// NetworkServer는 Listen() 때 받은 값을 따로 들고 있으므로, 이미 서버가 떠 있는
    /// 상태에서 NetworkManager 쪽만 바꾸면 반영되지 않는다. 둘 다 갱신해야 한다.
    /// (호스트 자신도 connections에 포함되므로 targetPlayers를 그대로 넣으면 된다)
    /// </summary>
    [Server]
    private void ApplyMaxConnections()
    {
        if (NetworkManager.singleton != null)
            NetworkManager.singleton.maxConnections = targetPlayers;

        NetworkServer.maxConnections = targetPlayers;
    }

    // ------------------------------------------------------------ 준비 요청 (로컬 → 서버)

    /// <summary>출발 발판이 호출한다. 로컬 플레이어의 준비 상태가 바뀔 때만 서버에 알린다.</summary>
    public void SetLocalReady(bool ready)
    {
        if (localReady == ready)
            return;
        localReady = ready;

        if (NetworkClient.active || NetworkServer.active)
            CmdSetReady(ready);
    }

    [Command(requiresAuthority = false)]
    private void CmdSetReady(bool ready, NetworkConnectionToClient sender = null)
    {
        if (sender == null || sender.identity == null)
            return;

        if (ready)
            readyIds.Add(sender.identity.netId);
        else
            readyIds.Remove(sender.identity.netId);

        ServerRecount();
    }

    // ------------------------------------------------------------ 서버 집계

    [Server]
    private void ServerRecount()
    {
        if (serverChangingScene)
            return;

        // 나간 플레이어의 준비 상태를 정리한다.
        readyIds.RemoveWhere(id => !NetworkServer.spawned.ContainsKey(id));

        int total = 0;
        foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
            if (conn != null && conn.identity != null)
                total++;

        playerCount = total;
        readyCount = readyIds.Count;

        // 출발 조건은 오직 '정한 정원이 다 찼는가' 하나다.
        // 예전에는 "있는 사람 전원이 발판 위" 조건도 있었는데, 호스트 혼자 발판에 서면
        // 1/1로 전원 준비가 되어 정원과 무관하게 출발해 버렸다.
        // 발판은 이제 준비 표시 전용이고, 혼자 테스트하려면 정원을 1로 내리면 된다.
        bool shouldStart = total > 0 && total >= targetPlayers;

        if (shouldStart && countdownEndsAt < 0d)
            countdownEndsAt = NetworkTime.time + countdownSeconds;
        else if (!shouldStart && countdownEndsAt >= 0d)
            countdownEndsAt = -1d;
    }

    // ------------------------------------------------------------ 안내 UI

    /// <summary>
    /// 화면에 표시할 방 코드. 타이틀에서 넘어왔으면 그 값을, 대기실 씬을 직접 열어
    /// 호스트를 시작했으면 이 PC의 주소로 만들어 보여준다.
    /// </summary>
    private string RoomCodeText()
    {
        if (!string.IsNullOrEmpty(GameLaunch.Code))
            return GameLaunch.Code;

        if (cachedCode == null && NetworkServer.active)
            cachedCode = RoomCode.FromAddress(RoomCode.LocalAddress());

        return cachedCode;
    }

    private void OnGUI()
    {
        var title = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 26,
            fontStyle = FontStyle.Bold,
        };
        title.normal.textColor = new Color(1f, 0.88f, 0.5f);

        var body = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 18,
        };
        body.normal.textColor = new Color(0.92f, 0.92f, 0.95f);

        // 대기실 정보: 왼쪽 위에 방 코드, 오른쪽 위에 인원 (친구를 부르는 데 쓰는 정보)
        var corner = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
        };
        corner.normal.textColor = new Color(0.95f, 0.95f, 0.97f);

        string code = RoomCodeText();
        if (!string.IsNullOrEmpty(code))
            GUI.Label(new Rect(20f, 16f, 460f, 30f), "Code : " + code, corner);

        corner.alignment = TextAnchor.UpperRight;
        GUI.Label(new Rect(Screen.width - 240f, 16f, 220f, 30f), $"Member {playerCount}/{targetPlayers}", corner);

        // 남은 카운트다운이 있으면 화면 가운데 위쪽에 크게.
        if (countdownEndsAt >= 0d)
        {
            int left = Mathf.Max(0, Mathf.CeilToInt((float)(countdownEndsAt - NetworkTime.time)));
            GUI.Label(new Rect(0f, Screen.height * 0.14f, Screen.width, 40f), $"출발까지 {left}", title);
        }
        else
        {
            int missing = Mathf.Max(0, targetPlayers - playerCount);
            if (missing > 0)
                GUI.Label(new Rect(0f, Screen.height * 0.14f, Screen.width, 40f), $"{missing}명 더 모이면 출발", body);
        }

        GUI.Label(new Rect(0f, Screen.height - 92f, Screen.width, 32f), $"준비  {readyCount} / {playerCount}", title);
        GUI.Label(new Rect(0f, Screen.height - 60f, Screen.width, 26f),
            $"정원 {targetPlayers}명이 다 모이면 출발합니다", body);

        if (IsHost)
        {
            var hint = new GUIStyle(body) { fontSize = 15 };
            hint.normal.textColor = new Color(0.75f, 0.78f, 0.85f);
            GUI.Label(new Rect(0f, Screen.height - 32f, Screen.width, 24f),
                $"[{optionsKey}] 방 옵션 (호스트 전용)", hint);
        }

        if (optionsOpen)
            DrawOptionsPanel();
    }

    /// <summary>호스트만 보는 방 옵션 창.</summary>
    private void DrawOptionsPanel()
    {
        const float w = 420f, h = 230f;
        var rect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

        GUI.Box(rect, GUIContent.none);

        var header = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 22,
            fontStyle = FontStyle.Bold,
        };
        header.normal.textColor = new Color(1f, 0.88f, 0.5f);

        var label = new GUIStyle(GUI.skin.label) { fontSize = 17 };
        label.normal.textColor = new Color(0.93f, 0.93f, 0.96f);

        var value = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 24,
            fontStyle = FontStyle.Bold,
        };
        value.normal.textColor = Color.white;

        GUI.Label(new Rect(rect.x, rect.y + 14f, w, 30f), "방 옵션", header);

        GUI.Label(new Rect(rect.x + 30f, rect.y + 68f, 160f, 28f), "정원 (명)", label);

        // 정원 조절. 이미 들어와 있는 인원보다 적게는 내릴 수 없다.
        int lowest = Mathf.Max(minPlayers, playerCount);
        bool canDecrease = targetPlayers > lowest;
        bool canIncrease = targetPlayers < maxPlayers;

        GUI.enabled = canDecrease;
        if (GUI.Button(new Rect(rect.x + 210f, rect.y + 62f, 40f, 40f), "-"))
            RequestTargetPlayers(targetPlayers - 1);

        GUI.enabled = true;
        GUI.Label(new Rect(rect.x + 250f, rect.y + 62f, 80f, 40f), targetPlayers.ToString(), value);

        GUI.enabled = canIncrease;
        if (GUI.Button(new Rect(rect.x + 330f, rect.y + 62f, 40f, 40f), "+"))
            RequestTargetPlayers(targetPlayers + 1);
        GUI.enabled = true;

        var note = new GUIStyle(label) { fontSize = 14, wordWrap = true };
        note.normal.textColor = new Color(0.75f, 0.78f, 0.85f);
        GUI.Label(new Rect(rect.x + 30f, rect.y + 112f, w - 60f, 46f),
            $"{targetPlayers}명이 모이면 자동으로 {firstStageScene} 로 출발합니다.\n" +
            $"(조절 범위 {minPlayers}~{maxPlayers}명, 현재 접속 {playerCount}명)", note);

        if (GUI.Button(new Rect(rect.x + (w - 140f) * 0.5f, rect.y + h - 56f, 140f, 36f), "닫기"))
            SetOptionsOpen(false);
    }
}
