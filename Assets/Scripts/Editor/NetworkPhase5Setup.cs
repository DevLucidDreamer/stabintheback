#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Phase 5: 마검 전투(원킬·리스폰) 셋업.
/// NetworkPlayer 프리팹에 PlayerHealth를 추가한다. (살상 판정은 WeaponNetworkManager가 처리)
/// </summary>
public static class NetworkPhase5Setup
{
    private const string PlayerPrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";

    [MenuItem("Tools/Multiplayer/Phase 5/Setup Combat")]
    public static void SetupCombat()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("NetworkPlayer 프리팹 없음", "먼저 Phase 1 셋업을 실행해 프리팹을 만드세요.", "OK");
            return;
        }

        if (prefab.GetComponent<PlayerHealth>() != null)
        {
            Debug.Log("[Phase5] NetworkPlayer에 PlayerHealth가 이미 있습니다.");
            return;
        }

        GameObject contents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        if (contents.GetComponent<PlayerHealth>() == null)
            contents.AddComponent<PlayerHealth>();
        PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
        PrefabUtility.UnloadPrefabContents(contents);

        Debug.Log("[Phase5] NetworkPlayer 프리팹에 PlayerHealth 추가 완료. 마검을 든 플레이어의 스윙이 원킬이 됩니다.");
    }
}
#endif
