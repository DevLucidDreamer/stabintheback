#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// goshi 캐릭터의 이동 애니메이션을 한 번에 셋업하는 에디터 유틸.
///
/// 1) goshi FBX 안의 클립(start/running/stop/jump)을 이름으로 찾아
///    AnimatorController(GoshiAnimator.controller)를 자동 생성한다.
///    상태: Idle → Start → Run → Stop → Idle, 어디서든 Jump.
/// 2) NetworkPlayer.prefab의 RemoteAvatar(남에게 보이는 몸)를 goshi 모델 +
///    Animator + PlayerAnimator 로 교체한다.
///
/// 두 플레이 씬 모두 NetworkManager가 이 프리팹을 스폰하므로,
/// 프리팹을 한 번 셋업하면 양쪽 씬에 자동 적용된다.
/// </summary>
public static class PlayerAnimatorSetup
{
    private const string FbxPath = "Assets/Player/goshi(final).fbx";
    private const string ControllerPath = "Assets/Player/GoshiAnimator.controller";
    private const string PlayerPrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";

    [MenuItem("Tools/Player/Setup Character Animations")]
    public static void SetupCharacterAnimations()
    {
        AnimatorController controller = BuildController();
        if (controller == null)
            return;

        WireIntoPrefab(controller);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[PlayerAnimator] 셋업 완료. NetworkPlayer 프리팹의 RemoteAvatar가 goshi 모델로 교체되었습니다.");
    }

    // ------------------------------------------------------------ AnimatorController 생성

    private static AnimatorController BuildController()
    {
        // FBX 안의 애니메이션 클립 수집
        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(FbxPath)
            .OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__"))
            .ToArray();

        if (clips.Length == 0)
        {
            EditorUtility.DisplayDialog("클립 없음",
                $"{FbxPath} 안에서 애니메이션 클립을 찾지 못했습니다.\nFBX import의 Animation 탭을 확인하세요.", "OK");
            return null;
        }

        Debug.Log("[PlayerAnimator] FBX 클립: " + string.Join(", ", clips.Select(c => c.name)));

        AnimationClip startClip = FindClip(clips, "start");
        AnimationClip runClip = FindClip(clips, "running", "run");
        AnimationClip stopClip = FindClip(clips, "stop");
        AnimationClip jumpClip = FindClip(clips, "jump");

        // running 루프 설정(반복되어야 자연스러움)
        SetLoop(runClip, true);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        AnimatorState idle = sm.AddState("Idle");
        AnimatorState start = sm.AddState("Start");
        AnimatorState run = sm.AddState("Run");
        AnimatorState stop = sm.AddState("Stop");
        AnimatorState jump = sm.AddState("Jump");

        idle.motion = null;             // 대기 클립이 없으므로 비워둠(정지 자세 유지)
        start.motion = startClip;
        run.motion = runClip;
        stop.motion = stopClip;
        jump.motion = jumpClip;

        sm.defaultState = idle;

        // Idle → Start : 움직이기 시작
        AddTransition(idle, start, hasExit: false, exitTime: 0f, dur: 0.05f,
            ("IsMoving", AnimatorConditionMode.If, 0f));

        // Start → Run : 시작 동작이 끝나면 자동으로 달리기 루프
        AddTransition(start, run, hasExit: true, exitTime: 0.85f, dur: 0.1f);

        // Start 도중에 멈추면 바로 Stop 으로
        AddTransition(start, stop, hasExit: false, exitTime: 0f, dur: 0.1f,
            ("IsMoving", AnimatorConditionMode.IfNot, 0f));

        // Run → Stop : 멈춤
        AddTransition(run, stop, hasExit: false, exitTime: 0f, dur: 0.1f,
            ("IsMoving", AnimatorConditionMode.IfNot, 0f));

        // Stop → Idle : 멈춤 동작이 끝나면 대기
        AddTransition(stop, idle, hasExit: true, exitTime: 0.85f, dur: 0.1f);

        // Stop 도중 다시 움직이면 Start 로
        AddTransition(stop, start, hasExit: false, exitTime: 0f, dur: 0.1f,
            ("IsMoving", AnimatorConditionMode.If, 0f));

        // 어디서든 Jump
        AnimatorStateTransition anyJump = sm.AddAnyStateTransition(jump);
        anyJump.hasExitTime = false;
        anyJump.duration = 0.05f;
        anyJump.canTransitionToSelf = false;
        anyJump.AddCondition(AnimatorConditionMode.If, 0f, "Jump");

        // Jump 착지 후: 움직이는 중이면 Run, 아니면 Idle
        AddTransition(jump, run, hasExit: true, exitTime: 0.8f, dur: 0.1f,
            ("IsMoving", AnimatorConditionMode.If, 0f));
        AddTransition(jump, idle, hasExit: true, exitTime: 0.8f, dur: 0.1f,
            ("IsMoving", AnimatorConditionMode.IfNot, 0f));

        EditorUtility.SetDirty(controller);
        return controller;
    }

