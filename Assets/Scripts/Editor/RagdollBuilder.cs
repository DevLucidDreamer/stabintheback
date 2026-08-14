#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// goshi 캐릭터의 뼈대에서 래그돌(시체) 프리팹을 자동으로 만든다.
///
/// goshi 리그는 뼈 이름이 Bone / Bone.001 ... 처럼 의미가 없어서 Unity의 Ragdoll 마법사
/// (휴머노이드 본 지정이 필요)를 쓸 수 없다. 대신 뼈에서 자식 뼈로 향하는 실제 방향/길이를
/// 재서 캡슐 콜라이더를 씌우고, 부모 뼈와 CharacterJoint로 묶는다.
/// 리그가 바뀌어도 이름을 몰라도 되므로 그대로 다시 돌리면 된다.
///
/// 메뉴: Tools > Player > Build Ragdoll Prefab
/// </summary>
public static class RagdollBuilder
{
    private const string RagdollPath = "Assets/Prefabs/GoshiRagdoll.prefab";
    private const string PlayerPrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";

    [MenuItem("Tools/Player/Build Ragdoll Prefab")]
    public static void BuildRagdoll()
    {
        string sourcePath = GoshiModel.FindModelPath();
        if (sourcePath == null)
        {
            EditorUtility.DisplayDialog("모델 없음", GoshiModel.MissingMessage, "OK");
            return;
        }

        // 재임포트가 일어나면 기존 애셋 참조가 무효화되므로 설정을 먼저 손보고 나서 읽는다.
        GoshiModel.EnsureImportSettings(sourcePath);

        var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        if (modelAsset == null)
        {
            EditorUtility.DisplayDialog("모델 없음", GoshiModel.MissingMessage, "OK");
            return;
        }

        var root = new GameObject("GoshiRagdoll");
        try
        {
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = "GoshiModel";
            model.transform.SetParent(root.transform, false);

            // 시체도 살아있는 몸과 같은 방식으로 세운다. 시체는 죽은 플레이어의 루트 위치
            // (=캡슐 중심)에 생기므로, 발을 캡슐 바닥에 맞춰야 바닥에 묻히지 않는다.
            GoshiModel.PlaceOnFeet(model);

            // 시체는 애니메이션이 아니라 물리로 움직인다.
            foreach (Animator animator in model.GetComponentsInChildren<Animator>(true))
                Object.DestroyImmediate(animator);
            foreach (PlayerAnimator pa in model.GetComponentsInChildren<PlayerAnimator>(true))
                Object.DestroyImmediate(pa);

            // 뼈를 따라 날아다니므로 화면 밖으로 판정돼 사라지지 않게 한다.
            foreach (SkinnedMeshRenderer smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                smr.updateWhenOffscreen = true;

            int bones = BuildBodies(model.transform);
            if (bones == 0)
            {
                EditorUtility.DisplayDialog("뼈 없음",
                    $"{sourcePath} 안에서 뼈대를 찾지 못했습니다.\n스킨드 메시가 있는지 확인하세요.", "OK");
                return;
            }

            root.AddComponent<RagdollCorpse>();
            SetLayerRecursive(root, 2); // Ignore Raycast — 상호작용 조준에 시체가 걸리지 않게

            PrefabUtility.SaveAsPrefabAsset(root, RagdollPath);
            Debug.Log($"[Ragdoll] {sourcePath} 기준으로 뼈 {bones}개짜리 시체 프리팹 생성 → {RagdollPath}");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        WireIntoPlayerPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    // ---------------------------------------------------------------- 뼈에 물리 붙이기

    /// <summary>스킨드 메시가 실제로 쓰는 뼈에만 Rigidbody/콜라이더/조인트를 붙인다.</summary>
    private static int BuildBodies(Transform modelRoot)
    {
        var bones = new List<Transform>();
        foreach (SkinnedMeshRenderer smr in modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.bones == null)
                continue;
            foreach (Transform bone in smr.bones)
                if (bone != null && !bones.Contains(bone))
                    bones.Add(bone);
        }

        if (bones.Count == 0)
            return 0;

        // 부모가 먼저 오도록 정렬해야 조인트를 걸 때 부모 Rigidbody가 이미 존재한다.
        bones.Sort((a, b) => Depth(a).CompareTo(Depth(b)));

        var bodies = new Dictionary<Transform, Rigidbody>();
        foreach (Transform bone in bones)
        {
            // 콜라이더 치수는 뼈의 로컬 좌표계 기준인데, 이 리그는 아마추어 스케일이 88이라
            // 뼈의 lossyScale이 40을 넘는다. 로컬 값에 최소치(0.05 등)를 걸면 실제로는
            // 반지름 2m짜리 캡슐이 생겨 서로 파고들고, 솔버가 그걸 밀어내다가 몸이 늘어난다.
            // 그래서 눈에 보이는 월드 크기로 정한 뒤 스케일로 나눠 로컬 값으로 되돌린다.
            Vector3 axis = BoneAxis(bone);
            float worldLength = bone.TransformVector(axis).magnitude;

            float scale = Mathf.Abs(bone.lossyScale.x);
            if (scale < 1e-6f)
                scale = 1f;

            float worldHeight = Mathf.Max(worldLength, 0.08f);
            float worldRadius = Mathf.Clamp(worldLength * 0.28f, 0.03f, 0.18f);

            var capsule = bone.gameObject.AddComponent<CapsuleCollider>();
            capsule.direction = DominantAxis(axis);
            capsule.height = worldHeight / scale;
            capsule.radius = worldRadius / scale;
            capsule.center = axis * 0.5f;

            var rb = bone.gameObject.AddComponent<Rigidbody>();
            rb.mass = Mathf.Clamp(worldLength * 22f, 1.5f, 14f);
            rb.linearDamping = 0.4f;      // 공중에서 팔다리가 펄럭이지 않게 조금 눌러 준다
            rb.angularDamping = 3f;       // 뼈가 팽이처럼 도는 것을 막는다 → 흐느적거리는 느낌
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // 조인트가 늘어나(뼈가 뽑혀) 보이는 주범들:
            //  - 솔버 반복이 기본값(6)이면 여러 조인트가 얽혔을 때 다 못 풀고 벌어진다.
            //  - 콜라이더가 서로 겹친 채로 시작하면 밀어내는 속도가 폭발한다.
            //  - 각속도 상한(기본 7)까지 돌면 원심력으로 사지가 뽑힌다.
            rb.solverIterations = 24;
            rb.solverVelocityIterations = 12;
            rb.maxDepenetrationVelocity = 1.5f;
            rb.maxAngularVelocity = 8f;
            // 시체는 1초만 살고 빠르게 날아갈 일도 없으니 Discrete로 충분하다.
            // (ContinuousDynamic은 조인트와 겹치면 오히려 튀는 원인이 된다)
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            bodies[bone] = rb;

            Rigidbody parent = FindParentBody(bone, bodies);
            if (parent == null)
                continue; // 골반/몸통 = 루트 바디

            var joint = bone.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = parent;
            joint.enablePreprocessing = false;

            // 벌어진 조인트를 매 프레임 제자리로 당겨 붙인다. 이게 없으면 힘을 받은 순간
            // 뼈 사이가 쭉 늘어난 채로 남는다.
            joint.enableProjection = true;
            joint.projectionDistance = 0.01f;
            joint.projectionAngle = 5f;

            joint.swingAxis = Vector3.forward;
            joint.lowTwistLimit = new SoftJointLimit { limit = -25f };
            joint.highTwistLimit = new SoftJointLimit { limit = 25f };
            joint.swing1Limit = new SoftJointLimit { limit = 45f };
            joint.swing2Limit = new SoftJointLimit { limit = 30f };
        }

        return bodies.Count;
    }

    /// <summary>
    /// 뼈가 뻗어나가는 방향과 길이(뼈의 로컬 좌표). 가장 가까운 자식 뼈를 향한다.
    /// (FBX의 *_end 더미 노드도 자식이라 말단 뼈까지 잘 잡힌다)
    /// 자식이 없으면 위쪽으로 짧게 잡는다.
    /// </summary>
    private static Vector3 BoneAxis(Transform bone)
    {
        Transform nearest = null;
        float best = 0f;
        for (int i = 0; i < bone.childCount; i++)
        {
            float d = Vector3.Distance(bone.position, bone.GetChild(i).position);
            if (d > 0.001f && (best <= 0f || d < best))
            {
                best = d;
                nearest = bone.GetChild(i);
            }
        }

        if (nearest != null)
            return bone.InverseTransformPoint(nearest.position);

        return Vector3.up * 0.2f;
    }

    private static int DominantAxis(Vector3 v)
    {
        float ax = Mathf.Abs(v.x), ay = Mathf.Abs(v.y), az = Mathf.Abs(v.z);
        if (ax >= ay && ax >= az) return 0;
        return ay >= az ? 1 : 2;
    }

    private static Rigidbody FindParentBody(Transform bone, Dictionary<Transform, Rigidbody> bodies)
    {
        for (Transform t = bone.parent; t != null; t = t.parent)
            if (bodies.TryGetValue(t, out Rigidbody rb))
                return rb;
        return null;
    }

    private static int Depth(Transform t)
    {
        int d = 0;
        for (Transform p = t.parent; p != null; p = p.parent)
            d++;
        return d;
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    // ---------------------------------------------------------------- 플레이어 프리팹 연결

    private static void WireIntoPlayerPrefab()
    {
        var ragdoll = AssetDatabase.LoadAssetAtPath<GameObject>(RagdollPath);
        if (ragdoll == null)
            return;

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            var component = root.GetComponent<PlayerRagdoll>();
            if (component == null)
                component = root.AddComponent<PlayerRagdoll>();

            var so = new SerializedObject(component);
            so.FindProperty("ragdollPrefab").objectReferenceValue = ragdoll;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Debug.Log("[Ragdoll] NetworkPlayer 프리팹에 PlayerRagdoll 연결 완료.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
#endif
