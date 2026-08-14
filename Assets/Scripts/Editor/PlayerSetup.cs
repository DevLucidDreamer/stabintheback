#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 캐릭터 모델을 바꾼 뒤 눌러야 하는 셋업을 한 번에 묶어 둔다.
///
/// NetworkPlayer.prefab 안의 몸(RemoteAvatar)과 시체(GoshiRagdoll.prefab)는 모델 파일의
/// 메시/아바타/클립을 직접 참조한다. 모델 파일을 갈아 끼우면 이 참조가 전부 끊기므로
/// (인스펙터에 Missing으로 남는다) 프리팹을 다시 만들어 줘야 한다.
///
/// 프리팹이 깨진 채로 플레이하면 Animator가 컨트롤러를 못 찾아
/// "Animator is not playing an AnimatorController" 에러가 매 프레임 쏟아지므로,
/// 에디터를 열 때 한 번 검사해서 알려 준다.
/// </summary>
public static class PlayerSetup
{
    [MenuItem("Tools/Player/Setup Player (모델 + 애니메이션 + 래그돌)", priority = 0)]
    public static void SetupAll()
    {
        PlayerAnimatorSetup.SetupCharacterAnimations();
        RagdollBuilder.BuildRagdoll();
    }

    // ---------------------------------------------------------------- 깨진 상태 알림

    [InitializeOnLoadMethod]
    private static void WarnIfBroken()
    {
        // 임포트/컴파일이 끝난 뒤에 검사해야 애셋을 제대로 읽는다.
        EditorApplication.delayCall += Check;
    }

    [MenuItem("Tools/Player/Check Player Prefab")]
    private static void Check()
    {
        var player = AssetDatabase.LoadAssetAtPath<GameObject>(GoshiModel.PlayerPrefabPath);
        if (player == null)
            return;

        string problem = FindProblem(player);
        if (problem == null)
            return;

        Debug.LogWarning(
            $"[Player] NetworkPlayer 프리팹의 캐릭터가 깨져 있습니다: {problem}\n" +
            "Tools > Player > Setup Player (모델 + 애니메이션 + 래그돌) 를 실행하세요.", player);
    }

    private static string FindProblem(GameObject player)
    {
        var skin = player.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (skin == null)
            return "RemoteAvatar에 캐릭터 메시가 없습니다";
        if (skin.sharedMesh == null)
            return "캐릭터 메시가 없어진 모델 파일을 가리킵니다(Missing)";

        var animator = player.GetComponentInChildren<Animator>(true);
        if (animator == null)
            return "Animator가 없습니다";
        if (animator.runtimeAnimatorController == null)
            return "AnimatorController가 비어 있거나 없어졌습니다";
        if (animator.avatar == null)
            return "Avatar가 없습니다 (Avatar 없이는 클립이 재생되지 않습니다)";

        var ragdoll = player.GetComponent<PlayerRagdoll>();
        if (ragdoll != null && ragdoll.RagdollPrefab == null)
            return "시체(래그돌) 프리팹이 연결돼 있지 않습니다";

        return null;
    }
}
#endif
