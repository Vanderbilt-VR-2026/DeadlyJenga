#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadlyJenga.CastlePrototype
{
    // Opt-in bounded profiling for one desktop match. Capture-free so readbacks stay out of timings.
    [DefaultExecutionOrder(10000)]
    public sealed class CrossProcessFrameEvidence : MonoBehaviour
    {
        const int Capacity = 4096;
        static readonly HashSet<string> Names = new HashSet<string>(StringComparer.Ordinal)
        {
            "Main Thread", "Render Thread", "GPU Frame Time", "CPU Main Thread Frame Time",
            "CPU Render Thread Frame Time", "Gfx.WaitForPresentOnGfxThread", "GC.Collect",
            "Physics.Simulate", "DJ.Network.WorldDecode", "DJ.Network.WorldApply",
            "DJ.Network.ActorState", "DJ.Network.ActorRender", "DJ.Network.ReplicaBlocks",
        };

        readonly List<(string name, ProfilerRecorder recorder)> recorders = new();
        string path;
        int count;
        int[] frames;
        double[] wall;
        long[] utc;
        int[] recorderCounts;
        long[,] values;
        double began, previous, delay, enteredGame = -1;
        bool finished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            var args = Environment.GetCommandLineArgs();
            if (!args.Contains("--dj-frame-profile") || args.Contains("--dj-motion-capture")) return;
            string folder = args.FirstOrDefault(value => value.StartsWith("--dj-smoke-dir=", StringComparison.Ordinal));
            string role = args.FirstOrDefault(value => value.StartsWith("--dj-smoke=", StringComparison.Ordinal));
            if (folder == null || role == null) return;
            const string delayPrefix = "--dj-frame-profile-delay=";
            string delayArg = args.FirstOrDefault(value => value.StartsWith(delayPrefix, StringComparison.Ordinal));
            var root = new GameObject("Explicit peer frame profile");
            DontDestroyOnLoad(root);
            var probe = root.AddComponent<CrossProcessFrameEvidence>();
            if (delayArg != null && int.TryParse(delayArg.Substring(delayPrefix.Length), out int seconds))
                probe.delay = Mathf.Clamp(seconds, 0, 600);
            probe.path = Path.Combine(Path.GetFullPath(folder.Substring("--dj-smoke-dir=".Length)),
                role.Substring("--dj-smoke=".Length) + "-frame-profile.csv");
        }

        void LateUpdate()
        {
            if (finished) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (frames == null)
            {
                if (SceneManager.GetActiveScene().name != "Game") { enteredGame = -1; return; }
                if (enteredGame < 0) enteredGame = now;
                if (now - enteredGame >= delay) Begin(now);
                return;
            }
            if (SceneManager.GetActiveScene().name != "Game" || now - began >= 15 || count == Capacity)
            {
                Finish();
                return;
            }
            frames[count] = Time.frameCount;
            utc[count] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            wall[count] = (now - previous) * 1000;
            previous = now;
            for (int i = 0; i < recorders.Count; i++)
            {
                var recorder = recorders[i].recorder;
                int received = recorder.Count;
                values[count, i] = received > recorderCounts[i] ? recorder.LastValue : -1;
                recorderCounts[i] = received;
            }
            count++;
        }

        void Begin(double now)
        {
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            foreach (var handle in handles)
            {
                var description = ProfilerRecorderHandle.GetDescription(handle);
                if (!Names.Contains(description.Name)) continue;
                var recorder = ProfilerRecorder.StartNew(description.Category, description.Name, Capacity,
                    ProfilerRecorderOptions.SumAllSamplesInFrame | ProfilerRecorderOptions.StartImmediately);
                if (recorder.Valid) recorders.Add((description.Name + " [" + description.UnitType + "]", recorder));
                else recorder.Dispose();
            }
            frames = new int[Capacity];
            wall = new double[Capacity];
            utc = new long[Capacity];
            recorderCounts = new int[recorders.Count];
            values = new long[Capacity, recorders.Count];
            began = previous = now;
        }

        void Finish()
        {
            finished = true;
            try
            {
                if (count == 0) return;
                using var output = new StreamWriter(path);
                output.Write("observer_frame,utc_ms,lateupdate_wall_ms");
                foreach (var row in recorders) output.Write("," + row.name);
                output.WriteLine();
                for (int row = 0; row < count; row++)
                {
                    output.Write(FormattableString.Invariant($"{frames[row]},{utc[row]},{wall[row]:F3}"));
                    for (int i = 0; i < recorders.Count; i++)
                    {
                        output.Write(',');
                        output.Write(values[row, i].ToString(CultureInfo.InvariantCulture));
                    }
                    output.WriteLine();
                }
            }
            finally
            {
                foreach (var entry in recorders) entry.recorder.Dispose();
            }
        }

        void OnDestroy()
        {
            if (!finished) Finish();
        }
    }
}
#endif
