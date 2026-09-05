using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>Server-owned bridge construction, statue sockets and four-person ritual.</summary>
[RequireComponent(typeof(NetworkIdentity))]
public sealed class ExpeditionManager : NetworkBehaviour
{
    public static ExpeditionManager Instance { get; private set; }
    public int stage = 2;
    public ExpeditionCargo[] cargo;
    public ExpeditionNode[] nodes;
    public TemplePressurePlate[] plates;
    public Transform[] sockets;
    public int[] socketKinds = { 2, 0, 3, 1 };
    public int runeMask = (1 << 0) | (1 << 2) | (1 << 5) | (1 << 7);
    public Weapon lantern;
    [SyncVar] public int delivered;
    [SyncVar] public int phase;
    [SyncVar] public int pressureMask;
    [SyncVar] public bool lanternTaken;
    [SyncVar] public bool completed;
    [SyncVar] public float ritualProgress;
    public readonly SyncList<ExpeditionCargoState> Cargo = new SyncList<ExpeditionCargoState>();
    public readonly SyncList<uint> Hands = new SyncList<uint>();
    private readonly Dictionary<uint, int> previousPlate = new Dictionary<uint, int>();
    private double lastTick;
    private float stableTime;
    private bool changingScene;
    public const int BridgeUnits = 24;
    public int BridgeSections => delivered / 3;
    public Vector3 BuildPosition => new Vector3(2.1f, 0.85f, 21f + BridgeSections * 3f);
    public bool IsPressed(int index) => (pressureMask & (1 << index)) != 0;
    private void Awake() => Instance = this;
    private void OnDestroy() { if (Instance == this) Instance = null; }

    public override void OnStartServer()
    {
        delivered = phase = pressureMask = 0;
        lanternTaken = completed = changingScene = false;
        ritualProgress = stableTime = 0;
        lastTick = NetworkTime.time;
        previousPlate.Clear(); Cargo.Clear(); Hands.Clear();
        foreach (var item in cargo) Cargo.Add(new ExpeditionCargoState { position = item.home, socket = -1 });
        for (int i = 0; i < 4; i++) Hands.Add(0);
    }

    [ServerCallback]
    private void Update()
    {
        if (NetworkTime.time - lastTick < 0.05d) return;
        float dt = Mathf.Min(0.1f, (float)(NetworkTime.time - lastTick)); lastTick = NetworkTime.time;
        ServerTick(dt);
    }

    // Explicit tick supports the same deterministic fixtures as the first temple.
    public void ServerTick(float dt)
    {
        foreach (var connection in NetworkServer.connections.Values)
            if (ServerInteractionGuard.HasPlayer(connection) && connection.identity.transform.position.y < -5f)
                connection.identity.GetComponent<PlayerHealth>()?.ServerKill(0);
        for (int i = 0; i < Cargo.Count; i++)
        {
            var item = Cargo[i];
            if (item.holder != 0 && !Alive(item.holder))
                Cargo[i] = new ExpeditionCargoState { position = cargo[i].home, socket = -1 };
        }
        if (stage == 2)
        {
            if (delivered == BridgeUnits) phase = 1;
            if (phase == 1 && lantern != null && WeaponNetworkManager.Instance != null &&
                WeaponNetworkManager.Instance.ServerIsWeaponHeld(lantern.WeaponId)) lanternTaken = true;
            completed = phase == 1 && lanternTaken;
            return;
        }
        SampleRunes();
        if (phase == 0)
        {
            stableTime = StatuesMatch() ? stableTime + dt : 0;
            if (stableTime >= 0.6f) { phase = 1; stableTime = 0; }
        }
        else if (phase == 1)
        {
            stableTime = pressureMask == runeMask && PlatePlayerCount() == 4 ? stableTime + dt : 0;
            if (stableTime >= 1.5f) { phase = 2; stableTime = 0; }
        }
        else if (phase == 2)
        {
            for (int i = 0; i < Hands.Count; i++)
            {
                var connection = Connection(Hands[i]);
                if (!ServerInteractionGuard.IsNear(connection, nodes[4 + i].transform.position, 3f)) Hands[i] = 0;
            }
            var unique = new HashSet<uint>(Hands); unique.Remove(0);
            ritualProgress = unique.Count == 4 ? Mathf.Min(1f, ritualProgress + dt / 2f) : 0f;
            if (ritualProgress >= 1f) { completed = true; phase = 3; }
        }
    }

