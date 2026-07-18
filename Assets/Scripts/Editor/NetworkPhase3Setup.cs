#if UNITY_EDITOR
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Phase 3: 협동(팀 공용) 준비물 체크리스트 네트워크 셋업.
/// - 씬에 CampChecklistManager(NetworkIdentity) 오브젝트를 하나 만든다.
/// - 모든 CollectibleItem에 고유 ID를 부여한다(씬에 저장되어 모든 클라이언트가 공유).
///
/// 아이템/집을 다시 생성했다면 이 메뉴를 다시 실행해야 한다.
/// </summary>
public static class NetworkPhase3Setup
{
    [MenuItem("Tools/Multiplayer/Phase 3/Setup Checklist Sync")]
    public static void SetupChecklistSync()
    {
        // 1) 매니저 오브젝트 보장
        CampChecklistManager manager = Object.FindFirstObjectByType<CampChecklistManager>(FindObjectsInactive.Include);
        if (manager == null)
        {
            var go = new GameObject("ChecklistManager");
            Undo.RegisterCreatedObjectUndo(go, "Create Checklist Manager");
            Undo.AddComponent<NetworkIdentity>(go);
            manager = Undo.AddComponent<CampChecklistManager>(go);
        }
        else if (manager.GetComponent<NetworkIdentity>() == null)
        {
            Undo.AddComponent<NetworkIdentity>(manager.gameObject);
        }

        // 2) 모든 CollectibleItem에 고유 ID 부여
        CollectibleItem[] items = Object.FindObjectsByType<CollectibleItem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (items.Length == 0)
        {
            EditorUtility.DisplayDialog("CollectibleItem 없음", "현재 씬에 준비물이 없습니다. NetworkDemo 씬을 열고 실행하세요.", "OK");
            return;
        }

        for (int i = 0; i < items.Length; i++)
        {
            items[i].SetItemId(i);
            EditorUtility.SetDirty(items[i]);
        }

        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[Phase3] ChecklistManager 준비, CollectibleItem {items.Length}개에 ID 부여. 씬을 저장하세요(Ctrl+S).");
    }
}
#endif
