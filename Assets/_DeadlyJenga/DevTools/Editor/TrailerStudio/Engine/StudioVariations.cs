using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Variation runs: simulate the same stretch of physics many times with small changes, score each
    // result, and let the person pick the great one. Every result is a normal world take; none is lost.
    public static class StudioVariations
    {
        [Serializable] public sealed class Settings
        {
            public int runs = 20;
            public int fromFrame;             // 0 = from a fresh map; otherwise branch from this tape frame
            public float forceJitter = .15f;  // each push or collapse is up to this much stronger or weaker
            public float timeJitter = .1f;    // each cue moves up to this many seconds
            public int seed = 1;
            public int targetBlock = -1;      // "did this block land near the spot?"
            public Vector3 targetPoint;       // metres from the stage spot
            public float zoneRadius = 1;
        }

        public sealed class Result
        {
            public int fallen;
            public float travelled, targetDistance = -1;
            public bool inZone;
        }

        const string Key = "DeadlyJenga.TrailerStudio.Variations";
        public static Settings Current
        {
            get { try { return JsonUtility.FromJson<Settings>(SessionState.GetString(Key, "")) ?? new Settings(); } catch { return new Settings(); } }
            set => SessionState.SetString(Key, JsonUtility.ToJson(value));
        }

        public static string RunningGroup { get; private set; } = "";
        public static int Done { get; private set; }
        public static int Total { get; private set; }
        public static bool Running => Total > 0;
        public static string LastGroup { get; private set; } = "";

        static Settings running;
        static StudioTake before;
        static readonly List<(StudioCue cue, float at)> moved = new List<(StudioCue, float)>();
        static bool hooked;

        // How a world take turned out: what fell, how far, and whether the chosen block reached the chosen spot.
        public static Result Score(StudioStage stage, StudioTake take)
        {
            var result = new Result();
            var settings = running ?? Current;
            if (!WorldTrack.Positions(take, take.start, out var first, out var firstOn) || !WorldTrack.Positions(take, take.End - 1, out var last, out var lastOn)) return result;
            for (int i = 0; i < first.Length; i++)
            {
                if (!firstOn[i]) continue;
                if (!lastOn[i]) { result.fallen++; continue; }
                float moved = Vector3.Distance(first[i], last[i]);
                result.travelled += moved;
                if (first[i].y - last[i].y > 1) result.fallen++;
            }
            int block = settings.targetBlock;
            if (block >= 0 && block < last.Length && lastOn[block])
            {
                result.targetDistance = Vector3.Distance(last[block], stage.ToWorld(settings.targetPoint));
                result.inZone = result.targetDistance <= settings.zoneRadius;
            }
            return result;
        }

        public static List<StudioTake> Results(StudioTape tape, string group) =>
            string.IsNullOrEmpty(group) || tape == null ? new List<StudioTake>()
                : (tape.FindType(WorldTrack.Id)?.Kept.Where(t => StudioBranch.NoteOf(t).group == group).ToList() ?? new List<StudioTake>());

        public static List<string> Groups(StudioTape tape) =>
            tape?.FindType(WorldTrack.Id)?.Kept.Select(t => StudioBranch.NoteOf(t).group).Where(g => !string.IsNullOrEmpty(g)).Distinct().ToList() ?? new List<string>();

        // Make a world take active together with the hazard, effect and sound takes recorded in the same run.
        public static void Activate(StudioTape tape, StudioTake world)
        {
            foreach (var track in tape.tracks)
            {
                if (track.type == SurvivorTrack.Id || track.type == HandTrack.Id) continue;
                int index = track.takes.FindIndex(t => t == world || t.created == world.created && !t.trashed);
                if (index >= 0) track.active = index;
            }
        }

        public static void Start(Settings settings)
        {
            if (StudioRun.State != StudioState.Held) throw new InvalidOperationException("Open the stage, and wait for any run to finish.");
            if (Running) throw new InvalidOperationException("A variation run is already going.");
            var stage = StudioRun.Stage;
            var world = stage.tape.FindType(WorldTrack.Id)?.ActiveTake;
            if (settings.fromFrame > 0 && (world == null || !world.Covers(settings.fromFrame)))
                throw new InvalidOperationException("To vary from a frame, record the world once first.");
            Current = settings;
            running = settings; before = world;
            Total = Mathf.Clamp(settings.runs, 1, 200); Done = 0;
            RunningGroup = LastGroup = "v" + DateTime.Now.ToString("HHmmss");
            if (!hooked) { StudioRun.Changed += Next; hooked = true; }
            Launch();
        }

        public static void Cancel()
        {
            if (!Running) return;
            Total = Done;   // the run in progress finishes and is kept; no more are started
            StudioRun.Stop();
        }

        static void Launch()
        {
            var stage = StudioRun.Stage;
            var random = new System.Random(running.seed * 7919 + Done);
            float Spread(float amount) => (float)(random.NextDouble() * 2 - 1) * amount;
            Restore();
            foreach (var cue in stage.set.cues)
            {
                if (cue.at * stage.Fps < running.fromFrame) continue;   // cues before the branch already happened on the tape
                cue.jitter = cue.action == PushCue.Id || cue.action == "collapse" ? Spread(running.forceJitter) : 0;
                moved.Add((cue, cue.at));
                if (string.IsNullOrEmpty(cue.link)) cue.at = Mathf.Clamp(cue.at + Spread(running.timeJitter), running.fromFrame / (float)stage.Fps, stage.set.length - .05f);
            }
            var pass = new StudioPass { label = $"Variation {Done + 1} of {Total}", liveWorld = true, fixedClock = true,
                branchFrom = running.fromFrame > 0 ? running.fromFrame : -1, seed = running.seed * 1000 + Done, note = $"Variation {Done + 1}" };
            foreach (var track in stage.tape.tracks)
                if (!track.locked && track.type != SurvivorTrack.Id && track.type != HandTrack.Id && StudioTrackTypes.Get(track.type) != null) pass.record.Add(track.id);
            StudioRun.Record(pass);
        }

        // Put cue times and strengths back the way the person set them.
        static void Restore()
        {
            foreach (var (cue, at) in moved) { cue.at = at; cue.jitter = 0; }
            moved.Clear();
        }

        static void Next()
        {
            if (!Running || StudioRun.State != StudioState.Held) return;
            // One editor tick later, so the finished run has fully settled.
            EditorApplication.delayCall += () =>
            {
                if (!Running || StudioRun.State != StudioState.Held) return;
                Done++;
                var stage = StudioRun.Stage;
                // Every run must start from the same world take, not from the previous variation.
                if (before != null && stage != null) Activate(stage.tape, before);
                if (Done < Total && stage != null)
                {
                    try { Launch(); return; }
                    catch (Exception error) { Debug.LogWarning("Trailer Studio: variation run stopped. " + error.Message); }
                }
                Restore();
                Total = 0; running = null; RunningGroup = "";
                if (stage != null) { StudioRun.SaveTape(); StudioRun.Refresh(); }
            };
        }
    }
}
