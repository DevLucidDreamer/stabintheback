#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 본편 무대 "캠핑장"을 생성하는 에디터 도구.
/// 새 씬을 만들고 캠핑장 환경 + 스폰포인트 + 준비물 + 음식 무기 + 매니저 + 탈출존까지 구성한다.
/// 대기실에서 정원이 다 차면 이 씬으로 넘어온다.
/// 메뉴: Tools > Stage > Build Stage 2 (Campground)
///
/// 무기(소세지 꼬치·국자·냉동참치 등)는 Assets/Prefabs/Weapons 의 프리팹을 가져다 놓으므로,
/// 'Tools > Weapons > Build Weapon Prefabs'를 먼저 실행해야 한다.
///
/// NetworkManager(NetworkBootstrap)는 Mirror에서 씬 전환 시 유지되므로 이 씬엔 넣지 않는다.
/// </summary>
public static class Stage2Builder
{
    private const string ScenePath = "Assets/Scenes/Stage2_Campground.unity";

    /// <summary>준비물을 다 챙겨 탈출하면 돌아갈 씬. 대기실에서 다시 인원을 모은다.</summary>
    private const string LobbyScene = "Lobby";

    private const string ConeMeshPath = "Assets/Models/Generated/Camp_Cone.asset";
    private static Mesh coneMesh;

    /// <summary>캠핑 프롭 프리팹을 모아 두는 곳. 씬에는 이 프리팹의 인스턴스만 놓인다.</summary>
    private const string PropFolder = "Assets/Prefabs/Camp";
    private const string TentModelPath = "Assets/Models/TENT.blend";
    private const string ChairModelPath = "Assets/Models/chair.blend";

    private static readonly Dictionary<string, GameObject> props = new Dictionary<string, GameObject>();

    private static Material matGrass, matGrassDark, matGrassLight, matDirt;
    private static Material matWood, matWoodDark, matStone, matMetal, matDark;
    private static Material matTent, matTentDark, matFlame, matEmber;
    private static Material matLeaf, matLeafDark, matLeafLight;
    private static Material matAccent, matWater, matRope;
    private static Material matMeat, matFat, matCorn, matHusk, matGrill, matCoal;

