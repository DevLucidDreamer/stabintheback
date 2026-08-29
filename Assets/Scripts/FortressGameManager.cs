using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public enum FortressPhase
{
    Counterweights = 0,
    RuneCipher = 1,
    TwinLevers = 2,
    SealBreaking = 3,
    RallyEscape = 4,
    Victory = 5,
}

/// <summary>
/// 저주받은 성채 탈출전의 서버 권한 진행 관리자.
/// 압력판, 룬, 쌍레버, 마검 봉인, 전원 탈출을 한 상태 머신으로 묶는다.
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
public class FortressGameManager : NetworkBehaviour
{
    public static FortressGameManager Instance { get; private set; }
    public static event Action OnChanged;
    public static event Action<FortressPhase> OnPhaseEntered;

    [Header("협동 규칙")]
    [SerializeField, Min(1)] private int pressurePlateCount = 2;
    [SerializeField, Min(0.2f)] private float pressureHoldSeconds = 2.5f;
    [SerializeField, Min(1f)] private float twinLeverWindow = 7f;
    [SerializeField, Min(0.2f)] private float rallyHoldSeconds = 2f;
    [SerializeField] private bool allowSoloAssist = true;

    [Header("룬 암호")]
    [Tooltip("룬 받침대 번호의 정답 순서. 기본값은 달-불-가시-눈(2,0,3,1)")]
    [SerializeField] private int[] runeSequence = { 2, 0, 3, 1 };

    [Header("마지막")]
    [SerializeField, Min(1)] private int sealCount = 3;
    [SerializeField, Min(1f)] private float returnDelay = 8f;
    [SerializeField] private string lobbyScene = "Lobby";

    [SyncVar(hook = nameof(OnPhaseSync))] private int phase;
    [SyncVar(hook = nameof(OnIntSync))] private int pressureMask;
    [SyncVar(hook = nameof(OnIntSync))] private int pressureLatchedMask;
    [SyncVar(hook = nameof(OnFloatSync))] private float pressureProgress;
    [SyncVar(hook = nameof(OnIntSync))] private int runeProgress;
    [SyncVar(hook = nameof(OnIntSync))] private int leverMask;
    [SyncVar(hook = nameof(OnDoubleSync))] private double leverDeadline = -1d;
    [SyncVar(hook = nameof(OnIntSync))] private int brokenSealMask;
    [SyncVar(hook = nameof(OnIntSync))] private int rallyCount;
    [SyncVar(hook = nameof(OnFloatSync))] private float rallyProgress;
    [SyncVar(hook = nameof(OnIntSync))] private int connectedPlayerCount = 1;

    private readonly Dictionary<int, HashSet<uint>> plateOccupants = new Dictionary<int, HashSet<uint>>();
    private readonly HashSet<uint> rallyPlayers = new HashSet<uint>();
    private readonly Dictionary<uint, double> nextActionAt = new Dictionary<uint, double>();
    private readonly uint[] leverUsers = new uint[2];
    private double victoryEndsAt = -1d;
    private bool sceneChanging;

    public FortressPhase Phase => (FortressPhase)phase;
    public int PressureMask => pressureMask;
    public int PressureLatchedMask => pressureLatchedMask;
    public float PressureProgress01 => Mathf.Clamp01(pressureProgress / pressureHoldSeconds);
    public int RuneProgress => runeProgress;
    public int RuneLength => runeSequence != null ? runeSequence.Length : 0;
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
        for (int i = 0; i < pressurePlateCount; i++)
            plateOccupants[i] = new HashSet<uint>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        nextActionAt.Clear();
        phase = (int)FortressPhase.Counterweights;
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

