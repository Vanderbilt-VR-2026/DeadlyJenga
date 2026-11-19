using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using System.IO;
namespace DeadlyJenga.CastlePrototype.Editor
{
    [InitializeOnLoad]
    public static class CrossProcessBuild
    {
        const string LobbyHandoffKey = "DeadlyJenga.LobbyHandoffFixture";
        static CrossProcessBuild()
        {
            if (SessionState.GetBool(LobbyHandoffKey, false)) EditorApplication.update += PollLobbyHandoff;
        }
        public static void RunLobbyHandoffBatch()
        {
            string scene = AssetDatabase.FindAssets("Home t:Scene").Select(AssetDatabase.GUIDToAssetPath)
                .Single(path => Path.GetFileNameWithoutExtension(path) == "Home");
            SessionState.SetBool(LobbyHandoffKey, true);
            EditorApplication.update += PollLobbyHandoff;
            EditorSceneManager.OpenScene(scene);
            EditorApplication.EnterPlaymode();
        }
        static void PollLobbyHandoff()
        {
            if (!EditorApplication.isPlaying) return;
            var args = Environment.GetCommandLineArgs();
            var folder = args.Single(a => a.StartsWith("--dj-smoke-dir=")).Substring(15);
            var role = args.Single(a => a.StartsWith("--dj-smoke=")).Substring(11);
            string file = Path.Combine(folder, role + ".txt");
            if (!File.Exists(file)) return;
            var report = File.ReadAllLines(file);
            if (!report.Any(line => line.StartsWith("COMPLETE ") || line.StartsWith("FAILED "))) return;
            SessionState.SetBool(LobbyHandoffKey, false);
            EditorApplication.update -= PollLobbyHandoff;
            EditorApplication.Exit(report.Any(line => line.StartsWith("COMPLETE ")) ? 0 : 2);
        }

        public static void RunBatch()
        {
            bool poseProbe = Environment.GetCommandLineArgs().Contains("--dj-pose-probe");
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path);
            var composition = (poseProbe || Environment.GetCommandLineArgs().Contains("--dj-tycoon") ||
                Environment.GetCommandLineArgs().Contains("--dj-full-round")) ? UnityEngine.XR.OpenXR.OpenXRSettings
                .GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone)
                ?.GetFeatures<UnityEngine.XR.OpenXR.Features.OpenXRFeature>()
                .FirstOrDefault(feature => feature.GetType().Name == "OpenXRCompositionLayersFeature") : null;
            bool compositionWasEnabled = composition && composition.enabled;
            try
            {
                if (composition) composition.enabled = true;
                if (poseProbe) scenes = scenes.OrderBy(path => System.IO.Path.GetFileNameWithoutExtension(path) == "Lobby" ? 0 : 1);
                var build = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes.ToArray(), locationPathName = "Builds/IntegrationSweep/DeadlyJenga.exe",
                    target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development | (Environment.GetCommandLineArgs().Contains("--dj-scripts-only") ? BuildOptions.BuildScriptsOnly : BuildOptions.None)
                });
                if (build.summary.result != BuildResult.Succeeded)
                    throw new Exception("Independent player build failed: " + build.summary.result);
            }
            finally
            {
                if (composition) composition.enabled = compositionWasEnabled;
            }
            EditorApplication.Exit(0);
        }
    }
}
