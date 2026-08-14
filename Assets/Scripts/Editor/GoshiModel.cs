#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 플레이어 캐릭터(goshi) 모델을 어디서 가져올지, 어떻게 세워 둘지 한 곳에서 정한다.
///
/// 모델 파일 이름은 자주 바뀌므로(goshi(final).fbx → goshi(final!).fbx 처럼) 경로를 박아 두지 않고
/// Assets/Player 폴더에서 모델 파일을 직접 찾는다. 이름이 바뀌어도 셋업 도구가 계속 돈다.
/// .blend 원본이 임포트돼 있으면(=이 PC에 Blender가 설치돼 있으면) 그것을, 아니면 .fbx를 쓴다.
/// </summary>
public static class GoshiModel
{
    public const string ModelFolder = "Assets/Player";
    public const string PlayerPrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";

    /// <summary>
    /// 발 높이 미세 조정(m). 발바닥은 기본적으로 캡슐 바닥에 딱 맞춘다.
    /// 떠 보이면 음수로, 묻혀 보이면 양수로 조금씩 준다.
    /// </summary>
    private const float FootOffset = -0.08f;

    public const string MissingMessage =
        "goshi 모델을 불러오지 못했습니다.\n\n" +
        "Assets/Player 폴더에 캐릭터 .fbx(또는 임포트된 .blend)가 있는지 확인하세요.\n" +
        ".blend 원본을 그대로 쓰려면 이 PC에 Blender를 설치해야 합니다.";

    // ---------------------------------------------------------------- 모델 찾기

    /// <summary>
    /// Assets/Player 안의 모델 파일 중 하나를 고른다.
    /// 이름에 goshi가 들어간 것 → .blend → 최근에 저장된 것 순으로 우선한다.
    /// (모델을 새로 export 해서 넣으면 자동으로 그쪽이 잡힌다)
    /// </summary>
    public static string FindModelPath()
    {
        string folder = Path.Combine(Directory.GetCurrentDirectory(), ModelFolder);
        if (!Directory.Exists(folder))
            return null;

        var candidates = new List<string>();
        foreach (string file in Directory.GetFiles(folder))
        {
            string ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext != ".fbx" && ext != ".blend")
                continue;

            string path = ModelFolder + "/" + Path.GetFileName(file);
            // .blend는 Blender가 없으면 임포트 자체가 안 되므로 실제로 읽히는지 확인한다.
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                continue;

            candidates.Add(path);
        }

        return candidates
            .OrderByDescending(p => Path.GetFileName(p).ToLowerInvariant().Contains("goshi"))
            .ThenByDescending(p => Path.GetExtension(p).ToLowerInvariant() == ".blend")
            .ThenByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    // ---------------------------------------------------------------- 임포트 설정

    /// <summary>
    /// 애니메이션이 재생되는 데 필요한 임포트 설정을 강제한다.
    ///
    /// 교체된 모델은 Avatar 없음(No Avatar)으로 임포트돼 있었다. Avatar가 없으면 Animator에
    /// 컨트롤러를 붙여도 클립이 하나도 재생되지 않는다 — 애니메이션이 안 먹던 주 원인이다.
    /// 또 뼈를 이름으로 찾아 래그돌을 만들고 포즈를 복사하므로 계층 최적화도 꺼 둬야 한다.
    /// </summary>
    /// <returns>설정을 바꿔서 재임포트했으면 true (= 기존 클립 참조가 무효가 됨)</returns>
    public static bool EnsureImportSettings(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
            return false;

        bool changed = false;

        if (importer.animationType != ModelImporterAnimationType.Generic)
        {
            importer.animationType = ModelImporterAnimationType.Generic;
            changed = true;
        }

        if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
        {
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            changed = true;
        }

        if (!importer.importAnimation)
        {
            importer.importAnimation = true;
            changed = true;
        }

        if (importer.optimizeGameObjects)
        {
            importer.optimizeGameObjects = false;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
            Debug.Log($"[GoshiModel] {path} 임포트 설정을 고쳤습니다 (Generic + Avatar 생성 + 애니메이션 임포트).");
        }

        return changed;
    }

    /// <summary>
    /// 지정한 클립들의 Loop Time을 켠다. 모델 안의 클립은 읽기 전용 서브에셋이라
    /// ModelImporter의 clipAnimations를 통해서만 바꿀 수 있다.
    /// 한 번에 처리해야 재임포트가 한 번만 일어난다(재임포트하면 클립 참조가 전부 무효가 된다).
    /// </summary>
    /// <returns>재임포트가 일어났으면 true</returns>
    public static bool SetLooping(string path, IEnumerable<string> clipNames)
    {
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
            return false;

        var wanted = new HashSet<string>(clipNames.Where(n => !string.IsNullOrEmpty(n)));
        if (wanted.Count == 0)
            return false;

        ModelImporterClipAnimation[] defs = importer.clipAnimations;
        if (defs == null || defs.Length == 0)
            defs = importer.defaultClipAnimations;
        if (defs == null || defs.Length == 0)
            return false;

        bool changed = false;
        foreach (ModelImporterClipAnimation def in defs)
        {
            bool loop = wanted.Contains(def.name);
            if (def.loopTime != loop)
            {
                def.loopTime = loop;
                changed = true;
            }
        }

        if (!changed)
            return false;

        importer.clipAnimations = defs;
        importer.SaveAndReimport();
        return true;
    }

    /// <summary>모델 파일 안의 애니메이션 클립(미리보기용 클립은 제외).</summary>
    public static AnimationClip[] Clips(string path)
        => AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__"))
            .ToArray();

    public static Avatar FindAvatar(string path)
        => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();

