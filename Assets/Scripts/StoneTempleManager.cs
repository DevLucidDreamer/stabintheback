using System.Collections.Generic;
using Mirror;
using UnityEngine;

[RequireComponent(typeof(NetworkIdentity))]
public sealed class StoneTempleManager : NetworkBehaviour
{
    public static StoneTempleManager Instance { get; private set; }
    public TemplePressurePlate[] plates;
    public TempleInteractable[] levers;
    public TempleStone[] stones;
    public TempleMechanism[] mechanisms;
    public Weapon chalice;
    public Transform exit;
    [SyncVar] public int phase;
    [SyncVar] public int pressureMask;
    [SyncVar] public int leverMask;
    [SyncVar] public int rallyCount;
    [SyncVar] public int connectedPlayers;
    [SyncVar] public float rallyProgress;
    [SyncVar] public bool relicUnlocked;
    [SyncVar] public bool completed;
    public readonly SyncList<TempleMotion> Motions = new SyncList<TempleMotion>();
    public readonly SyncList<TempleStoneState> Stones = new SyncList<TempleStoneState>();
    private readonly Dictionary<uint, int> previousPlate = new Dictionary<uint, int>();
    private readonly Dictionary<uint, int> currentPlate = new Dictionary<uint, int>();
    private double[] releaseAt;
    private double nextSample;
    private static readonly float[] Checkpoints = { 5f, 21f, 47f, 69f, 102f, 121f, 137f };

    private void Awake() { Instance = this; }
    private void OnDestroy() { if (Instance == this) Instance = null; }
    public bool IsPressed(int index) => (pressureMask & (1 << index)) != 0;
    public bool HasPlacedStone(int plateIndex)
    {
        TemplePressurePlate plate = plates[plateIndex];
        foreach (var state in Stones)
            if (state.holder == 0 && plate.ContainsFoot(state.position - Vector3.up * 0.45f)) return true;
        return false;
    }
    public override void OnStartServer()
    {
        phase = pressureMask = leverMask = rallyCount = 0;
        relicUnlocked = completed = false;
        rallyProgress = 0f;
        previousPlate.Clear();
        Motions.Clear();
        releaseAt = new double[mechanisms.Length];
        for (int i = 0; i < mechanisms.Length; i++)
            Motions.Add(new TempleMotion { changedAt = NetworkTime.time - 10d });
        Stones.Clear();
        foreach (var stone in stones) Stones.Add(new TempleStoneState { position = stone.home });
    }

    [ServerCallback]
    private void Update()
    {
        if (NetworkTime.time < nextSample) return;
        nextSample = NetworkTime.time + 0.05d;
        ServerSamplePlayers();
        ServerRecoverStones();
        for (int i = 0; i < Stones.Count; i++)
            if (Stones[i].holder == 0)
                foreach (var plate in plates)
                    if (plate.acceptsStone && plate.ContainsFoot(Stones[i].position - Vector3.up * 0.45f))
                        pressureMask |= 1 << plate.index;

        bool[] desired = {
            phase >= 1 || IsPressed(0),
            phase >= 2 || IsPressed(1) || IsPressed(2),
            phase >= 3 || IsPressed(3),
            phase >= 4 || (leverMask & 16) != 0 || IsPressed(5) || IsPressed(7),
            phase >= 4 || (leverMask & 8) != 0 || IsPressed(4) || IsPressed(6),
            phase >= 5 || IsPressed(8),
            phase >= 5,
            relicUnlocked,
            completed,
            phase >= 4
        };
        for (int i = 0; i < desired.Length; i++) SetMotion(i, desired[i]);

        if (phase == 5 && !relicUnlocked)
        {
            rallyProgress = connectedPlayers == 4 && rallyCount == 4 ? rallyProgress + 0.05f : 0f;
            if (rallyProgress >= 1.25f) relicUnlocked = true;
        }
        if (relicUnlocked && !completed && chalice != null && WeaponNetworkManager.Instance != null &&
            WeaponNetworkManager.Instance.ServerIsWeaponHeld(chalice.WeaponId))
        {
            completed = true;
            phase = 6;
        }
    }

    // Every connection contributes to at most one plate. Destroyed/disabled/dead/disconnected
    // occupants disappear on this sample; no dependence on OnTriggerExit or stale physics bounds.
    public void ServerSamplePlayers()
    {
        pressureMask = rallyCount = connectedPlayers = 0;
        currentPlate.Clear();
        foreach (var connection in NetworkServer.connections.Values)
        {
            if (connection?.identity == null) continue;
            connectedPlayers++;
            uint id = connection.identity.netId;
            if (connection.identity.transform.position.y < -5f)
            {
                var health = connection.identity.GetComponent<PlayerHealth>();
                if (health != null && !health.IsDead) health.ServerKill(0);
                continue;
            }
            previousPlate.TryGetValue(id, out int previous);
            int best = -1;
            float distance = float.PositiveInfinity;
            foreach (var plate in plates)
            {
                if (!plate.ContainsPlayer(connection, previousPlate.ContainsKey(id) && previous == plate.index)) continue;
                float d = (plate.transform.position - connection.identity.transform.position).sqrMagnitude;
                if (d < distance) { distance = d; best = plate.index; }
            }
            if (best < 0) continue;
            currentPlate[id] = best;
            pressureMask |= 1 << best;
        }
        previousPlate.Clear();
        foreach (var pair in currentPlate) previousPlate.Add(pair.Key, pair.Value);
        for (int i = 9; i <= 12; i++) if (IsPressed(i)) rallyCount++;
    }

