#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// goshi 캐릭터의 이동 애니메이션을 한 번에 셋업하는 에디터 유틸.
///
/// 1) 모델 파일 안의 클립을 찾아 AnimatorController(GoshiAnimator.controller)를 만든다.
///    상태: Idle → Start → Run → Stop → Idle, 어디서든 Jump.
/// 2) NetworkPlayer.prefab의 RemoteAvatar(남에게 보이는 몸)를 모델 + Animator +
///    PlayerAnimator 로 교체하고, 캡슐에 맞춰 세운다.
///
/// 두 플레이 씬 모두 NetworkManager가 이 프리팹을 스폰하므로,
/// 프리팹을 한 번 셋업하면 양쪽 씬에 자동 적용된다.
///
/// 메뉴:
///  - Tools/Player/Setup Character Animations : 클립 이름으로 자동 배정해서 바로 적용
///  - Tools/Player/Assign Animation Clips...  : 어떤 클립을 어느 상태에 쓸지 직접 고른다
///    (교체된 모델처럼 클립 이름이 Armature|Action.001 식이라 자동 배정이 애매할 때)
/// </summary>
public static class PlayerAnimatorSetup
{
    private const string ControllerPath = "Assets/Player/GoshiAnimator.controller";
    private const string PlayerPrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";

    /// <summary>모든 상태의 재생 속도 배율. 모델의 클립이 전체적으로 느려서 배로 돌린다.</summary>
    private const float PlaybackSpeed = 2.5f;

    /// <summary>정지 동작만 추가로 더 빠르게. 멈출 때 질질 끌리는 느낌을 없앤다.</summary>
    private const float StopSpeed = PlaybackSpeed * 2f;

    /// <summary>어느 클립을 어느 상태에 쓸지. 재임포트로 클립 참조가 날아가므로 이름으로 들고 다닌다.</summary>
    public struct ClipSet
    {
        public string Idle, Start, Run, Stop, Jump;

        /// <summary>반복 재생해야 자연스러운 클립. 나머지는 한 번만 재생한다.</summary>
        public string[] Looping => new[] { Idle, Run };
    }

    [MenuItem("Tools/Player/Setup Character Animations")]
    public static void SetupCharacterAnimations()
    {
        string modelPath = GoshiModel.FindModelPath();
        if (modelPath == null)
        {
            EditorUtility.DisplayDialog("모델 없음", GoshiModel.MissingMessage, "OK");
            return;
        }

        GoshiModel.EnsureImportSettings(modelPath);
        Apply(modelPath, Guess(GoshiModel.Clips(modelPath)));
    }

    /// <summary>
    /// 클립 이름으로 각 상태에 쓸 클립을 추측한다.
    ///
    /// 대기(Idle)는 이름이 딱 맞는 클립이 없으면 비워 둔다. 가만히 서 있을 때는
    /// 아무 것도 재생하지 않고 마지막 자세를 그대로 유지하는 쪽이 자연스럽다.
    /// 이름으로 못 찾은 점프는 남는 클립으로 채운다
    /// (교체된 모델에는 jump 이름의 클립이 없고 Armature|Action 계열만 남아 있다).
    /// </summary>
    public static ClipSet Guess(AnimationClip[] clips)
    {
        var set = new ClipSet
        {
            Start = MatchName(clips, "start"),
            Run = MatchName(clips, "running", "run"),
            Stop = MatchName(clips, "stop"),
            Jump = MatchName(clips, "jump"),
            Idle = MatchName(clips, "idle"),
        };

        if (set.Jump == null)
        {
            var used = new HashSet<string>(new[] { set.Start, set.Run, set.Stop, set.Idle }
                .Where(n => n != null));
            set.Jump = clips.Select(c => c.name).FirstOrDefault(n => !used.Contains(n));
        }

        return set;
    }

    /// <summary>고른 클립으로 컨트롤러를 만들고 플레이어 프리팹에 적용한다.</summary>
    public static void Apply(string modelPath, ClipSet set)
    {
        // 루프 설정은 재임포트를 일으켜 기존 클립 참조를 무효화한다.
        // 그래서 반드시 컨트롤러를 만들기 "전"에 끝내고, 클립은 그 뒤에 다시 읽어 온다.
        GoshiModel.SetLooping(modelPath, set.Looping);

        AnimationClip[] clips = GoshiModel.Clips(modelPath);
        if (clips.Length == 0)
        {
            EditorUtility.DisplayDialog("클립 없음",
                $"{modelPath} 안에서 애니메이션 클립을 찾지 못했습니다.\nimport 설정의 Animation 탭을 확인하세요.", "OK");
            return;
        }

        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (modelAsset == null)
        {
            EditorUtility.DisplayDialog("모델 없음", GoshiModel.MissingMessage, "OK");
            return;
        }

        AnimatorController controller = BuildController(clips, set);
        WireIntoPrefab(controller, modelAsset, modelPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[PlayerAnimator] 셋업 완료 ({modelPath}).\n" +
                  $"  Idle={Show(set.Idle)}  Start={Show(set.Start)}  Run={Show(set.Run)}  " +
                  $"Stop={Show(set.Stop)}  Jump={Show(set.Jump)}");
    }

    // ------------------------------------------------------------ AnimatorController 생성

