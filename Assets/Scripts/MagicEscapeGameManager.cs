using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public enum MagicEscapePhase
{
    HiddenSwitches = 0,
    SplitCipher = 1,
    Counterweights = 2,
    TwinLevers = 3,
    SealBreaking = 4,
    RallyEscape = 5,
    Victory = 6,
}

/// <summary>
/// 원작형 마검탈출 스테이지의 서버 권한 진행 관리자.
/// 탐색, 분산 정보, 협동 타이밍, 희소 마검, 전원 탈출을 한 상태 머신으로 묶는다.
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
public sealed class MagicEscapeGameManager : NetworkBehaviour
{
    public static MagicEscapeGameManager Instance { get; private set; }
    public static event Action OnChanged;
    public static event Action<MagicEscapePhase> OnPhaseEntered;

    [Header("퍼즐 규칙")]
    [SerializeField, Min(1)] private int hiddenSwitchCount = 5;
    [SerializeField] private int[] runeSequence = { 2, 0, 3, 1, 0, 2 };
    [SerializeField, Min(1)] private int pressurePlateCount = 2;
    [SerializeField, Min(0.2f)] private float pressureHoldSeconds = 3.5f;
    [SerializeField, Min(1f)] private float twinLeverWindow = 9f;
    [SerializeField, Min(1)] private int sealCount = 4;
    [SerializeField, Min(0.2f)] private float rallyHoldSeconds = 3f;
    [SerializeField] private bool allowSoloAssist = true;

    [Header("완료")]
    [SerializeField, Min(1f)] private float returnDelay = 8f;
    [SerializeField] private string nextScene = "Lobby";

    [SyncVar(hook = nameof(OnPhaseSync))] private int phase;
    [SyncVar(hook = nameof(OnIntSync))] private int switchMask;
    [SyncVar(hook = nameof(OnIntSync))] private int runeProgress;
    [SyncVar(hook = nameof(OnIntSync))] private int pressureMask;
    [SyncVar(hook = nameof(OnIntSync))] private int pressureLatchedMask;
    [SyncVar(hook = nameof(OnFloatSync))] private float pressureProgress;
    [SyncVar(hook = nameof(OnIntSync))] private int leverMask;
    [SyncVar(hook = nameof(OnDoubleSync))] private double leverDeadline = -1d;
    [SyncVar(hook = nameof(OnIntSync))] private int brokenSealMask;
    [SyncVar(hook = nameof(OnIntSync))] private int rallyCount;
    [SyncVar(hook = nameof(OnFloatSync))] private float rallyProgress;
    [SyncVar(hook = nameof(OnIntSync))] private int connectedPlayerCount = 1;

    private readonly Dictionary<int, HashSet<uint>> plateOccupants = new Dictionary<int, HashSet<uint>>();
    private MagicEscapePressurePlate[] pressurePlates;
    private readonly HashSet<uint> rallyPlayers = new HashSet<uint>();
    private readonly Dictionary<uint, double> nextActionAt = new Dictionary<uint, double>();
    private readonly uint[] leverUsers = new uint[2];
    private double victoryEndsAt = -1d;
    private bool sceneChanging;

    public MagicEscapePhase Phase => (MagicEscapePhase)phase;
    public int SwitchMask => switchMask;
    public int HiddenSwitchCount => hiddenSwitchCount;
    public int RuneProgress => runeProgress;
    public int RuneLength => runeSequence != null ? runeSequence.Length : 0;
    public int PressureMask => pressureMask;
    public int PressureLatchedMask => pressureLatchedMask;
    public float PressureProgress01 => Mathf.Clamp01(pressureProgress / pressureHoldSeconds);
    public int LeverMask => leverMask;
    public float LeverSecondsLeft => leverDeadline < 0d ? 0f : Mathf.Max(0f, (float)(leverDeadline - NetworkTime.time));
    public int BrokenSealMask => brokenSealMask;
    public int BrokenSealCount => CountBits(brokenSealMask);
    public int SealCount => sealCount;
    public int RallyCount => rallyCount;
    public int ConnectedPlayers => Mathf.Max(1, connectedPlayerCount);
    public float RallyProgress01 => Mathf.Clamp01(rallyProgress / rallyHoldSeconds);

