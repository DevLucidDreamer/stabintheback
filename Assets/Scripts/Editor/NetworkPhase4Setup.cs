#if UNITY_EDITOR
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Phase 4: 무기 네트워크 셋업.
/// - 씬에 WeaponNetworkManager(NetworkIdentity) 오브젝트를 하나 만든다.
/// - 모든 Weapon에 고유 ID를 부여한다(씬에 저장되어 모든 클라이언트가 공유).
///
/// 무기/집을 다시 생성했다면 이 메뉴를 다시 실행해야 한다.
/// </summary>
public static class NetworkPhase4Setup
{
    [MenuItem("Tools/Multiplayer/Phase 4/Setup Weapon Sync")]
    public static void SetupWeaponSync()
    {
        WeaponNetworkManager manager = Object.FindFirstObjectByType<WeaponNetworkManager>(FindObjectsInactive.Include);
        if (manager == null)
        {
            var go = new GameObject("WeaponManager");
            Undo.RegisterCreatedObjectUndo(go, "Create Weapon Manager");
            Undo.AddComponent<NetworkIdentity>(go);
            manager = Undo.AddComponent<WeaponNetworkManager>(go);
        }
        else if (manager.GetComponent<NetworkIdentity>() == null)
        {
            Undo.AddComponent<NetworkIdentity>(manager.gameObject);
        }

        Weapon[] weapons = Object.FindObjectsByType<Weapon>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (weapons.Length == 0)
        {
            EditorUtility.DisplayDialog("Weapon 없음", "현재 씬에 무기가 없습니다. NetworkDemo 씬을 열고 실행하세요.", "OK");
            return;
        }

        for (int i = 0; i < weapons.Length; i++)
        {
            weapons[i].SetWeaponId(i);
            EditorUtility.SetDirty(weapons[i]);
        }

        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[Phase4] WeaponManager 준비, Weapon {weapons.Length}개에 ID 부여. 씬을 저장하세요(Ctrl+S).");
    }
}
#endif
