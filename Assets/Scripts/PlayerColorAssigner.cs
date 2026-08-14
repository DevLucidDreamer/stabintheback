using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 서버가 플레이어에게 캐릭터 색을 나눠 준다.
///
/// 규칙
///  · 아직 아무도 안 쓰는 색을 먼저 준다 → 정원이 색 개수(4) 이하면 절대 겹치지 않는다.
///  · 색이 모자라면(5명째부터) 가장 적게 쓰인 색 중에서 무작위로 준다.
///  · 한 번 받은 색은 연결이 살아 있는 동안 유지된다 — 대기실에서 캠핑장으로 넘어가며
///    플레이어가 다시 스폰돼도 색이 바뀌지 않는다.
///
/// 끊긴 연결의 기록은 배정할 때마다 청소하므로, 방을 닫았다 다시 열어도 저절로 초기화된다.
/// (정적 상태를 따로 리셋해 줄 필요가 없다)
/// </summary>
public static class PlayerColorAssigner
{
    private static readonly Dictionary<int, int> byConnection = new Dictionary<int, int>();

    public static int Assign(int connectionKey, int paletteSize, PlayerColor requester)
    {
        if (paletteSize <= 0)
            return -1;

        Prune();

        // 주인이 없는 오브젝트(음수 열쇠)는 기억해 둘 이유가 없다.
        bool remember = connectionKey >= 0;

        if (remember && byConnection.TryGetValue(connectionKey, out int kept) && kept >= 0 && kept < paletteSize)
            return kept;

        var used = new int[paletteSize];
        foreach (PlayerColor other in Object.FindObjectsByType<PlayerColor>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (other == requester)
                continue;

            int index = other.ColorIndex;
            if (index >= 0 && index < paletteSize)
                used[index]++;
        }

        int fewest = int.MaxValue;
        foreach (int count in used)
            if (count < fewest)
                fewest = count;

        var candidates = new List<int>(paletteSize);
        for (int i = 0; i < paletteSize; i++)
            if (used[i] == fewest)
                candidates.Add(i);

        int pick = candidates[Random.Range(0, candidates.Count)];
        if (remember)
            byConnection[connectionKey] = pick;
        return pick;
    }

    /// <summary>이미 끊어진 연결의 기록을 지운다.</summary>
    private static void Prune()
    {
        if (byConnection.Count == 0)
            return;

        List<int> stale = null;
        foreach (KeyValuePair<int, int> pair in byConnection)
        {
            if (NetworkServer.connections.ContainsKey(pair.Key))
                continue;

            stale ??= new List<int>();
            stale.Add(pair.Key);
        }

        if (stale == null)
            return;

        foreach (int key in stale)
            byConnection.Remove(key);
    }
}