    private void Awake()
    {
        Instance = this;
        plateOccupants.Clear();
        for (int i = 0; i < pressurePlateCount; i++)
            plateOccupants[i] = new HashSet<uint>();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        phase = (int)MagicEscapePhase.HiddenSwitches;
        nextActionAt.Clear();
        sceneChanging = false;
        pressurePlates = FindObjectsByType<MagicEscapePressurePlate>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        OnChanged?.Invoke();
    }

    [ServerCallback]
    private void Update()
    {
        connectedPlayerCount = Mathf.Max(1, ServerPlayerCount());
        CleanupDisconnectedPlayers();

        if (Phase == MagicEscapePhase.Counterweights)
            ServerUpdatePressure();
        else if (Phase == MagicEscapePhase.TwinLevers && leverMask != 0 && NetworkTime.time > leverDeadline)
        {
            leverMask = 0;
            leverUsers[0] = leverUsers[1] = 0;
            leverDeadline = -1d;
            RpcToast("차단기 동기화 시간 초과. 다시 신호를 맞춰라!", false);
        }
        else if (Phase == MagicEscapePhase.RallyEscape)
            ServerUpdateRally();
        else if (Phase == MagicEscapePhase.Victory && !sceneChanging && NetworkTime.time >= victoryEndsAt)
        {
            sceneChanging = true;
            if (NetworkManager.singleton != null && !string.IsNullOrEmpty(nextScene))
                NetworkManager.singleton.ServerChangeScene(nextScene);
        }
    }

    // 숨은 스위치 -----------------------------------------------------------

    public void RequestSwitch(int index)
    {
        if (NetworkClient.active) CmdTrySwitch(index); else ServerTrySwitch(index);
    }

    [Command(requiresAuthority = false)]
    private void CmdTrySwitch(int index, NetworkConnectionToClient sender = null)
    {
        MagicEscapeSwitch target = FindSwitch(index);
        if (target == null || !ServerInteractionGuard.IsNear(sender, target.transform.position) || !ConsumeAction(sender))
            return;
        ServerTrySwitch(index);
    }

    [Server]
    private void ServerTrySwitch(int index)
    {
        if (Phase != MagicEscapePhase.HiddenSwitches || index < 0 || index >= hiddenSwitchCount)
            return;
        int bit = 1 << index;
        if ((switchMask & bit) != 0)
            return;
        switchMask |= bit;
        RpcToast($"보조 전력 복구 {CountBits(switchMask)} / {hiddenSwitchCount}", true);
        if (CountBits(switchMask) >= hiddenSwitchCount)
            ServerAdvance(MagicEscapePhase.SplitCipher, "B-02 기밀문 개방! 양쪽 관찰 기록을 합쳐라");
    }

    // 분산 룬 ---------------------------------------------------------------

    public void RequestRune(int index)
    {
        if (NetworkClient.active) CmdTryRune(index); else ServerTryRune(index);
    }

    [Command(requiresAuthority = false)]
    private void CmdTryRune(int index, NetworkConnectionToClient sender = null)
    {
        MagicEscapeRune target = FindRune(index);
        if (target == null || !ServerInteractionGuard.IsNear(sender, target.transform.position) || !ConsumeAction(sender))
            return;
        ServerTryRune(index);
    }

    [Server]
    private void ServerTryRune(int index)
    {
        if (Phase != MagicEscapePhase.SplitCipher || runeSequence == null || runeSequence.Length == 0)
            return;
        if (index == runeSequence[runeProgress])
        {
            runeProgress++;
            RpcToast($"접근 코드 입력 {runeProgress} / {runeSequence.Length}", true);
            if (runeProgress >= runeSequence.Length)
                ServerAdvance(MagicEscapePhase.Counterweights, "B-03 운송실 개방! 두 작업자가 중량판을 유지하라");
        }
        else
        {
            runeProgress = 0;
            RpcToast("접근 코드 불일치. 관찰 기록을 다시 확인하라!", false);
        }
    }

