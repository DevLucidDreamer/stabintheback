#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 프리미티브만으로 넓은 실내 주택 맵을 생성하는 에디터 도구.
/// 상단 메뉴 [Tools > House > Build House] 로 생성, [Clear House] 로 제거.
///
/// 구조 (내부 32 x 24m, 문폭 3~4m — 통행 방해 없음):
///   남쪽 절반 = 벽 없는 오픈 공간 (거실 - 중앙 통로 - 주방/식당)
///   북쪽 절반 = 침실(서) / 서재(중) / 욕실(동)
/// 상호작용: 서랍(주방 3 + 침실 2 + 서재 1), 냉장고(문 열림, 안에 냉동참치),
///           무기 = 국자(조리대 위), 냉동참치(냉장고 안)
/// </summary>
public static class HouseBuilder
{
    const float WALL_H = 3f;   // 벽 높이
    const float WALL_T = 0.2f; // 벽 두께

    static Material matWall, matFloor, matWood, matFabric, matWhite, matMetal, matDark, matAccent, matPlant, matTuna, matOrange;

    [MenuItem("Tools/House/Build House")]
    public static void BuildHouse()
    {
        ClearHouse();
        LoadMaterials();

        var root = new GameObject("House");
        Undo.RegisterCreatedObjectUndo(root, "Build House");
        Transform t = root.transform;

        // 바닥/천장 — URP의 오브젝트당 조명 개수 제한을 피하려고 구역별로 분할한다.
        float[] tileX = { -10.5f, 0f, 10.5f };
        float[] tileW = { 11f, 10f, 11f };
        for (int i = 0; i < 3; i++)
        {
            Box(t, "Floor", new Vector3(tileX[i], 0.02f, -5f), new Vector3(tileW[i], 0.04f, 14f), matFloor);
            Box(t, "Floor", new Vector3(tileX[i], 0.02f, 7f), new Vector3(tileW[i], 0.04f, 10f), matFloor);
            Box(t, "Ceiling", new Vector3(tileX[i], 3.1f, -5f), new Vector3(tileW[i], 0.2f, 14f), matWhite);
            Box(t, "Ceiling", new Vector3(tileX[i], 3.1f, 7f), new Vector3(tileW[i], 0.2f, 10f), matWhite);
        }

        BuildWalls(t);
        FrontDoor(t);
        FurnishLivingRoom(t);
        FurnishKitchen(t);
        FurnishBedroom(t);
        FurnishStudy(t);
        FurnishBathroom(t);
        FurnishCommon(t);

        AddRoomLight(t, "Light_Living", new Vector3(-12f, 2.7f, -5f));
        AddRoomLight(t, "Light_Kitchen", new Vector3(12f, 2.7f, -5f));
        AddRoomLight(t, "Light_Center", new Vector3(0f, 2.7f, -5f));
        AddRoomLight(t, "Light_Bedroom", new Vector3(-10.5f, 2.7f, 7f));
        AddRoomLight(t, "Light_Study", new Vector3(0f, 2.7f, 8f));
        AddRoomLight(t, "Light_Bathroom", new Vector3(11f, 2.7f, 7f));
        AddRoomLight(t, "Light_Entrance", new Vector3(0f, 2.7f, -9.5f));

        EnsurePlayerSetup();

        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        Debug.Log("[HouseBuilder] 집 생성 완료. 좌클릭=상호작용/스윙, 우클릭=들고도 열기, G=버리기");
    }

    [MenuItem("Tools/House/Clear House")]
    public static void ClearHouse()
    {
        var existing = GameObject.Find("House");
        while (existing != null && existing.transform.parent == null)
        {
            Undo.DestroyObjectImmediate(existing);
            existing = GameObject.Find("House");
        }
    }

    /// <summary>Player에 PlayerInteraction이 없으면 추가한다.</summary>
    static void EnsurePlayerSetup()
    {
        var pc = Object.FindFirstObjectByType<PlayerController>();
        if (pc == null)
            return;
        if (pc.GetComponent<PlayerInteraction>() == null)
            Undo.AddComponent<PlayerInteraction>(pc.gameObject);
        if (pc.GetComponent<ItemChecklist>() == null)
            Undo.AddComponent<ItemChecklist>(pc.gameObject);
    }

    // ---------------------------------------------------------------- 벽

