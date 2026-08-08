using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

/// <summary>
/// 무기 줍기/버리기/스윙을 서버 권한으로 동기화한다 (Phase 4).
/// - 씬에 하나 존재하는 NetworkIdentity 오브젝트.
/// - 무기엔 NetworkIdentity가 없고 고유 ID로 식별한다(냉장고 등 자식이라 중첩 불가).
/// - 서버가 "무기 id → 소지자 netId"를 관리하고, 각 클라이언트는 자기 로컬 무기 복사본을
///   해당 플레이어의 손 소켓에 붙이거나 월드에 내려놓는다.
/// - 스윙은 소지자 기준으로 모든 클라이언트에서 애니메이션되며, 타격 판정은 각 클라가 로컬 수행.
/// </summary>
public class WeaponNetworkManager : NetworkBehaviour
{
    public static WeaponNetworkManager Instance { get; private set; }

    [Header("무기 스윙 살상 판정")]
    [Tooltip("무기에 값이 없을 때 쓰는 기본 사거리 (m). 보통은 무기별 swingReach가 우선한다")]
    [SerializeField] private float lethalReach = 1.4f;
    [Tooltip("무기에 값이 없을 때 쓰는 기본 판정 반경 (m)")]
    [SerializeField] private float lethalRadius = 1.0f;
    [Tooltip("클릭 후 실제 타격까지 지연(내려치기 타이밍과 맞춤, 초)")]
    [SerializeField] private float strikeDelay = 0.18f;

    private readonly Dictionary<int, Weapon> registry = new Dictionary<int, Weapon>();

    // 서버 전용
    private readonly Dictionary<int, uint> heldBy = new Dictionary<int, uint>();          // weaponId → 소지자 netId(0=자유)
    private readonly Dictionary<int, (Vector3 pos, Vector3 euler)> freePose = new Dictionary<int, (Vector3, Vector3)>();