    // 압력판 ----------------------------------------------------------------

    [Server]
    private void RefreshPressureOccupants()
    {
        foreach (HashSet<uint> occupants in plateOccupants.Values)
            occupants.Clear();

        foreach (MagicEscapePressurePlate plate in pressurePlates)
        {
            if (plate == null || !plate.isActiveAndEnabled ||
                !plateOccupants.TryGetValue(plate.Index, out HashSet<uint> occupants))
                continue;

            foreach (NetworkConnectionToClient connection in NetworkServer.connections.Values)
                if (ServerInteractionGuard.IsOnPressurePlate(connection, plate.Area))
                    occupants.Add(connection.identity.netId);
        }
        RebuildPressureMask();
    }

    [Server]
    private void ServerUpdatePressure()
    {
        RefreshPressureOccupants();
        int fullMask = (1 << pressurePlateCount) - 1;
        bool solo = allowSoloAssist && ServerPlayerCount() <= 1;
        if (solo)
        {
            pressureLatchedMask |= pressureMask;
            pressureProgress = 0f;
            if ((pressureLatchedMask & fullMask) == fullMask)
                ServerAdvance(MagicEscapePhase.TwinLevers, "B-04 제어실 개방! 분산 차단기를 맞춰라");
            return;
        }
        if ((pressureMask & fullMask) == fullMask && UniquePressurePlayers() >= pressurePlateCount)
        {
            pressureProgress += Time.deltaTime;
            if (pressureProgress >= pressureHoldSeconds)
                ServerAdvance(MagicEscapePhase.TwinLevers, "B-04 제어실 개방! 분산 차단기를 맞춰라");
        }
        else
            pressureProgress = Mathf.Max(0f, pressureProgress - Time.deltaTime * 1.5f);
    }

    // 쌍레버 ----------------------------------------------------------------

    public void RequestLever(int index)
    {
        if (NetworkClient.active) CmdTryLever(index); else ServerTryLever(index, 1);
    }

    [Command(requiresAuthority = false)]
    private void CmdTryLever(int index, NetworkConnectionToClient sender = null)
    {
        MagicEscapeLever target = FindLever(index);
        if (sender?.identity == null || target == null ||
            !ServerInteractionGuard.IsNear(sender, target.transform.position) || !ConsumeAction(sender))
            return;
        ServerTryLever(index, sender.identity.netId);
    }

    [Server]
    private void ServerTryLever(int index, uint playerId)
    {
        if (Phase != MagicEscapePhase.TwinLevers || index < 0 || index > 1)
            return;
        if (leverMask == 0 || NetworkTime.time > leverDeadline)
        {
            leverMask = 0;
            leverUsers[0] = leverUsers[1] = 0;
            leverDeadline = NetworkTime.time + twinLeverWindow;
        }
        leverMask |= 1 << index;
        leverUsers[index] = playerId;
        bool solo = allowSoloAssist && ServerPlayerCount() <= 1;
        if (leverMask == 3 && (solo || leverUsers[0] != leverUsers[1]))
            ServerAdvance(MagicEscapePhase.SealBreaking, "B-05 격리실 개방! 승인 도구를 차지해 격리핵을 파괴하라");
        else if (leverMask == 3)
        {
            leverMask = 1 << index;
            leverUsers[1 - index] = 0;
            RpcToast("두 차단기는 서로 다른 작업자가 맡아야 한다!", false);
        }
    }

    // 봉인 ------------------------------------------------------------------

    public void RequestBreakSeal(int index)
    {
        if (NetworkClient.active) CmdBreakSeal(index); else ServerBreakSeal(index);
    }

