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
            if (Environment.GetCommandLineArgs().Contains("--dj-tycoon")) ValidateFingerPose();
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
            if (Environment.GetCommandLineArgs().Any(arg => arg.StartsWith("--dj-profile-input=", StringComparison.Ordinal)))
            {
                SummarizeCpuProfileBatch();
                return;
            }
            EditorApplication.Exit(0);
        }

        static void ValidateFingerPose()
        {
            var root = new UnityEngine.GameObject("Temporary finger pose equivalence");
            try
            {
                var rig = root.AddComponent<DeadlyJenga.CandyPlayers.CandyPlayerRig>();
                if (!rig.bodyRenderer) rig.BuildModel();
                var orientation = typeof(DeadlyJenga.CandyPlayers.CandyPlayerRig).GetMethod("Orientation",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                var lengths = (float[])typeof(DeadlyJenga.CandyPlayers.CandyPlayerRig).GetField("lengths",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(rig);
                foreach (float scale in new[] { .1f, .5f, 2f })
                foreach (float curl in new[] { 0f, .12f, .85f })
                for (int pass = 0; pass < 3; pass++)
                {
                    var frame = new DeadlyJenga.CandyPlayers.CandyAvatarFrame
                    {
                        feet = new UnityEngine.Vector3(pass, 2, -3), scale = scale,
                        head = new UnityEngine.Vector3(pass, 2 + 1.6f * scale, -3),
                        headRotation = UnityEngine.Quaternion.Euler(10, pass * 43, 0)
                    };
                    for (int side = 0; side < 2; side++)
                    {
                        var hand = new DeadlyJenga.CandyPlayers.CandyHandPose
                        {
                            tracked = true, skeletonTracked = true,
                            position = frame.feet + new UnityEngine.Vector3(side == 0 ? -.3f : .3f, 1.1f, .2f) * scale,
                            rotation = UnityEngine.Quaternion.Euler(pass * 21, pass * 47, side * 75),
                            skeleton = DeadlyJenga.CandyPlayers.CandyHandSkeleton.Rest(side == 0, curl)
                        };
                        if (side == 0) frame.left = hand; else frame.right = hand;
                    }
                    rig.ApplyFrame(frame, false, .016f);
                    for (int side = 0; side < 2; side++)
                    for (int finger = 0; finger < 5; finger++)
                    for (int joint = 0; joint < 3; joint++)
                    {
                        var hand = side == 0 ? frame.left : frame.right;
                        int index = 14 + side * 15 + finger * 3 + joint;
                        var start = hand.position + hand.rotation * (hand.skeleton.Joint(finger * 4 + joint) * scale);
                        var end = hand.position + hand.rotation * (hand.skeleton.Joint(finger * 4 + joint + 1) * scale);
                        var delta = end - start;
                        var expected = (UnityEngine.Quaternion)orientation.Invoke(null, new object[] { delta, hand.rotation * UnityEngine.Vector3.up });
                        var bone = rig.AppearanceBone(index);
                        float expectedLength = UnityEngine.Mathf.Max(.001f, delta.magnitude) / UnityEngine.Mathf.Max(.001f, lengths[index]);
                        if (UnityEngine.Vector3.Distance(bone.position, start) > .0001f ||
                            UnityEngine.Quaternion.Angle(bone.rotation, expected) > .15f ||
                            UnityEngine.Mathf.Abs(bone.lossyScale.y - expectedLength) > .0002f)
                            throw new InvalidOperationException($"Finger pose mismatch: scale={scale} curl={curl} pass={pass} bone={index}");
                    }
                }
                UnityEngine.Debug.Log("PASS 810 finger world poses match uncached transforms across scale, curl and palm motion");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
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
            var behaviours = new Dictionary<(string thread, string name), List<double>>();
            using var reconciliation = new StreamWriter(output + ".frames.csv", append: false);
            reconciliation.WriteLine("frame,thread,root_ms,exclusive_ms,residual_ms");
            for (int frameIndex = first; frameIndex <= ProfilerDriver.lastFrameIndex; frameIndex++)
            {
                bool validFrame = false;
                var selfTimes = new Dictionary<(string thread, string name), double>();
                var behaviourTimes = new Dictionary<(string thread, string name), double>();
                for (int thread = 0; ; thread++)
                {
                    using var frame = ProfilerDriver.GetRawFrameDataView(frameIndex, thread);
                    if (!frame.valid) break;
                    validFrame = true;
                    string threadName = frame.threadName + " #" + thread;
                    double rootTime = 0, exclusiveTime = 0;
                    int nextRoot = 0;
                    for (int sample = 0; sample < frame.sampleCount; sample++)
                    {
                        double inclusiveTime = frame.GetSampleTimeMs(sample);
                        double selfTime = inclusiveTime;
                        int end = sample + frame.GetSampleChildrenCountRecursive(sample) + 1;
                        if (sample == nextRoot) { rootTime += inclusiveTime; nextRoot = end; }
                        for (int child = sample + 1; child < end; child += 1 + frame.GetSampleChildrenCountRecursive(child))
                            selfTime -= frame.GetSampleTimeMs(child);
                        string name = frame.GetSampleName(sample) ?? "[Unnamed marker]";
                        var marker = (threadName, name);
                        exclusiveTime += selfTime;
                        if (selfTime > 0) AddTime(selfTimes, marker, selfTime);
                        if (inclusiveTime > 0 && name.Contains("Update", StringComparison.Ordinal) && name.Contains('('))
                            AddTime(behaviourTimes, marker, inclusiveTime);
                    }
                    reconciliation.WriteLine(FormattableString.Invariant($"{frameIndex},\"{threadName.Replace("\"", "\"\"")}\",{rootTime:F6},{exclusiveTime:F6},{rootTime-exclusiveTime:F6}"));
                }
                if (!validFrame) continue;
                captured++;
                Accumulate(markers, selfTimes);
                Accumulate(behaviours, behaviourTimes);
            }
            if (captured == 0) throw new InvalidOperationException("Profiler contains no captured main/render thread frames");

            double Median(List<double> values)
            {
                values.Sort();
                int index = (captured - 1) / 2 - (captured - values.Count);
                return index < 0 ? 0 : values[index];
            }
            using var report = new StreamWriter(output, append: false);
            report.WriteLine($"Frames {first}–{ProfilerDriver.lastFrameIndex}, {captured} captured. Times are CPU ms, including waits.");
            report.WriteLine("Exclusive samples reconcile to each thread's roots in .frames.csv. Threads overlap: never add their totals to estimate frame time. Inclusive Update times overlap exclusive samples. CPU waits do not establish GPU execution time.");
            WriteTimings("Exclusive marker times", markers);
            WriteTimings("Inclusive Update method times", behaviours);
            report.Flush();
            reconciliation.Flush();
            EditorApplication.Exit(0);

            static void AddTime(Dictionary<(string thread, string name), double> times, (string thread, string name) marker, double time)
            {
                times.TryGetValue(marker, out double previous);
                times[marker] = previous + time;
            }
            static void Accumulate(Dictionary<(string thread, string name), List<double>> series,
                Dictionary<(string thread, string name), double> times)
            {
                foreach (var (marker, time) in times)
                {
                    if (!series.TryGetValue(marker, out var samples)) series.Add(marker, samples = new List<double>());
                    samples.Add(time);
                }
            }
            void WriteTimings(string title, Dictionary<(string thread, string name), List<double>> series)
            {
                report.WriteLine(title);
                foreach (var thread in series.GroupBy(entry => entry.Key.thread))
                {
                    report.WriteLine(thread.Key);
                    foreach (var marker in thread.Select(entry => (entry.Key.name, median: Median(entry.Value),
                        mean: entry.Value.Sum() / captured, present: entry.Value.Count)).OrderByDescending(entry => entry.mean))
                        report.WriteLine(FormattableString.Invariant($"{marker.mean:F3} mean  {marker.median:F3} median  {marker.present}/{captured}  {marker.name}"));
                }
            }
        }
    }
}