    private void Awake()
    {
        Instance = this;
        BuildRegistry(); // 오프라인/조기 접근 대비
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void BuildRegistry()
    {
        registry.Clear();
        foreach (Weapon w in FindObjectsByType<Weapon>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (w.WeaponId >= 0)
                registry[w.WeaponId] = w;
    }

    public override void OnStartServer()
    {
        BuildRegistry();
        heldBy.Clear();
        freePose.Clear();
        foreach (var kv in registry)
        {
            heldBy[kv.Key] = 0;
            freePose[kv.Key] = (kv.Value.transform.position, kv.Value.transform.eulerAngles);
        }
    }

    public override void OnStartClient()
    {
        BuildRegistry();
        CmdRequestSync();
    }

    // ------------------------------------------------------------ 요청 (로컬 플레이어 → 서버)

    public void RequestPickup(int weaponId)
    {
        if (!NetworkClient.active && !NetworkServer.active)
        {
            // 오프라인 폴백: 아무 로컬 플레이어 손에 붙인다.
            var pi = Object.FindFirstObjectByType<PlayerInteraction>();
            if (pi != null && registry.TryGetValue(weaponId, out var w))
                pi.AttachWeaponVisual(w);
            return;
        }
        CmdPickup(weaponId);
    }

    public void RequestDrop(int weaponId, Vector3 pos, Quaternion rot)
    {
        if (!NetworkClient.active && !NetworkServer.active)
        {
            if (registry.TryGetValue(weaponId, out var w))
                w.DetachTo(pos, rot, false);
            return;
        }
        CmdDrop(weaponId, pos, rot.eulerAngles);
    }

    public void RequestSwing()
    {
        if (!NetworkClient.active && !NetworkServer.active)
            return;
        CmdSwing();
    }

    // ------------------------------------------------------------ 서버 처리

    [Command(requiresAuthority = false)]
    private void CmdPickup(int weaponId, NetworkConnectionToClient sender = null)
    {
        if (sender == null || sender.identity == null)
            return;
        if (!registry.ContainsKey(weaponId))
            return;
        if (heldBy.TryGetValue(weaponId, out uint cur) && cur != 0)
            return; // 이미 누가 들고 있음

        uint holder = sender.identity.netId;

        // 이 플레이어가 이미 다른 무기를 들고 있으면 먼저 내려놓게 한다.
        int prev = HeldWeaponOf(holder);
        if (prev >= 0)
        {
            Vector3 p = sender.identity.transform.position + Vector3.up + sender.identity.transform.forward * 0.5f;
            heldBy[prev] = 0;
            freePose[prev] = (p, Vector3.zero);
            RpcFree(prev, holder, p, Vector3.zero);
        }

        heldBy[weaponId] = holder;
        RpcHold(weaponId, holder);
    }

    [Command(requiresAuthority = false)]
    private void CmdDrop(int weaponId, Vector3 pos, Vector3 euler, NetworkConnectionToClient sender = null)
    {
        if (sender == null || sender.identity == null)
            return;
        if (!heldBy.TryGetValue(weaponId, out uint cur) || cur != sender.identity.netId)
            return; // 소지자만 버릴 수 있음

        heldBy[weaponId] = 0;
        freePose[weaponId] = (pos, euler);
        RpcFree(weaponId, sender.identity.netId, pos, euler);
    }

    [Command(requiresAuthority = false)]
    private void CmdSwing(NetworkConnectionToClient sender = null)
    {
        if (sender == null || sender.identity == null)
            return;

        uint attacker = sender.identity.netId;
        RpcSwing(attacker); // 모든 클라에서 스윙 애니메이션

        // 무기를 든 사람만 살상 가능. 내려치는 타이밍에 판정.
        if (HeldWeaponOf(attacker) >= 0)
            StartCoroutine(DelayedKill(attacker));
    }

    private IEnumerator DelayedKill(uint attackerNetId)
    {
        yield return new WaitForSeconds(strikeDelay);
        ServerSwingKill(attackerNetId);
    }

    /// <summary>스윙한 사람 앞쪽의 다른 플레이어를 즉사시킨다(서버 권한).</summary>
    [Server]
    private void ServerSwingKill(uint attackerNetId)
    {
        int weaponId = HeldWeaponOf(attackerNetId);
        if (weaponId < 0)
            return; // 그 사이 무기를 놓쳤으면 살상 없음
        if (!NetworkServer.spawned.TryGetValue(attackerNetId, out var attacker) || attacker == null)
            return;

        // 무기마다 사거리/판정 크기가 다르다 (대검은 멀리, 고무 오리는 코앞).
        Weapon weapon = Resolve(weaponId);
        float reach = weapon != null ? weapon.swingReach : lethalReach;
        float radius = weapon != null ? weapon.swingRadius : lethalRadius;

        Transform t = attacker.transform;
        Vector3 origin = t.position + Vector3.up * 1.0f + t.forward * reach;

        foreach (Collider col in Physics.OverlapSphere(origin, radius))
        {
            PlayerHealth victim = col.GetComponentInParent<PlayerHealth>();
            if (victim == null || victim.netId == attackerNetId)
                continue;

            // 맞은 방향으로 시체가 날아가도록 타격 방향을 함께 넘긴다.
            Vector3 blow = victim.transform.position - t.position;
            blow.y = 0f;
            if (blow.sqrMagnitude < 0.0001f)
                blow = t.forward;
            victim.ServerKill(attackerNetId, blow.normalized);
        }
    }

    /// <summary>지정 플레이어가 든 무기를 그 자리에 떨어뜨린다(사망 시 권력 이동).</summary>
    [Server]
    public void ServerDropWeaponOf(uint holderNetId, Vector3 pos)
    {
        int id = HeldWeaponOf(holderNetId);
        if (id < 0)
            return;

        heldBy[id] = 0;
        freePose[id] = (pos, Vector3.zero);
        RpcFree(id, holderNetId, pos, Vector3.zero);
    }

    private int HeldWeaponOf(uint holder)
    {
        foreach (var kv in heldBy)
            if (kv.Value == holder)
                return kv.Key;
        return -1;
    }

    // ------------------------------------------------------------ 클라이언트 반영

    [ClientRpc]
    private void RpcHold(int weaponId, uint holderNetId)
    {
        Weapon w = Resolve(weaponId);
        PlayerInteraction pi = ResolvePlayer(holderNetId);
        if (w != null && pi != null)
            pi.AttachWeaponVisual(w);
    }

    [ClientRpc]
    private void RpcFree(int weaponId, uint prevHolderNetId, Vector3 pos, Vector3 euler)
    {
        Weapon w = Resolve(weaponId);
        if (w == null)
            return;

        PlayerInteraction pi = prevHolderNetId != 0 ? ResolvePlayer(prevHolderNetId) : null;
        if (pi != null)
            pi.DetachWeaponVisual(w, pos, Quaternion.Euler(euler));
        else
            w.DetachTo(pos, Quaternion.Euler(euler), false);
    }

    [ClientRpc]
    private void RpcSwing(uint holderNetId)
    {
        PlayerInteraction pi = ResolvePlayer(holderNetId);
        if (pi != null)
            pi.PlaySwingVisual();
    }

    [Command(requiresAuthority = false)]
    private void CmdRequestSync(NetworkConnectionToClient sender = null)
    {
        int[] ids = registry.Keys.ToArray();
        uint[] holders = new uint[ids.Length];
        Vector3[] positions = new Vector3[ids.Length];
        Vector3[] eulers = new Vector3[ids.Length];

        for (int i = 0; i < ids.Length; i++)
        {
            heldBy.TryGetValue(ids[i], out holders[i]);
            if (holders[i] == 0 && freePose.TryGetValue(ids[i], out var pose))
            {
                positions[i] = pose.pos;
                eulers[i] = pose.euler;
            }
        }

        TargetSync(sender, ids, holders, positions, eulers);
    }

    [TargetRpc]
    private void TargetSync(NetworkConnectionToClient target, int[] ids, uint[] holders, Vector3[] positions, Vector3[] eulers)
    {
        for (int i = 0; i < ids.Length; i++)
        {
            Weapon w = Resolve(ids[i]);
            if (w == null)
                continue;

            if (holders[i] != 0)
            {
                PlayerInteraction pi = ResolvePlayer(holders[i]);
                if (pi != null)
                    pi.AttachWeaponVisual(w);
            }
            else
            {
                w.DetachTo(positions[i], Quaternion.Euler(eulers[i]), false);
            }
        }
    }

    // ------------------------------------------------------------ 조회

    private Weapon Resolve(int weaponId)
    {
        registry.TryGetValue(weaponId, out Weapon w);
        return w;
    }

    private static PlayerInteraction ResolvePlayer(uint netId)
    {
        NetworkIdentity identity = null;
        if (NetworkServer.active)
            NetworkServer.spawned.TryGetValue(netId, out identity);
        else
            NetworkClient.spawned.TryGetValue(netId, out identity);

        return identity != null ? identity.GetComponent<PlayerInteraction>() : null;
    }
}
