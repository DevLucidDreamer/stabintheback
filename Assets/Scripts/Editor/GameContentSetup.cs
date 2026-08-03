#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 무기 · 플레이어 · 캠핑장을 올바른 순서로 한 번에 만든다.
///
/// 순서가 중요하다.
///  1) 무기 프리팹이 있어야 캠핑장이 그걸 집어다 놓을 수 있고,
///  2) 플레이어 애니메이션 셋업이 RemoteAvatar를 goshi로 바꾼 뒤라야
///     래그돌 빌더가 같은 뼈대에서 시체를 만들 수 있다.
///  3) 씬을 갈아엎는 캠핑장 생성은 프리팹 작업이 다 끝난 마지막에 한다.
///
/// 메뉴: Tools > Setup > Build Everything
/// </summary>
public static class GameContentSetup
{
    [MenuItem("Tools/Setup/Build Everything (Weapons, Player, Campground)")]
    public static void BuildEverything()
    {
        bool ok = EditorUtility.DisplayDialog(
            "전체 다시 만들기",
            "다음을 순서대로 실행합니다.\n\n" +
            "1. 무기 프리팹 (.blend → 메시/머티리얼/프리팹)\n" +
            "2. 플레이어 애니메이션 (goshi)\n" +
            "3. 래그돌 시체 프리팹\n" +
            "4. 캠핑장 씬 (Stage2_Campground) — 현재 씬이 닫힙니다\n\n" +
            "계속할까요?",
            "실행", "취소");

        if (!ok)
            return;

        WeaponPrefabBuilder.BuildAll();
        PlayerAnimatorSetup.SetupCharacterAnimations();
        RagdollBuilder.BuildRagdoll();
        Stage2Builder.BuildStage2();

        Debug.Log("[Setup] 전체 생성 완료. 대기실을 다시 만들려면 'Tools > Lobby > Build Lobby'도 실행하세요.");
    }
}
#endif
