using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // One 5-second shot, "the hand reveal", made with the Studio's directed hand, camera targets and moves.
    //   ...PilotHandReveal.RunPreview   record the take, render 480x270 at 15 fps, filmstrip and metrics
    //   ...PilotHandReveal.RunFinal     record the take, render 1920x1080 at 30 fps, mux the take's sound into an MP4
    //   ...PilotHandReveal.RunChecks    acceptance checks (deterministic cue, hand drawn, metrics written)
    // Everything goes to Logs/PilotHandReveal/.
    [InitializeOnLoad]
    public static class PilotHandReveal
    {
        // ---- The shot plan: what an iteration changes ----
        const int Iteration = 1;
        const float ShotStart = 1.6f, ShotLength = 5f;      // tape seconds
        const float FlightStart = .55f, Landed = 2.5f, Yank = 3.0f;   // shot seconds
        const float PullSeconds = .9f;
        const float CameraDistance = 4.5f, CameraHeight = 1.2f, CameraAzimuth = 40f;   // from the hero, degrees off the pull line
        const float VerticalFov = 50f;
        const float RevealAt = 1.3f, RevealSeconds = 1.7f;  // shot seconds: the tilt up
        const float HeroAlong = .35f, HeroAcross = .0f;     // where the hero stands on the top (fractions of half-size)
        const int Side = 1;                                 // which side of the pull line the camera is on

        const string Key = "DeadlyJenga.PilotHandReveal";
        const string SetId = "pilot-hand-reveal";
        static int Step { get => SessionState.GetInt(Key, 0); set => SessionState.SetInt(Key, value); }
        static string Mode { get => SessionState.GetString(Key + ".Mode", "preview"); set => SessionState.SetString(Key + ".Mode", value); }
        static double Deadline { get => SessionState.GetFloat(Key + ".Deadline", 0); set => SessionState.SetFloat(Key + ".Deadline", (float)value); }
        static string Failure { get => SessionState.GetString(Key + ".Failure", ""); set => SessionState.SetString(Key + ".Failure", value); }
        static int Fails { get => SessionState.GetInt(Key + ".Fails", 0); set => SessionState.SetInt(Key + ".Fails", value); }

        static IEnumerator<int> routine;
        static int lastTick = -1;
        static readonly StringBuilder notes = new StringBuilder();

        public static string Folder => Path.Combine(StudioLibrary.ProjectRoot, "Logs", "PilotHandReveal");

        static PilotHandReveal() { if (Step != 0) Hook(); }
        static void Hook() { EditorApplication.update -= Tick; EditorApplication.update += Tick; }

        public static void RunPreview() => Begin("preview");
        public static void RunFinal() => Begin("final");
        public static void RunChecks() => Begin("checks");

        static void Begin(string mode)
        {
            if (Directory.Exists(Folder)) Directory.Delete(Folder, true);
            Directory.CreateDirectory(Folder);
            notes.Clear();
            string tapes = StudioLibrary.TapeFolder(SetId);
            if (Directory.Exists(tapes)) Directory.Delete(tapes, true);
            Mode = mode; Failure = ""; Fails = 0; Step = 1;
            Deadline = EditorApplication.timeSinceStartup + 240;
            Hook();
        }

        static void Note(string line)
        {
            notes.AppendLine(line);
            Debug.Log("[PilotHandReveal] " + line);
            try { File.WriteAllText(Path.Combine(Folder, "notes.md"), notes.ToString()); } catch { }
        }

        static void Check(string name, bool passed, string detail)
        {
            if (!passed) Fails++;
            Note($"{(passed ? "PASS" : "FAIL")} {name}: {detail}");
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
                        StudioRun.OpenStage(NewSet());
                        Step = 2; Deadline = EditorApplication.timeSinceStartup + 570;
                        break;
                    case 2:
                        if (routine == null)
                        {
                            if (StudioRun.State == StudioState.Held) routine = Mode == "checks" ? Checks() : Shoot(Mode == "final");
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
                Note("FAIL stopped at step " + Step + ": " + error.Message);
                routine = null;
                if (StudioRun.State != StudioState.Closed && Step < 3) { Step = 3; StudioRun.CloseStage(); }
                else Finish();
            }
        }

        static void Finish()
        {
            bool failed = Failure.Length > 0 || Fails > 0;
            Step = 0;
            StudioLibrary.SetsFolderOverride = "";
            EditorApplication.update -= Tick;
            Note(failed ? $"FAIL pilot {Mode}: {Failure} ({Fails} failed checks)" : "PASS pilot " + Mode + " finished, output in " + Folder);
            if (SessionState.GetString("DJWarm.id", "") != "")
            {
                Directory.CreateDirectory(Path.Combine("Logs", "Warm"));
                File.WriteAllText(Path.Combine("Logs", "Warm", "exit.txt"), failed ? "1" : "0");
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
                return;
            }
            bool startedForThis = Environment.GetCommandLineArgs().Any(a => a.Contains("PilotHandReveal.Run"));
            if (Application.isBatchMode || startedForThis) EditorApplication.Exit(failed ? 1 : 0);
        }

        static StudioSet NewSet()
        {
            var set = new StudioSet
            {
                id = SetId, title = "Pilot: the hand reveal", map = "castle", fps = 30, length = ShotStart + ShotLength + .4f, seed = 2026,
                handBot = false, notes = "Made by PilotHandReveal. Safe to delete."
            };
            StudioLibrary.Save(set);
            return set;
        }

        // ---- Waiting ----

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

        // ---- The plan: worked out from the map, so nothing is placed by hand ----

        sealed class Plan
        {
            public int block;
            public Vector3 pull, across, hero, camera, flightStart, blockSize;
            public float pullDistance, heroYaw;
            public string heroId, handId;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0; return v.sqrMagnitude < 1e-6f ? Vector3.forward : v.normalized; }

        static float TopOf(Rigidbody body) => body.GetComponentsInChildren<Collider>().Where(c => c.enabled && !c.isTrigger).Select(c => c.bounds.max.y).DefaultIfEmpty(body.position.y).Max();

        static Plan MakePlan(StudioStage stage)
        {
            var blocks = Enumerable.Range(0, stage.blocks.Length).Where(i => stage.blocks[i] && stage.blocks[i].gameObject.activeInHierarchy).ToList();
            int topIndex = blocks.OrderByDescending(i => stage.blocks[i].position.y).First();
            var top = stage.blocks[topIndex];
            Vector3 size = StudioHandDirection.BlockSize(top);
            float layer = top.GetComponentsInChildren<Collider>().Where(c => c.enabled && !c.isTrigger).Select(c => c.bounds.size.y).DefaultIfEmpty(size.y).Min();
            Note($"Top block #{topIndex} at {top.position}, size {size}, layer height {layer:0.00} m, top surface y {TopOf(top):0.00}.");
            List<int> Layer(int down) => blocks.Where(i => Mathf.Abs(stage.blocks[i].position.y - (top.position.y - down * layer)) < layer * .4f)
                .Where(i => Vector3.ProjectOnPlane(stage.blocks[i].position - top.position, Vector3.up).magnitude < 8).ToList();
            var topLayer = Layer(0);
            Vector3 centre = topLayer.Aggregate(Vector3.zero, (s, i) => s + stage.blocks[i].position) / topLayer.Count;
            Note($"Top layer: {topLayer.Count} blocks around {centre}. Layers below: {Layer(1).Count}, {Layer(2).Count}.");

            var sun = RenderSettings.sun ? RenderSettings.sun : Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.type == LightType.Directional).OrderByDescending(l => l.intensity).FirstOrDefault();
            Vector3 toSun = sun ? -sun.transform.forward : new Vector3(-.4f, .8f, -.4f).normalized;
            Note($"Sun: {(sun ? sun.name : "none")}, direction to the sun {toSun} (elevation {Mathf.Asin(toSun.y) * Mathf.Rad2Deg:0} deg).");

            // The layer to pull from: one or two below the top, the one whose blocks point most toward the sun
            // (the camera has the sun behind it; the block comes out toward the camera's side).
            Vector3 LongAxis(Rigidbody b) { var s = StudioHandDirection.BlockSize(b); return Flat(b.rotation * (s.x >= s.z ? Vector3.right : Vector3.forward)); }
            int pick = -1; Vector3 axis = Vector3.forward; float best = -1;
            foreach (int down in new[] { 1, 2 })
            {
                var row = Layer(down);
                if (row.Count == 0) continue;
                var a = LongAxis(stage.blocks[row[0]]);
                float score = Mathf.Abs(Vector3.Dot(a, Flat(toSun))) + (down == 1 ? .15f : 0);
                if (score <= best) continue;
                best = score; axis = a;
                Vector3 pullDir = Vector3.Dot(a, Flat(toSun)) >= 0 ? a : -a;
                Vector3 across = Vector3.Cross(Vector3.up, pullDir).normalized * Side;
                // An edge block on the camera's side, so it is in plain view.
                pick = row.OrderByDescending(i => Vector3.Dot(stage.blocks[i].position - centre, across)).First();
            }
            if (pick < 0) throw new InvalidOperationException("No layer under the top to pull from.");
            var plan = new Plan { block = pick };
            var body = stage.blocks[pick];
            plan.pull = Vector3.Dot(axis, Flat(toSun)) >= 0 ? axis : -axis;
            plan.across = Vector3.Cross(Vector3.up, plan.pull).normalized * Side;
            plan.blockSize = StudioHandDirection.BlockSize(body);
            float blockLength = Mathf.Max(plan.blockSize.x, plan.blockSize.z);
            plan.pullDistance = blockLength * .9f + .4f;
            Note($"Pull block #{pick} at {body.position} ({top.position.y - body.position.y:0.00} m below the top block), out along {plan.pull}, {plan.pullDistance:0.0} m. Block size {plan.blockSize}.");

            // The hero: on the top, toward the pull side and the camera side.
            float halfTop = topLayer.Max(i => Vector3.ProjectOnPlane(stage.blocks[i].position - centre, Vector3.up).magnitude) + Mathf.Min(plan.blockSize.x, plan.blockSize.z) * .5f;
            Vector3 wanted = centre + plan.pull * (HeroAlong * halfTop) + plan.across * (HeroAcross * halfTop) + Vector3.up * 2;
            if (!StudioStage.SurfaceBelow(wanted, out var feet)) feet = new Vector3(centre.x, TopOf(top), centre.z);
            plan.hero = feet;
            plan.camera = feet + (Quaternion.AngleAxis(-CameraAzimuth * Side, Vector3.up) * plan.pull) * CameraDistance + Vector3.up * CameraHeight;
            plan.heroYaw = Quaternion.LookRotation(Flat(plan.camera - feet)).eulerAngles.y + 28 * Side;
            // The flight starts on the hero's line to the sun, so the hand's shadow is on the hero first.
            plan.flightStart = feet + toSun * 16;
            if (plan.flightStart.y < feet.y + 9) plan.flightStart.y = feet.y + 9;
            Note($"Hero feet {feet} (top half-size {halfTop:0.0} m). Camera {plan.camera} ({CameraDistance} m, +{CameraHeight} m, {CameraAzimuth} deg). Hand flight starts at {plan.flightStart}.");
            return plan;
        }

        // ---- The Set: marks, the hand cue, the hero's performance layer ----

        static Plan Direct(StudioStage stage)
        {
            var set = stage.set;
            var plan = MakePlan(stage);
            set.marks.Clear();
            var hero = set.AddMark(StudioRole.Survivor, stage.ToStage(plan.hero));
            hero.actor = "Hero"; hero.yaw = plan.heroYaw; hero.color = 2;
            var hand = set.AddMark(StudioRole.Hand, stage.ToStage(plan.flightStart));
            hand.actor = "Hand";
            plan.heroId = hero.id; plan.handId = hand.id;

            set.cues.Clear();
            var pull = new StudioCue
            {
                id = "handpull1", action = HandPullCue.Id, actor = hand.id, at = ShotStart + Yank,
                duration = Landed - FlightStart, grip = Yank - Landed, pullTime = PullSeconds,
                point = stage.ToStage(plan.flightStart), height = 2.5f, direction = plan.pull, strength = plan.pullDistance,
                carry = Vector3.up * 5 + plan.pull * 1.5f
            };
            pull.blocks.Add(plan.block);
            set.cues.Add(pull);

            set.overrides.Clear();
            set.overrides.Add(new StudioOverride { actor = hero.id, kind = "lookAt", target = "actor:" + hand.id, from = ShotStart + 1.1f, to = ShotStart + ShotLength, blendIn = .45f, blendOut = .1f });
            set.overrides.Add(new StudioOverride { actor = hero.id, kind = "reaction", name = "cower", from = ShotStart + Yank + .1f, to = ShotStart + Yank + 1.7f });
            StudioLibrary.Save(set);
            StudioRun.Refresh();
            return plan;
        }

        static IEnumerable<int> RecordTake(Plan plan)
        {
            var clock = Stopwatch.StartNew();
            StudioRun.RecordWholeSet();
            foreach (int wait in UntilHeld()) yield return wait;
            StudioRun.SaveTape();
            Note($"Recorded in {clock.Elapsed.TotalSeconds:0} s. {StudioRun.Status}");
            Note("Cue log: " + string.Join(" | ", StudioRun.CueLog));
        }

        // What happened in the take, from the tape: the pulled block, the hero, the rest of the tower.
        static void Describe(StudioStage stage, Plan plan)
        {
            var world = stage.tape.FindType(WorldTrack.Id).ActiveTake;
            WorldTrack.Positions(world, world.start, out var start, out _);
            var line = new StringBuilder("Take, every half second (block offset from start, hero feet from start, blocks moved > 5 cm):\n");
            var heroStart = StudioActorPoses.At(stage.tape, plan.heroId, ShotStart, stage.Fps);
            for (float t = ShotStart; t <= ShotStart + ShotLength + .01f; t += .5f)
            {
                WorldTrack.Positions(world, Mathf.Min(Mathf.RoundToInt(t * stage.Fps), stage.Frames - 1), out var now, out var on);
                int moved = 0;
                for (int i = 0; i < now.Length; i++) if (i != plan.block && on[i] && Vector3.Distance(now[i], start[i]) > .05f) moved++;
                var hero = StudioActorPoses.At(stage.tape, plan.heroId, t, stage.Fps);
                line.Append($"- {t - ShotStart:0.0}s: block {(now[plan.block] - start[plan.block]).magnitude:0.00} m, hero {(hero.found && heroStart.found ? (hero.feet - heroStart.feet).ToString("F2") : "gone")}, other blocks moved {moved}\n");
            }
            Note(line.ToString());
        }

        // ---- The camera ----

        static StudioCameraTrack BuildCamera(StudioStage stage, Plan plan)
        {
            float t0 = ShotStart;
            var camera = StudioMoves.Camera("Hand reveal", t0, t0 + ShotLength, 20);
            camera.id = "handreveal";
            camera.keys[0].focal = StudioMoves.FocalForVertical(camera, VerticalFov);
            string hero = "actor:" + plan.heroId, block = "block:" + plan.block;
            Vector3 camera0 = stage.ToStage(plan.camera);
            // Calm: locked-off, low, the hero in the lower part of the frame and sky above.
            StudioMoves.LockedOff(camera, t0, camera0, hero, new Vector3(0, 1.9f, 0));
            // A slow push-in all the way through (about 15 % of the distance).
            StudioMoves.PushIn(camera, t0, t0 + ShotLength, stage.ToStage(plan.hero + Vector3.up * 1.2f), .15f);
            // The reveal: tilt up to where the hand lands, above the block next to the hero.
            StudioMoves.TiltTo(camera, t0 + RevealAt, block, Vector3.up * 3.2f - plan.pull * .5f, RevealSeconds);
            // The yank: hold, then shake on the beat.
            StudioMoves.Shake(camera, t0 + Yank + .05f, 1.6f, .55f);
            camera.measure.Add(hero); camera.measure.Add("actor:" + plan.handId); camera.measure.Add(block);
            camera.bloom = .3f; camera.vignette = .35f; camera.grain = .08f;
            return camera;
        }

        // ---- Preview and final ----

        static IEnumerator<int> Shoot(bool final)
        {
            var stage = StudioRun.Stage;
            Note($"# Pilot hand reveal, {(final ? "final" : "preview")} run, iteration {Iteration}\n");
            var plan = Direct(stage);
            yield return 0;
            foreach (int wait in RecordTake(plan)) yield return wait;
            Describe(stage, plan);
            MeasureSizes(stage, plan);

            var set = stage.set;
            var camera = BuildCamera(stage, plan);
            if (final) camera.motionBlur = .35f;
            set.cameras.Clear(); set.cameras.Add(camera);
            set.render = final
                ? new StudioRenderSettings { width = 1920, height = 1080, fps = 30, subFrames = 1, format = "png", cinema = true, filmstrip = 12 }
                : new StudioRenderSettings { width = 480, height = 270, fps = 15, subFrames = 1, format = "png", cinema = false, filmstrip = 12 };
            StudioLibrary.Save(set);
            string renders = Path.Combine(Folder, "renders");
            var clock = Stopwatch.StartNew();
            StudioRender.Start(set.cameras, set.render, renders);
            foreach (int wait in UntilRendered()) yield return wait;
            var result = StudioRender.Results.FirstOrDefault();
            Note($"Render {set.render.width}x{set.render.height} at {set.render.fps} fps: {StudioRender.Progress} in {clock.Elapsed.TotalSeconds:0} s. " +
                (result != null ? $"{result.frames} frames {result.error}" : "no result"));
            if (result != null)
            {
                string movie = Path.Combine(Folder, final ? "hand-reveal.mp4" : "preview.mp4");
                Note(Mux(result.folder, set.render.fps, StudioRender.AudioFile(set.id), ShotStart, ShotLength, movie));
                foreach (string name in new[] { "metrics.csv", "metrics.json", "filmstrip.png", "filmstrip.txt" })
                    if (File.Exists(Path.Combine(result.folder, name))) File.Copy(Path.Combine(result.folder, name), Path.Combine(Folder, name), true);
            }
            string audio = StudioRender.AudioFile(set.id);
            if (File.Exists(audio)) File.Copy(audio, Path.Combine(Folder, "tape.wav"), true);
            File.WriteAllText(Path.Combine(Folder, "set.json"), JsonUtility.ToJson(set, true));
        }

        static void MeasureSizes(StudioStage stage, Plan plan)
        {
            StudioRun.ShowExact((ShotStart + Yank) * stage.Fps);
            var heroRoot = StudioTargets.Object(stage, "actor:" + plan.heroId);
            var handRoot = StudioTargets.Object(stage, "actor:" + plan.handId);
            string Size(GameObject root) => root && StudioTargets.TryBounds(root, out var b) ? b.size.ToString("F2") : "not drawn";
            Note($"At the yank: hero drawn size {Size(heroRoot)}, hand drawn size {Size(handRoot)} (hand scale {StudioHandDirection.HandSize(stage)}).");
        }

        // Picture and the take's own sound in one MP4 (H.264 + AAC), with FFmpeg.
        static string Mux(string frames, int fps, string audio, float from, float length, string movie)
        {
            string encoder = StudioRender.FindEncoder();
            if (encoder.Length == 0) return "Mux: FFmpeg not found.";
            string inv(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
            string sound = File.Exists(audio) ? $"-ss {inv(from)} -t {inv(length)} -i \"{audio}\" -c:a aac -b:a 192k " : "";
            string args = $"-hide_banner -loglevel error -nostdin -y -framerate {fps} -i \"{Path.Combine(frames, "%05d.png")}\" {sound}-c:v libx264 -preset medium -crf 17 -pix_fmt yuv420p -movflags +faststart -shortest \"{movie}\"";
            try
            {
                using (var process = Process.Start(new ProcessStartInfo(encoder, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true }))
                {
                    string errors = process.StandardError.ReadToEnd();
                    process.WaitForExit(120000);
                    return process.ExitCode == 0 && File.Exists(movie) ? $"Mux: {Path.GetFileName(movie)} written ({(sound.Length > 0 ? "with sound" : "NO SOUND FILE")})." : "Mux failed: " + errors;
                }
            }
            catch (Exception error) { return "Mux failed: " + error.Message; }
        }

        // ---- Acceptance checks ----

        static IEnumerator<int> Checks()
        {
            var stage = StudioRun.Stage;
            Note("# Pilot acceptance checks\n");

            // 1. A hand frame without bones gets bones (the attempt-1 hand was invisible without them).
            var empty = new ActorFrame { isHand = true, visible = true, size = 20, target = new DeadlyJenga.CandyPlayers.CandyHandPose { position = stage.anchor + Vector3.up * 5, rotation = Quaternion.identity, skeleton = DeadlyJenga.CandyPlayers.CandyHandSkeleton.Rest(false) } };
            StudioHandDirection.FillBones(empty);
            Check("hand frame without bones is filled", empty.bones.Length == 16, $"{empty.bones.Length} bones");

            // 2. The same cues give the same take.
            var plan = Direct(stage);
            yield return 0;
            foreach (int wait in RecordTake(plan)) yield return wait;
            var handA = stage.tape.Find(plan.handId).ActiveTake;
            var worldA = stage.tape.FindType(WorldTrack.Id).ActiveTake;
            var heroA = stage.tape.Find(plan.heroId).ActiveTake;
            foreach (int wait in RecordTake(plan)) yield return wait;
            var handB = stage.tape.Find(plan.handId).ActiveTake;
            var worldB = stage.tape.FindType(WorldTrack.Id).ActiveTake;
            var heroB = stage.tape.Find(plan.heroId).ActiveTake;
            bool sameHand = handA != handB && handA.frames.Count == handB.frames.Count && handA.frames.Zip(handB.frames, (a, b) => a.SequenceEqual(b)).All(x => x);
            Check("hand take is identical across two recordings", sameHand, $"{handA.frames.Count} and {handB.frames.Count} frames");
            int differ = WorldTrack.FirstDifference(worldA, worldB, 5);
            Check("world take matches within 5 mm across two recordings", differ < 0, differ < 0 ? "never differs" : $"first differs at frame {differ} ({differ / (float)stage.Fps:0.00} s)");
            float heroOff = 0;
            for (int f = 0; f < Mathf.Min(heroA.frames.Count, heroB.frames.Count); f++)
            {
                var a = ActorFrame.Read(SurvivorTrack.Id, heroA.frames[f]); var b = ActorFrame.Read(SurvivorTrack.Id, heroB.frames[f]);
                if (a.visible && b.visible) heroOff = Mathf.Max(heroOff, Vector3.Distance(a.body.feet, b.body.feet));
            }
            Check("hero matches within 5 cm across two recordings", heroOff < .05f, $"worst {heroOff * 100:0.0} cm");
            WorldTrack.Positions(worldB, Mathf.RoundToInt((ShotStart + Yank + PullSeconds) * stage.Fps), out var after, out _);
            WorldTrack.Positions(worldB, 0, out var before, out _);
            float slid = Vector3.Distance(after[plan.block], before[plan.block]);
            Check("the cue pulls the chosen block out", slid > plan.pullDistance * .8f, $"{slid:0.00} m of {plan.pullDistance:0.00} m");

            // 3. The hand is drawn: a still with and without the hand copy differs where the hand is.
            float grab = ShotStart + Landed + .2f;
            StudioRun.ShowExact(grab * stage.Fps);
            var cam = new GameObject("Pilot check camera").AddComponent<Camera>();
            cam.enabled = false; cam.fieldOfView = 60;
            var handRoot = StudioTargets.Object(stage, "actor:" + plan.handId);
            Vector3 handCentre = stage.anchor;
            if (handRoot && StudioTargets.TryBounds(handRoot, out var handBox)) handCentre = handBox.center;
            cam.transform.position = handCentre + (plan.camera - plan.hero).normalized * 12 + Vector3.up * 2;
            cam.transform.LookAt(handCentre);
            var target = new RenderTexture(320, 180, 24); cam.targetTexture = target;
            Color32[] Snap()
            {
                cam.Render();
                var read = new Texture2D(320, 180, TextureFormat.RGB24, false);
                RenderTexture.active = target; read.ReadPixels(new Rect(0, 0, 320, 180), 0, 0); RenderTexture.active = null;
                var pixels = read.GetPixels32(); Object.Destroy(read); return pixels;
            }
            var with = Snap();
            int changed = 0;
            if (handRoot)
            {
                handRoot.SetActive(false);
                var without = Snap();
                handRoot.SetActive(true);
                for (int i = 0; i < with.Length; i++) if (Mathf.Abs(with[i].r - without[i].r) + Mathf.Abs(with[i].g - without[i].g) + Mathf.Abs(with[i].b - without[i].b) > 30) changed++;
            }
            File.WriteAllBytes(Path.Combine(Folder, "hand-check.png"), Encode(with));
            Check("hand is drawn in a render", changed > with.Length * .02f, $"{changed * 100f / with.Length:0.0}% of pixels change when the hand is hidden");
            var sample = StudioFraming.MeasureOne(cam, "actor:" + plan.handId, grab);
            Check("metrics see the hand", sample.onScreen && sample.area > .02f, $"area {sample.area:0.000}, at {sample.x:0.00},{sample.y:0.00}, occluded {sample.occluded:0.00}");
            cam.targetTexture = null; Object.Destroy(target); Object.Destroy(cam.gameObject);

            // 4. A tiny render writes metrics and a filmstrip next to the frames.
            var set = stage.set;
            var camera = BuildCamera(stage, plan);
            camera.end = camera.start + 1;
            set.cameras.Clear(); set.cameras.Add(camera);
            set.render = new StudioRenderSettings { width = 256, height = 144, fps = 10, cinema = false, filmstrip = 4 };
            StudioRender.Start(set.cameras, set.render, Path.Combine(Folder, "renders"));
            foreach (int wait in UntilRendered()) yield return wait;
            var result = StudioRender.Results.FirstOrDefault();
            string csv = result != null ? Path.Combine(result.folder, "metrics.csv") : "";
            int rows = File.Exists(csv) ? File.ReadAllLines(csv).Length - 1 : -1;
            Check("metrics file is written, one row per frame", result != null && rows == result.frames, $"{rows} rows for {result?.frames} frames");
            Check("filmstrip is written", result != null && File.Exists(Path.Combine(result.folder, "filmstrip.png")), result?.folder ?? "no render");
        }

        static byte[] Encode(Color32[] pixels)
        {
            var image = new Texture2D(320, 180, TextureFormat.RGB24, false);
            image.SetPixels32(pixels); image.Apply();
            var bytes = image.EncodeToPNG(); Object.Destroy(image); return bytes;
        }
    }
}
