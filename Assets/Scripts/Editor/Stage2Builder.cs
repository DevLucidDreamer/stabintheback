#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 두 번째 스테이지 "캠핑장"을 생성하는 에디터 도구 (Phase 6).
/// 새 씬을 만들고 캠핑장 환경 + 스폰포인트 + 준비물/마검 + 매니저 + 탈출존까지 구성한다.
/// 메뉴: Tools > Stage > Build Stage 2 (Campground)
///
/// NetworkManager(NetworkBootstrap)는 Mirror에서 씬 전환 시 유지되므로 이 씬엔 넣지 않는다.
/// </summary>
public static class Stage2Builder
{
    private const string ScenePath = "Assets/Scenes/Stage2_Campground.unity";

    private static Material matGrass, matDirt, matWood, matStone, matTent, matMetal, matDark, matFlame, matLeaf, matAccent;

    [MenuItem("Tools/Stage/Build Stage 2 (Campground)")]
    public static void BuildStage2()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        LoadMaterials();

        AddSun();

        var root = new GameObject("Campground").transform;

        // 바닥 (윗면 y=0)
        Box(root, "Ground", new Vector3(0f, -0.5f, 0f), new Vector3(60f, 1f, 60f), matGrass);
        Box(root, "Path", new Vector3(0f, 0.03f, 6f), new Vector3(3f, 0.06f, 36f), matDirt);

        Campfire(root, new Vector3(0f, 0f, 0f));
        Tent(root, new Vector3(-8f, 0f, -3f), 20f, matTent);
        Tent(root, new Vector3(8f, 0f, -3f), -20f, matAccent);
        Tent(root, new Vector3(0f, 0f, 10f), 180f, matTent);
        PicnicTable(root, new Vector3(7f, 0f, 4f));
        Cooler(root, new Vector3(9f, 0f, 4.5f));

        // 나무들 (외곽)
        for (int i = 0; i < 10; i++)
        {
            float ang = i / 10f * Mathf.PI * 2f;
            Tree(root, new Vector3(Mathf.Cos(ang) * 24f, 0f, Mathf.Sin(ang) * 24f));
        }

        // 준비물 (캠핑장 세팅 목표)
        Collectible(root, "장작", new Vector3(2.2f, 0.15f, 1.2f), FirewoodModel);
        Collectible(root, "마시멜로", new Vector3(-2.2f, 0.1f, 1.0f), MarshmallowModel);
        Collectible(root, "부탄가스", new Vector3(7f, 0.85f, 3.6f), ButaneModel);
        Collectible(root, "랜턴", new Vector3(-8f, 0.5f, -3f), LanternModel);
        Collectible(root, "낚싯대", new Vector3(11f, 0.2f, 9f), FishingRodModel);

        // 마검 2자루 (캠핑장 버전)
        Axe(root, new Vector3(4f, 0.4f, -2f), 0f);
        Tongs(root, new Vector3(7f, 0.9f, 3.2f), 0f);

        // 스폰 포인트
        SpawnPoints(new Vector3(0f, 1f, -14f));

        // 매니저(체크리스트/무기) + ID 부여 — 기존 셋업 로직 재사용
        NetworkPhase3Setup.SetupChecklistSync();
        NetworkPhase4Setup.SetupWeaponSync();

        // 탈출존: 캠핑장 완료 시 시작 스테이지로 루프(마지막 스테이지)
        var exit = NetworkPhase6Setup.CreateEscapeZone(new Vector3(0f, 1.5f, 24f), new Vector3(4f, 3f, 3f), "", true, NetworkPhase6Setup.Stage1Scene);
        Box(exit.transform, "ExitSign", new Vector3(0f, 1.2f, 0.2f), new Vector3(3f, 0.05f, 0.05f), matWood);

        EditorSceneManager.SaveScene(scene, ScenePath);
        NetworkPhase6Setup.EnsureSceneInBuildSettings("Assets/Scenes/NetworkDemo.unity");
        NetworkPhase6Setup.EnsureSceneInBuildSettings(ScenePath);

