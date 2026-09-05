#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class StoneTempleChecks
{
    private static int assertions;
    public static void BuildLocalPreviewBatch()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity", OpenSceneMode.Single);
            StoneTempleBuilder.Build();
            Run();
            File.WriteAllText("Logs/StoneTemple/checks.txt", "PASS " + assertions + " assertions\n" + DateTime.UtcNow.ToString("O"));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = EditorBuildSettings.scenes.Where(x => x.enabled).Select(x => x.path).ToArray(),
                locationPathName = "Builds/TemplePreview/StabInTheBack.exe",
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
            });
            Debug.Log("[StoneTemplePreview] " + report.summary.result);
            EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    public static void BuildReleaseBatch()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity", OpenSceneMode.Single);
            StoneTempleBuilder.Build();
            Run();
            File.WriteAllText("Logs/StoneTemple/checks.txt", "PASS " + assertions + " assertions\n" + DateTime.UtcNow.ToString("O"));
            ReleaseBuildValidator.BuildWindowsReleaseBatch();
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    public static void BuildAndCheckBatch()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity", OpenSceneMode.Single);
            StoneTempleBuilder.Build();
            Run();
            Debug.Log("[StoneTempleChecks] PASS " + assertions + " assertions");
            File.WriteAllText("Logs/StoneTemple/checks.txt", "PASS " + assertions + " assertions\n" + DateTime.UtcNow.ToString("O"));
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    private static void Assert(bool value, string message)
    {
        assertions++;
        if (!value) throw new Exception("[Temple regression] " + message);
    }
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene(StoneTempleBuilder.ScenePath, OpenSceneMode.Additive);
        try
        {
            var manager = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<StoneTempleManager>()).Single();
            Assert(manager.plates.Length == 13, "13 plate references");
            Assert(manager.mechanisms.Length == 10 && manager.mechanisms.All(x => x != null), "10 mechanism references");
            Assert(manager.levers.Length == 6 && manager.stones.Length == 2, "levers and stones");
            Assert(manager.chalice != null && manager.chalice.GetComponentsInChildren<MeshFilter>().All(x => x.sharedMesh != null), "chalice native meshes");
            Assert(scene.GetRootGameObjects().Count(x => x.GetComponent<NetworkStartPosition>() != null) == 4, "four spawn points");
            Assert(EditorBuildSettings.scenes.Length == 5 && EditorBuildSettings.scenes[2].path == StoneTempleBuilder.ScenePath, "legacy maps excluded, three stages included");
            foreach (var root in scene.GetRootGameObjects())
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    Assert(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0, "no missing scripts: " + transform.name);
            foreach (var renderer in scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Renderer>()))
                Assert(renderer.sharedMaterials.All(x => x != null && x.shader != null && x.shader.name != "Hidden/InternalErrorShader"), "valid material: " + renderer.name);
            Assert(Resources.Load<AudioClip>("Audio/Music/temple_mystic") != null, "temple music imported");
            Assert(Resources.Load<AudioClip>("Audio/Music/lobby_dungeon") != null, "lobby music imported");
            Assert(Resources.Load<AudioClip>("Audio/SFX/player_impact_01") != null, "hit sound imported");
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkPlayer.prefab");
            var body = playerPrefab.GetComponent<Rigidbody>();
            Assert(body == null || body.isKinematic && !body.useGravity, "CharacterController must be the only gravity owner");
            var writable = manager.Stones.IsWritable;
            var recording = manager.Stones.IsRecording;
            try
            {
                // Supply fixture data in an idle editor; this checks placement, not transport authority.
                manager.Stones.IsWritable = () => true;
                manager.Stones.IsRecording = () => false;
                manager.Stones.Clear();
                manager.Stones.Add(new TempleStoneState { position = manager.plates[3].transform.position + Vector3.up * 0.45f });
                Assert(manager.HasPlacedStone(3), "placed stone satisfies weight lock");
                manager.Stones[0] = new TempleStoneState { holder = 100, position = manager.plates[3].transform.position + Vector3.up * 0.45f };
                Assert(!manager.HasPlacedStone(3), "carried stone cannot satisfy weight lock");
                manager.Stones.Clear();
            }
            finally { manager.Stones.IsWritable = writable; manager.Stones.IsRecording = recording; }
            Occupancy(manager);
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }
    private static void Occupancy(StoneTempleManager manager)
    {
        if (NetworkServer.active || NetworkClient.active || NetworkServer.connections.Count != 0)
            throw new Exception("Run occupancy checks only in isolated idle editor");
        var players = new GameObject[4];
        try
        {
            for (int i = 0; i < 4; i++)
            {
                players[i] = new GameObject("TestPlayer_" + i);
                var identity = players[i].AddComponent<NetworkIdentity>();
                typeof(NetworkIdentity).GetProperty("netId").SetValue(identity, (uint)(100 + i));
                var character = players[i].AddComponent<CharacterController>(); character.height = 2; character.radius = 0.35f; character.center = Vector3.up;
                var connection = new NetworkConnectionToClient(100 + i);
                typeof(NetworkConnection).GetProperty("identity").SetValue(connection, identity);
                NetworkServer.connections.Add(connection.connectionId, connection);
                players[i].transform.position = new Vector3(0, 0, -20);
            }
            var plate = manager.plates[0];
            manager.ServerSamplePlayers(); Assert(manager.pressureMask == 0, "empty plates");
            players[0].transform.position = plate.transform.position;
            manager.ServerSamplePlayers(); Assert(manager.IsPressed(0), "replicated transform without physics step or enter callback");
            players[1].transform.position = plate.transform.position;
            players[0].transform.position = Vector3.back * 20;
            manager.ServerSamplePlayers(); Assert(manager.IsPressed(0), "one leaves, another remains");
            players[1].transform.position += Vector3.up * 0.5f;
            manager.ServerSamplePlayers(); Assert(!manager.IsPressed(0), "jumping above plate releases it");
            players[1].transform.position = plate.transform.position;
            players[1].GetComponent<CharacterController>().enabled = false;
            manager.ServerSamplePlayers(); Assert(!manager.IsPressed(0), "disabled character removed");
            players[1].GetComponent<CharacterController>().enabled = true;
            players[1].SetActive(false);
            manager.ServerSamplePlayers(); Assert(!manager.IsPressed(0), "inactive player removed");
            players[1].SetActive(true);
            var health = players[1].AddComponent<PlayerHealth>();
            typeof(PlayerHealth).GetField("life", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(health, new PlayerHealth.LifeState { dead = true });
            manager.ServerSamplePlayers(); Assert(!manager.IsPressed(0), "dead player removed even with enabled controller");
            typeof(PlayerHealth).GetField("life", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(health, new PlayerHealth.LifeState());
            var saved = NetworkServer.connections[101]; NetworkServer.connections.Remove(101);
            manager.ServerSamplePlayers(); Assert(!manager.IsPressed(0), "disconnect clears occupancy while object exists");
            NetworkServer.connections.Add(101, saved);
            plate.enabled = false; manager.ServerSamplePlayers(); Assert(!manager.IsPressed(0), "disabled plate removed"); plate.enabled = true;
            for (int i = 0; i < 4; i++) players[i].transform.position = manager.plates[9 + i].transform.position;
            manager.ServerSamplePlayers(); Assert(manager.rallyCount == 4, "four distinct final plates");
            players[3].transform.position = players[2].transform.position;
            manager.ServerSamplePlayers(); Assert(manager.rallyCount == 3, "four players on three plates cannot unlock");
            Vector3 old = manager.plates[10].transform.position;
            manager.plates[10].transform.position = manager.plates[9].transform.position;
            for (int i = 1; i < 4; i++) players[i].transform.position = Vector3.back * 20;
            manager.ServerSamplePlayers(); Assert(manager.rallyCount == 1, "overlapping areas count one player once");
            manager.plates[10].transform.position = old;
            Assert(!plate.ContainsFoot(new Vector3(float.NaN, 0, 0)), "invalid positions rejected");
            Assert(!plate.ContainsFoot(plate.transform.position - Vector3.up), "below floor rejected");
            plate.transform.rotation = Quaternion.Euler(0, 45, 0);
            Assert(plate.ContainsFoot(plate.transform.TransformPoint(new Vector3(0.9f, 0, 0.9f))), "rotated plate local coordinates");
            Assert(!plate.ContainsFoot(plate.transform.TransformPoint(new Vector3(1.5f, 0, 0))), "rotated plate outside rejected");
            Assert(plate.ContainsFoot(plate.transform.TransformPoint(new Vector3(1.1f, 0, 0)), 0.08f), "existing occupant edge hysteresis");
            Assert(!plate.ContainsFoot(plate.transform.TransformPoint(new Vector3(1.1f, 0, 0))), "new occupant must enter actual plate");
        }
        finally
        {
            NetworkServer.connections.Clear();
            foreach (var player in players) if (player != null) Object.DestroyImmediate(player);
        }
    }
}
#endif
