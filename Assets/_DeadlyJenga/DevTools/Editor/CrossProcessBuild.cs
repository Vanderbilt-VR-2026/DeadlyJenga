using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
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

        public static void SummarizeCpuProfileBatch()
        {
            var args = Environment.GetCommandLineArgs();
            string Argument(string prefix) => args.Single(arg => arg.StartsWith(prefix, StringComparison.Ordinal))[prefix.Length..];
            string input = Argument("--dj-profile-input=");
            string output = Argument("--dj-profile-report=");
            int frames = int.Parse(Argument("--dj-profile-frames="), CultureInfo.InvariantCulture);
            if (frames < 1 || !ProfilerDriver.LoadProfile(input, false))
                throw new InvalidOperationException($"Could not load CPU profile {input}");

            int first = Math.Max(ProfilerDriver.firstFrameIndex, ProfilerDriver.lastFrameIndex - frames + 1);
            int captured = 0;
            var markers = new Dictionary<(string thread, string name), List<double>>();
            for (int frameIndex = first; frameIndex <= ProfilerDriver.lastFrameIndex; frameIndex++)
            {
                bool validFrame = false;
                var selfTimes = new Dictionary<(string thread, string name), double>();
                for (int thread = 0; ; thread++)
                {
                    using var frame = ProfilerDriver.GetRawFrameDataView(frameIndex, thread);
                    if (!frame.valid) break;
                    if (frame.threadName != "Main Thread" && frame.threadName != "Render Thread") continue;
                    validFrame = true;
                    for (int sample = 0; sample < frame.sampleCount; sample++)
                    {
                        double selfTime = frame.GetSampleTimeMs(sample);
                        int end = sample + frame.GetSampleChildrenCountRecursive(sample) + 1;
                        for (int child = sample + 1; child < end; child += 1 + frame.GetSampleChildrenCountRecursive(child))
                            selfTime -= frame.GetSampleTimeMs(child);
                        if (selfTime <= 0) continue;
                        var marker = (frame.threadName, frame.GetSampleName(sample));
                        selfTimes.TryGetValue(marker, out double previous);
                        selfTimes[marker] = previous + selfTime;
                    }
                }
                if (!validFrame) continue;
                captured++;
                foreach (var (marker, time) in selfTimes)
                {
                    if (!markers.TryGetValue(marker, out var samples)) markers.Add(marker, samples = new List<double>());
                    samples.Add(time);
                }
            }
            if (captured == 0) throw new InvalidOperationException("Profiler contains no captured main/render thread frames");

            double Median(List<double> values)
            {
                values.Sort();
                int index = (captured - 1) / 2 - (captured - values.Count);
                return index < 0 ? 0 : values[index];
            }
            using var report = new StreamWriter(output, append: false);
            report.WriteLine($"Frames {first}–{ProfilerDriver.lastFrameIndex}, {captured} captured. Times are exclusive CPU ms, including waits.");
            foreach (var thread in markers.GroupBy(entry => entry.Key.thread))
            {
                report.WriteLine(thread.Key);
                foreach (var marker in thread.Select(entry => (entry.Key.name, median: Median(entry.Value),
                    mean: entry.Value.Sum() / captured, present: entry.Value.Count)).OrderByDescending(entry => entry.mean).Take(35))
                    report.WriteLine(FormattableString.Invariant($"{marker.mean:F3} mean  {marker.median:F3} median  {marker.present}/{captured}  {marker.name}"));
            }
            report.Flush();
            EditorApplication.Exit(0);
        }
    }
}
