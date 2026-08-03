#if UNITY_EDITOR
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

    private static Material matGrass, matGrassDark, matGrassLight, matDirt;
    private static Material matWood, matWoodDark, matStone, matMetal, matDark;
    private static Material matTent, matTentDark, matFlame, matEmber;
    private static Material matLeaf, matLeafDark, matLeafLight;
    private static Material matAccent, matWater, matRope;

    [MenuItem("Tools/Stage/Build Stage 2 (Campground)")]
    public static void BuildStage2()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        LoadMaterials();

        AddSun();

        // 배치가 매번 같도록 시드를 고정한다. 모든 클라이언트가 같은 씬 파일을 쓰므로
        // 실제로는 씬에 구워지지만, 다시 빌드해도 맵이 뒤바뀌지 않게 하려는 목적.
        Random.InitState(2026);

        var root = new GameObject("Campground").transform;

        Terrain(root);
        Lake(root, new Vector3(15f, 0f, 13f), 7.5f);

        Campfire(root, new Vector3(0f, 0f, 0f));
        Tripod(root, new Vector3(0f, 0f, 0f));
        LogSeats(root, new Vector3(0f, 0f, 0f), 2.4f);

        Tent(root, new Vector3(-8f, 0f, -3f), 25f, matTent);
        Tent(root, new Vector3(8f, 0f, -3.5f), -25f, matAccent);
        Tent(root, new Vector3(-2f, 0f, 11f), 190f, matTent);

        PicnicTable(root, new Vector3(7f, 0f, 4f));
        Cooler(root, new Vector3(9.2f, 0f, 4.5f));
        CampChair(root, new Vector3(-3.6f, 0f, 3.4f), 130f);
        CampChair(root, new Vector3(3.4f, 0f, -3.2f), -30f);
        Clothesline(root, new Vector3(-6.5f, 0f, 3.5f), new Vector3(-1.5f, 0f, 6.5f));
        WoodPile(root, new Vector3(-5.2f, 0f, -1.2f), 20f);
        Signpost(root, new Vector3(1.8f, 0f, -12.5f), 8f);

        Forest(root);
        Scatter(root);

        // 지형/숲/잡목은 오브젝트 수가 많다. 움직이지 않으므로 static으로 묶어
        // 배칭과 라이트맵 대상이 되게 한다. (무기·준비물은 움직이므로 제외)
        MarkStatic(root, "Terrain", "Lake", "Forest", "Scatter");

        // 준비물 (캠핑장 세팅 목표)
        Collectible(root, "장작", new Vector3(2.2f, 0.15f, 1.2f), FirewoodModel);
        Collectible(root, "마시멜로", new Vector3(-2.2f, 0.1f, 1.0f), MarshmallowModel);
        Collectible(root, "부탄가스", new Vector3(7f, 0.85f, 3.6f), ButaneModel);
        Collectible(root, "랜턴", new Vector3(-6.5f, 2.05f, 3.5f), LanternModel);   // 빨랫줄 기둥에 걸어 둠
        Collectible(root, "낚싯대", new Vector3(9.4f, 0.32f, 9.25f), FishingRodModel); // 호수 잔교 위

        // 무기 — .blend에서 가져온 음식 무기들을 캠핑장 곳곳에 흩뿌린다.
        PlaceWeapons(root);

        // 스폰 포인트
        SpawnPoints(new Vector3(0f, 1f, -14f));

        // 매니저(체크리스트/무기) + ID 부여 — 기존 셋업 로직 재사용
        NetworkPhase3Setup.SetupChecklistSync();
        NetworkPhase4Setup.SetupWeaponSync();

        // 탈출존: 캠핑장을 마치면 대기실로 돌아간다 (대기실 → 캠핑장 → 대기실 순환).
        var exit = NetworkPhase6Setup.CreateEscapeZone(
            new Vector3(0f, 1.5f, 24f), new Vector3(4f, 3f, 3f), LobbyScene, false, LobbyScene);
        Box(exit.transform, "ExitSign", new Vector3(0f, 1.2f, 0.2f), new Vector3(3f, 0.05f, 0.05f), matWood);

        EditorSceneManager.SaveScene(scene, ScenePath);
        NetworkPhase6Setup.EnsureSceneInBuildSettings("Assets/Scenes/Lobby.unity");
        NetworkPhase6Setup.EnsureSceneInBuildSettings(ScenePath);

        Debug.Log("[Stage2] 캠핑장 스테이지 생성 완료: " + ScenePath +
                  "\n- 무기가 안 보이면 'Tools > Weapons > Build Weapon Prefabs'를 먼저 실행하고 다시 만드세요.");
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

        Box(g, "Ground", new Vector3(0f, -0.5f, 0f), new Vector3(70f, 1f, 70f), matGrass);

        // 잔디 색 패치 — 단색 평면의 밋밋함을 깨는 용도.
        for (int i = 0; i < 26; i++)
        {
            var pos = new Vector3(Random.Range(-30f, 30f), 0.01f, Random.Range(-30f, 30f));
            float s = Random.Range(3.5f, 9f);
            Prim(g, "GrassPatch", PrimitiveType.Cylinder, pos, new Vector3(0f, Random.value * 360f, 0f),
                new Vector3(s, 0.005f, s * Random.Range(0.7f, 1.3f)), i % 2 == 0 ? matGrassDark : matGrassLight);
        }

        // 캠프로 들어오는 흙길 (스폰 지점 → 모닥불 → 출구)
        Box(g, "Path", new Vector3(0f, 0.02f, 4f), new Vector3(3.4f, 0.04f, 40f), matDirt);
        Box(g, "PathSide", new Vector3(9f, 0.02f, 9f), new Vector3(14f, 0.04f, 2.6f), matDirt);
        Prim(g, "FirePad", PrimitiveType.Cylinder, new Vector3(0f, 0.025f, 0f), new Vector3(6.4f, 0.02f, 6.4f), matDirt);

        // 바깥 둔덕 — 낮은 언덕처럼 보이면서 실제로는 벽 역할을 한다.
        for (int i = 0; i < 40; i++)
        {
            float a = i / 40f * Mathf.PI * 2f;
            float r = 31f;
            var pos = new Vector3(Mathf.Cos(a) * r, Random.Range(1.2f, 2.2f), Mathf.Sin(a) * r);
            Prim(g, "Berm", PrimitiveType.Cube, pos, new Vector3(0f, a * Mathf.Rad2Deg, Random.Range(-8f, 8f)),
                new Vector3(6f, Random.Range(3f, 4.5f), 6f), matGrassDark);
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

    private static void Campfire(Transform p, Vector3 pos)
    {
        Transform g = Group(p, "Campfire", pos, 0f);

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

        // 원뿔로 세운 장작
        for (int i = 0; i < 5; i++)
        {
            float a = i / 5f * Mathf.PI * 2f;
            Prim(g, "Log", PrimitiveType.Cylinder,
                new Vector3(Mathf.Cos(a) * 0.16f, 0.3f, Mathf.Sin(a) * 0.16f),
                new Vector3(Mathf.Sin(a) * 22f, 0f, -Mathf.Cos(a) * 22f),
                new Vector3(0.12f, 0.42f, 0.12f), matWoodDark);
        }

        Prim(g, "Flame", PrimitiveType.Sphere, new Vector3(0f, 0.55f, 0f), new Vector3(0.44f, 0.72f, 0.44f), matFlame);
        Prim(g, "FlameTip", PrimitiveType.Sphere, new Vector3(0f, 0.92f, 0f), new Vector3(0.22f, 0.36f, 0.22f), matEmber);

        var lgo = new GameObject("FireLight");
        lgo.transform.SetParent(g, false);
        lgo.transform.localPosition = new Vector3(0f, 0.8f, 0f);
        var l = lgo.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1f, 0.6f, 0.25f);
        l.range = 16f;
        l.intensity = 3.5f;
        l.shadows = LightShadows.Soft;
    }

    /// <summary>모닥불 위 삼각대와 주전자.</summary>
    private static void Tripod(Transform p, Vector3 pos)
    {
        Transform g = Group(p, "Tripod", pos, 0f);
        for (int i = 0; i < 3; i++)
        {
            float a = i / 3f * Mathf.PI * 2f;
            Prim(g, "Leg", PrimitiveType.Cylinder,
                new Vector3(Mathf.Cos(a) * 0.55f, 0.9f, Mathf.Sin(a) * 0.55f),
                new Vector3(Mathf.Sin(a) * 18f, 0f, -Mathf.Cos(a) * 18f),
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

        GameObject l = Box(g, "SideL", new Vector3(-0.62f, 0.95f, 0f), new Vector3(0.08f, 2.2f, 3.1f), mat);
        GameObject r = Box(g, "SideR", new Vector3(0.62f, 0.95f, 0f), new Vector3(0.08f, 2.2f, 3.1f), mat);
        l.transform.localRotation = Quaternion.Euler(0f, 0f, 32f);
        r.transform.localRotation = Quaternion.Euler(0f, 0f, -32f);

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

        for (int ring = 0; ring < 3; ring++)
        {
            int count = 26 - ring * 4;
            float radius = 20f + ring * 4.5f;
            for (int i = 0; i < count; i++)
            {
                float a = (i + Random.Range(-0.35f, 0.35f)) / count * Mathf.PI * 2f;
                var at = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (radius + Random.Range(-1.5f, 1.5f));
                if (Random.value < 0.55f)
                    PineTree(g, at, Random.Range(0.85f, 1.5f));
                else
                    BroadTree(g, at, Random.Range(0.8f, 1.35f));
            }
        }

        // 캠프 안쪽에 드문드문 — 은폐물이자 시야 차단.
        Vector3[] inner =
        {
            new Vector3(-12f, 0f, 6f), new Vector3(12.5f, 0f, -8f), new Vector3(-10f, 0f, -9f),
            new Vector3(4f, 0f, 15f), new Vector3(-14f, 0f, 14f), new Vector3(15f, 0f, 2f),
        };
        foreach (Vector3 at in inner)
        {
            if (Random.value < 0.5f)
                PineTree(g, at, Random.Range(1f, 1.4f));
            else
                BroadTree(g, at, Random.Range(0.9f, 1.2f));
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

        for (int i = 0; i < 55; i++)
        {
            var at = new Vector3(Random.Range(-27f, 27f), 0f, Random.Range(-27f, 27f));
            if (at.magnitude < 5.5f)
                continue; // 모닥불 주변은 비워 둔다

            float roll = Random.value;
            if (roll < 0.35f)
            {
                var rock = Prim(g, "Rock", PrimitiveType.Cube, at + Vector3.up * 0.15f,
                    new Vector3(Random.value * 40f, Random.value * 360f, Random.value * 40f),
                    new Vector3(Random.Range(0.4f, 1.2f), Random.Range(0.3f, 0.8f), Random.Range(0.4f, 1.2f)),
                    matStone);
                rock.name = "Rock";
            }
            else if (roll < 0.75f)
            {
                Transform b = Group(g, "Bush", at, Random.value * 360f);
                float s = Random.Range(0.5f, 1f);
                var a1 = Prim(b, "Leaf", PrimitiveType.Sphere, new Vector3(0f, s * 0.45f, 0f), Vector3.one * s, matLeafDark);
                var a2 = Prim(b, "Leaf", PrimitiveType.Sphere, new Vector3(s * 0.4f, s * 0.35f, s * 0.2f), Vector3.one * s * 0.75f, matLeaf);
                Object.DestroyImmediate(a1.GetComponent<Collider>());
                Object.DestroyImmediate(a2.GetComponent<Collider>());
            }
            else
            {
                Prim(g, "Stump", PrimitiveType.Cylinder, at + Vector3.up * 0.2f,
                    new Vector3(Random.Range(-6f, 6f), 0f, Random.Range(-6f, 6f)),
                    new Vector3(Random.Range(0.5f, 0.8f), 0.2f, Random.Range(0.5f, 0.8f)), matWoodDark);
            }
        }
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

    private static Mesh ConeMesh()
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
            (new Vector3(-2.8f, 0f, 2.0f), -40f),  // 모닥불 반대편
            (new Vector3(7.0f, 0.82f, 4.0f), 90f), // 식탁 위
            (new Vector3(-7.6f, 0f, -3.2f), 150f), // 왼쪽 텐트 앞
            (new Vector3(8.4f, 0f, -3.4f), -150f), // 오른쪽 텐트 앞
            (new Vector3(0.4f, 0f, 9.2f), 0f),     // 안쪽 텐트 앞
            (new Vector3(-5.0f, 0f, 8.0f), 60f),   // 숲 쪽
            (new Vector3(12.5f, 0f, 4.0f), -70f),  // 호숫가 (물에 빠지지 않게 물가 바깥)
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
    /// 늦은 오후 느낌의 조명. 해를 낮게 깔아 그림자를 길게 뽑고,
    /// 하늘/환경광을 따뜻하게 맞춰 프리미티브 덩어리들이 밋밋해 보이지 않게 한다.
    /// </summary>
    private static void AddSun()
    {
        var go = new GameObject("Directional Light");
        go.transform.rotation = Quaternion.Euler(28f, -35f, 0f);
        var l = go.AddComponent<Light>();
        l.type = LightType.Directional;
        l.color = new Color(1f, 0.91f, 0.75f);
        l.intensity = 1.35f;
        l.shadows = LightShadows.Soft;
        l.shadowStrength = 0.75f;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.55f, 0.66f, 0.82f);
        RenderSettings.ambientEquatorColor = new Color(0.44f, 0.47f, 0.44f);
        RenderSettings.ambientGroundColor = new Color(0.24f, 0.24f, 0.2f);

        // 멀리 있는 숲이 배경으로 녹아들도록 옅은 안개.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.68f, 0.75f, 0.82f);
        RenderSettings.fogStartDistance = 22f;
        RenderSettings.fogEndDistance = 85f;
    }

    private static Transform Collectible(Transform parent, string displayName, Vector3 pos, System.Action<Transform> model)
    {
        Transform g = Group(parent, displayName, pos, 0f);
        var c = g.gameObject.AddComponent<CollectibleItem>();
        c.SetDisplayName(displayName);
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
        return go;
    }

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
    }

    private static Material GetMat(string name, Color color) => BuilderMaterials.Ensure("Camp_" + name, color);
}
#endif