    static void BuildWalls(Transform p)
    {
        Transform w = new GameObject("Walls").transform;
        w.SetParent(p, false);

        // 외벽 (내부 x:-16~16, z:-12~12) + 남쪽 현관(폭 3.6m)
        WallRun(w, "Wall_South", 'X', -12f, -16f, 16f, -1.8f, 1.8f);
        WallRun(w, "Wall_North", 'X', 12f, -16f, 16f);
        WallRun(w, "Wall_West", 'Z', -16f, -12f, 12f);
        WallRun(w, "Wall_East", 'Z', 16f, -12f, 12f);

        // 북쪽 방들과 남쪽 오픈 공간을 나누는 벽 (문 3개: 침실 3m / 서재 4m / 욕실 3m)
        WallRun(w, "Wall_NorthDivider", 'X', 2f, -16f, 16f, -12f, -9f, -2f, 2f, 9f, 12f);

        // 북쪽 방 사이 세로 벽 (침실|서재, 서재|욕실)
        WallRun(w, "Wall_BedStudy", 'Z', -5f, 2f, 12f);
        WallRun(w, "Wall_StudyBath", 'Z', 5f, 2f, 12f);
    }

    /// <summary>현관 이중문 — 남쪽 출입구(폭 3.6m)에 힌지 문 2짝 + 상인방.</summary>
    static void FrontDoor(Transform p)
    {
        Transform g = Group(p, "FrontDoor", new Vector3(0f, 0f, -12f), 0f);

        // 문 위를 막는 상인방 (문 높이 2.2m ~ 벽 높이 3m)
        Box(g, "Header", new Vector3(0f, 2.6f, 0f), new Vector3(3.6f, 0.8f, WALL_T), matWall);

        DoorPanel(g, "Door_L", -1.8f, +1f);
        DoorPanel(g, "Door_R", +1.8f, -1f);
    }

    /// <summary>문 한 짝. hingeX = 힌지 위치, dir = 문이 뻗는 방향(+1 오른쪽/-1 왼쪽). 바깥(남쪽)으로 열린다.</summary>
    static void DoorPanel(Transform parent, string name, float hingeX, float dir)
    {
        Transform pivot = Group(parent, name, new Vector3(hingeX, 1.1f, 0f), 0f);
        Box(pivot, "Panel", new Vector3(dir * 0.88f, 0f, 0f), new Vector3(1.76f, 2.2f, 0.08f), matWood);
        Box(pivot, "Knob", new Vector3(dir * 1.6f, 0f, 0.07f), new Vector3(0.08f, 0.08f, 0.06f), matDark);

        var o = pivot.gameObject.AddComponent<Openable>();
        o.mode = Openable.Mode.Hinge;
        o.movingPart = pivot;
        o.hingeOpenEuler = new Vector3(0f, dir * 110f, 0f);
        o.duration = 0.4f;
        o.SetDisplayName("현관문");
    }

    /// <summary>
    /// 직선 벽 생성. axis 'Z'는 z축을 따라(고정 x), 'X'는 x축을 따라(고정 z).
    /// gaps는 (시작,끝) 쌍의 오름차순 목록 — 해당 구간은 출입구로 비운다.
    /// </summary>
    static void WallRun(Transform parent, string name, char axis, float fixedC, float from, float to, params float[] gaps)
    {
        var pts = new System.Collections.Generic.List<float> { from };
        for (int i = 0; i + 1 < gaps.Length; i += 2)
        {
            pts.Add(gaps[i]);
            pts.Add(gaps[i + 1]);
        }
        pts.Add(to);

        for (int i = 0; i + 1 < pts.Count; i += 2)
        {
            float a = pts[i], b = pts[i + 1];
            if (b - a <= 0.001f) continue;
            float len = b - a, c = (a + b) * 0.5f;

            Vector3 size, pos;
            if (axis == 'Z')
            {
                size = new Vector3(WALL_T, WALL_H, len);
                pos = new Vector3(fixedC, WALL_H * 0.5f, c);
            }
            else
            {
                size = new Vector3(len, WALL_H, WALL_T);
                pos = new Vector3(c, WALL_H * 0.5f, fixedC);
            }
            Box(parent, name, pos, size, matWall);
        }
    }

    // ---------------------------------------------------------------- 방 구성 (가구는 벽 쪽으로, 중앙은 비움)

    static void FurnishLivingRoom(Transform p)
    {
        Transform room = new GameObject("Room_Living").transform;
        room.SetParent(p, false);

        Sofa(room, new Vector3(-12f, 0f, 1.2f), 180f);       // 북쪽 벽에 붙여 남향
        CoffeeTable(room, new Vector3(-12f, 0f, -0.8f), 0f);
        Rug(room, new Vector3(-12f, 0f, -0.8f), new Vector3(3f, 0.04f, 2.4f));
        TvUnit(room, new Vector3(-12f, 0f, -11.4f), 0f);      // 남쪽 벽, 소파와 마주봄
        Bookshelf(room, new Vector3(-15.6f, 0f, -4f), 90f);
        Plant(room, new Vector3(-15.2f, 0f, -11.2f));
        Plant(room, new Vector3(-15.2f, 0f, 1.2f));

        // 접이식 캠핑의자 (서쪽 벽에 기대어 둠, 챙길 수 있음)
        Transform campChair = CampingItem(room, "CampChair", "캠핑의자", new Vector3(-15.35f, 0f, -7.5f), 0f);
        Prim(campChair, "Fold", PrimitiveType.Cube, new Vector3(0f, 0.55f, 0f), new Vector3(0f, 0f, 8f), new Vector3(0.28f, 1.1f, 0.18f), matAccent);
        Box(campChair, "Strap", new Vector3(-0.07f, 0.75f, 0f), new Vector3(0.29f, 0.08f, 0.19f), matDark);
    }

