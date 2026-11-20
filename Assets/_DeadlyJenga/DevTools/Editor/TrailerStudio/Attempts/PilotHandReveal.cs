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
        // Real sizes on the castle map (measured in iteration 1): survivor about 0.94 m tall, hand 4 m across,
        // blocks 6 x 2 x 2 m, and the spire ends in a single block. The research numbers assume a 1.7 m person,
        // so its distances are scaled by about 0.55 here.
        const int Iteration = 3;
        const float ShotStart = 1.6f, ShotLength = 5f;      // tape seconds
        const float FlightStart = .3f, Landed = 2.7f, Yank = 3.3f;    // shot seconds
        const float PullSeconds = 1.2f;                     // huge things move slowly
        const float VerticalFov = 55f;                      // about a 23 mm lens: wide, close to the action
        const float HandDistance = 5f;                      // metres from the lens to the middle of the hand at the grab
        const float HeadHeight = .8f;                       // survivor eye height above the feet (survivor is 0.94 m tall)
        const float HeroInset = .6f;                        // how far inside the top block's edges the hero stands
        const float CalmDrop = 9f;                          // degrees the calm frame looks lower than the final frame
        const float RevealAt = 1.0f, RevealSeconds = 1.7f;  // shot seconds: the tilt up into the final frame
        const float PushFraction = .1f;                     // slow push-in toward the hero over the whole shot
        const bool PullUnderHero = false;                   // true = pull the block that holds up the hero's end

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

        public static string Folder => Path.Combine(StudioLibrary.ProjectRoot, "Logs", "ClaudeHandRevealPilot");

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
            public Vector3 pull, across, hero, camera, flightStart, blockSize, blockEnd, blockPosition, handCentre, wrist;
            public Quaternion blockRotation;
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

            // The block to pull: in the layer right under the top block (so it is next to the hero's feet).
            // Its blocks lie across the top block; the hero stands over one of them and the hand pulls the other,
            // so the hero stays up to react while the top lurches. It comes out on the sun's side.
            Vector3 LongAxis(Rigidbody b) { var s = StudioHandDirection.BlockSize(b); return Flat(b.rotation * (s.x >= s.z ? Vector3.right : Vector3.forward)); }
            var row = Layer(1);
            if (row.Count == 0) throw new InvalidOperationException("No layer under the top to pull from.");
            Vector3 topAxis = LongAxis(top);
            Vector3 axis = LongAxis(stage.blocks[row[0]]);
            var plan = new Plan();
            plan.pull = Vector3.Dot(axis, Flat(toSun)) >= 0 ? axis : -axis;
            // Which end of the top block the hero stands on: over the row block nearer that end.
            var ordered = row.OrderBy(i => Vector3.Dot(stage.blocks[i].position - top.position, topAxis)).ToList();
            int heroBlock = ordered[ordered.Count - 1], pullBlock = ordered.Count > 1 ? ordered[0] : ordered[0];
            if (PullUnderHero) pullBlock = heroBlock;
            Vector3 heroSide = Flat(Vector3.ProjectOnPlane(stage.blocks[heroBlock].position - top.position, Vector3.up) + topAxis * .001f);
            plan.block = pullBlock;
            var body = stage.blocks[pullBlock];
            plan.blockPosition = body.position; plan.blockRotation = body.rotation;
            plan.blockSize = StudioHandDirection.BlockSize(body);
            plan.across = heroSide;
            float blockLength = Mathf.Max(plan.blockSize.x, plan.blockSize.z);
            plan.pullDistance = blockLength + .6f;          // all the way out, so lifting it never drags the tower
            plan.blockEnd = body.position + plan.pull * (blockLength * .5f) + Vector3.up * (plan.blockSize.y * .5f);
            Note($"Layer under the top: {row.Count} blocks. Pull block #{pullBlock} at {body.position}, out along {plan.pull}, {plan.pullDistance:0.0} m; hero over block #{heroBlock}.");

            // Where the hand will be at the grab: the middle of its bones, by the same rule the hand's take uses.
            float handSize = StudioHandDirection.HandSize(stage);
            var grip = StudioHandDirection.GripFor(body.position, body.rotation, plan.blockSize, plan.pull, handSize);
            var closed = new DeadlyJenga.CandyPlayers.CandyHandPose { position = grip.wrist.position, rotation = grip.wrist.rotation, skeleton = DeadlyJenga.CastleBots.CastleHandBotBrain.Articulate(false, .8f) };
            var bones = StudioHandDirection.BonesFor(closed, handSize);
            plan.wrist = grip.wrist.position;
            plan.handCentre = bones.Aggregate(Vector3.zero, (s, b) => s + b.position) / bones.Length;

            // The hero: on the top block, at the point of it nearest the grab, so the giant hand lands right beside them.
            Vector3 local = Quaternion.Inverse(top.rotation) * (plan.handCentre - top.position);
            local.x = Mathf.Clamp(local.x, -size.x * .5f + HeroInset, size.x * .5f - HeroInset);
            local.z = Mathf.Clamp(local.z, -size.z * .5f + HeroInset, size.z * .5f - HeroInset);
            local.y = size.y * .5f + 2;
            if (!StudioStage.SurfaceBelow(top.position + top.rotation * local, out var feet)) feet = new Vector3(top.position.x, TopOf(top), top.position.z);
            plan.hero = feet;
            plan.camera = SolveCamera(plan, toSun);
            plan.heroYaw = Quaternion.LookRotation(Flat(plan.camera - feet)).eulerAngles.y + 25;
            // The flight starts on the hero's line to the sun, so the hand's shadow reaches the hero first.
            plan.flightStart = feet + toSun * 16;
            if (plan.flightStart.y < feet.y + 9) plan.flightStart.y = feet.y + 9;
            Note($"Hero feet {feet}. Hand at the grab: wrist {plan.wrist}, middle {plan.handCentre}. Block end {plan.blockEnd}. Camera {plan.camera} ({Vector3.Distance(plan.camera, feet):0.0} m from the hero, {plan.camera.y - feet.y:+0.0;-0.0} m). Hand flight starts at {plan.flightStart}.");
            return plan;
        }

        // The direction the final frame looks: halfway between the hero's eyes and the middle of the hand.
        static Vector3 FinalAim(Plan plan, Vector3 from) =>
            ((plan.hero + Vector3.up * HeadHeight - from).normalized + (plan.handCentre - from).normalized).normalized;

        // The camera spot, tried on a grid outside the tower on the side the block comes out of (research 03, 4.1):
        // low (below the hero, so it looks up), close enough that the hand fills most of the frame, the hero and
        // the hand both in frame, nothing in the way, the hero's face toward the camera when they look at the hand,
        // and the sun behind the camera. Lowest score wins.
        static Vector3 SolveCamera(Plan plan, Vector3 toSun)
        {
            Vector3 head = plan.hero + Vector3.up * HeadHeight;
            Vector3 side = Vector3.Cross(Vector3.up, plan.pull).normalized;
            float tanV = Mathf.Tan(VerticalFov * .5f * Mathf.Deg2Rad), tanH = tanV * 16f / 9f;
            bool Blocked(Vector3 from, Vector3 to) => Physics.Raycast(from, (to - from).normalized, Vector3.Distance(from, to) - .8f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; Vector3 bestAt = plan.blockEnd + plan.pull * 3 + side * 3; string why = "nothing fitted; fallback spot";
            int tried = 0, clear = 0;
            foreach (float along in new[] { .5f, 1.5f, 2.5f, 3.5f, 4.5f, 5.5f })
            foreach (float across in new[] { -6f, -5f, -4f, -3f, -2f, 2f, 3f, 4f, 5f, 6f })
            foreach (float up in new[] { -3f, -2.4f, -1.8f, -1.2f, -.6f, 0f })
            {
                tried++;
                Vector3 at = plan.blockEnd + plan.pull * along + side * across; at.y = plan.hero.y + up;
                if (Physics.CheckSphere(at, .5f, ~0, QueryTriggerInteraction.Ignore) || Blocked(at, head) || Blocked(at, plan.wrist)) continue;
                clear++;
                var look = Quaternion.LookRotation(FinalAim(plan, at));
                Vector3 h = Quaternion.Inverse(look) * (head - at), k = Quaternion.Inverse(look) * (plan.handCentre - at);
                if (h.z <= 0 || k.z <= 0) continue;
                // Where the two land on screen, as a fraction of the half frame (0 = centre, 1 = edge).
                float hx = h.x / h.z / tanH, hy = h.y / h.z / tanV, kx = k.x / k.z / tanH, ky = k.y / k.z / tanV;
                if (Mathf.Max(Mathf.Abs(hx), Mathf.Abs(kx)) > .7f || Mathf.Max(Mathf.Abs(hy), Mathf.Abs(ky)) > .65f) continue;
                float handDistance = Vector3.Distance(at, plan.handCentre), heroDistance = Vector3.Distance(at, head);
                float phi = Vector3.Angle(Flat(at - plan.hero), Flat(plan.handCentre - plan.hero));
                float pitch = Mathf.Asin(FinalAim(plan, at).y) * Mathf.Rad2Deg;
                bool sunBehind = Vector3.Dot(Flat(at - plan.hero), Flat(toSun)) > 0;
                float score = Mathf.Abs(handDistance - HandDistance) * 2 + Mathf.Max(0, heroDistance - 7.5f) * 2 + Mathf.Max(0, 5 - heroDistance) * 2
                    + phi / 30 + Mathf.Max(0, 8 - pitch) / 4 + (sunBehind ? 0 : 1.5f);
                if (score < best)
                {
                    best = score; bestAt = at;
                    why = $"along {along}, across {across}, {up:+0.0;-0.0} m from the hero's feet: hand {handDistance:0.0} m away, hero {heroDistance:0.0} m away, " +
                        $"face angle {phi:0} deg, looking up {pitch:0} deg, sun {(sunBehind ? "behind" : "in front of")} the camera, hero on screen ({hx:0.00},{hy:0.00}), hand ({kx:0.00},{ky:0.00}); score {score:0.0}";
                }
            }
            Note($"Camera spot: tried {tried}, {clear} clear of blocks. Chosen {why}.");
            return bestAt;
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
                carry = Vector3.up * 6 + plan.pull * 1f,
                fixedGrab = true, grabPosition = stage.ToStage(plan.blockPosition), grabRotation = plan.blockRotation
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
            Vector3 head = plan.hero + Vector3.up * HeadHeight;
            // Fixed points (never the moving block), so the camera does not chase the block when it flies off.
            Vector3 aim = FinalAim(plan, plan.camera);
            Vector3 lower = Quaternion.AngleAxis(CalmDrop, Vector3.Cross(Vector3.up, aim).normalized) * aim;
            string calm = StudioTargets.Point(stage.ToStage(plan.camera + lower * 10));
            string final = StudioTargets.Point(stage.ToStage(plan.camera + aim * 10));
            // Calm: low, locked-off, the hero up on the tower with room above where the hand will come in.
            StudioMoves.LockedOff(camera, t0, camera0, calm);
            // A slow push-in all the way through.
            StudioMoves.PushIn(camera, t0, t0 + ShotLength, stage.ToStage(head), PushFraction);
            // The reveal: as the shadow passes, tilt up into the final frame while the hand comes down into it.
            StudioMoves.TiltTo(camera, t0 + RevealAt, final, Vector3.zero, RevealSeconds);
            // The yank: held still, then a shake on the beat (rotation only, 1.5 degrees, gone in under half a second).
            StudioMoves.Shake(camera, t0 + Yank + 1f / 30, 1.5f, .45f);
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
            foreach (int wait in HandTimeline(stage, plan, camera)) yield return wait;
            string audio = StudioRender.AudioFile(set.id);
            if (File.Exists(audio)) File.Copy(audio, Path.Combine(Folder, "tape.wav"), true);
            File.WriteAllText(Path.Combine(Folder, "set.json"), JsonUtility.ToJson(set, true));
        }

        // Where the hand is drawn during the shot, and where the shot camera sees it (0..1 across and up the frame).
        static IEnumerable<int> HandTimeline(StudioStage stage, Plan plan, StudioCameraTrack shot)
        {
            var probe = new GameObject("Pilot probe camera").AddComponent<Camera>();
            probe.enabled = false;
            var line = new StringBuilder("Hand during the shot (shot time: drawn renderers, middle, where on screen):\n");
            for (float t = .5f; t <= ShotLength + .01f; t += .5f)
            {
                float tape = ShotStart + t;
                StudioRun.ShowExact(tape * stage.Fps);
                yield return 0;
                StudioRun.ShowExact(tape * stage.Fps);
                StudioCameras.Apply(probe, StudioCameras.Evaluate(shot, stage.set, stage.tape, stage.anchor, tape, tape));
                var root = StudioTargets.Object(stage, "actor:" + plan.handId);
                var drawn = root ? root.GetComponentsInChildren<Renderer>().Where(r => r.enabled && !r.forceRenderingOff).Select(r => r.GetType().Name).ToList() : new List<string>();
                if (root && StudioTargets.TryBounds(root, out var box))
                {
                    var v = probe.WorldToViewportPoint(box.center);
                    line.Append($"- {t:0.0}s: {string.Join("+", drawn)}, middle {box.center:F1}, size {box.size:F1}, screen ({v.x:0.00},{v.y:0.00}) {v.z:0.0} m ahead\n");
                }
                else line.Append($"- {t:0.0}s: nothing drawn (root {(root ? root.activeInHierarchy.ToString() : "missing")})\n");
            }
            Object.Destroy(probe.gameObject);
            Note(line.ToString());
        }

        static void MeasureSizes(StudioStage stage, Plan plan)
        {
            StudioRun.ShowExact((ShotStart + Yank) * stage.Fps);
            var heroRoot = StudioTargets.Object(stage, "actor:" + plan.heroId);
            var handRoot = StudioTargets.Object(stage, "actor:" + plan.handId);
            string Size(GameObject root) => root && StudioTargets.TryBounds(root, out var b) ? b.size.ToString("F2") : "not drawn";
            Note($"At the yank: hero drawn size {Size(heroRoot)}, hand drawn size {Size(handRoot)} (hand scale {StudioHandDirection.HandSize(stage)}).");
            Note(HandReport(stage, plan));
        }

        // Picture and the take's own sound in one MP4 (H.264 + AAC), with FFmpeg.
        static string Mux(string frames, int fps, string audio, float from, float length, string movie)
        {
            string encoder = StudioRender.FindEncoder();
            if (encoder.Length == 0) return "Mux: FFmpeg not found.";
            string inv(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
            // The directed hand is not live while recording, so the take has no sound of its own for it.
            // Its sound is laid in here from the game's own files: the flight loop under the flight,
            // the block scrape and a block hit on the yank. (The Studio has no sound cues: see the report.)
            string Clip(string name) { var guid = UnityEditor.AssetDatabase.FindAssets(name + " t:AudioClip").FirstOrDefault(); return guid == null ? "" : Path.GetFullPath(UnityEditor.AssetDatabase.GUIDToAssetPath(guid)); }
            string flight = Clip("HandFlight_01"), scrape = Clip("BlockScrape_01"), hit = Clip("BlockHit_02");
            string sound = "";
            if (File.Exists(audio))
            {
                var inputs = new StringBuilder($"-ss {inv(from)} -t {inv(length)} -i \"{audio}\" ");
                var graph = new StringBuilder("[1:a]aresample=48000,volume=1.6[take];");
                var mix = new List<string> { "[take]" };
                int n = 2;
                void Layer(string file, float at, float lasts, float gain, float fadeIn, float fadeOut, string label)
                {
                    if (file.Length == 0) return;
                    inputs.Append($"-i \"{file}\" ");
                    graph.Append($"[{n}:a]aresample=48000,aformat=channel_layouts=stereo,atrim=0:{inv(lasts)},afade=t=in:d={inv(fadeIn)},afade=t=out:st={inv(Mathf.Max(0, lasts - fadeOut))}:d={inv(fadeOut)},volume={inv(gain)},adelay={Mathf.RoundToInt(at * 1000)}|{Mathf.RoundToInt(at * 1000)}[{label}];");
                    mix.Add($"[{label}]"); n++;
                }
                Layer(flight, Mathf.Max(0, FlightStart - .35f), Landed - FlightStart + .7f, .9f, .6f, .6f, "flight");
                Layer(scrape, Yank, PullSeconds + .2f, 1.0f, .05f, .3f, "scrape");
                Layer(hit, Yank + .12f, .6f, 1.0f, .005f, .2f, "hit");
                graph.Append($"{string.Concat(mix)}amix=inputs={mix.Count}:normalize=0,alimiter=limit=0.89,apad[mixed]");
                sound = inputs + $"-filter_complex \"{graph}\" -map 0:v -map \"[mixed]\" -c:a aac -b:a 192k ";
            }
            string args = $"-hide_banner -loglevel error -nostdin -y -framerate {fps} -i \"{Path.Combine(frames, "%05d.png")}\" {sound}-c:v libx264 -preset medium -crf 17 -pix_fmt yuv420p -movflags +faststart -t {inv(length)} \"{movie}\"";
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
            Check("hand take is byte-identical across two recordings", sameHand, $"{handA.frames.Count} and {handB.frames.Count} frames");
            // The pulled block follows the cue in both takes, from the yank on.
            float blockOff = 0;
            int yankFrame = Mathf.RoundToInt((ShotStart + Yank) * stage.Fps);
            for (int f = yankFrame; f < stage.Frames; f++)
            {
                WorldTrack.Positions(worldA, f, out var pa, out _);
                WorldTrack.Positions(worldB, f, out var pb, out _);
                blockOff = Mathf.Max(blockOff, Vector3.Distance(pa[plan.block], pb[plan.block]));
            }
            Check("pulled block path matches within 2 mm across two recordings", blockOff < .002f, $"worst {blockOff * 1000:0.0} mm");
            // The rest of the world is free physics: say how far apart the two takes are, before and after the yank.
            float Worst(int from, int to, out int at)
            {
                float worst = 0; at = -1;
                for (int f = from; f < to; f++)
                {
                    WorldTrack.Positions(worldA, f, out var pa, out var oa);
                    WorldTrack.Positions(worldB, f, out var pb, out var ob);
                    for (int i = 0; i < pa.Length; i++) if (oa[i] && ob[i] && Vector3.Distance(pa[i], pb[i]) > worst) { worst = Vector3.Distance(pa[i], pb[i]); at = f; }
                }
                return worst;
            }
            float early = Worst(0, yankFrame, out int earlyAt), late = Worst(yankFrame, stage.Frames, out int lateAt);
            Check("world before the yank matches within 5 cm", early < .05f, $"worst {early * 100:0.0} cm (frame {earlyAt})");
            Note($"INFO world after the yank (free physics, not required to match): worst {late * 100:0.0} cm (frame {lateAt}); first frame over 5 mm: {WorldTrack.FirstDifference(worldA, worldB, 5)}.");
            float heroEarly = 0, heroLate = 0;
            for (int f = 0; f < Mathf.Min(heroA.frames.Count, heroB.frames.Count); f++)
            {
                var a = ActorFrame.Read(SurvivorTrack.Id, heroA.frames[f]); var b = ActorFrame.Read(SurvivorTrack.Id, heroB.frames[f]);
                if (!a.visible || !b.visible) continue;
                float d = Vector3.Distance(a.body.feet, b.body.feet);
                if (heroA.start + f < yankFrame) heroEarly = Mathf.Max(heroEarly, d); else heroLate = Mathf.Max(heroLate, d);
            }
            Check("hero before the yank matches within 5 cm", heroEarly < .05f, $"worst {heroEarly * 100:0.0} cm; after the yank {heroLate * 100:0.0} cm (free physics)");
            WorldTrack.Positions(worldB, Mathf.RoundToInt((ShotStart + Yank + PullSeconds) * stage.Fps), out var after, out _);
            WorldTrack.Positions(worldB, 0, out var before, out _);
            float slid = Vector3.Distance(after[plan.block], before[plan.block]);
            Check("the cue pulls the chosen block out", slid > plan.pullDistance * .8f, $"{slid:0.00} m of {plan.pullDistance:0.00} m");

            // 3. The hand is drawn: a still with and without the hand copy differs where the hand is.
            float grab = ShotStart + Landed + .2f;
            StudioRun.ShowExact(grab * stage.Fps);
            yield return 0; yield return 0;          // skinned meshes update in the game loop
            StudioRun.ShowExact(grab * stage.Fps);
            Note(HandReport(stage, plan));
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
                StudioRun.ShowExact(grab * stage.Fps);
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

        // Everything drawn for the hand copy, for finding out why a hand is or is not seen.
        static string HandReport(StudioStage stage, Plan plan)
        {
            var root = StudioTargets.Object(stage, "actor:" + plan.handId);
            if (!root) return "Hand report: no playback object.";
            var line = new StringBuilder($"Hand report: root active {root.activeInHierarchy} at {root.transform.position}.\n");
            foreach (var hand in root.GetComponentsInChildren<DeadlyJenga.CandyPhysicsHands.CandyPhysicalHand>(true))
                line.Append($"- physical hand: size {hand.GeometrySize}, palm {(hand.bodies != null && hand.bodies.Length > 0 ? hand.bodies[0].position.ToString("F2") : "none")}, " +
                    $"skin {(hand.skin ? $"enabled {hand.skin.enabled}, bones {hand.skin.bones?.Length}, first bone {(hand.skin.bones != null && hand.skin.bones.Length > 0 && hand.skin.bones[0] ? hand.skin.bones[0].position.ToString("F2") : "none")}, root bone {(hand.skin.rootBone ? hand.skin.rootBone.name : "none")}, mesh {(hand.skin.sharedMesh ? hand.skin.sharedMesh.vertexCount.ToString() : "none")}" : "none")}\n");
            foreach (var shown in root.GetComponentsInChildren<Renderer>(true).Take(12))
                line.Append($"- {shown.GetType().Name} '{shown.name}' enabled {shown.enabled} forceOff {shown.forceRenderingOff} active {shown.gameObject.activeInHierarchy} layer {shown.gameObject.layer} " +
                    (shown is SkinnedMeshRenderer sk ? $"offscreen {sk.updateWhenOffscreen} local {sk.localBounds.center:F1}/{sk.localBounds.size:F1} " : "") +
                    (shown.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh ? $"mesh {mf.sharedMesh.vertexCount} v local {mf.sharedMesh.bounds.center:F1}/{mf.sharedMesh.bounds.size:F1} " : "") + $"bounds {shown.bounds.center:F1}/{shown.bounds.size:F1} " +
                    $"shadows {shown.shadowCastingMode} material {(shown.sharedMaterial ? shown.sharedMaterial.name + " / " + shown.sharedMaterial.shader.name + " q" + shown.sharedMaterial.renderQueue : "none")}\n");
            return line.ToString();
        }

        static byte[] Encode(Color32[] pixels)
        {
            var image = new Texture2D(320, 180, TextureFormat.RGB24, false);
            image.SetPixels32(pixels); image.Apply();
            var bytes = image.EncodeToPNG(); Object.Destroy(image); return bytes;
        }
    }
}
