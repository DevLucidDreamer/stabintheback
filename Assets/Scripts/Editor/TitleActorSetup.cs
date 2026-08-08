#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 타이틀 배경에서 뛰어다닐 캐릭터를 준비한다.
///
/// Assets/Player 안의 색깔별 goshi 모델을 전부 찾아, 모델마다 "자기 파일 안의 클립"으로
/// 도는 전용 컨트롤러(Assets/Player/Title/Roam_*.controller)를 굽는다.
/// 게임용 GoshiAnimator를 그대로 돌려쓰지 않는 이유는, 그 컨트롤러가 goshi(final!) 의
/// 클립만 참조하기 때문이다 — 색깔 모델에 물리면 리타게팅에 기대게 되고,
/// 뼈 이름이 조금만 달라져도 조용히 아무 동작도 안 나온다.
///
/// 상태는 Idle ↔ (Start) → Run → (Stop) 로 게임용과 같은 흐름이되,
/// 점프가 없어 훨씬 단순하다. 파라미터는 IsMoving(bool) / Speed(float)로 TitleActor가 넣는다.
/// </summary>
public static class TitleActorSetup
{
    public const string ControllerFolder = "Assets/Player/Title";

    /// <summary>게임용 셋업과 같은 배속. 원본 클립이 전체적으로 느리다.</summary>
    private const float PlaybackSpeed = 2.5f;

    /// <summary>준비된 캐릭터 하나 — 모델 프리팹, 아바타, 전용 컨트롤러.</summary>
    public struct Actor
    {
        public string ModelPath;
        public GameObject Model;
        public Avatar Avatar;
        public RuntimeAnimatorController Controller;
    }

    [MenuItem("Tools/Title/Rebuild Title Character Animators")]
    public static void RebuildFromMenu()
    {
        List<Actor> actors = PrepareAll();
        AssetDatabase.SaveAssets();

        Debug.Log(actors.Count == 0
            ? "[TitleActor] Assets/Player 에서 캐릭터 모델을 찾지 못했습니다."
            : $"[TitleActor] {actors.Count}종 준비 완료: " +
              string.Join(", ", actors.Select(a => Path.GetFileName(a.ModelPath))));
    }

    /// <summary>
    /// Assets/Player 의 모델을 전부 준비한다.
    /// 임포트 설정(Avatar 생성 · 애니메이션 임포트)까지 강제하므로,
    /// 새로 넣은 색깔 모델처럼 Avatar 없이 들어온 파일도 여기서 고쳐진다.
    /// </summary>
    public static List<Actor> PrepareAll()
    {
        EnsureFolder();

        var actors = new List<Actor>();
        foreach (string path in FindModels())
        {
            Actor actor = Prepare(path);
            if (actor.Model != null && actor.Controller != null)
                actors.Add(actor);
        }
        return actors;
    }