    [Command(requiresAuthority = false)]
    private void CmdBreakSeal(int index, NetworkConnectionToClient sender = null)
    {
        MagicEscapeSeal target = FindSeal(index);
        WeaponNetworkManager weapons = WeaponNetworkManager.Instance;
        if (sender?.identity == null || target == null || weapons == null ||
            !weapons.ServerPlayerHasWeapon(sender.identity.netId) ||
            !ServerInteractionGuard.IsNear(sender, target.transform.position, 4.5f) || !ConsumeAction(sender, 0.3d))
            return;
        ServerBreakSeal(index);
    }

    [Server]
    private void ServerBreakSeal(int index)
    {
        if (Phase != MagicEscapePhase.SealBreaking || index < 0 || index >= sealCount)
            return;
        int bit = 1 << index;
        if ((brokenSealMask & bit) != 0) return;
        brokenSealMask |= bit;
        RpcToast($"격리핵 파괴 {BrokenSealCount} / {sealCount}", true);
        if (BrokenSealCount >= sealCount)
            ServerAdvance(MagicEscapePhase.RallyEscape, "B-06 비상 승강기 개방! 전원이 탑승해야 한다");
    }

    // 전원 탈출 -------------------------------------------------------------

    public void SetLocalRally(bool inside)
    {
        if (NetworkClient.active) CmdSetRally(inside); else OfflineSetRally(inside);
    }

    [Command(requiresAuthority = false)]
    private void CmdSetRally(bool inside, NetworkConnectionToClient sender = null)
    {
        if (Phase != MagicEscapePhase.RallyEscape || sender?.identity == null) return;
        MagicEscapeRallyZone zone = FindFirstObjectByType<MagicEscapeRallyZone>();
        if (inside && (zone == null || !ServerInteractionGuard.IsInside(sender, zone.Area))) return;
        if (inside) rallyPlayers.Add(sender.identity.netId); else rallyPlayers.Remove(sender.identity.netId);
        rallyCount = rallyPlayers.Count;
    }

    private void OfflineSetRally(bool inside)
    {
        if (inside) rallyPlayers.Add(1); else rallyPlayers.Remove(1);
        rallyCount = rallyPlayers.Count;
    }

    [Server]
    private void ServerUpdateRally()
    {
        int players = Mathf.Max(1, ServerPlayerCount());
        if (rallyPlayers.Count >= players)
        {
            rallyProgress += Time.deltaTime;
            if (rallyProgress >= rallyHoldSeconds)
            {
                phase = (int)MagicEscapePhase.Victory;
                victoryEndsAt = NetworkTime.time + returnDelay;
                RpcPhaseEntered((int)MagicEscapePhase.Victory, "B-13 탈출 성공! 지상 이송을 시작한다");
            }
        }
        else rallyProgress = 0f;
    }

    [Server]
    private void ServerAdvance(MagicEscapePhase next, string banner)
    {
        phase = (int)next;
        pressureProgress = 0f;
        leverDeadline = -1d;
        rallyProgress = 0f;
        RpcPhaseEntered((int)next, banner);
    }

    [ClientRpc]
    private void RpcPhaseEntered(int next, string banner)
    {
        OnChanged?.Invoke();
        OnPhaseEntered?.Invoke((MagicEscapePhase)next);
        GameHud.Ensure().ShowBanner(banner, next == (int)MagicEscapePhase.Victory ? 6f : 4f,
            next == (int)MagicEscapePhase.Victory ? new Color(0.55f, 1f, 0.7f) : new Color(0.75f, 0.48f, 1f));
    }

    [ClientRpc]
    private void RpcToast(string message, bool success)
    {
        GameHud.Ensure().ShowToast(message, 2.5f,
            success ? new Color(0.6f, 1f, 0.65f) : new Color(1f, 0.48f, 0.42f));
        OnChanged?.Invoke();
    }

    private void OnPhaseSync(int oldValue, int newValue)
    {
        OnChanged?.Invoke();
        if (oldValue != newValue) OnPhaseEntered?.Invoke((MagicEscapePhase)newValue);
    }