    static void FurnishKitchen(Transform p)
    {
        Transform room = new GameObject("Room_Kitchen").transform;
        room.SetParent(p, false);

        // 조리대 (동쪽 벽, 서랍 3개는 서쪽을 향해 열림)
        Transform counter = Group(room, "KitchenCounter", new Vector3(15.5f, 0f, -4f), 0f);
        Box(counter, "Body", new Vector3(0f, 0.45f, 0f), new Vector3(0.7f, 0.9f, 8f), matWhite);
        Box(counter, "Top", new Vector3(0f, 0.93f, 0f), new Vector3(0.78f, 0.06f, 8.2f), matWood);
        Drawer(counter, new Vector3(-0.05f, 0.68f, -2.5f), -90f, new Vector3(0.55f, 0.25f, 0.55f), 0.5f);
        Transform midDrawer = Drawer(counter, new Vector3(-0.05f, 0.68f, 0f), -90f, new Vector3(0.55f, 0.25f, 0.55f), 0.5f);
        Drawer(counter, new Vector3(-0.05f, 0.68f, 2.5f), -90f, new Vector3(0.55f, 0.25f, 0.55f), 0.5f);

        // 가운데 서랍 안 라면 2개 (서랍을 열어야 챙길 수 있음)
        Transform ramen1 = CampingItem(midDrawer, "Ramen1", "라면", new Vector3(-0.12f, 0.15f, 0.05f), 10f);
        Box(ramen1, "Pack", Vector3.zero, new Vector3(0.13f, 0.05f, 0.17f), matOrange);
        Transform ramen2 = CampingItem(midDrawer, "Ramen2", "라면", new Vector3(0.1f, 0.15f, -0.06f), -25f);
        Box(ramen2, "Pack", Vector3.zero, new Vector3(0.13f, 0.05f, 0.17f), matOrange);

        // 조리대 위 주방 도구 (국자만 무기)
        Ladle(room, new Vector3(15.5f, 0.96f, -0.8f), 0f);
        Pan(room, new Vector3(15.45f, 0.99f, -5.3f));
        Pot(room, new Vector3(15.5f, 1.08f, -6.6f));
        Spatula(room, new Vector3(15.45f, 0.98f, -3.4f));
        Box(room, "CuttingBoard", new Vector3(15.5f, 0.975f, -2f), new Vector3(0.4f, 0.03f, 0.3f), matWood);

        // 캠핑용 코펠 (챙길 수 있음)
        Transform cookset = CampingItem(room, "CampCookset", "코펠", new Vector3(15.5f, 1.05f, -7.6f), 0f);
        Prim(cookset, "Body", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.24f, 0.09f, 0.24f), matMetal);
        Prim(cookset, "Lid", PrimitiveType.Cylinder, new Vector3(0f, 0.1f, 0f), new Vector3(0.25f, 0.012f, 0.25f), matDark);

        // 냉장고 (문이 서쪽으로 열리고, 안에 냉동참치)
        FridgeWithTuna(room, new Vector3(15.4f, 0f, 1.2f), -90f);

