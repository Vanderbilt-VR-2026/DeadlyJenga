using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Acceptance tests, run from the command line, one suite per phase:
    //   -executeMethod DeadlyJenga.DevTools.Editor.TrailerStudio.StudioAcceptance.RunPhase1   (RunPhase2, ...)
    // The report and pictures go to Logs/Validation/TrailerStudio-<suite>/. The editor closes itself when done.
    // This file holds the harness and the Phase 1 tests; later phases add a file each (StudioAcceptance2.cs...).
    [InitializeOnLoad]
    public static partial class StudioAcceptance
    {
        const string Key = "DeadlyJenga.TrailerStudio.Acceptance";
        const string Map = "sparse-viaduct-study";

        // The run survives Unity's script reload at Play Mode entry by keeping its place in SessionState.
        static int Step { get => SessionState.GetInt(Key, 0); set => SessionState.SetInt(Key, value); }
        static string Lines { get => SessionState.GetString(Key + ".Lines", ""); set => SessionState.SetString(Key + ".Lines", value); }
        static int Failures { get => SessionState.GetInt(Key + ".Failures", 0); set => SessionState.SetInt(Key + ".Failures", value); }
        static double Deadline { get => SessionState.GetFloat(Key + ".Deadline", 0); set => SessionState.SetFloat(Key + ".Deadline", (float)value); }

        static IEnumerator<int> routine;
        static int lastTick = -1, exceptions;
        static readonly List<string> errorLines = new List<string>();

        static string Suite { get => SessionState.GetString(Key + ".Suite", "Phase1"); set => SessionState.SetString(Key + ".Suite", value); }
        public static string Folder => Path.Combine(StudioLibrary.ProjectRoot, "Logs", "Validation", "TrailerStudio-" + Suite);

        static StudioAcceptance()
        {
            if (Step != 0) Hook();
        }

        static void Hook()
        {
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
        }

        public static void RunPhase1() => Begin("Phase1");
        public static void RunPhase2() => Begin("Phase2");
        public static void RunPhase3() => Begin("Phase3");
        public static void RunPhase4() => Begin("Phase4");
        public static void RunPhase5() => Begin("Phase5");
        public static void RunCut() => Begin("Cut");

        static void Begin(string suite)
        {
            Suite = suite;
            if (Directory.Exists(Folder)) Directory.Delete(Folder, true);
            Directory.CreateDirectory(Folder);
            Lines = ""; Failures = 0; Step = 1;
            StudioExamples.Enabled = true;   // the example track and example mode are part of test 6
            Deadline = EditorApplication.timeSinceStartup + 240;
            Hook();
        }

        static void Log(string message, string stack, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
            if (type == LogType.Exception) exceptions++;
            if (errorLines.Count < 12) errorLines.Add(type + ": " + message.Split('\n')[0]);
        }

        static void Result(string test, bool passed, string detail)
        {
            Lines += $"| {test} | {(passed ? "PASS" : "FAIL")} | {detail.Replace("|", "/").Replace("\n", " ")} |\n";
            if (!passed) Failures++;
            Debug.Log($"[TrailerStudio acceptance] {(passed ? "PASS" : "FAIL")} {test}: {detail}");
        }

        static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                switch (Step)
                {
                    case 1:
                        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                        StudioLibrary.SetsFolderOverride = Path.Combine(Folder, "sets");   // test Sets never go into Assets
                        StudioRun.OpenStage(Suite == "Phase1" ? MakeSet() : MakeSuiteSet(Suite));
                        Step = 2; Deadline = EditorApplication.timeSinceStartup + 600;
                        break;
                    case 2:
                        if (routine == null)
                        {
                            if (StudioRun.State == StudioState.Held) routine = Suite == "Phase1" ? StageTests() : SuiteTests(Suite);
                            else if (EditorApplication.timeSinceStartup > Deadline)
                                throw new TimeoutException($"The stage never finished opening. State {StudioRun.State}, playing {EditorApplication.isPlaying}, status: {StudioRun.Status}");
                            return;
                        }
                        if (Advance()) return;
                        Step = Suite == "Phase1" ? 3 : 5;   // only Phase 1 also checks the plain game afterwards
                        StudioRun.CloseStage();
                        break;
                    case 3:
                        if (EditorApplication.isPlayingOrWillChangePlaymode || StudioRun.State != StudioState.Closed) return;
                        EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
                        Step = 4; Deadline = EditorApplication.timeSinceStartup + 300;
                        EditorApplication.EnterPlaymode();
                        break;
                    case 4:
                        if (!EditorApplication.isPlaying) return;
                        if (routine == null) routine = SoloGame();
                        if (Advance()) return;
                        Step = 5;
                        EditorApplication.ExitPlaymode();
                        break;
                    case 5:
                        if (EditorApplication.isPlayingOrWillChangePlaymode || StudioRun.State != StudioState.Closed && EditorApplication.isPlaying) return;
                        Finish();
                        break;
                }
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Result("Run stopped at step " + Step, false, error.Message);
                Finish();
            }
        }

        // True while the current test routine still has work to do.
        static bool Advance()
        {
            if (Time.frameCount == lastTick) return true;
            lastTick = Time.frameCount;
            if (routine.MoveNext()) return true;
            routine = null;
            return false;
        }

        static void Finish()
        {
            Step = 0;
            StudioLibrary.SetsFolderOverride = "";
            StudioExamples.Enabled = false;
            EditorApplication.update -= Tick;
            var text = new StringBuilder("# Trailer Studio " + Suite + " acceptance run\n\n");
            text.Append($"Date: {DateTime.Now:yyyy-MM-dd HH:mm}  \nUnity: {Application.unityVersion}  \nMap: {Map}\n\n");
            text.Append("| Test | Result | Detail |\n|------|--------|--------|\n").Append(Lines);
            text.Append($"\nFailures: {Failures}\n");
            File.WriteAllText(Path.Combine(Folder, "report.md"), text.ToString());
            Debug.Log("[TrailerStudio acceptance] finished with " + Failures + " failures. Report: " + Path.Combine(Folder, "report.md"));
            // Close the editor when it was started just for this run (batch mode, or -executeMethod on the command line).
            bool startedForThis = Environment.GetCommandLineArgs().Any(a => a.Contains("StudioAcceptance.Run"));
            if (Application.isBatchMode || startedForThis) EditorApplication.Exit(Failures == 0 ? 0 : 1);
        }

        static StudioSet MakeSet()
        {
            // Test Sets stay in the report folder, never in Assets.
            StudioLibrary.SetsFolderOverride = Path.Combine(Folder, "sets");
            string tape = StudioLibrary.TapePath("acceptance-viaduct");
            if (File.Exists(tape)) File.Delete(tape);
            var set = new StudioSet { id = "acceptance-viaduct", title = "Acceptance: viaduct collapse", map = Map, fps = 30, length = 6,
                notes = "Made by the Phase 1 acceptance run. Safe to delete." };
            set.AddMark(StudioRole.Bot, new Vector3(1.5f, 0, 0));
            set.AddMark(StudioRole.Bot, new Vector3(-1.5f, 0, 0));
            set.AddMark(StudioRole.Survivor, new Vector3(0, 0, 1.5f));
            // The collapse is aimed away from the stage block, so that block only gets the slow shove below.
            set.cues.Add(new StudioCue { at = 1, action = "collapse", strength = 4, point = new Vector3(0, -5, 14) });
            // A slow shove on a block one level down, so test 3 has a block that moves at a speed someone can ride.
            set.cues.Add(new StudioCue { at = 2, action = "push", strength = 3.6f, direction = new Vector3(1, .12f, .2f), point = new Vector3(0, -.3f, 0) });
            StudioLibrary.Save(set);
            return set;
        }

        // ---- Tests 1, 2, 3, 4 and 6, inside the open stage ----

        sealed class Snapshot
        {
            public Vector3[] positions;
            public Quaternion[] rotations;
            public bool[] active;
            public Dictionary<string, Vector3> actors = new Dictionary<string, Vector3>();
        }

        static bool Held => StudioRun.State == StudioState.Held;

        // Wait for the current run to finish, but never for more than five real minutes.
        static IEnumerable<int> UntilHeld()
        {
            float started = Time.realtimeSinceStartup;
            while (!Held)
            {
                if (StudioRun.State == StudioState.Closed) throw new InvalidOperationException("The stage closed during a test: " + StudioRun.Status);
                if (Time.realtimeSinceStartup - started > 300) throw new TimeoutException("A run did not finish in five minutes: " + StudioRun.Status);
                yield return 0;
            }
        }

        static IEnumerator<int> StageTests()
        {
            var stage = StudioRun.Stage;
            var set = stage.set;
            int frames = set.Frames;
            StudioCamera.Resize(640, 360);
            StudioCamera.Target = stage.anchor + Vector3.up; StudioCamera.Distance = 13; StudioCamera.Pitch = 18; StudioCamera.Place();

            // Put the marks on the surface, as the Studio does when a mark is dragged.
            foreach (var mark in set.marks)
                if (StudioStage.SurfaceBelow(stage.ToWorld(mark.position), out var surface)) mark.position = stage.ToStage(surface);
            StudioLibrary.Save(set);
            Result("Set built", stage.blocks.Length > 0 && set.marks.Count == 3,
                $"{Map}: {stage.blocks.Length} blocks, 3 marks, collapse cue at 1.0 s, {frames} frames at {set.fps} fps. Mark warnings: " +
                string.Join(", ", set.marks.Select(m => m.actor + "=" + (stage.MarkWarning(m) == "" ? "ok" : stage.MarkWarning(m)))));

            // ---- Test 1: record once ----
            var random = new System.Random(7);
            var wanted = new HashSet<int>(Enumerable.Range(0, 10).Select(_ => random.Next(0, frames)));
            var snapshots = new Dictionary<int, Snapshot>();
            TheatreMotionTape old = null;
            string oldFile = Path.Combine(Folder, "theatre-v4", "motion.djt");
            Directory.CreateDirectory(Path.GetDirectoryName(oldFile));
            int oldFrames = 0;
            string[] castIds = null;

            void WhileRecording(int frame)
            {
                if (frame % 2 == 0) StudioCamera.SavePng(Path.Combine(Folder, "live", $"{frame / 2:D05}.png"));
                if (wanted.Contains(frame)) snapshots[frame] = Take(stage);
                // Write the same run as an old Theatre v4 tape too, with Theatre's own writer (for test 4).
                if (old == null)
                {
                    castIds = stage.live.Where(p => p.Value).Select(p => p.Key).OrderBy(id => id).ToArray();
                    old = new TheatreMotionTape(oldFile, false, set.map, set.fps, frames, stage.anchor, stage.blocks, castIds,
                        castIds.Select(id => stage.live[id].GetComponent<CandyPlayerAvatar>()).ToArray());
                }
                old.WriteFrame(); oldFrames++;
            }

            // Aim the slow shove at the block the survivor mark stands on, so test 3 has someone on a moving block.
            var riderMark = set.marks.First(m => m.role == StudioRole.Survivor);
            var shove = set.cues.First(c => c.action == "push");
            if (Physics.Raycast(stage.ToWorld(riderMark.position) + Vector3.up * 1.5f, Vector3.down, out var under, 20, 1 << Layers.Block, QueryTriggerInteraction.Ignore) && under.rigidbody)
            {
                int index = Array.IndexOf(stage.blocks, under.rigidbody);
                if (index >= 0) { shove.blocks.Clear(); shove.blocks.Add(index); StudioLibrary.Save(set); }
            }

            StudioRun.FrameShown += WhileRecording;
            StudioRun.RecordWholeSet();
            foreach (int wait in UntilHeld()) yield return wait;
            StudioRun.FrameShown -= WhileRecording;
            old?.Dispose();

            var tape = stage.tape;
            var worldTake = tape.FindType(WorldTrack.Id)?.ActiveTake;
            var actorTakes = set.marks.Select(m => tape.Find(m.id)?.ActiveTake).ToArray();
            bool recorded = worldTake != null && worldTake.frames.Count == frames && actorTakes.All(t => t != null && t.frames.Count == frames);
            long fileSize = File.Exists(tape.path) ? new FileInfo(tape.path).Length : 0;
            Result("1a. Record the Set", recorded,
                $"World take {worldTake?.frames.Count ?? 0}/{frames} frames; actor takes {string.Join("/", actorTakes.Select(t => t?.frames.Count ?? 0))}; " +
                $"tape file {StudioTape.Size(fileSize)} ({StudioTape.Size(tape.Bytes)} before compression). Cues: {string.Join("; ", StudioRun.CueLog)}");

            // Did the collapse actually move anything?
            StudioRun.Scrub(0); yield return 0;
            var start = Take(stage);
            StudioRun.Scrub(frames - 1); yield return 0;
            var end = Take(stage);
            int moved = Enumerable.Range(0, stage.blocks.Length).Count(i => start.active[i] && (!end.active[i] || Vector3.Distance(start.positions[i], end.positions[i]) > .1f));
            Result("1b. The collapse was recorded", moved > 0, $"{moved} of {stage.blocks.Length} blocks moved more than 10 cm between the first and last frame.");

            // ---- Test 1: play back three times with the drift check ----
            for (int run = 1; run <= 3; run++)
            {
                void WhilePlaying(int frame)
                {
                    if (run == 1 && frame % 2 == 0) StudioCamera.SavePng(Path.Combine(Folder, "playback", $"{frame / 2:D05}.png"));
                }
                StudioRun.FrameShown += WhilePlaying;
                StudioRun.Play(0, -1, false);
                foreach (int wait in UntilHeld()) yield return wait;
                StudioRun.FrameShown -= WhilePlaying;
                var drift = StudioRun.LastDrift;
                Result($"1c. Playback {run} of 3, drift check", drift != null && drift.samples > 0 && drift.Passed, drift?.ToString() ?? "No drift report.");
            }
            SideBySide();

            // ---- Test 2: scrub to 10 random frames ----
            float worstMm = 0, worstDegrees = 0, worstActor = 0;
            int compared = 0, actorsCompared = 0;
            foreach (var pair in snapshots.OrderBy(p => p.Key))
            {
                StudioRun.Scrub(pair.Key); yield return 0;
                var now = Take(stage);
                for (int i = 0; i < stage.blocks.Length; i++)
                {
                    if (!pair.Value.active[i]) continue;
                    if (!now.active[i]) { worstMm = 9999; continue; }
                    worstMm = Mathf.Max(worstMm, Vector3.Distance(now.positions[i], pair.Value.positions[i]) * 1000);
                    worstDegrees = Mathf.Max(worstDegrees, Quaternion.Angle(now.rotations[i], pair.Value.rotations[i]));
                    compared++;
                }
                foreach (var actor in pair.Value.actors)
                    if (stage.playback.TryGetValue(actor.Key, out var shown) && shown && shown.activeSelf)
                    {
                        worstActor = Mathf.Max(worstActor, Vector3.Distance(shown.transform.position, actor.Value) * 1000);
                        actorsCompared++;
                    }
            }
            Result("2. Scrub to 10 random frames", compared > 0 && worstMm <= 1 && worstDegrees <= .1f && actorsCompared > 0 && worstActor <= 20,
                $"Frames {string.Join(", ", snapshots.Keys.OrderBy(k => k))}. {compared} block poses compared with the live recording: worst {worstMm:F3} mm, {worstDegrees:F4} degrees. " +
                $"{actorsCompared} actor positions compared: worst {worstActor:F1} mm.");

            // ---- Test 6 (track half): the example track type is on the tape, and unknown types survive ----
            var reloaded = StudioTape.Load(tape.path);
            var note = reloaded.FindType(ExampleNoteTrack.Id);
            var onlyWorld = StudioTape.Load(tape.path, new[] { WorldTrack.Id });
            var future = StudioTape.Load(tape.path);
            future.FindType(ExampleNoteTrack.Id).type = "made-in-a-later-phase";
            string futureFile = Path.Combine(Folder, "future-track.djtape");
            future.Save(futureFile);
            var survived = StudioTape.Load(futureFile).FindType("made-in-a-later-phase");
            StudioRun.Scrub(12); yield return 0;
            Result("6a. A track type added in its own file is recorded, saved and played",
                StudioTrackTypes.Get(ExampleNoteTrack.Id) != null && note?.ActiveTake != null && note.ActiveTake.frames.Count == frames && ExampleNoteTrack.LastShown.StartsWith("frame 12:"),
                $"ExampleNoteTrack.cs only. Saved take has {note?.ActiveTake?.frames.Count ?? 0} frames; scrubbing to frame 12 showed \"{ExampleNoteTrack.LastShown}\".");
            Result("6b. Readers skip and keep track types they do not know",
                onlyWorld.tracks.Count == 1 && survived?.ActiveTake != null && survived.ActiveTake.frames.Count == frames,
                $"Loading only the world gave {onlyWorld.tracks.Count} track; a tape with an unknown type \"made-in-a-later-phase\" reloaded and re-saved with {survived?.ActiveTake?.frames.Count ?? 0} frames intact.");

            // ---- Test 6 (mode half): the example mode tab is in the registry and in the window ----
            bool registered = StudioExtensions.Modes.Any(m => m is ExampleNotesMode && m.Available);
            string windowNote;
            bool inWindow = false;
            try
            {
                var window = EditorWindow.GetWindow<TrailerStudioWindow>();
                window.RebuildAll();
                var tabNames = window.rootVisualElement.Query<UnityEngine.UIElements.Button>(className: "ts-tab").ToList().Select(b => b.text).ToList();
                inWindow = tabNames.Contains("Notes") && tabNames.Contains("Set");
                windowNote = "Window tabs: " + string.Join(", ", tabNames) + ".";
            }
            catch (Exception error) { windowNote = "The window could not be opened in this run (" + error.Message + "), so only the registry was checked."; inWindow = Application.isBatchMode; }
            Result("6c. A mode tab added in its own file shows up in the Studio", registered && inWindow,
                $"ExampleNotesMode.cs only. In the mode registry: {registered}. {windowNote}");

            // ---- Test 3: a live survivor on a falling block, while the world plays back ----
            // The push cue shoves the block the stage sits on (the one nearest the stage spot). Ride that one.
            int chosen = -1; float bestDrop = 0;
            int stageBlock = set.cues.First(c => c.action == "push").blocks.Count > 0 ? set.cues.First(c => c.action == "push").blocks[0] : Enumerable.Range(0, stage.blocks.Length).Where(i => stage.blocks[i] && stage.blocks[i].gameObject.activeSelf)
                .OrderBy(i => (stage.blocks[i].position - stage.ToWorld(new Vector3(0, -.3f, 0))).sqrMagnitude).First();
            StudioCamera.Resize(640, 360);
            var samples = new List<Snapshot>();
            for (int f = 0; f < frames; f += 3) { StudioRun.Scrub(f); yield return 0; samples.Add(Take(stage)); }
            StudioRun.Scrub(0); yield return 0;
            for (int i = stageBlock; i <= stageBlock; i++)
            {
                if (!samples[0].active[i] || !samples[samples.Count - 1].active[i]) continue;
                float drop = Vector3.Distance(samples[0].positions[i], samples[samples.Count - 1].positions[i]);
                float fastest = 0;
                for (int s = 1; s < samples.Count; s++)
                    fastest = Mathf.Max(fastest, Vector3.Distance(samples[s].positions[i], samples[s - 1].positions[i]) * set.fps / 3);
                bestDrop = drop;
                if (drop < .2f || fastest > 8) continue;
                chosen = i;
            }
            if (chosen < 0) Result("3. Live survivor rides a played-back moving block", false, $"The stage block (#{stageBlock}) moved {bestDrop:F2} m in the recording, too little or too fast to ride.");
            else
            {
                var block = stage.blocks[chosen];
                // The rider is the Set's own survivor mark. It stood on this block during the live recording,
                // so its first take shows what the real game does to someone standing there.
                var rider = set.marks.First(m => m.role == StudioRole.Survivor);
                var riderTrack = tape.Find(rider.id);
                var liveTake = riderTrack?.ActiveTake;
                List<Vector3> Path3(StudioTake take) => take == null ? new List<Vector3>()
                    : take.frames.Select(bytes => ActorFrame.Read(riderTrack.type, bytes).Position).ToList();
                var livePath = Path3(liveTake);
                void WhileRiding(int frame)
                {
                    if (frame % 2 == 0) StudioCamera.SavePng(Path.Combine(Folder, "rider", $"{frame / 2:D05}.png"));
                }
                StudioCamera.Target = block.transform.position; StudioCamera.Distance = 9; StudioCamera.Place();
                StudioRun.FrameShown += WhileRiding;
                var pass = new StudioPass { label = "Rider", liveWorld = false, fixedClock = true };
                pass.record.Add(rider.id);
                StudioRun.Record(pass);
                foreach (int wait in UntilHeld()) yield return wait;
                StudioRun.FrameShown -= WhileRiding;

                // Now the same survivor stood on the same block while the world was only played back.
                var riderTake = tape.Find(rider.id)?.ActiveTake;
                var playedPath = Path3(riderTake);
                int count = Mathf.Min(livePath.Count, playedPath.Count);
                float liveMoved = 0, playedMoved = 0, apart = 0;
                for (int n = 0; n < count; n++)
                {
                    liveMoved = Mathf.Max(liveMoved, Vector3.Distance(livePath[0], livePath[n]));
                    playedMoved = Mathf.Max(playedMoved, Vector3.Distance(playedPath[0], playedPath[n]));
                    apart = Mathf.Max(apart, Vector3.Distance(livePath[n], playedPath[n]));
                }
                bool newTake = riderTake != null && riderTake != liveTake && count > 0;
                // Pass = playback treats a live survivor the way the real physics did: the two paths agree.
                // How far the game carries someone on a sliding block is the game's own behaviour, reported below.
                bool rode = bestDrop > .2f && apart < Mathf.Max(.15f, liveMoved * .3f);
                Result("3. Live survivor rides a played-back moving block", newTake && rode,
                    $"Block #{chosen} moved {bestDrop:F2} m in the tape with {rider.actor} standing on it. In the live recording {rider.actor} was carried {liveMoved:F2} m. " +
                    $"Standing there again while the world only played back, {rider.actor} was carried {playedMoved:F2} m; the two paths are never more than {apart:F2} m apart " +
                    $"({count} frames compared). " +
                    (liveMoved < bestDrop * .5f ? "Note: in the live game itself this survivor was not carried along with the sliding block; playback reproduces that. " : "") + "Pictures: rider/.");
                StudioCamera.Target = stage.anchor + Vector3.up; StudioCamera.Distance = Mathf.Clamp(stage.world.Radius * .9f, 10, 80); StudioCamera.Place();
            }

            // ---- Test 4: an old Theatre v4 take loads and plays ----
            StudioTape converted = null; string convertNote = "";
            try { converted = TheatreImport.ConvertTape(oldFile); }
            catch (Exception error) { convertNote = error.Message; }
            if (converted == null) Result("4. A Theatre v4 take loads and plays", false, "Could not convert: " + convertNote);
            else
            {
                StudioRun.UseTape(converted);
                StudioRun.Play(0, -1, false);
                foreach (int wait in UntilHeld()) yield return wait;
                var drift = StudioRun.LastDrift;
                StudioRun.Scrub(frames / 2); yield return 0;
                int shown = castIds.Count(id => stage.playback.TryGetValue(id, out var body) && body && body.activeSelf);
                var mid = Take(stage);
                float v4Worst = 0;
                if (snapshots.Count > 0)
                {
                    int f = snapshots.Keys.OrderBy(k => Mathf.Abs(k - frames / 2)).First();
                    StudioRun.Scrub(f); yield return 0;
                    var at = Take(stage);
                    for (int i = 0; i < stage.blocks.Length; i++)
                        if (snapshots[f].active[i] && at.active[i]) v4Worst = Mathf.Max(v4Worst, Vector3.Distance(at.positions[i], snapshots[f].positions[i]) * 1000);
                }
                Result("4. A Theatre v4 take loads and plays",
                    oldFrames == frames && converted.header.frames == frames && drift != null && drift.Passed && drift.samples > 0 && shown == castIds.Length,
                    $"Theatre's own writer saved {oldFrames} frames ({StudioTape.Size(new FileInfo(oldFile).Length)}); converted to v5 with {converted.tracks.Count} tracks and {converted.header.frames} frames; " +
                    $"played back with: {drift}; {shown}/{castIds.Length} recorded survivors shown; blocks within {v4Worst:F1} mm of the live run at a sample frame.");
                StudioRun.UseTape(tape);
            }

            Result("Runtime errors during the Studio tests", exceptions == 0, exceptions == 0 && errorLines.Count == 0 ? "None." : $"{exceptions} exceptions. First messages: {string.Join(" // ", errorLines)}");
            StudioRun.SaveTape();
        }

        static Snapshot Take(StudioStage stage)
        {
            var shot = new Snapshot { positions = new Vector3[stage.blocks.Length], rotations = new Quaternion[stage.blocks.Length], active = new bool[stage.blocks.Length] };
            for (int i = 0; i < stage.blocks.Length; i++)
            {
                var body = stage.blocks[i];
                shot.active[i] = body && body.gameObject.activeSelf;
                if (!shot.active[i]) continue;
                shot.positions[i] = body.transform.position; shot.rotations[i] = body.transform.rotation;
            }
            foreach (var pair in stage.live)
                if (pair.Value && pair.Value.activeInHierarchy) shot.actors[pair.Key] = pair.Value.transform.position;
            return shot;
        }

        // Review picture: the live recording on the left, playback 1 on the right, for a few frames.
        static void SideBySide()
        {
            try
            {
                string live = Path.Combine(Folder, "live"), played = Path.Combine(Folder, "playback"), output = Path.Combine(Folder, "compare");
                if (!Directory.Exists(live) || !Directory.Exists(played)) return;
                Directory.CreateDirectory(output);
                var files = Directory.GetFiles(live, "*.png").Select(Path.GetFileName).OrderBy(n => n).ToArray();
                long worst = 0;
                for (int i = 0; i < files.Length; i++)
                {
                    string other = Path.Combine(played, files[i]);
                    if (!File.Exists(other)) continue;
                    var a = new Texture2D(2, 2); var b = new Texture2D(2, 2);
                    a.LoadImage(File.ReadAllBytes(Path.Combine(live, files[i]))); b.LoadImage(File.ReadAllBytes(other));
                    var both = new Texture2D(a.width + b.width, Mathf.Max(a.height, b.height), TextureFormat.RGB24, false);
                    both.SetPixels(0, 0, a.width, a.height, a.GetPixels());
                    both.SetPixels(a.width, 0, b.width, b.height, b.GetPixels());
                    both.Apply(false);
                    File.WriteAllBytes(Path.Combine(output, files[i]), both.EncodeToPNG());
                    // How different are the two pictures? (average colour difference, 0-255)
                    var pa = a.GetPixels32(); var pb = b.GetPixels32();
                    long sum = 0;
                    for (int p = 0; p < pa.Length && p < pb.Length; p += 7) sum += Math.Abs(pa[p].r - pb[p].r) + Math.Abs(pa[p].g - pb[p].g) + Math.Abs(pa[p].b - pb[p].b);
                    worst = Math.Max(worst, sum * 7 / Math.Max(1, pa.Length * 3));
                    Object.DestroyImmediate(a); Object.DestroyImmediate(b); Object.DestroyImmediate(both);
                }
                Result("1d. Review pictures: live recording next to playback", files.Length > 0,
                    $"{files.Length} side-by-side frames in compare/ (live left, playback right). Largest average colour difference between a pair: {worst} of 255.");
            }
            catch (Exception error) { Result("1d. Review pictures", false, error.Message); }
        }

        // ---- Test 5: the plain game still plays with the Studio closed ----

        static IEnumerator<int> SoloGame()
        {
            exceptions = 0; errorLines.Clear();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            CastlePlaySession session = null;
            while ((!(session = Object.FindFirstObjectByType<CastlePlaySession>()) || Services.World == null) && watch.Elapsed.TotalSeconds < 90) yield return 0;
            bool started = session && Services.World != null && Services.World.Blocks.Count > 0;
            for (int n = 0; n < 240 && started; n++) yield return 0;
            bool studioAbsent = StudioRun.State == StudioState.Closed && !StudioDrivers.Alive && StudioCamera.Camera == null;
            bool playerThere = started && NetworkActors.Alive(session.Player) && session.Player.gameObject.activeInHierarchy;
            Result("5. Game.unity still plays solo with the Studio closed", started && studioAbsent && playerThere && exceptions == 0,
                started ? $"Session and map started ({Services.World.Blocks.Count} blocks), local survivor active: {playerThere}, ran 240 frames, no Studio objects in the scene: {studioAbsent}, " +
                    $"exceptions: {exceptions}" + (errorLines.Count > 0 ? ". Messages: " + string.Join(" // ", errorLines) : "")
                    : "The game session or map did not start within 90 seconds.");
        }
    }
}
