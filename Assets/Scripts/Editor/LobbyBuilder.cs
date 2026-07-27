#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 대기실(로비) 씬을 생성하는 에디터 도구.
/// 숲 들판 + 중앙 참치 제단(나무 식탁 위 냉동참치 = 마검) + 허수아비 연습장
/// + 출발 게이트/발판 + 스폰포인트 + 네트워크 매니저까지 한 번에 구성한다.
/// 메뉴: Tools > Lobby > Build Lobby
///
/// 함께 처리하는 것:
/// - Build Settings 순서를 [MainTitle, Lobby, NetworkDemo, Stage2]로 정리
/// - MainTitle의 MainMenu가 대기실로 넘어가도록 대상 씬을 Lobby로 수정
///
/// 대기실을 수정할 땐 씬을 손으로 고치지 말고 이 파일의 좌표/치수를 바꿔 다시 실행한다.
/// </summary>
public static class LobbyBuilder
{
    private const string LobbyPath = "Assets/Scenes/Lobby.unity";
    private const string TitlePath = "Assets/Scenes/MainTitle.unity";
    private const string DemoPath = "Assets/Scenes/NetworkDemo.unity";
    private const string Stage2Path = "Assets/Scenes/Stage2_Campground.unity";
    private const string PlayerPrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";

    private const string LobbySceneName = "Lobby";
    private const string FirstStageScene = "NetworkDemo";

    /// <summary>방 정원(호스트 포함). 스폰 지점 수와 맞춰 둔다.</summary>
    private const int MaxPlayers = 5;

    // 주요 좌표 (바닥 윗면 y = 0)
    private static readonly Vector3 AltarPos = Vector3.zero;
    private static readonly Vector3 GatePos = new Vector3(0f, 0f, 14f);
    private static readonly Vector3 ReadyPadPos = new Vector3(0f, 0f, 11.8f);
    private static readonly Vector3 SpawnBase = new Vector3(0f, 1f, -12f);

    private static Material matGrass, matDirt, matWood, matStone, matDark, matFlame, matLeaf, matAccent, matTuna, matStraw;

    [MenuItem("Tools/Lobby/Build Lobby")]
    public static void BuildLobby()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        // 1) 타이틀이 대기실로 넘어가도록 먼저 고쳐 둔다(씬을 오가야 하므로).
        PatchTitleTarget();

        // 2) 대기실 씬 생성
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        LoadMaterials();
        AddSun();

        var root = new GameObject("Lobby").transform;

        Ground(root);
        Forest(root);
        Altar(root);
        Gate(root);
        PracticeArea(root);
        Campfire(root, new Vector3(-9.5f, 0f, -6.5f));
        GuideSign(root);

        SpawnPoints();

        // 3) 네트워크 구성 — 대기실의 NetworkManager가 이후 스테이지까지 유지된다.
        SetupNetworkBootstrap();
        NetworkPhase4Setup.SetupWeaponSync(); // WeaponManager + 참치에 무기 ID 부여
        SetupLobbyManager();

        EditorSceneManager.SaveScene(scene, LobbyPath);

        // 4) Build Settings 순서 정리
        SetBuildOrder();