        // 식탁 (동쪽 구역, 중앙 통로 비켜서)
        DiningTable(room, new Vector3(9f, 0f, -8f), 0f);
        Chair(room, new Vector3(7.6f, 0f, -8f), 90f);
        Chair(room, new Vector3(10.4f, 0f, -8f), -90f);
        Plant(room, new Vector3(15.2f, 0f, -11.2f));
    }

    static void FurnishBedroom(Transform p)
    {
        Transform room = new GameObject("Room_Bedroom").transform;
        room.SetParent(p, false);

        Bed(room, new Vector3(-10.5f, 0f, 10f), 0f);
        NightstandWithDrawer(room, new Vector3(-13f, 0f, 11.2f), 0f);
        NightstandWithDrawer(room, new Vector3(-8f, 0f, 11.2f), 0f);

        // 텐트 가방 (옷장 옆) + 침낭 (침대 위)
        Transform tent = CampingItem(room, "TentBag", "텐트", new Vector3(-14.6f, 0f, 7.5f), 15f);
        Box(tent, "Bag", new Vector3(0f, 0.16f, 0f), new Vector3(0.9f, 0.32f, 0.35f), matPlant);
        Box(tent, "Strap1", new Vector3(-0.25f, 0.16f, 0f), new Vector3(0.06f, 0.34f, 0.37f), matDark);
        Box(tent, "Strap2", new Vector3(0.25f, 0.16f, 0f), new Vector3(0.06f, 0.34f, 0.37f), matDark);

        Transform sleepBag = CampingItem(room, "SleepingBag", "침낭", new Vector3(-10f, 0.8f, 8.8f), 0f);
        Prim(sleepBag, "Roll", PrimitiveType.Cylinder, Vector3.zero, new Vector3(90f, 0f, 0f), new Vector3(0.3f, 0.35f, 0.3f), matAccent);
        Wardrobe(room, new Vector3(-15.55f, 0f, 5f), 90f);
        Rug(room, new Vector3(-10.5f, 0f, 7f), new Vector3(2.6f, 0.04f, 1.8f));
        Plant(room, new Vector3(-5.6f, 0f, 11.2f));
    }

    static void FurnishStudy(Transform p)
    {
        Transform room = new GameObject("Room_Study").transform;
        room.SetParent(p, false);

        Transform deskDrawer = Desk(room, new Vector3(0f, 0f, 11f), 0f);
        Chair(room, new Vector3(0f, 0f, 9.8f), 0f);

        // 책상 서랍 안 손전등 (서랍을 열어야 챙길 수 있음)
        Transform flash = CampingItem(deskDrawer, "Flashlight", "손전등", new Vector3(-0.05f, 0.125f, 0f), 20f);
        Prim(flash, "Body", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0f, 0f, 90f), new Vector3(0.07f, 0.08f, 0.07f), matDark);
        Box(flash, "Head", new Vector3(0.1f, 0f, 0f), new Vector3(0.08f, 0.08f, 0.08f), matMetal);
        Bookshelf(room, new Vector3(-3f, 0f, 11.65f), 180f);
        Bookshelf(room, new Vector3(3f, 0f, 11.65f), 180f);
        Rug(room, new Vector3(0f, 0f, 8f), new Vector3(2.4f, 0.04f, 2f));
        Plant(room, new Vector3(-4.5f, 0f, 3f));
    }

    static void FurnishBathroom(Transform p)
    {
        Transform room = new GameObject("Room_Bathroom").transform;
        room.SetParent(p, false);

        Bathtub(room, new Vector3(14f, 0f, 11.3f), 0f);
        Toilet(room, new Vector3(15.5f, 0f, 7f), -90f);
        Sink(room, new Vector3(15.5f, 0f, 9f), -90f);
        Washer(room, new Vector3(7f, 0f, 11.4f), 180f);
        Plant(room, new Vector3(15.2f, 0f, 3f));

        // 세탁기 위 구급상자 (챙길 수 있음)
        Transform aid = CampingItem(room, "FirstAidKit", "구급상자", new Vector3(7f, 0.99f, 11.4f), 0f);
        Box(aid, "Case", Vector3.zero, new Vector3(0.35f, 0.18f, 0.25f), matWhite);
        Box(aid, "CrossV", new Vector3(0f, 0.02f, -0.128f), new Vector3(0.04f, 0.12f, 0.012f), matAccent);
        Box(aid, "CrossH", new Vector3(0f, 0.02f, -0.128f), new Vector3(0.12f, 0.04f, 0.012f), matAccent);
    }

    static void FurnishCommon(Transform p)
    {
        Transform room = new GameObject("Room_Common").transform;
        room.SetParent(p, false);

        // 현관에서 이어지는 러그 (콜라이더 없음 — 통행 방해 X)
        Rug(room, new Vector3(0f, 0f, -8f), new Vector3(2.4f, 0.04f, 6f));
        Plant(room, new Vector3(-2.6f, 0f, -11.2f));
        Plant(room, new Vector3(2.6f, 0f, -11.2f));
    }

    // ---------------------------------------------------------------- 상호작용 오브젝트

    /// <summary>챙길 수 있는 캠핑 준비물 그룹. 자식으로 모델 프리미티브를 붙여 쓴다.</summary>
    static Transform CampingItem(Transform parent, string goName, string displayName, Vector3 localPos, float rotY)
    {
        Transform g = Group(parent, goName, localPos, rotY);
        var c = g.gameObject.AddComponent<CollectibleItem>();
        c.SetDisplayName(displayName);
        return g;
    }

    /// <summary>슬라이드 서랍. 로컬 +z 방향(rotY로 지정)으로 열린다. 반환값 아래에 아이템을 넣으면 같이 움직인다.</summary>
    static Transform Drawer(Transform parent, Vector3 localPos, float rotY, Vector3 traySize, float slide)
    {
        Transform g = Group(parent, "Drawer", localPos, rotY);
        Box(g, "Tray", Vector3.zero, traySize, matWood);
        Box(g, "Front", new Vector3(0f, 0f, traySize.z * 0.5f + 0.03f),
            new Vector3(traySize.x + 0.08f, traySize.y + 0.06f, 0.06f), matWood);
        Box(g, "Knob", new Vector3(0f, 0f, traySize.z * 0.5f + 0.08f),
            new Vector3(0.06f, 0.06f, 0.06f), matDark);

        var o = g.gameObject.AddComponent<Openable>();
        o.mode = Openable.Mode.Slide;
        o.movingPart = g;
        o.slideOffset = new Vector3(0f, 0f, slide);
        o.duration = 0.25f;
        o.SetDisplayName("서랍");
        return g;
    }

    /// <summary>냉장고: 패널로 속이 빈 본체 + 힌지 문 + 안에 냉동참치.</summary>
    static void FridgeWithTuna(Transform parent, Vector3 pos, float rotY)
    {
        Transform g = Group(parent, "Fridge", pos, rotY); // 로컬 +z = 문 방향

        Box(g, "Bottom", new Vector3(0f, 0.05f, 0f), new Vector3(1f, 0.1f, 0.8f), matMetal);
        Box(g, "Top", new Vector3(0f, 1.85f, 0f), new Vector3(1f, 0.1f, 0.8f), matMetal);
        Box(g, "Back", new Vector3(0f, 0.95f, -0.36f), new Vector3(1f, 1.8f, 0.08f), matMetal);
        Box(g, "Left", new Vector3(-0.46f, 0.95f, 0f), new Vector3(0.08f, 1.8f, 0.8f), matMetal);
        Box(g, "Right", new Vector3(0.46f, 0.95f, 0f), new Vector3(0.08f, 1.8f, 0.8f), matMetal);
        Box(g, "Shelf", new Vector3(0f, 0.85f, 0f), new Vector3(0.84f, 0.05f, 0.6f), matWhite);

        // 문 (힌지 피벗은 문의 오른쪽 모서리)
        Transform pivot = Group(g, "DoorPivot", new Vector3(0.5f, 0.95f, 0.4f), 0f);
        Box(pivot, "Door", new Vector3(-0.5f, 0f, 0.04f), new Vector3(1f, 1.9f, 0.08f), matMetal);
        Box(pivot, "Handle", new Vector3(-0.88f, 0f, 0.12f), new Vector3(0.06f, 0.6f, 0.06f), matDark);

        var o = g.gameObject.AddComponent<Openable>();
        o.mode = Openable.Mode.Hinge;
        o.movingPart = pivot;
        o.hingeOpenEuler = new Vector3(0f, 110f, 0f);
        o.duration = 0.35f;
        o.SetDisplayName("냉장고");

        // 냉동참치 (선반 위, 문을 열어야 보임/집을 수 있음)
        FrozenTuna(g, new Vector3(0f, 0.9f, 0.03f), 90f);

        // 물병 (챙길 수 있음, 냉장고 안)
        Transform bottle = CampingItem(g, "WaterBottle", "물병", new Vector3(-0.24f, 1.02f, 0.12f), 0f);
        Prim(bottle, "Body", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.11f, 0.14f, 0.11f), matFabric);
        Box(bottle, "Cap", new Vector3(0f, 0.17f, 0f), new Vector3(0.06f, 0.05f, 0.06f), matDark);
    }

    /// <summary>무기: 국자.</summary>
    static void Ladle(Transform parent, Vector3 pos, float rotY)
    {
        Transform g = Group(parent, "Ladle", pos, rotY);
        Box(g, "Handle", new Vector3(0f, 0.03f, -0.05f), new Vector3(0.04f, 0.04f, 0.5f), matMetal);
        Prim(g, "Bowl", PrimitiveType.Sphere, new Vector3(0f, 0.06f, 0.24f), new Vector3(0.18f, 0.12f, 0.18f), matMetal);

        var w = g.gameObject.AddComponent<Weapon>();
        w.SetDisplayName("국자");
        w.holdPosition = new Vector3(0f, -0.08f, -0.1f);
        w.holdEuler = new Vector3(-70f, 0f, 0f);
    }

    /// <summary>무기: 냉동참치.</summary>
    static void FrozenTuna(Transform parent, Vector3 localPos, float rotY)
    {
        Transform g = Group(parent, "FrozenTuna", localPos, rotY);
        Prim(g, "Body", PrimitiveType.Sphere, new Vector3(0f, 0.13f, 0.02f), new Vector3(0.22f, 0.26f, 0.6f), matTuna);
        Box(g, "Tail", new Vector3(0f, 0.13f, -0.36f), new Vector3(0.04f, 0.3f, 0.12f), matTuna);
        Box(g, "FinTop", new Vector3(0f, 0.29f, 0.05f), new Vector3(0.03f, 0.1f, 0.15f), matTuna);

        var w = g.gameObject.AddComponent<Weapon>();
        w.SetDisplayName("냉동참치");
        w.holdPosition = new Vector3(0f, -0.05f, -0.05f);
        w.holdEuler = new Vector3(-75f, 0f, 0f);
    }

    // ---------------------------------------------------------------- 장식용 주방 도구

    static void Pan(Transform parent, Vector3 pos)
    {
        Transform g = Group(parent, "Pan", pos, 0f);
        Prim(g, "Base", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.5f, 0.025f, 0.5f), matDark);
        Box(g, "Handle", new Vector3(0f, 0.01f, 0.4f), new Vector3(0.06f, 0.03f, 0.35f), matDark);
    }

    static void Pot(Transform parent, Vector3 pos)
    {
        Transform g = Group(parent, "Pot", pos, 0f);
        Prim(g, "Body", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.4f, 0.12f, 0.4f), matMetal);
        Prim(g, "Lid", PrimitiveType.Cylinder, new Vector3(0f, 0.13f, 0f), new Vector3(0.42f, 0.015f, 0.42f), matDark);
        Box(g, "Knob", new Vector3(0f, 0.17f, 0f), new Vector3(0.06f, 0.05f, 0.06f), matDark);
    }

    static void Spatula(Transform parent, Vector3 pos)
    {
        Transform g = Group(parent, "Spatula", pos, 20f);
        Box(g, "Handle", new Vector3(0f, 0.02f, -0.1f), new Vector3(0.03f, 0.02f, 0.3f), matWood);
        Box(g, "Blade", new Vector3(0f, 0.02f, 0.12f), new Vector3(0.12f, 0.015f, 0.14f), matMetal);
    }

    // ---------------------------------------------------------------- 일반 가구

    static void Sofa(Transform parent, Vector3 pos, float rotY)
    {
        Transform g = Group(parent, "Sofa", pos, rotY);
        Box(g, "Base", new Vector3(0f, 0.25f, 0f), new Vector3(2.4f, 0.5f, 0.9f), matFabric);
        Box(g, "Back", new Vector3(0f, 0.65f, -0.35f), new Vector3(2.4f, 0.7f, 0.2f), matFabric);
        Box(g, "ArmL", new Vector3(-1.1f, 0.45f, 0f), new Vector3(0.2f, 0.7f, 0.9f), matFabric);
        Box(g, "ArmR", new Vector3(1.1f, 0.45f, 0f), new Vector3(0.2f, 0.7f, 0.9f), matFabric);
    }

    static void CoffeeTable(Transform parent, Vector3 pos, float rotY)
    {
        Transform g = Group(parent, "CoffeeTable", pos, rotY);
        Box(g, "Top", new Vector3(0f, 0.4f, 0f), new Vector3(1.2f, 0.1f, 0.6f), matWood);
        Legs(g, 0.5f, 0.2f, 0.08f, 0.25f);
    }

    static void TvUnit(Transform parent, Vector3 pos, float rotY)
    {
        Transform g = Group(parent, "TvUnit", pos, rotY);
        Box(g, "Stand", new Vector3(0f, 0.25f, 0f), new Vector3(2f, 0.5f, 0.5f), matDark);
        Box(g, "Tv", new Vector3(0f, 1.25f, -0.1f), new Vector3(1.6f, 0.9f, 0.08f), matDark);
        Box(g, "Screen", new Vector3(0f, 1.25f, -0.05f), new Vector3(1.4f, 0.7f, 0.02f), matFabric);
    }

    static void Bed(Transform parent, Vector3 pos, float rotY)
    {
        Transform g = Group(parent, "Bed", pos, rotY);
        Box(g, "Frame", new Vector3(0f, 0.2f, 0f), new Vector3(2.2f, 0.4f, 3f), matWood);
        Box(g, "Mattress", new Vector3(0f, 0.5f, 0.1f), new Vector3(2f, 0.3f, 2.6f), matWhite);
        Box(g, "Pillow", new Vector3(0f, 0.7f, 1.05f), new Vector3(1.6f, 0.2f, 0.5f), matWhite);
        Box(g, "Blanket", new Vector3(0f, 0.57f, -0.3f), new Vector3(2f, 0.12f, 1.8f), matFabric);
    }

    /// <summary>협탁 + 앞쪽(남쪽)으로 열리는 서랍.</summary>
    static void NightstandWithDrawer(Transform parent, Vector3 pos, float rotY)
    {
        Transform g = Group(parent, "Nightstand", pos, rotY);
        Box(g, "Body", new Vector3(0f, 0.25f, 0f), new Vector3(0.6f, 0.5f, 0.5f), matWood);
        Drawer(g, new Vector3(0f, 0.35f, -0.02f), 180f, new Vector3(0.45f, 0.18f, 0.4f), 0.3f);
    }

    static void Wardrobe(Transform parent, Vector3 pos, float rotY)
    {
        Transform g = Group(parent, "Wardrobe", pos, rotY);
        Box(g, "Body", new Vector3(0f, 1.1f, 0f), new Vector3(1.6f, 2.2f, 0.6f), matWood);
        Box(g, "DoorGap", new Vector3(0f, 1.1f, 0.31f), new Vector3(0.04f, 2f, 0.02f), matDark);
    }

    /// <summary>책상 + 우측 서랍. 서랍 Transform을 반환한다.</summary>
    static Transform Desk(Transform parent, Vector3 pos, float rotY)
    {
        Transform g = Group(parent, "Desk", pos, rotY);
        Box(g, "Top", new Vector3(0f, 0.76f, 0f), new Vector3(1.8f, 0.08f, 0.8f), matWood);
        Box(g, "SideL", new Vector3(-0.85f, 0.38f, 0f), new Vector3(0.08f, 0.76f, 0.7f), matWood);
        Box(g, "SideR", new Vector3(0.85f, 0.38f, 0f), new Vector3(0.08f, 0.76f, 0.7f), matWood);
        Box(g, "BackPanel", new Vector3(0f, 0.45f, 0.32f), new Vector3(1.7f, 0.5f, 0.06f), matWood);
        return Drawer(g, new Vector3(0.45f, 0.55f, -0.05f), 180f, new Vector3(0.6f, 0.18f, 0.5f), 0.35f);
    }

    static void DiningTable(Transform parent, Vector3 pos, float rotY)
    {
        Transform g = Group(parent, "DiningTable", pos, rotY);
        Box(g, "Top", new Vector3(0f, 0.75f, 0f), new Vector3(1.6f, 0.1f, 1f), matWood);
        Legs(g, 0.7f, 0.375f, 0.1f, 0.4f);
    }

    static void Chair(Transform parent, Vector3 pos, float rotY)
    {
        Transform g = Group(parent, "Chair", pos, rotY);
        Box(g, "Seat", new Vector3(0f, 0.45f, 0f), new Vector3(0.5f, 0.08f, 0.5f), matWood);
        Box(g, "Back", new Vector3(0f, 0.7f, -0.21f), new Vector3(0.5f, 0.5f, 0.08f), matWood);
        Legs(g, 0.2f, 0.225f, 0.06f, 0.2f);
    }

    static void Bathtub(Transform parent, Vector3 pos, float rotY)
    {
        Transform g = Group(parent, "Bathtub", pos, rotY);
        Box(g, "Shell", new Vector3(0f, 0.3f, 0f), new Vector3(1.8f, 0.6f, 0.9f), matWhite);
        Box(g, "Water", new Vector3(0f, 0.45f, 0f), new Vector3(1.5f, 0.4f, 0.6f), matFabric);
    }

    static void Toilet(Transform parent, Vector3 pos, float rotY)
    {
        Transform g = Group(parent, "Toilet", pos, rotY);
        Box(g, "Base", new Vector3(0f, 0.2f, 0.05f), new Vector3(0.5f, 0.4f, 0.6f), matWhite);
        Box(g, "Seat", new Vector3(0f, 0.42f, 0.08f), new Vector3(0.5f, 0.08f, 0.55f), matWhite);
        Box(g, "Tank", new Vector3(0f, 0.6f, -0.3f), new Vector3(0.5f, 0.6f, 0.2f), matWhite);
    }

    static void Sink(Transform parent, Vector3 pos, float rotY)
    {
        Transform g = Group(parent, "Sink", pos, rotY);
        Box(g, "Pedestal", new Vector3(0f, 0.4f, 0f), new Vector3(0.35f, 0.8f, 0.35f), matWhite);
        Box(g, "Basin", new Vector3(0f, 0.85f, 0f), new Vector3(0.6f, 0.2f, 0.45f), matWhite);
    }

    static void Washer(Transform parent, Vector3 pos, float rotY)
    {
        Transform g = Group(parent, "Washer", pos, rotY);
        Box(g, "Body", new Vector3(0f, 0.45f, 0f), new Vector3(0.8f, 0.9f, 0.7f), matWhite);
        Box(g, "Door", new Vector3(0f, 0.45f, 0.36f), new Vector3(0.5f, 0.5f, 0.04f), matDark);
    }

    static void Bookshelf(Transform parent, Vector3 pos, float rotY)
    {
        Transform g = Group(parent, "Bookshelf", pos, rotY);
        Box(g, "Body", new Vector3(0f, 1f, 0f), new Vector3(1.2f, 2f, 0.4f), matWood);
        Box(g, "Shelf1", new Vector3(0f, 0.55f, 0.05f), new Vector3(1.1f, 0.05f, 0.35f), matDark);
        Box(g, "Shelf2", new Vector3(0f, 1.05f, 0.05f), new Vector3(1.1f, 0.05f, 0.35f), matDark);
        Box(g, "Shelf3", new Vector3(0f, 1.55f, 0.05f), new Vector3(1.1f, 0.05f, 0.35f), matDark);
    }

    /// <summary>러그 — 통행에 걸리지 않도록 콜라이더를 제거한다.</summary>
    static void Rug(Transform parent, Vector3 pos, Vector3 size)
    {
        var go = Box(parent, "Rug", new Vector3(pos.x, 0.065f, pos.z), size, matAccent);
        Object.DestroyImmediate(go.GetComponent<Collider>());
    }

    static void Plant(Transform parent, Vector3 pos)
    {
        Transform g = Group(parent, "Plant", pos, 0f);
        Box(g, "Pot", new Vector3(0f, 0.2f, 0f), new Vector3(0.4f, 0.4f, 0.4f), matWood);
        Prim(g, "Foliage", PrimitiveType.Sphere, new Vector3(0f, 0.75f, 0f), new Vector3(0.7f, 0.8f, 0.7f), matPlant);
    }

    /// <summary>테이블/의자 다리 4개를 모서리에 생성.</summary>
    static void Legs(Transform g, float halfX, float legHalfH, float thick, float halfZ)
    {
        float y = legHalfH;
        float h = legHalfH * 2f;
        Box(g, "Leg", new Vector3(halfX, y, halfZ), new Vector3(thick, h, thick), matWood);
        Box(g, "Leg", new Vector3(-halfX, y, halfZ), new Vector3(thick, h, thick), matWood);
        Box(g, "Leg", new Vector3(halfX, y, -halfZ), new Vector3(thick, h, thick), matWood);
        Box(g, "Leg", new Vector3(-halfX, y, -halfZ), new Vector3(thick, h, thick), matWood);
    }

    // ---------------------------------------------------------------- 유틸

    static Transform Group(Transform parent, string name, Vector3 localPos, float rotY)
    {
        var g = new GameObject(name).transform;
        g.SetParent(parent, false);
        g.localPosition = localPos;
        g.localRotation = Quaternion.Euler(0f, rotY, 0f);
        return g;
    }

    static GameObject Box(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat)
    {
        return Prim(parent, name, PrimitiveType.Cube, localPos, size, mat);
    }

    static GameObject Prim(Transform parent, string name, PrimitiveType type, Vector3 localPos, Vector3 size, Material mat)
    {
        return Prim(parent, name, type, localPos, Vector3.zero, size, mat);
    }

    static GameObject Prim(Transform parent, string name, PrimitiveType type, Vector3 localPos, Vector3 euler, Vector3 size, Material mat)
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

    static void AddRoomLight(Transform parent, string name, Vector3 localPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var l = go.AddComponent<Light>();
        // 천장이 태양광을 막으므로 실내는 포인트 라이트가 주 광원이다.
        l.type = LightType.Point;
        l.range = 15f;
        l.intensity = 1.9f;
        l.color = new Color(1f, 0.96f, 0.88f);
        l.shadows = LightShadows.Soft;
    }

    static void LoadMaterials()
    {
        matWall = GetMat("Wall", new Color(0.85f, 0.82f, 0.78f));
        matFloor = GetMat("Floor", new Color(0.72f, 0.58f, 0.42f));
        matWood = GetMat("Wood", new Color(0.55f, 0.36f, 0.20f));
        matFabric = GetMat("Fabric", new Color(0.30f, 0.45f, 0.65f));
        matWhite = GetMat("White", new Color(0.92f, 0.92f, 0.92f));
        matMetal = GetMat("Metal", new Color(0.72f, 0.73f, 0.75f));
        matDark = GetMat("Dark", new Color(0.12f, 0.12f, 0.13f));
        matAccent = GetMat("Accent", new Color(0.60f, 0.20f, 0.25f));
        matPlant = GetMat("Plant", new Color(0.20f, 0.50f, 0.22f));
        matTuna = GetMat("Tuna", new Color(0.55f, 0.65f, 0.72f));
        matOrange = GetMat("Orange", new Color(0.9f, 0.45f, 0.1f));
    }

    static Material GetMat(string name, Color color) => BuilderMaterials.Ensure("House_" + name, color);
}
#endif
