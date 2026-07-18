#if UNITY_EDITOR
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Phase 2: 현재 씬의 모든 Openable(서랍/문/냉장고)에
/// NetworkIdentity + NetworkOpenable을 붙여 서버 권한 동기화가 되도록 만든다.
/// 씬 오브젝트이므로 Mirror가 Host/Server 시작 시 자동으로 스폰한다.
///
/// 집을 다시 생성(Tools > House > Build House)했다면 이 메뉴를 다시 실행해야 한다.
/// </summary>
public static class NetworkPhase2Setup
{
    [MenuItem("Tools/Multiplayer/Phase 2/Setup Interactable Sync")]
    public static void SetupInteractableSync()
    {
        Openable[] openables = Object.FindObjectsByType<Openable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (openables.Length == 0)
        {
            EditorUtility.DisplayDialog("Openable 없음", "현재 씬에 Openable이 없습니다. NetworkDemo 씬을 열고 실행하세요.", "OK");
            return;
        }

        int configured = 0;
        foreach (Openable openable in openables)
        {
            GameObject go = openable.gameObject;

            if (go.GetComponent<NetworkIdentity>() == null)
                Undo.AddComponent<NetworkIdentity>(go);

            if (go.GetComponent<NetworkOpenable>() == null)
            {
                Undo.AddComponent<NetworkOpenable>(go);
                configured++;
            }

            EditorUtility.SetDirty(go);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[Phase2] Openable {openables.Length}개 확인, {configured}개에 NetworkOpenable 추가. 씬을 저장하세요(Ctrl+S).");
    }
}
#endif
