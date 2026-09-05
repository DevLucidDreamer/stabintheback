#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Stage4를 폐쇄형 멀티플레이 맵 "지하 격리 연구동 B-13"으로 절차 생성한다.
/// 여섯 개의 반복되는 실험실 셀 안에서 탐색, 정보 공유, 동시 조작,
/// 희소 마검을 둘러싼 협동과 배신이 한 판의 흐름으로 이어진다.
/// </summary>
public static class Stage4MagicEscapeBuilder
{
    public const string ScenePath = "Assets/Scenes/Stage4_MagicSwordEscape.unity";
    private const string TitlePath = "Assets/Scenes/MainTitle.unity";
    private const string LobbyPath = "Assets/Scenes/Lobby.unity";
    private const string Stage3Path = "Assets/Scenes/Stage3_CursedFortress.unity";
    private const string CampPath = "Assets/Scenes/Stage2_Campground.unity";

    private const float RoomWidth = 30f;
    private const float RoomLength = 20f;
    private const float RoomHeight = 6.2f;

    private static Material concrete, concreteDark, floor, steel, steelDark, panel;
    private static Material white, amber, cyan, red, green, warning, glass, voidMat;

    [MenuItem("Tools/Stage/Build Stage 4 (B-13 Laboratory)")]
    public static void BuildStage4()
    {
        if (File.Exists(ScenePath) && !Application.isBatchMode &&
            !EditorUtility.DisplayDialog("B-13 연구동 다시 만들기",
                "기존 Stage4 씬을 회색 지하 연구시설 설계로 전면 교체합니다.", "새로 만들기", "취소"))
            return;
        BuildInternal();
    }

    public static void BuildStage4Batch()
    {
        BuildInternal();
        EditorApplication.Exit(0);
    }

    private static void BuildInternal()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        LoadMaterials();
        AddLighting();

        Transform root = new GameObject("B13_UndergroundContainmentLab").transform;
        BuildArchitecture(root);
        BuildEntrance(root);
        BuildQuarantineSearch(root);
        BuildObservationCipher(root);
        BuildSpecimenTransit(root);
        BuildControlRoom(root);
        BuildContainmentVault(root);
        BuildEmergencyLift(root);
        AddLaboratoryDetails(root);
        AddAtmosphere(root);
        AddSpawns(new Vector3(0f, 0.1f, -53f));

        var managerObject = new GameObject("B13GameManager");
        managerObject.AddComponent<NetworkIdentity>();
        managerObject.AddComponent<MagicEscapeGameManager>();
        managerObject.AddComponent<MagicEscapeHud>();

        PlaceScarceWeapons(root);
        NetworkPhase4Setup.SetupWeaponSync();

