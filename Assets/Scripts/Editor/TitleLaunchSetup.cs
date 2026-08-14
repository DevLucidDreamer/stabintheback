#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 타이틀(MainTitle)에서 Host/Join을 누르면 게임 씬이 로드되는데,
/// 게임 씬에 NetworkManager + NetworkAutoLaunch(=NetworkBootstrap)가 없으면
/// StartHost/Client가 호출되지 않아 플레이어(카메라 포함)가 스폰되지 않는다.
/// 그 결과 "No cameras rendering"이 뜬다.
///
/// 이 메뉴는 현재 열려 있는 씬에 부트스트랩을 완전히 배선해준다:
///  - TelepathyTransport
///  - NetworkManager (playerPrefab = NetworkPlayer, autoCreatePlayer = true)
///  - NetworkManagerHUD (수동 제어용)
///  - NetworkAutoLaunch (타이틀에서 넘어온 GameLaunch.Mode를 읽어 자동 시작)
///
/// NetworkDemo 씬을 열고 실행하면 타이틀 → 게임 흐름이 정상 동작한다.
/// (Stage2 등 다른 게임 씬에서도 동일하게 실행하면 된다)
/// </summary>
public static class TitleLaunchSetup
{
    private const string BootstrapName = "NetworkBootstrap";
    private const string PlayerPrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";
    private const string TitleScenePath = "Assets/Scenes/MainTitle.unity";

    [MenuItem("Tools/Multiplayer/Fix Title Launch (Current Scene)")]
    public static void FixCurrentScene()
    {
        GameObject bootstrap = GameObject.Find(BootstrapName);
        if (bootstrap == null)
        {
            bootstrap = new GameObject(BootstrapName);
            Undo.RegisterCreatedObjectUndo(bootstrap, "Create Network Bootstrap");
        }

        // --- Transport (TelepathyTransport) ---
        Transport transport = bootstrap.GetComponent<Transport>();
        if (transport == null)
        {
            Type telepathyType = FindType("TelepathyTransport");
            if (telepathyType == null)
            {
                EditorUtility.DisplayDialog("Mirror 없음",
                    "TelepathyTransport 타입을 찾지 못했습니다. Mirror가 임포트되어 있는지 확인하세요.", "OK");
                return;
            }
            transport = (Transport)Undo.AddComponent(bootstrap, telepathyType);
        }

        // --- NetworkManager ---
        NetworkManager manager = bootstrap.GetComponent<NetworkManager>();
        if (manager == null)
            manager = Undo.AddComponent<NetworkManager>(bootstrap);

        manager.transport = transport;
        manager.autoCreatePlayer = true;
        manager.playerSpawnMethod = PlayerSpawnMethod.RoundRobin;

        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (playerPrefab != null)
            manager.playerPrefab = playerPrefab;
        else
            Debug.LogWarning("[TitleLaunch] NetworkPlayer 프리팹을 찾지 못함: " + PlayerPrefabPath);

        // --- HUD (수동 Host/Client 버튼, 있으면 편리) ---
        if (bootstrap.GetComponent<NetworkManagerHUD>() == null)
            Undo.AddComponent<NetworkManagerHUD>(bootstrap);

        // --- NetworkAutoLaunch (타이틀 실행 의도를 읽어 자동 시작) ---
        if (bootstrap.GetComponent<NetworkAutoLaunch>() == null)
            Undo.AddComponent<NetworkAutoLaunch>(bootstrap);

        // --- LanRoomAdvertiser (대기실에서 방을 알려 타이틀의 '빠른 참가'에 잡히게 한다) ---
        if (bootstrap.GetComponent<LanRoomAdvertiser>() == null)
            Undo.AddComponent<LanRoomAdvertiser>(bootstrap);

        EditorUtility.SetDirty(bootstrap);
        EditorSceneManager.MarkSceneDirty(bootstrap.scene);
        EditorSceneManager.SaveScene(bootstrap.scene);

        EnsureTitleInBuildSettings();

        Debug.Log($"[TitleLaunch] '{bootstrap.scene.name}' 씬에 NetworkBootstrap 배선 완료. " +
                  "이제 타이틀에서 Host/Join 하면 플레이어와 카메라가 스폰됩니다.");
    }

    private static void EnsureTitleInBuildSettings()
    {
        if (!File.Exists(TitleScenePath))
            return;

        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == TitleScenePath))
            return;

        // 타이틀을 첫 씬으로 넣어 빌드 시 시작 화면이 되도록 한다.
        scenes.Insert(0, new EditorBuildSettingsScene(TitleScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("[TitleLaunch] MainTitle 씬을 Build Settings 맨 앞에 추가했습니다.");
    }

    private static Type FindType(string typeName)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(SafeGetTypes)
            .FirstOrDefault(type => type.Name == typeName);
    }

    private static Type[] SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type != null).ToArray();
        }
    }
}
#endif
