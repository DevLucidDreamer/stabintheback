using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

public struct WeaponState
{
    public uint holder;
    public bool equipped, available;
    public int order;
    public Vector3 position, euler;
}

/// <summary>Server-owned inventory. Full replicated state supports late joins and delayed player spawns.</summary>
public class WeaponNetworkManager : NetworkBehaviour
{
    public static WeaponNetworkManager Instance { get; private set; }
    public const int Capacity = 3;
    [SerializeField] private float lethalReach = 1.4f, lethalRadius = 1f, strikeDelay = .18f, swingCooldown = .45f;
    public readonly SyncDictionary<int, WeaponState> States = new SyncDictionary<int, WeaponState>();
    private readonly Dictionary<int, Weapon> registry = new Dictionary<int, Weapon>();
    private readonly Dictionary<int, WeaponState> applied = new Dictionary<int, WeaponState>();
    private readonly Dictionary<int, Vector3> spawnPositions = new Dictionary<int, Vector3>();
    private readonly Dictionary<uint, double> nextSwingAt = new Dictionary<uint, double>();
    private int order;
    private void Awake() { Instance = this; BuildRegistry(); }
    private void OnDestroy() { if (Instance == this) Instance = null; }
    private void BuildRegistry()
    {
        var weapons = FindObjectsByType<Weapon>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(w => w.gameObject.scene == gameObject.scene).ToList();
        var catalog = Resources.LoadAll<GameObject>("CampaignWeapons").OrderBy(p => p.name).ToArray();
        for (int i = 0; i < catalog.Length; i++)
        {
            var spec = catalog[i].GetComponent<Weapon>();
            if (weapons.Any(w => w.campaignKey == spec.campaignKey)) continue;
            var copy = Instantiate(catalog[i], transform).GetComponent<Weapon>();
            copy.name = "TravelReserve_" + spec.campaignKey; copy.SetWeaponId(10000 + i);
            copy.startsAvailable = false; copy.transform.position = new Vector3(0, 1.2f, 5);
            copy.gameObject.SetActive(false); weapons.Add(copy);
        }
        registry.Clear(); foreach (var w in weapons) if (w.WeaponId >= 0) registry.Add(w.WeaponId, w);
    }
    public override void OnStartServer()
    {
        BuildRegistry(); States.Clear(); spawnPositions.Clear(); nextSwingAt.Clear(); order = 0;
        if (!WeaponJourney.Transitioning) WeaponJourney.Clear();
        foreach (var pair in registry)
        {
            bool reserved = WeaponJourney.Records.Any(r => r.key == pair.Value.campaignKey);
            spawnPositions[pair.Key] = pair.Value.transform.position;
            States[pair.Key] = new WeaponState { available = pair.Value.startsAvailable && !reserved,
                position = pair.Value.transform.position, euler = pair.Value.transform.eulerAngles };
        }
        StartCoroutine(RestoreJourney());
    }
    public override void OnStartClient() { BuildRegistry(); applied.Clear(); }
    private IEnumerator RestoreJourney()
    {
        while (WeaponJourney.Records.Count > 0)
        {
            foreach (var record in WeaponJourney.Records.ToArray())
            {
                int id = FindKey(record.key);
                if (id < 0) { WeaponJourney.Records.Remove(record); continue; }
                bool connected = NetworkServer.connections.TryGetValue(record.connection.connectionId, out var connection) && ReferenceEquals(connection, record.connection);
                if (!connected)
                {
                    var lost = States[id]; lost.available = true; lost.position = new Vector3(0, 1.2f, 5); States[id] = lost;
                    WeaponJourney.Records.Remove(record); continue;
                }
                if (!connection.isReady || connection.identity == null || connection.identity.gameObject.scene != gameObject.scene) continue;
                var state = States[id]; state.holder = connection.identity.netId; state.available = true;
                state.equipped = record.equipped; state.order = record.order; order = Mathf.Max(order, record.order);
                States[id] = state; WeaponJourney.Records.Remove(record);
            }
            yield return null;
        }
        WeaponJourney.Transitioning = false;
    }
    public int FindKey(string key)
    {
        foreach (var pair in registry) if (!string.IsNullOrEmpty(key) && pair.Value.campaignKey == key) return pair.Key;
        return -1;
    }
    public Weapon Resolve(int id) => registry.TryGetValue(id, out var weapon) ? weapon : null;
    public int[] Inventory(uint holder) => States.Where(p => p.Value.holder == holder && holder != 0)
        .OrderBy(p => p.Value.order).ThenBy(p => p.Key).Select(p => p.Key).ToArray();
    public int Equipped(uint holder)
    {
        foreach (var pair in States) if (holder != 0 && pair.Value.holder == holder && pair.Value.equipped) return pair.Key;
        return -1;
    }
    public void CaptureJourney()
    {
        WeaponJourney.Clear(); WeaponJourney.Transitioning = true;
        foreach (var connection in NetworkServer.connections.Values)
        {
            if (connection?.identity == null) continue;
            foreach (int id in Inventory(connection.identity.netId))
            {
                string key = Resolve(id).campaignKey; if (string.IsNullOrEmpty(key)) continue;
                WeaponJourney.Records.Add(new WeaponJourney.Record { connection = connection, key = key,
                    equipped = States[id].equipped, order = States[id].order });
            }
        }
    }
    private void Update()
    {
        if (!isClient) return;
        foreach (var pair in States)
        {
            if (applied.TryGetValue(pair.Key, out var old) && old.Equals(pair.Value)) continue;
            var weapon = Resolve(pair.Key); if (weapon == null) continue;
            var state = pair.Value;
            var previous = old.holder != 0 ? ResolvePlayer(old.holder) : null;
            if (state.holder != 0)
            {
                var player = ResolvePlayer(state.holder);
                if (player == null || player.HandSocket == null) continue;
                if (previous != null && previous != player) previous.StoreWeaponVisual(weapon);
                if (state.equipped) player.AttachWeaponVisual(weapon); else player.StoreWeaponVisual(weapon);
            }
            else if (state.available)
            {
                if (previous != null) previous.DetachWeaponVisual(weapon, state.position, Quaternion.Euler(state.euler), false);
                else weapon.DetachTo(state.position, Quaternion.Euler(state.euler), false, false);
            }
            else weapon.gameObject.SetActive(false);
            applied[pair.Key] = state;
        }
    }
    [ServerCallback] private void LateUpdate()
    {
        if (WeaponJourney.Transitioning) return;
        foreach (var pair in States.ToArray())
        {
            var state = pair.Value;
            bool disconnected = state.holder != 0 && (!NetworkServer.spawned.TryGetValue(state.holder, out var player) ||
                player == null || player.connectionToClient == null || !NetworkServer.connections.ContainsKey(player.connectionToClient.connectionId));
            bool lost = state.available && state.holder == 0 && state.position.y < -3f;
            if (!disconnected && !lost) continue;
            state.holder = 0; state.equipped = false; state.available = true;
            state.position = spawnPositions[pair.Key]; States[pair.Key] = state;
        }
    }
    public void RequestPickup(int id)
    {
        if (!NetworkClient.active && !NetworkServer.active)
        { var p = FindFirstObjectByType<PlayerInteraction>(); if (p != null && Resolve(id) != null) p.AttachWeaponVisual(Resolve(id)); return; }
        CmdPickup(id);
    }
    public void RequestCycle(int direction) { if (NetworkClient.active) CmdCycle(direction); }
    public void RequestDrop(int id, Vector3 position, Quaternion rotation) => CmdDrop(id);
    public void RequestSwing() { if (NetworkClient.active) CmdSwing(); }
    [Command(requiresAuthority = false)] private void CmdPickup(int id, NetworkConnectionToClient sender = null) => ServerPickup(id, sender);
    public bool ServerPickup(int id, NetworkConnectionToClient sender)
    {
        if (!States.TryGetValue(id, out var state) || !state.available || state.holder != 0 || !ServerInteractionGuard.IsNear(sender, state.position)) return false;
        if (StoneTempleManager.Instance != null && !StoneTempleManager.Instance.CanTakeRelic(id)) return false;
        uint holder = sender.identity.netId; if (Inventory(holder).Length >= Capacity) return false;
        Unequip(holder); state.holder = holder; state.equipped = true; state.order = ++order; States[id] = state;
        nextSwingAt[holder] = System.Math.Max(NetworkTime.time + .2d, nextSwingAt.TryGetValue(holder, out double old) ? old : 0);
        return true;
    }
    private void Unequip(uint holder)
    { foreach (int id in Inventory(holder)) { var state = States[id]; state.equipped = false; States[id] = state; } }
    [Command(requiresAuthority = false)] private void CmdCycle(int direction, NetworkConnectionToClient sender = null) => ServerCycle(direction, sender);
    public bool ServerCycle(int direction, NetworkConnectionToClient sender)
    {
        if (!ServerInteractionGuard.HasPlayer(sender) || direction != 1 && direction != -1) return false;
        uint holder = sender.identity.netId;
        if (nextSwingAt.TryGetValue(holder, out double next) && NetworkTime.time < next) return false;
        int[] inventory = Inventory(holder); if (inventory.Length < 2) return false;
        int current = System.Array.IndexOf(inventory, Equipped(holder));
        int id = inventory[(current + direction + inventory.Length) % inventory.Length];
        Unequip(holder); var state = States[id]; state.equipped = true; States[id] = state;
        nextSwingAt[holder] = NetworkTime.time + .16d; return true;
    }
    [Command(requiresAuthority = false)] private void CmdDrop(int id, NetworkConnectionToClient sender = null)
    {
        if (!ServerInteractionGuard.HasPlayer(sender) || Equipped(sender.identity.netId) != id) return;
        uint holder = sender.identity.netId; DropOne(id, sender.identity.transform.position + sender.identity.transform.forward * 1.2f);
        int[] left = Inventory(holder);
        if (left.Length > 0) { var state = States[left[0]]; state.equipped = true; States[left[0]] = state; }
    }
    private void DropOne(int id, Vector3 position)
    {
        var hits = Physics.RaycastAll(position + Vector3.up, Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore)
            .Where(h => h.collider.attachedRigidbody == null && h.collider.GetComponentInParent<PlayerHealth>() == null && h.collider.GetComponentInParent<Weapon>() == null)
            .OrderBy(h => h.distance).ToArray();
        float offset = Resolve(id).groundOffset + .04f;
        Vector3 drop = hits.Length > 0 && hits[0].point.y > -3f ? hits[0].point + Vector3.up * offset : spawnPositions[id];
        var state = States[id]; state.holder = 0; state.equipped = false; state.available = true;
        state.position = drop; state.euler = Vector3.zero; States[id] = state;
    }
    [Server] public void ServerDropWeaponOf(uint holder, Vector3 position)
    {
        int[] items = Inventory(holder);
        for (int i = 0; i < items.Length; i++) DropOne(items[i], position + new Vector3((i - (items.Length - 1) * .5f) * .9f, 0, .55f));
    }
    [Server] public bool ServerPlayerHasWeapon(uint holder) => Inventory(holder).Length > 0;
    [Server] public bool ServerIsWeaponHeld(int id) => States.TryGetValue(id, out var state) && state.holder != 0;
    [Command(requiresAuthority = false)] private void CmdSwing(NetworkConnectionToClient sender = null)
    {
        if (!ServerInteractionGuard.HasPlayer(sender)) return;
        uint holder = sender.identity.netId; int id = Equipped(holder); if (id < 0) return;
        if (nextSwingAt.TryGetValue(holder, out double next) && NetworkTime.time < next) return;
        nextSwingAt[holder] = NetworkTime.time + swingCooldown; RpcSwing(holder); StartCoroutine(DelayedKill(holder, id));
    }
    private IEnumerator DelayedKill(uint holder, int weapon)
    { yield return new WaitForSeconds(strikeDelay); ServerSwingKill(holder, weapon); }
    [Server] private void ServerSwingKill(uint holder, int expectedWeapon)
    {
        if (Equipped(holder) != expectedWeapon || !NetworkServer.spawned.TryGetValue(holder, out var attacker) || attacker == null) return;
        if (attacker.TryGetComponent(out PlayerHealth health) && health.IsDead) return;
        Weapon weapon = Resolve(expectedWeapon); var t = attacker.transform;
        float reach = weapon != null ? weapon.swingReach : lethalReach, radius = weapon != null ? weapon.swingRadius : lethalRadius;
        foreach (var col in Physics.OverlapSphere(t.position + Vector3.up + t.forward * reach, radius))
        {
            var victim = col.GetComponentInParent<PlayerHealth>();
            if (victim == null || victim.netId == holder || victim.IsDead || victim.IsSpawnProtected) continue;
            Vector3 blow = victim.transform.position - t.position; blow.y = 0;
            victim.ServerKill(holder, blow.sqrMagnitude > .001f ? blow.normalized : t.forward);
            if (victim.IsDead) RpcConfirmedImpact(victim.transform.position + Vector3.up);
        }
    }
    [ClientRpc] private void RpcSwing(uint holder) => ResolvePlayer(holder)?.PlaySwingVisual();
    [ClientRpc] private void RpcConfirmedImpact(Vector3 position)
    {
        GameAudio.PlayAt(Random.value < .5f ? "player_impact_01" : "player_impact_02", position, .85f, Random.Range(.94f, 1.04f), 2.5f, 25);
        GameAudio.PlayAt("player_impact_body", position, .38f, .85f, 2, 20);
    }
    private static PlayerInteraction ResolvePlayer(uint id)
        => NetworkClient.spawned.TryGetValue(id, out var player) && player != null ? player.GetComponent<PlayerInteraction>() : null;
}
