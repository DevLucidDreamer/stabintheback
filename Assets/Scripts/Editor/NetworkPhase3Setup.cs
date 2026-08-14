#if UNITY_EDITOR
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 캠핑장 진행 매니저 셋업.
/// - 씬에 CampGameManager(+CampHud, NetworkIdentity) 오브젝트를 하나 만든다.
/// - 모든 CollectibleItem에 고유 ID를 부여한다(씬에 저장되어 모든 클라이언트가 공유).
///
/// 재료를 다시 배치했다면 이 메뉴를 다시 실행해야 ID가 맞는다.
/// (캠핑장 빌더는 마지막에 이걸 자동으로 부른다)
/// </summary>
public static class NetworkPhase3Setup
{
    [MenuItem("Tools/Multiplayer/Phase 3/Setup Camp Game Sync")]
    public static void SetupChecklistSync()
    {
        CampGameManager manager = EnsureManager();

        CollectibleItem[] items = Object.FindObjectsByType<CollectibleItem>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (items.Length == 0)
        {
            EditorUtility.DisplayDialog("재료 없음",
                "현재 씬에 재료(CollectibleItem)가 없습니다.\n" +
                "'Tools > Stage > Build Stage 2 (Campground)'로 캠핑장을 먼저 만드세요.", "확인");
            return;
        }

        for (int i = 0; i < items.Length; i++)
        {
            items[i].SetItemId(i);
            EditorUtility.SetDirty(items[i]);
        }

        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[CampGame] 매니저 준비 완료, 재료 {items.Length}개에 ID 부여. 씬을 저장하세요(Ctrl+S).");
    }

    /// <summary>씬에 CampGameManager를 보장하고 돌려준다. 빌더도 이걸 쓴다.</summary>
    public static CampGameManager EnsureManager()
    {
        CampGameManager manager = Object.FindFirstObjectByType<CampGameManager>(FindObjectsInactive.Include);
        if (manager == null)
        {
            var go = new GameObject("CampGameManager");
            go.AddComponent<NetworkIdentity>();
            manager = go.AddComponent<CampGameManager>();
        }
        else if (manager.GetComponent<NetworkIdentity>() == null)
        {
            manager.gameObject.AddComponent<NetworkIdentity>();
        }

        // 진행 상황을 화면에 그려 주는 표시 전용 컴포넌트.
        if (manager.GetComponent<CampHud>() == null)
            manager.gameObject.AddComponent<CampHud>();

        return manager;
    }
}
#endif
