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

public static class ExpeditionChecks
{
    private static int assertions;
    public static void BuildBatch()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity", OpenSceneMode.Single);
            StoneTempleBuilder.Build(); ExpeditionBuilder.Build();
            StoneTempleChecks.Run(); Run(); FinishingChecks.Run();
            Directory.CreateDirectory("Logs/Expedition");
            File.WriteAllText("Logs/Expedition/checks.txt", "PASS " + assertions + " expedition assertions\n" + DateTime.UtcNow.ToString("O"));
            Debug.Log("[ExpeditionChecks] PASS " + assertions);
            if (Environment.GetCommandLineArgs().Contains("-expeditionPlayer"))
            {
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = EditorBuildSettings.scenes.Where(x => x.enabled).Select(x => x.path).ToArray(),
                    locationPathName = "Builds/ExpeditionPreview/StabInTheBack.exe", target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development
                });
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Player build failed");
            }
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    private static void Assert(bool value, string message)
    {
        assertions++; if (!value) throw new Exception("[ExpeditionChecks] " + message);
    }
    public static void Run()
    {
        if (NetworkServer.active || NetworkClient.active || NetworkServer.connections.Count != 0) throw new Exception("Isolated idle editor required");
        TestLevers(); TestStage(ExpeditionBuilder.BridgeScene); TestStage(ExpeditionBuilder.AltarScene);
        Assert(EditorBuildSettings.scenes.Length == 5, "continuous five-scene campaign");
    }
    private static NetworkConnectionToClient[] Players()
    {
        var players = new NetworkConnectionToClient[4];
        for (int i = 0; i < 4; i++)
        {
            var go = new GameObject("FixturePlayer_" + i); var identity = go.AddComponent<NetworkIdentity>();
            typeof(NetworkIdentity).GetProperty("netId").SetValue(identity, (uint)(200 + i));
            var cc = go.AddComponent<CharacterController>(); cc.height = 2; cc.radius = .35f; cc.center = Vector3.up;
            go.transform.position = new Vector3(0, 0, -25 - i * 3);
            players[i] = new NetworkConnectionToClient(200 + i);
            typeof(NetworkConnection).GetProperty("identity").SetValue(players[i], identity);
            NetworkServer.connections.Add(200 + i, players[i]);
        }
        return players;
    }
    private static void Cleanup(NetworkConnectionToClient[] players)
    {
        NetworkServer.connections.Clear(); foreach (var p in players) Object.DestroyImmediate(p.identity.gameObject);
    }
    private static void Move(NetworkConnectionToClient p, Vector3 point)
    {
        p.identity.transform.position = point; Physics.SyncTransforms();
    }
    private static void TestLevers()
    {
        var scene = EditorSceneManager.OpenScene(StoneTempleBuilder.ScenePath, OpenSceneMode.Single);
        var game = Object.FindFirstObjectByType<StoneTempleManager>(); var players = Players();
        try
        {
            game.phase = game.leverMask = 0;
            // All routes remain inactive: reaching the lever must still latch the completed crossing.
            for (int i = 0; i < 6; i++)
            {
                Move(players[0], game.levers[i].transform.position + Vector3.back * 1.4f - Vector3.up * .65f);
                Assert(game.ServerLatchLever(i, players[0]), "far-side lever works after pressure released: " + i);
                int phase = game.phase;
                Assert(!game.ServerLatchLever(i, players[0]) && phase == game.phase, "duplicate lever does not advance twice");
            }
            Assert(game.phase == 5, "six levers open route into chalice chamber");
            Assert(game.RespawnPoint(200).z == 121, "checkpoint is outside final room after final latch");
            game.phase = 0; game.leverMask = 0;
            Move(players[0], game.levers[0].transform.position + Vector3.back * 1.4f - Vector3.up * .65f);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = game.levers[0].transform.position + Vector3.back * .75f;
            wall.transform.localScale = new Vector3(2, 3, .2f); Physics.SyncTransforms();
            Assert(!game.ServerLatchLever(0, players[0]), "wall blocks lever request"); Object.DestroyImmediate(wall);
            Move(players[0], Vector3.zero); Assert(!game.ServerLatchLever(0, players[0]), "remote lever rejected");
        }
        finally { Cleanup(players); }
    }
    private static void TestStage(string path)
    {
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        var game = Object.FindFirstObjectByType<ExpeditionManager>(); var players = Players();
        try
        {
            Assert(game != null && game.cargo.Length == 4, "four cargo objects: " + path);
            Assert(Object.FindObjectsByType<NetworkStartPosition>(FindObjectsSortMode.None).Length == 4, "four spawns");
            foreach (var root in scene.GetRootGameObjects()) foreach (var t in root.GetComponentsInChildren<Transform>(true))
                Assert(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0, "no missing script " + t.name);
            foreach (var r in scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Renderer>(true)))
                Assert(r.sharedMaterials.All(m => m != null && m.shader != null), "valid material " + r.name);
            game.Cargo.IsWritable = () => true; game.Cargo.IsRecording = () => false;
            game.Hands.IsWritable = () => true; game.Hands.IsRecording = () => false;
            game.OnStartServer();
            Assert(!game.ServerUse(0, players[0]), "remote or empty-handed interaction rejected");
            Move(players[0], game.cargo[0].home + Vector3.back * 1.2f);
            Assert(game.ServerCargo(0, players[0]), "cargo can be picked up");
            Move(players[1], game.cargo[0].home + Vector3.right);
            Assert(!game.ServerCargo(0, players[1]), "two clients cannot own one cargo");
            var saved = NetworkServer.connections[200]; NetworkServer.connections.Remove(200);
            game.ServerTick(.05f);
            Assert(game.Cargo[0].holder == 0 && game.Cargo[0].position == game.cargo[0].home, "disconnect recovers cargo");
            NetworkServer.connections.Add(200, saved);
            if (game.stage == 2) TestBridge(game, players); else TestAltar(game, players);
        }
        finally { Cleanup(players); }
    }
    private static void TestBridge(ExpeditionManager game, NetworkConnectionToClient[] players)
    {
        for (int unit = 0; unit < 24; unit++)
        {
            int i = unit % 4; var player = players[i];
            // Sync the representation just as LateUpdate does after a deposit.
            game.cargo[i].transform.position = game.Cargo[i].position;
            Move(player, game.cargo[i].home + Vector3.back * 1.2f);
            Assert(game.ServerCargo(i, player), "quarry pickup " + unit);
            Move(player, game.BuildPosition + Vector3.back - Vector3.up * .85f);
            Assert(game.ServerUse(0, player), "deposit " + unit);
            Assert(!game.ServerUse(0, player), "duplicate deposit cannot create resources");
            Assert(game.BridgeSections == (unit + 1) / 3, "only three stones create a traversable span");
        }
        game.ServerTick(.05f);
        Assert(game.phase == 1 && game.BridgeSections == 8, "full bridge completed");
        Assert(!game.completed, "lantern must be claimed before descent");
        Assert(game.RespawnPoint(200).z == 49, "far-bank checkpoint");
        Assert(!game.ServerCargo(-1, players[0]), "invalid cargo rejected");
    }
    private static void TestAltar(ExpeditionManager game, NetworkConnectionToClient[] players)
    {
        // Put all four on wrong matching sockets first, then correct them by picking them back up.
        for (int i = 0; i < 4; i++) game.Cargo[i] = new ExpeditionCargoState { socket = i, position = game.sockets[i].position };
        Assert(!game.StatuesMatch(), "wrong statue arrangement stays locked");
        game.OnStartServer();
        for (int slot = 0; slot < 4; slot++)
        {
            int item = Array.FindIndex(game.cargo, x => x.kind == game.socketKinds[slot]);
            Move(players[slot], game.cargo[item].home + Vector3.back * 1.2f);
            Assert(game.ServerCargo(item, players[slot]), "statue pickup " + item);
            Move(players[slot], game.sockets[slot].position + Vector3.back * 1.5f);
            Assert(game.ServerUse(slot, players[slot]), "statue placed in socket " + slot);
        }
        Assert(game.StatuesMatch(), "four symbol matches");
        for (int i = 0; i < 15; i++) game.ServerTick(.05f);
        Assert(game.phase == 1, "statue door stays unlocked");
        int[] correct = { 0, 2, 5, 7 };
        for (int i = 0; i < 4; i++) Move(players[i], game.plates[correct[i]].transform.position);
        Move(players[3], game.plates[5].transform.position);
        for (int i = 0; i < 40; i++) game.ServerTick(.05f);
        Assert(game.phase == 1, "two people sharing a rune cannot replace missing fourth rune");
        Move(players[3], game.plates[7].transform.position + Vector3.up * .6f);
        game.ServerTick(.05f); Assert(!game.IsPressed(7), "jumping cannot hold rune");
        Move(players[3], game.plates[7].transform.position);
        for (int i = 0; i < 40; i++) game.ServerTick(.05f);
        Assert(game.phase == 2, "four distinct correct runes open altar chamber");
        for (int i = 0; i < 4; i++)
        {
            Move(players[i], game.nodes[4 + i].transform.position + Vector3.back);
            Assert(game.ServerUse(4 + i, players[i]), "ritual hand " + i);
        }
        Move(players[3], Vector3.zero); game.ServerTick(.05f);
        Assert(game.Hands[3] == 0 && game.ritualProgress == 0, "leaving altar clears ritual hand and charge");
        Move(players[3], game.nodes[7].transform.position + Vector3.back);
        Assert(game.ServerUse(7, players[3]), "ritual can resume");
        for (int i = 0; i < 45; i++) game.ServerTick(.05f);
        Assert(game.completed && game.phase == 3, "four-person ritual completes expedition");
    }
}
#endif
