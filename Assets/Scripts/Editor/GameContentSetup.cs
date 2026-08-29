#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 게임 전체(폰트 · 무기 · 플레이어 · 타이틀 · 캠핑장 · 대기실)를 올바른 순서로 한 번에 만든다.
///
/// 순서가 중요하다.
///  1) TMP 폰트가 기본으로 잡혀 있어야 런타임에 만드는 HUD 글자에 한글이 나온다.
///  2) 무기 프리팹이 있어야 캠핑장과 대기실이 그걸 집어다 놓을 수 있고,
///  3) 플레이어 애니메이션 셋업이 RemoteAvatar를 goshi로 바꾼 뒤라야
///     래그돌 빌더가 같은 뼈대에서 시체를 만들 수 있다.
///  4) 씬을 갈아엎는 작업은 프리팹이 다 끝난 뒤에 한다.
///  5) 대기실을 <b>마지막</b>에 만든다 — 대기실 빌더가 타이틀 연결과
///     Build Settings 순서(타이틀 → 대기실 → 성채 → 캠핑장)를 정리하기 때문이다.
///
/// 메뉴: Tools > Setup > Build Everything
/// </summary>
public static class GameContentSetup
{
    [MenuItem("Tools/Setup/Build Everything (전체 다시 만들기)", priority = 0)]
    public static void BuildEverything()
    {
        bool ok = EditorUtility.DisplayDialog(
            "전체 다시 만들기",
            "다음을 순서대로 실행합니다.\n\n" +
            "1. TMP 폰트 (Jalnan2 SDF) 생성 및 기본 폰트 지정\n" +
            "2. 무기 프리팹 (.blend → 메시/머티리얼/프리팹)\n" +
            "3. 플레이어 애니메이션 (goshi)\n" +
            "4. 래그돌 시체 프리팹\n" +
            "5. 타이틀 화면\n" +
            "6. 캠핑장 씬 (Stage2_Campground)\n" +
            "7. 협동 탈출 성채 (Stage3_CursedFortress)\n" +
            "8. 대기실 씬 (Lobby) + Build Settings 정리\n\n" +
            "씬을 새로 만들기 때문에 현재 씬에서 손으로 고쳐 둔 것은 사라집니다.\n" +
            "계속할까요?",
            "실행", "취소");

        if (!ok)
            return;

        if (!JalnanFontAssetBuilder.ApplyAsTmpDefault(out string fontProblem))
            Debug.LogWarning("[Setup] TMP 기본 폰트 지정 실패 — HUD 한글이 깨질 수 있습니다.\n" + fontProblem);

        WeaponPrefabBuilder.BuildAll();
        PlayerAnimatorSetup.SetupCharacterAnimations();
        RagdollBuilder.BuildRagdoll();

        TitleSceneSetup.SetupMainTitle();
        Stage2Builder.BuildStage2();
        Stage3FortressBuilder.BuildStage3();
        LobbyBuilder.BuildLobby();

        Debug.Log("[Setup] 전체 생성 완료.\n" +
                  "타이틀 → 대기실 → 성채/캠핑장 순서로 Build Settings가 정리되었습니다.\n" +
                  "MainTitle 씬을 열고 플레이하면 처음부터 확인할 수 있습니다.");
    }

    /// <summary>
    /// 씬은 그대로 두고 프리팹만 다시 굽는다.
    /// 맵을 손으로 배치해 둔 뒤 무기나 캐릭터만 고치고 싶을 때 쓴다.
    /// </summary>
    [MenuItem("Tools/Setup/Rebuild Prefabs Only (씬 유지)", priority = 1)]
    public static void RebuildPrefabsOnly()
    {
        WeaponPrefabBuilder.BuildAll();
        PlayerAnimatorSetup.SetupCharacterAnimations();
        RagdollBuilder.BuildRagdoll();
        Stage2Builder.RebuildProps();

        Debug.Log("[Setup] 프리팹만 다시 구웠습니다. 씬 배치는 그대로입니다.");
    }
}
#endif