        Debug.Log("[Lobby] 대기실 씬 생성 완료: " + LobbyPath +
                  "\n- 타이틀에서 호스트/참가하면 대기실로 들어갑니다." +
                  "\n- 전원이 출발 발판에 올라서면 " + FirstStageScene + " 으로 전환됩니다.");
    }

    // ---------------------------------------------------------------- 환경

    private static void Ground(Transform p)
    {
        Box(p, "Ground", new Vector3(0f, -0.5f, 0f), new Vector3(70f, 1f, 70f), matGrass);

        // 스폰 → 제단 → 출발 게이트로 이어지는 흙길
        Box(p, "Path", new Vector3(0f, 0.03f, 1f), new Vector3(3.2f, 0.06f, 30f), matDirt);
    }

    private static void Forest(Transform p)
    {
        Transform g = Group(p, "Forest", Vector3.zero, 0f);
        var rng = new System.Random(20260725); // 다시 빌드해도 같은 숲이 나오도록 고정 시드

        // 외곽을 둘러싸는 숲
        for (int i = 0; i < 30; i++)
        {
            float ang = (i / 30f + (float)rng.NextDouble() * 0.02f) * Mathf.PI * 2f;
            float r = 25f + (float)rng.NextDouble() * 6f;
            Tree(g, new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r), 0.85f + (float)rng.NextDouble() * 0.5f);
        }

        // 공터 안쪽에 드문드문 — 길과 제단은 피한다
        Vector3[] inner =
        {
            new Vector3(-15f, 0f, 8f), new Vector3(14f, 0f, 10f), new Vector3(-16f, 0f, -9f),
            new Vector3(15f, 0f, -6f), new Vector3(9f, 0f, 17f), new Vector3(-10f, 0f, 16f),
        };
        foreach (Vector3 pos in inner)
            Tree(g, pos, 1f);

        // 덤불/바위 장식 (콜라이더 제거 — 발이 걸리지 않게)
        Vector3[] props =
        {
            new Vector3(-6f, 0f, 6f), new Vector3(6.5f, 0f, -5f), new Vector3(-4.5f, 0f, -9f),
            new Vector3(8f, 0f, 8f), new Vector3(-12f, 0f, 2f), new Vector3(12f, 0f, 2.5f),
        };
        for (int i = 0; i < props.Length; i++)
        {
            bool rock = i % 2 == 0;
            GameObject go = Prim(g, rock ? "Rock" : "Bush", PrimitiveType.Sphere,
                props[i] + new Vector3(0f, rock ? 0.18f : 0.25f, 0f),
                rock ? new Vector3(1.1f, 0.5f, 0.9f) : new Vector3(1.3f, 0.7f, 1.3f),
                rock ? matStone : matLeaf);
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }
    }

    private static void Tree(Transform p, Vector3 pos, float scale)
    {
        Transform g = Group(p, "Tree", pos, 0f);
        g.localScale = Vector3.one * scale;
        Prim(g, "Trunk", PrimitiveType.Cylinder, new Vector3(0f, 1.6f, 0f), new Vector3(0.55f, 1.6f, 0.55f), matWood);
        Prim(g, "Leaves", PrimitiveType.Sphere, new Vector3(0f, 3.6f, 0f), new Vector3(3f, 2.8f, 3f), matLeaf);
    }

    // ---------------------------------------------------------------- 참치 제단 (대기실의 얼굴)

    private static void Altar(Transform p)
    {
        Transform g = Group(p, "TunaAltar", AltarPos, 0f);

        // 돌 원판 + 둘러싼 돌기둥 (성검 제단 패러디)
        Disc(g, "Platform", 9f, 0.14f, matStone);
        Disc(g, "PlatformInlay", 6f, 0.16f, matDirt);
        for (int i = 0; i < 8; i++)
        {
            float a = i / 8f * Mathf.PI * 2f;
            Box(g, "Pillar", new Vector3(Mathf.Cos(a) * 3.6f, 0.86f, Mathf.Sin(a) * 3.6f), new Vector3(0.35f, 1.4f, 0.35f), matStone);
        }

        // 나무 식탁 — 이 위에 참치가 놓인다. (제단 윗면 y = 0.16 위에 세운다)
        Transform table = Group(g, "WoodenTable", new Vector3(0f, 0.16f, 0f), 0f);
        Box(table, "Top", new Vector3(0f, 0.78f, 0f), new Vector3(2.6f, 0.12f, 1.2f), matWood);
        Box(table, "Apron", new Vector3(0f, 0.68f, 0f), new Vector3(2.4f, 0.1f, 1f), matDark);
        Box(table, "LegNW", new Vector3(-1.15f, 0.36f, -0.45f), new Vector3(0.14f, 0.72f, 0.14f), matWood);
        Box(table, "LegNE", new Vector3(1.15f, 0.36f, -0.45f), new Vector3(0.14f, 0.72f, 0.14f), matWood);
        Box(table, "LegSW", new Vector3(-1.15f, 0.36f, 0.45f), new Vector3(0.14f, 0.72f, 0.14f), matWood);
        Box(table, "LegSE", new Vector3(1.15f, 0.36f, 0.45f), new Vector3(0.14f, 0.72f, 0.14f), matWood);
        Box(table, "BenchS", new Vector3(0f, 0.42f, 1.05f), new Vector3(2.4f, 0.1f, 0.35f), matWood);
        Box(table, "BenchN", new Vector3(0f, 0.42f, -1.05f), new Vector3(2.4f, 0.1f, 0.35f), matWood);
        Box(table, "BenchLegS", new Vector3(0f, 0.19f, 1.05f), new Vector3(2.2f, 0.38f, 0.1f), matWood);
        Box(table, "BenchLegN", new Vector3(0f, 0.19f, -1.05f), new Vector3(2.2f, 0.38f, 0.1f), matWood);

        // 마검 = 냉동참치. 식탁 상판 윗면(y = 0.16 + 0.78 + 0.06 = 1.00) 위에 안치.
        Transform tuna = FrozenTuna(g, new Vector3(0f, 1f, 0f), 90f);

        // 제단 조명 — 참치가 사라지면 꺼진다(LobbyAltar가 제어)
        var lightGo = new GameObject("AltarLight");
        lightGo.transform.SetParent(g, false);
        lightGo.transform.localPosition = new Vector3(0f, 7f, 0f);
        lightGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Spot;
        light.color = new Color(1f, 0.93f, 0.7f);
        light.spotAngle = 34f;
        light.range = 14f;
        light.intensity = 8f; // 눈에 띄되 과하지 않게. 밝기는 취향껏 조절
        light.shadows = LightShadows.None;

        // 제단 팻말 (참치를 뽑으면 문구가 바뀐다). 길을 막지 않게 옆으로 비켜 세운다.
        TextMesh sign = Sign(g, "AltarSign", new Vector3(3.4f, 0f, -4.4f), 205f, "이 냉동참치가 마검이다");

        var altar = g.gameObject.AddComponent<LobbyAltar>();
        altar.Configure(tuna.GetComponent<Weapon>(), light, sign);
    }

    /// <summary>마검: 냉동참치. (집 냉장고 안의 것과 같은 형태)</summary>
    private static Transform FrozenTuna(Transform parent, Vector3 localPos, float rotY)
    {
        Transform g = Group(parent, "FrozenTuna", localPos, rotY);
        Prim(g, "Body", PrimitiveType.Sphere, new Vector3(0f, 0.13f, 0.02f), new Vector3(0.22f, 0.26f, 0.6f), matTuna);
        Box(g, "Tail", new Vector3(0f, 0.13f, -0.36f), new Vector3(0.04f, 0.3f, 0.12f), matTuna);
        Box(g, "FinTop", new Vector3(0f, 0.29f, 0.05f), new Vector3(0.03f, 0.1f, 0.15f), matTuna);

        var w = g.gameObject.AddComponent<Weapon>();
        w.SetDisplayName("냉동참치");
        w.holdPosition = new Vector3(0f, -0.05f, -0.05f);
        w.holdEuler = new Vector3(-75f, 0f, 0f);
        return g;
    }

    // ---------------------------------------------------------------- 출발 게이트 + 준비 발판

    private static void Gate(Transform p)
    {
        Transform g = Group(p, "StartGate", GatePos, 0f);
        Box(g, "PostL", new Vector3(-2.6f, 2f, 0f), new Vector3(0.35f, 4f, 0.35f), matWood);
        Box(g, "PostR", new Vector3(2.6f, 2f, 0f), new Vector3(0.35f, 4f, 0.35f), matWood);
        Box(g, "Lintel", new Vector3(0f, 4.1f, 0f), new Vector3(5.9f, 0.35f, 0.4f), matWood);
        Box(g, "Banner", new Vector3(0f, 3.5f, 0.05f), new Vector3(3.4f, 0.9f, 0.06f), matAccent);

        // 준비 발판 (나무 데크)
        Transform pad = Group(p, "ReadyPad", ReadyPadPos, 0f);
        Box(pad, "Deck", new Vector3(0f, 0.09f, 0f), new Vector3(4.2f, 0.18f, 4.2f), matWood);
        Box(pad, "TrimN", new Vector3(0f, 0.22f, -2.1f), new Vector3(4.2f, 0.1f, 0.14f), matAccent);
        Box(pad, "TrimS", new Vector3(0f, 0.22f, 2.1f), new Vector3(4.2f, 0.1f, 0.14f), matAccent);

        // 준비 판정 영역 (발판 위 공간)
        var zoneGo = new GameObject("ReadyZone");
        zoneGo.transform.SetParent(pad, false);
        zoneGo.transform.localPosition = new Vector3(0f, 1.3f, 0f);
        var box = zoneGo.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(4.2f, 2.6f, 4.2f);
        zoneGo.AddComponent<LobbyReadyZone>();

        Sign(p, "GateSign", new Vector3(4.8f, 0f, 9.6f), 200f, "준비되면 발판 위로\n전원이 서면 출발!");
    }

    // ---------------------------------------------------------------- 허수아비 연습장

    private static void PracticeArea(Transform p)
    {
        Transform g = Group(p, "PracticeArea", new Vector3(-9.5f, 0f, 2.5f), 0f);
        Disc(g, "Sand", 8f, 0.05f, matDirt);

        Dummy(g, new Vector3(-1.6f, 0f, 1.4f), 20f);
        Dummy(g, new Vector3(1.2f, 0f, 1.9f), -25f);
        Dummy(g, new Vector3(0f, 0f, -1.6f), 165f);

        Sign(p, "PracticeSign", new Vector3(-6.4f, 0f, -1.6f), 150f, "허수아비\n마검으로 휘둘러 보세요");
    }

    private static void Dummy(Transform p, Vector3 pos, float rotY)
    {
        Transform g = Group(p, "Dummy", pos, rotY);
        Box(g, "Post", new Vector3(0f, 0.6f, 0f), new Vector3(0.12f, 1.2f, 0.12f), matWood);
        Box(g, "Body", new Vector3(0f, 1.25f, 0f), new Vector3(0.55f, 0.7f, 0.35f), matStraw);
        Box(g, "Arms", new Vector3(0f, 1.5f, 0f), new Vector3(1.5f, 0.12f, 0.12f), matWood);
        Prim(g, "Head", PrimitiveType.Sphere, new Vector3(0f, 1.8f, 0f), new Vector3(0.35f, 0.4f, 0.35f), matStraw);
        Box(g, "Hat", new Vector3(0f, 1.98f, 0f), new Vector3(0.6f, 0.05f, 0.6f), matStraw);
        g.gameObject.AddComponent<PracticeDummy>();
    }

    // ---------------------------------------------------------------- 모닥불 / 안내

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

        var lightGo = new GameObject("FireLight");
        lightGo.transform.SetParent(g, false);
        lightGo.transform.localPosition = new Vector3(0f, 0.7f, 0f);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.6f, 0.25f);
        light.range = 10f;
        light.intensity = 2f;

        // 둘러앉는 통나무
        for (int i = 0; i < 3; i++)
        {
            float a = (i / 3f) * Mathf.PI * 2f + 0.4f;
            Prim(g, "SitLog", PrimitiveType.Cylinder,
                new Vector3(Mathf.Cos(a) * 2f, 0.22f, Mathf.Sin(a) * 2f),
                new Vector3(0f, -a * Mathf.Rad2Deg, 90f),
                new Vector3(0.44f, 0.9f, 0.44f), matWood);
        }
    }

    private static void GuideSign(Transform p)
    {
        Sign(p, "ControlSign", new Vector3(4.4f, 0f, -8.6f), 200f,
            "WASD 이동 · Space 점프\n좌클릭 줍기 / 휘두르기\nG 내려놓기");
    }

    /// <summary>
    /// 나무 팻말 + 글자. rotY가 향하는 쪽(+Z)에서 읽힌다.
    /// 글자 크기는 characterSize로 조절한다 — 한글은 전각이라 한 줄 20자쯤이 판 폭에 맞는다.
    /// </summary>
    private static TextMesh Sign(Transform parent, string name, Vector3 pos, float rotY, string text)
    {
        Transform g = Group(parent, name, pos, rotY);
        Box(g, "Post", new Vector3(0f, 0.7f, 0f), new Vector3(0.1f, 1.4f, 0.1f), matWood);
        Box(g, "Board", new Vector3(0f, 1.6f, 0f), new Vector3(3.4f, 1f, 0.08f), matWood);

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(g, false);
        textGo.transform.localPosition = new Vector3(0f, 1.6f, 0.06f);

        var tm = textGo.AddComponent<TextMesh>();
        tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        textGo.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
        tm.text = text;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.characterSize = 0.02f;
        tm.fontSize = 64;
        tm.lineSpacing = 1.1f;
        tm.color = new Color(0.14f, 0.11f, 0.08f);
        textGo.AddComponent<WorldSign>();

        return tm;
    }

    // ---------------------------------------------------------------- 스폰 / 네트워크

    private static void SpawnPoints()
    {
        var root = new GameObject("NetworkSpawnPoints").transform;
        Vector3[] offsets =
        {
            new Vector3(-3.2f, 0f, 0f), new Vector3(-1.6f, 0f, -1.2f), new Vector3(0f, 0f, 0f),
            new Vector3(1.6f, 0f, -1.2f), new Vector3(3.2f, 0f, 0f),
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            var sp = new GameObject("Spawn_" + (char)('A' + i));
            sp.transform.SetParent(root, false);
            sp.transform.position = SpawnBase + offsets[i];
            sp.transform.rotation = Quaternion.identity; // +Z = 제단 방향
            sp.AddComponent<NetworkStartPosition>();
        }
    }

    /// <summary>
    /// 대기실의 NetworkBootstrap을 구성한다.
    /// Mirror의 NetworkManager는 씬 전환 시 유지되므로, 여기 있는 것이 게임 내내 쓰인다.
    /// </summary>
    private static void SetupNetworkBootstrap()
    {
        NetworkPhase0Setup.CreateEmptyNetworkBootstrap();

        GameObject bootstrap = GameObject.Find("NetworkBootstrap");
        if (bootstrap == null)
        {
            Debug.LogError("[Lobby] NetworkBootstrap 생성 실패 — Mirror가 임포트되어 있는지 확인하세요.");
            return;
        }

        if (bootstrap.GetComponent<NetworkAutoLaunch>() == null)
            bootstrap.AddComponent<NetworkAutoLaunch>();

        var manager = bootstrap.GetComponent<NetworkManager>();
        if (manager == null)
            return;

        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (playerPrefab == null)
        {
            Debug.LogWarning("[Lobby] " + PlayerPrefabPath + " 가 없습니다. " +
                             "NetworkDemo 씬에서 'Phase 1 > Setup Player Spawning'을 먼저 실행하세요.");
            return;
        }

        manager.playerPrefab = playerPrefab;
        manager.autoCreatePlayer = true;
        manager.playerSpawnMethod = PlayerSpawnMethod.RoundRobin;
        manager.maxConnections = MaxPlayers; // 대기실의 "Member n/5" 표시가 이 값을 쓴다
        EditorUtility.SetDirty(manager);
    }

    private static void SetupLobbyManager()
    {
        var go = new GameObject("LobbyManager");
        go.AddComponent<NetworkIdentity>();
        var lobby = go.AddComponent<LobbyManager>();

        var so = new SerializedObject(lobby);
        so.FindProperty("firstStageScene").stringValue = FirstStageScene;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(lobby);
    }

    // ---------------------------------------------------------------- 씬 흐름 배선

    /// <summary>타이틀 화면이 대기실로 넘어가도록 MainMenu의 대상 씬을 고친다.</summary>
    private static void PatchTitleTarget()
    {
        if (!System.IO.File.Exists(TitlePath))
        {
            Debug.LogWarning("[Lobby] " + TitlePath + " 가 없습니다. " +
                             "'Tools > Title > Setup Main Title'을 먼저 실행하면 타이틀에서 대기실로 연결됩니다.");
            return;
        }

        var title = EditorSceneManager.OpenScene(TitlePath, OpenSceneMode.Single);
        var menu = Object.FindFirstObjectByType<MainMenu>(FindObjectsInactive.Include);
        if (menu == null)
        {
            Debug.LogWarning("[Lobby] MainTitle 씬에 MainMenu가 없습니다. 타이틀 연결을 건너뜁니다.");
            return;
        }

        var so = new SerializedObject(menu);
        SerializedProperty prop = so.FindProperty("lobbyScene");
        if (prop == null)
        {
            Debug.LogWarning("[Lobby] MainTitle 씬이 옛 구조입니다. 'Tools > Title > Setup Main Title'로 다시 만드세요.");
            return;
        }

        if (prop.stringValue != LobbySceneName)
        {
            prop.stringValue = LobbySceneName;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(menu);
            EditorSceneManager.MarkSceneDirty(title);
            EditorSceneManager.SaveScene(title);
            Debug.Log("[Lobby] 타이틀의 이동 대상 씬을 Lobby로 변경했습니다.");
        }
    }

    private static void SetBuildOrder()
    {
        var order = new List<string>();
        if (System.IO.File.Exists(TitlePath)) order.Add(TitlePath);
        order.Add(LobbyPath);
        if (System.IO.File.Exists(DemoPath)) order.Add(DemoPath);
        if (System.IO.File.Exists(Stage2Path)) order.Add(Stage2Path);

        var scenes = order.Select(path => new EditorBuildSettingsScene(path, true)).ToList();
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            if (!order.Contains(s.path))
                scenes.Add(s);

        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // ---------------------------------------------------------------- 유틸

    private static void AddSun()
    {
        var go = new GameObject("Directional Light");
        go.transform.rotation = Quaternion.Euler(46f, -25f, 0f);
        var light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.97f, 0.88f);
        light.intensity = 1.15f;
        light.shadows = LightShadows.Soft;
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

    /// <summary>
    /// 바닥에 깔리는 납작한 원판. 바닥면이 부모의 y=0에 오도록 놓는다.
    /// 납작한 실린더의 CapsuleCollider는 지름만 한 거대한 구가 되어 길을 막으므로 제거한다
    /// (높이가 몇 cm뿐이라 플레이어는 그냥 위를 지나다닌다).
    /// </summary>
    private static GameObject Disc(Transform parent, string name, float diameter, float height, Material mat)
    {
        GameObject go = Prim(parent, name, PrimitiveType.Cylinder,
            new Vector3(0f, height * 0.5f, 0f), new Vector3(diameter, height * 0.5f, diameter), mat);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

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
        matGrass = GetMat("Camp_Grass", new Color(0.30f, 0.5f, 0.22f));
        matDirt = GetMat("Camp_Dirt", new Color(0.45f, 0.36f, 0.24f));
        matWood = GetMat("Camp_Wood", new Color(0.5f, 0.33f, 0.18f));
        matStone = GetMat("Camp_Stone", new Color(0.5f, 0.5f, 0.52f));
        matDark = GetMat("Camp_Dark", new Color(0.13f, 0.13f, 0.14f));
        matFlame = GetMat("Camp_Flame", new Color(1f, 0.55f, 0.15f));
        matLeaf = GetMat("Camp_Leaf", new Color(0.22f, 0.45f, 0.2f));
        matAccent = GetMat("Camp_Accent", new Color(0.8f, 0.3f, 0.2f));
        matStraw = GetMat("Lobby_Straw", new Color(0.82f, 0.72f, 0.38f));

        // 참치는 집(스테이지1)의 것과 같은 색을 쓴다.
        var houseTuna = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/House_Tuna.mat");
        matTuna = houseTuna != null ? houseTuna : GetMat("Lobby_Tuna", new Color(0.55f, 0.65f, 0.72f));
    }

    private static Material GetMat(string assetName, Color color) => BuilderMaterials.Ensure(assetName, color);
}
#endif
