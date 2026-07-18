#if UNITY_EDITOR
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class NetworkPhase1Setup
{
    private const string PrefabFolder = "Assets/Prefabs";
    private const string PlayerPrefabPath = PrefabFolder + "/NetworkPlayer.prefab";

    [MenuItem("Tools/Multiplayer/Phase 1/Setup Player Spawning")]
    public static void SetupPlayerSpawning()
    {
        PlayerController scenePlayer = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (scenePlayer == null)
        {
            EditorUtility.DisplayDialog("Player not found", "No PlayerController exists in the active scene.", "OK");
            return;
        }

        GameObject player = scenePlayer.gameObject;
        ConfigurePlayerObject(player);

        if (!AssetDatabase.IsValidFolder(PrefabFolder))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
        ConfigureNetworkManager(prefab);
        EnsureSpawnPoints(player.transform.position);

        player.name = "SinglePlayerScenePlayer_DisabledForNetwork";
        player.SetActive(false);

        EditorSceneManager.MarkSceneDirty(player.scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Phase1] Network player prefab registered. The scene player was disabled for network spawning.");
    }

    private static void ConfigurePlayerObject(GameObject player)
    {
        if (player.GetComponent<NetworkIdentity>() == null)
            Undo.AddComponent<NetworkIdentity>(player);

        NetworkTransformUnreliable networkTransform = player.GetComponent<NetworkTransformUnreliable>();
        if (networkTransform == null)
            networkTransform = Undo.AddComponent<NetworkTransformUnreliable>(player);

        networkTransform.target = player.transform;
        networkTransform.syncDirection = SyncDirection.ClientToServer;
        networkTransform.coordinateSpace = CoordinateSpace.World;
        networkTransform.syncPosition = true;
        networkTransform.syncRotation = true;
        networkTransform.syncScale = false;
        networkTransform.syncInterval = 0.05f;
        networkTransform.onlySyncOnChange = false;

        if (player.GetComponent<NetworkPlayerSetup>() == null)
            Undo.AddComponent<NetworkPlayerSetup>(player);

        Camera camera = player.GetComponentInChildren<Camera>(true);
        if (camera != null && camera.GetComponent<AudioListener>() == null)
            Undo.AddComponent<AudioListener>(camera.gameObject);

        EditorUtility.SetDirty(player);
    }

    private static void ConfigureNetworkManager(GameObject playerPrefab)
    {
        NetworkManager manager = Object.FindFirstObjectByType<NetworkManager>(FindObjectsInactive.Include);
        if (manager == null)
        {
            EditorUtility.DisplayDialog("NetworkManager not found", "Run the Phase 0 bootstrap menu first.", "OK");
            return;
        }

        manager.playerPrefab = playerPrefab;
        manager.autoCreatePlayer = true;
        manager.playerSpawnMethod = PlayerSpawnMethod.RoundRobin;
        EditorUtility.SetDirty(manager);
    }

    private static void EnsureSpawnPoints(Vector3 basePosition)
    {
        Transform root = GameObject.Find("NetworkSpawnPoints")?.transform;
        if (root == null)
        {
            GameObject rootObject = new GameObject("NetworkSpawnPoints");
            Undo.RegisterCreatedObjectUndo(rootObject, "Create Network Spawn Points");
            root = rootObject.transform;
        }

        EnsureSpawnPoint(root, "Spawn_A", basePosition);
        EnsureSpawnPoint(root, "Spawn_B", basePosition + new Vector3(2f, 0f, 0f));
        EnsureSpawnPoint(root, "Spawn_C", basePosition + new Vector3(-2f, 0f, 0f));
        EnsureSpawnPoint(root, "Spawn_D", basePosition + new Vector3(0f, 0f, 2f));
    }

    private static void EnsureSpawnPoint(Transform root, string name, Vector3 position)
    {
        Transform child = root.Find(name);
        if (child == null)
        {
            GameObject spawn = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(spawn, "Create Network Spawn Point");
            child = spawn.transform;
            child.SetParent(root, false);
        }

        child.position = position;
        child.rotation = Quaternion.identity;
        if (child.GetComponent<NetworkStartPosition>() == null)
            Undo.AddComponent<NetworkStartPosition>(child.gameObject);
    }
}
#endif