        Debug.Log("[Stage2] 캠핑장 스테이지 생성 완료: " + ScenePath +
                  "\n- Stage1(NetworkDemo)에서 'Phase 6 > Setup Escape (Stage1 -> Stage2)'도 실행했는지 확인하세요.");
    }

    // ---------------------------------------------------------------- 환경 프롭

    private static void Campfire(Transform p, Vector3 pos)
    {
        Transform g = Group(p, "Campfire", pos, 0f);
        for (int i = 0; i < 8; i++)
        {
            float a = i / 8f * Mathf.PI * 2f;
            Box(g, "Stone", new Vector3(Mathf.Cos(a) * 0.9f, 0.12f, Mathf.Sin(a) * 0.9f), new Vector3(0.28f, 0.24f, 0.28f), matStone);
        }
        Prim(g, "Log1", PrimitiveType.Cylinder, new Vector3(0f, 0.12f, 0f), new Vector3(30f, 0f, 0f), new Vector3(0.16f, 0.5f, 0.16f), matWood);
        Prim(g, "Log2", PrimitiveType.Cylinder, new Vector3(0f, 0.12f, 0f), new Vector3(30f, 90f, 0f), new Vector3(0.16f, 0.5f, 0.16f), matWood);
        Prim(g, "Flame", PrimitiveType.Sphere, new Vector3(0f, 0.4f, 0f), new Vector3(0.4f, 0.6f, 0.4f), matFlame);

        // 모닥불 빛
        var lgo = new GameObject("FireLight");
        lgo.transform.SetParent(g, false);
        lgo.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        var l = lgo.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1f, 0.6f, 0.25f);
        l.range = 12f;
        l.intensity = 2.2f;
    }

    private static void Tent(Transform p, Vector3 pos, float rotY, Material mat)
    {
        Transform g = Group(p, "Tent", pos, rotY);
        Box(g, "SideL", new Vector3(-0.9f, 0.85f, 0f), new Vector3(0.08f, 1.9f, 2.6f), mat);
        Box(g, "SideR", new Vector3(0.9f, 0.85f, 0f), new Vector3(0.08f, 1.9f, 2.6f), mat);
        g.Find("SideL").localRotation = Quaternion.Euler(0f, 0f, 40f);
        g.Find("SideR").localRotation = Quaternion.Euler(0f, 0f, -40f);
        Box(g, "Back", new Vector3(0f, 0.7f, -1.3f), new Vector3(1.8f, 1.4f, 0.06f), mat);
        Box(g, "Floor", new Vector3(0f, 0.05f, 0f), new Vector3(2f, 0.1f, 2.6f), matDark);
    }

    private static void PicnicTable(Transform p, Vector3 pos)
    {
        Transform g = Group(p, "PicnicTable", pos, 0f);
        Box(g, "Top", new Vector3(0f, 0.75f, 0f), new Vector3(2f, 0.1f, 1f), matWood);
        Box(g, "BenchL", new Vector3(0f, 0.4f, 0.7f), new Vector3(2f, 0.08f, 0.3f), matWood);
        Box(g, "BenchR", new Vector3(0f, 0.4f, -0.7f), new Vector3(2f, 0.08f, 0.3f), matWood);
        Box(g, "LegL", new Vector3(-0.9f, 0.37f, 0f), new Vector3(0.1f, 0.75f, 0.9f), matWood);
        Box(g, "LegR", new Vector3(0.9f, 0.37f, 0f), new Vector3(0.1f, 0.75f, 0.9f), matWood);
    }

    private static void Cooler(Transform p, Vector3 pos)
    {
        Transform g = Group(p, "Cooler", pos, 0f);
        Box(g, "Body", new Vector3(0f, 0.3f, 0f), new Vector3(0.8f, 0.6f, 0.5f), matAccent);
        Box(g, "Lid", new Vector3(0f, 0.63f, 0f), new Vector3(0.84f, 0.08f, 0.54f), matDark);
    }

    private static void Tree(Transform p, Vector3 pos)
    {
        Transform g = Group(p, "Tree", pos, 0f);
        Prim(g, "Trunk", PrimitiveType.Cylinder, new Vector3(0f, 1.4f, 0f), new Vector3(0.5f, 1.4f, 0.5f), matWood);
        Prim(g, "Leaves", PrimitiveType.Sphere, new Vector3(0f, 3.2f, 0f), new Vector3(2.6f, 2.6f, 2.6f), matLeaf);
    }

    // ---------------------------------------------------------------- 준비물 모델

    private static void FirewoodModel(Transform g)
    {
        Prim(g, "Log1", PrimitiveType.Cylinder, new Vector3(0f, 0.08f, 0f), new Vector3(90f, 0f, 0f), new Vector3(0.12f, 0.35f, 0.12f), matWood);
        Prim(g, "Log2", PrimitiveType.Cylinder, new Vector3(0.14f, 0.08f, 0f), new Vector3(90f, 0f, 0f), new Vector3(0.12f, 0.35f, 0.12f), matWood);
        Prim(g, "Log3", PrimitiveType.Cylinder, new Vector3(0.07f, 0.22f, 0f), new Vector3(90f, 0f, 0f), new Vector3(0.12f, 0.35f, 0.12f), matWood);
    }

    private static void MarshmallowModel(Transform g)
    {
        Box(g, "Bag", new Vector3(0f, 0.12f, 0f), new Vector3(0.22f, 0.24f, 0.14f), matAccent);
    }

    private static void ButaneModel(Transform g)
    {
        Prim(g, "Can", PrimitiveType.Cylinder, new Vector3(0f, 0.1f, 0f), new Vector3(90f, 0f, 0f), new Vector3(0.1f, 0.14f, 0.1f), matMetal);
    }

    private static void LanternModel(Transform g)
    {
        Box(g, "Body", new Vector3(0f, 0.14f, 0f), new Vector3(0.18f, 0.28f, 0.18f), matMetal);
        Prim(g, "Glass", PrimitiveType.Sphere, new Vector3(0f, 0.14f, 0f), new Vector3(0.14f, 0.14f, 0.14f), matFlame);
    }

    private static void FishingRodModel(Transform g)
    {
        Prim(g, "Rod", PrimitiveType.Cylinder, new Vector3(0f, 0.05f, 0.6f), new Vector3(90f, 0f, 0f), new Vector3(0.03f, 0.7f, 0.03f), matDark);
        Box(g, "Reel", new Vector3(0f, 0.05f, -0.05f), new Vector3(0.1f, 0.1f, 0.08f), matMetal);
    }

    // ---------------------------------------------------------------- 마검(무기)

    private static void Axe(Transform p, Vector3 pos, float rotY)
    {
        Transform g = WeaponObj(p, "Axe", "도끼", pos, rotY, new Vector3(0f, -0.05f, 0f), new Vector3(-75f, 0f, 0f));
        Prim(g, "Handle", PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0f), new Vector3(90f, 0f, 0f), new Vector3(0.05f, 0.35f, 0.05f), matWood);
        Box(g, "Head", new Vector3(0f, 0.02f, 0.34f), new Vector3(0.05f, 0.18f, 0.16f), matMetal);
    }

    private static void Tongs(Transform p, Vector3 pos, float rotY)
    {
        Transform g = WeaponObj(p, "BBQTongs", "바비큐집게", pos, rotY, new Vector3(0f, -0.06f, -0.05f), new Vector3(-70f, 0f, 0f));
        Box(g, "ArmA", new Vector3(-0.03f, 0.02f, 0.05f), new Vector3(0.03f, 0.03f, 0.5f), matMetal);
        Box(g, "ArmB", new Vector3(0.03f, 0.02f, 0.05f), new Vector3(0.03f, 0.03f, 0.5f), matMetal);
    }

    // ---------------------------------------------------------------- 스폰 포인트

    private static void SpawnPoints(Vector3 basePos)
    {
        var root = new GameObject("NetworkSpawnPoints").transform;
        Vector3[] offsets =
        {
            Vector3.zero, new Vector3(2f, 0f, 0f), new Vector3(-2f, 0f, 0f), new Vector3(0f, 0f, 2f),
        };
        for (int i = 0; i < offsets.Length; i++)
        {
            var sp = new GameObject("Spawn_" + (char)('A' + i));
            sp.transform.SetParent(root, false);
            sp.transform.position = basePos + offsets[i];
            sp.AddComponent<Mirror.NetworkStartPosition>();
        }
    }

    // ---------------------------------------------------------------- 유틸

    private static void AddSun()
    {
        var go = new GameObject("Directional Light");
        go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        var l = go.AddComponent<Light>();
        l.type = LightType.Directional;
        l.color = new Color(1f, 0.96f, 0.85f);
        l.intensity = 1.1f;
        l.shadows = LightShadows.Soft;
    }

    private static Transform Collectible(Transform parent, string displayName, Vector3 pos, System.Action<Transform> model)
    {
        Transform g = Group(parent, displayName, pos, 0f);
        var c = g.gameObject.AddComponent<CollectibleItem>();
        c.SetDisplayName(displayName);
        model(g);
        return g;
    }

    private static Transform WeaponObj(Transform parent, string goName, string displayName, Vector3 pos, float rotY, Vector3 holdPos, Vector3 holdEuler)
    {
        Transform g = Group(parent, goName, pos, rotY);
        var w = g.gameObject.AddComponent<Weapon>();
        w.SetDisplayName(displayName);
        w.holdPosition = holdPos;
        w.holdEuler = holdEuler;
        return g;
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
        return go;
    }

    private static void LoadMaterials()
    {
        matGrass = GetMat("Grass", new Color(0.30f, 0.5f, 0.22f));
        matDirt = GetMat("Dirt", new Color(0.45f, 0.36f, 0.24f));
        matWood = GetMat("Wood", new Color(0.5f, 0.33f, 0.18f));
        matStone = GetMat("Stone", new Color(0.5f, 0.5f, 0.52f));
        matTent = GetMat("Tent", new Color(0.2f, 0.45f, 0.6f));
        matMetal = GetMat("Metal", new Color(0.72f, 0.73f, 0.75f));
        matDark = GetMat("Dark", new Color(0.13f, 0.13f, 0.14f));
        matFlame = GetMat("Flame", new Color(1f, 0.55f, 0.15f));
        matLeaf = GetMat("Leaf", new Color(0.22f, 0.45f, 0.2f));
        matAccent = GetMat("Accent", new Color(0.8f, 0.3f, 0.2f));
    }

    private static Material GetMat(string name, Color color)
    {
        const string dir = "Assets/Materials";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets", "Materials");

        string path = $"{dir}/Camp_{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        else mat.color = color;
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
#endif
