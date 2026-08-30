#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 협동 탈출맵 "저주받은 성채"를 생성한다.
/// 직선형 진행이지만 양 날개에 단서와 장치를 갈라 놓아 음성 소통과 역할 분담을 유도한다.
/// </summary>
public static class Stage3FortressBuilder
{
    public const string ScenePath = "Assets/Scenes/Stage3_CursedFortress.unity";
    private const string LobbyPath = "Assets/Scenes/Lobby.unity";
    private const string TitlePath = "Assets/Scenes/MainTitle.unity";
    private const string CampPath = "Assets/Scenes/Stage2_Campground.unity";
    private const string Stage4Path = "Assets/Scenes/Stage4_MagicSwordEscape.unity";

    private static Material stone, stoneDark, floor, iron, gold, purple, cyan, red, green, parchment;

    [MenuItem("Tools/Stage/Build Stage 3 (Cursed Fortress)")]
    public static void BuildStage3()
    {
        if (File.Exists(ScenePath) && !Application.isBatchMode &&
            !EditorUtility.DisplayDialog("성채 다시 만들기", "기존 성채 씬을 새 기획으로 다시 만듭니다.", "새로 만들기", "취소"))
            return;
        BuildInternal();
    }

    /// <summary>CI/명령행에서 확인 대화상자 없이 호출하는 진입점.</summary>
    public static void BuildStage3Batch()
    {
        BuildInternal();
        EditorApplication.Exit(0);
    }

    private static void BuildInternal()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        LoadMaterials();
        AddLighting();

        Transform root = new GameObject("CursedFortress").transform;
        BuildShell(root);
        BuildSpawnCourtyard(root);
        BuildCounterweights(root);
        BuildRuneGallery(root);
        BuildTwinLeverHall(root);
        BuildArmoryAndSeals(root);
        BuildEscapeSanctum(root);
        AddStoryDetails(root);
        AddDecor(root);
        AddSpawns(new Vector3(0f, 0.1f, -36f));

        var managerGo = new GameObject("FortressGameManager");
        managerGo.AddComponent<NetworkIdentity>();
        managerGo.AddComponent<FortressGameManager>();
        managerGo.AddComponent<FortressHud>();

        PlaceWeapons(root);
        NetworkPhase4Setup.SetupWeaponSync();

