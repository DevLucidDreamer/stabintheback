#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 어느 애니메이션 클립을 어느 상태(Idle/Start/Run/Stop/Jump)에 쓸지 직접 고르는 창.
///
/// 자동 배정은 클립 이름(start / running / stop / jump)에 기대는데, 교체된 모델처럼
/// 이름이 "Armature|Action.001" 식이면 어느 게 대기고 어느 게 점프인지 알 수가 없다.
/// 그럴 때 여기서 하나씩 골라 적용한다. 창을 열면 자동 추측이 미리 채워져 있다.
///
/// 메뉴: Tools > Player > Assign Animation Clips...
/// </summary>
public class PlayerAnimationClipWindow : EditorWindow
{
    private const string None = "(없음)";

    private string modelPath;
    private string[] options;                 // None + 클립 이름들
    private PlayerAnimatorSetup.ClipSet set;

    [MenuItem("Tools/Player/Assign Animation Clips...")]
    public static void Open()
    {
        var window = GetWindow<PlayerAnimationClipWindow>(true, "캐릭터 애니메이션 클립 배정");
        window.minSize = new Vector2(420f, 260f);
        window.Reload();
    }

    private void Reload()
    {
        modelPath = GoshiModel.FindModelPath();
        if (modelPath == null)
        {
            options = null;
            return;
        }

        // 창을 열자마자 Avatar/애니메이션 임포트 설정을 고쳐 둔다. 그래야 클립 목록이 제대로 나온다.
        GoshiModel.EnsureImportSettings(modelPath);

        AnimationClip[] clips = GoshiModel.Clips(modelPath);
        options = new[] { None }.Concat(clips.Select(c => c.name)).ToArray();
        set = PlayerAnimatorSetup.Guess(clips);
    }

    private void OnGUI()
    {
        if (options == null)
        {
            // 아직 안 읽었거나(에디터 재컴파일 직후) 모델을 못 찾은 상태.
            EditorGUILayout.HelpBox(GoshiModel.MissingMessage, MessageType.Info);
            if (GUILayout.Button("모델 찾기"))
                Reload();
            return;
        }

        EditorGUILayout.LabelField("모델", modelPath);
        EditorGUILayout.LabelField("클립", (options.Length - 1) + "개");
        EditorGUILayout.Space();

        if (options.Length <= 1)
        {
            EditorGUILayout.HelpBox(
                "모델 안에서 애니메이션 클립을 찾지 못했습니다.\n" +
                "모델 import 설정의 Animation 탭에서 Import Animation이 켜져 있는지 확인하세요.",
                MessageType.Warning);
            if (GUILayout.Button("다시 읽기"))
                Reload();
            return;
        }

        set.Idle = Popup("Idle (대기, 반복)", set.Idle);
        set.Start = Popup("Start (출발)", set.Start);
        set.Run = Popup("Run (달리기, 반복)", set.Run);
        set.Stop = Popup("Stop (정지)", set.Stop);
        set.Jump = Popup("Jump (점프)", set.Jump);

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "비워 둔(없음) 상태는 만들지 않고 건너뜁니다.\n" +
            "적용하면 GoshiAnimator.controller를 다시 만들고 NetworkPlayer 프리팹의 RemoteAvatar를 교체합니다.",
            MessageType.Info);

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("자동 추측으로 되돌리기"))
                Reload();

            using (new EditorGUI.DisabledScope(set.Run == null && set.Idle == null))
            {
                if (GUILayout.Button("적용", GUILayout.Height(24f)))
                {
                    PlayerAnimatorSetup.Apply(modelPath, set);
                    Reload();   // 재임포트 뒤 클립 목록을 다시 읽는다
                }
            }
        }
    }

    private string Popup(string label, string current)
    {
        int index = Mathf.Max(0, System.Array.IndexOf(options, current ?? None));
        index = EditorGUILayout.Popup(label, index, options);
        return index == 0 ? null : options[index];
    }
}
#endif