        if (Phase == FortressPhase.Counterweights)
            ServerUpdatePressure();
        else if (Phase == FortressPhase.TwinLevers && leverMask != 0 && NetworkTime.time > leverDeadline)
        {
            leverMask = 0;
            leverUsers[0] = leverUsers[1] = 0;
            leverDeadline = -1d;
            RpcToast("레버의 공명이 끊겼다. 동시에 당겨야 한다!", false);
        }
        else if (Phase == FortressPhase.RallyEscape)
            ServerUpdateRally();
        else if (Phase == FortressPhase.Victory && !sceneChanging && NetworkTime.time >= victoryEndsAt)
        {
            sceneChanging = true;
            NetworkManager.singleton.ServerChangeScene(lobbyScene);
        }
    }

    // 압력판 -----------------------------------------------------------------

    public void SetLocalPressure(int index, bool occupied)
    {
        if (NetworkClient.active)
            CmdSetPressure(index, occupied);
        else
            OfflineSetPressure(index, occupied);
    }

    [Command(requiresAuthority = false)]
    private void CmdSetPressure(int index, bool occupied, NetworkConnectionToClient sender = null)
    {
        if (Phase != FortressPhase.Counterweights || sender?.identity == null || !plateOccupants.ContainsKey(index))
            return;

        CoopPressurePlate plate = FindPressurePlate(index);
        if (occupied && (plate == null || !ServerInteractionGuard.IsInside(sender, plate.Area)))
            return;

        uint id = sender.identity.netId;
        if (occupied)
            plateOccupants[index].Add(id);
        else
            plateOccupants[index].Remove(id);
        RebuildPressureMask();
    }

    private void OfflineSetPressure(int index, bool occupied)
    {
        if (!plateOccupants.ContainsKey(index))
            return;
        if (occupied) plateOccupants[index].Add(1); else plateOccupants[index].Remove(1);
        RebuildPressureMask();
    }

    [Server]
    private void ServerUpdatePressure()
    {
        int fullMask = (1 << pressurePlateCount) - 1;
        bool solo = allowSoloAssist && ServerPlayerCount() <= 1;

        if (solo)
        {
            pressureLatchedMask |= pressureMask;
            pressureProgress = 0f;
            if ((pressureLatchedMask & fullMask) == fullMask)
                ServerAdvance(FortressPhase.RuneCipher, "쇠사슬 관문이 열렸다! 룬의 순서를 맞춰라");
            return;
        }

        if ((pressureMask & fullMask) == fullMask && UniquePressurePlayers() >= pressurePlateCount)
        {
            pressureProgress += Time.deltaTime;
            if (pressureProgress >= pressureHoldSeconds)
                ServerAdvance(FortressPhase.RuneCipher, "쇠사슬 관문이 열렸다! 룬의 순서를 맞춰라");
        }
        else
        {
            pressureProgress = Mathf.Max(0f, pressureProgress - Time.deltaTime * 1.5f);
        }
    }

    // 룬 ---------------------------------------------------------------------

    public void RequestRune(int runeIndex)
    {
        if (NetworkClient.active) CmdTryRune(runeIndex); else ServerTryRune(runeIndex);
    }

    [Command(requiresAuthority = false)]
    private void CmdTryRune(int runeIndex, NetworkConnectionToClient sender = null)
    {
        RunePedestal rune = FindRune(runeIndex);
        if (rune == null || !ServerInteractionGuard.IsNear(sender, rune.transform.position) || !ConsumeAction(sender))
            return;
        ServerTryRune(runeIndex);
    }

    [Server]
    private void ServerTryRune(int runeIndex)
    {
        if (Phase != FortressPhase.RuneCipher || runeSequence == null || runeSequence.Length == 0)
            return;

        if (runeIndex == runeSequence[runeProgress])
        {
            runeProgress++;
            RpcToast($"룬 공명 {runeProgress} / {runeSequence.Length}", true);
            if (runeProgress >= runeSequence.Length)
                ServerAdvance(FortressPhase.TwinLevers, "룬 문이 열렸다! 양쪽 레버를 함께 당겨라");
        }
        else
        {
            runeProgress = 0;
            RpcToast("룬이 비명을 지르며 꺼졌다. 순서가 틀렸다!", false);
        }
    }

    // 쌍레버 -----------------------------------------------------------------

    public void RequestLever(int leverIndex)
    {
        if (NetworkClient.active) CmdTryLever(leverIndex); else ServerTryLever(leverIndex, 1);
    }

    [Command(requiresAuthority = false)]
    private void CmdTryLever(int leverIndex, NetworkConnectionToClient sender = null)
    {
        CoopLever lever = FindLever(leverIndex);
        if (sender?.identity == null || lever == null ||
            !ServerInteractionGuard.IsNear(sender, lever.transform.position) || !ConsumeAction(sender))
            return;
        ServerTryLever(leverIndex, sender.identity.netId);
    }

    [Server]
    private void ServerTryLever(int leverIndex, uint playerId)
    {
        if (Phase != FortressPhase.TwinLevers || leverIndex < 0 || leverIndex > 1)
            return;

        if (leverMask == 0 || NetworkTime.time > leverDeadline)
        {
            leverMask = 0;
            leverUsers[0] = leverUsers[1] = 0;
            leverDeadline = NetworkTime.time + twinLeverWindow;
        }

        leverMask |= 1 << leverIndex;
        leverUsers[leverIndex] = playerId;

        bool solo = allowSoloAssist && ServerPlayerCount() <= 1;
        if (leverMask == 3 && (solo || leverUsers[0] != leverUsers[1]))
            ServerAdvance(FortressPhase.SealBreaking, "무기고가 열렸다! 마검을 들어 봉인핵을 부숴라");
        else if (leverMask == 3)
        {
            leverMask = 1 << leverIndex;
            leverUsers[1 - leverIndex] = 0;
            RpcToast("두 레버는 서로 다른 사람이 맡아야 한다!", false);
        }
    }

    // 봉인 -------------------------------------------------------------------

    public void RequestBreakSeal(int sealIndex)
    {
        if (NetworkClient.active) CmdBreakSeal(sealIndex); else ServerBreakSeal(sealIndex);
    }

    [Command(requiresAuthority = false)]
    private void CmdBreakSeal(int sealIndex, NetworkConnectionToClient sender = null)
    {
        CursedSeal seal = FindSeal(sealIndex);
        WeaponNetworkManager weapons = WeaponNetworkManager.Instance;
        if (sender?.identity == null || seal == null || weapons == null ||
            !weapons.ServerPlayerHasWeapon(sender.identity.netId) ||
            !ServerInteractionGuard.IsNear(sender, seal.transform.position, 4.5f) || !ConsumeAction(sender, 0.3d))
            return;
        ServerBreakSeal(sealIndex);
    }

    [Server]
    private void ServerBreakSeal(int sealIndex)
    {
        if (Phase != FortressPhase.SealBreaking || sealIndex < 0 || sealIndex >= sealCount)
            return;

        int bit = 1 << sealIndex;
        if ((brokenSealMask & bit) != 0)
            return;

        brokenSealMask |= bit;
        RpcToast($"봉인핵 파괴 {BrokenSealCount} / {sealCount}", true);
        if (BrokenSealCount >= sealCount)
            ServerAdvance(FortressPhase.RallyEscape, "저주가 풀렸다! 모두 탈출진에 모여라");
    }

    // 전원 탈출 ---------------------------------------------------------------

    public void SetLocalRally(bool inside)
    {
        if (NetworkClient.active) CmdSetRally(inside); else OfflineSetRally(inside);
    }

    [Command(requiresAuthority = false)]
    private void CmdSetRally(bool inside, NetworkConnectionToClient sender = null)
    {
        if (Phase != FortressPhase.RallyEscape || sender?.identity == null)
            return;

        CoopRallyZone zone = FindFirstObjectByType<CoopRallyZone>();
        if (inside && (zone == null || !ServerInteractionGuard.IsInside(sender, zone.Area)))
            return;
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
                phase = (int)FortressPhase.Victory;
                victoryEndsAt = NetworkTime.time + returnDelay;
                RpcPhaseEntered((int)FortressPhase.Victory, "성채 탈출 성공! 함께였기에 살아남았다");
            }
        }
        else
        {
            rallyProgress = 0f;
        }
    }

    // 공통 -------------------------------------------------------------------

    [Server]
    private void ServerAdvance(FortressPhase next, string banner)
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
        OnPhaseEntered?.Invoke((FortressPhase)next);
        GameHud.Ensure().ShowBanner(banner, next == (int)FortressPhase.Victory ? 6f : 4f,
            next == (int)FortressPhase.Victory ? new Color(0.55f, 1f, 0.7f) : new Color(0.75f, 0.55f, 1f));
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
        if (oldValue != newValue)
            OnPhaseEntered?.Invoke((FortressPhase)newValue);
    }

    private void OnIntSync(int oldValue, int newValue) => OnChanged?.Invoke();
    private void OnFloatSync(float oldValue, float newValue) => OnChanged?.Invoke();
    private void OnDoubleSync(double oldValue, double newValue) => OnChanged?.Invoke();

    private void RebuildPressureMask()
    {
        int mask = 0;
        foreach (var pair in plateOccupants)
            if (pair.Value.Count > 0)
                mask |= 1 << pair.Key;
        pressureMask = mask;
    }

    private int UniquePressurePlayers()
    {
        var unique = new HashSet<uint>();
        foreach (HashSet<uint> set in plateOccupants.Values)
            unique.UnionWith(set);
        return unique.Count;
    }

    [Server]
    private void CleanupDisconnectedPlayers()
    {
        if (!NetworkServer.active)
            return;
        foreach (HashSet<uint> set in plateOccupants.Values)
            set.RemoveWhere(id => !NetworkServer.spawned.ContainsKey(id));
        rallyPlayers.RemoveWhere(id => !NetworkServer.spawned.ContainsKey(id));
        if (nextActionAt.Count > 0)
        {
            var staleActions = new List<uint>();
            foreach (KeyValuePair<uint, double> pair in nextActionAt)
                if (!NetworkServer.spawned.ContainsKey(pair.Key)) staleActions.Add(pair.Key);
            foreach (uint id in staleActions) nextActionAt.Remove(id);
        }
        RebuildPressureMask();
        rallyCount = rallyPlayers.Count;
    }

    private static int ServerPlayerCount()
    {
        if (!NetworkServer.active)
            return 1;
        int count = 0;
        foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
            if (conn?.identity != null)
                count++;
        return count;
    }

    private static int CountBits(int value)
    {
        int count = 0;
        while (value != 0) { count += value & 1; value >>= 1; }
        return count;
    }

    [Server]
    private bool ConsumeAction(NetworkConnectionToClient sender, double cooldown = 0.12d)
    {
        if (sender?.identity == null)
            return false;
        uint id = sender.identity.netId;
        if (nextActionAt.TryGetValue(id, out double next) && NetworkTime.time < next)
            return false;
        nextActionAt[id] = NetworkTime.time + cooldown;
        return true;
    }

    private static CoopPressurePlate FindPressurePlate(int index)
    {
        foreach (CoopPressurePlate plate in FindObjectsByType<CoopPressurePlate>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (plate.PlateIndex == index) return plate;
        return null;
    }

    private static RunePedestal FindRune(int index)
    {
        foreach (RunePedestal rune in FindObjectsByType<RunePedestal>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (rune.RuneIndex == index) return rune;
        return null;
    }

    private static CoopLever FindLever(int index)
    {
        foreach (CoopLever lever in FindObjectsByType<CoopLever>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (lever.LeverIndex == index) return lever;
        return null;
    }

    private static CursedSeal FindSeal(int index)
    {
        foreach (CursedSeal seal in FindObjectsByType<CursedSeal>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (seal.SealIndex == index) return seal;
        return null;
    }

    public string GoalLine()
    {
        switch (Phase)
        {
            case FortressPhase.Counterweights: return "협동: 양쪽 압력판을 동시에 눌러라";
            case FortressPhase.RuneCipher: return "암호: 양쪽 벽의 단서를 합쳐 룬을 눌러라";
            case FortressPhase.TwinLevers: return "협동: 제한 시간 안에 두 레버를 당겨라";
            case FortressPhase.SealBreaking: return "마검: 무기를 들고 봉인핵 3개를 파괴하라";
            case FortressPhase.RallyEscape: return "마지막: 전원이 탈출진에 모여라";
            default: return "성채 탈출 성공";
        }
    }
}