        EditorSceneManager.SaveScene(scene, ScenePath);
        SetBuildOrder();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Stage3] 저주받은 성채 생성 완료: " + ScenePath +
                  "\n압력판 → 분산 룬 단서 → 쌍레버 → 마검 봉인 → 전원 탈출");
    }

    private static void BuildShell(Transform root)
    {
        Transform shell = Group(root, "Architecture", Vector3.zero);
        Box(shell, "Foundation", new Vector3(0f, -0.65f, 4f), new Vector3(26f, 1.3f, 92f), floor);
        Box(shell, "WestOuterWall", new Vector3(-12.5f, 3.5f, 4f), new Vector3(1f, 7f, 92f), stoneDark);
        Box(shell, "EastOuterWall", new Vector3(12.5f, 3.5f, 4f), new Vector3(1f, 7f, 92f), stoneDark);
        Box(shell, "StartWall", new Vector3(0f, 3.5f, -42f), new Vector3(26f, 7f, 1f), stoneDark);
        Box(shell, "EndWall", new Vector3(0f, 3.5f, 50f), new Vector3(26f, 7f, 1f), stoneDark);

        // 방 경계를 바닥 무늬로 읽히게 해 플레이어가 목표 공간을 놓치지 않게 한다.
        for (int z = -38; z <= 46; z += 4)
            Box(shell, "FloorBand_" + z, new Vector3(0f, 0.015f, z), new Vector3(24f, 0.03f, 0.08f), stoneDark, false);

        // 진행 관문 3개. 중앙 문짝은 상태에 따라 위로 열린다.
        GateWall(shell, "CounterweightGate", -12f, FortressPhase.RuneCipher, purple);
        GateWall(shell, "RuneGate", 10f, FortressPhase.TwinLevers, cyan);
        GateWall(shell, "LeverGate", 27f, FortressPhase.SealBreaking, red);
        GateWall(shell, "SealGate", 40f, FortressPhase.RallyEscape, gold);
    }

    private static void GateWall(Transform parent, string name, float z, FortressPhase opensAt, Material accent)
    {
        Transform g = Group(parent, name, Vector3.zero);
        Box(g, "WestPier", new Vector3(-7.5f, 3f, z), new Vector3(10f, 6f, 1f), stone);
        Box(g, "EastPier", new Vector3(7.5f, 3f, z), new Vector3(10f, 6f, 1f), stone);
        Box(g, "Lintel", new Vector3(0f, 5.3f, z), new Vector3(5f, 1.4f, 1.2f), stoneDark);
        GameObject door = Box(g, "Portcullis", new Vector3(0f, 2.35f, z), new Vector3(4.6f, 4.7f, 0.42f), iron);
        door.AddComponent<FortressGate>().Configure(opensAt, new Vector3(0f, 5.4f, 0f));
        Box(g, "RuneBar", new Vector3(0f, 3.2f, z - 0.28f), new Vector3(3.8f, 0.18f, 0.12f), accent, false);
    }

    private static void BuildSpawnCourtyard(Transform root)
    {
        Transform g = Group(root, "SpawnCourtyard", Vector3.zero);
        Sign(g, "Mission", new Vector3(0f, 0f, -39.8f), 0f,
            "저주받은 성채\n혼자서는 어떤 문도 열리지 않는다");
        Box(g, "Path", new Vector3(0f, 0.04f, -31f), new Vector3(7f, 0.08f, 18f), stone, false);
        Pillar(g, new Vector3(-8.5f, 0f, -34f));
        Pillar(g, new Vector3(8.5f, 0f, -34f));
    }

    private static void BuildCounterweights(Transform root)
    {
        Transform g = Group(root, "Puzzle01_Counterweights", Vector3.zero);
        Sign(g, "Guide", new Vector3(0f, 0f, -26.5f), 180f,
            "첫 관문 · 균형의 사슬\n서로 다른 두 사람이 양쪽 판을 함께 눌러라");
        PressurePlate(g, 0, new Vector3(-5.2f, 0.12f, -18.5f), purple);
        PressurePlate(g, 1, new Vector3(5.2f, 0.12f, -18.5f), cyan);

        // 각 판 위 쇠사슬과 도르래가 반대편 문에 연결된 듯한 실루엣.
        for (int side = -1; side <= 1; side += 2)
        {
            Box(g, "Chain_" + side, new Vector3(side * 5.2f, 3.4f, -16f), new Vector3(0.16f, 6.5f, 0.16f), iron, false);
            Cylinder(g, "Weight_" + side, new Vector3(side * 5.2f, 0.85f, -15.8f), new Vector3(0.8f, 0.8f, 0.8f), iron);
        }
    }

    private static void PressurePlate(Transform parent, int index, Vector3 pos, Material accent)
    {
        Transform g = Group(parent, "PressurePlate_" + index, pos);
        Cylinder(g, "Base", Vector3.zero, new Vector3(3.4f, 0.14f, 3.4f), stoneDark);
        GameObject top = Cylinder(g, "PressedVisual", new Vector3(0f, 0.11f, 0f), new Vector3(2.8f, 0.12f, 2.8f), accent);
        Object.DestroyImmediate(top.GetComponent<Collider>());
        var trigger = g.gameObject.AddComponent<BoxCollider>();
        trigger.center = new Vector3(0f, 0.8f, 0f);
        trigger.size = new Vector3(3.6f, 1.8f, 3.6f);
        trigger.isTrigger = true;
        g.gameObject.AddComponent<CoopPressurePlate>().Configure(index, top.transform);
    }

    private static void BuildRuneGallery(Transform root)
    {
        Transform g = Group(root, "Puzzle02_RuneGallery", Vector3.zero);
        Sign(g, "Guide", new Vector3(0f, 0f, -9.3f), 180f,
            "둘째 관문 · 갈라진 예언\n양쪽 벽화의 반쪽 문장을 음성으로 맞춰라");

        // 한쪽만 보면 순서를 알 수 없다: 서쪽은 앞의 둘, 동쪽은 뒤의 둘을 알려 준다.
        WallClue(g, new Vector3(-11.85f, 2.2f, -2f), 90f, "서쪽 기록\n첫째 달 · 둘째 불 · 다섯째 불");
        WallClue(g, new Vector3(11.85f, 2.2f, 4f), -90f, "동쪽 기록\n셋째 가시 · 넷째 눈 · 여섯째 달");

        string[] names = { "불", "눈", "달", "가시" };
        Material[] colors = { red, cyan, gold, green };
        Vector3[] spots =
        {
            new Vector3(-5.4f, 0f, 4.5f), new Vector3(-1.8f, 0f, 4.5f),
            new Vector3(1.8f, 0f, 4.5f), new Vector3(5.4f, 0f, 4.5f),
        };
        for (int i = 0; i < names.Length; i++) Rune(g, i, names[i], spots[i], colors[i]);
    }

    private static void WallClue(Transform parent, Vector3 pos, float rotY, string text)
    {
        Transform g = Group(parent, "Clue", pos, rotY);
        Box(g, "Tablet", Vector3.zero, new Vector3(6f, 3.2f, 0.25f), parchment);
        BuilderText.World(g, "Writing", new Vector3(0f, 0f, 0.15f), text, new Vector2(5.5f, 2.7f), BuilderText.SignInk, 2.4f);
    }

    private static void Rune(Transform parent, int index, string name, Vector3 pos, Material mat)
    {
        Transform g = Group(parent, "Rune_" + name, pos);
        Cylinder(g, "Pedestal", new Vector3(0f, 0.65f, 0f), new Vector3(1.35f, 1.3f, 1.35f), stoneDark);
        GameObject glow = Cylinder(g, "RuneGlow", new Vector3(0f, 1.38f, 0f), new Vector3(1.05f, 0.16f, 1.05f), mat);
        g.gameObject.AddComponent<RunePedestal>().Configure(index, name, glow.GetComponent<Renderer>());
        BuilderText.World(g, "Label", new Vector3(0f, 1.9f, -0.58f), name, new Vector2(1.3f, 0.6f), Color.white, 2f);
    }

    private static void BuildTwinLeverHall(Transform root)
    {
        Transform g = Group(root, "Puzzle03_TwinLevers", Vector3.zero);
        Sign(g, "Guide", new Vector3(0f, 0f, 12f), 180f,
            "셋째 관문 · 쌍둥이 심장\n서로 다른 사람이 7초 안에 두 레버를 당겨라");

        // 가운데 장벽 때문에 한 사람이 두 레버를 바로 볼 수 없고 Vivox 위치 음성이 역할을 한다.
        Box(g, "Divider", new Vector3(0f, 1.8f, 20f), new Vector3(0.8f, 3.6f, 12f), stone);
        Lever(g, 0, new Vector3(-8.3f, 0f, 20.5f), 90f, purple);
        Lever(g, 1, new Vector3(8.3f, 0f, 20.5f), -90f, cyan);
        Sign(g, "WestCallout", new Vector3(-8f, 0f, 15f), 180f, "서쪽 레버\n준비되면 외쳐라");
        Sign(g, "EastCallout", new Vector3(8f, 0f, 15f), 180f, "동쪽 레버\n상대의 신호를 들어라");
    }

    private static void Lever(Transform parent, int index, Vector3 pos, float rotY, Material accent)
    {
        Transform g = Group(parent, "Lever_" + index, pos, rotY);
        Box(g, "Mount", new Vector3(0f, 1.1f, 0f), new Vector3(1.5f, 2.2f, 0.6f), stoneDark);
        Box(g, "Glow", new Vector3(0f, 1.1f, -0.34f), new Vector3(0.8f, 0.8f, 0.08f), accent, false);
        GameObject handle = Cylinder(g, "Handle", new Vector3(0f, 1.35f, -0.55f), new Vector3(0.18f, 1.4f, 0.18f), iron);
        g.gameObject.AddComponent<CoopLever>().Configure(index, handle.transform);
    }

    private static void BuildArmoryAndSeals(Transform root)
    {
        Transform g = Group(root, "Puzzle04_CursedArmory", Vector3.zero);
        Sign(g, "Guide", new Vector3(0f, 0f, 29f), 180f,
            "마검의 회랑\n측면 무기고를 수색해 네 봉인핵을 파괴하라");
        Sign(g, "Armory", new Vector3(-8.5f, 0f, 31f), 90f,
            "마검 무기고\n좌클릭 휘두르기 · G 내려놓기");

        Box(g, "WestVaultScreen", new Vector3(-7f, 2f, 33f), new Vector3(1f, 4f, 8f), stone);
        Box(g, "EastVaultScreen", new Vector3(7f, 2f, 33f), new Vector3(1f, 4f, 8f), stone);
        Seal(g, 0, new Vector3(-8f, 1.2f, 37f), purple);
        Seal(g, 1, new Vector3(-2.7f, 1.2f, 37.5f), red);
        Seal(g, 2, new Vector3(2.7f, 1.2f, 37.5f), cyan);
        Seal(g, 3, new Vector3(8f, 1.2f, 37f), green);
    }

    private static void Seal(Transform parent, int index, Vector3 pos, Material mat)
    {
        Transform g = Group(parent, "SealCore_" + index, pos);
        GameObject outer = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        outer.name = "OuterCrystal";
        outer.transform.SetParent(g, false);
        outer.transform.localScale = new Vector3(1.5f, 2.2f, 1.5f);
        outer.GetComponent<Renderer>().sharedMaterial = mat;
        var visuals = new List<Renderer> { outer.GetComponent<Renderer>() };
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI * 0.25f;
            GameObject segment = Box(g, "SealRing_" + i,
                new Vector3(Mathf.Cos(angle) * 1.35f, Mathf.Sin(angle) * 1.35f, 0f),
                new Vector3(0.85f, 0.16f, 0.16f), gold, false);
            segment.transform.localRotation = Quaternion.Euler(0f, 0f, i * 45f + 90f);
            visuals.Add(segment.GetComponent<Renderer>());
        }
        g.gameObject.AddComponent<CursedSeal>().Configure(index, visuals.ToArray());
        PointLight(g, new Vector3(0f, 0f, 0f), mat.color, 3.5f, 4f);
    }

    private static void BuildEscapeSanctum(Transform root)
    {
        Transform g = Group(root, "Final_RallyEscape", Vector3.zero);
        Sign(g, "Guide", new Vector3(0f, 0f, 42f), 180f,
            "생환의 진\n한 명도 남기지 말고 전원이 원 안에 모여라");
        Cylinder(g, "PortalBase", new Vector3(0f, 0.06f, 46f), new Vector3(8f, 0.12f, 8f), stoneDark);
        GameObject glow = Cylinder(g, "PortalGlow", new Vector3(0f, 0.14f, 46f), new Vector3(7.2f, 0.08f, 7.2f), green);
        Object.DestroyImmediate(glow.GetComponent<Collider>());
        var zone = new GameObject("RallyTrigger");
        zone.transform.SetParent(g, false);
        zone.transform.localPosition = new Vector3(0f, 1f, 46f);
        var collider = zone.AddComponent<BoxCollider>();
        collider.size = new Vector3(7.5f, 2.5f, 7.5f);
        collider.isTrigger = true;
        zone.AddComponent<CoopRallyZone>();
        PointLight(g, new Vector3(0f, 2f, 46f), green.color, 7f, 5f);
    }

    private static void PlaceWeapons(Transform parent)
    {
        Transform g = Group(parent, "CursedWeapons", Vector3.zero);
        string[] weapons = { "Carrot_Greatsword", "Whisk_Axe", "Pineapple_MorningStar", "Frozen_Tuna" };
        Vector3[] spots =
        {
            new Vector3(-10.2f, 1f, 31f), new Vector3(-9.2f, 1f, 35f),
            new Vector3(9.2f, 1f, 35f), new Vector3(10.2f, 1f, 31f),
        };
        for (int i = 0; i < weapons.Length; i++)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponPrefabBuilder.PrefabPath(weapons[i]));
            if (prefab == null) continue;
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, g);
            Weapon weapon = instance.GetComponent<Weapon>();
            float lift = weapon != null ? weapon.groundOffset : 0f;
            instance.transform.position = spots[i] + Vector3.up * lift;
            instance.transform.rotation = Quaternion.Euler(0f, i * 70f, 0f);
        }
    }

    /// <summary>긴 복도를 비워 두지 않고 성채의 사용 흔적과 측면 관심 지점을 만든다.</summary>
    private static void AddStoryDetails(Transform root)
    {
        Transform g = Group(root, "StoryDetails", Vector3.zero);

        int[] bannerZ = { -32, -8, 14, 31 };
        foreach (int z in bannerZ)
        {
            Box(g, "WestBanner", new Vector3(-11.92f, 3.4f, z), new Vector3(0.05f, 2.8f, 2.2f), purple, false);
            Box(g, "EastBanner", new Vector3(11.92f, 3.4f, z), new Vector3(0.05f, 2.8f, 2.2f), red, false);
        }

        Vector3[] rubble =
        {
            new Vector3(-9.5f, 0.25f, -28f), new Vector3(9f, 0.2f, -7f),
            new Vector3(-9f, 0.3f, 7f), new Vector3(9.4f, 0.25f, 25f),
            new Vector3(-10f, 0.2f, 41f),
        };
        for (int i = 0; i < rubble.Length; i++)
        {
            GameObject chunk = Box(g, "Rubble_" + i, rubble[i],
                new Vector3(1.2f + i * 0.08f, 0.5f, 0.9f), i % 2 == 0 ? stone : stoneDark);
            chunk.transform.localRotation = Quaternion.Euler(i * 9f, i * 31f, i * 6f);
        }

        for (int side = -1; side <= 1; side += 2)
        {
            Transform cell = Group(g, side < 0 ? "WestCell" : "EastCell", new Vector3(side * 10.2f, 0f, 2f));
            Box(cell, "Back", new Vector3(0f, 1.8f, 0f), new Vector3(3.5f, 3.6f, 0.35f), stoneDark);
            for (int i = -2; i <= 2; i++)
                Box(cell, "Bar", new Vector3(i * 0.6f, 1.6f, side * -0.3f), new Vector3(0.1f, 3.2f, 0.1f), iron);
            Box(cell, "Bench", new Vector3(0f, 0.35f, side * -0.8f), new Vector3(2.5f, 0.35f, 0.7f), stone);
        }

        Transform armory = Group(g, "ArmoryFurniture", Vector3.zero);
        foreach (float x in new[] { -10.6f, 10.6f })
        {
            Box(armory, "Rack", new Vector3(x, 1.2f, 32.5f), new Vector3(0.35f, 2.4f, 3.8f), iron);
            Box(armory, "Crate", new Vector3(x > 0f ? 8.8f : -8.8f, 0.55f, 29.5f),
                new Vector3(1.5f, 1.1f, 1.5f), stoneDark);
        }
    }

    private static void AddDecor(Transform root)
    {
        Transform g = Group(root, "Atmosphere", Vector3.zero);
        for (int z = -36; z <= 46; z += 8)
        {
            Torch(g, new Vector3(-11.8f, 2.2f, z));
            Torch(g, new Vector3(11.8f, 2.2f, z));
        }
        for (int z = -34; z <= 46; z += 10)
        {
            Pillar(g, new Vector3(-10.3f, 0f, z));
            Pillar(g, new Vector3(10.3f, 0f, z));
        }
    }

    private static void Torch(Transform parent, Vector3 pos)
    {
        Transform g = Group(parent, "Torch", pos);
        Box(g, "Bracket", Vector3.zero, new Vector3(0.14f, 1.1f, 0.14f), iron, false);
        GameObject flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flame.name = "Flame";
        flame.transform.SetParent(g, false);
        flame.transform.localPosition = new Vector3(0f, 0.7f, 0f);
        flame.transform.localScale = new Vector3(0.35f, 0.65f, 0.35f);
        flame.GetComponent<Renderer>().sharedMaterial = purple;
        Object.DestroyImmediate(flame.GetComponent<Collider>());
        PointLight(g, new Vector3(0f, 0.7f, 0f), new Color(0.55f, 0.3f, 1f), 5f, 2.2f);
    }

    private static void Pillar(Transform parent, Vector3 pos)
    {
        Transform g = Group(parent, "Pillar", pos);
        Box(g, "Base", new Vector3(0f, 0.3f, 0f), new Vector3(1.6f, 0.6f, 1.6f), stoneDark);
        Cylinder(g, "Shaft", new Vector3(0f, 2.1f, 0f), new Vector3(1.1f, 3.6f, 1.1f), stone);
        Box(g, "Cap", new Vector3(0f, 4.05f, 0f), new Vector3(1.7f, 0.35f, 1.7f), stoneDark);
    }

    private static void Sign(Transform parent, string name, Vector3 pos, float rotY, string text)
    {
        Transform g = Group(parent, name, pos, rotY);
        Box(g, "Posts", new Vector3(0f, 1.1f, 0.08f), new Vector3(0.14f, 2.2f, 0.14f), iron);
        Box(g, "Board", new Vector3(0f, 2.15f, 0f), new Vector3(6.8f, 1.55f, 0.18f), parchment);
        BuilderText.World(g, "Text", new Vector3(0f, 2.15f, 0.105f), text,
            new Vector2(6.3f, 1.25f), BuilderText.SignInk, 2.4f);
    }

    private static void AddSpawns(Vector3 origin)
    {
        Transform root = new GameObject("NetworkSpawnPoints").transform;
        for (int i = 0; i < 8; i++)
        {
            GameObject point = new GameObject("Spawn_" + (char)('A' + i));
            point.transform.SetParent(root, false);
            point.transform.position = origin + new Vector3((i % 4 - 1.5f) * 2f, 0f, (i / 4) * -2f);
            point.AddComponent<NetworkStartPosition>();
        }
    }

    private static void AddLighting()
    {
        var sunGo = new GameObject("Moon Light");
        sunGo.transform.rotation = Quaternion.Euler(52f, -28f, 0f);
        Light sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(0.38f, 0.46f, 0.7f);
        sun.intensity = 0.72f;
        sun.shadows = LightShadows.Soft;
        RenderSettings.sun = sun;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.07f, 0.08f, 0.16f);
        RenderSettings.ambientEquatorColor = new Color(0.11f, 0.09f, 0.16f);
        RenderSettings.ambientGroundColor = new Color(0.025f, 0.02f, 0.035f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.055f, 0.045f, 0.09f);
        RenderSettings.fogStartDistance = 18f;
        RenderSettings.fogEndDistance = 65f;
    }

    private static void PointLight(Transform parent, Vector3 localPos, Color color, float range, float intensity)
    {
        var go = new GameObject("PointLight");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        Light light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = range;
        light.intensity = intensity;
        light.shadows = LightShadows.None;
    }

    private static void SetBuildOrder()
    {
        var order = new List<string>();
        if (File.Exists(TitlePath)) order.Add(TitlePath);
        if (File.Exists(LobbyPath)) order.Add(LobbyPath);
        if (File.Exists(ScenePath)) order.Add(ScenePath);
        if (File.Exists(Stage4Path)) order.Add(Stage4Path);
        if (File.Exists(CampPath)) order.Add(CampPath); // 기존 캠핑장은 별도 플레이테스트용으로 보존.
        EditorBuildSettings.scenes = order.Select(p => new EditorBuildSettingsScene(p, true)).ToArray();
    }

    private static void LoadMaterials()
    {
        stone = BuilderMaterials.Ensure("Fortress_Stone", new Color(0.26f, 0.27f, 0.34f));
        stoneDark = BuilderMaterials.Ensure("Fortress_StoneDark", new Color(0.12f, 0.12f, 0.17f));
        floor = BuilderMaterials.Ensure("Fortress_Floor", new Color(0.17f, 0.18f, 0.23f));
        iron = BuilderMaterials.Ensure("Fortress_Iron", new Color(0.18f, 0.19f, 0.23f));
        gold = BuilderMaterials.Ensure("Fortress_Gold", new Color(0.9f, 0.63f, 0.18f));
        purple = BuilderMaterials.Ensure("Fortress_Curse", new Color(0.52f, 0.2f, 0.92f));
        cyan = BuilderMaterials.Ensure("Fortress_Cyan", new Color(0.16f, 0.75f, 0.95f));
        red = BuilderMaterials.Ensure("Fortress_Red", new Color(0.9f, 0.18f, 0.25f));
        green = BuilderMaterials.Ensure("Fortress_Escape", new Color(0.18f, 0.9f, 0.55f));
        parchment = BuilderMaterials.Ensure("Fortress_Parchment", new Color(0.7f, 0.58f, 0.4f));
    }

    private static Transform Group(Transform parent, string name, Vector3 pos, float rotY = 0f)
    {
        Transform g = new GameObject(name).transform;
        g.SetParent(parent, false);
        g.localPosition = pos;
        g.localRotation = Quaternion.Euler(0f, rotY, 0f);
        return g;
    }

    private static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, bool collider = true)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    private static GameObject Cylinder(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = new Vector3(size.x, size.y * 0.5f, size.z);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }
}
#endif