    private NetworkConnectionToClient Connection(uint id)
    {
        if (id == 0) return null;
        foreach (var connection in NetworkServer.connections.Values)
            if (connection?.identity != null && connection.identity.netId == id) return connection;
        return null;
    }
    private bool Alive(uint id) => ServerInteractionGuard.HasPlayer(Connection(id));
    public int HeldCargo(uint id)
    {
        for (int i = 0; i < Cargo.Count; i++) if (Cargo[i].holder == id) return i;
        return -1;
    }
    public bool StatuesMatch()
    {
        if (Cargo.Count != 4 || sockets.Length != 4) return false;
        for (int slot = 0; slot < 4; slot++)
        {
            bool match = false;
            for (int i = 0; i < Cargo.Count; i++)
                if (Cargo[i].holder == 0 && Cargo[i].socket == slot && cargo[i].kind == socketKinds[slot]) match = true;
            if (!match) return false;
        }
        return true;
    }
    public void SampleRunes()
    {
        pressureMask = 0;
        var next = new Dictionary<uint, int>();
        foreach (var connection in NetworkServer.connections.Values)
        {
            if (!ServerInteractionGuard.HasPlayer(connection)) continue;
            uint id = connection.identity.netId;
            int best = -1; float nearest = float.PositiveInfinity;
            foreach (var plate in plates)
            {
                if (!plate.ContainsPlayer(connection, previousPlate.TryGetValue(id, out int old) && old == plate.index)) continue;
                float d = (plate.transform.position - connection.identity.transform.position).sqrMagnitude;
                if (d < nearest) { best = plate.index; nearest = d; }
            }
            if (best >= 0) { pressureMask |= 1 << best; next[id] = best; }
        }
        previousPlate.Clear(); foreach (var pair in next) previousPlate.Add(pair.Key, pair.Value);
    }
    private int PlatePlayerCount() => previousPlate.Count;

    public Vector3 RespawnPoint(uint id)
        => new Vector3(((int)(id % 4) - 1.5f) * 1.6f, 1.15f,
            stage == 2 ? (phase > 0 ? 49f : 5f) : phase >= 2 ? 53f : phase == 1 ? 30f : 5f);

    public void RequestCargo(int index) => CmdCargo(index);
    public void RequestUse(int index) => CmdUse(index);
    [Command(requiresAuthority = false)]
    private void CmdCargo(int index, NetworkConnectionToClient sender = null) => ServerCargo(index, sender);
    public bool ServerCargo(int index, NetworkConnectionToClient sender)
    {
        if (index < 0 || index >= Cargo.Count || !ServerInteractionGuard.HasPlayer(sender) || stage == 3 && phase > 0) return false;
        uint id = sender.identity.netId;
        var state = Cargo[index];
        if (state.holder == id)
        {
            Vector3 drop = sender.identity.transform.position + sender.identity.transform.forward * 1.5f;
            bool floor = Physics.Raycast(drop + Vector3.up * 2f, Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore);
            Cargo[index] = new ExpeditionCargoState { socket = -1,
                position = floor && hit.point.y > -2f ? hit.point + Vector3.up * 0.45f : cargo[index].home };
            return true;
        }
        if (state.holder != 0 || HeldCargo(id) >= 0 || !ServerInteractionGuard.IsNear(sender, state.position) ||
            !ServerInteractionGuard.CanReach(sender, cargo[index])) return false;
        Cargo[index] = new ExpeditionCargoState { holder = id, position = state.position, socket = -1 };
        return true;
    }
    [Command(requiresAuthority = false)]
    private void CmdUse(int index, NetworkConnectionToClient sender = null)
    {
        if (index < 0 || index >= nodes.Length || !ServerInteractionGuard.CanReach(sender, nodes[index])) return;
        if (nodes[index].action == ExpeditionAction.Exit)
        {
            if (!completed || changingScene) return;
            changingScene = true;
            WeaponJourney.ChangeScene(stage == 2 ? "Stage3_UndergroundAltar" : "Lobby");
        }
        else if (ServerUse(index, sender)) RpcUse(nodes[index].transform.position);
    }
    public bool ServerUse(int index, NetworkConnectionToClient sender)
    {
        if (index < 0 || index >= nodes.Length || !ServerInteractionGuard.HasPlayer(sender)) return false;
        var node = nodes[index];
        Vector3 point = node.action == ExpeditionAction.Build ? BuildPosition : node.transform.position;
        if (!ServerInteractionGuard.IsNear(sender, point)) return false;
        uint id = sender.identity.netId; int held = HeldCargo(id);
        if (node.action == ExpeditionAction.Build)
        {
            if (stage != 2 || delivered >= BridgeUnits || held < 0) return false;
            delivered++;
            Cargo[held] = new ExpeditionCargoState { socket = -1, position = cargo[held].home };
            return true;
        }
        if (node.action == ExpeditionAction.Socket)
        {
            if (stage != 3 || phase != 0 || held < 0) return false;
            foreach (var item in Cargo) if (item.socket == node.slot) return false;
            Cargo[held] = new ExpeditionCargoState { socket = node.slot, position = sockets[node.slot].position + Vector3.up * 0.45f };
            return true;
        }
        if (node.action == ExpeditionAction.Altar)
        {
            if (stage != 3 || phase != 2 || Hands[node.slot] != 0 && Hands[node.slot] != id) return false;
            for (int i = 0; i < Hands.Count; i++) if (Hands[i] == id) Hands[i] = 0;
            Hands[node.slot] = id;
            return true;
        }
        return false;
    }
    [ClientRpc] private void RpcUse(Vector3 position) => GameAudio.PlayAt("lever", position, 0.5f, stage == 2 ? 0.65f : 0.85f);
}

public struct ExpeditionCargoState
{
    public uint holder;
    public Vector3 position;
    public int socket;
}
