#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Phase 6: 스테이지 전환. 현재 씬(집=Stage1)의 현관 밖에 탈출 트리거를 놓고,
/// 준비물 완료 시 다음 스테이지(Stage2_Campground)로 넘어가게 한다.
/// </summary>
public static class NetworkPhase6Setup
{
    public const string Stage1Scene = "NetworkDemo";
    public const string Stage2Scene = "Stage2_Campground";
    private const string Stage1ScenePath = "Assets/Scenes/NetworkDemo.unity";
    private const string Stage2ScenePath = "Assets/Scenes/Stage2_Campground.unity";

    [MenuItem("Tools/Multiplayer/Phase 6/Setup Escape (Stage1 -> Stage2)")]
    public static void SetupStage1Escape()
    {
        // 집 현관(남쪽 문 z=-12) 바로 밖에 탈출 트리거를 놓는다.
        CreateEscapeZone(new Vector3(0f, 1.5f, -13.4f), new Vector3(3.6f, 3f, 2.2f), Stage2Scene, false, Stage1Scene);

        EnsureSceneInBuildSettings(Stage1ScenePath);
        EnsureSceneInBuildSettings(Stage2ScenePath);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[Phase6] Stage1 탈출 트리거 배치 완료(준비물 완료 시 Stage2로 전환). 씬을 저장하세요(Ctrl+S).");
    }

    /// <summary>활성 씬에 EscapeZone 오브젝트를 만든다. (Stage2 빌더도 사용)</summary>
    public static GameObject CreateEscapeZone(Vector3 pos, Vector3 size, string nextScene, bool loopIfEmpty, string startScene)
    {
        GameObject existing = GameObject.Find("EscapeZone");
        if (existing == null)
        {
            existing = new GameObject("EscapeZone");
            Undo.RegisterCreatedObjectUndo(existing, "Create Escape Zone");
        }
        existing.transform.position = pos;

        var box = existing.GetComponent<BoxCollider>();
        if (box == null) box = Undo.AddComponent<BoxCollider>(existing);
        box.isTrigger = true;
        box.size = size;

        if (existing.GetComponent<NetworkIdentity>() == null)
            Undo.AddComponent<NetworkIdentity>(existing);

        var zone = existing.GetComponent<EscapeZone>();
        if (zone == null) zone = Undo.AddComponent<EscapeZone>(existing);

        var so = new SerializedObject(zone);
        so.FindProperty("nextSceneName").stringValue = nextScene ?? "";
        so.FindProperty("loopToStartIfEmpty").boolValue = loopIfEmpty;
        so.FindProperty("startSceneName").stringValue = startScene ?? Stage1Scene;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(existing);
        return existing;
    }

    /// <summary>씬을 Build Settings에 없으면 추가한다.</summary>
    public static void EnsureSceneInBuildSettings(string scenePath)
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == scenePath))
            return;
        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log($"[Phase6] Build Settings에 씬 추가: {scenePath}");
    }
}
#endif
