using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

/// <summary>
/// 집 스테이지의 캠핑 준비물 체크리스트를 서버 권한으로 관리하는 팀 공용 매니저.
/// 아이템은 고유 ID로 식별하며, 획득 상태와 진행도를 모든 클라이언트 및 늦은 참가자에게 동기화한다.
/// </summary>
public class CampChecklistManager : NetworkBehaviour
{
    public static CampChecklistManager Instance { get; private set; }
    public static event Action OnChanged;

    public string[] Names { get; private set; } = Array.Empty<string>();
    public int[] Need { get; private set; } = Array.Empty<int>();
    public int[] Have { get; private set; } = Array.Empty<int>();

    private readonly Dictionary<int, CollectibleItem> registry = new Dictionary<int, CollectibleItem>();
    private readonly Dictionary<int, string> idToName = new Dictionary<int, string>();
    private readonly Dictionary<string, int> needCounts = new Dictionary<string, int>();
    private readonly Dictionary<string, int> haveCounts = new Dictionary<string, int>();
    private readonly HashSet<int> collectedIds = new HashSet<int>();

    private void Awake()
    {
        Instance = this;
        BuildRegistry();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void BuildRegistry()
    {
        registry.Clear();
        foreach (CollectibleItem item in FindObjectsByType<CollectibleItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
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

        foreach (KeyValuePair<int, CollectibleItem> pair in registry)
        {
            string itemName = pair.Value.DisplayName;
            idToName[pair.Key] = itemName;
            needCounts[itemName] = (needCounts.TryGetValue(itemName, out int count) ? count : 0) + 1;
            if (!haveCounts.ContainsKey(itemName))
                haveCounts[itemName] = 0;
        }
    }

    public override void OnStartClient()
    {
        BuildRegistry();
        CmdRequestSync();
    }

    public void RequestCollect(int id)
    {
        if (!NetworkClient.active && !NetworkServer.active)
        {
            if (registry.TryGetValue(id, out CollectibleItem item) && item != null)
                item.gameObject.SetActive(false);
            return;
        }

        CmdCollect(id);
    }

    [Command(requiresAuthority = false)]
    private void CmdCollect(int id)
    {
        if (collectedIds.Contains(id) || !idToName.TryGetValue(id, out string itemName))
            return;

        collectedIds.Add(id);
        haveCounts[itemName] = Mathf.Min(haveCounts[itemName] + 1, needCounts[itemName]);
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

    private void HideItem(int id)
    {
        if (registry.TryGetValue(id, out CollectibleItem item) && item != null)
            item.gameObject.SetActive(false);
    }

    private void SetProgress(string[] names, int[] need, int[] have)
    {
        Names = names;
        Need = need;
        Have = have;
        OnChanged?.Invoke();
    }

    public bool IsComplete()
    {
        if (NetworkServer.active)
        {
            if (needCounts.Count == 0)
                return false;

            foreach (KeyValuePair<string, int> pair in needCounts)
            {
                haveCounts.TryGetValue(pair.Key, out int have);
                if (have < pair.Value)
                    return false;
            }
            return true;
        }

        if (Names.Length == 0)
            return false;

        int count = Mathf.Min(Need.Length, Have.Length);
        for (int i = 0; i < count; i++)
            if (Have[i] < Need[i])
                return false;
        return true;
    }

    [Server]
    private void BuildProgress(out string[] names, out int[] need, out int[] have)
    {
        names = needCounts.Keys.OrderBy(key => key).ToArray();
        need = new int[names.Length];
        have = new int[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            need[i] = needCounts[names[i]];
            have[i] = haveCounts.TryGetValue(names[i], out int count) ? count : 0;
        }
    }
}
