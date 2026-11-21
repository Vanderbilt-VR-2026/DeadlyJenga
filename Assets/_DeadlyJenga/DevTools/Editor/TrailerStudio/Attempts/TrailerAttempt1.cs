using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // An AI's attempt at a 15-second gameplay trailer, made only through the Studio's own data model and engine.
    // Two entry points, because recording plus a 1080p render does not fit in one ten-minute test run:
    //   ...TrailerAttempt1.RunRecord  sets up the castle, records one whole-set take (world, actors, sound),
    //                                 renders a small preview of every camera, and saves the take as a bundle.
    //   ...TrailerAttempt1.RunRender  loads the committed bundle (Docs/.../attempts/attempt-1/bundle),
    //                                 renders the cameras at full size and exports the cut.
    // Everything goes to Logs/TrailerAttempt1/.
    [InitializeOnLoad]
    public static class TrailerAttempt1
    {
        const int Iteration = 6;
        const string Subject = "Dev";   // from the record run's motion log: on the camera-side edge of the top
        const int Fps = 30;

        const string Key = "DeadlyJenga.TrailerAttempt1";
        const string SetId = "trailer-attempt-1";
        static int Step { get => SessionState.GetInt(Key, 0); set => SessionState.SetInt(Key, value); }
        static bool Recording { get => SessionState.GetBool(Key + ".Record", true); set => SessionState.SetBool(Key + ".Record", value); }
        static double Deadline { get => SessionState.GetFloat(Key + ".Deadline", 0); set => SessionState.SetFloat(Key + ".Deadline", (float)value); }
        static string Failure { get => SessionState.GetString(Key + ".Failure", ""); set => SessionState.SetString(Key + ".Failure", value); }

        static IEnumerator<int> routine;
        static int lastTick = -1;
        static readonly StringBuilder notes = new StringBuilder();

        public static string Folder => Path.Combine(StudioLibrary.ProjectRoot, "Logs", "TrailerAttempt1");
        static string Bundle => Path.Combine(StudioLibrary.ProjectRoot, "Docs", "Development", "TrailerStudio", "attempts", "attempt-1", "bundle");

        static TrailerAttempt1() { if (Step != 0) Hook(); }
        static void Hook() { EditorApplication.update -= Tick; EditorApplication.update += Tick; }

        public static void RunRecord() => Begin(true);
        public static void RunRender() { Only = ""; Begin(false); }
        // A 1080p render of all six cameras did not fit in ten minutes on a busy PC, so it is split in two runs.
        public static void RunRender1() { Only = "A Establish|B Edge|D Collapse"; Begin(false); }
        public static void RunRender2() { Only = "C Falling|E Wave|F Finale"; Begin(false); }
        static string Only { get => SessionState.GetString(Key + ".Only", ""); set => SessionState.SetString(Key + ".Only", value); }

        static void Begin(bool record)
        {
            if (Directory.Exists(Folder)) Directory.Delete(Folder, true);
            Directory.CreateDirectory(Folder);
            notes.Clear();
            string tapes = StudioLibrary.TapeFolder(SetId);
            if (Directory.Exists(tapes)) Directory.Delete(tapes, true);
            Recording = record;
            Failure = ""; Step = 1;
            Deadline = EditorApplication.timeSinceStartup + 120;
            Hook();
        }

        static void Note(string line)
        {
            notes.AppendLine(line);
            Debug.Log("[TrailerAttempt1] " + line);
            try { File.WriteAllText(Path.Combine(Folder, "notes.md"), notes.ToString()); } catch { }
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
                        StudioLibrary.SetsFolderOverride = Path.Combine(Folder, "sets");
                        StudioRun.OpenStage(Recording ? NewSet() : LoadBundle());
                        Step = 2; Deadline = EditorApplication.timeSinceStartup + 560;
                        break;
                    case 2:
                        if (routine == null)
                        {
                            if (StudioRun.State == StudioState.Held) routine = Recording ? Record() : Render();
                            else if (EditorApplication.timeSinceStartup > Deadline) throw new TimeoutException("The stage never opened: " + StudioRun.Status);
                            return;
                        }
                        if (EditorApplication.timeSinceStartup > Deadline) { StudioRender.Cancel(); throw new TimeoutException("Out of time inside the stage."); }
                        if (Time.frameCount == lastTick) return;
                        lastTick = Time.frameCount;
                        if (routine.MoveNext()) return;
                        routine = null;
                        Step = 3;
                        StudioRun.CloseStage();
                        break;
                    case 3:
                        if (EditorApplication.isPlayingOrWillChangePlaymode || StudioRun.State != StudioState.Closed) return;
                        Finish();
                        break;
                }
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Failure = error.Message;
                Note("FAIL: stopped at step " + Step + ": " + error.Message);
                routine = null;
                if (StudioRun.State != StudioState.Closed && Step < 3) { Step = 3; StudioRun.CloseStage(); }
                else Finish();
            }
        }

        static void Finish()
        {
            bool failed = Failure.Length > 0;
            Step = 0;
            StudioLibrary.SetsFolderOverride = "";
            EditorApplication.update -= Tick;
            Note(failed ? "FAIL trailer attempt: " + Failure : "PASS trailer attempt finished, output in " + Folder);
            if (SessionState.GetString("DJWarm.id", "") != "")
            {
                Directory.CreateDirectory(Path.Combine("Logs", "Warm"));
                File.WriteAllText(Path.Combine("Logs", "Warm", "exit.txt"), failed ? "1" : "0");
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
                return;
            }
            bool startedForThis = Environment.GetCommandLineArgs().Any(a => a.Contains("TrailerAttempt1.Run"));
            if (Application.isBatchMode || startedForThis) EditorApplication.Exit(failed ? 1 : 0);
        }

        static StudioSet NewSet()
        {
            var set = new StudioSet
            {
                id = SetId, title = "Deadly Jenga - trailer attempt", map = "castle", fps = Fps, length = 13.6f, seed = 2026,
                handBot = true, notes = "Made by TrailerAttempt1. Safe to delete.",
                alive = new StudioAliveSettings { on = false }   // rendered before the alive layer existed; keep it as it was
            };
            StudioLibrary.Save(set);
            return set;
        }

        // The take from an earlier record run, committed to the repository: the Set, the tape and its sound.
        static StudioSet LoadBundle()
        {
            if (!Directory.Exists(Bundle)) throw new DirectoryNotFoundException("No recorded bundle at " + Bundle);
            var set = JsonUtility.FromJson<StudioSet>(File.ReadAllText(Path.Combine(Bundle, "set.json")));
            set.alive.on = false;   // bundles from before the alive layer play back as they were recorded
            string tape = StudioLibrary.TapePath(SetId);
            Directory.CreateDirectory(Path.GetDirectoryName(tape));
            File.Copy(Path.Combine(Bundle, "main.djtape"), tape, true);
            string take = File.ReadAllText(Path.Combine(Bundle, "take.txt")).Trim();
            string audio = StudioRender.TakeAudioFile(SetId, take);
            Directory.CreateDirectory(Path.GetDirectoryName(audio));
            File.Copy(Path.Combine(Bundle, "tape.wav"), audio, true);
            StudioLibrary.Save(set);
            return set;
        }

        // ---- Helpers ----

        static StudioCameraKey K(float time, Vector3 position, Vector3 lookAt, float focal, int ease = 1, float aperture = 16, float focus = 10) => new StudioCameraKey
        {
            time = time, position = position, euler = Quaternion.LookRotation((lookAt - position).sqrMagnitude < .0001f ? Vector3.forward : lookAt - position).eulerAngles,
            focal = focal, aperture = aperture, focus = focus, ease = ease
        };

        static IEnumerable<int> UntilHeld()
        {
            float started = Time.realtimeSinceStartup;
            while (StudioRun.State != StudioState.Held)
            {
                if (StudioRun.State == StudioState.Closed) throw new InvalidOperationException("The stage closed: " + StudioRun.Status);
                if (Time.realtimeSinceStartup - started > 300) throw new TimeoutException("A run took more than five minutes: " + StudioRun.Status);
                yield return 0;
            }
        }

        static IEnumerable<int> UntilRendered() { while (StudioRender.Running) yield return 0; }

        static Vector3? ActorAt(StudioTape tape, string markId, float time, int fps)
        {
            var pose = StudioActorPoses.At(tape, markId, time, fps);
            return pose.found ? pose.feet : (Vector3?)null;
        }

        static readonly Vector3 Side = new Vector3(1, 0, -1).normalized;   // the main camera side; survivors face it

        // ---- Record run ----

        static IEnumerator<int> Record()
        {
            var stage = StudioRun.Stage;
            var set = stage.set;
            var tape = stage.tape;
            Vector3 anchor = stage.anchor;
            float water = stage.world.WaterHeight(anchor);
            float towerHeight = anchor.y - water;
            Note($"# Trailer attempt, record run (iteration {Iteration})\n");
            Note($"Map {set.map}: {stage.blocks.Length} blocks. Anchor {anchor}. Tower height above water {towerHeight:0.0} m.");

            // Survivors on the top of the spire, the hand bot beside it.
            var spots = new List<Vector3>();
            for (float r = 0; r <= 4.5f; r += .75f)
                for (int a = 0; a < (r == 0 ? 1 : 10); a++)
                {
                    Vector3 offset = Quaternion.Euler(0, a * 36 + r * 17, 0) * new Vector3(r, 0, 0);
                    if (!StudioStage.SurfaceBelow(anchor + offset + Vector3.up * .5f, out var surface)) continue;
                    if (anchor.y - surface.y > 3.5f) continue;
                    if (spots.Any(s => Vector3.Distance(s, surface) < 1.1f)) continue;
                    if (stage.MarkWarning(new StudioMark { position = stage.ToStage(surface) }) != "") continue;
                    spots.Add(surface);
                }
            if (spots.Count == 0) throw new InvalidOperationException("No place to stand on the top of the tower.");
            float faceCamera = Quaternion.LookRotation(Side).eulerAngles.y;
            set.marks.Clear();
            var hero = set.AddMark(StudioRole.Survivor, stage.ToStage(spots[0]));
            hero.actor = "Hero"; hero.yaw = faceCamera; hero.color = 0;
            var names = new[] { "Ada", "Ben", "Cleo", "Dev", "Eli" };
            var crew = new List<StudioMark>();
            for (int i = 1; i < spots.Count && crew.Count < 5; i++)
            {
                var mark = set.AddMark(i % 3 == 0 ? StudioRole.Survivor : StudioRole.Bot, stage.ToStage(spots[i]));
                mark.actor = names[crew.Count]; mark.color = crew.Count + 1; mark.yaw = faceCamera + (i * 47) % 120 - 60;
                crew.Add(mark);
            }
            var hand = set.AddMark(StudioRole.Hand, -Side * 5 + Vector3.up * 3);
            hand.actor = "Hand";
            Note("Marks: " + string.Join("; ", set.marks.Select(m => $"{m.actor} ({m.role}) at {m.position}")));

            // The pull: loosen one spire block on the camera side, then slide it out. Then the collapse, then the wave.
            var candidates = Enumerable.Range(0, stage.blocks.Length)
                .Where(i => stage.blocks[i] && stage.blocks[i].gameObject.activeInHierarchy)
                .Where(i => anchor.y - stage.blocks[i].position.y > 2 && anchor.y - stage.blocks[i].position.y < 5.5f)
                .Where(i => Vector3.ProjectOnPlane(stage.blocks[i].position - anchor, Vector3.up).magnitude < 7)
                .OrderByDescending(i => Vector3.Dot(Vector3.ProjectOnPlane(stage.blocks[i].position - anchor, Vector3.up), Side)).ToList();
            int pull = candidates.Count > 0 ? candidates[0] : -1;
            Vector3 pullPoint = pull >= 0 ? stage.blocks[pull].position : anchor + Vector3.down * 3;
            Vector3 pullOut = Vector3.ProjectOnPlane(pullPoint - anchor, Vector3.up);
            pullOut = pullOut.sqrMagnitude < .01f ? Side : pullOut.normalized;
            set.cues.Clear();
            var loosen = new StudioCue { at = 5.9f, action = "weaken", point = stage.ToStage(pullPoint) };
            var slide = new StudioCue { at = 6.0f, action = "push", strength = 9, direction = pullOut + Vector3.up * .08f, point = stage.ToStage(pullPoint) };
            if (pull >= 0) { loosen.blocks.Add(pull); slide.blocks.Add(pull); }
            set.cues.Add(loosen); set.cues.Add(slide);
            Vector3 heroWorld = stage.ToWorld(hero.position);
            set.cues.Add(new StudioCue { at = 7.0f, action = "collapse", strength = 8, point = stage.ToStage(heroWorld - Side * 2.5f + Vector3.down * 1.5f) });
            set.cues.Add(new StudioCue { at = 9.4f, action = "tsunami", heading = 225, strength = 22, height = Mathf.Max(6, towerHeight * .55f) });
            Note($"Pull block #{pull} at {pullPoint} ({anchor.y - pullPoint.y:0.0} m below the top), out along {pullOut}.");
            StudioLibrary.Save(set);
            StudioRun.Refresh();
            yield return 0;

            StudioRun.RecordWholeSet();
            foreach (int wait in UntilHeld()) yield return wait;
            StudioRun.SaveTape();
            Note("Cue log: " + string.Join(" | ", StudioRun.CueLog));
            Note($"Recording has sound: {StudioRun.LastRecordingHasSound}. Sound file: {StudioRender.AudioFile(set.id)}");

            var motion = new StringBuilder("Motion (relative to the anchor, from the tape):\n");
            foreach (var mark in set.marks)
            {
                motion.Append($"- {mark.actor}: ");
                for (float t = 0; t < set.length; t += 1)
                {
                    var at = ActorAt(tape, mark.id, t, set.fps);
                    motion.Append(at.HasValue ? $"{t:0}s {(at.Value - anchor).x:0.0},{(at.Value - anchor).y:0.0},{(at.Value - anchor).z:0.0}  " : $"{t:0}s -  ");
                }
                motion.Append('\n');
            }
            Note(motion.ToString());
            var worldTake = tape.FindType(WorldTrack.Id).ActiveTake;
            WorldTrack.Positions(worldTake, worldTake.start, out var start, out _);
            if (pull >= 0)
                for (float t = 5.5f; t < 7.5f; t += .5f)
                {
                    WorldTrack.Positions(worldTake, Mathf.RoundToInt(t * set.fps), out var p, out _);
                    Note($"Pull block at {t:0.0}s moved {Vector3.Distance(p[pull], start[pull]):0.00} m.");
                }
            for (float t = 6; t < set.length; t += 1)
            {
                WorldTrack.Positions(worldTake, Mathf.Min(Mathf.RoundToInt(t * set.fps), set.Frames - 1), out var now, out var on);
                int moved = 0;
                for (int i = 0; i < start.Length; i++) if (on[i] && Vector3.Distance(start[i], now[i]) > .5f) moved++;
                Note($"World at {t:0}s: {moved} blocks moved more than 0.5 m.");
            }

            // Bundle for the render runs.
            string bundle = Path.Combine(Folder, "bundle");
            Directory.CreateDirectory(bundle);
            File.WriteAllText(Path.Combine(bundle, "set.json"), JsonUtility.ToJson(set, true));
            File.Copy(StudioLibrary.TapePath(set.id), Path.Combine(bundle, "main.djtape"), true);
            File.WriteAllText(Path.Combine(bundle, "take.txt"), worldTake.id);
            string audio = StudioRender.AudioFile(set.id);
            if (File.Exists(audio))
            {
                File.Copy(audio, Path.Combine(bundle, "tape.wav"), true);
                string stems = Path.Combine(Path.GetDirectoryName(audio), "stems");
                if (Directory.Exists(stems)) { Directory.CreateDirectory(Path.Combine(Folder, "stems")); foreach (var s in Directory.GetFiles(stems)) File.Copy(s, Path.Combine(Folder, "stems", Path.GetFileName(s)), true); }
            }

            // A small preview of every camera, to check framing.
            foreach (int wait in Shoot(640, 360, 1, false)) yield return wait;
        }

        // ---- Render run ----

        static IEnumerator<int> Render()
        {
            Note($"# Trailer attempt, render run (iteration {Iteration})\n");
            foreach (int wait in Shoot(1920, 1080, 1, true)) yield return wait;
        }

        // ---- Cameras, cut, render, export ----

        static IEnumerable<int> Shoot(int width, int height, int subFrames, bool cinema)
        {
            var stage = StudioRun.Stage;
            var set = stage.set;
            var tape = stage.tape;
            Vector3 anchor = stage.anchor;
            float water = stage.world.WaterHeight(anchor);
            float H = Mathf.Max(anchor.y - water, 6);
            var hero = set.marks.First(m => m.actor == Subject);
            var hand = set.marks.FirstOrDefault(m => m.role == StudioRole.Hand);
            Vector3 up = Vector3.up, across = Vector3.Cross(up, Side);
            // The subject stands on the camera-side edge of the top from 2 s to 7 s in the recorded take.
            Vector3 heroStage = (ActorAt(tape, hero.id, 4f, set.fps) ?? stage.ToWorld(hero.position)) - anchor;
            Note($"Subject {hero.actor} at {heroStage} (stage) at 4 s.");

            // Performance layer on the subject: cowers as the block slides out.
            set.overrides.Clear();
            set.overrides.Add(new StudioOverride { actor = hero.id, kind = "reaction", name = "cower", from = 5.9f, to = 7.0f });

            void Style(StudioCameraTrack c) { c.bloom = .35f; c.vignette = .45f; c.grain = .12f; c.chroma = .06f; }
            set.cameras.Clear();
            // A. Establishing: far over the ocean, the whole spire, slow push in.
            var establish = new StudioCameraTrack { name = "A Establish", start = 0, end = 2.8f, noise = "subtle" };
            establish.keys.Add(K(0, Side * 72 + up * -9, new Vector3(0, -24, 0), 18));
            establish.keys.Add(K(2.8f, Side * 50 + up * -6, new Vector3(0, -18, 0), 20, 3));
            // B. The subject on the edge of the top, sky behind, slow push in.
            var heroShot = new StudioCameraTrack { name = "B Edge", start = 2.8f, end = 5.4f, behavior = "follow", subject = hero.id, lag = .15f, noise = "walking", noiseStrength = .5f, focusOnSubject = true, aimOffset = new Vector3(0, 1.0f, 0) };
            // Iteration 5: the collapse shot's angle (clear line of sight in iteration 4), much closer.
            heroShot.keys.Add(K(2.8f, heroStage + Side * 6f + across * 3f + up * 1.8f, heroStage + up, 32, 1, 4f));
            heroShot.keys.Add(K(5.4f, heroStage + Side * 4.5f + across * 2.2f + up * 1.4f, heroStage + up, 36, 1, 4f));
            // D. The pull and the collapse, slowing down as the top goes, on the subject.
            var fall = new StudioCameraTrack { name = "D Collapse", start = 5.6f, end = 8.6f, behavior = "follow", subject = hero.id, lag = .15f, noise = "impact", noiseStrength = .4f };
            fall.keys.Add(K(5.6f, heroStage + Side * 11 + across * 3 + up * 1, heroStage, 34));
            fall.keys.Add(K(8.6f, heroStage + Side * 13 + across * 4 + up * -3, heroStage + up * -6, 28));
            fall.speeds.Add(new StudioSpeedMark { time = 5.6f, speed = 1 });
            fall.speeds.Add(new StudioSpeedMark { time = 6.8f, speed = 1 });
            fall.speeds.Add(new StudioSpeedMark { time = 7.1f, speed = .35f });
            fall.speeds.Add(new StudioSpeedMark { time = 7.8f, speed = .35f });
            fall.speeds.Add(new StudioSpeedMark { time = 8.2f, speed = 1 });
            // C. Falling with the subject (iteration 3's hand shot showed no hand: the played-back hand is not drawn).
            var handShot = new StudioCameraTrack { name = "C Falling", start = 7.9f, end = 9.6f, behavior = "ride", subject = hero.id, lag = .05f, noise = "impact", noiseStrength = .5f, aimOffset = new Vector3(0, .8f, 0) };
            handShot.keys.Add(new StudioCameraKey { time = 7.9f, position = Side * 3.2f + across * 1.5f + up * 2.2f, focal = 22, aperture = 16 });
            // E. The wave hits the castle: low over the water, wide.
            var wave = new StudioCameraTrack { name = "E Wave", start = 9.4f, end = 12.0f, noise = "walking" };
            Vector3 waveSide = Quaternion.Euler(0, -35, 0) * Side;
            wave.keys.Add(K(9.4f, waveSide * 47 + up * (-H * .75f), new Vector3(0, -H * .45f, 0), 22));
            wave.keys.Add(K(12.0f, waveSide * 42 + up * (-H * .6f), new Vector3(0, -H * .35f, 0), 22));
            // F. Finale: high and wide over what is left; the title goes on top of this.
            var finale = new StudioCameraTrack { name = "F Finale", start = 11.2f, end = 13.6f, noise = "subtle" };
            Vector3 finSide = Quaternion.Euler(0, -60, 0) * Side;
            // Iteration 4: high above the wave (it filled the lens in iteration 3).
            finale.keys.Add(K(11.2f, finSide * 50 + up * 2, new Vector3(0, -46, 0), 20));
            finale.keys.Add(K(13.6f, finSide * 62 + up * 8, new Vector3(0, -48, 0), 20, 3));
            int color = 0;
            // Fixed ids, so the shot folders of two separate render runs belong to the same cameras.
            foreach (var c in new[] { establish, heroShot, fall, handShot, wave, finale }) { Style(c); c.id = "cam" + color; c.color = color++; set.cameras.Add(c); }

            float fallLength = StudioCameras.OutputLength(fall, set, Fps);
            set.cuts.Clear();
            set.cuts.Add(new StudioCut { camera = establish.id, from = 0, to = 2.8f });
            set.cuts.Add(new StudioCut { camera = heroShot.id, from = 0, to = 2.4f });
            set.cuts.Add(new StudioCut { camera = fall.id, from = 0, to = Mathf.Min(3.4f, fallLength) });
            set.cuts.Add(new StudioCut { camera = handShot.id, from = 0, to = 1.6f });
            set.cuts.Add(new StudioCut { camera = wave.id, from = 0, to = 2.4f });
            set.cuts.Add(new StudioCut { camera = finale.id, from = 0, to = 2.4f });
            Note($"Cut: {set.cuts.Count} pieces, {set.cuts.Sum(c => c.to - c.from):0.00} s. Slow-motion shot output length {fallLength:0.00} s.");
            // The music bed: the game's own match music. The Studio puts it on its own lane in the FCPXML.
            string music = AssetDatabase.FindAssets("Music_01 t:AudioClip").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault() ?? "";
            set.music = music.Length > 0 ? Path.GetFullPath(music) : "";
            Note("Music bed: " + (set.music.Length > 0 ? set.music : "none found"));
            set.render = new StudioRenderSettings { width = width, height = height, fps = Fps, subFrames = subFrames, format = "png", cinema = cinema, freezeAmbient = false };
            StudioLibrary.Save(set);

            string renders = Path.Combine(Folder, "renders");
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var only = Only.Length > 0 ? Only.Split('|') : null;
            StudioRender.Start(set.cameras.Where(c => only == null || only.Contains(c.name)), set.render, renders);
            foreach (int wait in UntilRendered()) yield return wait;
            Note($"Render {width}x{height}, {subFrames} sub-frames: {StudioRender.Progress} in {clock.Elapsed.TotalSeconds:0} s. " +
                string.Join("; ", StudioRender.Results.Select(r => $"{r.camera}: {r.frames} frames, movie {(r.movie.Length > 0 ? Path.GetFileName(r.movie) : "none")} {r.error}")));

            // Shots rendered by the other render run: their shot records (committed to the bundle) let the export
            // place them in the cut. The pictures themselves are joined outside Unity.
            string others = Path.Combine(Bundle, "shots");
            if (only != null && Directory.Exists(others))
                foreach (string record in Directory.GetFiles(others, "*.json"))
                {
                    string folder = Path.Combine(renders, Path.GetFileNameWithoutExtension(record));
                    if (Directory.Exists(folder)) continue;
                    Directory.CreateDirectory(folder);
                    File.Copy(record, Path.Combine(folder, "shot.json"));
                }
            string audio = StudioRender.AudioFile(set.id);
            try
            {
                string xml = StudioExport.WriteFcpxml(set, renders, audio, out var problems);
                string edl = StudioExport.WriteEdl(set, renders, out _);
                Note($"Export: {Path.GetFileName(xml)}, {Path.GetFileName(edl)}. Exporter notes: {(problems.Count == 0 ? "none" : string.Join(" / ", problems))}");
                var pieces = StudioExport.Resolve(set, renders, out _);
                var manifest = new StringBuilder("index\tshot\tmedia\tinFrame\tframes\trecordStart\tdissolve\tfps\ttapeStart\n");
                for (int i = 0; i < pieces.Count; i++)
                    manifest.Append($"{i}\t{pieces[i].shot.name}\t{pieces[i].media}\t{pieces[i].inFrame}\t{pieces[i].frames}\t{pieces[i].recordStart}\t{pieces[i].cut.dissolve}\t{pieces[i].shot.fps}\t{pieces[i].shot.tapeStart}\n");
                File.WriteAllText(Path.Combine(renders, "pieces.tsv"), manifest.ToString());
            }
            catch (Exception error) { Note("Export failed: " + error.Message); }
            File.WriteAllText(Path.Combine(Folder, "set-final.json"), JsonUtility.ToJson(set, true));
            Note("Done.");
        }
    }
}
