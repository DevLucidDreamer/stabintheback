#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class StoneTempleBuilder
{
    public const string ScenePath = "Assets/Scenes/Stage1_StoneTemple.unity";
    private static Material stone, dark, tile, cyan, gold, moss;
    private static Transform root;
    private static readonly List<TemplePressurePlate> plates = new List<TemplePressurePlate>();
    private static readonly List<TempleMechanism> motions = new List<TempleMechanism>();
    private static readonly List<TempleInteractable> levers = new List<TempleInteractable>();
    private static readonly List<TempleStone> stones = new List<TempleStone>();

    [MenuItem("Tools/Stage/Build Stage 1 (Stone Temple)")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Stop Play Mode before building.");
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            plates.Clear(); motions.Clear(); levers.Clear(); stones.Clear();
            stone = TempleSurfaceBuilder.Surface("Temple_Sandstone", new Color(0.82f, 0.84f, 0.86f), false);
            dark = TempleSurfaceBuilder.Surface("Temple_Basalt", new Color(0.48f, 0.50f, 0.53f), false);
            tile = TempleSurfaceBuilder.Surface("Temple_Limestone", new Color(0.80f, 0.82f, 0.84f), true);
            cyan = TempleSurfaceBuilder.Surface("Temple_Turquoise", new Color(0.66f, 0.69f, 0.70f), true);
            gold = TempleSurfaceBuilder.Surface("Temple_Gold", new Color(0.69f, 0.68f, 0.65f), true);
            moss = TempleSurfaceBuilder.Surface("Temple_Moss", new Color(0.38f, 0.41f, 0.38f), false);
            foreach (var mat in new[] { cyan, gold })
            { mat.DisableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", Color.black); }
            root = new GameObject("StoneTemple_FourPlayers").transform;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.56f, 0.60f, 0.66f);
            RenderSettings.ambientEquatorColor = new Color(0.43f, 0.47f, 0.53f);
            RenderSettings.ambientGroundColor = new Color(0.23f, 0.26f, 0.30f);
            RenderSettings.fog = true; RenderSettings.fogColor = new Color(0.08f, 0.09f, 0.11f);
            RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = 0.008f;

            // No foundation under the gaps. Side walls prevent walking around the puzzles.
            Floor(0, 26); Floor(36, 74); Floor(80, 84); Floor(90, 108); Floor(116, 145);
            Box("Abyss", new Vector3(0, -13, 73), new Vector3(22, 1, 150), dark, null, false);
            Box("WestWall", new Vector3(-11, 6, 70), new Vector3(1, 12, 150), stone);
            Box("EastWall", new Vector3(11, 6, 70), new Vector3(1, 12, 150), stone);
            Box("EntranceWall", new Vector3(0, 6, 0), new Vector3(22, 12, 1), stone);
            Box("SealedStoneCeiling", new Vector3(0, 12, 70), new Vector3(23, 0.7f, 150), stone);
            Box("EndBalustrade", new Vector3(0, 1, 145), new Vector3(22, 2, 0.7f), dark);
            for (int z = 3; z <= 141; z += 7)
                for (int side = -1; side <= 1; side += 2) Pillar(side * 9.8f, z);
            for (int z = 3; z <= 143; z += 14)
            {
                Box("CeilingRib", new Vector3(0, 11.2f, z), new Vector3(21, 0.75f, 1.1f), dark);
                for (int side = -1; side <= 1; side += 2)
                    Box("HighCorbel", new Vector3(side * 8.9f, 10.65f, z), new Vector3(2.7f, 0.65f, 1.4f), tile);
            }
            AddInteriorLights();

            Gate(0, 16);
            Bridge(1, 0, 26, 36, 4.8f, false);
            Gate(2, 62);
            Bridge(3, -5, 74, 90, 3.3f, true);
            Bridge(4, 5, 74, 90, 3.3f, true);
            Bridge(5, 0, 108, 116, 4.8f, false);
            Gate(6, 122);
            // Relic cage is a small raised cover, outside the four rally pads.
            Mechanism(7, "ChaliceSeal", new Vector3(0, 1.65f, 130), new Vector3(2.2f, 3.3f, 2.2f), true, cyan);
            Gate(8, 135);
            Gate(9, 98);

            Plate(0, -4, 10); Plate(1, -4, 22); Plate(2, 4, 40);
            Plate(3, 4, 56, true);
            Plate(4, -5, 70); Plate(5, 5, 70); Plate(6, -5, 82); Plate(7, 5, 82);
            Plate(8, -4, 104, true);
            Plate(9, -4.2f, 126); Plate(10, -1.4f, 126); Plate(11, 1.4f, 126); Plate(12, 4.2f, 126);
            Lever(0, 3, 19); Lever(1, -3, 43); Lever(2, -3, 65);
            Lever(3, -5, 94); Lever(4, 5, 94); Lever(5, 0, 119);
            Stone(0, -5, 51); Stone(1, 5, 103);

            // Center divider keeps the twin lanes distinct while allowing everyone to see each other.
            Box("TwinLaneDivider", new Vector3(0, 1.6f, 82), new Vector3(0.5f, 3.2f, 23), dark);
            foreach (int side in new[] { -1, 1 })
            {
                Line(new Vector3(side * 5, 0.015f, 70), new Vector3(-side * 5, 0.015f, 72), side < 0 ? cyan : gold);
                Line(new Vector3(side * 5, 0.015f, 82), new Vector3(-side * 5, 0.015f, 83), side < 0 ? cyan : gold);
            }

            WeaponPrefabBuilder.BuildChalice();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponPrefabBuilder.PrefabPath("ChaliceBottle"));
            var relic = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            relic.name = "성배병"; relic.transform.position = new Vector3(0, 1.6f, 130);
            var weapon = relic.GetComponent<Weapon>(); weapon.SetWeaponId(0);
            relic.transform.position = new Vector3(0, 1.03f + weapon.groundOffset, 130);
            Box("RelicPlinth", new Vector3(0, 0.5f, 130), new Vector3(1.5f, 1, 1.5f), dark);
            for (int i = 0; i < 4; i++)
            {
                var spawn = new GameObject("TempleSpawn_" + i);
                spawn.transform.position = new Vector3((i - 1.5f) * 1.7f, 1.15f, 5);
                spawn.AddComponent<NetworkStartPosition>();
            }
            var manager = new GameObject("StoneTempleManager", typeof(NetworkIdentity)).AddComponent<StoneTempleManager>();
            manager.plates = plates.ToArray(); manager.mechanisms = motions.ToArray();
            manager.levers = levers.ToArray(); manager.stones = stones.ToArray(); manager.chalice = weapon;
            manager.gameObject.AddComponent<StoneTempleHud>();
            new GameObject("WeaponManager", typeof(NetworkIdentity)).AddComponent<WeaponNetworkManager>();
            var returnStone = Box("DescendToMine", new Vector3(0, 1, 140), new Vector3(1.2f, 2, 1.2f), cyan);
            returnStone.AddComponent<TempleInteractable>().returnToLobby = true;
            manager.exit = returnStone.transform;
            // Broken bridge beyond this exit previews the next playable scene.
            for (int i = 0; i < 3; i++) Box("BrokenBridgeVista", new Vector3(i == 1 ? 2 : 0, -1f - i * 0.5f, 151 + i * 7), new Vector3(5, 1.5f, 3), stone);
            EditorSceneManager.SaveScene(scene, ScenePath);
            SetBuildOrder();
            AssetDatabase.SaveAssets();
            Capture(scene);
            Debug.Log("[StoneTemple] Scene generated: five rooms, 13 plates, 4 spawns, native chalice prefab.");
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }

    public static void SetBuildOrder()
    {
        var scenes = new[] {
            new EditorBuildSettingsScene("Assets/Scenes/MainTitle.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/Lobby.unity", true),
            new EditorBuildSettingsScene(ScenePath, true),
            new EditorBuildSettingsScene(ExpeditionBuilder.BridgeScene, true),
            new EditorBuildSettingsScene(ExpeditionBuilder.AltarScene, true)
        };
        foreach (var scene in scenes)
        {
            string guid = AssetDatabase.AssetPathToGUID(scene.path);
            if (!string.IsNullOrEmpty(guid)) scene.guid = new GUID(guid);
        }
        EditorBuildSettings.scenes = scenes;
    }

    private static GameObject Box(string name, Vector3 p, Vector3 size, Material mat, Transform parent = null, bool collision = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name; go.transform.SetParent(parent != null ? parent : root, false);
        go.transform.localPosition = p; go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (mat != null && mat.GetTexture("_BaseMap") != null) TempleSurfaceBuilder.ApplyBoxUV(go);
        if (!collision) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }
    private static void Floor(float from, float to)
    {
        Box("Foundation", new Vector3(0, -0.55f, (from + to) / 2), new Vector3(22, 1.1f, to - from), dark);
        // Decorative inlaid slabs have no colliders: one seamless collision floor avoids edge snagging.
        for (float z = from + 1; z < to; z += 2)
            for (int x = -9; x <= 9; x += 3)
                Box("LimestoneSlab", new Vector3(x, 0.006f, z), new Vector3(2.93f, 0.012f, Mathf.Min(1.93f, to - z + 0.9f)), tile, null, false);
    }
    private static void Pillar(float x, float z)
    {
        Box("PillarFoot", new Vector3(x, 0.3f, z), new Vector3(1.6f, 0.6f, 1.6f), dark);
        Box("Pillar", new Vector3(x, 5.5f, z), new Vector3(1, 10.4f, 1), stone);
        Box("Capital", new Vector3(x, 10.85f, z), new Vector3(1.7f, 0.55f, 1.7f), tile);
        Box("Moss", new Vector3(x, 0.015f, z + 1), new Vector3(1.5f, 0.03f, 0.4f), moss, null, false);
        Box("Inlay", new Vector3(x - Mathf.Sign(x) * 0.52f, 2.6f, z), new Vector3(0.04f, 1.6f, 0.18f), cyan, null, false);
    }
    private static void Gate(int index, float z)
    {
        Box("GatePierL", new Vector3(-7, 2.5f, z), new Vector3(8, 5, 1), stone);
        Box("GatePierR", new Vector3(7, 2.5f, z), new Vector3(8, 5, 1), stone);
        Box("GateLintel", new Vector3(0, 5.3f, z), new Vector3(7, 0.8f, 1.6f), tile);
        Box("UpperGateWall", new Vector3(0, 8.7f, z), new Vector3(22, 6, 1), stone);
        Mechanism(index, "StoneGate", new Vector3(0, 2.5f, z), new Vector3(6, 5, 0.7f), true, dark);
    }
    private static void Mechanism(int index, string name, Vector3 position, Vector3 size, bool gate, Material mat)
    {
        var group = new GameObject(name + "_" + index); group.transform.SetParent(root); group.transform.position = position;
        var visual = Box("MovingVisual", Vector3.zero, size, mat, group.transform, false);
        if (index == 7)
        {
            visual.GetComponent<Renderer>().enabled = false;
            for (int side = -1; side <= 1; side += 2)
                for (int edge = -1; edge <= 1; edge += 2)
                    Box("SealPillar", new Vector3(side * 0.44f, 0, edge * 0.44f), new Vector3(0.06f, 1, 0.06f), cyan, visual.transform, false);
            Box("SealCrown", new Vector3(0, 0.48f, 0), new Vector3(1, 0.07f, 1), gold, visual.transform, false);
        }
        if (index == 6)
        {
            visual.GetComponent<Renderer>().enabled = false;
            for (int i = -4; i <= 4; i++)
                Box("PortcullisBar", new Vector3(i * 0.11f, 0, 0), new Vector3(0.025f, 1, 1), dark, visual.transform, false);
            for (int i = -1; i <= 1; i++)
                Box("PortcullisCrossbar", new Vector3(0, i * 0.35f, 0), new Vector3(1, 0.035f, 1), gold, visual.transform, false);
        }
        var col = group.AddComponent<BoxCollider>(); col.size = size;
        var motion = group.AddComponent<TempleMechanism>(); motion.index = index; motion.visual = visual.transform;
        motion.solid = new Collider[] { col }; motion.gate = gate; motion.inactiveOffset = gate ? Vector3.up * 6.5f : Vector3.down * 4f;
        while (motions.Count <= index) motions.Add(null);
        motions[index] = motion;
    }
    private static void Bridge(int index, float x, float from, float to, float width, bool steps)
    {
        var group = new GameObject("RisingPath_" + index); group.transform.SetParent(root);
        var visual = new GameObject("MovingVisual"); visual.transform.SetParent(group.transform, false);
        var cols = new List<Collider>();
        for (float z = from; z < to; z += 1f)
        {
            // Each half crosses a 6m gap, landing on a fixed 4m-wide midpoint platform.
            if (steps && z >= 80 && z < 84) continue;
            float h = steps ? 0.12f + Mathf.Min((z - from) % 10, 5 - (z - from) % 10) * 0.10f : 0f;
            h = Mathf.Max(0, h);
            Vector3 p = new Vector3(x, -0.25f + h, z + 0.5f);
            Vector3 size = new Vector3(width, 0.5f, 1f);
            Box("RisingStone", p, size, index == 4 ? stone : tile, visual.transform, false);
            Box("PathInlay", p + Vector3.up * 0.255f, new Vector3(width - 0.3f, 0.012f, 0.09f), index == 4 ? gold : cyan, visual.transform, false);
            var collision = new GameObject("FixedSupport"); collision.transform.SetParent(group.transform, false); collision.transform.localPosition = p;
            var box = collision.AddComponent<BoxCollider>(); box.size = size; box.enabled = false; cols.Add(box);
        }
        var motion = group.AddComponent<TempleMechanism>(); motion.index = index; motion.visual = visual.transform; motion.solid = cols.ToArray();
        while (motions.Count <= index) motions.Add(null);
        motions[index] = motion;
    }
    private static void Plate(int index, float x, float z, bool weight = false)
    {
        var go = new GameObject("PressurePlate_" + index); go.transform.SetParent(root); go.transform.position = new Vector3(x, 0.03f, z);
        Box("FlushBase", Vector3.down * 0.015f, new Vector3(2.4f, 0.03f, 2.4f), dark, go.transform, false);
        var top = Box("DepressVisual", new Vector3(0, 0.045f, 0), new Vector3(2.05f, 0.08f, 2.05f), index == 5 || index == 7 ? gold : cyan, go.transform, false);
        var plate = go.AddComponent<TemplePressurePlate>(); plate.index = index; plate.visual = top.transform; plate.acceptsStone = weight;
        plates.Add(plate);
        // Unique geometric bars on the four final plates make them distinct without relying only on color.
        int bars = index >= 9 ? index - 8 : 1;
        for (int i = 0; i < bars; i++) Box("PlateMark", new Vector3((i - (bars - 1) * 0.5f) * 0.27f, 0.505f, 0),
            new Vector3(0.06f, 0.015f, 0.75f), dark, top.transform, false);
    }
    private static void Lever(int index, float x, float z)
    {
        var go = Box("LockLever_" + index, new Vector3(x, 0.65f, z), new Vector3(0.85f, 1.3f, 0.85f), dark);
        var handle = Box("Handle", new Vector3(0, 0.6f, 0), new Vector3(0.3f, 0.5f, 0.3f), gold, go.transform);
        var lever = go.AddComponent<TempleInteractable>(); lever.index = index; lever.handle = handle.transform;
        lever.SetDisplayName("통로 고정 레버"); levers.Add(lever);
    }
    private static void Stone(int index, float x, float z)
    {
        var go = Box("CarryStone_" + index, new Vector3(x, 0.45f, z), Vector3.one * 0.9f, stone);
        var interact = go.AddComponent<TempleInteractable>(); interact.index = index; interact.stone = true;
        var stoneComponent = go.AddComponent<TempleStone>(); stoneComponent.index = index; stoneComponent.home = go.transform.position; stones.Add(stoneComponent);
    }
    private static void AddInteriorLights()
    {
        Material lamp = BuilderMaterials.Ensure("Temple_Lamp", new Color(0.73f, 0.76f, 0.77f));
        lamp.EnableKeyword("_EMISSION"); lamp.SetColor("_EmissionColor", new Color(1.1f, 1.15f, 1.2f));
        for (int z = 9; z <= 141; z += 12)
        {
            int side = z % 24 < 12 ? -1 : 1;
            Box("SconceMount", new Vector3(side * 10.2f, 3.4f, z), new Vector3(0.4f, 0.8f, 0.65f), dark);
            Box("SconceLens", new Vector3(side * 9.95f, 3.6f, z), new Vector3(0.15f, 0.48f, 0.32f), lamp, null, false);
            var light = new GameObject("InteriorLight").AddComponent<Light>();
            light.transform.SetParent(root); light.transform.position = new Vector3(side * 7.8f, 5.5f, z);
            light.type = LightType.Point; light.range = 17f; light.intensity = 6f;
            light.color = new Color(0.9f, 0.94f, 1f); light.shadows = LightShadows.None;
        }
        for (int z = 10; z <= 140; z += 20)
        {
            var light = new GameObject("VaultFill").AddComponent<Light>(); light.transform.SetParent(root);
            light.transform.position = new Vector3(0, 9.8f, z); light.type = LightType.Point;
            light.range = 18f; light.intensity = 4.5f; light.color = new Color(0.94f, 0.97f, 1f);
        }
    }
    private static void Line(Vector3 from, Vector3 to, Material mat)
    {
        var go = Box("MechanismLink", (from + to) / 2, new Vector3(0.08f, 0.02f, Vector3.Distance(from, to)), mat, null, false);
        go.transform.rotation = Quaternion.LookRotation(to - from);
    }
    private static void Capture(Scene scene)
    {
        Directory.CreateDirectory("Logs/StoneTemple");
        var go = new GameObject("PreviewCamera"); var camera = go.AddComponent<Camera>();
        var originalLayers = new Dictionary<GameObject, int>();
        var otherLights = new List<Light>();
        foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (light.gameObject.scene != scene && light.enabled) { light.enabled = false; otherLights.Add(light); }
        foreach (var sceneRoot in scene.GetRootGameObjects())
            foreach (var t in sceneRoot.GetComponentsInChildren<Transform>(true))
            { originalLayers[t.gameObject] = t.gameObject.layer; t.gameObject.layer = 30; }
        camera.cullingMask = 1 << 30;
        camera.backgroundColor = RenderSettings.fogColor; camera.clearFlags = CameraClearFlags.SolidColor;
        camera.farClipPlane = 220;
        var texture = new RenderTexture(1600, 1000, 24); camera.targetTexture = texture;
        var old = RenderTexture.active;
        try
        {
            for (int i = 0; i < 3; i++)
            {
                go.transform.position = i == 0 ? new Vector3(6, 2.5f, 5) : i == 1 ? new Vector3(-5, 3, 68) : new Vector3(-4, 2.4f, 124);
                go.transform.LookAt(i == 0 ? new Vector3(0, 4.4f, 16) : i == 1 ? new Vector3(0, 3.6f, 91) : new Vector3(0, 2.4f, 130));
                camera.Render(); RenderTexture.active = texture;
                var png = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
                png.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); png.Apply();
                File.WriteAllBytes("Logs/StoneTemple/preview_" + i + ".png", png.EncodeToPNG()); Object.DestroyImmediate(png);
            }
        }
        finally
        {
            foreach (var pair in originalLayers) if (pair.Key != null) pair.Key.layer = pair.Value;
            foreach (var light in otherLights) if (light != null) light.enabled = true;
            RenderTexture.active = old; camera.targetTexture = null; Object.DestroyImmediate(texture); Object.DestroyImmediate(go);
        }
    }
}
#endif
