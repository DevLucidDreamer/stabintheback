#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 배포 직전에 자주 놓치는 설정과 에셋 참조를 한 번에 검사하고 Windows 빌드를 만든다.
/// CI에서는 -executeMethod ReleaseBuildValidator.BuildWindowsReleaseBatch 로 호출할 수 있다.
/// </summary>
public static class ReleaseBuildValidator
{
    private static readonly string[] ExpectedScenes =
    {
        "Assets/Scenes/MainTitle.unity",
        "Assets/Scenes/Lobby.unity",
        "Assets/Scenes/Stage3_CursedFortress.unity",
        "Assets/Scenes/Stage4_MagicSwordEscape.unity",
        "Assets/Scenes/Stage2_Campground.unity"
    };

    private const string PlayerPrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";
    private const string IconPath = "Assets/Sprite/GameIcon.png";
    private const string NoticesPath = "Assets/StreamingAssets/THIRD_PARTY_NOTICES.txt";
    private const string ProductName = "Stab in the Back";
    private const string BundleId = "com.luciddreamer.stabintheback";

    [MenuItem("Tools/Release/Validate Release (출시 점검)")]
    public static void ValidateReleaseMenu()
    {
        List<string> problems = CollectProblems();
        if (problems.Count == 0)
        {
            Debug.Log("[Release] 출시 점검 통과 — 빌드 설정, 씬, 프리팹 참조가 정상입니다.");
            EditorUtility.DisplayDialog("출시 점검", "자동 점검을 모두 통과했습니다.\n실기기 2대 멀티플레이 검증 후 배포하세요.", "확인");
            return;
        }

        string report = string.Join("\n", problems.Select(p => "• " + p));
        Debug.LogError("[Release] 출시 점검 실패\n" + report);
        EditorUtility.DisplayDialog("출시 점검 실패", report, "확인");
    }

    [MenuItem("Tools/Release/Build Windows x64 (출시 빌드)")]
    public static void BuildWindowsRelease()
    {
        BuildWindows(false);
    }

    public static void BuildWindowsReleaseBatch()
    {
        try
        {
            BuildWindows(true);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void BuildWindows(bool batchMode)
    {
        List<string> problems = CollectProblems();
        if (problems.Count > 0)
            throw new BuildFailedException("출시 점검 실패:\n" + string.Join("\n", problems));

        if (!batchMode && !EditorUtility.DisplayDialog(
                "Windows 출시 빌드",
                "Builds/Windows/StabInTheBack.exe를 새로 만듭니다.",
                "빌드", "취소"))
            return;

        const string outputDirectory = "Builds/Windows";
        Directory.CreateDirectory(outputDirectory);

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = ExpectedScenes,
            locationPathName = outputDirectory + "/StabInTheBack.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new BuildFailedException($"Windows 빌드 실패: {report.summary.result}, 오류 {report.summary.totalErrors}개");

        string archivePath = CreateReleaseArchive(outputDirectory);
        Debug.Log($"[Release] Windows 빌드 완료 — {options.locationPathName} ({report.summary.totalSize / 1048576f:F1} MB)\n" +
                  $"[Release] 배포 ZIP 완료 — {archivePath}");
        if (batchMode)
            EditorApplication.Exit(0);
        else
            EditorUtility.RevealInFinder(outputDirectory);
    }

    private static List<string> CollectProblems()
    {
        var problems = new List<string>();

        string[] enabledScenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
        if (!enabledScenes.SequenceEqual(ExpectedScenes))
            problems.Add("Build Settings 씬 순서가 MainTitle → Lobby → Stage3 → Stage4 → Stage2가 아닙니다.");

        foreach (string path in ExpectedScenes)
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                problems.Add("필수 씬이 없습니다: " + path);

        if (PlayerSettings.companyName != "Lucid Dreamer")
            problems.Add("Company Name이 Lucid Dreamer가 아닙니다.");
        if (PlayerSettings.productName != ProductName)
            problems.Add("Product Name이 Stab in the Back이 아닙니다.");
        if (!Version.TryParse(PlayerSettings.bundleVersion, out Version releaseVersion) || releaseVersion.Major < 1)
            problems.Add("출시 버전이 유효한 1.0.0 이상 형식이 아닙니다.");
        if (PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone) != BundleId)
            problems.Add("Standalone Application Identifier가 제품용 값이 아닙니다.");
        if (!HasSerializedCloudProjectId())
            problems.Add("Unity Cloud Project ID가 연결되지 않았습니다. Relay/Vivox 배포 환경을 확인하세요.");
        else if (string.IsNullOrWhiteSpace(CloudProjectSettings.projectId))
            problems.Add("Unity Services가 현재 프로젝트를 연결된 Cloud 프로젝트로 인식하지 못합니다. " +
                         "Unity Hub 로그인 후 Edit > Project Settings > Services에서 기존 프로젝트를 다시 연결하세요.");
        if (PlayerSettings.defaultScreenWidth < 1280 || PlayerSettings.defaultScreenHeight < 720)
            problems.Add("기본 해상도가 1280×720보다 작습니다.");
        if (!PlayerSettings.resizableWindow)
            problems.Add("창 크기 조절이 비활성화되어 있습니다.");
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath) == null)
            problems.Add("게임 아이콘이 없습니다: " + IconPath);
        if (!File.Exists(NoticesPath))
            problems.Add("배포에 포함할 서드파티 고지문이 없습니다: " + NoticesPath);

