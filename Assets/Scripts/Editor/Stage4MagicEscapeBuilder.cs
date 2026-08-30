#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 원작 마검탈출맵의 탐색·협동·희소 마검·배신 구조를 검증하는 신규 스테이지를 절차 생성한다.
/// 기존 Stage3 성채는 회귀 비교용으로 보존한다.
/// </summary>
public static class Stage4MagicEscapeBuilder
{
    public const string ScenePath = "Assets/Scenes/Stage4_MagicSwordEscape.unity";
    private const string TitlePath = "Assets/Scenes/MainTitle.unity";
    private const string LobbyPath = "Assets/Scenes/Lobby.unity";
    private const string Stage3Path = "Assets/Scenes/Stage3_CursedFortress.unity";
    private const string CampPath = "Assets/Scenes/Stage2_Campground.unity";

    private static Material stone, dark, floor, iron, parchment, purple, cyan, red, gold, green, voidMat;

    [MenuItem("Tools/Stage/Build Stage 4 (Magic Sword Escape)")]
    public static void BuildStage4()
    {
        if (File.Exists(ScenePath) && !Application.isBatchMode &&
            !EditorUtility.DisplayDialog("마검 탈출맵 다시 만들기", "기존 Stage4 씬을 새 설계로 다시 만듭니다.", "새로 만들기", "취소"))
            return;
        BuildInternal(false);
    }

    public static void BuildStage4Batch()
    {
        BuildInternal(true);
        EditorApplication.Exit(0);
    }

    private static void BuildInternal(bool batch)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        LoadMaterials();
        AddLighting();

        Transform root = new GameObject("MagicSwordEscapeSanctum").transform;
        BuildArchitecture(root);
        BuildSpawn(root);
        BuildHiddenSwitchMaze(root);
        BuildSplitCipher(root);
        BuildTraversalAndPressure(root);
        BuildTwinLeverLabyrinth(root);
        BuildCursedVault(root);
        BuildFinalEscape(root);
        AddStoryDetails(root);
        AddAtmosphere(root);
        AddSpawns(new Vector3(0f, 0.1f, -50f));

        var managerObject = new GameObject("MagicEscapeGameManager");
        managerObject.AddComponent<NetworkIdentity>();
        managerObject.AddComponent<MagicEscapeGameManager>();
        managerObject.AddComponent<MagicEscapeHud>();

        PlaceScarceWeapons(root);
        NetworkPhase4Setup.SetupWeaponSync();