    private void OnIntSync(int oldValue, int newValue) => OnChanged?.Invoke();
    private void OnFloatSync(float oldValue, float newValue) => OnChanged?.Invoke();
    private void OnDoubleSync(double oldValue, double newValue) => OnChanged?.Invoke();

    private void RebuildPressureMask()
    {
        int mask = 0;
        foreach (KeyValuePair<int, HashSet<uint>> pair in plateOccupants)
            if (pair.Value.Count > 0) mask |= 1 << pair.Key;
        pressureMask = mask;
    }

    private int UniquePressurePlayers()
    {
        var unique = new HashSet<uint>();
        foreach (HashSet<uint> set in plateOccupants.Values) unique.UnionWith(set);
        return unique.Count;
    }

    [Server]
    private void CleanupDisconnectedPlayers()
    {
        if (!NetworkServer.active) return;
        foreach (HashSet<uint> set in plateOccupants.Values)
            set.RemoveWhere(id => !NetworkServer.spawned.ContainsKey(id));
        rallyPlayers.RemoveWhere(id => !NetworkServer.spawned.ContainsKey(id));
        RebuildPressureMask();
        rallyCount = rallyPlayers.Count;
    }

    private static int ServerPlayerCount()
    {
        if (!NetworkServer.active) return 1;
        int count = 0;
        foreach (NetworkConnectionToClient connection in NetworkServer.connections.Values)
            if (connection?.identity != null) count++;
        return count;
    }

    [Server]
    private bool ConsumeAction(NetworkConnectionToClient sender, double cooldown = 0.12d)
    {
        if (sender?.identity == null) return false;
        uint id = sender.identity.netId;
        if (nextActionAt.TryGetValue(id, out double next) && NetworkTime.time < next) return false;
        nextActionAt[id] = NetworkTime.time + cooldown;
        return true;
    }

    private static int CountBits(int value)
    {
        int count = 0;
        while (value != 0) { count += value & 1; value >>= 1; }
        return count;
    }

    private static MagicEscapeSwitch FindSwitch(int index)
    {
        foreach (MagicEscapeSwitch item in FindObjectsByType<MagicEscapeSwitch>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (item.Index == index) return item;
        return null;
    }

    private static MagicEscapeRune FindRune(int index)
    {
        foreach (MagicEscapeRune item in FindObjectsByType<MagicEscapeRune>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (item.Index == index) return item;
        return null;
    }

    private static MagicEscapeLever FindLever(int index)
    {
        foreach (MagicEscapeLever item in FindObjectsByType<MagicEscapeLever>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (item.Index == index) return item;
        return null;
    }

    private static MagicEscapeSeal FindSeal(int index)
    {
        foreach (MagicEscapeSeal item in FindObjectsByType<MagicEscapeSeal>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (item.Index == index) return item;
        return null;
    }

    public string GoalLine()
    {
        switch (Phase)
        {
            case MagicEscapePhase.HiddenSwitches: return $"B-01 탐색: 숨은 보조 전력 단자 {hiddenSwitchCount}개를 복구하라";
            case MagicEscapePhase.SplitCipher: return "B-02 정보: 양쪽 관찰 기록을 합쳐 접근 코드를 입력하라";
            case MagicEscapePhase.Counterweights: return "B-03 협동: 서로 다른 작업자가 양쪽 중량판을 유지하라";
            case MagicEscapePhase.TwinLevers: return "B-04 타이밍: 분리된 두 차단기를 9초 안에 작동하라";
            case MagicEscapePhase.SealBreaking: return "B-05 마검: 승인 도구를 차지해 격리핵을 파괴하라";
            case MagicEscapePhase.RallyEscape: return "B-06 탈출: 한 명도 버리지 말고 비상 승강기에 탑승하라";
            default: return "지하 격리 연구동 B-13 탈출 성공";
        }
    }
}