        EditorSceneManager.SaveScene(scene, ScenePath);
        SetBuildOrder();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Stage4] 지하 격리 연구동 B-13 생성 완료 — 검역 → 관찰 → 운송 → 제어 → 격리 → 비상 승강기");
    }

    private static void BuildArchitecture(Transform root)
    {
        Transform shell = Group(root, "Architecture");
        float[] centers = { -48f, -26f, -4f, 18f, 40f, 62f };
        string[] labels =
        {
            "B-01  QUARANTINE", "B-02  OBSERVATION", "B-03  TRANSIT",
            "B-04  CONTROL", "B-05  CONTAINMENT", "B-06  EVACUATION"
        };
        Material[] accents = { amber, cyan, warning, red, red, green };

        for (int i = 0; i < centers.Length; i++)
            ModuleShell(shell, "Cell_" + (i + 1), centers[i], labels[i], accents[i], i);

        Box(shell, "SouthBulkhead", new Vector3(0f, RoomHeight * 0.5f, -58.5f),
            new Vector3(RoomWidth + 1f, RoomHeight, 1f), concreteDark);
        Box(shell, "NorthBulkhead", new Vector3(0f, RoomHeight * 0.5f, 72.5f),
            new Vector3(RoomWidth + 1f, RoomHeight, 1f), concreteDark);

        Airlock(shell, "Airlock_B01_B02", -37f, MagicEscapePhase.SplitCipher, amber);
        Airlock(shell, "Airlock_B02_B03", -15f, MagicEscapePhase.Counterweights, cyan);
        Airlock(shell, "Airlock_B03_B04", 7f, MagicEscapePhase.TwinLevers, warning);
        Airlock(shell, "Airlock_B04_B05", 29f, MagicEscapePhase.SealBreaking, red);
        Airlock(shell, "Airlock_B05_B06", 51f, MagicEscapePhase.RallyEscape, red);
    }

    private static void ModuleShell(Transform parent, string name, float centerZ, string label,
        Material accent, int index)
    {
        Transform room = Group(parent, name);
        Box(room, "Floor", new Vector3(0f, -0.22f, centerZ),
            new Vector3(RoomWidth, 0.44f, RoomLength), floor);
        Box(room, "Ceiling", new Vector3(0f, RoomHeight + 0.2f, centerZ),
            new Vector3(RoomWidth, 0.4f, RoomLength), concreteDark);
        Box(room, "WestWall", new Vector3(-RoomWidth * 0.5f, RoomHeight * 0.5f, centerZ),
            new Vector3(0.55f, RoomHeight, RoomLength), concrete);
        Box(room, "EastWall", new Vector3(RoomWidth * 0.5f, RoomHeight * 0.5f, centerZ),
            new Vector3(0.55f, RoomHeight, RoomLength), concrete);

        for (int zOffset = -8; zOffset <= 8; zOffset += 4)
        {
            Box(room, "WestFrame", new Vector3(-14.66f, 3f, centerZ + zOffset),
                new Vector3(0.16f, 5.6f, 0.18f), steelDark, false);
            Box(room, "EastFrame", new Vector3(14.66f, 3f, centerZ + zOffset),
                new Vector3(0.16f, 5.6f, 0.18f), steelDark, false);
        }
        for (int x = -12; x <= 12; x += 4)
            Box(room, "FloorSeam", new Vector3(x, 0.015f, centerZ),
                new Vector3(0.035f, 0.02f, 19f), steelDark, false);

        Box(room, "RoomLabelPanel", new Vector3(-10.5f, 4.85f, centerZ - 9.68f),
            new Vector3(7f, 0.85f, 0.08f), steelDark, false);
        BuilderText.World(room, "RoomLabel", new Vector3(-10.5f, 4.85f, centerZ - 9.74f),
            label, new Vector2(6.5f, 0.65f), Color.white, 2.3f);
        Box(room, "StatusLine", new Vector3(0f, 5.72f, centerZ - 9.7f),
            new Vector3(28f, 0.12f, 0.06f), accent, false);

        CeilingLight(room, new Vector3(-7.5f, 5.78f, centerZ - 4.8f), index == 4 ? red : white);
        CeilingLight(room, new Vector3(7.5f, 5.78f, centerZ - 4.8f), index == 4 ? red : white);
        CeilingLight(room, new Vector3(-7.5f, 5.78f, centerZ + 4.8f), index == 4 ? red : white);
        CeilingLight(room, new Vector3(7.5f, 5.78f, centerZ + 4.8f), index == 4 ? red : white);
    }

    private static void Airlock(Transform parent, string name, float z, MagicEscapePhase phase, Material accent)
    {
        Transform group = Group(parent, name);
        Box(group, "WestBulkhead", new Vector3(-9f, 3f, z), new Vector3(12f, 6f, 0.8f), concreteDark);
        Box(group, "EastBulkhead", new Vector3(9f, 3f, z), new Vector3(12f, 6f, 0.8f), concreteDark);
        Box(group, "Header", new Vector3(0f, 5.65f, z), new Vector3(6f, 0.7f, 0.8f), steel);

        GameObject door = Box(group, "SealedDoor", new Vector3(0f, 2.55f, z),
            new Vector3(6f, 5.1f, 0.65f), steel);
        door.AddComponent<MagicEscapeGate>().Configure(phase, new Vector3(0f, 5.8f, 0f));

        Box(group, "DoorInset", new Vector3(0f, 2.55f, z - 0.34f),
            new Vector3(4.7f, 3.7f, 0.05f), steelDark, false);
        Box(group, "StatusBar", new Vector3(0f, 4.75f, z - 0.38f),
            new Vector3(5.2f, 0.16f, 0.06f), accent, false);
        HazardStripe(group, new Vector3(0f, 0.025f, z - 0.75f), 6f, 0f);

        for (int side = -1; side <= 1; side += 2)
        {
            Box(group, "Frame", new Vector3(side * 3.25f, 2.9f, z),
                new Vector3(0.35f, 5.8f, 1.35f), warning);
            PointLight(group, new Vector3(side * 3.2f, 4.8f, z - 0.8f), accent.color, 3f, 2.5f);
        }
    }

    private static void BuildEntrance(Transform root)
    {
        Transform group = Group(root, "Entrance_B01");
        WallDisplay(group, "MissionDisplay", new Vector3(0f, 3f, -57.9f), 0f,
            "B-13 / 피실험자 이송 중\n비상 전력을 복구하고 승강기까지 이동하십시오", red, 11f);
        Box(group, "IntakeRailLeft", new Vector3(-5f, 0.55f, -53f), new Vector3(0.12f, 1.1f, 7f), steel);
        Box(group, "IntakeRailRight", new Vector3(5f, 0.55f, -53f), new Vector3(0.12f, 1.1f, 7f), steel);
        HazardStripe(group, new Vector3(0f, 0.025f, -50f), 10f, 0f);
    }

    private static void BuildQuarantineSearch(Transform root)
    {
        Transform group = Group(root, "B01_QuarantineSearch");
        WallDisplay(group, "Guide", new Vector3(0f, 3f, -57.85f), 180f,
            "검역 절차 중단 / 보조 전력 단자 5개를 수동 복구하십시오", amber, 12f);

        Partition(group, new Vector3(-7f, 1.6f, -49f), new Vector3(0.45f, 3.2f, 11f));
        Partition(group, new Vector3(7f, 1.6f, -47f), new Vector3(0.45f, 3.2f, 10f));
        Partition(group, new Vector3(-11f, 1.6f, -43f), new Vector3(7.5f, 3.2f, 0.45f));
        Partition(group, new Vector3(11f, 1.6f, -52f), new Vector3(7.5f, 3.2f, 0.45f));
        BuildScanner(group, new Vector3(0f, 0f, -49f));

        PowerTerminal(group, 0, new Vector3(-14.55f, 0f, -53f), 90f, amber);
        PowerTerminal(group, 1, new Vector3(14.55f, 0f, -51f), -90f, amber);
        PowerTerminal(group, 2, new Vector3(-10.5f, 0f, -42.7f), 180f, amber);
        PowerTerminal(group, 3, new Vector3(9f, 0f, -55f), 0f, amber);
        PowerTerminal(group, 4, new Vector3(14.55f, 0f, -40f), -90f, amber);
    }

    private static void BuildObservationCipher(Transform root)
    {
        Transform group = Group(root, "B02_ObservationCipher");
        WallDisplay(group, "Guide", new Vector3(0f, 3f, -36.85f), 180f,
            "관찰 기록이 분리되었습니다 / 양쪽 기록을 음성으로 조합하십시오", cyan, 12f);
        Box(group, "ObservationDivider", new Vector3(0f, 2.3f, -27f),
            new Vector3(0.65f, 4.6f, 13f), concreteDark);

        ObservationWindow(group, new Vector3(-14.62f, 2.8f, -27f), 90f);
        ObservationWindow(group, new Vector3(14.62f, 2.8f, -27f), -90f);
        WallDisplay(group, "WestRecord", new Vector3(-14.31f, 2.7f, -24f), 90f,
            "서측 기록\n01 원형 / 02 파동 / 05 파동", cyan, 6.5f);
        WallDisplay(group, "EastRecord", new Vector3(14.31f, 2.7f, -29f), -90f,
            "동측 기록\n03 교차 / 04 격자 / 06 원형", cyan, 6.5f);

        string[] names = { "파동", "격자", "원형", "교차" };
        Material[] colors = { red, cyan, warning, green };
        for (int i = 0; i < names.Length; i++)
            RuneTerminal(group, i, names[i], new Vector3(-7.5f + i * 5f, 0f, -18.8f), colors[i]);
    }

    private static void BuildSpecimenTransit(Transform root)
    {
        Transform group = Group(root, "B03_SpecimenTransit");
        WallDisplay(group, "Guide", new Vector3(0f, 3f, -14.85f), 180f,
            "운송 잠금 감지 / 서로 다른 작업자가 양쪽 중량판을 유지하십시오", warning, 12f);

        Box(group, "CoolantChannel", new Vector3(0f, 0.03f, -3f),
            new Vector3(5.5f, 0.05f, 16f), voidMat, false);
        for (int z = -11; z <= 3; z += 3)
        {
            Box(group, "LeftCatwalk", new Vector3(-7f, 0.2f, z), new Vector3(8f, 0.4f, 2.2f), steel);
            Box(group, "RightCatwalk", new Vector3(7f, 0.2f, z), new Vector3(8f, 0.4f, 2.2f), steel);
        }
        for (int side = -1; side <= 1; side += 2)
        {
            Box(group, "SafetyRail", new Vector3(side * 3.1f, 0.75f, -3f),
                new Vector3(0.12f, 1.1f, 15f), warning);
            BuildSpecimenCart(group, new Vector3(side * 9f, 0f, -5f + side * 2f));
        }

        PressurePlate(group, 0, new Vector3(-9f, 0.28f, 3.5f), cyan);
        PressurePlate(group, 1, new Vector3(9f, 0.28f, 3.5f), amber);
    }

    private static void BuildControlRoom(Transform root)
    {
        Transform group = Group(root, "B04_CentralControl");
        WallDisplay(group, "Guide", new Vector3(0f, 3f, 7.15f), 180f,
            "격리 차단기 분산 배치 / 9초 안에 두 계통을 동시에 복구하십시오", red, 12f);
        Box(group, "BlastDivider", new Vector3(0f, 2.4f, 18f),
            new Vector3(0.75f, 4.8f, 15f), concreteDark);
        Partition(group, new Vector3(-8f, 1.8f, 16f), new Vector3(9f, 3.6f, 0.45f));
        Partition(group, new Vector3(8f, 1.8f, 21f), new Vector3(9f, 3.6f, 0.45f));

        ControlDesk(group, new Vector3(-8.5f, 0f, 11.5f), 0f, cyan);
        ControlDesk(group, new Vector3(8.5f, 0f, 11.5f), 0f, red);
        Lever(group, 0, new Vector3(-14.45f, 0f, 25f), 90f, cyan);
        Lever(group, 1, new Vector3(14.45f, 0f, 25f), -90f, red);
        WallDisplay(group, "WestCallout", new Vector3(-14.3f, 3.6f, 18f), 90f,
            "A 계통\n음성 연결 확인", cyan, 5f);
        WallDisplay(group, "EastCallout", new Vector3(14.3f, 3.6f, 18f), -90f,
            "B 계통\n카운트 후 작동", red, 5f);
    }

    private static void BuildContainmentVault(Transform root)
    {
        Transform group = Group(root, "B05_ContainmentVault");
        WallDisplay(group, "Guide", new Vector3(0f, 3f, 29.15f), 180f,
            "위험 시료 격리 실패 / 승인된 도구로 격리핵 4개를 파괴하십시오", red, 12f);

        BuildWeaponCage(group, new Vector3(-11.5f, 0f, 35f), "보관함 W-01");
        BuildWeaponCage(group, new Vector3(11.5f, 0f, 38f), "보관함 W-02");
        BuildEmptyCage(group, new Vector3(-11.5f, 0f, 43f), "비어 있음");
        BuildEmptyCage(group, new Vector3(11.5f, 0f, 33f), "접근 거부");

        SealCore(group, 0, new Vector3(-10.5f, 1.15f, 47f), red);
        SealCore(group, 1, new Vector3(-3.5f, 1.15f, 47f), white);
        SealCore(group, 2, new Vector3(3.5f, 1.15f, 47f), red);
        SealCore(group, 3, new Vector3(10.5f, 1.15f, 47f), white);
        HazardStripe(group, new Vector3(0f, 0.025f, 44.2f), 27f, 0f);
    }

    private static void BuildEmergencyLift(Transform root)
    {
        Transform group = Group(root, "B06_EmergencyLift");
        WallDisplay(group, "Guide", new Vector3(0f, 3f, 51.15f), 180f,
            "비상 승강기 온라인 / 등록된 전원이 탑승해야 지상으로 이동합니다", green, 12f);

        Box(group, "LiftBack", new Vector3(0f, 2.6f, 70.8f), new Vector3(12f, 5.2f, 0.55f), steelDark);
        Box(group, "LiftLeft", new Vector3(-6f, 2.6f, 65f), new Vector3(0.55f, 5.2f, 12f), steel);
        Box(group, "LiftRight", new Vector3(6f, 2.6f, 65f), new Vector3(0.55f, 5.2f, 12f), steel);
        Box(group, "LiftCeiling", new Vector3(0f, 5.2f, 65f), new Vector3(12f, 0.35f, 12f), steelDark);
        Box(group, "LiftFloor", new Vector3(0f, 0.06f, 65f), new Vector3(11.5f, 0.12f, 11.5f), steel);
        Box(group, "LiftGlow", new Vector3(0f, 0.14f, 65f), new Vector3(9.8f, 0.05f, 9.8f), green, false);
        WallDisplay(group, "LiftDisplay", new Vector3(0f, 3f, 70.48f), 0f,
            "EVAC READY\n탑승 인원 확인 중", green, 7f);

        GameObject triggerObject = new GameObject("RallyTrigger");
        triggerObject.transform.SetParent(group, false);
        triggerObject.transform.localPosition = new Vector3(0f, 1.2f, 65f);
        BoxCollider trigger = triggerObject.AddComponent<BoxCollider>();
        trigger.size = new Vector3(10.6f, 2.6f, 10.6f);
        trigger.isTrigger = true;
        triggerObject.AddComponent<MagicEscapeRallyZone>();
        PointLight(group, new Vector3(0f, 4.6f, 65f), green.color, 11f, 4f);
    }

    private static void PowerTerminal(Transform parent, int index, Vector3 pos, float rotY, Material accent)
    {
        Transform group = Group(parent, "PowerTerminal_" + (index + 1), pos, rotY);
        Box(group, "Housing", new Vector3(0f, 1.35f, 0f), new Vector3(1.45f, 2.7f, 0.5f), steelDark);
        GameObject screen = Box(group, "Screen", new Vector3(0f, 1.85f, -0.28f),
            new Vector3(0.9f, 0.55f, 0.06f), accent, false);
        GameObject handle = Cylinder(group, "Breaker", new Vector3(0f, 1.05f, -0.46f),
            new Vector3(0.14f, 1f, 0.14f), steel);
        group.gameObject.AddComponent<MagicEscapeSwitch>().Configure(index, handle.transform, screen.GetComponent<Renderer>());
        BuilderText.World(group, "Number", new Vector3(0f, 2.4f, -0.29f),
            "P-" + (index + 1).ToString("00"), new Vector2(1.1f, 0.35f), Color.white, 1.5f);
    }

    private static void RuneTerminal(Transform parent, int index, string label, Vector3 pos, Material accent)
    {
        Transform group = Group(parent, "Cipher_" + label, pos);
        Box(group, "Console", new Vector3(0f, 0.65f, 0f), new Vector3(3.7f, 1.3f, 2.5f), steelDark);
        GameObject glow = Box(group, "InputPad", new Vector3(0f, 1.35f, -0.15f),
            new Vector3(2.5f, 0.16f, 1.45f), accent);
        group.gameObject.AddComponent<MagicEscapeRune>().Configure(index, label, glow.GetComponent<Renderer>());
        BuilderText.World(group, "Label", new Vector3(0f, 1.72f, -0.7f), label,
            new Vector2(2.4f, 0.55f), Color.white, 1.8f);
    }

    private static void PressurePlate(Transform parent, int index, Vector3 pos, Material accent)
    {
        Transform group = Group(parent, "PressurePlate_" + (index + 1), pos);
        Box(group, "Base", Vector3.zero, new Vector3(4.4f, 0.16f, 4.4f), steelDark);
        GameObject visual = Box(group, "PressedVisual", new Vector3(0f, 0.12f, 0f),
            new Vector3(3.8f, 0.12f, 3.8f), accent, false);
        BoxCollider trigger = group.gameObject.AddComponent<BoxCollider>();
        trigger.center = new Vector3(0f, 0.8f, 0f);
        trigger.size = new Vector3(4.5f, 1.8f, 4.5f);
        trigger.isTrigger = true;
        group.gameObject.AddComponent<MagicEscapePressurePlate>().Configure(index, visual.transform);
    }

    private static void Lever(Transform parent, int index, Vector3 pos, float rotY, Material accent)
    {
        Transform group = Group(parent, "IsolationBreaker_" + (index + 1), pos, rotY);
        Box(group, "Housing", new Vector3(0f, 1.45f, 0f), new Vector3(1.7f, 2.9f, 0.6f), steelDark);
        Box(group, "Status", new Vector3(0f, 2.15f, -0.34f), new Vector3(1.05f, 0.42f, 0.08f), accent, false);
        GameObject handle = Cylinder(group, "Handle", new Vector3(0f, 1.25f, -0.55f),
            new Vector3(0.18f, 1.35f, 0.18f), steel);
        group.gameObject.AddComponent<MagicEscapeLever>().Configure(index, handle.transform);
    }

    private static void SealCore(Transform parent, int index, Vector3 pos, Material accent)
    {
        Transform group = Group(parent, "ContainmentCore_" + (index + 1), pos);
        Cylinder(group, "Pedestal", new Vector3(0f, -0.65f, 0f), new Vector3(1.9f, 1f, 1.9f), steelDark);
        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "Core";
        core.transform.SetParent(group, false);
        core.transform.localScale = new Vector3(1.35f, 1.8f, 1.35f);
        core.GetComponent<Renderer>().sharedMaterial = accent;
        var renderers = new List<Renderer> { core.GetComponent<Renderer>() };
        for (int i = 0; i < 6; i++)
        {
            float angle = i * Mathf.PI / 3f;
            GameObject clamp = Box(group, "Clamp_" + i,
                new Vector3(Mathf.Cos(angle) * 1.2f, Mathf.Sin(angle) * 1.2f, 0f),
                new Vector3(0.72f, 0.13f, 0.16f), steel, false);
            clamp.transform.localRotation = Quaternion.Euler(0f, 0f, i * 60f + 90f);
            renderers.Add(clamp.GetComponent<Renderer>());
        }
        group.gameObject.AddComponent<MagicEscapeSeal>().Configure(index, renderers.ToArray());
        PointLight(group, Vector3.zero, accent.color, 3.8f, 2.5f);
    }

    private static void PlaceScarceWeapons(Transform parent)
    {
        Transform group = Group(parent, "ContainedMagicWeapons");
        string[] weapons = { "Frozen_Tuna", "Whisk_Axe" };
        Vector3[] positions = { new Vector3(-11.5f, 1f, 35f), new Vector3(11.5f, 1f, 38f) };
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

    private static void AddLaboratoryDetails(Transform root)
    {
        Transform group = Group(root, "LaboratoryDetails");
        for (int i = 0; i < 6; i++)
        {
            float z = -48f + i * 22f;
            PipeRun(group, new Vector3(-13.9f, 4.7f, z), RoomLength - 2f, i % 2 == 0 ? cyan : red);
            SecurityCamera(group, new Vector3(13.9f, 4.8f, z - 6f), -90f);
            WallPanel(group, new Vector3(14.68f, 2.4f, z + 6f), -90f, i % 2 == 0 ? amber : cyan);
        }

        string[] ids = { "B-01", "B-02", "B-03", "B-04", "B-05", "B-06" };
        for (int i = 0; i < ids.Length; i++)
        {
            float z = -48f + i * 22f;
            Box(group, "FloorIDPlate", new Vector3(11.5f, 0.025f, z), new Vector3(5f, 0.03f, 2.2f), panel, false);
            var floorId = BuilderText.World(group, "FloorID", new Vector3(11.5f, 0.05f, z), ids[i],
                new Vector2(4.5f, 1.5f), new Color(0.7f, 0.72f, 0.74f), 3f);
            floorId.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }
    }

    private static void AddAtmosphere(Transform root)
    {
        Transform group = Group(root, "Atmosphere");
        float[] doorZ = { -37f, -15f, 7f, 29f, 51f };
        foreach (float z in doorZ)
        {
            PointLight(group, new Vector3(-4.2f, 4.8f, z - 1f), red.color, 4f, 2f);
            PointLight(group, new Vector3(4.2f, 4.8f, z - 1f), red.color, 4f, 2f);
        }
    }

    private static void BuildScanner(Transform parent, Vector3 pos)
    {
        Transform group = Group(parent, "BodyScanner", pos);
        for (int side = -1; side <= 1; side += 2)
        {
            Box(group, "ScannerPillar", new Vector3(side * 2.1f, 2f, 0f), new Vector3(0.55f, 4f, 1.3f), steel);
            Box(group, "ScannerLight", new Vector3(side * 1.78f, 2.1f, 0f), new Vector3(0.08f, 2.8f, 0.7f), cyan, false);
        }
        Box(group, "ScannerHeader", new Vector3(0f, 4f, 0f), new Vector3(4.8f, 0.5f, 1.3f), steelDark);
    }

    private static void ObservationWindow(Transform parent, Vector3 pos, float rotY)
    {
        Transform group = Group(parent, "ObservationWindow", pos, rotY);
        Box(group, "Frame", Vector3.zero, new Vector3(6.5f, 3.4f, 0.22f), steelDark);
        Box(group, "Glass", new Vector3(0f, 0f, -0.13f), new Vector3(5.8f, 2.7f, 0.06f), glass, false);
        Box(group, "Crossbar", new Vector3(0f, 0f, -0.18f), new Vector3(0.12f, 2.8f, 0.08f), steel, false);
    }

    private static void BuildSpecimenCart(Transform parent, Vector3 pos)
    {
        Transform group = Group(parent, "SpecimenCart", pos);
        Box(group, "Bed", new Vector3(0f, 0.8f, 0f), new Vector3(3.8f, 0.25f, 1.7f), steel);
        Cylinder(group, "Tank", new Vector3(0f, 1.45f, 0f), new Vector3(0.7f, 1.2f, 0.7f), glass);
        for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
                Cylinder(group, "Wheel", new Vector3(x * 1.55f, 0.35f, z * 0.55f),
                    new Vector3(0.25f, 0.2f, 0.25f), steelDark);
    }

    private static void ControlDesk(Transform parent, Vector3 pos, float rotY, Material accent)
    {
        Transform group = Group(parent, "ControlDesk", pos, rotY);
        Box(group, "Desk", new Vector3(0f, 0.65f, 0f), new Vector3(5.2f, 1.3f, 1.7f), steelDark);
        for (int i = -1; i <= 1; i++)
            Box(group, "Monitor", new Vector3(i * 1.5f, 1.65f, 0.25f), new Vector3(1.25f, 1.2f, 0.16f), accent, false);
    }

    private static void BuildWeaponCage(Transform parent, Vector3 pos, string label)
    {
        Transform group = Group(parent, "WeaponCage", pos);
        Box(group, "Back", new Vector3(0f, 1.4f, 0.6f), new Vector3(4.2f, 2.8f, 0.3f), steelDark);
        Box(group, "Base", new Vector3(0f, 0.15f, 0f), new Vector3(4.2f, 0.3f, 2.2f), steel);
        for (int x = -2; x <= 2; x++)
            Box(group, "Bar", new Vector3(x * 0.72f, 1.5f, -0.8f), new Vector3(0.08f, 3f, 0.08f), steel, false);
        BuilderText.World(group, "Label", new Vector3(0f, 2.55f, -0.85f), label,
            new Vector2(3.8f, 0.45f), Color.white, 1.4f);
        PointLight(group, new Vector3(0f, 2.2f, 0f), red.color, 3.5f, 2.2f);
    }

    private static void BuildEmptyCage(Transform parent, Vector3 pos, string label)
    {
        BuildWeaponCage(parent, pos, label);
        Transform marker = Group(parent, "EmptyMarker", pos + new Vector3(0f, 1.2f, -0.9f));
        Box(marker, "SlashA", Vector3.zero, new Vector3(2.2f, 0.14f, 0.08f), red, false).transform.localRotation = Quaternion.Euler(0f, 0f, 35f);
        Box(marker, "SlashB", Vector3.zero, new Vector3(2.2f, 0.14f, 0.08f), red, false).transform.localRotation = Quaternion.Euler(0f, 0f, -35f);
    }

    private static void Partition(Transform parent, Vector3 pos, Vector3 size)
    {
        Box(parent, "LabPartition", pos, size, panel);
        Box(parent, "PartitionCap", pos + Vector3.up * (size.y * 0.5f + 0.05f),
            new Vector3(size.x + 0.12f, 0.1f, size.z + 0.12f), steelDark, false);
    }

    private static void WallPanel(Transform parent, Vector3 pos, float rotY, Material accent)
    {
        Transform group = Group(parent, "UtilityPanel", pos, rotY);
        Box(group, "Housing", Vector3.zero, new Vector3(2.4f, 3.2f, 0.2f), steelDark, false);
        Box(group, "Display", new Vector3(0f, 0.6f, -0.13f), new Vector3(1.55f, 0.7f, 0.05f), accent, false);
        for (int i = -1; i <= 1; i++)
            Box(group, "Button", new Vector3(i * 0.45f, -0.55f, -0.14f), new Vector3(0.2f, 0.2f, 0.05f), white, false);
    }

    private static void PipeRun(Transform parent, Vector3 pos, float length, Material accent)
    {
        Transform group = Group(parent, "PipeRun", pos);
        GameObject pipe = Cylinder(group, "MainPipe", Vector3.zero, new Vector3(0.22f, length, 0.22f), steel);
        pipe.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        for (int z = -8; z <= 8; z += 4)
            Cylinder(group, "PipeClamp", new Vector3(0f, 0f, z), new Vector3(0.31f, 0.08f, 0.31f), accent);
    }

    private static void SecurityCamera(Transform parent, Vector3 pos, float rotY)
    {
        Transform group = Group(parent, "SecurityCamera", pos, rotY);
        Box(group, "Arm", new Vector3(0f, 0.2f, 0f), new Vector3(0.12f, 0.65f, 0.12f), steelDark, false);
        GameObject body = Box(group, "CameraBody", new Vector3(0f, -0.2f, -0.35f), new Vector3(0.65f, 0.45f, 1.1f), steel, false);
        body.transform.localRotation = Quaternion.Euler(18f, 0f, 0f);
        Box(group, "Lens", new Vector3(0f, -0.32f, -0.94f), new Vector3(0.25f, 0.25f, 0.05f), red, false);
    }

    private static void WallDisplay(Transform parent, string name, Vector3 pos, float rotY,
        string text, Material accent, float width)
    {
        Transform group = Group(parent, name, pos, rotY);
        Box(group, "Housing", Vector3.zero, new Vector3(width, 2.1f, 0.22f), steelDark, false);
        Box(group, "Screen", new Vector3(0f, 0f, -0.13f), new Vector3(width - 0.5f, 1.6f, 0.05f), panel, false);
        Box(group, "Status", new Vector3(0f, 0.9f, -0.17f), new Vector3(width - 0.6f, 0.08f, 0.04f), accent, false);
        BuilderText.World(group, "Text", new Vector3(0f, -0.05f, -0.18f), text,
            new Vector2(width - 0.7f, 1.35f), Color.white, 1.9f);
    }

    private static void HazardStripe(Transform parent, Vector3 center, float width, float rotY)
    {
        Transform group = Group(parent, "HazardStripe", center, rotY);
        int count = Mathf.Max(2, Mathf.RoundToInt(width / 0.65f));
        float unit = width / count;
        for (int i = 0; i < count; i++)
            Box(group, "Stripe", new Vector3(-width * 0.5f + unit * (i + 0.5f), 0f, 0f),
                new Vector3(unit * 0.92f, 0.025f, 0.7f), i % 2 == 0 ? warning : steelDark, false);
    }

    private static void CeilingLight(Transform parent, Vector3 pos, Material material)
    {
        Box(parent, "LightHousing", pos + Vector3.up * 0.08f, new Vector3(5.2f, 0.16f, 0.7f), steelDark, false);
        Box(parent, "LightPanel", pos, new Vector3(4.6f, 0.08f, 0.42f), material, false);
        PointLight(parent, pos + Vector3.down * 0.35f, material.color, 9f, material == red ? 2.2f : 1.7f);
    }

    private static void AddSpawns(Vector3 origin)
    {
        Transform root = new GameObject("NetworkSpawnPoints").transform;
        for (int i = 0; i < 8; i++)
        {
            GameObject point = new GameObject("Spawn_" + (char)('A' + i));
            point.transform.SetParent(root, false);
            point.transform.position = origin + new Vector3((i % 4 - 1.5f) * 2f, 0f, (i / 4) * -2f);
            point.transform.rotation = Quaternion.identity;
            point.AddComponent<NetworkStartPosition>();
        }
    }

    private static void AddLighting()
    {
        RenderSettings.sun = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.11f, 0.12f, 0.13f);
        RenderSettings.ambientEquatorColor = new Color(0.075f, 0.08f, 0.085f);
        RenderSettings.ambientGroundColor = new Color(0.025f, 0.028f, 0.03f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.07f, 0.075f, 0.08f);
        RenderSettings.fogStartDistance = 24f;
        RenderSettings.fogEndDistance = 78f;
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
        concrete = EnsureSurface("B13_Concrete", new Color(0.34f, 0.35f, 0.36f), 0f, 0.22f);
        concreteDark = EnsureSurface("B13_ConcreteDark", new Color(0.16f, 0.17f, 0.18f), 0f, 0.2f);
        floor = EnsureSurface("B13_Floor", new Color(0.105f, 0.115f, 0.12f), 0.35f, 0.35f);
        steel = EnsureSurface("B13_Steel", new Color(0.25f, 0.27f, 0.29f), 0.72f, 0.58f);
        steelDark = EnsureSurface("B13_SteelDark", new Color(0.065f, 0.072f, 0.078f), 0.65f, 0.42f);
        panel = EnsureSurface("B13_Panel", new Color(0.12f, 0.135f, 0.145f), 0.25f, 0.35f);
        glass = EnsureSurface("B13_Glass", new Color(0.035f, 0.08f, 0.095f), 0.35f, 0.85f);
        voidMat = EnsureSurface("B13_CoolantVoid", new Color(0.012f, 0.028f, 0.034f), 0.1f, 0.7f);
        white = EnsureSurface("B13_Light", new Color(0.78f, 0.84f, 0.86f), 0f, 0.4f);
        amber = EnsureSurface("B13_Amber", new Color(0.94f, 0.46f, 0.08f), 0.1f, 0.45f);
        cyan = EnsureSurface("B13_Cyan", new Color(0.08f, 0.68f, 0.76f), 0.1f, 0.5f);
        red = EnsureSurface("B13_Red", new Color(0.82f, 0.055f, 0.045f), 0.1f, 0.45f);
        green = EnsureSurface("B13_Green", new Color(0.08f, 0.72f, 0.35f), 0.1f, 0.5f);
        warning = EnsureSurface("B13_Warning", new Color(0.95f, 0.7f, 0.06f), 0.15f, 0.35f);
    }

    private static Material EnsureSurface(string name, Color color, float metallic, float smoothness)
    {
        Material material = BuilderMaterials.Ensure(name, color);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void SetBuildOrder()
    {
        string[] paths = { TitlePath, LobbyPath, Stage3Path, ScenePath, CampPath };
        EditorBuildSettings.scenes = paths.Where(File.Exists)
            .Select(path => new EditorBuildSettingsScene(path, true)).ToArray();
    }

    private static Transform Group(Transform parent, string name, Vector3 pos = default, float rotY = 0f)
    {
        Transform group = new GameObject(name).transform;
        group.SetParent(parent, false);
        group.localPosition = pos;
        group.localRotation = Quaternion.Euler(0f, rotY, 0f);
        return group;
    }

    private static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size,
        Material material, bool collider = true)
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
