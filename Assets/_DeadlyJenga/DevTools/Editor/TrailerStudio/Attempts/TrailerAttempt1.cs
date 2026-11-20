using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // An AI's attempt at a 15-second gameplay trailer, made only through the Studio's own data model and engine.
    // Run from the command line (or the coordinator):
    //   -executeMethod DeadlyJenga.DevTools.Editor.TrailerStudio.TrailerAttempt1.Run
    // Everything goes to Logs/TrailerAttempt1/: scout pictures, notes, the rendered shots, the cut export.
    [InitializeOnLoad]
    public static class TrailerAttempt1
    {
        // ---- What this iteration renders (changed between iterations) ----
        const int Iteration = 1;
        const int Width = 960, Height = 540, Fps = 30, SubFrames = 1;
        const bool Cinema = true;
        const bool Scout = true;            // scout pictures and notes before recording

        const string Key = "DeadlyJenga.TrailerAttempt1";
        const string SetId = "trailer-attempt-1";
        static int Step { get => SessionState.GetInt(Key, 0); set => SessionState.SetInt(Key, value); }
        static double Deadline { get => SessionState.GetFloat(Key + ".Deadline", 0); set => SessionState.SetFloat(Key + ".Deadline", (float)value); }
        static string Failure { get => SessionState.GetString(Key + ".Failure", ""); set => SessionState.SetString(Key + ".Failure", value); }

        static IEnumerator<int> routine;
        static int lastTick = -1;
        static readonly StringBuilder notes = new StringBuilder();

        public static string Folder => Path.Combine(StudioLibrary.ProjectRoot, "Logs", "TrailerAttempt1");

        static TrailerAttempt1() { if (Step != 0) Hook(); }
        static void Hook() { EditorApplication.update -= Tick; EditorApplication.update += Tick; }

        public static void Run()
        {
            if (Directory.Exists(Folder)) Directory.Delete(Folder, true);
            Directory.CreateDirectory(Folder);
            // A fresh tape every run, so nothing from an older attempt is mixed in.
            string tapes = StudioLibrary.TapeFolder(SetId);
            if (Directory.Exists(tapes)) Directory.Delete(tapes, true);
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
                        StudioRun.OpenStage(MakeSet());
                        Step = 2; Deadline = EditorApplication.timeSinceStartup + 540;
                        break;
                    case 2:
                        if (routine == null)
                        {
                            if (StudioRun.State == StudioState.Held) routine = Film();
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
            Note(failed ? "FAIL trailer attempt: " + Failure : "PASS trailer attempt rendered and exported to " + Folder);
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

        static StudioSet MakeSet()
        {
            var set = new StudioSet
            {
                id = SetId, title = "Deadly Jenga - trailer attempt " + Iteration, map = "castle", fps = Fps, length = 14, seed = 2026,
                handBot = true, notes = "Made by TrailerAttempt1. Safe to delete."
            };
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

        static IEnumerable<int> UntilRendered()
        {
            while (StudioRender.Running) yield return 0;
        }

        static void Shoot(string file, Vector3 target, float yaw, float pitch, float distance)
        {
            StudioCamera.Target = target; StudioCamera.Yaw = yaw; StudioCamera.Pitch = pitch; StudioCamera.Distance = distance; StudioCamera.Place();
            StudioCamera.SavePng(file);
        }

        static Vector3? ActorAt(StudioTape tape, string markId, float time, int fps)
        {
            var pose = StudioActorPoses.At(tape, markId, time, fps);
            return pose.found ? pose.feet : (Vector3?)null;
        }

        // ---- The film ----

        static IEnumerator<int> Film()
        {
            var stage = StudioRun.Stage;
            var set = stage.set;
            var tape = stage.tape;
            var world = stage.world;
            Vector3 anchor = stage.anchor;
            float water = world.WaterHeight(anchor);

            // Bounds of the map's blocks.
            var live = stage.blocks.Where(b => b && b.gameObject.activeInHierarchy).ToArray();
            var bounds = new Bounds(live[0].position, Vector3.zero);
            foreach (var b in live) foreach (var c in b.GetComponentsInChildren<Collider>()) if (c.enabled && !c.isTrigger) bounds.Encapsulate(c.bounds);
            float towerHeight = anchor.y - water;
            float radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
            Note($"# Trailer attempt, iteration {Iteration}\n");
            Note($"Map {set.map}: {live.Length} blocks. Anchor (top of the tower) {anchor}. Water height {water:0.00}. Tower height above water {towerHeight:0.0} m.");
            Note($"Block bounds centre {bounds.center}, size {bounds.size}. World centre {world.Center}, world radius {world.Radius:0.0}, top height {world.TopHeight:0.0}. Playable bounds {world.PlayableBounds.center} size {world.PlayableBounds.size}.");
            Note("Disasters: " + string.Join("; ", SetLists.Hazards.Select(h =>
            {
                var prefab = DemoDisasters.Prefabs.FirstOrDefault(p => p && p.name == h);
                return h + (prefab ? " (" + string.Join(", ", DemoDisasters.Parameters(prefab).Select(p => $"{p.id} {p.min}..{p.max} default {p.initial}")) + ")" : "");
            })));

            // Height profile: how many blocks per 2 m band below the top.
            var bands = live.GroupBy(b => Mathf.FloorToInt((anchor.y - b.position.y) / 2)).OrderBy(g => g.Key).Select(g => $"{g.Key * 2}-{g.Key * 2 + 2} m below top: {g.Count()} blocks, spread {g.Max(b => Vector3.ProjectOnPlane(b.position - anchor, Vector3.up).magnitude):0.0} m");
            Note("Height profile: " + string.Join("; ", bands));

            if (Scout)
            {
                StudioCamera.Resize(960, 540);
                float far = Mathf.Max(bounds.extents.magnitude * 1.6f, 18);
                for (int yaw = 0; yaw < 360; yaw += 45) Shoot(Path.Combine(Folder, "scout", $"wide-yaw{yaw:000}.png"), bounds.center, yaw, 18, far);
                for (int yaw = 0; yaw < 360; yaw += 90) Shoot(Path.Combine(Folder, "scout", $"top-yaw{yaw:000}.png"), anchor, yaw, 30, 9);
                Shoot(Path.Combine(Folder, "scout", "overhead.png"), bounds.center, 0, 85, far);
                yield return 0;
            }

            // ---- Marks: survivors on the top of the tower, the hand bot nearby ----
            set.marks.Clear();
            var spots = new List<Vector3>();
            for (float r = 0; r <= 4.5f; r += .75f)
                for (int a = 0; a < (r == 0 ? 1 : 10); a++)
                {
                    Vector3 offset = Quaternion.Euler(0, a * 36 + r * 17, 0) * new Vector3(r, 0, 0);
                    if (!StudioStage.SurfaceBelow(anchor + offset + Vector3.up * .5f, out var surface)) continue;
                    if (anchor.y - surface.y > 3.5f) continue;   // only the top levels
                    if (spots.Any(s => Vector3.Distance(s, surface) < 1.1f)) continue;
                    spots.Add(surface);
                }
            // Main camera side: the survivors face it.
            Vector3 side = new Vector3(1, 0, -1).normalized;
            float faceCamera = Quaternion.LookRotation(side).eulerAngles.y;
            var good = new List<Vector3>();
            foreach (var spot in spots)
            {
                var probe = new StudioMark { position = stage.ToStage(spot), role = StudioRole.Survivor };
                if (stage.MarkWarning(probe) == "") good.Add(spot);
            }
            Note($"Found {spots.Count} surface spots on the top levels, {good.Count} without a warning.");
            if (good.Count == 0) throw new InvalidOperationException("No place to stand on the top of the tower.");

            var hero = set.AddMark(StudioRole.Survivor, stage.ToStage(good[0]));
            hero.actor = "Hero"; hero.yaw = faceCamera; hero.color = 0;
            var names = new[] { "Ada", "Ben", "Cleo", "Dev", "Eli" };
            var crew = new List<StudioMark>();
            for (int i = 1; i < good.Count && crew.Count < 5; i++)
            {
                var mark = set.AddMark(i % 3 == 0 ? StudioRole.Survivor : StudioRole.Bot, stage.ToStage(good[i]));
                mark.actor = names[crew.Count]; mark.color = crew.Count + 1; mark.yaw = faceCamera + (i * 47) % 120 - 60;
                crew.Add(mark);
            }
            var hand = set.AddMark(StudioRole.Hand, new Vector3(-side.x * 6, 4, -side.z * 6) + new Vector3(0, 0, 0));
            hand.actor = "Hand";
            Note("Marks: " + string.Join("; ", set.marks.Select(m => $"{m.actor} ({m.role}) at {m.position} warn \"{stage.MarkWarning(m)}\"")));

            // ---- Cues ----
            // The "pull": a block two to six metres below the top, on the camera side, slid straight out.
            var pullCandidates = Enumerable.Range(0, stage.blocks.Length)
                .Where(i => stage.blocks[i] && stage.blocks[i].gameObject.activeInHierarchy && !stage.blocks[i].isKinematic)
                .Where(i => anchor.y - stage.blocks[i].position.y > 2 && anchor.y - stage.blocks[i].position.y < 6.5f)
                .OrderByDescending(i => Vector3.Dot(Vector3.ProjectOnPlane(stage.blocks[i].position - anchor, Vector3.up), side)).ToList();
            int pull = pullCandidates.Count > 0 ? pullCandidates[0] : -1;
            Vector3 pullPoint = pull >= 0 ? stage.blocks[pull].position : anchor + Vector3.down * 3;
            Vector3 pullOut = Vector3.ProjectOnPlane(pullPoint - anchor, Vector3.up);
            pullOut = pullOut.sqrMagnitude < .01f ? side : pullOut.normalized;
            set.cues.Clear();
            var pullCue = new StudioCue { at = 5.5f, action = "push", strength = 7, direction = pullOut + Vector3.up * .05f, point = stage.ToStage(pullPoint) };
            if (pull >= 0) pullCue.blocks.Add(pull);
            set.cues.Add(pullCue);
            // The collapse: aimed beside the hero, so the crew goes and the hero holds on.
            Vector3 heroWorld = stage.ToWorld(hero.position);
            Vector3 collapsePoint = stage.ToStage(heroWorld - side * 2.5f + Vector3.down * 1.5f);
            set.cues.Add(new StudioCue { at = 7.0f, action = "collapse", strength = 8, point = collapsePoint });
            // The wave: lower than the top, so it smashes the base.
            set.cues.Add(new StudioCue { at = 9.4f, action = "tsunami", heading = 225, strength = 22, height = Mathf.Max(6, towerHeight * .55f) });
            Note($"Pull block #{pull} at {pullPoint} ({anchor.y - pullPoint.y:0.0} m below the top), pushed {pullOut}. Collapse at stage {collapsePoint}. Tsunami heading 225, height {Mathf.Max(6, towerHeight * .55f):0.0} m.");
            StudioLibrary.Save(set);
            StudioRun.Refresh();
            yield return 0;

            // ---- Record the whole set once, live physics, with sound ----
            StudioCamera.Resize(640, 360);
            void Watch(int frame)
            {
                if (frame % 15 != 0) return;
                Shoot(Path.Combine(Folder, "live", $"t{frame / (float)set.fps:00.0}s.png"), anchor + Vector3.down * towerHeight * .4f, 135 + 180, 12, Mathf.Max(towerHeight * 1.6f, 20));
            }
            StudioRun.FrameShown += Watch;
            StudioRun.RecordWholeSet();
            foreach (int wait in UntilHeld()) yield return wait;
            StudioRun.FrameShown -= Watch;
            StudioRun.SaveTape();
            Note("Cue log: " + string.Join(" | ", StudioRun.CueLog));
            Note($"Recording has sound: {StudioRun.LastRecordingHasSound}. Sound file: {StudioRender.AudioFile(set.id)}");

            // Where everyone went, once a second (read from the tape).
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
            for (float t = 0; t < set.length; t += 1)
            {
                int f = Mathf.Min(Mathf.RoundToInt(t * set.fps), set.Frames - 1);
                WorldTrack.Positions(worldTake, worldTake.start, out var start, out _);
                WorldTrack.Positions(worldTake, f, out var now, out var on);
                int moved = 0, gone = 0;
                for (int i = 0; i < start.Length; i++) { if (!on[i]) gone++; else if (Vector3.Distance(start[i], now[i]) > .5f) moved++; }
                if (t % 2 == 0 || t > 5) Note($"World at {t:0}s: {moved} blocks moved more than 0.5 m, {gone} gone.");
            }

            // Who fell furthest during the collapse?
            StudioMark faller = hero; float biggest = 0;
            foreach (var mark in crew)
            {
                var a = ActorAt(tape, mark.id, 6.8f, set.fps); var b = ActorAt(tape, mark.id, 9.5f, set.fps);
                if (a.HasValue && b.HasValue && a.Value.y - b.Value.y > biggest) { biggest = a.Value.y - b.Value.y; faller = mark; }
            }
            Note($"Fall shot subject: {faller.actor}, dropped {biggest:0.0} m between 6.8 s and 9.5 s.");

            // ---- Cameras (positions are metres from the anchor) ----
            set.cameras.Clear();
            float H = Mathf.Max(towerHeight, 6), R = Mathf.Max(radius, 4);
            Vector3 up = Vector3.up;
            Vector3 heroStage = hero.position;
            void Style(StudioCameraTrack c) { c.bloom = .35f; c.vignette = .4f; c.grain = .12f; c.chroma = .08f; }

            // A. Establishing: from far over the ocean, push in toward the tower top.
            var establish = new StudioCameraTrack { name = "A Establish", color = 0, start = 0, end = 3.2f, noise = "subtle" };
            establish.keys.Add(K(0, side * (R * 3f + 22) + up * 5, new Vector3(0, -H * .45f, 0), 24));
            establish.keys.Add(K(3.2f, side * (R * 1.6f + 7) + up * 2.2f, new Vector3(0, -H * .2f, 0), 28, 3));
            // B. Hero: low angle near the hero, looking up past them.
            var heroShot = new StudioCameraTrack { name = "B Hero", color = 1, start = 2.8f, end = 5.4f, behavior = "follow", subject = hero.id, lag = .15f, noise = "walking", noiseStrength = .6f };
            heroShot.keys.Add(K(2.8f, heroStage + side * 2.6f + Vector3.Cross(up, side) * 1.0f + up * .35f, heroStage + up * 1.4f, 30));
            heroShot.keys.Add(K(5.4f, heroStage + side * 2.0f + Vector3.Cross(up, side) * .6f + up * .25f, heroStage + up * 1.4f, 34));
            // C. The pull: on the block that slides out.
            Vector3 pullStage = stage.ToStage(pullPoint);
            var pullShot = new StudioCameraTrack { name = "C Pull", color = 2, start = 4.8f, end = 7.2f, noise = "subtle" };
            pullShot.keys.Add(K(4.8f, pullStage + pullOut * 7 + Vector3.Cross(up, pullOut) * 2.5f + up * 1.2f, pullStage, 32));
            pullShot.keys.Add(K(7.2f, pullStage + pullOut * 8.5f + Vector3.Cross(up, pullOut) * 3.5f + up * 2.2f, pullStage + up * 1.5f, 28));
            // D. The collapse in slow motion, on whoever falls furthest.
            var fall = new StudioCameraTrack { name = "D Fall", color = 3, start = 6.6f, end = 9.2f, behavior = "follow", subject = faller.id, lag = .1f, noise = "impact", noiseStrength = .5f };
            fall.keys.Add(K(6.6f, heroStage + side * 9 + up * 1.5f, heroStage, 26));
            fall.keys.Add(K(9.2f, heroStage + side * 11 + up * -1.5f, heroStage + up * -3, 24));
            fall.speeds.Add(new StudioSpeedMark { time = 6.6f, speed = 1 });
            fall.speeds.Add(new StudioSpeedMark { time = 7.1f, speed = .3f });
            fall.speeds.Add(new StudioSpeedMark { time = 7.9f, speed = .3f });
            fall.speeds.Add(new StudioSpeedMark { time = 8.4f, speed = 1 });
            // E. The wave: low over the water, wide.
            var wave = new StudioCameraTrack { name = "E Wave", color = 4, start = 9.0f, end = 11.8f, noise = "walking" };
            Vector3 waveSide = Quaternion.Euler(0, -35, 0) * side;
            wave.keys.Add(K(9.0f, waveSide * (R * 2.2f + 14) + up * (-H * .75f), new Vector3(0, -H * .45f, 0), 22));
            wave.keys.Add(K(11.8f, waveSide * (R * 2.0f + 12) + up * (-H * .6f), new Vector3(0, -H * .35f, 0), 22));
            // F. Finale: crane up and away from what is left, the hero on top.
            var finale = new StudioCameraTrack { name = "F Finale", color = 5, start = 11.2f, end = 14f, noise = "subtle" };
            Vector3 finSide = Quaternion.Euler(0, 40, 0) * side;
            finale.keys.Add(K(11.2f, heroStage + finSide * 4 + up * .6f, heroStage + up * 1.2f, 30));
            finale.keys.Add(K(14f, heroStage + finSide * 12 + up * 7, heroStage + up * .5f, 24, 3));
            foreach (var c in new[] { establish, heroShot, pullShot, fall, wave, finale }) { Style(c); set.cameras.Add(c); }

            // ---- The cut: 15.0 s ----
            float fallLength = StudioCameras.OutputLength(fall, set, Fps);
            set.cuts.Clear();
            set.cuts.Add(new StudioCut { camera = establish.id, from = 0, to = 3.0f });
            set.cuts.Add(new StudioCut { camera = heroShot.id, from = .2f, to = 2.4f });
            set.cuts.Add(new StudioCut { camera = pullShot.id, from = .2f, to = 2.2f });
            set.cuts.Add(new StudioCut { camera = fall.id, from = .2f, to = Mathf.Min(3.4f, fallLength) });
            set.cuts.Add(new StudioCut { camera = wave.id, from = .2f, to = 2.6f });
            set.cuts.Add(new StudioCut { camera = finale.id, from = .4f, to = 2.6f, dissolve = .3f });
            float total = set.cuts.Sum(c => c.to - c.from);
            Note($"Cut: {set.cuts.Count} pieces, {total:0.00} s. Slow-motion shot output length {fallLength:0.00} s.");
            set.render = new StudioRenderSettings { width = Width, height = Height, fps = Fps, subFrames = SubFrames, format = "png", cinema = Cinema, freezeAmbient = false };
            StudioLibrary.Save(set);

            // ---- Render every camera ----
            string renders = Path.Combine(Folder, "renders");
            var clock = System.Diagnostics.Stopwatch.StartNew();
            StudioRender.Start(set.cameras, set.render, renders);
            foreach (int wait in UntilRendered()) yield return wait;
            Note($"Render: {StudioRender.Progress} in {clock.Elapsed.TotalSeconds:0} s. " + string.Join("; ", StudioRender.Results.Select(r => $"{r.camera}: {r.frames} frames, movie {(r.movie.Length > 0 ? Path.GetFileName(r.movie) : "none")} {r.error}")));

            // ---- Export the cut ----
            string audio = StudioRender.AudioFile(set.id);
            try
            {
                string xml = StudioExport.WriteFcpxml(set, renders, audio, out var problems);
                string edl = StudioExport.WriteEdl(set, renders, out _);
                Note($"Export: {Path.GetFileName(xml)}, {Path.GetFileName(edl)}. Exporter notes: {(problems.Count == 0 ? "none" : string.Join(" / ", problems))}");
                var pieces = StudioExport.Resolve(set, renders, out _);
                var manifest = new StringBuilder("index\tshot\tmedia\tinFrame\tframes\trecordStart\tdissolve\tfps\n");
                for (int i = 0; i < pieces.Count; i++)
                    manifest.Append($"{i}\t{pieces[i].shot.name}\t{pieces[i].media}\t{pieces[i].inFrame}\t{pieces[i].frames}\t{pieces[i].recordStart}\t{pieces[i].cut.dissolve}\t{pieces[i].shot.fps}\n");
                File.WriteAllText(Path.Combine(renders, "pieces.tsv"), manifest.ToString());
            }
            catch (Exception error) { Note("Export failed: " + error.Message); }
            if (File.Exists(audio))
            {
                Directory.CreateDirectory(Path.Combine(Folder, "audio"));
                File.Copy(audio, Path.Combine(Folder, "audio", "tape.wav"), true);
                string stems = Path.Combine(Path.GetDirectoryName(audio), "stems");
                if (Directory.Exists(stems)) foreach (var s in Directory.GetFiles(stems)) File.Copy(s, Path.Combine(Folder, "audio", Path.GetFileName(s)), true);
            }
            File.Copy(StudioLibrary.TapePath(set.id), Path.Combine(Folder, "tape.djtape"), true);
            Note("Done.");
        }
    }
}
