#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 대기실(로비) 씬을 생성하는 에디터 도구.
/// 메뉴: Tools > Lobby > Build Lobby
///
/// 대기실은 출발 전에 모여 서로를 살피는 곳이다. 그래서 세 가지만 또렷하면 된다.
///   · <b>어디에 모이는가</b> — 가운데 원형 광장과 모닥불
///   · <b>무엇을 노리는가</b> — 광장 한복판 좌대 위의 냉동참치(마검)
///   · <b>어디로 나가는가</b> — 북쪽 출발 게이트와 준비 발판
/// 나머지(연습장·텐트·숲)는 그 셋을 둘러싸는 배경이고,
/// 랜턴과 전구 줄이 시선을 광장 안쪽으로 모은다.
///
/// 함께 처리하는 것:
/// - Build Settings 순서를 [MainTitle, Lobby, NetworkDemo, Stage2]로 정리
/// - MainTitle의 MainMenu가 대기실로 넘어가도록 대상 씬을 Lobby로 수정
/// - TMP 기본 폰트를 Jalnan2 SDF로 지정 (런타임 HUD 글자용)
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
    private const string TunaPrefabPath = "Assets/Prefabs/Weapons/Frozen_Tuna.prefab";

    private const string LobbySceneName = "Lobby";

    /// <summary>대기실에서 인원이 다 모이면 출발할 게임 씬.</summary>
    private const string FirstStageScene = "NetworkDemo";

    /// <summary>방 정원의 기본값. 호스트가 대기실에서 Tab을 눌러 바꿀 수 있다.</summary>
    private const int DefaultTargetPlayers = 4;

    /// <summary>정원의 상한. NetworkManager의 접속 상한 초기값으로도 쓴다.</summary>
    private const int MaxPlayers = 8;

    // 주요 좌표 (바닥 윗면 y = 0)
    private const float PlazaRadius = 11f;
    private static readonly Vector3 AltarPos = Vector3.zero;
    private static readonly Vector3 GatePos = new Vector3(0f, 0f, 14.5f);
    private static readonly Vector3 ReadyPadPos = new Vector3(0f, 0f, 11.6f);
    private static readonly Vector3 SpawnBase = new Vector3(0f, 1f, -13f);
    private static readonly Vector3 CampfirePos = new Vector3(-6.4f, 0f, -5.6f);
    private static readonly Vector3 PracticePos = new Vector3(-10.5f, 0f, 4.5f);

    private static Material matGrass, matGrassDark, matDirt, matSand;
    private static Material matPave, matPaveDark, matStone;
    private static Material matWood, matWoodDark, matMetal, matDark;
    private static Material matFlame, matEmber, matGlass;
    private static Material matLeaf, matLeafDark, matLeafLight;
    private static Material matAccent, matTent, matTuna, matStraw;

    [MenuItem("Tools/Lobby/Build Lobby")]
    public static void BuildLobby()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        // 런타임에 만드는 HUD 글자가 한글을 그릴 수 있게 기본 폰트부터 잡는다.
        if (!JalnanFontAssetBuilder.ApplyAsTmpDefault(out string fontProblem))
            Debug.LogWarning("[Lobby] TMP 기본 폰트 지정 실패 — HUD 한글이 깨질 수 있습니다.\n" + fontProblem);

        // 1) 타이틀이 대기실로 넘어가도록 먼저 고쳐 둔다(씬을 오가야 하므로).
        PatchTitleTarget();

        // 2) 대기실 씬 생성
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        LoadMaterials();
        Random.InitState(20260809); // 다시 빌드해도 같은 숲/같은 배치가 나오도록
        AddSun();

        var root = new GameObject("Lobby").transform;

        Ground(root);
        Plaza(root);
        Forest(root);

        Altar(root);
        List<Vector3> lanternTops = LanternRing(root);
        StringLights(root, lanternTops);

        Gate(root);
        PracticeArea(root);
        Campfire(root, CampfirePos);
        Camp(root);
        Signs(root);

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
                  "\n- 정원이 다 차면 " + FirstStageScene + " 으로 출발합니다 (호스트가 Tab으로 정원 조절)." +
                  "\n- 배치를 바꾸려면 이 파일의 좌표를 고치고 다시 실행하세요.");
    }

    /// <summary>
    /// 기존 대기실의 배치는 유지한 채 냉동참치와 씬 흐름만 최신 구성으로 갱신한다.
    /// 전체 빌더를 다시 돌릴 필요가 없는 콘텐츠 마이그레이션용 메뉴다.
    /// </summary>
    [MenuItem("Tools/Lobby/Apply Lobby Content Upgrade")]
    public static void UpgradeExistingLobby()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.OpenScene(LobbyPath, OpenSceneMode.Single);
        LobbyAltar altar = Object.FindFirstObjectByType<LobbyAltar>();
        if (altar == null)
        {
            Debug.LogError("[Lobby] TunaAltar/LobbyAltar를 찾지 못했습니다. 대기실 전체 빌더를 실행하세요.");
            return;
        }

        Weapon oldWeapon = altar.GetComponentInChildren<Weapon>(true);
        Transform tuna = InstantiateFrozenTuna(altar.transform, new Vector3(0f, 1.18f, 0f), 90f);
        if (tuna == null)
            return;

        if (oldWeapon != null)
            Object.DestroyImmediate(oldWeapon.gameObject);

        altar.Configure(tuna.GetComponent<Weapon>());

        LobbyManager lobby = Object.FindFirstObjectByType<LobbyManager>();
        if (lobby != null)
        {
            var so = new SerializedObject(lobby);
            so.FindProperty("firstStageScene").stringValue = FirstStageScene;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(lobby);
        }

        NetworkPhase4Setup.SetupWeaponSync();
        SetBuildOrder();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Lobby] Frozen_Tuna 프리팹 교체 및 Lobby → NetworkDemo → Stage2 흐름 갱신 완료.");
    }

    // ---------------------------------------------------------------- 지형

    /// <summary>
    /// 잔디 바닥 + 스폰에서 광장으로 들어오는 흙길.
    /// 바깥은 낮은 둔덕으로 둘러 맵 밖으로 못 나가게 한다.
    /// </summary>
    private static void Ground(Transform p)
    {
        Transform g = Group(p, "Ground", Vector3.zero, 0f);

        Box(g, "Grass", new Vector3(0f, -0.5f, 0f), new Vector3(80f, 1f, 80f), matGrass);

        // 색이 조금씩 다른 잔디 패치 — 단색 평면의 밋밋함을 깬다.
        for (int i = 0; i < 18; i++)
        {
            var at = new Vector3(Random.Range(-32f, 32f), 0.008f, Random.Range(-32f, 32f));
            if (new Vector2(at.x, at.z).magnitude < PlazaRadius + 1.5f)
                continue; // 광장 안은 포장이라 건드리지 않는다

            float s = Random.Range(4f, 9f);
            Disc(g, "GrassPatch", s, 0.012f, matGrassDark).transform.localPosition = at;
        }

        // 스폰 → 광장 → 게이트로 이어지는 길
        Box(g, "PathSouth", new Vector3(0f, 0.02f, -14f), new Vector3(4.2f, 0.04f, 12f), matDirt);
        Box(g, "PathNorth", new Vector3(0f, 0.02f, 14f), new Vector3(4.2f, 0.04f, 10f), matDirt);

        // 바깥 둔덕 — 낮은 언덕처럼 보이면서 실제로는 벽 역할을 한다.
        for (int i = 0; i < 44; i++)
        {
            float a = i / 44f * Mathf.PI * 2f;
            const float r = 33f;
            var at = new Vector3(Mathf.Cos(a) * r, Random.Range(1.1f, 2f), Mathf.Sin(a) * r);
            Prim(g, "Berm", PrimitiveType.Cube, at,
                new Vector3(0f, a * Mathf.Rad2Deg, Random.Range(-7f, 7f)),
                new Vector3(6f, Random.Range(3f, 4.4f), 6f), matGrassDark);
        }
    }

    /// <summary>
    /// 가운데 원형 광장. 돌 포장 두 겹과 테두리 연석으로 "정돈된 자리"를 만든다.
    /// 대기실이 허전해 보이던 건 바닥이 전부 같은 잔디였기 때문이다.
    /// </summary>
    private static void Plaza(Transform p)
    {
        Transform g = Group(p, "Plaza", Vector3.zero, 0f);

        Disc(g, "Pave", PlazaRadius * 2f, 0.06f, matPave);
        Disc(g, "PaveInner", PlazaRadius * 1.25f, 0.075f, matPaveDark);
        Disc(g, "Center", 4.6f, 0.09f, matPave);

        // 테두리 연석 — 낮게 둘러 광장 경계를 또렷하게.
        const int kerbs = 40;
        for (int i = 0; i < kerbs; i++)
        {
            float a = i / (float)kerbs * Mathf.PI * 2f;

            // 남북 통로는 비워 둔다(길이 광장으로 이어져야 한다).
            float deg = Mathf.Repeat(a * Mathf.Rad2Deg, 360f);
            if (Mathf.Abs(Mathf.DeltaAngle(deg, 90f)) < 12f || Mathf.Abs(Mathf.DeltaAngle(deg, 270f)) < 12f)
                continue;

            var at = new Vector3(Mathf.Cos(a) * PlazaRadius, 0.11f, Mathf.Sin(a) * PlazaRadius);
            Prim(g, "Kerb", PrimitiveType.Cube, at,
                new Vector3(0f, a * Mathf.Rad2Deg, 0f),
                new Vector3(0.5f, 0.22f, 1.75f), matStone);
        }
    }

    /// <summary>
    /// 외곽 숲. 침엽수와 활엽수를 섞고 크기·회전을 흩뜨려 같은 나무가 반복되는 느낌을 없앤다.
    /// </summary>
    private static void Forest(Transform p)
    {
        Transform g = Group(p, "Forest", Vector3.zero, 0f);

        for (int ring = 0; ring < 3; ring++)
        {
            int count = 30 - ring * 5;
            float radius = 21f + ring * 4.5f;
            for (int i = 0; i < count; i++)
            {
                float a = (i + Random.Range(-0.35f, 0.35f)) / count * Mathf.PI * 2f;
                var at = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (radius + Random.Range(-1.6f, 1.6f));

                // 스폰·게이트 통로는 비워 둔다.
                if (Mathf.Abs(at.x) < 3.5f && Mathf.Abs(at.z) > 12f)
                    continue;

                if (Random.value < 0.55f)
                    PineTree(g, at, Random.Range(0.9f, 1.5f));
                else
                    BroadTree(g, at, Random.Range(0.85f, 1.3f));
            }
        }

        // 광장 바깥 빈 잔디를 덤불·바위로 메운다.
        for (int i = 0; i < 26; i++)
        {
            float a = Random.value * Mathf.PI * 2f;
            float r = Random.Range(PlazaRadius + 2.5f, 19f);
            var at = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);

            bool rock = Random.value < 0.4f;
            GameObject go = Prim(g, rock ? "Rock" : "Bush", PrimitiveType.Sphere,
                at + new Vector3(0f, rock ? 0.16f : 0.24f, 0f),
                rock ? new Vector3(Random.Range(0.7f, 1.4f), 0.5f, Random.Range(0.7f, 1.2f))
                     : new Vector3(Random.Range(1f, 1.6f), 0.7f, Random.Range(1f, 1.6f)),
                rock ? matStone : (Random.value < 0.5f ? matLeaf : matLeafDark));
            Object.DestroyImmediate(go.GetComponent<Collider>()); // 발이 걸리지 않게
        }
    }

    /// <summary>원뿔 세 겹으로 쌓은 침엽수. 원뿔 메시는 캠핑장 빌더 것을 함께 쓴다.</summary>
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

        Leaves(g, "Leaves1", new Vector3(0f, 3.3f, 0f), 2.9f, matLeaf);
        Leaves(g, "Leaves2", new Vector3(0.9f, 2.9f, 0.5f), 2f, matLeafDark);
        Leaves(g, "Leaves3", new Vector3(-0.7f, 3.1f, -0.6f), 1.9f, matLeafLight);
    }

    /// <summary>수관 덩어리. 콜라이더는 빼서 나무는 줄기만 막게 한다.</summary>
    private static void Leaves(Transform g, string name, Vector3 pos, float size, Material mat)
    {
        GameObject go = Prim(g, name, PrimitiveType.Sphere, pos, Vector3.one * size, mat);
        Object.DestroyImmediate(go.GetComponent<Collider>());
    }

    // ---------------------------------------------------------------- 광장 한복판 (대기실의 얼굴)

    /// <summary>
    /// 냉동참치를 올려 둔 좌대.
    ///
    /// 마검이 딱 하나 놓여 있고 모두가 그것을 본다는 그림이 이 게임의 첫인상이다.
    /// 그래서 광장 정중앙에 단을 쌓고 위에서 조명을 떨어뜨린다.
    /// </summary>
    private static void Altar(Transform p)
    {
        Transform g = Group(p, "TunaAltar", AltarPos, 0f);

        // 세 겹으로 좁아지는 단
        Disc(g, "Step1", 5.2f, 0.18f, matStone);
        Disc(g, "Step2", 3.8f, 0.34f, matPave);
        Disc(g, "Step3", 2.6f, 0.5f, matStone);

        // 좌대 위 나무 상판
        Transform top = Group(g, "Pedestal", new Vector3(0f, 0.5f, 0f), 0f);
        Prim(top, "Column", PrimitiveType.Cylinder, new Vector3(0f, 0.3f, 0f), new Vector3(0.9f, 0.3f, 0.9f), matPaveDark);
        Prim(top, "Board", PrimitiveType.Cylinder, new Vector3(0f, 0.63f, 0f), new Vector3(1.5f, 0.05f, 1.5f), matWood);
        Prim(top, "Trim", PrimitiveType.Cylinder, new Vector3(0f, 0.58f, 0f), new Vector3(1.6f, 0.03f, 1.6f), matWoodDark);

        // 무기 = 냉동참치. 상판 윗면(y = 0.5 + 0.63 + 0.05 = 1.18) 위에 안치.
        Transform tuna = InstantiateFrozenTuna(g, new Vector3(0f, 1.18f, 0f), 90f);
        if (tuna == null)
            return;

        // 좌대 조명. 참치를 집어도 그대로 켜 둔다 —
        // 조명이 꺼지면 누가 가져갔다는 걸 모두가 알아채기 때문이다.
        var lightGo = new GameObject("AltarLight");
        lightGo.transform.SetParent(g, false);
        lightGo.transform.localPosition = new Vector3(0f, 7.5f, 0f);
        lightGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Spot;
        light.color = new Color(1f, 0.94f, 0.74f);
        light.spotAngle = 38f;
        light.range = 16f;
        light.intensity = 9f;
        light.shadows = LightShadows.None;

        var altar = g.gameObject.AddComponent<LobbyAltar>();
        altar.Configure(tuna.GetComponent<Weapon>());
    }

    /// <summary>캠핑장과 동일한 Frozen_Tuna 무기 프리팹을 배치한다.</summary>
    private static Transform InstantiateFrozenTuna(Transform parent, Vector3 localPos, float rotY)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TunaPrefabPath);
        if (prefab == null)
        {
            Debug.LogError("[Lobby] Frozen_Tuna 프리팹이 없습니다. Tools > Weapons > Build Weapon Prefabs를 먼저 실행하세요.");
            return null;
        }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        Weapon weapon = go.GetComponent<Weapon>();
        float lift = weapon != null ? weapon.groundOffset : 0f;
        go.transform.localPosition = localPos + Vector3.up * lift;
        go.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
        return go.transform;
    }

    /// <summary>
    /// 광장을 둘러싼 랜턴 기둥. 시선을 안쪽으로 모으고 저녁 분위기를 만든다.
    /// 기둥 꼭대기 좌표를 돌려준다 — 전구 줄이 이 점들 사이에 걸린다.
    /// </summary>
    private static List<Vector3> LanternRing(Transform p)
    {
        Transform g = Group(p, "Lanterns", Vector3.zero, 0f);
        var tops = new List<Vector3>();

        const int count = 8;
        for (int i = 0; i < count; i++)
        {
            float a = (i / (float)count + 0.0625f) * Mathf.PI * 2f;
            var at = new Vector3(Mathf.Cos(a) * (PlazaRadius - 1.2f), 0f, Mathf.Sin(a) * (PlazaRadius - 1.2f));

            Transform post = Group(g, "Lantern", at, -a * Mathf.Rad2Deg);
            Box(post, "Base", new Vector3(0f, 0.1f, 0f), new Vector3(0.36f, 0.2f, 0.36f), matStone);
            Box(post, "Post", new Vector3(0f, 1.4f, 0f), new Vector3(0.12f, 2.6f, 0.12f), matWoodDark);
            Box(post, "Arm", new Vector3(0f, 2.62f, 0.22f), new Vector3(0.08f, 0.08f, 0.5f), matMetal);

            Transform head = Group(post, "Head", new Vector3(0f, 2.34f, 0.44f), 0f);
            Box(head, "Cap", new Vector3(0f, 0.2f, 0f), new Vector3(0.3f, 0.06f, 0.3f), matMetal);
            GameObject glass = Box(head, "Glass", Vector3.zero, new Vector3(0.22f, 0.3f, 0.22f), matGlass);
            Object.DestroyImmediate(glass.GetComponent<Collider>());
            GameObject bulb = Prim(head, "Bulb", PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.14f, matEmber);
            Object.DestroyImmediate(bulb.GetComponent<Collider>());

            var lightGo = new GameObject("LanternLight");
            lightGo.transform.SetParent(head, false);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.82f, 0.5f);
            light.range = 9f;
            light.intensity = 1.5f;
            light.shadows = LightShadows.None;

            // 기둥 꼭대기 (Post는 높이 2.6, 중심 y = 1.4 → 윗면 2.7)
            tops.Add(at + new Vector3(0f, 2.7f, 0f));
        }

        return tops;
    }

    /// <summary>
    /// 랜턴 기둥 사이에 늘어뜨린 전구 줄. 광장 둘레를 화환처럼 두른다.
    ///
    /// 광장을 가로지르지 않고 둘레만 두르는 이유: 가운데를 지나가면 줄이 좌대와 겹치고,
    /// 사람 키(1.8m) 높이로 처져 시야를 막는다. 이웃한 기둥끼리만 이으면
    /// 가장 낮은 지점도 2.2m라 아래를 지나다닐 수 있다.
    /// </summary>
    private static void StringLights(Transform p, List<Vector3> anchors)
    {
        if (anchors == null || anchors.Count < 2)
            return;

        Transform g = Group(p, "StringLights", Vector3.zero, 0f);

        for (int i = 0; i < anchors.Count; i++)
            Strand(g, anchors[i], anchors[(i + 1) % anchors.Count]);
    }

    /// <summary>
    /// 전구 줄 하나. 가운데가 처지도록 포물선을 그리고,
    /// 그 곡선을 따라 <b>줄(케이블)</b>을 이은 뒤 전구를 매단다.
    ///
    /// 줄을 그리지 않으면 전구만 공중에 떠 있는 것처럼 보인다 —
    /// 곡선 위의 점들을 짧은 원기둥으로 하나씩 이어 붙여 줄처럼 보이게 한다.
    /// </summary>
    private static void Strand(Transform g, Vector3 a, Vector3 b)
    {
        const int segments = 10;   // 곡선을 쪼갤 수. 많을수록 부드럽지만 무거워진다
        const float sag = 0.55f;   // 가운데가 처지는 깊이 (m)

        // 1) 곡선 위의 점을 먼저 구한다.
        var points = new Vector3[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 at = Vector3.Lerp(a, b, t);
            at.y -= Mathf.Sin(t * Mathf.PI) * sag;
            points[i] = at;
        }

        // 2) 점과 점 사이를 가는 원기둥으로 잇는다 = 줄.
        for (int i = 0; i < segments; i++)
        {
            Vector3 from = points[i];
            Vector3 to = points[i + 1];
            Vector3 dir = to - from;
            if (dir.sqrMagnitude < 0.0001f)
                continue;

            // 기본 실린더는 높이 2짜리라 스케일 y에 길이의 절반을 넣는다.
            GameObject cable = Prim(g, "Cable", PrimitiveType.Cylinder, (from + to) * 0.5f, Vector3.zero,
                new Vector3(0.028f, dir.magnitude * 0.5f, 0.028f), matDark);
            cable.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
            Object.DestroyImmediate(cable.GetComponent<Collider>()); // 줄에 걸려 넘어지지 않게
        }

        // 3) 줄에 전구를 매단다. 양 끝(기둥에 묶이는 지점)은 비워 둔다.
        for (int i = 1; i < segments; i++)
        {
            Vector3 at = points[i];

            GameObject stem = Prim(g, "Stem", PrimitiveType.Cube, at - Vector3.up * 0.07f,
                new Vector3(0.022f, 0.14f, 0.022f), matDark);
            Object.DestroyImmediate(stem.GetComponent<Collider>());

            GameObject bulb = Prim(g, "Bulb", PrimitiveType.Sphere, at - Vector3.up * 0.2f,
                Vector3.one * 0.14f, matEmber);
            Object.DestroyImmediate(bulb.GetComponent<Collider>());
        }
    }

    // ---------------------------------------------------------------- 출발 게이트 + 준비 발판

    private static void Gate(Transform p)
    {
        // 180도 돌려 세운다. 게이트 자체는 좌우 대칭이라 모양은 그대로지만,
        // 현수막과 글자가 붙는 +Z 면이 광장(남쪽)을 보게 되어 들어오면서 읽힌다.
        Transform g = Group(p, "StartGate", GatePos, 180f);

        Box(g, "BaseL", new Vector3(-2.8f, 0.16f, 0f), new Vector3(0.9f, 0.32f, 0.9f), matStone);
        Box(g, "BaseR", new Vector3(2.8f, 0.16f, 0f), new Vector3(0.9f, 0.32f, 0.9f), matStone);
        Box(g, "PostL", new Vector3(-2.8f, 2.2f, 0f), new Vector3(0.34f, 4.4f, 0.34f), matWood);
        Box(g, "PostR", new Vector3(2.8f, 2.2f, 0f), new Vector3(0.34f, 4.4f, 0.34f), matWood);
        Box(g, "Lintel", new Vector3(0f, 4.5f, 0f), new Vector3(6.4f, 0.36f, 0.42f), matWoodDark);
        Box(g, "Banner", new Vector3(0f, 3.8f, 0.06f), new Vector3(3.8f, 1f, 0.06f), matAccent);

        BuilderText.World(g, "BannerText", new Vector3(0f, 3.8f, 0.11f), "출  발",
            new Vector2(3.4f, 0.8f), new Color(1f, 0.96f, 0.88f));

        // 기둥 위 장식 등
        foreach (float x in new[] { -2.8f, 2.8f })
        {
            GameObject bulb = Prim(g, "GateLamp", PrimitiveType.Sphere, new Vector3(x, 4.62f, 0f),
                Vector3.one * 0.24f, matEmber);
            Object.DestroyImmediate(bulb.GetComponent<Collider>());
        }

        // 준비 발판 (나무 데크)
        Transform pad = Group(p, "ReadyPad", ReadyPadPos, 0f);
        Disc(pad, "Ring", 5.2f, 0.08f, matPaveDark);
        Box(pad, "Deck", new Vector3(0f, 0.11f, 0f), new Vector3(4.2f, 0.22f, 4.2f), matWood);
        Box(pad, "TrimN", new Vector3(0f, 0.25f, -2.1f), new Vector3(4.3f, 0.12f, 0.16f), matAccent);
        Box(pad, "TrimS", new Vector3(0f, 0.25f, 2.1f), new Vector3(4.3f, 0.12f, 0.16f), matAccent);
        Box(pad, "TrimW", new Vector3(-2.1f, 0.25f, 0f), new Vector3(0.16f, 0.12f, 4.3f), matAccent);
        Box(pad, "TrimE", new Vector3(2.1f, 0.25f, 0f), new Vector3(0.16f, 0.12f, 4.3f), matAccent);

        // 준비 판정 영역 (발판 위 공간)
        var zoneGo = new GameObject("ReadyZone");
        zoneGo.transform.SetParent(pad, false);
        zoneGo.transform.localPosition = new Vector3(0f, 1.4f, 0f);
        var box = zoneGo.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(4.2f, 2.6f, 4.2f);
        zoneGo.AddComponent<LobbyReadyZone>();
    }

    // ---------------------------------------------------------------- 허수아비 연습장

    private static void PracticeArea(Transform p)
    {
        Transform g = Group(p, "PracticeArea", PracticePos, 0f);

        Disc(g, "Sand", 8.4f, 0.06f, matSand);
        Disc(g, "SandInner", 6f, 0.075f, matDirt);

        // 낮은 통나무 울타리로 구역을 나눈다.
        const int posts = 14;
        for (int i = 0; i < posts; i++)
        {
            float a = i / (float)posts * Mathf.PI * 2f;

            // 광장 쪽(동쪽)은 터 놓아 드나들 수 있게 한다.
            if (Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, 0f)) < 34f)
                continue;

            var at = new Vector3(Mathf.Cos(a) * 4.4f, 0.4f, Mathf.Sin(a) * 4.4f);
            Prim(g, "Post", PrimitiveType.Cylinder, at, new Vector3(0f, 0f, 0f),
                new Vector3(0.11f, 0.4f, 0.11f), matWoodDark);
        }

        Dummy(g, new Vector3(-1.8f, 0f, 1.5f), 110f);
        Dummy(g, new Vector3(1.3f, 0f, 2f), 130f);
        Dummy(g, new Vector3(-0.2f, 0f, -1.8f), 80f);
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

    // ---------------------------------------------------------------- 모닥불 / 야영지

    private static void Campfire(Transform p, Vector3 pos)
    {
        Transform g = Group(p, "Campfire", pos, 0f);

        Disc(g, "Pad", 5f, 0.05f, matDirt);

        for (int i = 0; i < 10; i++)
        {
            float a = i / 10f * Mathf.PI * 2f;
            Prim(g, "Stone", PrimitiveType.Cube,
                new Vector3(Mathf.Cos(a) * 0.9f, 0.12f, Mathf.Sin(a) * 0.9f),
                new Vector3(0f, a * Mathf.Rad2Deg + Random.Range(-18f, 18f), 0f),
                new Vector3(0.3f, 0.24f, 0.26f), matStone);
        }

        Prim(g, "Ash", PrimitiveType.Cylinder, new Vector3(0f, 0.03f, 0f), new Vector3(1.4f, 0.03f, 1.4f), matDark);

        // 원뿔로 세운 장작 — 위로 갈수록 가운데로 모여야 한다.
        for (int i = 0; i < 4; i++)
        {
            float a = i / 4f * Mathf.PI * 2f;
            Prim(g, "Log", PrimitiveType.Cylinder,
                new Vector3(Mathf.Cos(a) * 0.16f, 0.3f, Mathf.Sin(a) * 0.16f),
                LeanInward(a, 22f),
                new Vector3(0.12f, 0.42f, 0.12f), matWoodDark);
        }

        GameObject flame = Prim(g, "Flame", PrimitiveType.Sphere, new Vector3(0f, 0.5f, 0f),
            new Vector3(0.42f, 0.68f, 0.42f), matFlame);
        Object.DestroyImmediate(flame.GetComponent<Collider>());
        GameObject tip = Prim(g, "FlameTip", PrimitiveType.Sphere, new Vector3(0f, 0.86f, 0f),
            new Vector3(0.2f, 0.34f, 0.2f), matEmber);
        Object.DestroyImmediate(tip.GetComponent<Collider>());

        var lightGo = new GameObject("FireLight");
        lightGo.transform.SetParent(g, false);
        lightGo.transform.localPosition = new Vector3(0f, 0.8f, 0f);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.6f, 0.25f);
        light.range = 13f;
        light.intensity = 2.6f;
        light.shadows = LightShadows.Soft;

        // 둘러앉는 통나무
        for (int i = 0; i < 4; i++)
        {
            float a = (i / 4f) * Mathf.PI * 2f + 0.4f;
            Prim(g, "SitLog", PrimitiveType.Cylinder,
                new Vector3(Mathf.Cos(a) * 2.1f, 0.22f, Mathf.Sin(a) * 2.1f),
                new Vector3(0f, -a * Mathf.Rad2Deg, 90f),
                new Vector3(0.44f, 0.9f, 0.44f), matWood);
        }
    }

    /// <summary>광장 동쪽의 야영지. 대기실이 "출발 전 캠프"라는 걸 눈으로 알려 준다.</summary>
    private static void Camp(Transform p)
    {
        Transform g = Group(p, "Camp", new Vector3(10f, 0f, -3f), 0f);

        Tent(g, new Vector3(0f, 0f, 0f), -110f, matTent);
        Tent(g, new Vector3(2.6f, 0f, 5.4f), -145f, matAccent);

        // 짐 상자 몇 개
        Box(g, "Crate1", new Vector3(-1.8f, 0.3f, 2.6f), new Vector3(0.7f, 0.6f, 0.7f), matWood);
        Box(g, "Crate2", new Vector3(-1.5f, 0.85f, 2.9f), new Vector3(0.5f, 0.5f, 0.5f), matWoodDark);
        Box(g, "Cooler", new Vector3(-0.4f, 0.28f, 3.6f), new Vector3(0.86f, 0.56f, 0.54f), matAccent);
    }

    /// <summary>A형 텐트. 지붕 두 장이 마루에서 만나야 ∧ 모양이 된다.</summary>
    private static void Tent(Transform p, Vector3 pos, float rotY, Material mat)
    {
        Transform g = Group(p, "Tent", pos, rotY);

        Box(g, "Floor", new Vector3(0f, 0.06f, 0f), new Vector3(2.3f, 0.12f, 3f), matDark);

        GameObject l = Box(g, "SideL", new Vector3(-0.62f, 0.95f, 0f), new Vector3(0.08f, 2.2f, 3.1f), mat);
        GameObject r = Box(g, "SideR", new Vector3(0.62f, 0.95f, 0f), new Vector3(0.08f, 2.2f, 3.1f), mat);
        l.transform.localRotation = Quaternion.Euler(0f, 0f, -32f);
        r.transform.localRotation = Quaternion.Euler(0f, 0f, 32f);

        Box(g, "Back", new Vector3(0f, 0.85f, -1.52f), new Vector3(2.2f, 1.7f, 0.06f), mat);
        Box(g, "Ridge", new Vector3(0f, 1.86f, 0f), new Vector3(0.07f, 0.07f, 3.3f), matWoodDark);
    }

    // ---------------------------------------------------------------- 안내 팻말

    private static void Signs(Transform p)
    {
        Transform g = Group(p, "Signs", Vector3.zero, 0f);

        // 팻말은 rotY가 가리키는 쪽에서 읽힌다. 스폰(0, -13)에서 광장으로 걸어오는
        // 사람이 읽어야 하므로 남쪽을 보게 세운다.
        Sign(g, "ControlSign", new Vector3(-4.2f, 0f, -10.2f), 145f,
            "WASD 이동 · Space 점프\n좌클릭 줍기 / 휘두르기 · G 내려놓기");

        Sign(g, "AltarSign", new Vector3(4.6f, 0f, -4.6f), 210f,
            "냉동참치 — 한 방이면 끝난다\n먼저 잡는 사람이 임자");

        Sign(g, "GateSign", new Vector3(5.2f, 0f, 9.4f), 205f,
            "정원이 다 차면 출발\n발판 위에서 기다리자");

        Sign(g, "PracticeSign", new Vector3(-5.6f, 0f, 5.2f), 115f,
            "허수아비 연습장\n무기를 들고 휘둘러 보세요");
    }

    /// <summary>나무 팻말 + TMP 글자. rotY가 향하는 쪽(+Z)에서 읽힌다.</summary>
    private static void Sign(Transform parent, string name, Vector3 pos, float rotY, string text)
    {
        Transform g = Group(parent, name, pos, rotY);
        Box(g, "Post", new Vector3(0f, 0.7f, 0f), new Vector3(0.1f, 1.4f, 0.1f), matWoodDark);
        Box(g, "Board", new Vector3(0f, 1.62f, 0f), new Vector3(3.2f, 0.95f, 0.08f), matWood);
        Box(g, "BoardTrim", new Vector3(0f, 1.62f, -0.02f), new Vector3(3.34f, 1.06f, 0.06f), matWoodDark);

        // 판보다 살짝 안쪽으로 잡아 글자가 테두리에 닿지 않게 한다.
        BuilderText.World(g, "Text", new Vector3(0f, 1.62f, 0.055f), text,
            new Vector2(2.9f, 0.76f), BuilderText.SignInk);
    }

    // ---------------------------------------------------------------- 스폰 / 네트워크

    private static void SpawnPoints()
    {
        var root = new GameObject("NetworkSpawnPoints").transform;

        // 정원 상한만큼 두 줄로 늘어세운다. RoundRobin이라 자리는 순서대로 배정된다.
        for (int i = 0; i < MaxPlayers; i++)
        {
            int row = i / 4;
            int col = i % 4;
            var offset = new Vector3((col - 1.5f) * 2.1f, 0f, row * -1.8f);

            var sp = new GameObject("Spawn_" + (char)('A' + i));
            sp.transform.SetParent(root, false);
            sp.transform.position = SpawnBase + offset;
            sp.transform.rotation = Quaternion.identity; // +Z = 광장 방향
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

        // 대기실에 있는 동안 LAN에 방을 알린다 → 타이틀의 '빠른 참가'가 이걸 찾아 들어온다.
        if (bootstrap.GetComponent<LanRoomAdvertiser>() == null)
            bootstrap.AddComponent<LanRoomAdvertiser>();

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
        // 실제 정원은 호스트가 대기실 옵션에서 정하고 LobbyManager가 이 값을 덮어쓴다.
        manager.maxConnections = MaxPlayers;
        EditorUtility.SetDirty(manager);
    }

    private static void SetupLobbyManager()
    {
        var go = new GameObject("LobbyManager");
        go.AddComponent<NetworkIdentity>();
        var lobby = go.AddComponent<LobbyManager>();
        go.AddComponent<LobbyHud>(); // 방 코드·인원·카운트다운 + 호스트 옵션 창

        var so = new SerializedObject(lobby);
        so.FindProperty("firstStageScene").stringValue = FirstStageScene;
        so.FindProperty("minPlayers").intValue = 1;
        so.FindProperty("maxPlayers").intValue = MaxPlayers;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(lobby);

        Debug.Log($"[Lobby] 기본 정원 {DefaultTargetPlayers}명. 호스트가 대기실에서 Tab으로 1~{MaxPlayers}명 사이에서 바꿀 수 있습니다.");
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

    /// <summary>
    /// Build Settings를 실제 게임 흐름대로 정리한다:
    /// 타이틀 → 대기실 → 집(NetworkDemo) → 캠핑장.
    /// </summary>
    private static void SetBuildOrder()
    {
        var order = new List<string>();
        if (System.IO.File.Exists(TitlePath)) order.Add(TitlePath);
        order.Add(LobbyPath);
        if (System.IO.File.Exists(DemoPath)) order.Add(DemoPath);
        if (System.IO.File.Exists(Stage2Path)) order.Add(Stage2Path);

        EditorBuildSettings.scenes = order
            .Select(path => new EditorBuildSettingsScene(path, true))
            .ToArray();

        if (!System.IO.File.Exists(DemoPath))
            Debug.LogWarning("[Lobby] 첫 스테이지 NetworkDemo 씬이 없어 빌드 목록에 넣지 못했습니다.");
    }

    // ---------------------------------------------------------------- 유틸

    /// <summary>
    /// 해질 무렵의 빛. 대기실은 "출발 직전"이라 낮보다 조금 기운 해가 어울린다.
    /// 그림자가 길게 눕고, 랜턴·모닥불이 살아 보인다.
    /// </summary>
    private static void AddSun()
    {
        var go = new GameObject("Directional Light");
        go.transform.rotation = Quaternion.Euler(34f, -28f, 0f);
        var light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.92f, 0.78f);
        light.intensity = 1.15f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.7f;
        RenderSettings.sun = light;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.5f, 0.6f, 0.76f);
        RenderSettings.ambientEquatorColor = new Color(0.45f, 0.45f, 0.42f);
        RenderSettings.ambientGroundColor = new Color(0.22f, 0.22f, 0.2f);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.66f, 0.72f, 0.8f);
        RenderSettings.fogStartDistance = 26f;
        RenderSettings.fogEndDistance = 92f;
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

    /// <summary>원뿔. 캠핑장 빌더가 구워 둔 메시를 함께 쓴다(나무마다 새로 만들면 씬이 무거워진다).</summary>
    private static GameObject Cone(Transform parent, string name, Vector3 localPos, float radius, float height, Material mat)
    {
        GameObject go = Prim(parent, name, PrimitiveType.Cylinder, localPos, Vector3.zero,
            new Vector3(radius, height * 0.5f, radius), mat);

        go.GetComponent<MeshFilter>().sharedMesh = Stage2Builder.ConeMesh();

        // 수관이 벽처럼 굴지 않도록 나무는 줄기만 막는다.
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

        FixFlatCollider(go, type, size);
        return go;
    }

    /// <summary>
    /// 납작하게 눌러 놓은 원기둥·구의 콜라이더를 실제 모양에 맞춘다.
    /// CapsuleCollider·SphereCollider의 반지름은 X·Z 스케일 중 큰 쪽을 따라가고
    /// 캡슐 높이는 최소 지름만큼 강제돼서, 얇게 편 원판이 거대한 덩어리가 된다
    /// (그 위를 걸으면 공중에 뜬 것처럼 보인다). BoxCollider는 스케일을 그대로 따라간다.
    /// </summary>
    private static void FixFlatCollider(GameObject go, PrimitiveType type, Vector3 size)
    {
        if (type != PrimitiveType.Cylinder && type != PrimitiveType.Sphere && type != PrimitiveType.Capsule)
            return;

        float widest = Mathf.Max(Mathf.Abs(size.x), Mathf.Abs(size.z));
        if (Mathf.Abs(size.y) >= widest * 0.5f)
            return;

        Collider round = go.GetComponent<Collider>();
        if (round != null)
            Object.DestroyImmediate(round);

        go.AddComponent<BoxCollider>(); // 메시 경계에 맞춰 자동으로 잡힌다
    }

    /// <summary>
    /// 중심에서 각도 a 방향으로 세운 막대를 위쪽이 가운데로 모이도록 눕히는 회전.
    /// 부호를 뒤집으면 위가 벌어지는 ∨ 모양이 된다.
    /// </summary>
    private static Vector3 LeanInward(float angle, float degrees)
        => new Vector3(-Mathf.Sin(angle) * degrees, 0f, Mathf.Cos(angle) * degrees);

    private static void LoadMaterials()
    {
        matGrass = GetMat("Camp_Grass", new Color(0.30f, 0.5f, 0.22f));
        matGrassDark = GetMat("Camp_GrassDark", new Color(0.22f, 0.39f, 0.17f));
        matDirt = GetMat("Camp_Dirt", new Color(0.45f, 0.36f, 0.24f));
        matSand = GetMat("Lobby_Sand", new Color(0.72f, 0.62f, 0.44f));

        matPave = GetMat("Lobby_Pave", new Color(0.58f, 0.56f, 0.52f));
        matPaveDark = GetMat("Lobby_PaveDark", new Color(0.44f, 0.42f, 0.40f));
        matStone = GetMat("Camp_Stone", new Color(0.5f, 0.5f, 0.52f));

        matWood = GetMat("Camp_Wood", new Color(0.5f, 0.33f, 0.18f));
        matWoodDark = GetMat("Camp_WoodDark", new Color(0.33f, 0.21f, 0.12f));
        matMetal = GetMat("Camp_Metal", new Color(0.72f, 0.73f, 0.75f));
        matDark = GetMat("Camp_Dark", new Color(0.13f, 0.13f, 0.14f));

        matFlame = GetMat("Camp_Flame", new Color(1f, 0.55f, 0.15f));
        matEmber = GetMat("Camp_Ember", new Color(1f, 0.83f, 0.4f));
        matGlass = GetMat("Lobby_Glass", new Color(0.86f, 0.82f, 0.66f));

        matLeaf = GetMat("Camp_Leaf", new Color(0.22f, 0.45f, 0.2f));
        matLeafDark = GetMat("Camp_LeafDark", new Color(0.13f, 0.31f, 0.15f));
        matLeafLight = GetMat("Camp_LeafLight", new Color(0.36f, 0.58f, 0.26f));

        matAccent = GetMat("Camp_Accent", new Color(0.8f, 0.3f, 0.2f));
        matTent = GetMat("Camp_Tent", new Color(0.2f, 0.45f, 0.6f));
        matStraw = GetMat("Lobby_Straw", new Color(0.82f, 0.72f, 0.38f));

        // 참치는 캠핑장의 것과 같은 색을 쓴다.
        var houseTuna = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/House_Tuna.mat");
        matTuna = houseTuna != null ? houseTuna : GetMat("Lobby_Tuna", new Color(0.55f, 0.65f, 0.72f));
    }

    private static Material GetMat(string assetName, Color color) => BuilderMaterials.Ensure(assetName, color);
}
#endif