    /// <summary>
    /// Assets/Player 안의 캐릭터 모델 경로들.
    /// 원본(goshi(final!))을 앞에 두고 나머지는 이름순 — 배치 순서를 매번 같게 하려는 것.
    /// </summary>
    internal static string[] FindModels()
    {
        string folder = Path.Combine(Directory.GetCurrentDirectory(), GoshiModel.ModelFolder);
        if (!Directory.Exists(folder))
            return new string[0];

        return Directory.GetFiles(folder)
            .Where(f =>
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                return ext == ".fbx" || ext == ".blend";
            })
            .Select(f => GoshiModel.ModelFolder + "/" + Path.GetFileName(f))
            .Where(p => AssetDatabase.LoadAssetAtPath<GameObject>(p) != null)
            .OrderByDescending(p => Path.GetFileName(p).ToLowerInvariant().Contains("final"))
            .ThenBy(p => Path.GetFileName(p))
            .ToArray();
    }

    private static Actor Prepare(string modelPath)
    {
        var actor = new Actor { ModelPath = modelPath };

        // Avatar 없이 임포트된 모델은 컨트롤러를 붙여도 클립이 하나도 재생되지 않는다.
        GoshiModel.EnsureImportSettings(modelPath);

        AnimationClip[] clips = GoshiModel.Clips(modelPath);
        if (clips.Length == 0)
        {
            Debug.LogWarning($"[TitleActor] {modelPath} 안에 애니메이션 클립이 없습니다. 이 모델은 서 있기만 합니다.");
            return actor;
        }

        PlayerAnimatorSetup.ClipSet set = PlayerAnimatorSetup.Guess(clips);

        // 루프 설정은 재임포트를 일으켜 클립 참조를 무효화한다. 반드시 먼저 끝내고 다시 읽는다.
        GoshiModel.SetLooping(modelPath, set.Looping);
        clips = GoshiModel.Clips(modelPath);

        actor.Model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        actor.Avatar = GoshiModel.FindAvatar(modelPath);
        actor.Controller = BuildRoamController(ControllerPath(modelPath), clips, set);

        if (actor.Avatar == null)
            Debug.LogWarning($"[TitleActor] {modelPath} 에 Avatar가 없습니다. 애니메이션이 재생되지 않을 수 있습니다.");

        return actor;
    }

    private static string ControllerPath(string modelPath)
    {
        var safe = new string(Path.GetFileNameWithoutExtension(modelPath)
            .Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        return $"{ControllerFolder}/Roam_{safe}.controller";
    }

    /// <summary>Idle ↔ Start → Run → Stop 만 도는 가벼운 컨트롤러.</summary>
    private static AnimatorController BuildRoamController(string path, AnimationClip[] clips, PlayerAnimatorSetup.ClipSet set)
    {
        AnimationClip idleClip = Find(clips, set.Idle);
        AnimationClip startClip = Find(clips, set.Start);
        AnimationClip runClip = Find(clips, set.Run);
        AnimationClip stopClip = Find(clips, set.Stop);

        // 달리기 클립을 못 찾으면 남는 클립 아무거나 쓴다 — 서 있기만 하는 것보단 낫다.
        if (runClip == null)
            runClip = clips.FirstOrDefault(c => c != idleClip && c != startClip && c != stopClip) ?? clips[0];

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        AnimatorState idle = sm.AddState("Idle");
        AnimatorState run = sm.AddState("Run");
        AnimatorState start = startClip != null ? sm.AddState("Start") : null;
        AnimatorState stop = stopClip != null ? sm.AddState("Stop") : null;

        // Idle 클립이 없으면 비워 둔다 — 마지막 자세를 그대로 유지하는 쪽이 자연스럽다.
        idle.motion = idleClip;
        run.motion = runClip;
        if (start != null) start.motion = startClip;
        if (stop != null) stop.motion = stopClip;

        sm.defaultState = idle;

        AnimatorState toRun = start ?? run;
        AnimatorState toIdle = stop ?? idle;

        AddTransition(idle, toRun, false, 0f, 0.05f, ("IsMoving", AnimatorConditionMode.If));

        if (start != null)
        {
            AddTransition(start, run, true, 0.85f, 0.1f);
            AddTransition(start, toIdle, false, 0f, 0.1f, ("IsMoving", AnimatorConditionMode.IfNot));
        }

        AddTransition(run, toIdle, false, 0f, 0.1f, ("IsMoving", AnimatorConditionMode.IfNot));

        if (stop != null)
        {
            AddTransition(stop, idle, true, 0.85f, 0.1f);
            AddTransition(stop, toRun, false, 0f, 0.1f, ("IsMoving", AnimatorConditionMode.If));
        }

        foreach (ChildAnimatorState child in sm.states)
            child.state.speed = PlaybackSpeed;

        if (stop != null)
            stop.speed = PlaybackSpeed * 2f;

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void AddTransition(AnimatorState from, AnimatorState to,
        bool hasExit, float exitTime, float duration,
        params (string param, AnimatorConditionMode mode)[] conditions)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = hasExit;
        if (hasExit)
            t.exitTime = exitTime;
        t.hasFixedDuration = true;
        t.duration = duration;
        foreach (var c in conditions)
            t.AddCondition(c.mode, 0f, c.param);
    }

    private static AnimationClip Find(AnimationClip[] clips, string name)
        => name == null ? null : clips.FirstOrDefault(c => c.name == name);

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(ControllerFolder))
            AssetDatabase.CreateFolder(GoshiModel.ModelFolder, "Title");
    }
}
#endif
