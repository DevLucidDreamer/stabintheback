#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 메인 타이틀 씬을 만든다. 메뉴: Tools > Title > Setup Main Title
///
/// 화면은 두 겹이다.
///   1) 3D 배경 — 해질녘 캠핑장 디오라마. 색깔별 goshi 4종이 모닥불 주변을 뛰어다닌다.
///   2) UI      — STAB IN THE / BACK 로고 + 게임 시작 / 옵션 / 종료 (TitleUiBuilder가 만든다)
///
/// 배경은 '한 각도에서만 보이는 무대'라서 Stage2 캠핑장과 달리 콜라이더를 전부 뺀다.
/// 캐릭터도 물리 없이 Transform만 움직인다(TitleActor).
///
/// 실제 UI 동작은 MainMenu.cs가 이름으로 계층을 찾아 연결한다 —
/// 오브젝트 이름을 바꾸면 MainMenu의 문자열도 같이 고쳐야 한다.
///
/// Build Settings 순서도 [MainTitle, Lobby, Stage3, Stage4, Stage2]로 맞춘다.
/// </summary>
public static class TitleSceneSetup
{
    private const string TitlePath = "Assets/Scenes/MainTitle.unity";
    private const string LobbyPath = "Assets/Scenes/Lobby.unity";
    private const string Stage3Path = "Assets/Scenes/Stage3_CursedFortress.unity";
    private const string Stage4Path = "Assets/Scenes/Stage4_MagicSwordEscape.unity";
    private const string Stage2Path = "Assets/Scenes/Stage2_Campground.unity";

    /// <summary>모닥불 자리. 캐릭터가 돌아다니는 중심이자 화면 구도의 기준점.</summary>
    private static readonly Vector3 CampCenter = new Vector3(3f, 0f, 2f);

    /// <summary>캐릭터가 뛰어다니는 고리. 이 안에는 통나무 의자 같은 프롭을 두지 않는다.</summary>
    private const float RoamInner = 3.4f;
    private const float RoamOuter = 6.4f;

    // 카메라 — 불이 화면 오른쪽 아래쯤에 오고, 왼쪽 위는 하늘/숲이라 로고가 얹히는 자리가 된다.
    private static readonly Vector3 CameraPosition = new Vector3(-2.2f, 2.9f, -13f);
    private static readonly Vector3 CameraEuler = new Vector3(5f, 7.5f, 0f);
    private const float CameraFov = 40f;

    private static Material matGrass, matGrassDark, matGrassLight, matDirt;
    private static Material matWood, matWoodDark, matStone, matMetal, matDark;
    private static Material matTent, matTentDark, matFlame, matEmber;
    private static Material matLeaf, matLeafDark, matLeafLight;
    private static Material matAccent, matWater, matRope;
    private static Material matGlow;

    [MenuItem("Tools/Title/Setup Main Title")]
    public static void SetupMainTitle()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        // 타이틀에서 넘어온 의도(호스트/참가)를 실행할 씬들에 자동 시작 + 방 알림을 보장한다.
        EnsureLaunchWiring(LobbyPath);

        // 새 씬을 만들기 전에 끝내 둘 것들 — 둘 다 재임포트/애셋 생성을 일으킨다.
        TMPro.TMP_FontAsset font = JalnanFontAssetBuilder.Ensure();
        List<TitleActorSetup.Actor> actors = TitleActorSetup.PrepareAll();

        var title = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        LoadMaterials();
        Random.InitState(1207); // 나무·돌 배치가 매번 같도록

        BuildCamera();
        BuildLighting();

