#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FinalBuildPipeline
{
    public static void BuildBatch()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity", OpenSceneMode.Single);
            if (!Environment.GetCommandLineArgs().Contains("-skip-checks"))
            {
                StoneTempleBuilder.Build(); ExpeditionBuilder.Build();
                StoneTempleChecks.Run(); ExpeditionChecks.Run(); FinishingChecks.Run();
            }
            bool probe = Environment.GetCommandLineArgs().Contains("-network-probe");
            string folder = probe ? "Builds/FinalProbe" : "Builds/FinalWindows";
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = folder + "/StabInTheBack.exe", target = BuildTarget.StandaloneWindows64,
                options = probe ? BuildOptions.Development : BuildOptions.None
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Windows build failed");
            if (!probe)
            {
                File.Copy("README.md", folder + "/README.md", true);
                File.Copy("Assets/StreamingAssets/THIRD_PARTY_NOTICES.txt", folder + "/THIRD_PARTY_NOTICES.txt", true);
                File.WriteAllText(folder + "/BUILD_INFO.txt", "Stab in the Back\nWindows x64\nDevelopment Build: false\n" + DateTime.UtcNow.ToString("O"));
            }
            Debug.Log("[FinalBuild] SUCCESS " + folder); EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
#endif
