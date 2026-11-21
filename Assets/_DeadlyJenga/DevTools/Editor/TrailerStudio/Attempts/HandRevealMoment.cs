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
    // "Hand reveal 2": a six-shot, 8.8-second hand reveal cut from one directed take.
    // The giant hand lifts the summit block off the castle spire with the hero still standing on it.
    //   ...HandRevealMoment.RunScout     record, render every shot at 640x360, plus hand-design comparison stills
    //   ...HandRevealMoment.RunPreview   record, render every shot at 640x360
    //   ...HandRevealMoment.RunFinal     record, render every shot at 1920x1080 with motion blur
    // Each shot is rendered with a few frames of handle on each side; the cut, sound and music are
    // assembled outside Unity (Docs/Development/TrailerStudio/attempts/hand-reveal-2/tools/assemble.py).
    // Everything goes to Logs/ClaudeHandReveal2/.
    [InitializeOnLoad]
    public static class HandRevealMoment
    {
        // ---- The plan (trailer frames at 30 fps; see plan.yaml in the attempt folder) ----
        const int Version = 1;
        const float TapeAtZero = 1.6f;          // tape second shown at trailer frame 0
        const int Fps = 30, LengthFrames = 264, Handle = 3;
        const int YankFrame = 150;              // the pull, on the music's main hit
        const int ShadowFrame = 28;             // the hand's shadow reaches the hero (on the music accent at frame 28)
        const float PullDistance = 2.5f, PullSeconds = .9f, GripSeconds = 1.1f, FlightSeconds = 3.6f;
        const float CarryUp = 5f, CarryOut = 1.5f, CarrySeconds = 2.4f;
        // Metres from the summit block's inner end to the hero's feet: the block's middle. The game carries a standing
        // survivor by the block's position only, so off-centre he sinks into the block when it tilts (r1: 0.36 m at 0.8 m).
        const float HeroInset = 3.0f;
        const int HandDesign = 3;               // CandyHandDesigns: 3 = Lava (the scout's design stills: it reads most as a hand)
        const int HeroColour = 6;               // renders as a strong yellow on this map (scout)
        const float Exposure = 0, Contrast = 12, Saturation = 10;
        static readonly int[] DesignsToCompare = { 0, 8, 3 };

        sealed class Shot
        {
            public string id, what;
            public int cutIn, cutOut, key, shift;   // shift = tape frames added (overlapping action), normally 0
            public int Length => cutOut - cutIn;
        }

        static readonly Shot[] Shots =
        {
            new Shot { id = "S01", what = "effect: the hand's shadow covers the hero", cutIn = 0, cutOut = 40, key = 28 },
            new Shot { id = "S02", what = "reaction: the hero looks up", cutIn = 40, cutOut = 74, key = 12 },
            new Shot { id = "S03", what = "whole: the hand settles on the summit beside the hero", cutIn = 74, cutOut = 135, key = 42 },
            new Shot { id = "S04", what = "payoff: the yank, the summit and the hero go with it", cutIn = 135, cutOut = 195, key = 15 },
            new Shot { id = "S05", what = "consequence: the hero rides the stolen block up", cutIn = 195, cutOut = 219, key = 9 },
            new Shot { id = "S06", what = "final image: hand, block, hero and the spire together", cutIn = 219, cutOut = 264, key = 30 },
        };

        static float Tape(int trailerFrame) => TapeAtZero + trailerFrame / (float)Fps;

        // ---- Running ----
        const string Key = "DeadlyJenga.HandRevealMoment";
        const string SetId = "hand-reveal-2";
        static int Step { get => SessionState.GetInt(Key, 0); set => SessionState.SetInt(Key, value); }
        static string Mode { get => SessionState.GetString(Key + ".Mode", "preview"); set => SessionState.SetString(Key + ".Mode", value); }
        static double Deadline { get => SessionState.GetFloat(Key + ".Deadline", 0); set => SessionState.SetFloat(Key + ".Deadline", (float)value); }
        static string Failure { get => SessionState.GetString(Key + ".Failure", ""); set => SessionState.SetString(Key + ".Failure", value); }

        static IEnumerator<int> routine;
        static int lastTick = -1;
        static readonly StringBuilder notes = new StringBuilder();

        public static string Folder => Path.Combine(StudioLibrary.ProjectRoot, "Logs", "ClaudeHandReveal2");

        static HandRevealMoment() { if (Step != 0) Hook(); }
        static void Hook() { EditorApplication.update -= Tick; EditorApplication.update += Tick; }

        public static void RunScout() => Begin("scout");
        public static void RunPreview() => Begin("preview");
        public static void RunFinal() => Begin("final");
        // The 1080p render in two halves, so each job stays well inside the coordinator's 10 minutes on a busy PC.
        // The directed hand and the summit block repeat exactly between takes; the rest of the world within about 5 cm.
        public static void RunFinalA() => Begin("finalA");
        public static void RunFinalB() => Begin("finalB");

        static void Begin(string mode)
        {
            if (Directory.Exists(Folder)) Directory.Delete(Folder, true);
            Directory.CreateDirectory(Folder);
            notes.Clear();
            string tapes = StudioLibrary.TapeFolder(SetId);
            if (Directory.Exists(tapes)) Directory.Delete(tapes, true);
            Mode = mode; Failure = ""; Step = 1;
            Deadline = EditorApplication.timeSinceStartup + 240;
            Hook();
        }

        static void Note(string line)
        {
            notes.AppendLine(line);
            Debug.Log("[HandRevealMoment] " + line);
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
                        StudioRun.OpenStage(NewSet());
                        Step = 2; Deadline = EditorApplication.timeSinceStartup + 570;
                        break;
                    case 2:
                        if (routine == null)
                        {
                            if (StudioRun.State == StudioState.Held) routine = Shoot(Mode);
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
            bool failed = Failure.Length > 0;
            Step = 0;
            StudioLibrary.SetsFolderOverride = "";
            EditorApplication.update -= Tick;
            Note(failed ? $"FAIL hand reveal 2 {Mode}: {Failure}" : "PASS hand reveal 2 " + Mode + " finished, output in " + Folder);
            if (SessionState.GetString("DJWarm.id", "") != "")
            {
                Directory.CreateDirectory(Path.Combine("Logs", "Warm"));
                File.WriteAllText(Path.Combine("Logs", "Warm", "exit.txt"), failed ? "1" : "0");
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
                return;
            }
            bool startedForThis = Environment.GetCommandLineArgs().Any(a => a.Contains("HandRevealMoment.Run"));
            if (Application.isBatchMode || startedForThis) EditorApplication.Exit(failed ? 1 : 0);
        }

        static StudioSet NewSet()
        {
            var set = new StudioSet
            {
                id = SetId, title = "Hand reveal 2", map = "castle", fps = Fps, length = Tape(LengthFrames) + .6f, seed = 2026,
                handBot = false, notes = "Made by HandRevealMoment. Safe to delete.",
                alive = new StudioAliveSettings { on = false }   // rendered before the alive layer existed; keep it as it was
            };
            StudioLibrary.Save(set);
            return set;
        }

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

        // ---- The geometry, worked out from the map ----

        sealed class Plan
        {
            public int block;
            public Vector3 centre, along, side, hero, gripEnd, handCentre, toSun, flightStart;
            public Vector3 blockSize;
            public Quaternion blockRotation;
            public float top, heroYaw, shadowFrame;
            public Vector3 facing;
            public string heroId, handId;
            public StudioCue cue;
            public StudioHandDirection.Grab grab;
            // Where a point riding on the summit block is at a trailer frame (from the cue's own path).
            public Vector3 Riding(Vector3 point, int frame)
            {
                var pose = StudioHandDirection.BlockAt(cue, grab, Tape(frame));
                return pose.position + pose.rotation * (Quaternion.Inverse(blockRotation) * (point - centre));
            }
            public Vector3 Dir(float degrees) => (along * Mathf.Cos(degrees * Mathf.Deg2Rad) + side * Mathf.Sin(degrees * Mathf.Deg2Rad)).normalized;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0; return v.sqrMagnitude < 1e-6f ? Vector3.forward : v.normalized; }
        static string V(Vector3 v) => v.ToString("F2");

        static float TopOf(Rigidbody body) => body.GetComponentsInChildren<Collider>().Where(c => c.enabled && !c.isTrigger).Select(c => c.bounds.max.y).DefaultIfEmpty(body.position.y).Max();

        static Plan MakePlan(StudioStage stage)
        {
            var plan = new Plan();
            var blocks = Enumerable.Range(0, stage.blocks.Length).Where(i => stage.blocks[i] && stage.blocks[i].gameObject.activeInHierarchy).ToList();
            int topIndex = blocks.OrderByDescending(i => TopOf(stage.blocks[i])).First();
            var top = stage.blocks[topIndex];
            plan.block = topIndex;
            plan.centre = top.position; plan.blockRotation = top.rotation;
            plan.blockSize = StudioHandDirection.BlockSize(top);
            plan.top = TopOf(top);
            float halfLength = Mathf.Max(plan.blockSize.x, plan.blockSize.z) * .5f;
            Vector3 longAxis = Flat(top.rotation * (plan.blockSize.x >= plan.blockSize.z ? Vector3.right : Vector3.forward));

            var sun = RenderSettings.sun ? RenderSettings.sun : Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.type == LightType.Directional).OrderByDescending(l => l.intensity).FirstOrDefault();
            plan.toSun = sun ? -sun.transform.forward : new Vector3(-.56f, .67f, -.49f).normalized;

            // The grip end points toward the sun: the hand comes in from the sun's side, so its shadow sweeps
            // over the hero first and the hero looks up into the light (a lit face).
            plan.along = Vector3.Dot(longAxis, Flat(plan.toSun)) >= 0 ? longAxis : -longAxis;
            // The camera side: the side of the block nearer the sun, so the cameras there have the sun behind them.
            plan.side = Vector3.Cross(Vector3.up, plan.along).normalized;
            if (Vector3.Dot(plan.side, Flat(plan.toSun)) < 0) plan.side = -plan.side;
            plan.gripEnd = plan.centre + plan.along * halfLength;

            Vector3 heroGuess = plan.centre - plan.along * (halfLength - HeroInset) + Vector3.up * 3;
            plan.hero = StudioStage.SurfaceBelow(heroGuess, out var feet) ? feet : new Vector3(heroGuess.x, plan.top, heroGuess.z);
            plan.heroYaw = Quaternion.LookRotation(plan.along).eulerAngles.y;

            Note($"Summit block #{topIndex} at {V(plan.centre)}, size {V(plan.blockSize)}, top surface y {plan.top:0.00}, long axis {V(longAxis)}.");
            Note($"Sun: {(sun ? sun.name : "none")}, direction to the sun {V(plan.toSun)} (elevation {Mathf.Asin(plan.toSun.y) * Mathf.Rad2Deg:0} deg).");
            Note($"Pull along {V(plan.along)} (toward the sun), camera side {V(plan.side)}. Hero feet {V(plan.hero)}, facing yaw {plan.heroYaw:0}. Grip end {V(plan.gripEnd)}.");
            var below = blocks.Where(i => i != topIndex && Vector3.Distance(stage.blocks[i].position, plan.centre) < 5 && stage.blocks[i].position.y < plan.centre.y - .5f).ToList();
            Note($"Blocks under the summit (within 5 m): {string.Join(", ", below.Select(i => "#" + i + " " + V(stage.blocks[i].position)))}.");
            return plan;
        }

        static StudioCue PullCue(StudioStage stage, Plan plan, string handId, Vector3 flightStart) => new StudioCue
        {
            id = "lift1", action = HandPullCue.Id, actor = handId, at = Tape(YankFrame),
            duration = FlightSeconds, grip = GripSeconds, pullTime = PullSeconds,
            point = stage.ToStage(flightStart), height = 2.5f, direction = plan.along, strength = PullDistance,
            carry = Vector3.up * CarryUp + plan.along * CarryOut, carryTime = CarrySeconds,
            fixedGrab = true, grabPosition = stage.ToStage(plan.centre), grabRotation = plan.blockRotation,
            blocks = new List<int> { plan.block }
        };

        // Where the flight starts: tried on a grid on the sun's side, so the hand's shadow first reaches the
        // hero at ShadowFrame, and the hand never passes closer than 2.5 m to the hero's head.
        static void SolveFlight(StudioStage stage, Plan plan)
        {
            float size = StudioHandDirection.HandSize(stage);
            var grab = StudioHandDirection.GripFor(plan.centre, plan.blockRotation, plan.blockSize, plan.along, size);
            Vector3 head = plan.hero + Vector3.up * .85f;
            Vector3 Shadow(Vector3 p) => p - plan.toSun * ((p.y - plan.top) / Mathf.Max(.1f, plan.toSun.y));
            Vector3 Middle(CandyPlayers.CandyHandPose pose) { var b = StudioHandDirection.BonesFor(pose, size); return b.Aggregate(Vector3.zero, (s, x) => s + x.position) / b.Length; }
            float best = float.MaxValue; string why = "none fitted";
            plan.flightStart = plan.gripEnd + plan.along * 8 + Vector3.up * 12;
            // The shadow falls away from the sun by about 1.1 x the hand's height, so the start lies well out on
            // the sun's side (the camera side), up to 20 m across.
            for (float back = -8; back <= 10; back += 2)
            for (float up = 6; up <= 18; up += 2)
            for (float across = 0; across <= 20; across += 2)
            {
                Vector3 start = plan.gripEnd + plan.along * back + plan.side * across + Vector3.up * up;
                var cue = PullCue(stage, plan, "", start);
                float first = -1, nearest = float.MaxValue;
                for (int f = 0; f < YankFrame; f++)
                {
                    if (!StudioHandDirection.HandAt(stage, cue, grab, Tape(f), out var pose)) continue;
                    Vector3 middle = Middle(pose);
                    nearest = Mathf.Min(nearest, Vector3.Distance(middle, head));
                    Vector3 s = Shadow(middle);
                    if (first < 0 && middle.y > plan.top && Vector2.Distance(new Vector2(s.x, s.z), new Vector2(plan.hero.x, plan.hero.z)) < .4f) first = f;   // on him, not beside him (r1)
                }
                if (first < 0) continue;
                float score = Mathf.Abs(first - ShadowFrame) + (nearest < 2.5f ? 100 : 0) + Mathf.Abs(across) * .05f;
                if (score < best)
                {
                    best = score; plan.flightStart = start; plan.shadowFrame = first;
                    why = $"back {back} m past the grip end, {up} m up, {across} m to the side: shadow reaches the hero at trailer frame {first}, hand passes {nearest:0.0} m from the hero's head";
                }
            }
            var closed = new CandyPlayers.CandyHandPose { position = grab.wrist.position, rotation = grab.wrist.rotation, skeleton = DeadlyJenga.CastleBots.CastleHandBotBrain.Articulate(false, .8f) };
            plan.handCentre = Middle(closed);
            plan.grab = grab;
            Note($"Flight start {V(plan.flightStart)}: {why}. Hand middle at the grab {V(plan.handCentre)}, hand size {size}.");
        }

        static Plan Direct(StudioStage stage)
        {
            var set = stage.set;
            var plan = MakePlan(stage);
            SolveFlight(stage, plan);
            // He faces the way the hand comes from (seen where it is in the middle of the reaction shot), so he sees
            // its shadow arrive and looks up at it without turning his head over his shoulder.
            plan.cue = PullCue(stage, plan, "", plan.flightStart);
            plan.facing = Flat(FlyingHand(stage, plan, (Shots[1].cutIn + Shots[1].cutOut) / 2) - plan.hero);
            plan.heroYaw = Quaternion.LookRotation(plan.facing).eulerAngles.y;
            Note($"Hero faces {V(plan.facing)} (yaw {plan.heroYaw:0}), toward the incoming hand.");
            set.marks.Clear();
            var hero = set.AddMark(StudioRole.Survivor, stage.ToStage(plan.hero));
            hero.actor = "Hero"; hero.yaw = plan.heroYaw; hero.color = HeroColour;
            hero.customLook = true; hero.appearance = new AvatarAppearance { hair = AvatarHair.Spikes, hairShade = AvatarHairShade.Match };
            var hand = set.AddMark(StudioRole.Hand, stage.ToStage(plan.flightStart));
            hand.actor = "Hand";
            plan.heroId = hero.id; plan.handId = hand.id;

            set.cues.Clear();
            plan.cue = PullCue(stage, plan, hand.id, plan.flightStart);
            set.cues.Add(plan.cue);

            // The hero: sees the shadow, looks up at the hand and follows it in, screams on the yank,
            // then looks back down at the spire he was standing on.
            set.overrides.Clear();
            // (The avatars have no drawn eyes or mouth, so the reaction is carried by the head and hands: the
            // "scream" layer tips the head up with both hands at the cheeks, "cower" hunches with hands over the head.)
            set.overrides.Add(new StudioOverride { actor = hero.id, kind = "lookAt", target = "actor:" + hand.id, from = Tape(ShadowFrame - 8), to = Tape(LengthFrames + 10), blendIn = .35f, blendOut = .1f });
            set.overrides.Add(new StudioOverride { actor = hero.id, kind = "reaction", name = "scream", from = Tape(44), to = Tape(YankFrame) });
            set.overrides.Add(new StudioOverride { actor = hero.id, kind = "reaction", name = "cower", from = Tape(YankFrame + 3), to = Tape(LengthFrames + 10) });
            StudioLibrary.Save(set);
            StudioRun.Refresh();
            return plan;
        }

        static IEnumerable<int> RecordTake(Plan plan)
        {
            var clock = Stopwatch.StartNew();
            StudioHandDirection.BakeLook = HandDesign;
            StudioRun.RecordWholeSet();
            foreach (int wait in UntilHeld()) yield return wait;
            StudioRun.SaveTape();
            Note($"Recorded in {clock.Elapsed.TotalSeconds:0} s. {StudioRun.Status}");
            Note("Cue log: " + string.Join(" | ", StudioRun.CueLog));
        }

        // What happened in the take: the summit block and the hero, so the riding can be checked.
        static void Describe(StudioStage stage, Plan plan)
        {
            var world = stage.tape.FindType(WorldTrack.Id).ActiveTake;
            WorldTrack.Positions(world, Mathf.RoundToInt(Tape(0) * stage.Fps), out var start, out _);
            var heroStart = StudioActorPoses.At(stage.tape, plan.heroId, Tape(0), stage.Fps);
            var line = new StringBuilder("Take (trailer frame: summit block offset from start, hero feet offset from start, hero feet minus block top, other blocks moved > 5 cm):\n");
            for (int f = 0; f <= LengthFrames; f += f < YankFrame - 15 ? 15 : 5)
            {
                int tapeFrame = Mathf.Min(Mathf.RoundToInt(Tape(f) * stage.Fps), stage.Frames - 1);
                WorldTrack.Positions(world, tapeFrame, out var now, out var on);
                int moved = 0;
                for (int i = 0; i < now.Length; i++) if (i != plan.block && on[i] && Vector3.Distance(now[i], start[i]) > .05f) moved++;
                var hero = StudioActorPoses.At(stage.tape, plan.heroId, Tape(f), stage.Fps);
                float blockTop = now[plan.block].y + plan.top - plan.centre.y;
                line.Append($"- f{f}: block {V(now[plan.block] - start[plan.block])}, hero {(hero.found && heroStart.found ? V(hero.feet - heroStart.feet) : "gone")}, " +
                    $"hero above block top {(hero.found ? (hero.feet.y - blockTop).ToString("0.00") : "-")}, moved {moved}\n");
            }
            Note(line.ToString());
        }

        // ---- The cameras ----

        static StudioCameraTrack NewCamera(Shot shot, float focal)
        {
            float from = Tape(shot.cutIn - Handle + shot.shift), to = Tape(shot.cutOut + Handle + shot.shift);
            var camera = StudioMoves.Camera(shot.id, from, to, focal);
            camera.id = shot.id;
            camera.sensorHeight = 24;
            camera.keys[0].focal = focal;
            camera.vignette = .25f; camera.grain = .04f;
            camera.exposure = Exposure; camera.contrast = Contrast; camera.saturation = Saturation;
            return camera;
        }

        static bool Clear(Vector3 at) => !Physics.CheckSphere(at, .3f, ~0, QueryTriggerInteraction.Ignore);

        static string P(StudioStage stage, Vector3 world) => StudioTargets.Point(stage.ToStage(world));

        static List<StudioCameraTrack> BuildCameras(StudioStage stage, Plan plan)
        {
            var cameras = new List<StudioCameraTrack>();
            Vector3 up = Vector3.up, head = plan.hero + up * .85f;
            string hero = "head:" + plan.heroId;
            var lines = new StringBuilder("Cameras:\n");
            void Report(Shot shot, StudioCameraTrack camera, Vector3 at, Vector3 subject, float subjectHeight)
            {
                float d = Vector3.Distance(at, subject), hf = d * 24 / camera.keys[0].focal;
                lines.Append($"- {shot.id} at {V(at)}{(Clear(at) ? "" : " INSIDE GEOMETRY")}, {camera.keys[0].focal:0} mm, subject {d:0.0} m away: Hf {hf:0.00} m, subject fraction {subjectHeight / hf:0.00}\n");
            }

            // S01 effect: a little above and in front of the hero, the summit's top surface in frame so the
            // shadow can be seen sliding over it and over the hero.
            {
                var s = Shots[0]; var c = NewCamera(s, 28);
                // In front of him, 50 degrees round toward the grip end, so the shadow comes toward him and the lens.
                Vector3 a1 = Quaternion.AngleAxis(50, up) * plan.facing, a2 = Quaternion.AngleAxis(-50, up) * plan.facing;
                Vector3 front = Vector3.Dot(a1, plan.along) >= Vector3.Dot(a2, plan.along) ? a1 : a2;
                Vector3 at = plan.hero + front * 3.8f + up * 1.5f;
                StudioMoves.LockedOff(c, c.start, stage.ToStage(at), P(stage, plan.hero + up * .35f + plan.along * .6f));
                Report(s, c, at, head, .97f); cameras.Add(c);
            }
            // The first camera spot in a list that is clear of blocks and sees every target (the summit block
            // itself is ignored: it is not there any more when the late targets are).
            var summit = stage.blocks[plan.block];
            bool Sees(Vector3 from, Vector3 to) => !Physics.RaycastAll(from, (to - from).normalized, Vector3.Distance(from, to) - .3f, ~0, QueryTriggerInteraction.Ignore)
                .Any(h => h.collider.attachedRigidbody != summit && !h.collider.transform.IsChildOf(summit.transform));
            Vector3 Pick(string id, IEnumerable<Vector3> spots, params Vector3[] targets)
            {
                int tried = 0, bestSeen = -1; Vector3 best = spots.First();
                foreach (var spot in spots)
                {
                    tried++;
                    if (!Clear(spot)) continue;
                    int seen = targets.Count(t => Sees(spot, t));
                    if (seen == targets.Length) { lines.Append($"  {id}: spot {tried} of the list is clear\n"); return spot; }
                    if (seen > bestSeen) { bestSeen = seen; best = spot; }
                }
                lines.Append($"  {id}: NO fully clear spot in {tried}; using one that sees {bestSeen} of {targets.Length} targets\n");
                return best;
            }
            Vector3 heroAt(int f) => plan.Riding(head, f);
            Vector3 handAt(int f) => plan.Riding(plan.handCentre, f);

            // S02 reaction: medium close-up, lens below the eyes, in front of him; the hand is above and behind
            // the lens, so his eyeline (and the scream pose's raised head) goes past us.
            {
                var s = Shots[1]; var c = NewCamera(s, 50);
                // He looks at the flying hand, so the lens goes on the hand's side of him (v03 saw the back of his head).
                Vector3 toHand = Flat(FlyingHand(stage, plan, (s.cutIn + s.cutOut) / 2) - plan.hero);
                Vector3 at = Pick(s.id, new[] { 25f, -25f, 40f, -40f }.Select(a => plan.hero + up * 1.05f + Quaternion.AngleAxis(a, up) * toHand * 1.0f), head);   // just above his eyes: his face is tipped back toward the sky (v07 read as the back of his head from below)
                // A fixed aim point (r1: aiming at the moving head made a static shot pan at 40 deg/s).
                StudioMoves.LockedOff(c, c.start, stage.ToStage(at), P(stage, head + up * .05f));
                foreach (var key in c.keys) { key.aperture = 4f; key.focus = Vector3.Distance(at, head); }
                Report(s, c, at, head, .45f); cameras.Add(c);
            }
            // S03 whole: low on the camera side, nearer the hand than the hero (so the hero is the farther one
            // and the hand reads bigger), the summit's top almost edge-on, slow push-in while the hand settles.
            {
                var s = Shots[2]; var c = NewCamera(s, 24);
                float apart = Vector3.Distance(Flat2(plan.hero), Flat2(plan.handCentre));
                var spots = new List<Vector3>();
                // Round toward the front of the hand (critic r1: side-on, the Lava hand read as a tube), so the fingers
                // come toward the lens, with the hero behind and beside it for scale.
                foreach (float angle in new[] { 45f, 55f, 35f })
                foreach (float lift in new[] { .35f, .8f })
                { Vector3 p = plan.handCentre + (plan.along * Mathf.Cos(angle * Mathf.Deg2Rad) + plan.side * Mathf.Sin(angle * Mathf.Deg2Rad)) * 5.2f; p.y = plan.top + lift; spots.Add(p); }
                Vector3 at = Pick(s.id, spots, head, plan.handCentre);
                Vector3 aim = Vector3.Lerp(plan.hero + up * .6f, plan.handCentre, .55f) + up * .9f;
                StudioMoves.LockedOff(c, c.start, stage.ToStage(at), P(stage, aim));
                StudioMoves.PushIn(c, c.start, c.end, stage.ToStage(aim), .08f);
                Report(s, c, at, plan.handCentre, 2.4f); cameras.Add(c);
                lines.Append($"  S03 hero distance {Vector3.Distance(at, head):0.0} m, hand distance {Vector3.Distance(at, plan.handCentre):0.0} m\n");
            }
            // S04 payoff: wider, on the camera side and a little behind the hero's line, so the block slides across
            // and away (never toward the lens); the slide and the start of the lift stay in frame. Shake on the yank.
            {
                var s = Shots[3]; var c = NewCamera(s, 28);
                Vector3 middle = Vector3.Lerp(plan.centre, plan.Riding(plan.centre, YankFrame + 45), .5f) + up * 1.2f;
                var spots = new List<Vector3>();
                foreach (float lift in new[] { 1.5f, 2.5f, .5f })   // above the block's top, so the hand is not behind it (r1)
                foreach (float turn in new[] { 25f, 15f, 35f })
                foreach (float d in new[] { 6.5f, 7.5f, 9f })
                { Vector3 p = middle + (plan.side * Mathf.Cos(turn * Mathf.Deg2Rad) - plan.along * Mathf.Sin(turn * Mathf.Deg2Rad)) * d; p.y = plan.top + lift; spots.Add(p); }
                Vector3 at = Pick(s.id, spots, head, plan.handCentre, heroAt(YankFrame + 45), handAt(YankFrame + 45));
                StudioMoves.LockedOff(c, c.start, stage.ToStage(at), P(stage, middle));
                StudioMoves.Shake(c, Tape(YankFrame) + 1f / Fps, 1.5f, .45f);
                Report(s, c, at, middle, 7.5f); cameras.Add(c);
            }
            // S05 consequence: a camera that stays put while he is carried up past it, tilting with him, so the
            // rise shows against the sea and sky (r1: riding with him hid the rise). Aimed from his feet, because
            // the cower layer moves the recorded head point lower than the drawn head.
            {
                var s = Shots[4]; var c = NewCamera(s, 70);
                string feet = "actor:" + plan.heroId;
                Vector3 mid = plan.Riding(plan.hero, (s.cutIn + s.cutOut) / 2);
                // Well round to the side: the block tips in the plane of the pull, so a lens off that plane is never in its swing
                // (v07: at 3.4 m on the grip-end side the rising block passed through the lens).
                Vector3 dir = (plan.along * .45f + plan.side * .9f).normalized;
                Vector3 at = Pick(s.id, new[] { 5.5f, 6.5f, 4.5f }.Select(d => mid + dir * d + up * .3f), plan.Riding(head, s.cutIn) + up * .5f, plan.Riding(head, s.cutOut) + up * .5f);
                StudioMoves.LockedOff(c, c.start, stage.ToStage(at), feet, new Vector3(0, .6f, 0));
                c.keys[0].aperture = 5.6f; c.keys[0].focus = Vector3.Distance(at, mid);
                Report(s, c, at, mid, .97f); cameras.Add(c);
            }
            // S06 final image: wide on the camera side, the spire's stump low in frame, the block held up in the
            // giant fingers with the hero on it; the lift settles inside the shot.
            {
                var s = Shots[5]; var c = NewCamera(s, 24);
                Vector3 heroEnd = heroAt(LengthFrames), handEnd = handAt(LengthFrames);
                // Aim halfway between the stump's top and the held block, so both are in frame (v03 lost the stump).
                Vector3 stumpTop = plan.centre - up * 1f;
                Vector3 middle = Vector3.Lerp(stumpTop, (heroEnd + handEnd) * .5f, .55f);
                var spots = new List<Vector3>();
                // First choice (v06): out past the hand, a little below it, looking back at the tower: the giant hand
                // big in the foreground, the stolen block running back from it with the hero on the far end, the
                // stump below and behind. (From the side, v05 had the hand at 0.13 of frame among tower blocks.)
                Vector3 behindHand = Vector3.Lerp(heroEnd, stumpTop, .35f);
                foreach (float d in new[] { 5.5f, 7f })
                foreach (float angle in new[] { 60f, 75f, 45f })   // well round to the side, so the wrist's cut end is not toward the lens
                { Vector3 p = handEnd + (plan.along * Mathf.Cos(angle * Mathf.Deg2Rad) + plan.side * Mathf.Sin(angle * Mathf.Deg2Rad)) * d; p.y = handEnd.y - 1.2f; spots.Add(p); }
                int heroSideSpots = spots.Count;
                foreach (float d in new[] { 8f, 9.5f, 11f })
                foreach (float lift in new[] { -.5f, 1f, 2.5f, -2f })
                foreach (float turn in new[] { 15f, 0f, 30f, -15f, 45f })
                { Vector3 p = middle + (plan.side * Mathf.Cos(turn * Mathf.Deg2Rad) - plan.along * Mathf.Sin(turn * Mathf.Deg2Rad)) * d; p.y = plan.top + lift; spots.Add(p); }
                Vector3 at = Pick(s.id, spots, heroEnd, handEnd, plan.centre - up * .9f);
                bool pastHand = spots.IndexOf(at) < heroSideSpots;
                // Past the hand: aim between the hand and the hero's far end, lowered so the stump is in frame.
                Vector3 aimAt = pastHand ? Vector3.Lerp(handEnd, behindHand, .5f) + up * .4f : middle;   // r1/v06: the hand rose out of the top
                StudioMoves.LockedOff(c, c.start, stage.ToStage(at), P(stage, aimAt));
                lines.Append($"  S06 {(pastHand ? "past the hand" : "from the side")}, hand {Vector3.Distance(at, handEnd):0.0} m, hero {Vector3.Distance(at, heroEnd):0.0} m, stump {Vector3.Distance(at, stumpTop):0.0} m\n");
                Report(s, c, at, handEnd, 2.4f); cameras.Add(c);
                lines.Append($"  S06 hero end {V(heroEnd)}, hand end {V(handEnd)}\n");
            }
            foreach (var c in cameras) { c.measure.Add("actor:" + plan.heroId); c.measure.Add("actor:" + plan.handId); c.measure.Add("block:" + plan.block); }
            Note(lines.ToString());
            return cameras;
        }

        static Vector3 Flat2(Vector3 v) => new Vector3(v.x, 0, v.z);

        // The middle of the hand at a trailer frame during its flight (from the cue's own path).
        static Vector3 FlyingHand(StudioStage stage, Plan plan, int frame)
        {
            float size = StudioHandDirection.HandSize(stage);
            if (!StudioHandDirection.HandAt(stage, plan.cue, plan.grab, Tape(frame), out var pose)) return plan.flightStart;
            var bones = StudioHandDirection.BonesFor(pose, size);
            return bones.Aggregate(Vector3.zero, (s, b) => s + b.position) / bones.Length;
        }

        // ---- Shooting ----

        static IEnumerator<int> Shoot(string mode)
        {
            bool final = mode.StartsWith("final");
            var stage = StudioRun.Stage;
            Note($"# Hand reveal 2, {mode} run, version {Version}\n");
            var plan = Direct(stage);
            yield return 0;
            foreach (int wait in RecordTake(plan)) yield return wait;
            Describe(stage, plan);

            var set = stage.set;
            var cameras = BuildCameras(stage, plan);
            if (mode == "finalA") cameras = cameras.Take(3).ToList();
            if (mode == "finalB") cameras = cameras.Skip(3).ToList();
            // No motion blur: with it (v08 finals A and A2) the survivor rendered white; the previews never used it.
            if (final) foreach (var c in cameras) c.motionBlur = 0;
            set.cameras.Clear(); set.cameras.AddRange(cameras);
            set.render = final
                // cinema = false: the cinema preset (the project's top quality level) renders the survivor white
                // (v08 final A: the yellow hero came out white, as the pilot's "washed-out hero" did).
                ? new StudioRenderSettings { width = 1920, height = 1080, fps = Fps, subFrames = 1, format = "png", cinema = false, filmstrip = 12 }
                : new StudioRenderSettings { width = 640, height = 360, fps = Fps, subFrames = 1, format = "png", cinema = false, filmstrip = 8 };
            StudioLibrary.Save(set);
            string renders = Path.Combine(Folder, "renders");
            var clock = Stopwatch.StartNew();
            StudioRender.Start(set.cameras, set.render, renders);
            foreach (int wait in UntilRendered()) yield return wait;
            Note($"Render {set.render.width}x{set.render.height}: {StudioRender.Progress} in {clock.Elapsed.TotalSeconds:0} s.");
            KeepShots(StudioRender.Results.ToList());

            string audio = StudioRender.AudioFile(set.id);
            if (File.Exists(audio)) File.Copy(audio, Path.Combine(Folder, "tape.wav"), true);
            File.WriteAllText(Path.Combine(Folder, "set.json"), JsonUtility.ToJson(set, true));
            File.WriteAllText(Path.Combine(Folder, "shots.txt"), string.Join("\n", Shots.Select(s => $"{s.id} {s.cutIn} {s.cutOut} {s.key} {s.shift} {Handle}")) + "\n");

            if (mode == "scout") foreach (int wait in CompareDesigns(stage, plan, cameras)) yield return wait;
            if (mode == "finalA") foreach (int wait in ColourCheck(cameras[1])) yield return wait;
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

        // The hero's colour at two sizes and with and without motion blur, one frame of S02 each, to find what
        // turns the survivor white in 1080p renders.
        static IEnumerable<int> ColourCheck(StudioCameraTrack shot)
        {
            string folder = Path.Combine(Folder, "colour-check");
            Directory.CreateDirectory(folder);
            foreach (var (label, width, blur) in new[] { ("540", 960, 0f), ("1080-blur", 1920, .3f) })
            {
                var copy = JsonUtility.FromJson<StudioCameraTrack>(JsonUtility.ToJson(shot));
                float t = Tape(Shots[1].cutIn + Shots[1].key);
                copy.id = "colour-" + label; copy.name = copy.id; copy.start = t; copy.end = t + 1.5f / Fps; copy.measure.Clear(); copy.motionBlur = blur;
                StudioRender.Start(new[] { copy }, new StudioRenderSettings { width = width, height = width * 9 / 16, fps = Fps, format = "png", cinema = false, filmstrip = 0 }, Path.Combine(Folder, "renders-colour"));
                foreach (int wait in UntilRendered()) yield return wait;
                foreach (var result in StudioRender.Results)
                {
                    string first = Path.Combine(result.folder, "00000.png");
                    if (File.Exists(first)) File.Copy(first, Path.Combine(folder, label + ".png"), true);
                }
            }
            Note("Colour check stills written.");
            string renders = Path.Combine(Folder, "renders-colour");
            if (Directory.Exists(renders)) Directory.Delete(renders, true);
        }

        // Stills of the same frames with other game hand designs, to choose the hand's look.
        static IEnumerable<int> CompareDesigns(StudioStage stage, Plan plan, List<StudioCameraTrack> cameras)
        {
            var track = stage.tape.Find(plan.handId);
            var take = track?.ActiveTake;
            if (take == null) { Note("Design comparison: no hand take."); yield break; }
            var original = take.frames.ToList();
            string folder = Path.Combine(Folder, "designs");
            Directory.CreateDirectory(folder);
            foreach (int design in DesignsToCompare)
            {
                for (int f = 0; f < take.frames.Count; f++)
                {
                    var frame = ActorFrame.Read(HandTrack.Id, original[f]);
                    if (!frame.visible) continue;
                    frame.handLook = design;
                    take.frames[f] = frame.Write();
                }
                var stills = new List<StudioCameraTrack>();
                foreach (var (shotIndex, at) in new[] { (2, Shots[2].key), (5, Shots[5].key) })
                {
                    var copy = JsonUtility.FromJson<StudioCameraTrack>(JsonUtility.ToJson(cameras[shotIndex]));
                    float t = Tape(Shots[shotIndex].cutIn + at);
                    copy.id = $"design{design}-{Shots[shotIndex].id}"; copy.name = copy.id;
                    copy.start = t; copy.end = t + 1.5f / Fps;
                    copy.measure.Clear();
                    stills.Add(copy);
                }
                StudioRender.Start(stills, new StudioRenderSettings { width = 960, height = 540, fps = Fps, format = "png", cinema = false, filmstrip = 0 }, Path.Combine(Folder, "renders-designs"));
                foreach (int wait in UntilRendered()) yield return wait;
                foreach (var result in StudioRender.Results)
                {
                    string first = Path.Combine(result.folder, "00000.png");
                    if (File.Exists(first)) File.Copy(first, Path.Combine(folder, result.cameraId + ".png"), true);
                }
                Note($"Design {design} ({DeadlyJenga.CandyPhysicsHands.CandyHandDesigns.Names[design]}): {StudioRender.Results.Count} stills.");
            }
            for (int f = 0; f < take.frames.Count; f++) take.frames[f] = original[f];
            string renders = Path.Combine(Folder, "renders-designs");
            if (Directory.Exists(renders)) Directory.Delete(renders, true);
        }
    }
}