        // 배치 규칙: 캐릭터가 뛰어다니는 고리(모닥불에서 RoamInner~RoamOuter)는 비워 둔다.
        // 콜라이더가 없어서 프롭을 그 안에 두면 몸이 통과해 버린다.
        // 불 바로 옆(고리 안쪽)이나 고리 바깥에만 놓는다.
        var camp = new GameObject("Camp").transform;
        Terrain(camp);
        Lake(camp, new Vector3(15.5f, 0f, 12.5f), 6.5f);
        Campfire(camp, CampCenter);
        Tripod(camp, CampCenter);
        LogSeats(camp, CampCenter, 2.4f);
        CampChair(camp, CampCenter + new Vector3(2.7f, 0f, 0f), -90f);
        CampChair(camp, CampCenter + new Vector3(-2.7f, 0f, 0f), 90f);
        Tent(camp, new Vector3(-2.5f, 0f, 7.5f), 18f, matTent);
        Tent(camp, new Vector3(9.5f, 0f, 6f), -32f, matAccent);
        Tent(camp, new Vector3(12f, 0f, 5.5f), -72f, matTent);
        PicnicTable(camp, new Vector3(6f, 0f, 9f), 14f);
        Cooler(camp, new Vector3(7.6f, 0f, 9.2f));
        WoodPile(camp, new Vector3(0f, 0f, 8.5f), 22f);
        LanternString(camp, new Vector3(-2f, 0f, 7.5f), new Vector3(7.5f, 0f, 9f));
        Forest(camp);
        Scatter(camp);
        Fireflies(camp);

        BuildActors(actors);
        TitleUiBuilder.Build(font);

        EditorSceneManager.SaveScene(title, TitlePath);
        SetBuildOrder();

        // TMP 다이나믹 폰트는 화면에 쓰인 한글을 그때그때 아틀라스에 굽는다.
        // 여기서 저장해 두지 않으면 방금 구운 글리프가 날아간다.
        AssetDatabase.SaveAssets();

