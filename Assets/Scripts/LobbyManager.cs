using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 대기실 진행 관리 (서버 권한). 씬에 하나 존재하는 NetworkIdentity 오브젝트.
/// - 출발 발판(LobbyReadyZone) 위에 서 있는 플레이어를 '준비'로 기록한다.
/// - 접속자 전원이 준비되면 카운트다운 후 첫 스테이지로 전환한다.
/// - 카운트다운 중 한 명이라도 준비를 풀면 즉시 취소한다.
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
public class LobbyManager : NetworkBehaviour
{
    public static LobbyManager Instance { get; private set; }

    [Tooltip("전원 준비 시 이동할 첫 스테이지 씬 이름(Build Settings에 포함되어야 함)")]
    [SerializeField] private string firstStageScene = "NetworkDemo";

    [Tooltip("전원 준비 후 출발까지의 카운트다운(초)")]
    [SerializeField] private float countdownSeconds = 5f;

    [Tooltip("서버가 준비 인원을 다시 세는 주기(초). 접속 종료 정리용")]
    [SerializeField] private float recountInterval = 0.5f;

    [SyncVar] private int readyCount;
    [SyncVar] private int playerCount;
    [SyncVar] private double countdownEndsAt = -1d; // NetworkTime.time 기준. 음수면 카운트다운 없음

    private readonly HashSet<uint> readyIds = new HashSet<uint>(); // 서버 전용
    private bool serverChangingScene;
    private bool localReady;
    private float recountTimer;
    private string cachedCode;

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public override void OnStartServer()
    {
        readyIds.Clear();
        serverChangingScene = false;
        countdownEndsAt = -1d;
    }

    private void Update()
    {
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

        bool allReady = total > 0 && readyCount >= total;
        if (allReady && countdownEndsAt < 0d)
            countdownEndsAt = NetworkTime.time + countdownSeconds;
        else if (!allReady && countdownEndsAt >= 0d)
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

    /// <summary>방 정원. NetworkManager의 최대 접속 수를 그대로 쓴다.</summary>
    private int MaxPlayers()
        => NetworkManager.singleton != null ? NetworkManager.singleton.maxConnections : 0;

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

        int max = MaxPlayers();
        if (max > 0)
        {
            corner.alignment = TextAnchor.UpperRight;
            GUI.Label(new Rect(Screen.width - 240f, 16f, 220f, 30f), $"Member {playerCount}/{max}", corner);
        }

        // 남은 카운트다운이 있으면 화면 가운데 위쪽에 크게.
        if (countdownEndsAt >= 0d)
        {
            int left = Mathf.Max(0, Mathf.CeilToInt((float)(countdownEndsAt - NetworkTime.time)));
            GUI.Label(new Rect(0f, Screen.height * 0.14f, Screen.width, 40f), $"출발까지 {left}", title);
        }

        GUI.Label(new Rect(0f, Screen.height - 92f, Screen.width, 32f), $"준비  {readyCount} / {playerCount}", title);
        GUI.Label(new Rect(0f, Screen.height - 60f, Screen.width, 26f),
            localReady ? "발판에서 내려오면 준비 취소" : "출발 게이트의 나무 발판 위에 올라서면 준비 완료", body);
    }
}
