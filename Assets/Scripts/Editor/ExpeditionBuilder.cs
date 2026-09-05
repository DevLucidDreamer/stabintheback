#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Mirror;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class ExpeditionBuilder
{
    public const string BridgeScene = "Assets/Scenes/Stage2_BrokenBridge.unity";
    public const string AltarScene = "Assets/Scenes/Stage3_UndergroundAltar.unity";
    private static Transform root;
    private static Material stone, floor, dark, metal, light;
    private static readonly string[] Glyphs = { "A", "V", "X", "O" };
    private static readonly List<ExpeditionNode> nodes = new List<ExpeditionNode>();
    private static readonly List<ExpeditionCargo> cargo = new List<ExpeditionCargo>();

    [MenuItem("Tools/Stage/Build Stages 2 and 3")]
    public static void Build()
    {
        stone = TempleSurfaceBuilder.Surface("Temple_Sandstone", new Color(.82f, .84f, .86f), false);
        floor = TempleSurfaceBuilder.Surface("Temple_Limestone", new Color(.8f, .82f, .84f), true);
        dark = TempleSurfaceBuilder.Surface("Temple_Basalt", new Color(.48f, .50f, .53f), false);
        metal = BuilderMaterials.Ensure("Mine_Iron", new Color(.17f, .19f, .20f));
        light = BuilderMaterials.Ensure("Mine_LanternLight", new Color(.72f, .77f, .72f));
        light.EnableKeyword("_EMISSION"); light.SetColor("_EmissionColor", new Color(1.4f, 1.6f, 1.3f));
        BuildLantern(); CampaignWeaponBuilder.Build(); BuildScene(2); BuildScene(3); StoneTempleBuilder.SetBuildOrder(); AssetDatabase.SaveAssets();
    }
    private static void BuildScene(int stage)
    {
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            nodes.Clear(); cargo.Clear(); root = new GameObject(stage == 2 ? "BrokenBridgeMine" : "UndergroundAltar").transform;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.49f, .54f, .60f);
            RenderSettings.ambientEquatorColor = new Color(.39f, .43f, .49f);
            RenderSettings.ambientGroundColor = new Color(.20f, .23f, .27f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(.055f, .065f, .08f); RenderSettings.fogDensity = .009f;
            var manager = new GameObject("ExpeditionManager", typeof(NetworkIdentity)).AddComponent<ExpeditionManager>();
            manager.stage = stage; manager.plates = new TemplePressurePlate[0]; manager.sockets = new Transform[0];
            var visuals = manager.gameObject.AddComponent<ExpeditionVisuals>();
            visuals.bridgeStones = new GameObject[0]; visuals.bridgeFloors = new Collider[0];
            visuals.gates = new Transform[0]; visuals.gateColliders = new Collider[0]; visuals.handLights = new Renderer[0];
            manager.gameObject.AddComponent<StoneTempleHud>();
            Shell(stage);
            if (stage == 2) Bridge(manager, visuals); else Altar(manager, visuals);
            manager.nodes = nodes.ToArray(); manager.cargo = cargo.ToArray();
            for (int i = 0; i < 4; i++)
            {
                var spawn = new GameObject("ExpeditionSpawn_" + i);
                spawn.transform.position = new Vector3((i - 1.5f) * 1.7f, 1.15f, 5f);
                spawn.AddComponent<NetworkStartPosition>();
            }
            new GameObject("WeaponManager", typeof(NetworkIdentity)).AddComponent<WeaponNetworkManager>();
            EditorSceneManager.SaveScene(scene, stage == 2 ? BridgeScene : AltarScene);
            Capture(scene, stage);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }
    private static void Shell(int stage)
    {
        Box("Ceiling", new Vector3(0, 13, 37), new Vector3(25, 1, 76), stone);
        Box("WestWall", new Vector3(-12, 6, 37), new Vector3(1, 14, 76), stone);
        Box("EastWall", new Vector3(12, 6, 37), new Vector3(1, 14, 76), stone);
        Box("EntryWall", new Vector3(0, 6, 0), new Vector3(24, 14, 1), dark);
        Box("EndWall", new Vector3(0, 6, 74), new Vector3(24, 14, 1), dark);
        if (stage == 2) { Floor(0, 22); Floor(46, 74); }
        else Floor(0, 74);
        Box("Abyss", new Vector3(0, -14, 35), new Vector3(24, 1, 30), dark, null, false);
        for (int z = 5; z < 74; z += 8)
        {
            Box("VaultRib", new Vector3(0, 12, z), new Vector3(24, .65f, .8f), dark);
            foreach (int side in new[] { -1, 1 })
            {
                Box("TallColumn", new Vector3(side * 10.8f, 6, z), new Vector3(.8f, 12, 1.1f), dark);
                Box("LampMount", new Vector3(side * 10.2f, 4, z), new Vector3(.5f, .8f, .5f), metal);
                Box("Lamp", new Vector3(side * 9.9f, 4.1f, z), new Vector3(.18f, .5f, .32f), light, null, false);
                Lamp(new Vector3(side * 8f, 5, z), 5f, 14f);
            }
        }
    }
    private static void Bridge(ExpeditionManager manager, ExpeditionVisuals visuals)
    {
        var parts = new List<GameObject>(); var colliders = new List<Collider>();
        for (int segment = 0; segment < 8; segment++)
        {
            float z = 23.5f + segment * 3f;
            for (int lane = 0; lane < 3; lane++)
            {
                var part = Box("BridgeStone_" + (segment * 3 + lane), new Vector3((lane - 1) * 2, -.4f, z), new Vector3(1.98f, .8f, 2.98f), floor, null, false);
                part.SetActive(false); parts.Add(part);
            }
            var support = new GameObject("FixedBridgeFloor_" + segment); support.transform.SetParent(root);
            support.transform.position = new Vector3(0, -.4f, z);
            var col = support.AddComponent<BoxCollider>(); col.size = new Vector3(6, .8f, 3); col.enabled = false; colliders.Add(col);
            Box("OldBridgePier", new Vector3(0, -5, z), new Vector3(1.4f, 7, 1.2f), dark, null, false);
        }
        visuals.bridgeStones = parts.ToArray(); visuals.bridgeFloors = colliders.ToArray();
        visuals.buildPost = Node("MasonryBench", manager.BuildPosition, ExpeditionAction.Build).transform;
        for (int i = 0; i < 4; i++)
        {
            float x = new[] { -8f, -4f, 4f, 8f }[i];
            var item = Cargo("QuarryStone_" + i, new Vector3(x, .45f, 12), i);
            for (int j = 0; j < 3; j++) Box("QuarryPile", new Vector3(x, .3f + j * .55f, 13.5f), new Vector3(1.2f - j * .2f, .6f, .9f), dark);
            Box("StoneSupplyPlinth", new Vector3(x, -.1f, 12), new Vector3(2, .2f, 2), dark);
        }
        // A broken lintel and mine cart silhouette frame the reward alcove.
        Box("MineLintel", new Vector3(-6, 4, 57), new Vector3(6, .7f, 1), dark);
        for (int side = -1; side <= 1; side += 2) Box("MinePost", new Vector3(-6 + side * 2.6f, 2, 57), new Vector3(.6f, 4, .7f), dark);
        Box("LanternPlinth", new Vector3(-6, .5f, 59), new Vector3(2, 1, 2), dark);
        manager.lantern = SpawnWeapon("MineLantern", new Vector3(-6, 1.85f, 59), 0);
        SpawnWeapon("ChaliceBottle", new Vector3(6, 1.55f, 7), 1);
        var secretPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/CampaignWeapons/SecretTuna.prefab");
        var secret = (GameObject)PrefabUtility.InstantiatePrefab(secretPrefab, SceneManager.GetActiveScene());
        secret.name = "ForgottenFrozenTuna"; secret.transform.position = new Vector3(-9.2f, 1.1f, 18.5f);
        secret.GetComponent<Weapon>().SetWeaponId(2);
        Box("AbandonedStoneCrate", new Vector3(-7.8f, .9f, 18), new Vector3(1.1f, 1.8f, 2.3f), dark);
        Box("ChalicePlinth", new Vector3(6, .5f, 7), new Vector3(1.5f, 1, 1.5f), dark);
        Gate(69, out Transform gate, out Collider collider);
        visuals.gates = new[] { gate }; visuals.gateColliders = new[] { collider };
        Node("DescentStair", new Vector3(0, .8f, 72), ExpeditionAction.Exit);
        for (int i = 0; i < 4; i++) Box("DescentMark", new Vector3(0, .015f, 63 + i * 1.2f), new Vector3(3 - i * .3f, .02f, .15f), dark, null, false);
    }
    private static void Altar(ExpeditionManager manager, ExpeditionVisuals visuals)
    {
        var socketTransforms = new List<Transform>();
        for (int i = 0; i < 4; i++)
        {
            var item = Cargo("Statue_" + Glyphs[i], new Vector3((i - 1.5f) * 4.5f, .45f, 11), i);
            // Full-height stylised stone figures with matching carved sigils.
            Box("StatueBody", new Vector3(0, .55f, 0), new Vector3(.65f, .7f, .5f), stone, item.transform, false);
            Box("StatueHead", new Vector3(0, 1.05f, 0), Vector3.one * .48f, floor, item.transform, false);
            item.GetComponent<BoxCollider>().center = Vector3.up * .5f;
            item.GetComponent<BoxCollider>().size = new Vector3(.9f, 2f, .9f);
            Glyph(Glyphs[i], new Vector3(0, .6f, -.265f), item.transform, .6f, false);
            var node = Node("StatueSocket_" + i, new Vector3((i - 1.5f) * 4.5f, .2f, 22), ExpeditionAction.Socket, i);
            node.transform.localScale = new Vector3(1.7f, .4f, 1.7f);
            socketTransforms.Add(node.transform);
            Glyph(Glyphs[manager.socketKinds[i]], new Vector3((i - 1.5f) * 4.5f, 2.4f, 25.4f), root, 1.1f, false);
        }
        manager.sockets = socketTransforms.ToArray();
        Gate(26, out Transform statueGate, out Collider statueCollider);
        var pressurePlates = new List<TemplePressurePlate>();
        string[] runes = { "A", "B", "X", "Y", "C", "V", "D", "O" };
        for (int i = 0; i < 8; i++)
        {
            var go = new GameObject("RunePlate_" + runes[i]); go.transform.SetParent(root);
            go.transform.position = new Vector3((i % 4 - 1.5f) * 4, .03f, i < 4 ? 35 : 42);
            Box("PlateRim", Vector3.zero, new Vector3(2.4f, .035f, 2.4f), dark, go.transform, false);
            var top = Box("PlateFace", Vector3.up * .045f, new Vector3(2.1f, .08f, 2.1f), floor, go.transform, false);
            var plate = go.AddComponent<TemplePressurePlate>(); plate.index = i; plate.visual = top.transform; pressurePlates.Add(plate);
            Glyph(runes[i], new Vector3(0, .51f, 0), top.transform, .65f, true);
        }
        manager.plates = pressurePlates.ToArray();
        Gate(49, out Transform runeGate, out Collider runeCollider);
        for (int i = 0; i < 4; i++) Glyph(new[] { "A", "X", "V", "O" }[i], new Vector3((i - 1.5f) * 1.4f, 6.6f, 48.45f), root, .9f, false);
        visuals.gates = new[] { statueGate, runeGate }; visuals.gateColliders = new[] { statueCollider, runeCollider };
        Box("CentralAltar", new Vector3(0, .7f, 61), new Vector3(3.5f, 1.4f, 3.5f), dark);
        SpawnWeapon("ChaliceBottle", new Vector3(0, 1.95f, 61), 1);
        SpawnWeapon("MineLantern", new Vector3(7, 1.4f, 6), 0);
        var handLights = new List<Renderer>();
        for (int i = 0; i < 4; i++)
        {
            Vector3 p = new Vector3(i % 2 == 0 ? -4f : 4f, .9f, i < 2 ? 57 : 65);
            var node = Node("RitualHand_" + i, p, ExpeditionAction.Altar, i);
            Box("HandSlab", Vector3.up * .6f, new Vector3(1.1f, .15f, 1.1f), floor, node.transform, false);
            var glow = Box("RitualEmber", new Vector3(0, .69f, 0), new Vector3(.35f, .04f, .35f), light, node.transform, false);
            handLights.Add(glow.GetComponent<Renderer>());
            Glyph(Glyphs[i], new Vector3(p.x, .03f, p.z - 1.2f), root, .7f, true);
        }
        visuals.handLights = handLights.ToArray();
        visuals.ritualLight = Lamp(new Vector3(0, 4, 61), 1f, 18f);
        visuals.ritualBeam = Box("RitualPillarOfLight", new Vector3(0, 7.3f, 61), new Vector3(.18f, 11f, .18f), light, null, false);
        visuals.ritualBeam.SetActive(false);
        Node("ReturnToLobby", new Vector3(0, .8f, 71), ExpeditionAction.Exit);
    }
    private static ExpeditionCargo Cargo(string name, Vector3 p, int kind)
    {
        var go = Box(name, p, Vector3.one, dark); var collider = go.GetComponent<BoxCollider>(); collider.size = Vector3.one * .9f;
        var item = go.AddComponent<ExpeditionCargo>(); item.index = cargo.Count; item.kind = kind; item.home = p; cargo.Add(item); return item;
    }
    private static ExpeditionNode Node(string name, Vector3 p, ExpeditionAction action, int slot = 0)
    {
        var go = Box(name, p, Vector3.one, dark);
        var node = go.AddComponent<ExpeditionNode>(); node.index = nodes.Count; node.action = action; node.slot = slot;
        nodes.Add(node); return node;
    }
    private static void Gate(float z, out Transform visual, out Collider collider)
    {
        Box("DoorWallLeft", new Vector3(-8, 6, z), new Vector3(8, 13, 1), stone);
        Box("DoorWallRight", new Vector3(8, 6, z), new Vector3(8, 13, 1), stone);
        Box("DoorLintel", new Vector3(0, 9, z), new Vector3(8, 7, 1), stone);
        var anchor = new GameObject("GateAnchor"); anchor.transform.SetParent(root); anchor.transform.position = new Vector3(0, 2.75f, z);
        var moving = new GameObject("GateVisual"); moving.transform.SetParent(anchor.transform, false); visual = moving.transform;
        Box("StoneDoor", Vector3.zero, new Vector3(8, 5.5f, 1), dark, visual, false);
        var col = anchor.AddComponent<BoxCollider>(); col.size = new Vector3(8, 5.5f, 1); collider = col;
    }
    private static void Floor(float from, float to) => Box("Floor", new Vector3(0, -.5f, (from + to) / 2), new Vector3(24, 1, to - from), floor);
    private static GameObject Box(string name, Vector3 p, Vector3 size, Material mat, Transform parent = null, bool collision = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
        go.transform.SetParent(parent != null ? parent : root, false); go.transform.localPosition = p; go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") != null) TempleSurfaceBuilder.ApplyBoxUV(go);
        if (!collision) Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
    }
    private static Light Lamp(Vector3 p, float intensity, float range)
    {
        var lamp = new GameObject("MineLight").AddComponent<Light>(); lamp.transform.SetParent(root);
        lamp.transform.position = p; lamp.type = LightType.Point; lamp.color = new Color(.88f, .94f, 1f);
        lamp.intensity = intensity; lamp.range = range; return lamp;
    }
    private static void Glyph(string text, Vector3 p, Transform parent, float size, bool horizontal)
    {
        var go = new GameObject("CarvedGlyph_" + text); go.transform.SetParent(parent, false);
        go.transform.localPosition = p; go.transform.localRotation = horizontal ? Quaternion.Euler(90, 0, 0) : Quaternion.identity;
        var tmp = go.AddComponent<TextMeshPro>(); tmp.text = text; tmp.font = TMP_Settings.defaultFontAsset;
        tmp.fontSize = 6; tmp.alignment = TextAlignmentOptions.Center; tmp.color = new Color(.47f, .52f, .55f);
        tmp.rectTransform.sizeDelta = new Vector2(2, 2); go.transform.localScale = Vector3.one * size;
        // World-space runes are diegetic puzzle clues, never instruction paragraphs.
    }
    private static Weapon SpawnWeapon(string name, Vector3 p, int id)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Weapons/" + name + ".prefab");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, SceneManager.GetActiveScene());
        go.transform.position = p; var weapon = go.GetComponent<Weapon>(); weapon.SetWeaponId(id); return weapon;
    }
    private static void BuildLantern()
    {
        var go = new GameObject("MineLantern");
        try
        {
            Box("IronBase", new Vector3(0, -.72f, 0), new Vector3(.62f, .15f, .62f), metal, go.transform, false);
            Box("IronCap", new Vector3(0, -.13f, 0), new Vector3(.62f, .12f, .62f), metal, go.transform, false);
            Box("LampCore", new Vector3(0, -.42f, 0), new Vector3(.32f, .45f, .32f), light, go.transform, false);
            for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
                Box("IronCage", new Vector3(x * .25f, -.42f, z * .25f), new Vector3(.06f, .55f, .06f), metal, go.transform, false);
            for (int side = -1; side <= 1; side += 2) Box("HandleSide", new Vector3(side * .2f, .08f, 0), new Vector3(.065f, .35f, .07f), metal, go.transform, false);
            Box("Grip", new Vector3(0, .25f, 0), new Vector3(.46f, .065f, .07f), metal, go.transform, false);
            var col = go.AddComponent<BoxCollider>(); col.center = new Vector3(0, -.28f, 0); col.size = new Vector3(.65f, 1.1f, .65f);
            var weapon = go.AddComponent<Weapon>(); weapon.SetDisplayName("폐광 랜턴"); weapon.groundOffset = .8f;
            weapon.campaignKey = "mine_lantern";
            weapon.holdPosition = new Vector3(.1f, .05f, .15f); weapon.swingReach = 1.35f; weapon.swingRadius = 1f;
            var lamp = new GameObject("PortableLight").AddComponent<Light>(); lamp.transform.SetParent(go.transform, false);
            lamp.transform.localPosition = Vector3.down * .4f; lamp.type = LightType.Point; lamp.range = 7f; lamp.intensity = 2.5f;
            lamp.color = new Color(.9f, 1f, .86f);
            PrefabUtility.SaveAsPrefabAsset(go, "Assets/Prefabs/Weapons/MineLantern.prefab");
        }
        finally { Object.DestroyImmediate(go); }
    }
    private static void Capture(Scene scene, int stage)
    {
        Directory.CreateDirectory("Logs/Expedition"); var otherLights = new List<Light>();
        foreach (var lamp in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (lamp.gameObject.scene != scene && lamp.enabled) { lamp.enabled = false; otherLights.Add(lamp); }
        var layers = new Dictionary<GameObject, int>();
        foreach (var r in scene.GetRootGameObjects()) foreach (var t in r.GetComponentsInChildren<Transform>(true))
        { layers[t.gameObject] = t.gameObject.layer; t.gameObject.layer = 30; }
        var go = new GameObject("Capture"); var cam = go.AddComponent<Camera>(); cam.cullingMask = 1 << 30;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = RenderSettings.fogColor;
        var rt = new RenderTexture(1600, 1000, 24); cam.targetTexture = rt; var old = RenderTexture.active;
        try
        {
            for (int shot = 0; shot < (stage == 3 ? 3 : 1); shot++)
            {
                go.transform.position = stage == 2 ? new Vector3(9, 4, 15) :
                    shot == 0 ? new Vector3(9, 3, 7) : shot == 1 ? new Vector3(-8, 3, 30) : new Vector3(-8, 3, 53);
                go.transform.LookAt(stage == 2 ? new Vector3(0, 1, 34) :
                    shot == 0 ? new Vector3(0, 2, 22) : shot == 1 ? new Vector3(0, 2, 44) : new Vector3(0, 2, 63));
                cam.Render(); RenderTexture.active = rt; var png = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
                png.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); png.Apply();
                File.WriteAllBytes("Logs/Expedition/stage" + stage + (shot == 0 ? "" : "_" + shot) + ".png", png.EncodeToPNG()); Object.DestroyImmediate(png);
            }
        }
        finally
        {
            foreach (var pair in layers) if (pair.Key != null) pair.Key.layer = pair.Value;
            foreach (var lamp in otherLights) if (lamp != null) lamp.enabled = true;
            RenderTexture.active = old; Object.DestroyImmediate(go); Object.DestroyImmediate(rt);
        }
    }
}
#endif
