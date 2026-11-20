using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // The shots of the cut (filled in from the plan after the scout) and the run itself.
    public static partial class DisasterPvpMoment
    {
        sealed class Shot
        {
            public string id, what;
            public int cutIn, cutOut, key;
            public int tapeShift;   // trailer frames added to the tape time (to reuse a moment), normally 0
            public int Length => cutOut - cutIn;
        }

        static readonly Shot[] Shots = { };

        static List<StudioCameraTrack> ShotCameras(StudioStage stage, Plan plan) => new List<StudioCameraTrack>();

        static IEnumerator<int> Shoot(string mode)
        {
            var stage = StudioRun.Stage;
            Note($"# Disaster PvP 1, {mode} run, version {Version}\n");
            var plan = MakePlan(stage);
            if (Recording)
            {
                Direct(stage, plan);
                yield return 0;
                var clock = Stopwatch.StartNew();
                StudioRun.RecordWholeSet();
                foreach (int wait in UntilHeld()) yield return wait;
                StudioRun.SaveTape();
                Note($"Recorded in {clock.Elapsed.TotalSeconds:0} s. {StudioRun.Status}");
                Note("Cue log:\n- " + string.Join("\n- ", StudioRun.CueLog));
                SaveBundle(stage.set);
            }
            else
            {
                CastFromSet(stage, plan);
                stage.set.overrides.Clear();
                AddLayers(stage, plan);
                StudioLibrary.Save(stage.set);
                StudioRun.Refresh();
                yield return 0;
            }
            try { Report(stage, plan); } catch (Exception error) { Note("Take report failed: " + error); }

            if (mode == "scout")
            {
                foreach (int wait in BlurCheck(stage, plan)) yield return wait;
                foreach (int wait in RenderScout(stage, plan)) yield return wait;
                yield break;
            }
            foreach (int wait in RenderShots(stage, plan, mode)) yield return wait;
        }

        static IEnumerable<int> RenderShots(StudioStage stage, Plan plan, string mode)
        {
            var cameras = ShotCameras(stage, plan);
            if (cameras.Count == 0) { Note("No shots planned yet."); yield break; }
            bool final = mode.StartsWith("final");
            if (mode == "finalA") cameras = cameras.Take(4).ToList();
            if (mode == "finalB") cameras = cameras.Skip(4).Take(4).ToList();
            if (mode == "finalC") cameras = cameras.Skip(8).ToList();
            var settings = final
                ? new StudioRenderSettings { width = 1920, height = 1080, fps = Fps, subFrames = 1, format = "png", cinema = false, filmstrip = 12 }
                : new StudioRenderSettings { width = 640, height = 360, fps = Fps, subFrames = 1, format = "png", cinema = false, filmstrip = 8 };
            string renders = Path.Combine(Folder, "renders");
            var clock = Stopwatch.StartNew();
            StudioRender.Start(cameras, settings, renders);
            foreach (int wait in UntilRendered()) yield return wait;
            Note($"Render {settings.width}x{settings.height}: {StudioRender.Progress} in {clock.Elapsed.TotalSeconds:0} s.");
            KeepShots(StudioRender.Results.ToList());
            string audio = StudioRender.AudioFile(stage.set.id);
            if (File.Exists(audio)) File.Copy(audio, Path.Combine(Folder, "tape.wav"), true);
            File.WriteAllText(Path.Combine(Folder, "shots.txt"), string.Join("\n", Shots.Select(s => $"{s.id} {s.cutIn} {s.cutOut} {s.key} {s.tapeShift} {Handle}")) + "\n");
            if (Directory.Exists(renders)) Directory.Delete(renders, true);
        }

        // Keep each shot's movie, metrics, filmstrip and its first, key and last stills; the frames are deleted.
        static void KeepShots(List<StudioRender.Finished> results)
        {
            string shots = Path.Combine(Folder, "shots");
            Directory.CreateDirectory(shots);
            foreach (var result in results)
            {
                var shot = Shots.FirstOrDefault(s => s.id == result.cameraId);
                if (shot == null) continue;
                Note($"{shot.id}: {result.frames} frames in {result.seconds:0} s {result.error}");
                if (!string.IsNullOrEmpty(result.movie) && File.Exists(result.movie)) File.Copy(result.movie, Path.Combine(shots, shot.id + ".mp4"), true);
                foreach (string name in new[] { "metrics.csv", "metrics.json", "filmstrip.png" })
                    if (File.Exists(Path.Combine(result.folder, name))) File.Copy(Path.Combine(result.folder, name), Path.Combine(shots, shot.id + "-" + name), true);
                void Still(string label, int frame)
                {
                    string file = Path.Combine(result.folder, $"{Mathf.Clamp(frame, 0, result.frames - 1):D05}.png");
                    if (File.Exists(file)) File.Copy(file, Path.Combine(shots, $"{shot.id}-{label}.png"), true);
                }
                Still("first", Handle); Still("key", Handle + shot.key); Still("last", Handle + shot.Length - 1);
            }
        }
    }
}