    /// <summary>
    /// 프롭 프리팹만 다시 굽는다. 씬은 건드리지 않는다.
    ///
    /// 맵을 손으로 배치해 둔 뒤 텐트 모양 같은 걸 고치고 싶을 때 쓴다 —
    /// 씬에 놓인 것은 전부 프리팹 인스턴스라, 프리팹만 다시 구우면
    /// 배치를 그대로 둔 채 모양만 갱신된다.
    /// </summary>
    [MenuItem("Tools/Stage/Rebuild Camp Props (씬 유지)")]
    public static void RebuildProps()
    {
        LoadMaterials();
        props.Clear();
        Random.InitState(2026);

        BakeAllProps();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Stage2] 프롭 프리팹 {props.Count}종을 다시 구웠습니다: {PropFolder}\n" +
                  "씬에 놓인 인스턴스는 그대로 두고 모양만 갱신됩니다.");
    }

    [MenuItem("Tools/Stage/Build Stage 2 (Campground)")]
    public static void BuildStage2()
    {
        if (System.IO.File.Exists(ScenePath) && !Application.isBatchMode &&
            !EditorUtility.DisplayDialog("캠핑장 다시 만들기",
                $"{ScenePath} 를 처음부터 새로 만듭니다.\n" +
                "씬에서 손으로 옮기거나 추가해 둔 것은 전부 사라집니다.\n\n" +
                "모양만 고치고 배치를 지키고 싶다면 취소하고\n" +
                "'Tools > Stage > Rebuild Camp Props (씬 유지)'를 쓰세요.",
                "새로 만들기", "취소"))
        {
            return;
        }

        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        LoadMaterials();
        props.Clear();

        AddSun();

        // 배치가 매번 같도록 시드를 고정한다. 모든 클라이언트가 같은 씬 파일을 쓰므로
        // 실제로는 씬에 구워지지만, 다시 빌드해도 맵이 뒤바뀌지 않게 하려는 목적.
        Random.InitState(2026);

        BakeAllProps();

        var root = new GameObject("Campground").transform;

        Terrain(root);
        Lake(root, new Vector3(34f, 0f, 24f), 13f);

        // 캠프의 심장 — 불을 피울 화로와 그 옆의 바베큐 그릴.
        // 프리팹으로 굽지 않고 씬에 직접 짓는다. 스크립트가 자기 부품(장작·불꽃·불빛)을
        // 참조로 들고 있어야 하는데, 프리팹 인스턴스에 그 배선을 심는 것보다 이쪽이 확실하다.
        BuildFirepit(root, new Vector3(0f, 0f, 0f));
        BuildGrill(root, new Vector3(3.4f, 0f, 0.9f), -28f);

        Place("Tripod", root, new Vector3(-1.9f, 0f, -1.6f));
        Place("LogSeats", root, new Vector3(0f, 0f, 0f));

        Place("Tent_Blue", root, new Vector3(-10f, 0f, -5f), 25f);
        Place("Tent_Red", root, new Vector3(10f, 0f, -5.5f), -25f);
        Place("Tent_Blue", root, new Vector3(-3f, 0f, 13f), 190f);

        Place("PicnicTable", root, new Vector3(7f, 0f, 4f));
        Place("Cooler", root, new Vector3(9.2f, 0f, 4.5f));
        Place("CampChair", root, new Vector3(-3.6f, 0f, 3.4f), 130f);
        Place("CampChair", root, new Vector3(3.4f, 0f, -3.2f), -30f);
        Clothesline(root, new Vector3(-6.5f, 0f, 3.5f), new Vector3(-1.5f, 0f, 6.5f));
        Place("WoodPile", root, new Vector3(-5.2f, 0f, -1.2f), 20f);
        Place("Signpost", root, new Vector3(-3.4f, 0f, -12.5f), 8f);

        BuildDistricts(root);

        Forest(root);
        Scatter(root);

        // 지형/숲/잡목은 오브젝트 수가 많다. 움직이지 않으므로 static으로 묶어
        // 배칭과 라이트맵 대상이 되게 한다. (무기·준비물은 움직이므로 제외)
        MarkStatic(root, "Terrain", "Lake", "Forest", "Scatter", "Districts");

        // 재료 — 캠핑장 구석구석에 흩어 놓는다.
        PlaceIngredients(root);

        // 안내 팻말 (목표를 말로 한 번 더 알려 준다)
        GoalSigns(root);

        // 무기 — .blend에서 가져온 음식 무기들을 캠핑장 곳곳에 흩뿌린다.
        PlaceWeapons(root);

        // 스폰 포인트
        SpawnPoints(new Vector3(0f, 1f, -54f));

        // 진행 매니저(재료·페이즈·굽기) + 무기 동기화 + 하늘 연출
        CampGameManager game = NetworkPhase3Setup.EnsureManager();
        AssignIngredientIds();
        NetworkPhase4Setup.SetupWeaponSync();
        AddDayNight();

        // 탈출존은 두지 않는다 — 바베큐를 다 구우면 CampGameManager가 대기실로 돌려보낸다.
        if (game == null)
            Debug.LogError("[Stage2] CampGameManager를 만들지 못했습니다.");

        EditorSceneManager.SaveScene(scene, ScenePath);
        NetworkPhase6Setup.EnsureSceneInBuildSettings("Assets/Scenes/Lobby.unity");
        NetworkPhase6Setup.EnsureSceneInBuildSettings(ScenePath);

        Debug.Log("[Stage2] 캠핑장 생성 완료: " + ScenePath +
                  "\n목표: 장작·고기·야채를 모두 모으면 해가 지고, 화로에 불을 피워 바베큐를 굽는다." +
                  "\n- 캠핑 프롭은 " + PropFolder + " 의 프리팹 인스턴스입니다. 씬에서 마음대로 옮기고 복제하세요." +
                  "\n- 배치를 지킨 채 모양만 고치려면 'Tools > Stage > Rebuild Camp Props (씬 유지)'." +
                  "\n- 재료를 손으로 더 놓았다면 'Tools > Multiplayer/Phase 3 > Setup Camp Game Sync'로 ID를 다시 매기세요." +
                  "\n- 무기가 안 보이면 'Tools > Weapons > Build Weapon Prefabs'를 먼저 실행하고 다시 만드세요.");
    }

    public static void BuildStage2Batch()
    {
        BuildStage2();
        EditorApplication.Exit(0);
    }

    // ---------------------------------------------------------------- 프롭 프리팹

    /// <summary>
    /// 캠핑 프롭을 전부 프리팹으로 굽는다.
    ///
    /// 예전에는 씬에 직접 그려 넣어서, 맵을 손보려면 씬을 다시 만들어야 했고
    /// 그때마다 손으로 옮겨 둔 것이 날아갔다. 프리팹으로 두면
    ///  · 씬에서는 위치·회전·크기만 바꾸면 되고(자유 배치)
    ///  · 모양은 프리팹 하나만 고치면 모든 인스턴스에 반영된다.
    /// </summary>
    private static void BakeAllProps()
    {
        Prop("Tripod", t => Tripod(t, Vector3.zero));
        Prop("LogSeats", t => LogSeats(t, Vector3.zero, 2.4f));
        Prop("Tent_Blue", t => ImportedModelProp(t, "Tent", TentModelPath, 4.8f, () => Tent(t, Vector3.zero, 0f, matTent)));
        Prop("Tent_Red", t => ImportedModelProp(t, "Tent", TentModelPath, 4.8f, () => Tent(t, Vector3.zero, 0f, matAccent)));
        Prop("PicnicTable", t => PicnicTable(t, Vector3.zero));
        Prop("Cooler", t => Cooler(t, Vector3.zero));
        Prop("CampChair", t => ImportedModelProp(t, "CampChair", ChairModelPath, 1.2f, () => CampChair(t, Vector3.zero, 0f)));
        Prop("WoodPile", t => WoodPile(t, Vector3.zero, 0f));
        Prop("Signpost", t => Signpost(t, Vector3.zero, 0f));
        Prop("Tree_Pine", t => PineTree(t, Vector3.zero, 1f));
        Prop("Tree_Broad", t => BroadTree(t, Vector3.zero, 1f));
        Prop("Rock", t => RockModel(t));
        Prop("Bush", t => BushModel(t));
        Prop("Stump", t => StumpModel(t));
    }

    /// <summary>
    /// 프롭 하나를 원점에 만들어 프리팹으로 굽는다.
    /// 프롭 함수들은 자기 그룹을 자식으로 하나 만들므로, 그 자식을 프리팹 루트로 쓴다.
    /// </summary>
    private static GameObject Prop(string name, System.Action<Transform> build)
    {
        if (props.TryGetValue(name, out GameObject cached) && cached != null)
            return cached;

        if (!AssetDatabase.IsValidFolder(PropFolder))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            AssetDatabase.CreateFolder("Assets/Prefabs", "Camp");
        }

        var temp = new GameObject("__bake");
        GameObject asset = null;
        try
        {
            build(temp.transform);
            if (temp.transform.childCount == 0)
            {
                Debug.LogError($"[Stage2] 프롭 '{name}' 이 아무것도 만들지 않았습니다.");
                return null;
            }

            Transform prop = temp.transform.GetChild(0);
            prop.SetParent(null, false);
            prop.localPosition = Vector3.zero;
            prop.localRotation = Quaternion.identity;
            prop.name = name;

            asset = PrefabUtility.SaveAsPrefabAsset(prop.gameObject, $"{PropFolder}/{name}.prefab");
            Object.DestroyImmediate(prop.gameObject);
        }
        finally
        {
            Object.DestroyImmediate(temp);
        }

        props[name] = asset;
        return asset;
    }

    /// <summary>구워 둔 프롭을 씬에 놓는다. 돌려주는 인스턴스는 마음대로 더 손봐도 된다.</summary>
    private static GameObject Place(string name, Transform parent, Vector3 pos, float rotY = 0f, float scale = 1f)
    {
        if (!props.TryGetValue(name, out GameObject prefab) || prefab == null)
        {
            Debug.LogError($"[Stage2] 프롭 프리팹 '{name}' 을 찾지 못했습니다.");
            return null;
        }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
        go.transform.localScale = Vector3.one * scale;
        return go;
    }

    // ---------------------------------------------------------------- 지형

    /// <summary>
    /// 바닥 + 길 + 잔디 결. 넓은 평면 하나만 깔면 허전해서,
    /// 색이 조금씩 다른 잔디 패치와 흙길을 겹쳐 결을 만든다.
    /// 벽 대신 바깥쪽에 둔덕을 둘러 맵 밖으로 못 나가게 한다.
    /// </summary>
    private static void Terrain(Transform p)
    {
        Transform g = Group(p, "Terrain", Vector3.zero, 0f);

        Box(g, "Ground", new Vector3(0f, -0.5f, 0f), new Vector3(140f, 1f, 140f), matGrass);

        // 잔디 색 패치 — 단색 평면의 밋밋함을 깨는 용도.
        for (int i = 0; i < 72; i++)
        {
            var pos = new Vector3(Random.Range(-63f, 63f), 0.01f, Random.Range(-63f, 63f));
            float s = Random.Range(5f, 14f);
            Prim(g, "GrassPatch", PrimitiveType.Cylinder, pos, new Vector3(0f, Random.value * 360f, 0f),
                new Vector3(s, 0.005f, s * Random.Range(0.7f, 1.3f)), i % 2 == 0 ? matGrassDark : matGrassLight);
        }

        // 캠프로 들어오는 흙길 (스폰 지점 → 모닥불 → 출구)
        Box(g, "MainTrail", new Vector3(0f, 0.02f, -7f), new Vector3(4.2f, 0.04f, 102f), matDirt);
        Box(g, "LakeTrail", new Vector3(18f, 0.021f, 18f), new Vector3(37f, 0.04f, 3.2f), matDirt);
        Box(g, "RangerTrail", new Vector3(-22f, 0.022f, 22f), new Vector3(45f, 0.04f, 3.2f), matDirt);
        Box(g, "SouthLoop", new Vector3(-18f, 0.023f, -31f), new Vector3(38f, 0.04f, 2.8f), matDirt);
        Prim(g, "FirePad", PrimitiveType.Cylinder, new Vector3(0f, 0.025f, 0f), new Vector3(6.4f, 0.02f, 6.4f), matDirt);

        // 바깥 둔덕 — 낮은 언덕처럼 보이면서 실제로는 벽 역할을 한다.
        for (int i = 0; i < 72; i++)
        {
            float a = i / 72f * Mathf.PI * 2f;
            float r = 66f;
            var pos = new Vector3(Mathf.Cos(a) * r, Random.Range(1.2f, 2.2f), Mathf.Sin(a) * r);
            Prim(g, "Berm", PrimitiveType.Cube, pos, new Vector3(0f, a * Mathf.Rad2Deg, Random.Range(-8f, 8f)),
                new Vector3(7f, Random.Range(3f, 5f), 7f), matGrassDark);
        }
    }

    /// <summary>낚시터가 될 얕은 호수. 물은 살짝 투명한 판으로 대신한다.</summary>
    private static void Lake(Transform p, Vector3 pos, float radius)
    {
        Transform g = Group(p, "Lake", pos, 0f);

        Prim(g, "Bed", PrimitiveType.Cylinder, new Vector3(0f, -0.25f, 0f),
            new Vector3(radius * 2f, 0.25f, radius * 2f), matDirt);
        GameObject water = Prim(g, "Water", PrimitiveType.Cylinder, new Vector3(0f, -0.06f, 0f),
            new Vector3(radius * 2f - 0.4f, 0.06f, radius * 2f - 0.4f), matWater);
        Object.DestroyImmediate(water.GetComponent<Collider>()); // 물 위를 걸어다니지 않도록

        // 물가 돌 테두리
        for (int i = 0; i < 18; i++)
        {
            float a = i / 18f * Mathf.PI * 2f;
            float r = radius + Random.Range(-0.2f, 0.35f);
            Prim(g, "Rock", PrimitiveType.Cube, new Vector3(Mathf.Cos(a) * r, 0.05f, Mathf.Sin(a) * r),
                new Vector3(Random.value * 60f, Random.value * 360f, Random.value * 40f),
                Vector3.one * Random.Range(0.4f, 0.9f), matStone);
        }

        // 작은 나무 잔교
        Transform d = Group(g, "Dock", new Vector3(-radius * 0.75f, 0f, -radius * 0.5f), 35f);
        Box(d, "Planks", new Vector3(0f, 0.22f, 0f), new Vector3(1.4f, 0.1f, 4.5f), matWood);
        Box(d, "PostA", new Vector3(-0.55f, 0f, 1.9f), new Vector3(0.14f, 0.6f, 0.14f), matWoodDark);
        Box(d, "PostB", new Vector3(0.55f, 0f, 1.9f), new Vector3(0.14f, 0.6f, 0.14f), matWoodDark);
        Box(d, "PostC", new Vector3(-0.55f, 0f, -1.9f), new Vector3(0.14f, 0.6f, 0.14f), matWoodDark);
        Box(d, "PostD", new Vector3(0.55f, 0f, -1.9f), new Vector3(0.14f, 0.6f, 0.14f), matWoodDark);
    }

    // ---------------------------------------------------------------- 캠프 프롭

    /// <summary>
    /// 캠프 한가운데의 화로. 저녁이 되면 여기에 장작을 넣어 불을 피운다.
    ///
    /// 장작과 불꽃은 만들어만 두고 <see cref="Firepit"/>이 상황에 따라 켜고 끈다 —
    /// 처음에는 빈 돌 화덕이고, 장작을 넣을수록 하나씩 쌓이고, 다 채우면 타오른다.
    /// </summary>
    private static void BuildFirepit(Transform p, Vector3 pos)
    {
        Transform g = Group(p, "Firepit", pos, 0f);

        // 돌 화덕 — 크기와 각도를 흩어 놓으면 훨씬 자연스럽다.
        for (int i = 0; i < 14; i++)
        {
            float a = i / 14f * Mathf.PI * 2f;
            float r = 0.95f + Random.Range(-0.08f, 0.08f);
            Prim(g, "Stone", PrimitiveType.Cube,
                new Vector3(Mathf.Cos(a) * r, 0.11f, Mathf.Sin(a) * r),
                new Vector3(Random.Range(-15f, 15f), a * Mathf.Rad2Deg + Random.Range(-20f, 20f), Random.Range(-15f, 15f)),
                new Vector3(Random.Range(0.26f, 0.4f), Random.Range(0.2f, 0.3f), Random.Range(0.24f, 0.34f)), matStone);
        }

        Prim(g, "Ash", PrimitiveType.Cylinder, new Vector3(0f, 0.03f, 0f), new Vector3(1.5f, 0.03f, 1.5f), matDark);

        // 원뿔로 세운 장작 — 위로 갈수록 가운데로 모여야 한다.
        // 기울기 부호는 LeanInward가 잡아 준다.
        const int logCount = 4;
        var logs = new GameObject[logCount];
        for (int i = 0; i < logCount; i++)
        {
            float a = i / (float)logCount * Mathf.PI * 2f;
            logs[i] = Prim(g, "Log" + i, PrimitiveType.Cylinder,
                new Vector3(Mathf.Cos(a) * 0.16f, 0.3f, Mathf.Sin(a) * 0.16f),
                LeanInward(a, 22f),
                new Vector3(0.12f, 0.42f, 0.12f), matWoodDark);
            logs[i].SetActive(false); // 넣기 전에는 비어 있다
        }

        var flames = new[]
        {
            Prim(g, "Flame", PrimitiveType.Sphere, new Vector3(0f, 0.55f, 0f), new Vector3(0.44f, 0.72f, 0.44f), matFlame),
            Prim(g, "FlameTip", PrimitiveType.Sphere, new Vector3(0f, 0.92f, 0f), new Vector3(0.22f, 0.36f, 0.22f), matEmber),
        };
        foreach (GameObject flame in flames)
        {
            Object.DestroyImmediate(flame.GetComponent<Collider>()); // 불에 걸려 넘어지지 않게
            flame.SetActive(false);
        }

        var lgo = new GameObject("FireLight");
        lgo.transform.SetParent(g, false);
        lgo.transform.localPosition = new Vector3(0f, 0.8f, 0f);
        var light = lgo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.6f, 0.25f);
        light.range = 18f;
        light.intensity = 3.6f;
        light.shadows = LightShadows.Soft;
        light.enabled = false;

        // 조준을 받아 줄 몸통. 돌 하나하나를 노리게 하면 조작이 까다롭다.
        var hit = Prim(g, "Hitbox", PrimitiveType.Cylinder, new Vector3(0f, 0.45f, 0f),
            new Vector3(2.2f, 0.45f, 2.2f), null);
        Object.DestroyImmediate(hit.GetComponent<MeshRenderer>());
        Object.DestroyImmediate(hit.GetComponent<MeshFilter>());

        var pit = g.gameObject.AddComponent<Firepit>();
        pit.SetDisplayName("화로");
        pit.Configure(logs, flames, light);
    }

    /// <summary>
    /// 바베큐 그릴. 불이 붙은 뒤부터 고기와 야채를 올릴 수 있다.
    /// 칸(SlotAnchor)의 개수가 곧 동시에 구울 수 있는 개수다.
    /// </summary>
    private static void BuildGrill(Transform p, Vector3 pos, float rotY)
    {
        Transform g = Group(p, "BarbecueGrill", pos, rotY);

        // 다리 넷
        foreach (float x in new[] { -0.52f, 0.52f })
        foreach (float z in new[] { -0.3f, 0.3f })
            Box(g, "Leg", new Vector3(x, 0.32f, z), new Vector3(0.055f, 0.64f, 0.055f), matMetal);

        // 숯을 담는 통
        Box(g, "Basin", new Vector3(0f, 0.66f, 0f), new Vector3(1.24f, 0.16f, 0.74f), matGrill);
        Box(g, "RimN", new Vector3(0f, 0.76f, -0.37f), new Vector3(1.28f, 0.06f, 0.06f), matGrill);
        Box(g, "RimS", new Vector3(0f, 0.76f, 0.37f), new Vector3(1.28f, 0.06f, 0.06f), matGrill);
        Box(g, "Handle", new Vector3(-0.68f, 0.72f, 0f), new Vector3(0.12f, 0.05f, 0.3f), matWoodDark);

        // 숯 — 불이 붙어야 보인다.
        var coals = new GameObject[6];
        for (int i = 0; i < coals.Length; i++)
        {
            var at = new Vector3((i % 3 - 1) * 0.34f, 0.72f, (i / 3 - 0.5f) * 0.28f);
            coals[i] = Prim(g, "Coal" + i, PrimitiveType.Cube, at,
                new Vector3(0f, Random.Range(0f, 60f), 0f),
                new Vector3(0.26f, 0.05f, 0.2f), matCoal);
            Object.DestroyImmediate(coals[i].GetComponent<Collider>());
            coals[i].SetActive(false);
        }

        // 석쇠 — 가는 막대 여러 개
        Transform grate = Group(g, "Grate", new Vector3(0f, 0.82f, 0f), 0f);
        for (int i = 0; i < 9; i++)
        {
            var bar = Box(grate, "Bar", new Vector3((i - 4) * 0.13f, 0f, 0f),
                new Vector3(0.025f, 0.02f, 0.66f), matMetal);
            Object.DestroyImmediate(bar.GetComponent<Collider>());
        }

        // 재료가 올라갈 자리 4칸
        const int slots = 4;
        var anchors = new Transform[slots];
        for (int i = 0; i < slots; i++)
        {
            var go = new GameObject("Slot" + i);
            go.transform.SetParent(g, false);
            go.transform.localPosition = new Vector3((i - (slots - 1) * 0.5f) * 0.29f, 0.87f, 0f);
            anchors[i] = go.transform;
        }

        // 조준을 받아 줄 몸통 하나. 석쇠 막대를 하나씩 노리게 하면 안내 문구가 깜빡인다.
        var hit = Box(g, "Hitbox", new Vector3(0f, 0.72f, 0f), new Vector3(1.35f, 0.5f, 0.85f), null);
        Object.DestroyImmediate(hit.GetComponent<MeshRenderer>());
        Object.DestroyImmediate(hit.GetComponent<MeshFilter>());

        var grill = g.gameObject.AddComponent<BarbecueGrill>();
        grill.SetDisplayName("바베큐 그릴");
        grill.Configure(anchors, coals, JalnanFontAssetBuilder.Ensure());
    }

    /// <summary>모닥불 위 삼각대와 주전자. 다리 셋이 주전자 쪽으로 모여야 한다.</summary>
    private static void Tripod(Transform p, Vector3 pos)
    {
        Transform g = Group(p, "Tripod", pos, 0f);
        for (int i = 0; i < 3; i++)
        {
            float a = i / 3f * Mathf.PI * 2f;
            Prim(g, "Leg", PrimitiveType.Cylinder,
                new Vector3(Mathf.Cos(a) * 0.55f, 0.9f, Mathf.Sin(a) * 0.55f),
                LeanInward(a, 18f),
                new Vector3(0.06f, 0.95f, 0.06f), matWoodDark);
        }
        Box(g, "Hook", new Vector3(0f, 1.55f, 0f), new Vector3(0.03f, 0.5f, 0.03f), matMetal);
        Prim(g, "Pot", PrimitiveType.Cylinder, new Vector3(0f, 1.15f, 0f), new Vector3(0.42f, 0.22f, 0.42f), matDark);
    }

    /// <summary>모닥불을 둘러싼 통나무 의자.</summary>
    private static void LogSeats(Transform p, Vector3 pos, float radius)
    {
        Transform g = Group(p, "LogSeats", pos, 0f);
        for (int i = 0; i < 4; i++)
        {
            float a = (i / 4f + 0.13f) * Mathf.PI * 2f;
            Prim(g, "SeatLog", PrimitiveType.Cylinder,
                new Vector3(Mathf.Cos(a) * radius, 0.22f, Mathf.Sin(a) * radius),
                new Vector3(90f, -a * Mathf.Rad2Deg, 0f),
                new Vector3(0.44f, 0.75f, 0.44f), matWood);
        }
    }

    /// <summary>A형 텐트. 폴대와 젖혀 놓은 입구 플랩까지 있어서 실루엣이 산다.</summary>
    private static void Tent(Transform p, Vector3 pos, float rotY, Material mat)
    {
        Transform g = Group(p, "Tent", pos, rotY);

        Box(g, "Floor", new Vector3(0f, 0.06f, 0f), new Vector3(2.3f, 0.12f, 3f), matDark);

        // 지붕 두 장은 위에서 만나야 한다(∧). Z축 +회전은 위쪽을 -X로 눕히므로,
        // 왼쪽 판은 음수·오른쪽 판은 양수여야 마루에서 모인다. 예전엔 반대라 ∨ 모양이었다.
        GameObject l = Box(g, "SideL", new Vector3(-0.62f, 0.95f, 0f), new Vector3(0.08f, 2.2f, 3.1f), mat);
        GameObject r = Box(g, "SideR", new Vector3(0.62f, 0.95f, 0f), new Vector3(0.08f, 2.2f, 3.1f), mat);
        l.transform.localRotation = Quaternion.Euler(0f, 0f, -32f);
        r.transform.localRotation = Quaternion.Euler(0f, 0f, 32f);

        Box(g, "Back", new Vector3(0f, 0.85f, -1.52f), new Vector3(2.2f, 1.7f, 0.06f), mat);

        // 입구는 한쪽만 젖혀 놓는다.
        GameObject flap = Box(g, "Flap", new Vector3(-0.72f, 0.8f, 1.55f), new Vector3(1.1f, 1.6f, 0.05f), matTentDark);
        flap.transform.localRotation = Quaternion.Euler(0f, 42f, 12f);

        // 능선 폴대와 팽팽한 줄
        Box(g, "Ridge", new Vector3(0f, 1.86f, 0f), new Vector3(0.07f, 0.07f, 3.3f), matWoodDark);
        Guy(g, new Vector3(-1.35f, 0f, 1.7f), new Vector3(-0.55f, 1.7f, 1.5f));
        Guy(g, new Vector3(1.35f, 0f, 1.7f), new Vector3(0.55f, 1.7f, 1.5f));
        Guy(g, new Vector3(-1.35f, 0f, -1.7f), new Vector3(-0.55f, 1.7f, -1.5f));
        Guy(g, new Vector3(1.35f, 0f, -1.7f), new Vector3(0.55f, 1.7f, -1.5f));
    }

    /// <summary>텐트 고정줄 하나 (말뚝 + 줄).</summary>
    private static void Guy(Transform g, Vector3 peg, Vector3 anchor)
    {
        Box(g, "Peg", peg + Vector3.up * 0.12f, new Vector3(0.05f, 0.24f, 0.05f), matWoodDark);
        Vector3 mid = (peg + anchor) * 0.5f;
        Vector3 dir = anchor - peg;
        var line = Prim(g, "Rope", PrimitiveType.Cylinder, mid, Vector3.zero,
            new Vector3(0.02f, dir.magnitude * 0.5f, 0.02f), matRope);
        line.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
        Object.DestroyImmediate(line.GetComponent<Collider>()); // 줄에 걸려 넘어지지 않게
    }

    private static void PicnicTable(Transform p, Vector3 pos)
    {
        Transform g = Group(p, "PicnicTable", pos, 0f);
        Box(g, "Top", new Vector3(0f, 0.75f, 0f), new Vector3(2.2f, 0.1f, 1.05f), matWood);
        Box(g, "TopEdge", new Vector3(0f, 0.7f, 0f), new Vector3(2.3f, 0.05f, 1.15f), matWoodDark);
        Box(g, "BenchL", new Vector3(0f, 0.42f, 0.78f), new Vector3(2.2f, 0.08f, 0.34f), matWood);
        Box(g, "BenchR", new Vector3(0f, 0.42f, -0.78f), new Vector3(2.2f, 0.08f, 0.34f), matWood);

        foreach (float x in new[] { -0.95f, 0.95f })
        {
            GameObject a = Box(g, "Leg", new Vector3(x, 0.38f, 0.45f), new Vector3(0.1f, 0.95f, 0.1f), matWoodDark);
            GameObject b = Box(g, "Leg", new Vector3(x, 0.38f, -0.45f), new Vector3(0.1f, 0.95f, 0.1f), matWoodDark);
            a.transform.localRotation = Quaternion.Euler(22f, 0f, 0f);
            b.transform.localRotation = Quaternion.Euler(-22f, 0f, 0f);
        }
    }

    private static void Cooler(Transform p, Vector3 pos)
    {
        Transform g = Group(p, "Cooler", pos, 18f);
        Box(g, "Body", new Vector3(0f, 0.3f, 0f), new Vector3(0.86f, 0.6f, 0.54f), matAccent);
        Box(g, "Lid", new Vector3(0f, 0.64f, 0f), new Vector3(0.92f, 0.1f, 0.6f), matDark);
        Box(g, "Handle", new Vector3(0f, 0.42f, 0.3f), new Vector3(0.34f, 0.06f, 0.06f), matMetal);
    }

    /// <summary>접이식 캠핑 의자.</summary>
    private static void CampChair(Transform p, Vector3 pos, float rotY)
    {
        Transform g = Group(p, "CampChair", pos, rotY);
        Box(g, "Seat", new Vector3(0f, 0.42f, 0f), new Vector3(0.62f, 0.06f, 0.58f), matAccent);
        GameObject back = Box(g, "Back", new Vector3(0f, 0.75f, -0.28f), new Vector3(0.62f, 0.6f, 0.06f), matAccent);
        back.transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);
        foreach (float x in new[] { -0.28f, 0.28f })
        {
            GameObject a = Box(g, "Leg", new Vector3(x, 0.21f, 0.2f), new Vector3(0.05f, 0.5f, 0.05f), matMetal);
            GameObject b = Box(g, "Leg", new Vector3(x, 0.21f, -0.2f), new Vector3(0.05f, 0.5f, 0.05f), matMetal);
            a.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
            b.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
        }
    }

    /// <summary>
    /// Models 폴더의 실제 제작 모델을 프롭 크기에 맞춰 정규화한다.
    /// 모델의 원점과 단위가 달라도 바닥에 정확히 서도록 렌더러 경계를 기준으로 보정한다.
    /// </summary>
    private static void ImportedModelProp(Transform parent, string name, string assetPath,
        float targetFootprint, System.Action fallback)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (asset == null)
        {
            Debug.LogWarning($"[Stage2] 모델을 찾지 못해 임시 프롭을 사용합니다: {assetPath}");
            fallback?.Invoke();
            return;
        }

        Transform group = Group(parent, name, Vector3.zero, 0f);
        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(asset, group);
        model.name = "Model";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;

        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            Debug.LogWarning($"[Stage2] 렌더러가 없는 모델이라 임시 프롭을 사용합니다: {assetPath}");
            Object.DestroyImmediate(group.gameObject);
            fallback?.Invoke();
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        float widest = Mathf.Max(0.001f, Mathf.Max(bounds.size.x, bounds.size.z));
        model.transform.localScale *= targetFootprint / widest;

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        model.transform.position += new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        BoxCollider collider = group.gameObject.AddComponent<BoxCollider>();
        collider.center = group.InverseTransformPoint(bounds.center);
        collider.size = new Vector3(bounds.size.x * 0.92f, bounds.size.y, bounds.size.z * 0.92f);
    }

    /// <summary>한 화면짜리 캠프가 아니라 탐색할 목적지가 있는 넓은 야영지로 구역을 나눈다.</summary>
    private static void BuildDistricts(Transform root)
    {
        Transform districts = Group(root, "Districts", Vector3.zero, 0f);

        Transform ranger = Group(districts, "WestRangerOutpost", new Vector3(-36f, 0f, 22f), 0f);
        Place("Tent_Red", ranger, new Vector3(-3f, 0f, 2f), 70f);
        Place("PicnicTable", ranger, new Vector3(3f, 0f, 0f), 90f);
        Place("WoodPile", ranger, new Vector3(0f, 0f, -4f), -20f);
        Place("CampChair", ranger, new Vector3(1f, 0f, 3f), 150f);
        Place("Cooler", ranger, new Vector3(4f, 0f, 3f), -30f);

        Transform south = Group(districts, "SouthPicnicGrove", new Vector3(-31f, 0f, -31f), 0f);
        Place("Tent_Blue", south, new Vector3(-4f, 0f, 1f), 35f);
        Place("PicnicTable", south, new Vector3(2f, 0f, 2f), 15f);
        Place("CampChair", south, new Vector3(4f, 0f, -1f), -115f);
        Place("CampChair", south, new Vector3(1f, 0f, -3f), -20f);
        Clothesline(south, new Vector3(-1f, 0f, -4f), new Vector3(5f, 0f, -4f));

        Transform lakeCamp = Group(districts, "LakesideCamp", new Vector3(34f, 0f, 10f), 0f);
        Place("Tent_Red", lakeCamp, new Vector3(5f, 0f, 0f), -70f);
        Place("CampChair", lakeCamp, new Vector3(0f, 0f, 2f), 15f);
        Place("CampChair", lakeCamp, new Vector3(2f, 0f, 3f), -20f);
        Place("Cooler", lakeCamp, new Vector3(4f, 0f, 4f), 18f);

        Transform lookout = Group(districts, "NorthLookout", new Vector3(0f, 0f, 51f), 0f);
        Box(lookout, "Deck", new Vector3(0f, 0.35f, 0f), new Vector3(8f, 0.7f, 6f), matWood);
        for (int i = -1; i <= 1; i += 2)
            Box(lookout, "Rail", new Vector3(i * 3.8f, 1f, 0f), new Vector3(0.12f, 1.3f, 6f), matWoodDark);
        Place("CampChair", lookout, new Vector3(-1.4f, 0.7f, 0f), 180f);
        Place("CampChair", lookout, new Vector3(1.4f, 0.7f, 0f), 180f);

        Sign(districts, "TrailMap", new Vector3(3.8f, 0f, -48f), 180f,
            "북쪽 전망대 · 서쪽 관리소\n동쪽 호수 · 남쪽 피크닉 숲");
    }

    /// <summary>기둥 두 개에 걸린 빨랫줄 + 전구. 캠프 위쪽을 채워 준다.</summary>
    private static void Clothesline(Transform p, Vector3 a, Vector3 b)
    {
        Transform g = Group(p, "Clothesline", Vector3.zero, 0f);
        Box(g, "PoleA", a + Vector3.up * 1.3f, new Vector3(0.1f, 2.6f, 0.1f), matWoodDark);
        Box(g, "PoleB", b + Vector3.up * 1.3f, new Vector3(0.1f, 2.6f, 0.1f), matWoodDark);

        Vector3 top = Vector3.up * 2.45f;
        Vector3 dir = (b + top) - (a + top);
        var rope = Prim(g, "Rope", PrimitiveType.Cylinder, (a + b) * 0.5f + top, Vector3.zero,
            new Vector3(0.025f, dir.magnitude * 0.5f, 0.025f), matRope);
        rope.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
        Object.DestroyImmediate(rope.GetComponent<Collider>());

        for (int i = 1; i < 5; i++)
        {
            Vector3 at = Vector3.Lerp(a + top, b + top, i / 5f);
            var bulb = Prim(g, "Bulb", PrimitiveType.Sphere, at - Vector3.up * 0.16f,
                Vector3.one * 0.13f, matEmber);
            Object.DestroyImmediate(bulb.GetComponent<Collider>());
        }
    }

    /// <summary>패 놓은 장작 더미와 도끼 그루터기.</summary>
    private static void WoodPile(Transform p, Vector3 pos, float rotY)
    {
        Transform g = Group(p, "WoodPile", pos, rotY);
        Prim(g, "Stump", PrimitiveType.Cylinder, new Vector3(0f, 0.3f, 0f), new Vector3(0.7f, 0.3f, 0.7f), matWood);

        for (int row = 0; row < 3; row++)
        {
            int n = 4 - row;
            for (int i = 0; i < n; i++)
            {
                var at = new Vector3(1.3f, 0.16f + row * 0.28f, (i - (n - 1) * 0.5f) * 0.3f);
                Prim(g, "Log", PrimitiveType.Cylinder, at, new Vector3(0f, 0f, 90f),
                    new Vector3(0.26f, 0.5f, 0.26f), row % 2 == 0 ? matWood : matWoodDark);
            }
        }
    }

    /// <summary>출구 방향을 알려 주는 나무 표지판.</summary>
    private static void Signpost(Transform p, Vector3 pos, float rotY)
    {
        Transform g = Group(p, "Signpost", pos, rotY);
        Box(g, "Post", new Vector3(0f, 0.9f, 0f), new Vector3(0.12f, 1.8f, 0.12f), matWoodDark);
        GameObject board = Box(g, "Board", new Vector3(0.35f, 1.55f, 0f), new Vector3(1.1f, 0.3f, 0.05f), matWood);
        board.transform.localRotation = Quaternion.Euler(0f, 0f, -6f);
        GameObject board2 = Box(g, "Board2", new Vector3(-0.3f, 1.15f, 0f), new Vector3(0.9f, 0.26f, 0.05f), matWood);
        board2.transform.localRotation = Quaternion.Euler(0f, 0f, 5f);
    }

    // ---------------------------------------------------------------- 숲 · 잡목

    /// <summary>
    /// 맵 가장자리를 두르는 숲. 침엽수와 활엽수를 섞고 크기·회전을 흩뜨려
    /// 같은 나무가 반복되는 느낌을 없앤다. 안쪽에도 몇 그루 심어 시야를 끊는다.
    /// </summary>
    private static void Forest(Transform p)
    {
        Transform g = Group(p, "Forest", Vector3.zero, 0f);

        for (int ring = 0; ring < 4; ring++)
        {
            int count = 36 + ring * 6;
            float radius = 38f + ring * 7f;
            for (int i = 0; i < count; i++)
            {
                float a = (i + Random.Range(-0.35f, 0.35f)) / count * Mathf.PI * 2f;
                var at = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (radius + Random.Range(-1.5f, 1.5f));
                if (Mathf.Abs(at.x) < 3.2f || Mathf.Abs(at.z - 18f) < 2.6f || Mathf.Abs(at.z + 31f) < 2.4f)
                    continue; // 주요 산책로는 막지 않는다.
                if (Random.value < 0.55f)
                    Place("Tree_Pine", g, at, Random.value * 360f, Random.Range(0.85f, 1.5f));
                else
                    Place("Tree_Broad", g, at, Random.value * 360f, Random.Range(0.8f, 1.35f));
            }
        }

        // 캠프 안쪽에 드문드문 — 은폐물이자 시야 차단.
        Vector3[] inner =
        {
            new Vector3(-12f, 0f, 6f), new Vector3(12.5f, 0f, -8f), new Vector3(-10f, 0f, -9f),
            new Vector3(4f, 0f, 15f), new Vector3(-14f, 0f, 14f), new Vector3(15f, 0f, 2f),
            new Vector3(-28f, 0f, -18f), new Vector3(26f, 0f, -24f), new Vector3(-38f, 0f, 34f),
            new Vector3(45f, 0f, 4f), new Vector3(18f, 0f, 45f), new Vector3(-8f, 0f, 48f),
        };
        foreach (Vector3 at in inner)
        {
            if (Random.value < 0.5f)
                Place("Tree_Pine", g, at, Random.value * 360f, Random.Range(1f, 1.4f));
            else
                Place("Tree_Broad", g, at, Random.value * 360f, Random.Range(0.9f, 1.2f));
        }
    }

    /// <summary>원뿔 세 겹으로 쌓은 침엽수.</summary>
    private static void PineTree(Transform p, Vector3 pos, float scale)
    {
        Transform g = Group(p, "PineTree", pos, Random.value * 360f);
        g.localScale = Vector3.one * scale;

        Prim(g, "Trunk", PrimitiveType.Cylinder, new Vector3(0f, 1f, 0f), new Vector3(0.42f, 1f, 0.42f), matWoodDark);
        Cone(g, "Canopy1", new Vector3(0f, 1.9f, 0f), 2.5f, 2.0f, matLeafDark);
        Cone(g, "Canopy2", new Vector3(0f, 3.1f, 0f), 1.9f, 1.8f, matLeaf);
        Cone(g, "Canopy3", new Vector3(0f, 4.2f, 0f), 1.2f, 1.5f, matLeafLight);
    }

    /// <summary>덩어리 세 개로 수관을 만든 활엽수.</summary>
    private static void BroadTree(Transform p, Vector3 pos, float scale)
    {
        Transform g = Group(p, "BroadTree", pos, Random.value * 360f);
        g.localScale = Vector3.one * scale;

        GameObject trunk = Prim(g, "Trunk", PrimitiveType.Cylinder, new Vector3(0f, 1.5f, 0f),
            new Vector3(0.5f, 1.5f, 0.5f), matWood);
        trunk.transform.localRotation = Quaternion.Euler(Random.Range(-4f, 4f), 0f, Random.Range(-4f, 4f));

        Prim(g, "Leaves1", PrimitiveType.Sphere, new Vector3(0f, 3.3f, 0f), Vector3.one * 2.9f, matLeaf);
        Prim(g, "Leaves2", PrimitiveType.Sphere, new Vector3(0.9f, 2.9f, 0.5f), Vector3.one * 2f, matLeafDark);
        Prim(g, "Leaves3", PrimitiveType.Sphere, new Vector3(-0.7f, 3.1f, -0.6f), Vector3.one * 1.9f, matLeafLight);
    }

    /// <summary>바위·덤불·그루터기·갈대를 흩뿌려 빈 땅을 메운다.</summary>
    private static void Scatter(Transform p)
    {
        Transform g = Group(p, "Scatter", Vector3.zero, 0f);

        for (int i = 0; i < 170; i++)
        {
            var at = new Vector3(Random.Range(-61f, 61f), 0f, Random.Range(-61f, 61f));
            if (at.magnitude < 5.5f)
                continue; // 모닥불 주변은 비워 둔다
            if (Mathf.Abs(at.x) < 2.8f || Mathf.Abs(at.z - 18f) < 2.2f || Mathf.Abs(at.z + 31f) < 2f)
                continue; // 산책로 가독성 유지

            float roll = Random.value;
            if (roll < 0.35f)
            {
                GameObject rock = Place("Rock", g, at + Vector3.up * 0.15f, Random.value * 360f);
                if (rock != null)
                {
                    rock.transform.localRotation = Quaternion.Euler(Random.value * 40f, Random.value * 360f, Random.value * 40f);
                    rock.transform.localScale = new Vector3(
                        Random.Range(0.4f, 1.2f), Random.Range(0.3f, 0.8f), Random.Range(0.4f, 1.2f));
                }
            }
            else if (roll < 0.75f)
            {
                Place("Bush", g, at, Random.value * 360f, Random.Range(0.5f, 1f));
            }
            else
            {
                GameObject stump = Place("Stump", g, at + Vector3.up * 0.2f);
                if (stump != null)
                {
                    stump.transform.localRotation = Quaternion.Euler(Random.Range(-6f, 6f), 0f, Random.Range(-6f, 6f));
                    stump.transform.localScale = new Vector3(
                        Random.Range(0.5f, 0.8f), 0.2f, Random.Range(0.5f, 0.8f));
                }
            }
        }
    }

    // ---------------------------------------------------------------- 잡목 모델 (프리팹으로 굽는다)

    /// <summary>바위 한 덩이. 크기·각도는 씬에 놓을 때 인스턴스에서 흩뜨린다.</summary>
    private static void RockModel(Transform p)
    {
        Transform g = Group(p, "Rock", Vector3.zero, 0f);
        Prim(g, "Body", PrimitiveType.Cube, Vector3.zero, Vector3.one, matStone);
    }

    /// <summary>덤불. 크기 1 기준으로 만들고, 인스턴스 스케일로 키우고 줄인다.</summary>
    private static void BushModel(Transform p)
    {
        Transform g = Group(p, "Bush", Vector3.zero, 0f);
        var a = Prim(g, "Leaf", PrimitiveType.Sphere, new Vector3(0f, 0.45f, 0f), Vector3.one, matLeafDark);
        var b = Prim(g, "Leaf", PrimitiveType.Sphere, new Vector3(0.4f, 0.35f, 0.2f), Vector3.one * 0.75f, matLeaf);
        Object.DestroyImmediate(a.GetComponent<Collider>());
        Object.DestroyImmediate(b.GetComponent<Collider>());
    }

    /// <summary>그루터기.</summary>
    private static void StumpModel(Transform p)
    {
        Transform g = Group(p, "Stump", Vector3.zero, 0f);
        GameObject body = Prim(g, "Body", PrimitiveType.Cylinder, Vector3.zero, Vector3.one, matWoodDark);

        // 씬에 놓을 때 인스턴스에서 납작하게 눌린다. FixFlatCollider는 굽는 시점의 크기만 보므로
        // 여기서 미리 박스로 바꿔 둬야 눌린 뒤에도 콜라이더가 모양을 따라간다.
        Object.DestroyImmediate(body.GetComponent<Collider>());
        body.AddComponent<BoxCollider>();
    }

    /// <summary>
    /// 원뿔은 기본 프리미티브에 없어서 실린더 윗면을 좁혀 만든다.
    /// 나무마다 새 메시를 만들면 그게 전부 씬 파일에 직렬화되어 용량이 터지므로,
    /// 애셋으로 한 번만 굽고 모든 나무가 같은 메시를 공유한다.
    /// </summary>
    private static GameObject Cone(Transform parent, string name, Vector3 localPos, float radius, float height, Material mat)
    {
        GameObject go = Prim(parent, name, PrimitiveType.Cylinder, localPos, Vector3.zero,
            new Vector3(radius, height * 0.5f, radius), mat);

        go.GetComponent<MeshFilter>().sharedMesh = ConeMesh();

        // 콜라이더는 실린더 그대로면 수관이 벽처럼 굴어서, 나무는 줄기만 막는다.
        Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    /// <summary>원뿔 메시(애셋으로 한 번만 굽는다). 타이틀 배경 빌더도 같은 것을 쓴다.</summary>
    internal static Mesh ConeMesh()
    {
        if (coneMesh != null)
            return coneMesh;

        coneMesh = AssetDatabase.LoadAssetAtPath<Mesh>(ConeMeshPath);
        if (coneMesh != null)
            return coneMesh;

        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Mesh source = temp.GetComponent<MeshFilter>().sharedMesh;

        var cone = new Mesh { name = "Camp_Cone" };
        EditorUtility.CopySerialized(source, cone);
        Object.DestroyImmediate(temp);

        Vector3[] verts = cone.vertices;
        for (int i = 0; i < verts.Length; i++)
        {
            if (verts[i].y > 0f)
            {
                verts[i].x *= 0.04f;
                verts[i].z *= 0.04f;
            }
        }
        cone.vertices = verts;
        cone.RecalculateNormals();
        cone.RecalculateBounds();

        if (!AssetDatabase.IsValidFolder("Assets/Models/Generated"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Models"))
                AssetDatabase.CreateFolder("Assets", "Models");
            AssetDatabase.CreateFolder("Assets/Models", "Generated");
        }

        AssetDatabase.CreateAsset(cone, ConeMeshPath);
        coneMesh = cone;
        return coneMesh;
    }

    // ---------------------------------------------------------------- 재료 배치

    /// <summary>
    /// 장작·고기·야채를 캠핑장 곳곳에 흩어 놓는다.
    ///
    /// 한자리에 모아 두면 한 사람이 다 주워 버려서 협동이 성립하지 않는다.
    /// 캠프 안(식탁·쿨러 위), 텐트 주변, 숲 가장자리, 호숫가로 갈라 놓아
    /// 흩어져서 뒤지는 편이 항상 빠르게 만든다.
    ///
    /// 개수는 <see cref="CampGameManager"/>가 씬을 세어 자동으로 목표에 반영한다.
    /// 여기서 좌표를 더하거나 빼면 목표도 그만큼 따라온다.
    /// </summary>
    private static void PlaceIngredients(Transform root)
    {
        Transform g = Group(root, "Ingredients", Vector3.zero, 0f);

        // 장작 — 불을 피우는 연료. 캠프 바깥쪽에 둬서 한 번은 나가게 만든다.
        Vector3[] firewood =
        {
            new Vector3(-4.6f, 0.12f, -2.1f),   // 장작더미 옆
            new Vector3(-39f, 0.12f, 18f),      // 서쪽 관리소
            new Vector3(-34f, 0.12f, -34f),     // 남쪽 피크닉 숲
            new Vector3(24f, 0.42f, 18f),       // 호수 잔교
            new Vector3(1.5f, 0.82f, 51f),      // 북쪽 전망대
            new Vector3(48f, 0.12f, -18f),      // 동쪽 외곽 숲
        };
        foreach (Vector3 at in firewood)
            Collectible(g, Ingredient.Firewood, "장작", at, FirewoodModel);

        // 고기 — 쿨러·식탁처럼 "있을 법한 자리"와 텐트 주변에.
        Vector3[] meat =
        {
            new Vector3(9.2f, 0.72f, 4.5f),     // 쿨러 위
            new Vector3(-32f, 0.86f, 24f),      // 관리소 식탁
            new Vector3(-29f, 0.86f, -29f),     // 피크닉 식탁
            new Vector3(39f, 0.72f, 14f),       // 호숫가 쿨러
            new Vector3(-8f, 0.1f, 42f),        // 전망대 아래 숲
            new Vector3(42f, 0.1f, -38f),       // 남동쪽 야생 구역
        };
        foreach (Vector3 at in meat)
            Collectible(g, Ingredient.Meat, "고기", at, MeatModel);

        // 야채 — 나머지 방향을 채운다.
        Vector3[] vegetable =
        {
            new Vector3(7.7f, 0.86f, 4.3f),     // 식탁 위
            new Vector3(-42f, 0.1f, 28f),       // 서쪽 관리소 뒤
            new Vector3(-24f, 0.1f, -38f),      // 남쪽 피크닉 숲
            new Vector3(47f, 0.1f, 24f),        // 호수 동쪽
            new Vector3(-2f, 0.82f, 52f),       // 전망대
            new Vector3(32f, 0.1f, -46f),       // 남동쪽 숲
        };
        foreach (Vector3 at in vegetable)
            Collectible(g, Ingredient.Vegetable, "야채", at, VegetableModel);
    }

    /// <summary>흩어 놓은 재료에 고유 ID를 매긴다. 서버가 이 번호로 획득을 확정한다.</summary>
    private static void AssignIngredientIds()
    {
        CollectibleItem[] items = Object.FindObjectsByType<CollectibleItem>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < items.Length; i++)
        {
            items[i].SetItemId(i);
            EditorUtility.SetDirty(items[i]);
        }

        Debug.Log($"[Stage2] 재료 {items.Length}개 배치 완료 (ID 부여됨).");
    }

    // ---------------------------------------------------------------- 재료 모델

    private static void FirewoodModel(Transform g)
    {
        Prim(g, "Log1", PrimitiveType.Cylinder, new Vector3(0f, 0.08f, 0f), new Vector3(90f, 0f, 0f), new Vector3(0.12f, 0.35f, 0.12f), matWood);
        Prim(g, "Log2", PrimitiveType.Cylinder, new Vector3(0.14f, 0.08f, 0f), new Vector3(90f, 0f, 0f), new Vector3(0.12f, 0.35f, 0.12f), matWoodDark);
        Prim(g, "Log3", PrimitiveType.Cylinder, new Vector3(0.07f, 0.22f, 0f), new Vector3(90f, 0f, 0f), new Vector3(0.12f, 0.35f, 0.12f), matWood);
    }

    /// <summary>접시에 얹힌 생고기 두 덩이. 붉은 살에 흰 지방 결을 넣어 멀리서도 알아보게.</summary>
    private static void MeatModel(Transform g)
    {
        Box(g, "Tray", new Vector3(0f, 0.02f, 0f), new Vector3(0.4f, 0.03f, 0.3f), matFat);
        Box(g, "Cut1", new Vector3(-0.07f, 0.07f, 0f), new Vector3(0.22f, 0.07f, 0.2f), matMeat);
        Box(g, "Cut2", new Vector3(0.09f, 0.08f, 0.02f), new Vector3(0.2f, 0.07f, 0.18f), matMeat);
        Box(g, "Marble", new Vector3(-0.07f, 0.11f, 0f), new Vector3(0.16f, 0.012f, 0.05f), matFat);
    }

    /// <summary>껍질을 반쯤 벗긴 옥수수. 노란 알과 초록 껍질 대비가 확실하다.</summary>
    private static void VegetableModel(Transform g)
    {
        Prim(g, "Cob", PrimitiveType.Cylinder, new Vector3(0f, 0.09f, 0f), new Vector3(90f, 0f, 0f),
            new Vector3(0.09f, 0.2f, 0.09f), matCorn);

        GameObject huskA = Box(g, "Husk1", new Vector3(0.02f, 0.07f, -0.16f), new Vector3(0.1f, 0.03f, 0.2f), matHusk);
        huskA.transform.localRotation = Quaternion.Euler(18f, 12f, 0f);

        GameObject huskB = Box(g, "Husk2", new Vector3(-0.03f, 0.07f, -0.17f), new Vector3(0.09f, 0.03f, 0.19f), matLeafDark);
        huskB.transform.localRotation = Quaternion.Euler(14f, -20f, 0f);
    }

    // ---------------------------------------------------------------- 무기 배치

    /// <summary>
    /// 소세지 꼬치·국자·냉동참치 같은 음식 무기를 캠핑장에 놓는다.
    /// 프리팹은 Tools > Weapons > Build Weapon Prefabs 가 .blend에서 만들어 둔 것을 쓴다.
    /// 무기는 하나씩 흩어 놓아, 먼저 줍는 사람이 유리해지도록 한다.
    /// </summary>
    private static void PlaceWeapons(Transform parent)
    {
        // (놓을 자리, 바라볼 방향) — 모닥불 주변과 텐트/식탁 근처에 골고루.
        (Vector3 pos, float rotY)[] spots =
        {
            (new Vector3(2.6f, 0f, 2.4f), 25f),    // 모닥불 옆
            (new Vector3(-37f, 0f, 20f), -40f),    // 서쪽 관리소
            (new Vector3(-29f, 0.82f, -29f), 90f), // 피크닉 식탁
            (new Vector3(39f, 0f, 10f), 150f),     // 호숫가 텐트
            (new Vector3(0f, 0.72f, 51f), -150f),  // 북쪽 전망대
            (new Vector3(37f, 0f, -32f), 0f),      // 남동쪽 숲
            (new Vector3(-49f, 0f, -10f), 60f),    // 서쪽 외곽
            (new Vector3(48f, 0f, 36f), -70f),     // 북동쪽 숲
        };

        var weapons = new GameObject("Weapons").transform;
        weapons.SetParent(parent, false);

        int placed = 0;
        string[] names = WeaponPrefabBuilder.CampgroundWeapons;

        for (int i = 0; i < names.Length && i < spots.Length; i++)
        {
            string path = WeaponPrefabBuilder.PrefabPath(names[i]);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[Stage2] 무기 프리팹이 없어 건너뜁니다: {path}");
                continue;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, weapons);

            // 프리팹의 원점은 손잡이라서, 그대로 놓으면 모델이 바닥에 묻힌다.
            var weapon = go.GetComponent<Weapon>();
            float lift = weapon != null ? weapon.groundOffset : 0f;

            go.transform.SetPositionAndRotation(
                spots[i].pos + Vector3.up * lift,
                Quaternion.Euler(0f, spots[i].rotY, 0f));
            placed++;
        }

        if (placed == 0)
        {
            Debug.LogWarning("[Stage2] 배치된 무기가 없습니다. " +
                             "'Tools > Weapons > Build Weapon Prefabs'를 먼저 실행하세요.");
        }
    }

    // ---------------------------------------------------------------- 스폰 포인트

    private static void SpawnPoints(Vector3 basePos)
    {
        var root = new GameObject("NetworkSpawnPoints").transform;

        // 대기실 정원 상한(8명)만큼 두 줄로. 죽으면 여기로 리스폰하므로 넉넉히 벌려 둔다.
        const int count = 8;
        for (int i = 0; i < count; i++)
        {
            var offset = new Vector3((i % 4 - 1.5f) * 2.2f, 0f, i / 4 * -2.2f);

            var sp = new GameObject("Spawn_" + (char)('A' + i));
            sp.transform.SetParent(root, false);
            sp.transform.position = basePos + offset;
            sp.AddComponent<Mirror.NetworkStartPosition>();
        }
    }

    // ---------------------------------------------------------------- 유틸

    /// <summary>
    /// 한낮의 해. 실제 값은 <see cref="DayNightController"/>가 매 프레임 덮어쓰므로
    /// 여기서는 "시작 상태"만 맞춰 둔다(에디터에서 씬을 열었을 때의 모습).
    /// </summary>
    private static void AddSun()
    {
        var go = new GameObject("Directional Light");
        go.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
        var l = go.AddComponent<Light>();
        l.type = LightType.Directional;
        l.color = new Color(1f, 0.95f, 0.84f);
        l.intensity = 1.35f;
        l.shadows = LightShadows.Soft;
        l.shadowStrength = 0.75f;
        RenderSettings.sun = l;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.55f, 0.66f, 0.82f);
        RenderSettings.ambientEquatorColor = new Color(0.44f, 0.47f, 0.44f);
        RenderSettings.ambientGroundColor = new Color(0.24f, 0.24f, 0.2f);

        // 멀리 있는 숲이 배경으로 녹아들도록 옅은 안개.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.68f, 0.75f, 0.82f);
        RenderSettings.fogStartDistance = 45f;
        RenderSettings.fogEndDistance = 145f;
    }

    /// <summary>재료를 다 모으면 해를 넘겨 줄 연출 컨트롤러를 씬에 둔다.</summary>
    private static void AddDayNight()
    {
        if (Object.FindFirstObjectByType<DayNightController>(FindObjectsInactive.Include) != null)
            return;

        var go = new GameObject("DayNight");
        go.AddComponent<DayNightController>();
    }

    /// <summary>
    /// 목표를 말로 알려 주는 팻말. 스폰 지점과 화로 앞에 세운다.
    /// 팻말은 rotY가 가리키는 쪽에서 읽히므로, 스폰(0, -14)에서 북쪽으로 걸어오는
    /// 사람이 정면으로 보도록 둘 다 남쪽을 보게 세운다.
    /// </summary>
    private static void GoalSigns(Transform root)
    {
        Sign(root, "SpawnSign", new Vector3(3.2f, 0f, -49f), 180f,
            "넓은 야영지를 나눠 수색하라\n장작·고기·야채를 다 모으면 해가 진다");

        Sign(root, "FireSign", new Vector3(-3.2f, 0f, -3.4f), 165f,
            "저녁이 되면 화로에 장작을 넣고\n그릴에 바베큐를 구워라");

        Sign(root, "CrossroadSign", new Vector3(-4f, 0f, 18f), 90f,
            "← 서쪽 관리소 · 북쪽 전망대 ↑\n동쪽 호수 → · 남쪽 피크닉 숲 ↓");
    }

    /// <summary>나무 팻말 + TMP 글자. rotY가 향하는 쪽(+Z)에서 읽힌다.</summary>
    private static void Sign(Transform parent, string name, Vector3 pos, float rotY, string text)
    {
        Transform g = Group(parent, name, pos, rotY);
        Box(g, "Post", new Vector3(0f, 0.7f, 0f), new Vector3(0.1f, 1.4f, 0.1f), matWoodDark);
        Box(g, "Board", new Vector3(0f, 1.6f, 0f), new Vector3(3.2f, 0.95f, 0.08f), matWood);

        // 판보다 살짝 안쪽으로 잡아 글자가 테두리에 닿지 않게 한다.
        BuilderText.World(g, "Text", new Vector3(0f, 1.6f, 0.055f), text,
            new Vector2(2.9f, 0.76f), BuilderText.SignInk);
    }

    /// <summary>주울 수 있는 재료 하나를 놓는다. 방향은 조금씩 틀어 놓아야 놓아둔 티가 난다.</summary>
    private static Transform Collectible(Transform parent, Ingredient kind, string displayName,
        Vector3 pos, System.Action<Transform> model)
    {
        Transform g = Group(parent, displayName, pos, Random.Range(0f, 360f));
        var c = g.gameObject.AddComponent<CollectibleItem>();
        c.SetDisplayName(displayName);
        c.SetKind(kind);
        model(g);
        return g;
    }

    /// <summary>지정한 이름의 그룹들을 통째로 static으로 표시한다.</summary>
    private static void MarkStatic(Transform root, params string[] groupNames)
    {
        foreach (string name in groupNames)
        {
            Transform group = root.Find(name);
            if (group == null)
                continue;

            foreach (Transform t in group.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic
                                                                     | StaticEditorFlags.OccluderStatic
                                                                     | StaticEditorFlags.OccludeeStatic);
        }
    }

    private static Transform Group(Transform parent, string name, Vector3 localPos, float rotY)
    {
        var g = new GameObject(name).transform;
        g.SetParent(parent, false);
        g.localPosition = localPos;
        g.localRotation = Quaternion.Euler(0f, rotY, 0f);
        return g;
    }

    private static GameObject Box(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat)
        => Prim(parent, name, PrimitiveType.Cube, localPos, Vector3.zero, size, mat);

    private static GameObject Prim(Transform parent, string name, PrimitiveType type, Vector3 localPos, Vector3 size, Material mat)
        => Prim(parent, name, type, localPos, Vector3.zero, size, mat);

    private static GameObject Prim(Transform parent, string name, PrimitiveType type, Vector3 localPos, Vector3 euler, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = size;
        if (mat != null)
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;

        FixFlatCollider(go, type, size);
        return go;
    }

    /// <summary>
    /// 납작하게 눌러 놓은 원기둥·구의 콜라이더를 실제 모양에 맞춘다.
    ///
    /// CapsuleCollider와 SphereCollider의 반지름은 X·Z 스케일 중 <b>큰 쪽</b>을 따라가고,
    /// 캡슐 높이는 최소 '지름'만큼 강제된다. 그래서 지름 9m·두께 0.005m로 눌러 만든
    /// 잔디 무늬는 실제로는 높이 9m짜리 보이지 않는 덩어리가 되고,
    /// 그 위에 올라선 플레이어는 허공을 걷는 것처럼 보인다.
    ///
    /// 이런 경우 BoxCollider로 갈아 끼운다. 박스는 스케일을 그대로 따라가므로
    /// 두께 0.005m는 정말 0.005m가 되고, 바닥에 딱 붙어 아무 문제도 일으키지 않는다.
    /// </summary>
    private static void FixFlatCollider(GameObject go, PrimitiveType type, Vector3 size)
    {
        if (type != PrimitiveType.Cylinder && type != PrimitiveType.Sphere && type != PrimitiveType.Capsule)
            return;

        float widest = Mathf.Max(Mathf.Abs(size.x), Mathf.Abs(size.z));
        if (Mathf.Abs(size.y) >= widest * 0.5f)
            return; // 둥근 콜라이더가 모양에서 크게 벗어나지 않는다

        Collider round = go.GetComponent<Collider>();
        if (round != null)
            Object.DestroyImmediate(round);

        go.AddComponent<BoxCollider>(); // 메시 경계에 맞춰 자동으로 잡힌다
    }

    /// <summary>
    /// 중심에서 각도 a 방향으로 세운 막대를 위쪽이 가운데로 모이도록 눕히는 회전.
    ///
    /// 오일러 (x, 0, z)에서 막대의 +Y는 대략 (-sin z, ..., cos z·sin x) 방향으로 간다.
    /// 안쪽(-cos a, -sin a)으로 눕히려면 z = +cos a·각도, x = -sin a·각도 여야 한다.
    /// 부호를 뒤집으면 위가 벌어지는 ∨ 모양이 된다 — 모닥불 장작과 삼각대가 그랬다.
    /// </summary>
    private static Vector3 LeanInward(float angle, float degrees)
        => new Vector3(-Mathf.Sin(angle) * degrees, 0f, Mathf.Cos(angle) * degrees);

    private static void LoadMaterials()
    {
        matGrass = GetMat("Grass", new Color(0.30f, 0.5f, 0.22f));
        matGrassDark = GetMat("GrassDark", new Color(0.22f, 0.39f, 0.17f));
        matGrassLight = GetMat("GrassLight", new Color(0.42f, 0.6f, 0.28f));
        matDirt = GetMat("Dirt", new Color(0.45f, 0.36f, 0.24f));
        matWood = GetMat("Wood", new Color(0.5f, 0.33f, 0.18f));
        matWoodDark = GetMat("WoodDark", new Color(0.33f, 0.21f, 0.12f));
        matStone = GetMat("Stone", new Color(0.5f, 0.5f, 0.52f));
        matTent = GetMat("Tent", new Color(0.2f, 0.45f, 0.6f));
        matTentDark = GetMat("TentDark", new Color(0.13f, 0.31f, 0.43f));
        matMetal = GetMat("Metal", new Color(0.72f, 0.73f, 0.75f));
        matDark = GetMat("Dark", new Color(0.13f, 0.13f, 0.14f));
        matFlame = GetMat("Flame", new Color(1f, 0.55f, 0.15f));
        matEmber = GetMat("Ember", new Color(1f, 0.83f, 0.4f));
        matLeaf = GetMat("Leaf", new Color(0.22f, 0.45f, 0.2f));
        matLeafDark = GetMat("LeafDark", new Color(0.13f, 0.31f, 0.15f));
        matLeafLight = GetMat("LeafLight", new Color(0.36f, 0.58f, 0.26f));
        matAccent = GetMat("Accent", new Color(0.8f, 0.3f, 0.2f));
        matWater = GetMat("Water", new Color(0.22f, 0.45f, 0.58f));
        matRope = GetMat("Rope", new Color(0.72f, 0.66f, 0.5f));

        // 바베큐 재료와 조리 도구
        matMeat = GetMat("Meat", new Color(0.78f, 0.27f, 0.28f));
        matFat = GetMat("Fat", new Color(0.94f, 0.88f, 0.82f));
        matCorn = GetMat("Corn", new Color(0.96f, 0.79f, 0.24f));
        matHusk = GetMat("Husk", new Color(0.44f, 0.66f, 0.28f));
        matGrill = GetMat("Grill", new Color(0.24f, 0.25f, 0.27f));
        matCoal = GetMat("Coal", new Color(0.95f, 0.35f, 0.1f));
    }

    private static Material GetMat(string name, Color color) => BuilderMaterials.Ensure("Camp_" + name, color);
}
#endif