    private void SetMotion(int index, bool wanted)
    {
        TempleMotion old = Motions[index];
        if (wanted) releaseAt[index] = NetworkTime.time + 1d;
        else if (NetworkTime.time < releaseAt[index]) return;
        if (old.active == wanted) return;
        float t = Mathf.Clamp01((float)(NetworkTime.time - old.changedAt) / mechanisms[index].duration);
        float from = Mathf.Lerp(old.from, old.active ? 1f : 0f, Mathf.SmoothStep(0f, 1f, t));
        Motions[index] = new TempleMotion { active = wanted, changedAt = NetworkTime.time, from = from };
    }

    public Vector3 RespawnPoint(uint playerId)
        => new Vector3(((int)(playerId % 4) - 1.5f) * 1.7f, 1.15f, Checkpoints[Mathf.Clamp(phase, 0, 6)]);

    public bool CanTakeRelic(int weaponId) => chalice == null || weaponId != chalice.WeaponId || relicUnlocked;
    public void RequestLever(int index) => CmdLever(index);
    public void RequestStone(int index) => CmdStone(index);
    public void RequestReturn() => CmdReturn();

    [Command(requiresAuthority = false)]
    private void CmdLever(int index, NetworkConnectionToClient sender = null)
        => ServerLatchLever(index, sender);

    public bool ServerLatchLever(int index, NetworkConnectionToClient sender)
    {
        if (index < 0 || index >= levers.Length || !ServerInteractionGuard.IsNear(sender, levers[index].transform.position)) return false;
        if (!CanLatchLever(index)) return false;
        // Reaching the far-side lever is the achievement. Releasing a plate behind
        // the player must never invalidate that achievement. Walls still block use.
        if (!ServerInteractionGuard.CanReach(sender, levers[index])) return false;
        leverMask |= 1 << index;
        if (index == 3 || index == 4)
        {
            if ((leverMask & 24) == 24) phase = 4;
        }
        else phase++;
        return true;
    }

    public bool CanLatchLever(int index)
    {
        if (index < 0 || index >= levers.Length || (leverMask & (1 << index)) != 0) return false;
        int expected = index <= 2 ? index : index <= 4 ? 3 : 4;
        return phase == expected;
    }

    [Command(requiresAuthority = false)]
    private void CmdStone(int index, NetworkConnectionToClient sender = null)
    {
        if (index < 0 || index >= Stones.Count || !ServerInteractionGuard.HasPlayer(sender)) return;
        TempleStoneState state = Stones[index];
        uint id = sender.identity.netId;
        if (state.holder == id)
        {
            Vector3 drop = sender.identity.transform.position + sender.identity.transform.forward * 1.6f;
            // Prefer an empty weight socket in reach. A snapped stone has a deterministic pose on every peer.
            foreach (var plate in plates)
                if (plate.acceptsStone && (plate.transform.position - drop).sqrMagnitude < 5f)
                { drop = plate.transform.position + Vector3.up * 0.45f; Stones[index] = new TempleStoneState { position = drop }; return; }
            if (Physics.Raycast(drop + Vector3.up * 1.5f, Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore))
                drop = hit.point + Vector3.up * 0.45f;
            else drop = stones[index].home;
            Stones[index] = new TempleStoneState { position = drop };
        }
        else if (state.holder == 0 && ServerInteractionGuard.IsNear(sender, state.position, 3.2f))
        {
            foreach (var other in Stones) if (other.holder == id) return;
            Stones[index] = new TempleStoneState { holder = id, position = state.position };
        }
    }

    private void ServerRecoverStones()
    {
        for (int i = 0; i < Stones.Count; i++)
        {
            TempleStoneState state = Stones[i];
            if (state.holder == 0) continue;
            if (!NetworkServer.spawned.TryGetValue(state.holder, out var holder) || holder == null ||
                holder.connectionToClient == null || !NetworkServer.connections.ContainsKey(holder.connectionToClient.connectionId) ||
                !ServerInteractionGuard.HasPlayer(holder.connectionToClient))
                Stones[i] = new TempleStoneState { position = stones[i].home };
        }
    }

    [Command(requiresAuthority = false)]
    private void CmdReturn(NetworkConnectionToClient sender = null)
    {
        if (!completed || exit == null || !ServerInteractionGuard.IsNear(sender, exit.position)) return;
        WeaponJourney.ChangeScene("Stage2_BrokenBridge");
    }
}