    // ------------------------------------------------------------ 프리팹에 모델/애니메이터 연결

    private static void WireIntoPrefab(AnimatorController controller)
    {
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (modelAsset == null)
        {
            EditorUtility.DisplayDialog("모델 없음", $"{FbxPath} 를 불러오지 못했습니다.", "OK");
            return;
        }

        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<Avatar>().FirstOrDefault();

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            // 루트에 붙어있던 기본 캡슐 메시는 goshi와 겹치므로 끈다.
            MeshRenderer rootMesh = root.GetComponent<MeshRenderer>();
            if (rootMesh != null)
                rootMesh.enabled = false;

            // 기존 RemoteAvatar 자식이 있으면 제거하고 새로 만든다.
            Transform old = root.transform.Find("RemoteAvatar");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);

            GameObject remoteAvatar = new GameObject("RemoteAvatar");
            remoteAvatar.transform.SetParent(root.transform, false);

            // goshi 모델 인스턴스
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = "GoshiModel";
            model.transform.SetParent(remoteAvatar.transform, false);
            // CharacterController 중심이 루트 원점이고 높이 2m이므로 발이 y=-1 에 오도록 내림.
            model.transform.localPosition = new Vector3(0f, -1f, 0f);

            Animator animator = model.GetComponent<Animator>();
            if (animator == null)
                animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            if (avatar != null)
                animator.avatar = avatar;
            animator.applyRootMotion = false;

            if (model.GetComponent<PlayerAnimator>() == null)
                model.AddComponent<PlayerAnimator>(); // motionSource는 런타임에 transform.root로 자동 설정

            // 원격 시선(고개) 회전용 빈 오브젝트. NetworkPlayerSetup이 이 이름을 찾는다.
            GameObject headPitch = new GameObject("HeadPitch");
            headPitch.transform.SetParent(remoteAvatar.transform, false);
            headPitch.transform.localPosition = new Vector3(0f, 0.6f, 0f);

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ------------------------------------------------------------ 헬퍼

    private static AnimationClip FindClip(AnimationClip[] clips, params string[] keys)
    {
        foreach (string key in keys)
        {
            AnimationClip match = clips.FirstOrDefault(c => c.name.ToLowerInvariant().Contains(key));
            if (match != null)
                return match;
        }
        Debug.LogWarning("[PlayerAnimator] 클립을 찾지 못함: " + string.Join("/", keys));
        return null;
    }

    /// <summary>
    /// FBX 내부 클립은 읽기 전용 서브에셋이라 직접 못 바꾼다.
    /// ModelImporter의 clipAnimations에서 해당 이름의 loopTime을 켜고 재임포트한다.
    /// (해당 클립이 아직 importer 목록에 없으면 조용히 건너뛴다 — 필요시 import 설정에서 수동 설정)
    /// </summary>
    private static void SetLoop(AnimationClip clip, bool loop)
    {
        if (clip == null)
            return;

        var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (importer == null)
            return;

        ModelImporterClipAnimation[] clipDefs = importer.clipAnimations;
        if (clipDefs == null || clipDefs.Length == 0)
            clipDefs = importer.defaultClipAnimations;

        bool changed = false;
        foreach (ModelImporterClipAnimation def in clipDefs)
        {
            if (def.name == clip.name && def.loopTime != loop)
            {
                def.loopTime = loop;
                changed = true;
            }
        }

        if (changed)
        {
            importer.clipAnimations = clipDefs;
            importer.SaveAndReimport();
        }
    }

    private static void AddTransition(AnimatorState from, AnimatorState to,
        bool hasExit, float exitTime, float dur,
        params (string param, AnimatorConditionMode mode, float threshold)[] conditions)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = hasExit;
        if (hasExit)
            t.exitTime = exitTime;
        t.hasFixedDuration = true;
        t.duration = dur;
        foreach (var c in conditions)
            t.AddCondition(c.mode, c.threshold, c.param);
    }
}
#endif