    // ---------------------------------------------------------------- 캡슐에 맞춰 세우기

    /// <summary>
    /// 플레이어 캡슐 정보를 NetworkPlayer 프리팹의 CharacterController에서 읽는다.
    /// (값을 스크립트에 박아 두면 프리팹에서 캡슐을 손질했을 때 다시 어긋난다)
    /// </summary>
    /// <param name="height">캡슐 높이 = 캐릭터가 가져야 할 키</param>
    /// <param name="feetY">루트 기준으로 발바닥이 와야 할 y</param>
    public static void ReadCapsule(out float height, out float feetY)
    {
        height = 2f;
        float centerY = 0f;

        var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        var capsule = player != null ? player.GetComponent<CharacterController>() : null;
        if (capsule != null)
        {
            height = capsule.height;
            centerY = capsule.center.y;
        }

        // 발바닥을 캡슐 바닥에 맞춘다. 스킨 두께만큼 올려 봤더니 오히려 떠 보여서 보정은 빼고,
        // 눈으로 보고 맞출 여지만 FootOffset으로 남겨 뒀다.
        feetY = centerY - height * 0.5f + FootOffset;
    }

    /// <summary>
    /// 모델을 CharacterController 캡슐에 맞춰 세운다.
    ///
    /// 예전에는 localPosition을 -1로 그냥 박아 뒀는데, 모델마다 원점(아마추어 위치)이 달라서
    /// 새 모델에서는 몸이 바닥에 묻혀 다리가 보이지 않았다.
    /// 그래서 렌더러 바운즈로 실제 크기를 재서
    ///  - 키를 캡슐 높이에 맞추고
    ///  - 발바닥이 캡슐 바닥(+스킨 두께)에, 몸 중심이 캡슐 축에 오도록 옮긴다.
    /// </summary>
    /// <param name="model">RemoteAvatar(또는 래그돌 루트) 밑에 이미 붙여 둔 모델 인스턴스</param>
    /// <param name="fitHeight">키를 캡슐 높이에 맞춰 균일 스케일을 줄지</param>
    public static void PlaceOnFeet(GameObject model, bool fitHeight = true)
    {
        ReadCapsule(out float capsuleHeight, out float feetY);

        Transform t = model.transform;
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;

        if (!TryMeasure(model, out Bounds bounds))
        {
            t.localPosition = new Vector3(0f, feetY, 0f);
            Debug.LogWarning("[GoshiModel] 모델 크기를 재지 못해 기본 오프셋을 씁니다.");
            return;
        }

        float rawHeight = bounds.size.y;

        if (fitHeight && rawHeight > 0.01f)
        {
            float fit = capsuleHeight / rawHeight;
            t.localScale *= fit;
            // localPosition이 0이라 스케일 중심이 부모 원점이다 → 바운즈도 그대로 배율만 곱하면 된다.
            bounds = new Bounds(bounds.center * fit, bounds.size * fit);
        }

        // 발바닥(바운즈 최하단)을 캡슐 바닥으로, 좌우/앞뒤는 캡슐 축에 맞춘다.
        t.localPosition = new Vector3(
            -bounds.center.x,
            feetY - (bounds.center.y - bounds.size.y * 0.5f),
            -bounds.center.z);

        // 실제로 맞았는지 다시 재서 남긴다. 여전히 묻혀 보이면 이 숫자를 먼저 확인하면 된다.
        string check = TryMeasure(model, out Bounds after)
            ? $"실측 발바닥 y={after.center.y - after.size.y * 0.5f:F3}, 머리 y={after.center.y + after.size.y * 0.5f:F3}"
            : "실측 실패";

        Debug.Log($"[GoshiModel] 모델 키 {rawHeight:F2} → 캡슐 {capsuleHeight:F2}m 에 맞춤 " +
                  $"(스케일 {t.localScale.y:F3}, 목표 발바닥 y={feetY:F3}). {check}");
    }

    /// <summary>모델의 렌더러 바운즈를 부모(=RemoteAvatar) 로컬 좌표로 재 온다.</summary>
    private static bool TryMeasure(GameObject model, out Bounds bounds)
    {
        Transform parent = model.transform.parent;
        Bounds acc = new Bounds();
        bool has = false;

        void Add(Vector3 point)
        {
            Vector3 local = parent != null ? parent.InverseTransformPoint(point) : point;
            if (!has)
            {
                acc = new Bounds(local, Vector3.zero);
                has = true;
            }
            else
            {
                acc.Encapsulate(local);
            }
        }

        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
        {
            // 뼈가 크게 움직이면 화면 밖 판정으로 바운즈가 굳어 버리므로 항상 갱신하게 둔다.
            if (r is SkinnedMeshRenderer smr)
                smr.updateWhenOffscreen = true;

            Bounds wb = r.bounds;
            if (wb.size == Vector3.zero)
                continue;

            // 부모가 회전/스케일돼 있을 수 있으니 꼭짓점 8개를 다 넣는다.
            for (int i = 0; i < 8; i++)
            {
                Add(new Vector3(
                    (i & 1) == 0 ? wb.min.x : wb.max.x,
                    (i & 2) == 0 ? wb.min.y : wb.max.y,
                    (i & 4) == 0 ? wb.min.z : wb.max.z));
            }
        }

        // 렌더러 바운즈가 아직 계산되지 않은 경우(프리팹 편집용 씬 등)의 보험: 뼈 위치로 대충 잰다.
        if (!has)
        {
            foreach (SkinnedMeshRenderer smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.bones == null)
                    continue;
                foreach (Transform bone in smr.bones)
                {
                    if (bone != null)
                        Add(bone.position);
                }
            }
        }

        bounds = acc;
        return has;
    }
}
#endif