        EditorSceneManager.SaveScene(scene, ScenePath);
        SetBuildOrder();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Stage4] 마검 탈출맵 생성 완료 — 숨은 스위치 → 분산 룬 → 이동/발판 → 쌍레버 → 희소 마검/봉인 → 전원 탈출");
    }

    private static void BuildArchitecture(Transform root)
    {
        Transform shell = Group(root, "Architecture", Vector3.zero);
        Box(shell, "Foundation", new Vector3(0f, -0.7f, 10f), new Vector3(42f, 1.4f, 130f), floor);
        Box(shell, "WestWall", new Vector3(-20.5f, 3.5f, 10f), new Vector3(1f, 7f, 130f), dark);
        Box(shell, "EastWall", new Vector3(20.5f, 3.5f, 10f), new Vector3(1f, 7f, 130f), dark);
        Box(shell, "StartWall", new Vector3(0f, 3.5f, -55f), new Vector3(42f, 7f, 1f), dark);
        Box(shell, "EndWall", new Vector3(0f, 3.5f, 75f), new Vector3(42f, 7f, 1f), dark);

        GateWall(shell, "SearchGate", -30f, MagicEscapePhase.SplitCipher, purple);
        GateWall(shell, "CipherGate", -10f, MagicEscapePhase.Counterweights, cyan);
        GateWall(shell, "BalanceGate", 14f, MagicEscapePhase.TwinLevers, gold);
        GateWall(shell, "LeverGate", 37f, MagicEscapePhase.SealBreaking, red);
        GateWall(shell, "SealGate", 60f, MagicEscapePhase.RallyEscape, green);
    }

    private static void GateWall(Transform parent, string name, float z, MagicEscapePhase phase, Material accent)
    {
        Transform group = Group(parent, name, Vector3.zero);
        Box(group, "WestPier", new Vector3(-12f, 3f, z), new Vector3(16f, 6f, 1f), stone);
        Box(group, "EastPier", new Vector3(12f, 3f, z), new Vector3(16f, 6f, 1f), stone);
        GameObject door = Box(group, "Door", new Vector3(0f, 2.7f, z), new Vector3(8f, 5.4f, 0.8f), iron);
        door.AddComponent<MagicEscapeGate>().Configure(phase, new Vector3(0f, 5.7f, 0f));
        Box(group, "Accent", new Vector3(0f, 3.2f, z - 0.45f), new Vector3(7.2f, 0.2f, 0.12f), accent, false);
    }

    private static void BuildSpawn(Transform root)
    {
        Transform group = Group(root, "Entrance", Vector3.zero);
        Sign(group, "Mission", new Vector3(0f, 0f, -52f), 0f,
            "마검 탈출 · 숨겨진 성소\n협동해야 나가지만 마검은 모두의 것이 아니다");
        Box(group, "EntryPath", new Vector3(0f, 0.04f, -45f), new Vector3(10f, 0.08f, 18f), stone, false);
    }

    private static void BuildHiddenSwitchMaze(Transform root)
    {
        Transform group = Group(root, "Puzzle01_HiddenSwitchMaze", Vector3.zero);
        Sign(group, "Guide", new Vector3(0f, 0f, -40f), 180f,
            "첫째 방 · 숨은 손길\n벽 뒤와 막다른 길의 봉인 스위치 세 개를 찾아라");

        // 시야를 꺾는 짧은 탐색 미로. 모든 길은 되돌아올 수 있어 진행이 막히지 않는다.
        Box(group, "MazeA", new Vector3(-7f, 2f, -37f), new Vector3(1f, 4f, 10f), stone);
        Box(group, "MazeB", new Vector3(7f, 2f, -39f), new Vector3(1f, 4f, 8f), stone);
        Box(group, "MazeC", new Vector3(-13f, 2f, -33f), new Vector3(10f, 4f, 1f), stone);
        Box(group, "MazeD", new Vector3(13f, 2f, -35f), new Vector3(10f, 4f, 1f), stone);
        Box(group, "MazeE", new Vector3(0f, 2f, -34f), new Vector3(8f, 4f, 1f), stone);
        Box(group, "MazeF", new Vector3(-12f, 2f, -43f), new Vector3(10f, 4f, 1f), stone);
        Box(group, "MazeG", new Vector3(11f, 2f, -46f), new Vector3(1f, 4f, 9f), stone);

        HiddenSwitch(group, 0, new Vector3(-16f, 0f, -37.5f), 90f, purple);
        HiddenSwitch(group, 1, new Vector3(16f, 0f, -40f), -90f, cyan);
        HiddenSwitch(group, 2, new Vector3(0f, 0f, -31.4f), 180f, gold);
        HiddenSwitch(group, 3, new Vector3(-16f, 0f, -47f), 90f, red);
        HiddenSwitch(group, 4, new Vector3(15f, 0f, -50f), -90f, green);
    }

    private static void HiddenSwitch(Transform parent, int index, Vector3 pos, float rotY, Material accent)
    {
        Transform group = Group(parent, "HiddenSwitch_" + index, pos, rotY);
        Box(group, "Mount", new Vector3(0f, 1.2f, 0f), new Vector3(1.3f, 2.2f, 0.5f), dark);
        GameObject indicator = Box(group, "Indicator", new Vector3(0f, 1.75f, -0.3f), new Vector3(0.55f, 0.35f, 0.08f), accent, false);
        GameObject handle = Cylinder(group, "Handle", new Vector3(0f, 1.1f, -0.45f), new Vector3(0.16f, 1.1f, 0.16f), iron);
        group.gameObject.AddComponent<MagicEscapeSwitch>().Configure(index, handle.transform, indicator.GetComponent<Renderer>());
    }

    private static void BuildSplitCipher(Transform root)
    {
        Transform group = Group(root, "Puzzle02_SplitCipher", Vector3.zero);
        Sign(group, "Guide", new Vector3(0f, 0f, -27f), 180f,
            "둘째 방 · 갈라진 기록\n서로 볼 수 없는 양쪽 기록을 말로 합쳐라");
        Box(group, "SightDivider", new Vector3(0f, 2f, -20f), new Vector3(1f, 4f, 14f), stone);
        WallClue(group, new Vector3(-19.85f, 2.2f, -22f), 90f, "서쪽 기록\n첫째 달 · 둘째 불 · 다섯째 불");
        WallClue(group, new Vector3(19.85f, 2.2f, -16f), -90f, "동쪽 기록\n셋째 가시 · 넷째 눈 · 여섯째 달");

        string[] names = { "불", "눈", "달", "가시" };
        Material[] colors = { red, cyan, gold, green };
        Vector3[] positions =
        {
            new Vector3(-9f, 0f, -12.5f), new Vector3(-3f, 0f, -12.5f),
            new Vector3(3f, 0f, -12.5f), new Vector3(9f, 0f, -12.5f),
        };
        for (int i = 0; i < names.Length; i++) Rune(group, i, names[i], positions[i], colors[i]);
    }

    private static void WallClue(Transform parent, Vector3 pos, float rotY, string text)
    {
        Transform group = Group(parent, "SplitClue", pos, rotY);
        Box(group, "Tablet", Vector3.zero, new Vector3(7f, 3f, 0.25f), parchment);
        BuilderText.World(group, "Writing", new Vector3(0f, 0f, 0.15f), text,
            new Vector2(6.4f, 2.4f), BuilderText.SignInk, 2.4f);
    }

    private static void Rune(Transform parent, int index, string label, Vector3 pos, Material material)
    {
        Transform group = Group(parent, "Rune_" + label, pos);
        Cylinder(group, "Pedestal", new Vector3(0f, 0.65f, 0f), new Vector3(1.45f, 1.3f, 1.45f), dark);
        GameObject glow = Cylinder(group, "Glow", new Vector3(0f, 1.38f, 0f), new Vector3(1.1f, 0.16f, 1.1f), material);
        group.gameObject.AddComponent<MagicEscapeRune>().Configure(index, label, glow.GetComponent<Renderer>());
        BuilderText.World(group, "Label", new Vector3(0f, 1.9f, -0.6f), label,
            new Vector2(1.4f, 0.6f), Color.white, 2f);
    }

    private static void BuildTraversalAndPressure(Transform root)
    {
        Transform group = Group(root, "Puzzle03_TraversalBalance", Vector3.zero);
        Sign(group, "Guide", new Vector3(0f, 0f, -7f), 180f,
            "셋째 방 · 무너진 다리\n발판까지 건너가 서로 다른 사람이 균형을 유지하라");

        // 네트워크에서 억울한 추락을 줄이기 위해 짧고 넓은 발판으로 구성한다.
        Box(group, "Void", new Vector3(0f, 0.03f, 0f), new Vector3(34f, 0.04f, 11f), voidMat, false);
        Vector3[] stones =
        {
            new Vector3(-10f, 0.35f, -3f), new Vector3(-5f, 0.55f, 0f), new Vector3(0f, 0.35f, 3f),
            new Vector3(5f, 0.55f, 0f), new Vector3(10f, 0.35f, -3f),
        };
        for (int i = 0; i < stones.Length; i++)
            Box(group, "BridgeStone_" + i, stones[i], new Vector3(4.2f, 0.7f, 4.2f), stone);

        PressurePlate(group, 0, new Vector3(-12f, 0.12f, 9f), purple);
        PressurePlate(group, 1, new Vector3(12f, 0.12f, 9f), cyan);
    }

    private static void PressurePlate(Transform parent, int index, Vector3 pos, Material accent)
    {
        Transform group = Group(parent, "PressurePlate_" + index, pos);
        Cylinder(group, "Base", Vector3.zero, new Vector3(3.5f, 0.14f, 3.5f), dark);
        GameObject visual = Cylinder(group, "PressedVisual", new Vector3(0f, 0.11f, 0f), new Vector3(2.9f, 0.12f, 2.9f), accent);
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        BoxCollider trigger = group.gameObject.AddComponent<BoxCollider>();
        trigger.center = new Vector3(0f, 0.8f, 0f);
        trigger.size = new Vector3(3.8f, 1.8f, 3.8f);
        trigger.isTrigger = true;
        group.gameObject.AddComponent<MagicEscapePressurePlate>().Configure(index, visual.transform);
    }

    private static void BuildTwinLeverLabyrinth(Transform root)
    {
        Transform group = Group(root, "Puzzle04_TwinLeverLabyrinth", Vector3.zero);
        Sign(group, "Guide", new Vector3(0f, 0f, 17f), 180f,
            "넷째 방 · 쌍둥이 심장\n갈라진 길 끝에서 7초 안에 두 레버를 당겨라");
        Box(group, "CenterDivider", new Vector3(0f, 2f, 26f), new Vector3(1f, 4f, 20f), stone);
        Box(group, "WestTurn", new Vector3(-10f, 2f, 24f), new Vector3(12f, 4f, 1f), stone);
        Box(group, "EastTurn", new Vector3(10f, 2f, 29f), new Vector3(12f, 4f, 1f), stone);
        Lever(group, 0, new Vector3(-16f, 0f, 33f), 90f, purple);
        Lever(group, 1, new Vector3(16f, 0f, 33f), -90f, cyan);
        Sign(group, "WestCallout", new Vector3(-14f, 0f, 19f), 180f, "서쪽 길\n상대 목소리를 들어라");
        Sign(group, "EastCallout", new Vector3(14f, 0f, 19f), 180f, "동쪽 길\n준비되면 외쳐라");
    }

    private static void Lever(Transform parent, int index, Vector3 pos, float rotY, Material accent)
    {
        Transform group = Group(parent, "Lever_" + index, pos, rotY);
        Box(group, "Mount", new Vector3(0f, 1.1f, 0f), new Vector3(1.5f, 2.2f, 0.6f), dark);
        Box(group, "Glow", new Vector3(0f, 1.1f, -0.34f), new Vector3(0.8f, 0.8f, 0.08f), accent, false);
        GameObject handle = Cylinder(group, "Handle", new Vector3(0f, 1.35f, -0.55f), new Vector3(0.18f, 1.4f, 0.18f), iron);
        group.gameObject.AddComponent<MagicEscapeLever>().Configure(index, handle.transform);
    }

    private static void BuildCursedVault(Transform root)
    {
        Transform group = Group(root, "Puzzle05_CursedVault", Vector3.zero);
        Sign(group, "Guide", new Vector3(0f, 0f, 40f), 180f,
            "다섯째 방 · 마검의 선택\n숨은 무기 둘로 네 봉인을 깨라. 차지한 자를 믿을 것인가");
        Box(group, "WestCacheScreen", new Vector3(-11f, 2f, 43f), new Vector3(1f, 4f, 10f), stone);
        Box(group, "EastCacheScreen", new Vector3(11f, 2f, 47f), new Vector3(1f, 4f, 10f), stone);
        Sign(group, "WeaponHint", new Vector3(0f, 0f, 45f), 180f,
            "봉인은 맨손으로 깨지지 않는다\n구석의 숨은 보관실을 수색하라");

        Seal(group, 0, new Vector3(-12f, 1.2f, 55f), purple);
        Seal(group, 1, new Vector3(-4f, 1.2f, 57f), red);
        Seal(group, 2, new Vector3(4f, 1.2f, 57f), cyan);
        Seal(group, 3, new Vector3(12f, 1.2f, 55f), green);
    }

    private static void Seal(Transform parent, int index, Vector3 pos, Material material)
    {
        Transform group = Group(parent, "SealCore_" + index, pos);
        GameObject crystal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        crystal.name = "OuterCrystal";
        crystal.transform.SetParent(group, false);
        crystal.transform.localScale = new Vector3(1.6f, 2.2f, 1.6f);
        crystal.GetComponent<Renderer>().sharedMaterial = material;
        var renderers = new List<Renderer> { crystal.GetComponent<Renderer>() };
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI * 0.25f;
            GameObject segment = Box(group, "Ring_" + i,
                new Vector3(Mathf.Cos(angle) * 1.45f, Mathf.Sin(angle) * 1.45f, 0f),
                new Vector3(0.9f, 0.16f, 0.16f), gold, false);
            segment.transform.localRotation = Quaternion.Euler(0f, 0f, i * 45f + 90f);
            renderers.Add(segment.GetComponent<Renderer>());
        }
        group.gameObject.AddComponent<MagicEscapeSeal>().Configure(index, renderers.ToArray());
        PointLight(group, Vector3.zero, material.color, 4f, 3.5f);
    }

    private static void PlaceScarceWeapons(Transform parent)
    {
        Transform group = Group(parent, "HiddenMagicWeapons", Vector3.zero);
        string[] weapons = { "Frozen_Tuna", "Whisk_Axe" };
        Vector3[] positions = { new Vector3(-16f, 1f, 46f), new Vector3(16f, 1f, 50f) };
        for (int i = 0; i < weapons.Length; i++)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponPrefabBuilder.PrefabPath(weapons[i]));
            if (prefab == null)
            {
                Debug.LogWarning("[Stage4] 마검 프리팹을 찾지 못했습니다: " + weapons[i]);
                continue;
            }
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
            Weapon weapon = instance.GetComponent<Weapon>();
            instance.transform.position = positions[i] + Vector3.up * (weapon != null ? weapon.groundOffset : 0f);
            instance.transform.rotation = Quaternion.Euler(0f, i == 0 ? 35f : -35f, 0f);
        }
    }

    private static void BuildFinalEscape(Transform root)
    {
        Transform group = Group(root, "Final_RallyEscape", Vector3.zero);
        Sign(group, "Guide", new Vector3(0f, 0f, 63f), 180f,
            "마지막 방 · 배신의 출구\n마검을 경계하되 한 명도 버리지 말고 원 안에 모여라");
        Cylinder(group, "PortalBase", new Vector3(0f, 0.06f, 70f), new Vector3(9f, 0.12f, 9f), dark);
        GameObject glow = Cylinder(group, "PortalGlow", new Vector3(0f, 0.14f, 70f), new Vector3(8.2f, 0.08f, 8.2f), green);
        Object.DestroyImmediate(glow.GetComponent<Collider>());
        GameObject triggerObject = new GameObject("RallyTrigger");
        triggerObject.transform.SetParent(group, false);
        triggerObject.transform.localPosition = new Vector3(0f, 1f, 70f);
        BoxCollider trigger = triggerObject.AddComponent<BoxCollider>();
        trigger.size = new Vector3(8.8f, 2.5f, 8.8f);
        trigger.isTrigger = true;
        triggerObject.AddComponent<MagicEscapeRallyZone>();
        PointLight(group, new Vector3(0f, 2f, 70f), green.color, 9f, 5f);
    }

    /// <summary>빈 복도처럼 보이지 않도록 각 방에 가짜 단서·제단·붕괴 흔적을 분산 배치한다.</summary>
    private static void AddStoryDetails(Transform root)
    {
        Transform group = Group(root, "StoryDetails", Vector3.zero);

        Vector3[] rubble =
        {
            new Vector3(-17f, 0.3f, -27f), new Vector3(17f, 0.25f, -8f),
            new Vector3(-17f, 0.25f, 13f), new Vector3(17f, 0.3f, 38f),
            new Vector3(-17f, 0.2f, 61f),
        };
        for (int i = 0; i < rubble.Length; i++)
        {
            GameObject chunk = Box(group, "Rubble_" + i, rubble[i],
                new Vector3(1.8f, 0.55f, 1.2f), i % 2 == 0 ? stone : dark);
            chunk.transform.localRotation = Quaternion.Euler(i * 7f, 24f + i * 41f, i * 5f);
        }

        int[] archZ = { -28, -9, 15, 38, 61 };
        foreach (int z in archZ)
        {
            Box(group, "ArchWest", new Vector3(-18.5f, 2.8f, z), new Vector3(3f, 5.6f, 0.55f), stone);
            Box(group, "ArchEast", new Vector3(18.5f, 2.8f, z), new Vector3(3f, 5.6f, 0.55f), stone);
            Box(group, "ArchLintel", new Vector3(0f, 5.3f, z), new Vector3(34f, 0.55f, 0.55f), dark);
        }

        Transform fakeVaults = Group(group, "FalseWeaponCaches", Vector3.zero);
        Vector3[] caches =
        {
            new Vector3(-16f, 0.7f, 43f), new Vector3(16f, 0.7f, 43f),
            new Vector3(-15f, 0.7f, 52f), new Vector3(15f, 0.7f, 52f),
        };
        for (int i = 0; i < caches.Length; i++)
        {
            Box(fakeVaults, "Chest_" + i, caches[i], new Vector3(2.4f, 1.4f, 1.5f), dark);
            Box(fakeVaults, "Mark_" + i, caches[i] + new Vector3(0f, 0.3f, -0.78f),
                new Vector3(0.35f, 0.35f, 0.06f), i % 2 == 0 ? purple : red, false);
        }

        for (int side = -1; side <= 1; side += 2)
        {
            Transform statue = Group(group, side < 0 ? "MoonKeeper" : "SunKeeper",
                new Vector3(side * 16f, 0f, 24f));
            Cylinder(statue, "Plinth", new Vector3(0f, 0.5f, 0f), new Vector3(2.4f, 1f, 2.4f), dark);
            Box(statue, "Body", new Vector3(0f, 2f, 0f), new Vector3(1.1f, 2.6f, 0.8f), stone);
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(statue, false);
            head.transform.localPosition = new Vector3(0f, 3.7f, 0f);
            head.transform.localScale = Vector3.one * 0.9f;
            head.GetComponent<Renderer>().sharedMaterial = side < 0 ? cyan : gold;
        }
    }

    private static void AddAtmosphere(Transform root)
    {
        Transform group = Group(root, "Atmosphere", Vector3.zero);
        for (int z = -50; z <= 70; z += 10)
        {
            Torch(group, new Vector3(-19.8f, 2.3f, z));
            Torch(group, new Vector3(19.8f, 2.3f, z));
        }
    }

    private static void Torch(Transform parent, Vector3 pos)
    {
        Transform group = Group(parent, "Torch", pos);
        Box(group, "Bracket", Vector3.zero, new Vector3(0.14f, 1.1f, 0.14f), iron, false);
        GameObject flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flame.name = "Flame";
        flame.transform.SetParent(group, false);
        flame.transform.localPosition = new Vector3(0f, 0.7f, 0f);
        flame.transform.localScale = new Vector3(0.35f, 0.65f, 0.35f);
        flame.GetComponent<Renderer>().sharedMaterial = purple;
        Object.DestroyImmediate(flame.GetComponent<Collider>());
        PointLight(group, new Vector3(0f, 0.7f, 0f), new Color(0.55f, 0.25f, 1f), 5f, 2f);
    }

    private static void Sign(Transform parent, string name, Vector3 pos, float rotY, string text)
    {
        Transform group = Group(parent, name, pos, rotY);
        Box(group, "Post", new Vector3(0f, 1.1f, 0.08f), new Vector3(0.14f, 2.2f, 0.14f), iron);
        Box(group, "Board", new Vector3(0f, 2.15f, 0f), new Vector3(8.6f, 1.65f, 0.18f), parchment);
        BuilderText.World(group, "Text", new Vector3(0f, 2.15f, 0.105f), text,
            new Vector2(8f, 1.35f), BuilderText.SignInk, 2.25f);
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
        var sunObject = new GameObject("Cursed Moon");
        sunObject.transform.rotation = Quaternion.Euler(52f, -28f, 0f);
        Light sun = sunObject.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(0.33f, 0.4f, 0.68f);
        sun.intensity = 0.68f;
        sun.shadows = LightShadows.Soft;
        RenderSettings.sun = sun;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.045f, 0.05f, 0.12f);
        RenderSettings.ambientEquatorColor = new Color(0.09f, 0.06f, 0.14f);
        RenderSettings.ambientGroundColor = new Color(0.02f, 0.015f, 0.03f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.045f, 0.03f, 0.075f);
        RenderSettings.fogStartDistance = 20f;
        RenderSettings.fogEndDistance = 72f;
    }

    private static void PointLight(Transform parent, Vector3 position, Color color, float range, float intensity)
    {
        GameObject lightObject = new GameObject("PointLight");
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.localPosition = position;
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = range;
        light.intensity = intensity;
        light.shadows = LightShadows.None;
    }

    private static void LoadMaterials()
    {
        stone = BuilderMaterials.Ensure("MagicEscape_Stone", new Color(0.24f, 0.25f, 0.33f));
        dark = BuilderMaterials.Ensure("MagicEscape_Dark", new Color(0.09f, 0.08f, 0.14f));
        floor = BuilderMaterials.Ensure("MagicEscape_Floor", new Color(0.14f, 0.14f, 0.2f));
        iron = BuilderMaterials.Ensure("MagicEscape_Iron", new Color(0.15f, 0.16f, 0.2f));
        parchment = BuilderMaterials.Ensure("MagicEscape_Parchment", new Color(0.68f, 0.55f, 0.36f));
        purple = BuilderMaterials.Ensure("MagicEscape_Purple", new Color(0.56f, 0.16f, 0.95f));
        cyan = BuilderMaterials.Ensure("MagicEscape_Cyan", new Color(0.12f, 0.78f, 0.95f));
        red = BuilderMaterials.Ensure("MagicEscape_Red", new Color(0.92f, 0.14f, 0.23f));
        gold = BuilderMaterials.Ensure("MagicEscape_Gold", new Color(0.95f, 0.62f, 0.12f));
        green = BuilderMaterials.Ensure("MagicEscape_Green", new Color(0.12f, 0.92f, 0.52f));
        voidMat = BuilderMaterials.Ensure("MagicEscape_Void", new Color(0.035f, 0.01f, 0.07f));
    }

    private static void SetBuildOrder()
    {
        string[] paths = { TitlePath, LobbyPath, Stage3Path, ScenePath, CampPath };
        EditorBuildSettings.scenes = paths.Where(File.Exists)
            .Select(path => new EditorBuildSettingsScene(path, true)).ToArray();
    }

    private static Transform Group(Transform parent, string name, Vector3 pos, float rotY = 0f)
    {
        Transform group = new GameObject(name).transform;
        group.SetParent(parent, false);
        group.localPosition = pos;
        group.localRotation = Quaternion.Euler(0f, rotY, 0f);
        return group;
    }

    private static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material material, bool collider = true)
    {
        GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube);
        item.name = name;
        item.transform.SetParent(parent, false);
        item.transform.localPosition = pos;
        item.transform.localScale = size;
        item.GetComponent<Renderer>().sharedMaterial = material;
        if (!collider) Object.DestroyImmediate(item.GetComponent<Collider>());
        return item;
    }

    private static GameObject Cylinder(Transform parent, string name, Vector3 pos, Vector3 size, Material material)
    {
        GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        item.name = name;
        item.transform.SetParent(parent, false);
        item.transform.localPosition = pos;
        item.transform.localScale = new Vector3(size.x, size.y * 0.5f, size.z);
        item.GetComponent<Renderer>().sharedMaterial = material;
        return item;
    }
}
#endif