        string steamAppIdPath = Path.Combine(Directory.GetCurrentDirectory(), "steam_appid.txt");
        if (File.Exists(steamAppIdPath) && File.ReadAllText(steamAppIdPath).Trim() == "480")
            problems.Add("Steam 테스트 App ID 480이 남아 있습니다.");

        GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (player == null)
        {
            problems.Add("네트워크 플레이어 프리팹을 찾을 수 없습니다.");
        }
        else
        {
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(player) > 0)
                problems.Add("NetworkPlayer 프리팹에 Missing Script가 있습니다.");

            VivoxProximityVoice voice = player.GetComponent<VivoxProximityVoice>();
            SerializedObject voiceSettings = voice != null ? new SerializedObject(voice) : null;
            SerializedProperty pushToTalk = voiceSettings?.FindProperty("pushToTalk");
            if (pushToTalk == null || !pushToTalk.boolValue)
                problems.Add("음성이 Push-to-Talk 기본값으로 설정되지 않았습니다.");
        }

        foreach (string prefabGuid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                continue;

            foreach (Transform transform in prefab.GetComponentsInChildren<Transform>(true))
            {
                string context = path + " / " + GetHierarchyPath(transform);
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0)
                    problems.Add("프리팹에 Missing Script가 있습니다: " + context);
                ValidateMissingReferences(transform.gameObject, context, problems);
            }
        }

        ValidateSceneScripts(problems);
        return problems.Distinct().ToList();
    }

    private static void ValidateSceneScripts(List<string> problems)
    {
        foreach (string path in ExpectedScenes)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                continue;

            Scene scene = SceneManager.GetSceneByPath(path);
            bool openedHere = !scene.isLoaded;
            if (openedHere)
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

            try
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                    {
                        int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                        if (missing > 0)
                            problems.Add($"씬에 Missing Script가 있습니다: {path} / {GetHierarchyPath(transform)}");

                        ValidateMissingReferences(transform.gameObject, path + " / " + GetHierarchyPath(transform), problems);
                    }
                }
            }
            finally
            {
                if (openedHere)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static void ValidateMissingReferences(GameObject target, string context, List<string> problems)
    {
        foreach (Component component in target.GetComponents<Component>())
        {
            if (component == null)
                continue;

            var serialized = new SerializedObject(component);
            SerializedProperty property = serialized.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference ||
                    property.objectReferenceValue != null || property.objectReferenceInstanceIDValue == 0)
                    continue;

                problems.Add($"에셋 참조가 끊겼습니다: {context} / {component.GetType().Name}.{property.propertyPath}");
            }
        }
    }

    private static string CreateReleaseArchive(string outputDirectory)
    {
        string releaseDirectory = Path.Combine("Builds", "Releases");
        Directory.CreateDirectory(releaseDirectory);
        string archivePath = Path.Combine(releaseDirectory,
            $"StabInTheBack-Windows-x64-v{PlayerSettings.bundleVersion}.zip");

        if (File.Exists(archivePath))
            File.Delete(archivePath);

        string buildRoot = Path.GetFullPath(outputDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        using (FileStream stream = File.Create(archivePath))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            foreach (string file in Directory.EnumerateFiles(buildRoot, "*", SearchOption.AllDirectories))
            {
                string relativePath = file.Substring(buildRoot.Length).Replace('\\', '/');
                if (ShouldExcludeFromRelease(relativePath))
                    continue;

                AddFileToArchive(archive, file, relativePath);
            }

            AddFileToArchive(archive, NoticesPath, "THIRD_PARTY_NOTICES.txt");
        }

        return Path.GetFullPath(archivePath);
    }

    private static bool ShouldExcludeFromRelease(string relativePath)
    {
        return relativePath.IndexOf("BurstDebugInformation_DoNotShip", StringComparison.OrdinalIgnoreCase) >= 0 ||
               relativePath.IndexOf("BackUpThisFolder_ButDontShipItWithYourGame", StringComparison.OrdinalIgnoreCase) >= 0 ||
               relativePath.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase) ||
               relativePath.EndsWith(".mdb", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddFileToArchive(ZipArchive archive, string sourcePath, string entryPath)
    {
        ZipArchiveEntry entry = archive.CreateEntry(entryPath, System.IO.Compression.CompressionLevel.Optimal);
        using Stream input = File.OpenRead(sourcePath);
        using Stream output = entry.Open();
        input.CopyTo(output);
    }

    private static bool HasSerializedCloudProjectId()
    {
        const string projectSettingsPath = "ProjectSettings/ProjectSettings.asset";
        if (!File.Exists(projectSettingsPath))
            return false;

        string line = File.ReadLines(projectSettingsPath)
            .FirstOrDefault(value => value.TrimStart().StartsWith("cloudProjectId:", StringComparison.Ordinal));
        if (string.IsNullOrWhiteSpace(line))
            return false;

        string value = line.Substring(line.IndexOf(':') + 1).Trim();
        return Guid.TryParse(value, out _);
    }

    private static string GetHierarchyPath(Transform transform)
    {
        var names = new Stack<string>();
        for (Transform current = transform; current != null; current = current.parent)
            names.Push(current.name);
        return string.Join("/", names);
    }
}
#endif