        Debug.Log($"[Title] 메인 타이틀 씬을 새로 만들었습니다 (캐릭터 {actors.Count}종).\n" +
                  "- Build Settings 첫 씬 = MainTitle, 그 다음이 Lobby(대기실)입니다.\n" +
                  "- 대기실 씬이 없다면 'Tools > Lobby > Build Lobby'를 먼저 실행하세요.\n" +
                  "- '빠른 참가'는 같은 공유기 안에서만 방을 찾습니다(UDP " + LanRoomBeacon.Port + ").");
    }

    /// <summary>해당 씬의 NetworkBootstrap에 자동 시작 + LAN 방 알림이 붙어 있게 한다.</summary>
    private static void EnsureLaunchWiring(string scenePath)
    {
        if (!System.IO.File.Exists(scenePath))
            return;

        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        GameObject bootstrap = GameObject.Find("NetworkBootstrap");
        if (bootstrap == null)
        {
            Debug.LogWarning($"[Title] {scenePath} 에 NetworkBootstrap이 없습니다. 자동 시작 연결을 건너뜁니다.");
            return;
        }

        bool changed = false;

        if (bootstrap.GetComponent<NetworkAutoLaunch>() == null)
        {
            bootstrap.AddComponent<NetworkAutoLaunch>();
            changed = true;
        }

        // 호스트가 대기실에 있는 동안 방을 알린다 → 타이틀의 '빠른 참가'가 이걸 찾는다.
        if (bootstrap.GetComponent<LanRoomAdvertiser>() == null)
        {
            bootstrap.AddComponent<LanRoomAdvertiser>();
            changed = true;
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    // ---------------------------------------------------------------- 카메라 / 조명

    private static void BuildCamera()
    {
        var go = new GameObject("Main Camera");
        go.transform.position = CameraPosition;
        go.transform.rotation = Quaternion.Euler(CameraEuler);
        go.tag = "MainCamera";

        var cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.backgroundColor = new Color(0.35f, 0.45f, 0.58f);
        cam.fieldOfView = CameraFov;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 200f;

        go.AddComponent<AudioListener>();
        go.AddComponent<TitleCameraDrift>();
    }

    /// <summary>
    /// 해질녘 직전의 낮은 볕. 캐릭터가 화면 쪽을 보고 있을 때 얼굴이 어둡지 않도록
    /// 해를 카메라 뒤쪽에 두고, 그림자만 길게 뽑는다.
    /// </summary>
    private static void BuildLighting()
    {
        var go = new GameObject("Directional Light");
        go.transform.rotation = Quaternion.Euler(24f, 35f, 0f);

        var sun = go.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.88f, 0.70f);
        sun.intensity = 1.4f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.72f;

        RenderSettings.sun = sun;
        RenderSettings.skybox = EnsureSky();

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.52f, 0.62f, 0.80f);
        RenderSettings.ambientEquatorColor = new Color(0.50f, 0.45f, 0.38f);
        RenderSettings.ambientGroundColor = new Color(0.22f, 0.20f, 0.17f);

        // 멀리 있는 숲이 하늘로 녹아들도록 옅은 안개. 배경에 깊이를 준다.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.74f, 0.76f, 0.78f);
        RenderSettings.fogStartDistance = 26f;
        RenderSettings.fogEndDistance = 95f;
    }

    private static Material EnsureSky()
    {
        const string path = "Assets/Materials/Title_Sky.mat";

        var sky = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (sky == null)
        {
            Shader shader = Shader.Find("Skybox/Procedural");
            if (shader == null)
            {
                Debug.LogWarning("[Title] Skybox/Procedural 셰이더를 찾지 못했습니다. 기본 하늘로 둡니다.");
                return RenderSettings.skybox;
            }

            sky = new Material(shader);
            AssetDatabase.CreateAsset(sky, path);
        }

        sky.SetFloat("_SunSize", 0.05f);
        sky.SetFloat("_AtmosphereThickness", 1.35f);
        sky.SetColor("_SkyTint", new Color(0.60f, 0.66f, 0.80f));
        sky.SetColor("_GroundColor", new Color(0.42f, 0.37f, 0.31f));
        sky.SetFloat("_Exposure", 1.25f);
        EditorUtility.SetDirty(sky);

        return sky;
    }

    // ---------------------------------------------------------------- 지형 / 프롭

    private static void Terrain(Transform p)
    {
        Transform g = Group(p, "Terrain", Vector3.zero, 0f);

        Box(g, "Ground", new Vector3(CampCenter.x, -0.5f, CampCenter.z), new Vector3(120f, 1f, 120f), matGrass);

        // 잔디 색 패치 — 단색 평면의 밋밋함을 깬다.
        for (int i = 0; i < 34; i++)
        {
            Vector3 pos = CampCenter + new Vector3(Random.Range(-26f, 26f), 0.01f, Random.Range(-20f, 26f));
            float s = Random.Range(3.5f, 10f);
            Prim(g, "GrassPatch", PrimitiveType.Cylinder, pos, new Vector3(0f, Random.value * 360f, 0f),
                new Vector3(s, 0.005f, s * Random.Range(0.7f, 1.3f)), i % 2 == 0 ? matGrassDark : matGrassLight);
        }

        Prim(g, "FirePad", PrimitiveType.Cylinder, CampCenter + new Vector3(0f, 0.025f, 0f),
            new Vector3(7f, 0.02f, 7f), matDirt);
        Box(g, "Path", CampCenter + new Vector3(-1f, 0.02f, -8f), new Vector3(3.2f, 0.04f, 22f), matDirt);
    }

    private static void Lake(Transform p, Vector3 pos, float radius)
    {
        Transform g = Group(p, "Lake", pos, 0f);

        Prim(g, "Bed", PrimitiveType.Cylinder, new Vector3(0f, -0.25f, 0f),
            new Vector3(radius * 2f, 0.25f, radius * 2f), matDirt);
        Prim(g, "Water", PrimitiveType.Cylinder, new Vector3(0f, -0.05f, 0f),
            new Vector3(radius * 2f - 0.4f, 0.06f, radius * 2f - 0.4f), matWater);

        for (int i = 0; i < 20; i++)
        {
            float a = i / 20f * Mathf.PI * 2f;
            float r = radius + Random.Range(-0.2f, 0.35f);
            Prim(g, "Rock", PrimitiveType.Cube, new Vector3(Mathf.Cos(a) * r, 0.05f, Mathf.Sin(a) * r),
                new Vector3(Random.value * 60f, Random.value * 360f, Random.value * 40f),
                Vector3.one * Random.Range(0.4f, 0.9f), matStone);
        }
    }

    private static void Campfire(Transform p, Vector3 pos)
    {
        Transform g = Group(p, "Campfire", pos, 0f);

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

        for (int i = 0; i < 5; i++)
        {
            float a = i / 5f * Mathf.PI * 2f;
            Prim(g, "Log", PrimitiveType.Cylinder,
                new Vector3(Mathf.Cos(a) * 0.16f, 0.3f, Mathf.Sin(a) * 0.16f),
                LeanInward(a, 22f),
                new Vector3(0.12f, 0.42f, 0.12f), matWoodDark);
        }

        GameObject flame = Prim(g, "Flame", PrimitiveType.Sphere, new Vector3(0f, 0.58f, 0f),
            new Vector3(0.46f, 0.78f, 0.46f), matFlame);
        Prim(g, "FlameTip", PrimitiveType.Sphere, new Vector3(0f, 0.96f, 0f),
            new Vector3(0.22f, 0.36f, 0.22f), matEmber);

        var lightGo = new GameObject("FireLight");
        lightGo.transform.SetParent(g, false);
        lightGo.transform.localPosition = new Vector3(0f, 0.85f, 0f);

        var fire = lightGo.AddComponent<Light>();
        fire.type = LightType.Point;
        fire.color = new Color(1f, 0.58f, 0.24f);
        fire.range = 18f;
        fire.intensity = 3.4f;
        fire.shadows = LightShadows.Soft;

        var flicker = lightGo.AddComponent<TitleFireFlicker>();
        SetField(flicker, "flame", flame.transform);
    }

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

    private static void Tent(Transform p, Vector3 pos, float rotY, Material mat)
    {
        Transform g = Group(p, "Tent", pos, rotY);

        Box(g, "Floor", new Vector3(0f, 0.06f, 0f), new Vector3(2.3f, 0.12f, 3f), matDark);

        GameObject l = Box(g, "SideL", new Vector3(-0.62f, 0.95f, 0f), new Vector3(0.08f, 2.2f, 3.1f), mat);
        GameObject r = Box(g, "SideR", new Vector3(0.62f, 0.95f, 0f), new Vector3(0.08f, 2.2f, 3.1f), mat);
        l.transform.localRotation = Quaternion.Euler(0f, 0f, -32f);
        r.transform.localRotation = Quaternion.Euler(0f, 0f, 32f);

        Box(g, "Back", new Vector3(0f, 0.85f, -1.52f), new Vector3(2.2f, 1.7f, 0.06f), mat);

        GameObject flap = Box(g, "Flap", new Vector3(-0.72f, 0.8f, 1.55f), new Vector3(1.1f, 1.6f, 0.05f), matTentDark);
        flap.transform.localRotation = Quaternion.Euler(0f, 42f, 12f);

        Box(g, "Ridge", new Vector3(0f, 1.86f, 0f), new Vector3(0.07f, 0.07f, 3.3f), matWoodDark);
        Guy(g, new Vector3(-1.35f, 0f, 1.7f), new Vector3(-0.55f, 1.7f, 1.5f));
        Guy(g, new Vector3(1.35f, 0f, 1.7f), new Vector3(0.55f, 1.7f, 1.5f));
        Guy(g, new Vector3(-1.35f, 0f, -1.7f), new Vector3(-0.55f, 1.7f, -1.5f));
        Guy(g, new Vector3(1.35f, 0f, -1.7f), new Vector3(0.55f, 1.7f, -1.5f));
    }

    /// <summary>텐트를 잡아 주는 줄 하나 + 팩.</summary>
    private static void Guy(Transform g, Vector3 peg, Vector3 anchor)
    {
        Vector3 mid = (peg + anchor) * 0.5f;
        Vector3 dir = anchor - peg;

        GameObject rope = Prim(g, "Guy", PrimitiveType.Cylinder, mid, Vector3.zero,
            new Vector3(0.02f, dir.magnitude * 0.5f, 0.02f), matRope);
        rope.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);

        Prim(g, "Peg", PrimitiveType.Cylinder, peg + Vector3.up * 0.08f, Vector3.zero,
            new Vector3(0.05f, 0.1f, 0.05f), matWoodDark);
    }

    private static void PicnicTable(Transform p, Vector3 pos, float rotY)
    {
        Transform g = Group(p, "PicnicTable", pos, rotY);

        Box(g, "Top", new Vector3(0f, 0.76f, 0f), new Vector3(1.5f, 0.09f, 2.4f), matWood);
        Box(g, "BenchL", new Vector3(-1.05f, 0.44f, 0f), new Vector3(0.42f, 0.08f, 2.2f), matWood);
        Box(g, "BenchR", new Vector3(1.05f, 0.44f, 0f), new Vector3(0.42f, 0.08f, 2.2f), matWood);

        foreach (float z in new[] { -0.95f, 0.95f })
        {
            Box(g, "LegL", new Vector3(-0.62f, 0.38f, z), new Vector3(0.1f, 0.76f, 0.1f), matWoodDark);
            Box(g, "LegR", new Vector3(0.62f, 0.38f, z), new Vector3(0.1f, 0.76f, 0.1f), matWoodDark);
            Box(g, "Cross", new Vector3(0f, 0.42f, z), new Vector3(2.4f, 0.07f, 0.07f), matWoodDark);
        }

        // 테이블 위 소품 — 실루엣에 잔재미를 준다.
        Prim(g, "Kettle", PrimitiveType.Cylinder, new Vector3(-0.2f, 0.9f, 0.5f), new Vector3(0.3f, 0.15f, 0.3f), matMetal);
        Box(g, "Box", new Vector3(0.35f, 0.88f, -0.4f), new Vector3(0.34f, 0.22f, 0.26f), matAccent);
        Prim(g, "Cup", PrimitiveType.Cylinder, new Vector3(0.1f, 0.85f, -0.9f), new Vector3(0.12f, 0.09f, 0.12f), matEmber);
    }

    private static void Cooler(Transform p, Vector3 pos)
    {
        Transform g = Group(p, "Cooler", pos, 18f);
        Box(g, "Body", new Vector3(0f, 0.22f, 0f), new Vector3(0.75f, 0.44f, 0.5f), matAccent);
        Box(g, "Lid", new Vector3(0f, 0.47f, 0f), new Vector3(0.79f, 0.07f, 0.54f), matMetal);
    }

    private static void CampChair(Transform p, Vector3 pos, float rotY)
    {
        Transform g = Group(p, "CampChair", pos, rotY);

        Box(g, "Seat", new Vector3(0f, 0.42f, 0f), new Vector3(0.6f, 0.07f, 0.6f), matTentDark);
        GameObject back = Box(g, "Back", new Vector3(0f, 0.72f, -0.28f), new Vector3(0.6f, 0.6f, 0.06f), matTentDark);
        back.transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);

        foreach (float x in new[] { -0.26f, 0.26f })
        foreach (float z in new[] { -0.26f, 0.26f })
            Prim(g, "Leg", PrimitiveType.Cylinder, new Vector3(x, 0.21f, z),
                new Vector3(z > 0f ? 8f : -8f, 0f, x > 0f ? -8f : 8f),
                new Vector3(0.04f, 0.21f, 0.04f), matMetal);
    }

    private static void WoodPile(Transform p, Vector3 pos, float rotY)
    {
        Transform g = Group(p, "WoodPile", pos, rotY);

        for (int row = 0; row < 3; row++)
        for (int i = 0; i < 4 - row; i++)
        {
            Prim(g, "Log", PrimitiveType.Cylinder,
                new Vector3((i - (3 - row) * 0.5f) * 0.28f + 0.14f, 0.14f + row * 0.26f, 0f),
                new Vector3(90f, Random.Range(-4f, 4f), 0f),
                new Vector3(0.13f, 0.55f, 0.13f), row % 2 == 0 ? matWood : matWoodDark);
        }
    }

    /// <summary>
    /// 나무 기둥 두 개에 걸린 전구 줄. 해질녘 캠핑장의 분위기를 만드는 핵심 소품이라
    /// 전구는 발광 머티리얼을 쓰고 중간에 작은 점광원 하나를 넣어 준다.
    /// </summary>
    private static void LanternString(Transform p, Vector3 a, Vector3 b)
    {
        Transform g = Group(p, "LanternString", Vector3.zero, 0f);

        const float height = 2.6f;
        Prim(g, "PoleA", PrimitiveType.Cylinder, a + Vector3.up * height * 0.5f, Vector3.zero,
            new Vector3(0.09f, height * 0.5f, 0.09f), matWoodDark);
        Prim(g, "PoleB", PrimitiveType.Cylinder, b + Vector3.up * height * 0.5f, Vector3.zero,
            new Vector3(0.09f, height * 0.5f, 0.09f), matWoodDark);

        Vector3 top1 = a + Vector3.up * height;
        Vector3 top2 = b + Vector3.up * height;

        // 줄은 아래로 처지므로 여러 토막으로 나눠 포물선을 흉내 낸다.
        const int segments = 8;
        const float sag = 0.5f;
        Vector3 previous = top1;
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 point = Vector3.Lerp(top1, top2, t);
            point.y -= Mathf.Sin(t * Mathf.PI) * sag;

            Vector3 mid = (previous + point) * 0.5f;
            Vector3 dir = point - previous;
            GameObject seg = Prim(g, "Wire", PrimitiveType.Cylinder, mid, Vector3.zero,
                new Vector3(0.02f, dir.magnitude * 0.5f, 0.02f), matRope);
            seg.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);

            if (i < segments)
                Prim(g, "Bulb", PrimitiveType.Sphere, point - Vector3.up * 0.1f, Vector3.zero,
                    Vector3.one * 0.12f, matGlow);

            previous = point;
        }

        var lightGo = new GameObject("StringLight");
        lightGo.transform.SetParent(g, false);
        lightGo.transform.position = Vector3.Lerp(top1, top2, 0.5f) - Vector3.up * (sag + 0.2f);

        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.84f, 0.55f);
        light.range = 9f;
        light.intensity = 1.6f;
        light.shadows = LightShadows.None;
    }

    /// <summary>
    /// 배경을 막아 주는 숲. 카메라 앞(캠프보다 앞쪽)에는 심지 않는다 —
    /// 화면을 가려 버리기 때문이다. 대신 좌우 끝에만 몇 그루 세워 화면을 액자처럼 잡는다.
    /// </summary>
    private static void Forest(Transform p)
    {
        Transform g = Group(p, "Forest", Vector3.zero, 0f);

        for (int ring = 0; ring < 3; ring++)
        {
            int count = 30 - ring * 4;
            float radius = 17f + ring * 5f;

            for (int i = 0; i < count; i++)
            {
                float a = (i + Random.Range(-0.35f, 0.35f)) / count * Mathf.PI * 2f;
                Vector3 at = CampCenter + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (radius + Random.Range(-1.8f, 1.8f));

                if (at.z < CampCenter.z - 8f)
                    continue; // 카메라와 캠프 사이는 비워 둔다

                if (Random.value < 0.6f)
                    PineTree(g, at, Random.Range(0.9f, 1.6f));
                else
                    BroadTree(g, at, Random.Range(0.85f, 1.4f));
            }
        }

        // 화면 좌우 끝을 잡아 주는 큰 나무 — 액자처럼 걸리면서 깊이감이 확 산다.
        // 카메라 화각이 앞쪽으로 갈수록 좁아지므로 앞에 둘수록 x를 안쪽으로 당겨야 한다.
        PineTree(g, new Vector3(-6.5f, 0f, -2f), 1.55f);
        BroadTree(g, new Vector3(-9f, 0f, 3f), 1.4f);
        BroadTree(g, new Vector3(10.5f, 0f, 2.5f), 1.45f);
        PineTree(g, new Vector3(13f, 0f, 7f), 1.3f);
    }

    private static void PineTree(Transform p, Vector3 pos, float scale)
    {
        Transform g = Group(p, "PineTree", pos, Random.value * 360f);
        g.localScale = Vector3.one * scale;

        Prim(g, "Trunk", PrimitiveType.Cylinder, new Vector3(0f, 1f, 0f), new Vector3(0.42f, 1f, 0.42f), matWoodDark);
        Cone(g, "Canopy1", new Vector3(0f, 1.9f, 0f), 2.5f, 2.0f, matLeafDark);
        Cone(g, "Canopy2", new Vector3(0f, 3.1f, 0f), 1.9f, 1.8f, matLeaf);
        Cone(g, "Canopy3", new Vector3(0f, 4.2f, 0f), 1.2f, 1.5f, matLeafLight);
    }

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

    private static void Scatter(Transform p)
    {
        Transform g = Group(p, "Scatter", Vector3.zero, 0f);

        for (int i = 0; i < 60; i++)
        {
            Vector3 at = CampCenter + new Vector3(Random.Range(-16f, 16f), 0f, Random.Range(-9f, 16f));
            if ((at - CampCenter).magnitude < RoamOuter + 0.8f)
                continue; // 캐릭터가 뛰어다니는 고리는 비워 둔다

            float roll = Random.value;
            if (roll < 0.32f)
            {
                Prim(g, "Rock", PrimitiveType.Cube, at + Vector3.up * 0.15f,
                    new Vector3(Random.value * 40f, Random.value * 360f, Random.value * 40f),
                    new Vector3(Random.Range(0.4f, 1.2f), Random.Range(0.3f, 0.8f), Random.Range(0.4f, 1.2f)),
                    matStone);
            }
            else if (roll < 0.78f)
            {
                Transform b = Group(g, "Bush", at, Random.value * 360f);
                float s = Random.Range(0.5f, 1.1f);
                Prim(b, "Leaf", PrimitiveType.Sphere, new Vector3(0f, s * 0.45f, 0f), Vector3.one * s, matLeafDark);
                Prim(b, "Leaf", PrimitiveType.Sphere, new Vector3(s * 0.4f, s * 0.35f, s * 0.2f), Vector3.one * s * 0.75f, matLeaf);
            }
            else
            {
                Prim(g, "Stump", PrimitiveType.Cylinder, at + Vector3.up * 0.2f,
                    new Vector3(Random.Range(-6f, 6f), 0f, Random.Range(-6f, 6f)),
                    new Vector3(Random.Range(0.5f, 0.8f), 0.2f, Random.Range(0.5f, 0.8f)), matWoodDark);
            }
        }
    }

    private static void Fireflies(Transform p)
    {
        var go = new GameObject("Fireflies");
        go.transform.SetParent(p, false);
        go.transform.position = CampCenter + Vector3.up * 0.2f;

        var fireflies = go.AddComponent<TitleFireflies>();
        SetField(fireflies, "glow", matGlow);
    }

    // ---------------------------------------------------------------- 캐릭터

    /// <summary>
    /// 색깔별 모델을 모닥불 둘레에 흩어 놓고 TitleActor로 돌아다니게 한다.
    /// 모델은 프리팹 연결을 끊고(Unpack) 넣는다 — 씬만 열어도 바로 보이고,
    /// 원본 fbx를 다시 임포트해도 이 씬이 깨지지 않는다.
    /// </summary>
    private static void BuildActors(List<TitleActorSetup.Actor> actors)
    {
        if (actors.Count == 0)
        {
            Debug.LogWarning("[Title] 캐릭터 모델을 찾지 못해 배경에 사람이 없습니다. " +
                             "Assets/Player 안에 goshi 모델이 있는지 확인하세요.");
            return;
        }

        var root = new GameObject("TitleActors").transform;

        // 발바닥이 루트(=지면)에 오도록 프리팹 기준 오프셋을 되돌린다.
        GoshiModel.ReadCapsule(out _, out float feetY);

        for (int i = 0; i < actors.Count; i++)
        {
            TitleActorSetup.Actor actor = actors[i];

            float angle = (i / (float)actors.Count + 0.12f) * Mathf.PI * 2f;
            float radius = Random.Range(RoamInner + 0.3f, RoamOuter);
            Vector3 pos = CampCenter + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

            var go = new GameObject("Actor_" + System.IO.Path.GetFileNameWithoutExtension(actor.ModelPath));
            go.transform.SetParent(root, false);
            go.transform.position = pos;
            // 처음부터 불 쪽을 보고 서 있게 한다.
            go.transform.rotation = Quaternion.LookRotation(new Vector3(CampCenter.x - pos.x, 0f, CampCenter.z - pos.z));

            var model = (GameObject)PrefabUtility.InstantiatePrefab(actor.Model);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = "Model";
            model.transform.SetParent(go.transform, false);

            GoshiModel.PlaceOnFeet(model);
            model.transform.localPosition -= new Vector3(0f, feetY, 0f);

            Animator animator = model.GetComponent<Animator>();
            if (animator == null)
                animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = actor.Controller;
            animator.avatar = actor.Avatar;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // 게임용 애니메이터는 '실제 이동 거리'로 굴러서 여기선 필요 없다.
            var playerAnimator = model.GetComponent<PlayerAnimator>();
            if (playerAnimator != null)
                Object.DestroyImmediate(playerAnimator);

            var titleActor = go.AddComponent<TitleActor>();
            titleActor.Configure(CampCenter, RoamInner, RoamOuter, Random.Range(2.9f, 3.7f));
        }
    }

    // ---------------------------------------------------------------- 헬퍼

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

    /// <summary>배경 프롭 하나. 타이틀 배경은 아무도 부딪히지 않으므로 콜라이더는 전부 버린다.</summary>
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

        Collider collider = go.GetComponent<Collider>();
        if (collider != null)
            Object.DestroyImmediate(collider);

        return go;
    }

    /// <summary>
    /// 중심에서 각도 a 방향에 세운 막대를 위쪽이 가운데로 모이도록 눕히는 회전.
    /// 부호를 뒤집으면 위가 벌어지는 ∨ 모양이 된다(모닥불 장작·삼각대가 그랬다).
    /// </summary>
    private static Vector3 LeanInward(float angle, float degrees)
        => new Vector3(-Mathf.Sin(angle) * degrees, 0f, Mathf.Cos(angle) * degrees);

    private static GameObject Cone(Transform parent, string name, Vector3 localPos, float radius, float height, Material mat)
    {
        GameObject go = Prim(parent, name, PrimitiveType.Cylinder, localPos, Vector3.zero,
            new Vector3(radius, height * 0.5f, radius), mat);
        go.GetComponent<MeshFilter>().sharedMesh = Stage2Builder.ConeMesh();
        return go;
    }

    /// <summary>[SerializeField] private 필드에 값을 넣는다(빌더가 컴포넌트를 배선할 때).</summary>
    private static void SetField(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(field);
        if (property == null)
        {
            Debug.LogWarning($"[Title] {target.GetType().Name}.{field} 를 찾지 못했습니다.");
            return;
        }
        property.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void LoadMaterials()
    {
        matGrass = Camp("Grass", new Color(0.30f, 0.5f, 0.22f));
        matGrassDark = Camp("GrassDark", new Color(0.22f, 0.39f, 0.17f));
        matGrassLight = Camp("GrassLight", new Color(0.42f, 0.6f, 0.28f));
        matDirt = Camp("Dirt", new Color(0.45f, 0.36f, 0.24f));
        matWood = Camp("Wood", new Color(0.5f, 0.33f, 0.18f));
        matWoodDark = Camp("WoodDark", new Color(0.33f, 0.21f, 0.12f));
        matStone = Camp("Stone", new Color(0.5f, 0.5f, 0.52f));
        matTent = Camp("Tent", new Color(0.2f, 0.45f, 0.6f));
        matTentDark = Camp("TentDark", new Color(0.13f, 0.31f, 0.43f));
        matMetal = Camp("Metal", new Color(0.72f, 0.73f, 0.75f));
        matDark = Camp("Dark", new Color(0.13f, 0.13f, 0.14f));
        matFlame = Camp("Flame", new Color(1f, 0.55f, 0.15f));
        matEmber = Camp("Ember", new Color(1f, 0.83f, 0.4f));
        matLeaf = Camp("Leaf", new Color(0.22f, 0.45f, 0.2f));
        matLeafDark = Camp("LeafDark", new Color(0.13f, 0.31f, 0.15f));
        matLeafLight = Camp("LeafLight", new Color(0.36f, 0.58f, 0.26f));
        matAccent = Camp("Accent", new Color(0.8f, 0.3f, 0.2f));
        matWater = Camp("Water", new Color(0.22f, 0.45f, 0.58f));
        matRope = Camp("Rope", new Color(0.72f, 0.66f, 0.5f));

        matGlow = EnsureEmissive("Title_Glow", new Color(1f, 0.84f, 0.5f), 3.5f);
    }

    private static Material Camp(string name, Color color) => BuilderMaterials.Ensure("Camp_" + name, color);

    /// <summary>전구·반딧불처럼 스스로 빛나 보여야 하는 머티리얼.</summary>
    private static Material EnsureEmissive(string name, Color color, float strength)
    {
        Material material = BuilderMaterials.Ensure(name, color);
        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        if (material.HasProperty("_EmissionColor"))
            material.SetColor("_EmissionColor", color * strength);
        EditorUtility.SetDirty(material);
        return material;
    }

    // ---------------------------------------------------------------- Build Settings

    private static void SetBuildOrder()
    {
        var order = new List<string> { TitlePath };
        if (System.IO.File.Exists(LobbyPath)) order.Add(LobbyPath);
        if (System.IO.File.Exists(Stage3Path)) order.Add(Stage3Path);
        if (System.IO.File.Exists(Stage4Path)) order.Add(Stage4Path);
        if (System.IO.File.Exists(Stage2Path)) order.Add(Stage2Path);

        EditorBuildSettings.scenes = order
            .Select(path => new EditorBuildSettingsScene(path, true))
            .ToArray();
    }
}
#endif
