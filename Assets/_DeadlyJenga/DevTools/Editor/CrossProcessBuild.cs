using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using System.IO;
using UnityEngine;
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
            if (Environment.GetCommandLineArgs().Contains("--dj-tycoon-impact") && !Application.isBatchMode)
                ConfigureImpactGameView();
            string scene = AssetDatabase.FindAssets("Home t:Scene").Select(AssetDatabase.GUIDToAssetPath)
                .Single(path => Path.GetFileNameWithoutExtension(path) == "Home");
            SessionState.SetBool(LobbyHandoffKey, true);
            EditorApplication.update += PollLobbyHandoff;
            EditorSceneManager.OpenScene(scene);
            EditorApplication.EnterPlaymode();
        }
        static void ConfigureImpactGameView()
        {
            var assembly = typeof(UnityEditor.Editor).Assembly;
            var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var viewType = assembly.GetType("UnityEditor.GameView");
            var view = EditorWindow.GetWindow(viewType);
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes = singleton.GetProperty("instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).GetValue(null);
            var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
            var current = sizesType.GetProperty("currentGroupType", flags)?.GetValue(sizes) ?? Enum.Parse(groupType, "Standalone");
            var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { current });
            var sizeType = assembly.GetType("UnityEditor.GameViewSize");
            var kind = assembly.GetType("UnityEditor.GameViewSizeType");
            var size = Activator.CreateInstance(sizeType, new[] { Enum.Parse(kind, "FixedResolution"), (object)960, 540, "Impact performance 960x540" });
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            int index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) - 1;
            var selected = viewType.GetProperty("selectedSizeIndex", flags);
            SessionState.SetInt(LobbyHandoffKey + "GameViewSize", (int)selected.GetValue(view));
            SessionState.SetInt(LobbyHandoffKey + "GameViewAdded", index);
            SessionState.SetString(LobbyHandoffKey + "GameViewGroup", current.ToString());
            selected.SetValue(view, index);
            viewType.GetMethod("SizeSelectionCallback", flags)?.Invoke(view, new object[] { index, null });
            view.Show(); view.Focus(); view.Repaint();
            Debug.Log("Impact Editor GameView targetSize=" + viewType.GetProperty("targetSize", flags)?.GetValue(view));
        }
        static void RestoreImpactGameView()
        {
            string groupName = SessionState.GetString(LobbyHandoffKey + "GameViewGroup", "");
            if (groupName.Length == 0) return;
            var assembly = typeof(UnityEditor.Editor).Assembly;
            var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var viewType = assembly.GetType("UnityEditor.GameView");
            var view = Resources.FindObjectsOfTypeAll(viewType).FirstOrDefault();
            if (view) viewType.GetProperty("selectedSizeIndex", flags).SetValue(view, SessionState.GetInt(LobbyHandoffKey + "GameViewSize", 0));
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).GetValue(null);
            var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeGroupType"), groupName) });
            int builtins = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null);
            group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { SessionState.GetInt(LobbyHandoffKey + "GameViewAdded", builtins) - builtins });
            SessionState.EraseString(LobbyHandoffKey + "GameViewGroup");
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
            RestoreImpactGameView();
            EditorApplication.Exit(report.Any(line => line.StartsWith("COMPLETE ")) ? 0 : 2);
        }

        public static void RunBatch()
        {
            if (Environment.GetCommandLineArgs().Contains("--dj-tycoon")) { ValidateFingerPose(); ValidateDeathDeformation(); ValidateNativeWorldCodec(); }
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

        static void ValidateNativeWorldCodec()
        {
            var type=typeof(CastleNativeWorldChannel).Assembly.GetType("DeadlyJenga.CastlePrototype.WorldDeltaCodec",true);
            var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;
            var pack=type.GetMethod("PackNative",flags);var unpack=type.GetMethod("UnpackNative",flags);
            var random=new System.Random(71293);
            foreach(int length in new[]{1,1024,60000,2*1024*1024})
            foreach(bool noisy in new[]{false,true})
            {
                var raw=new byte[length];if(noisy)random.NextBytes(raw);
                var packet=(byte[])pack.Invoke(null,new object[]{raw});
                var restored=(byte[])unpack.Invoke(null,new object[]{packet});
                if(!raw.SequenceEqual(restored))throw new InvalidOperationException("Native world codec changed bytes");
            }
            foreach(var malformed in new[]{new byte[4],new byte[]{255,255,255,127,0},new byte[]{20,0,0,0,0}})
            {
                bool rejected=false;
                try{unpack.Invoke(null,new object[]{malformed});}
                catch(System.Reflection.TargetInvocationException error) when(error.InnerException is InvalidDataException){rejected=true;}
                if(!rejected)throw new InvalidOperationException("Native world codec accepted malformed input");
            }
            Debug.Log("PASS native world codec: eight lossless round trips and three malformed/oversized input rejections");
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

        static void ValidateDeathDeformation()
        {
            const int count = 769;
            var origin = new Vector3[count]; var normals = new Vector3[count];
            var weights = new BoneWeight[count]; var single = new int[count];
            var map = new[] { 2, 0, 3, 1 };
            for (int i = 0; i < count; i++)
            {
                origin[i] = new Vector3(Mathf.Sin(i * .17f), i * .013f, Mathf.Cos(i * .31f));
                normals[i] = new Vector3(.2f, Mathf.Sin(i), 1).normalized;
                weights[i] = new BoneWeight { boneIndex0 = 0, boneIndex1 = 1, boneIndex2 = 2, boneIndex3 = 3,
                    weight0 = .3f, weight1 = .2f, weight2 = .15f, weight3 = .05f };
                single[i] = i % 3 == 0 ? i % 4 : -1;
            }
            var type = typeof(DeadlyJenga.Presentation.CandyMelt).Assembly.GetType("DeadlyJenga.Presentation.VisualSkinDeformation", true);
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var worker = (IDisposable)Activator.CreateInstance(type, flags, null,
                new object[] { origin, normals, weights, map, single, 4 }, CultureInfo.InvariantCulture);
            var present = type.GetMethod("Present", flags);
            var mesh = new Mesh();
            try
            {
                for (int sample = 0; sample < 3; sample++)
                {
                    var poses = new Matrix4x4[4]; var normalPoses = new Matrix4x4[4];
                    for (int bone = 0; bone < 4; bone++)
                    {
                        var rotation = Quaternion.Euler(sample * 13 + bone * 17, bone * 23 - sample * 31, sample * 29);
                        poses[bone] = Matrix4x4.TRS(new Vector3(bone, sample - bone, sample * .3f), rotation,
                            new Vector3(.3f + sample, 1.1f + bone * .1f, .7f));
                        normalPoses[bone] = Matrix4x4.Rotate(rotation);
                    }
                    var bounds = (Bounds)present.Invoke(worker, new object[] { poses, normalPoses, mesh });
                    var actual = mesh.vertices; var actualNormals = mesh.normals;
                    Vector3 min = Vector3.one * float.PositiveInfinity, max = Vector3.one * float.NegativeInfinity;
                    for (int i = 0; i < count; i++)
                    {
                        Vector3 p = Vector3.zero, n = Vector3.zero;
                        if (single[i] >= 0)
                        { p = poses[single[i]].MultiplyPoint3x4(origin[i]); n = normalPoses[single[i]].MultiplyVector(normals[i]); }
                        else
                        {
                            var w = weights[i];
                            void Add(int index, float weight)
                            { p += poses[map[index]].MultiplyPoint3x4(origin[i]) * weight; n += normalPoses[map[index]].MultiplyVector(normals[i]) * weight; }
                            Add(w.boneIndex0, w.weight0); Add(w.boneIndex1, w.weight1);
                            Add(w.boneIndex2, w.weight2); Add(w.boneIndex3, w.weight3);
                            p /= w.weight0 + w.weight1 + w.weight2 + w.weight3;
                        }
                        if (Vector3.Distance(p, actual[i]) > .0002f || Vector3.Distance(n.normalized, actualNormals[i]) > .0002f)
                            throw new InvalidOperationException($"Parallel death deformation differs: sample {sample}, vertex {i}");
                        min = Vector3.Min(min, p); max = Vector3.Max(max, p);
                    }
                    if (Vector3.Distance(bounds.min, min) > .0003f || Vector3.Distance(bounds.max, max) > .0003f)
                        throw new InvalidOperationException("Parallel death deformation bounds differ");
                }
                UnityEngine.Debug.Log("PASS 4617 death deformation position/normal/bounds checks across four worker batches and three matrix sets");
            }
            finally { worker.Dispose(); UnityEngine.Object.DestroyImmediate(mesh); }
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
            var callPaths = new Dictionary<(string thread, string name), List<double>>();
            using var reconciliation = new StreamWriter(output + ".frames.csv", append: false);
            reconciliation.WriteLine("frame,thread,root_ms,exclusive_ms,residual_ms,cpu_frame_ms,gpu_frame_ms");
            for (int frameIndex = first; frameIndex <= ProfilerDriver.lastFrameIndex; frameIndex++)
            {
                bool validFrame = false;
                var selfTimes = new Dictionary<(string thread, string name), double>();
                var behaviourTimes = new Dictionary<(string thread, string name), double>();
                var pathTimes = new Dictionary<(string thread, string name), double>();
                for (int thread = 0; ; thread++)
                {
                    using var frame = ProfilerDriver.GetRawFrameDataView(frameIndex, thread);
                    if (!frame.valid) break;
                    validFrame = true;
                    string threadName = frame.threadName + " #" + thread;
                    double rootTime = 0, exclusiveTime = 0;
                    int nextRoot = 0;
                    var parents = new List<(string name,int end)>();
                    for (int sample = 0; sample < frame.sampleCount; sample++)
                    {
                        double inclusiveTime = frame.GetSampleTimeMs(sample);
                        double selfTime = inclusiveTime;
                        int end = sample + frame.GetSampleChildrenCountRecursive(sample) + 1;
                        if (sample == nextRoot) { rootTime += inclusiveTime; nextRoot = end; }
                        for (int child = sample + 1; child < end; child += 1 + frame.GetSampleChildrenCountRecursive(child))
                            selfTime -= frame.GetSampleTimeMs(child);
                        string name = frame.GetSampleName(sample) ?? "[Unnamed marker]";
                        while(parents.Count>0&&sample>=parents[^1].end)parents.RemoveAt(parents.Count-1);
                        if(name is "GetComponentNullErrorMessage" or "OnGUI" or "GC.Alloc" or "Physics.InvokeOnCollisionEvents")
                        {
                            string path=string.Join(" > ",parents.Skip(Math.Max(0,parents.Count-6)).Select(p=>p.name))+" > "+name;
                            AddTime(pathTimes,(threadName,path),selfTime);
                        }
                        if(end>sample+1)parents.Add((name,end));
                        var marker = (threadName, name);
                        exclusiveTime += selfTime;
                        if (selfTime > 0) AddTime(selfTimes, marker, selfTime);
                        if (inclusiveTime > 0 && name.Contains("Update", StringComparison.Ordinal) && name.Contains('('))
                            AddTime(behaviourTimes, marker, inclusiveTime);
                    }
                    reconciliation.WriteLine(FormattableString.Invariant($"{frameIndex},\"{threadName.Replace("\"", "\"\"")}\",{rootTime:F6},{exclusiveTime:F6},{rootTime-exclusiveTime:F6},{frame.frameTimeMs:F6},{frame.frameGpuTimeMs:F6}"));
                }
                if (!validFrame) continue;
                captured++;
                Accumulate(markers, selfTimes);
                Accumulate(behaviours, behaviourTimes);
                Accumulate(callPaths, pathTimes);
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
            WriteTimings("Selected expensive call paths (exclusive leaf time)", callPaths);
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