    private static AnimatorController BuildController(AnimationClip[] clips, ClipSet set)
    {
        AnimationClip idleClip = Find(clips, set.Idle);
        AnimationClip startClip = Find(clips, set.Start);
        AnimationClip runClip = Find(clips, set.Run);
        AnimationClip stopClip = Find(clips, set.Stop);
        AnimationClip jumpClip = Find(clips, set.Jump);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        // 클립이 있는 상태만 만든다. 교체된 모델처럼 일부 동작이 없어도 나머지는 그대로 돈다.
        AnimatorState idle = sm.AddState("Idle");
        AnimatorState run = sm.AddState("Run");
        AnimatorState start = startClip != null ? sm.AddState("Start") : null;
        AnimatorState stop = stopClip != null ? sm.AddState("Stop") : null;

        // 대기 상태는 보통 비워 둔다(null). 그러면 멈춰 있는 동안 아무 클립도 돌지 않고
        // 직전 자세(=Stop 마지막 프레임)를 그대로 유지한다.
        idle.motion = idleClip;
        run.motion = runClip;
        if (start != null)
            start.motion = startClip;
        if (stop != null)
            stop.motion = stopClip;

        sm.defaultState = idle;

        // 시작/멈춤 동작이 없는 모델이면 Idle ↔ Run 만으로 굴린다.
        AnimatorState toRun = start ?? run;
        AnimatorState toIdle = stop ?? idle;

        // Idle → (Start) : 움직이기 시작
        AddTransition(idle, toRun, hasExit: false, exitTime: 0f, dur: 0.05f,
            ("IsMoving", AnimatorConditionMode.If, 0f));

        if (start != null)
        {
            // Start → Run : 시작 동작이 끝나면 자동으로 달리기 루프
            AddTransition(start, run, hasExit: true, exitTime: 0.85f, dur: 0.1f);

            // Start 도중에 멈추면 바로 Stop(또는 Idle)으로
            AddTransition(start, toIdle, hasExit: false, exitTime: 0f, dur: 0.1f,
                ("IsMoving", AnimatorConditionMode.IfNot, 0f));
        }

        // Run → (Stop) : 멈춤
        AddTransition(run, toIdle, hasExit: false, exitTime: 0f, dur: 0.1f,
            ("IsMoving", AnimatorConditionMode.IfNot, 0f));

        if (stop != null)
        {
            // Stop → Idle : 멈춤 동작이 끝나면 대기
            AddTransition(stop, idle, hasExit: true, exitTime: 0.85f, dur: 0.1f);

            // Stop 도중 다시 움직이면 Start(또는 Run)로
            AddTransition(stop, toRun, hasExit: false, exitTime: 0f, dur: 0.1f,
                ("IsMoving", AnimatorConditionMode.If, 0f));
        }

        // 점프 클립이 있을 때만 점프 상태를 만든다. 없으면 Jump 트리거는 그냥 무시된다.
        if (jumpClip != null)
        {
            AnimatorState jump = sm.AddState("Jump");
            jump.motion = jumpClip;

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
        }

        // 클립 자체가 전체적으로 느려서 상태 단위로 배속을 준다.
        // (전이의 exitTime은 정규화 시간이라 배속을 따라가므로 따로 손댈 게 없다)
        foreach (ChildAnimatorState child in sm.states)
            child.state.speed = PlaybackSpeed;

        if (stop != null)
            stop.speed = StopSpeed;

        EditorUtility.SetDirty(controller);
        return controller;
    }

    // ------------------------------------------------------------ 프리팹에 모델/애니메이터 연결

    private static void WireIntoPrefab(AnimatorController controller, GameObject modelAsset, string modelPath)
    {
        Avatar avatar = GoshiModel.FindAvatar(modelPath);
        if (avatar == null)
        {
            Debug.LogError($"[PlayerAnimator] {modelPath} 에 Avatar가 없습니다. " +
                           "Avatar 없이는 클립이 재생되지 않습니다 (import 설정의 Rig 탭을 확인하세요).");
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            // 루트에 붙어있던 기본 캡슐 메시는 모델과 겹치므로 끈다.
            MeshRenderer rootMesh = root.GetComponent<MeshRenderer>();
            if (rootMesh != null)
                rootMesh.enabled = false;

            // 기존 RemoteAvatar 자식이 있으면 제거하고 새로 만든다.
            Transform old = root.transform.Find("RemoteAvatar");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);

            GameObject remoteAvatar = new GameObject("RemoteAvatar");
            remoteAvatar.transform.SetParent(root.transform, false);

            // 모델 인스턴스
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = "GoshiModel";
            model.transform.SetParent(remoteAvatar.transform, false);

            // 발이 캡슐 바닥에 닿도록 크기/위치를 맞춘다(예전처럼 -1을 박아 두면 모델에 따라 바닥에 묻힌다).
            GoshiModel.PlaceOnFeet(model);

            Animator animator = model.GetComponent<Animator>();
            if (animator == null)
                animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.avatar = avatar;
            animator.applyRootMotion = false;
            // 원격 아바타는 화면 밖에서도 상태가 갱신돼야 다시 보일 때 포즈가 튀지 않는다.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

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

    private static string MatchName(AnimationClip[] clips, params string[] keys)
    {
        foreach (string key in keys)
        {
            AnimationClip match = clips.FirstOrDefault(c => c.name.ToLowerInvariant().Contains(key));
            if (match != null)
                return match.name;
        }
        return null;
    }

    private static AnimationClip Find(AnimationClip[] clips, string name)
        => name == null ? null : clips.FirstOrDefault(c => c.name == name);

    private static string Show(string name) => name ?? "(없음)";

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
