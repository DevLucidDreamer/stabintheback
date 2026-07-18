using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

/// <summary>
/// 캠핑 준비물 체크리스트를 서버 권한으로 관리하는 팀 공용(협동) 매니저 (Phase 3).
/// - 씬에 단 하나 존재하는 NetworkIdentity 오브젝트.
/// - 각 CollectibleItem은 고유 ID로 식별된다(아이템엔 NetworkIdentity 불필요).
/// - 아무나 아이템을 챙기면 서버가 확정하고, 모든 클라이언트에서 그 아이템을 숨기고
///   공용 진행도를 갱신한다. 늦게 접속한 클라이언트도 현재 상태로 맞춰진다.
/// SyncList/SyncDictionary 대신 Command/Rpc로 전체 상태를 전송해 버전 의존성을 줄였다.
/// </summary>
public class CampChecklistManager : NetworkBehaviour
{
    public static CampChecklistManager Instance { get; private set; }

    /// <summary>진행도가 바뀌면 호출(각 ItemChecklist가 구독해 UI 갱신).</summary>
    public static event Action OnChanged;

    // 클라이언트에서 UI가 읽는 진행 상태(이름/필요/보유, 이름 오름차순).
    public string[] Names { get; private set; } = Array.Empty<string>();
    public int[] Need { get; private set; } = Array.Empty<int>();
    public int[] Have { get; private set; } = Array.Empty<int>();

    // id → 아이템 (서버·클라이언트 공통, 런타임 스캔)
    private readonly Dictionary<int, CollectibleItem> registry = new Dictionary<int, CollectibleItem>();

    // 서버 전용 상태
    private readonly Dictionary<int, string> idToName = new Dictionary<int, string>();
    private readonly Dictionary<string, int> needCounts = new Dictionary<string, int>();
    private readonly Dictionary<string, int> haveCounts = new Dictionary<string, int>();
    private readonly HashSet<int> collectedIds = new HashSet<int>();

    private void Awake()
    {
        Instance = this;
        BuildRegistry(); // 오프라인/조기 접근 대비
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void BuildRegistry()
    {
        registry.Clear();
        foreach (var item in FindObjectsByType<CollectibleItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (item.ItemId >= 0)
                registry[item.ItemId] = item;
        }
    }

    public override void OnStartServer()
    {
        BuildRegistry();
        idToName.Clear();
        needCounts.Clear();
        haveCounts.Clear();
        collectedIds.Clear();

        foreach (var kv in registry)
        {
            string n = kv.Value.DisplayName;
            idToName[kv.Key] = n;
            needCounts[n] = (needCounts.TryGetValue(n, out int c) ? c : 0) + 1;
            if (!haveCounts.ContainsKey(n))
                haveCounts[n] = 0;
        }
    }

    public override void OnStartClient()
    {
        BuildRegistry();
        CmdRequestSync(); // 현재 상태(획득 목록 + 진행도)를 받아온다
    }

    // ------------------------------------------------------------ 획득 흐름

    /// <summary>CollectibleItem.Interact가 호출한다.</summary>
    public void RequestCollect(int id)
    {
        if (!NetworkClient.active && !NetworkServer.active)
        {
            // 오프라인 폴백: 로컬에서 바로 숨김.
            if (registry.TryGetValue(id, out var item) && item != null)
                item.gameObject.SetActive(false);
            return;
        }

        CmdCollect(id);
    }

    [Command(requiresAuthority = false)]
    private void CmdCollect(int id)
    {
        if (collectedIds.Contains(id))
            return;
        if (!idToName.TryGetValue(id, out string n))
            return;

        collectedIds.Add(id);
        haveCounts[n] = Mathf.Min(haveCounts[n] + 1, needCounts[n]);

        BuildProgress(out string[] names, out int[] need, out int[] have);
        RpcCollected(id, names, need, have);
    }

    [ClientRpc]
    private void RpcCollected(int id, string[] names, int[] need, int[] have)
    {
        HideItem(id);
        SetProgress(names, need, have);
    }

    [Command(requiresAuthority = false)]
    private void CmdRequestSync(NetworkConnectionToClient sender = null)
    {
        BuildProgress(out string[] names, out int[] need, out int[] have);
        TargetSync(sender, collectedIds.ToArray(), names, need, have);
    }

    [TargetRpc]
    private void TargetSync(NetworkConnectionToClient target, int[] collected, string[] names, int[] need, int[] have)
    {
        foreach (int id in collected)
            HideItem(id);
        SetProgress(names, need, have);
    }

    // ------------------------------------------------------------ 유틸

    private void HideItem(int id)
    {
        if (registry.TryGetValue(id, out var item) && item != null)
            item.gameObject.SetActive(false);
    }

    private void SetProgress(string[] names, int[] need, int[] have)
    {
        Names = names;
        Need = need;
        Have = have;
        OnChanged?.Invoke();
    }

    /// <summary>모든 준비물을 다 챙겼는지. 서버는 권한 딕셔너리로, 클라는 동기화된 배열로 판단.</summary>
    public bool IsComplete()
    {
        if (NetworkServer.active)
        {
            if (needCounts.Count == 0)
                return false;
            foreach (var kv in needCounts)
            {
                haveCounts.TryGetValue(kv.Key, out int h);
                if (h < kv.Value)
                    return false;
            }
            return true;
        }

        if (Names.Length == 0)
            return false;
        int n = Mathf.Min(Need.Length, Have.Length);
        for (int i = 0; i < n; i++)
            if (Have[i] < Need[i])
                return false;
        return true;
    }

    [Server]
    private void BuildProgress(out string[] names, out int[] need, out int[] have)
    {
        names = needCounts.Keys.OrderBy(k => k).ToArray();
        need = new int[names.Length];
        have = new int[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            need[i] = needCounts[names[i]];
            have[i] = haveCounts.TryGetValue(names[i], out int h) ? h : 0;
        }
    }
}
