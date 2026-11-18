using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.CastleBots;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Phase 2 acceptance tests: performance passes.
    // A person cannot perform in a batch run, so a scripted input driver plays the local survivor
    // through the game's normal movement code. Everything else is the real Studio path.
    public static partial class StudioAcceptance
    {
        static StudioSet MakeSuiteSet(string suite)
        {
            string id = "acceptance-" + suite.ToLowerInvariant();
            string tape = StudioLibrary.TapePath(id);
            if (File.Exists(tape)) File.Delete(tape);
            var set = new StudioSet { id = id, title = "Acceptance: " + suite, map = Map, fps = 30, length = 6,
                notes = "Made by the " + suite + " acceptance run. Safe to delete." };
            set.AddMark(StudioRole.Survivor, new Vector3(1.2f, 0, 0)).actor = "Ada";
            set.AddMark(StudioRole.Survivor, new Vector3(-1.2f, 0, 0)).actor = "Ben";
            // The collapse comes late, so the performers have steady ground for the first four seconds.
            set.cues.Add(new StudioCue { at = 4.5f, action = "collapse", strength = 3 });
            StudioLibrary.Save(set);
            return set;
        }

        static IEnumerator<int> SuiteTests(string suite)
        {
            switch (suite)
            {
                case "Phase2": return Phase2Tests();
                case "Phase3": return Phase3Tests();
                case "Phase4": return Phase4Tests();
                case "Window": return WindowTour();
                case "Phase5": return Phase5Tests();
                default: throw new InvalidOperationException("There is no test suite called " + suite);
            }
        }

        // Plays the local survivor from a script: what to press at each moment of the tape.
        sealed class ScriptedPerformer : ICastleSurvivorDriver
        {
            public Func<float, CastleBotCommand> script = _ => default;
            public bool jumped, climbed;
            public float highest;
            float startHeight = float.NaN;

            public void Reset() { jumped = climbed = false; highest = 0; startHeight = float.NaN; }

            public CastleBotCommand ReadCommand(CastleSurvivor motor, float dt)
            {
                var stage = StudioRun.Stage;
                if (stage == null || !StudioRun.Recording || StudioRun.LeadIn || StudioRun.Countdown > 0) return default;
                if (float.IsNaN(startHeight)) startHeight = motor.transform.position.y;
                if (!motor.Grounded) jumped = true;
                if (motor.Climbing) climbed = true;
                highest = Mathf.Max(highest, motor.transform.position.y - startHeight);
                return script(StudioRun.Frame / (float)stage.Fps);
            }
        }

        static StudioPass PerformPass(StudioStage stage, StudioMark mark, bool fixedClock = true)
        {
            StudioPerform.Actor = mark.id;
            StudioPerform.Countdown = false; StudioPerform.PreRoll = 0; StudioPerform.Loop = false;
            StudioPerform.PunchIn = false; StudioPerform.LivePhysics = false; StudioPerform.Speed = 1;
            var pass = StudioPerform.BuildPass(stage, 0);
            pass.fixedClock = fixedClock;
            return pass;
        }

        // A wall close to a spot that a survivor could climb: returns the direction to it, or zero.
        static Vector3 FindWall(Vector3 feet)
        {
            for (int i = 0; i < 16; i++)
            {
                Vector3 direction = Quaternion.Euler(0, i * 22.5f, 0) * Vector3.forward;
                if (Physics.Raycast(feet + Vector3.up * 1f, direction, out var hit, 3.5f, 1 << Layers.Block, QueryTriggerInteraction.Ignore) &&
                    hit.collider.bounds.max.y > feet.y + 1.4f && Mathf.Abs(hit.normal.y) < .3f) return direction;
            }
            return Vector3.zero;
        }

        static List<Vector3> Positions(StudioTrack track, StudioTake take) =>
            take.frames.Select(f => ActorFrame.Read(track.type, f).Position).ToList();

        static IEnumerator<int> Phase2Tests()
        {
            var stage = StudioRun.Stage;
            var set = stage.set;
            var tape = stage.tape;
            int frames = set.Frames, fps = set.fps;
            StudioCamera.Resize(640, 360);
            foreach (var mark in set.marks)
                if (StudioStage.SurfaceBelow(stage.ToWorld(mark.position), out var surface)) mark.position = stage.ToStage(surface);
            StudioLibrary.Save(set);
            var ada = set.marks[0]; var ben = set.marks[1];

            // The local survivor takes its input from the script instead of the keyboard.
            var motor = stage.session.Survivor();
            var performer = new ScriptedPerformer();
            motor.externallyDriven = true;
            motor.InputDriver = performer;
            motor.RefreshMode();

            // The world first: one live recording with the collapse cue.
            var worldBefore = new Dictionary<int, Vector3[]>();
            void SampleWorld(int frame) => worldBefore[frame] = stage.blocks.Select(b => b ? b.transform.position : Vector3.zero).ToArray();
            StudioRun.FrameShown += SampleWorld;
            StudioRun.RecordWholeSet();
            foreach (int wait in UntilHeld()) yield return wait;
            StudioRun.FrameShown -= SampleWorld;
            var firstWorld = tape.FindType(WorldTrack.Id)?.ActiveTake;
            Result("Setup: world recorded", firstWorld != null && firstWorld.frames.Count == frames, $"{stage.blocks.Length} blocks, {frames} frames. Cues: {string.Join("; ", StudioRun.CueLog)}");

            // ---- Test 1: record Ada (run, jump, climb if there is a wall), then Ben while Ada plays back ----
            Vector3 adaFeet = stage.ToWorld(ada.position);
            Vector3 wall = FindWall(adaFeet);
            Vector3 run = wall != Vector3.zero ? wall : Vector3.right;
            performer.Reset();
            performer.script = t => new CastleBotCommand
            {
                move = t < .3f ? Vector3.zero : t < 2.4f ? run * .55f : Vector3.zero, look = run,
                jump = t >= 1.0f && t < 1.07f, climbHeld = wall != Vector3.zero && t > 1.4f && t < 3.5f
            };
            var adaLive = new List<Vector3>();
            void SampleAda(int frame) { var body = stage.Live(ada.id); if (body && StudioRun.Recording && !StudioRun.LeadIn) adaLive.Add(body.transform.position); }
            StudioRun.FrameShown += SampleAda;
            StudioRun.Record(PerformPass(stage, ada));
            foreach (int wait in UntilHeld()) yield return wait;
            StudioRun.FrameShown -= SampleAda;
            var adaTrack = tape.Find(ada.id);
            var adaTake = adaTrack?.ActiveTake;
            var adaPath = adaTake == null ? new List<Vector3>() : Positions(adaTrack, adaTake);
            float adaTravel = adaPath.Count > 1 ? Vector3.Distance(adaPath[0], adaPath[adaPath.Count - 1]) : 0;
            bool adaJumped = performer.jumped; bool adaClimbed = performer.climbed; float adaHigh = performer.highest;
            Result("1a. Record actor A through the local player (scripted run, jump, climb)", adaTake != null && adaTake.frames.Count == frames && adaTravel > .3f && adaJumped,
                $"Ada take: {adaTake?.frames.Count ?? 0}/{frames} frames, moved {adaTravel:F2} m, left the ground: {adaJumped}, highest point {adaHigh:F2} m above the mark, " +
                $"climbing state seen: {adaClimbed}" + (wall == Vector3.zero ? " (no climbable wall within 3.5 m of the mark, so the script could not climb)." : "."));

            performer.Reset();
            performer.script = t => new CastleBotCommand { move = t > .5f && t < 2f ? -run * .4f : Vector3.zero, look = -run, jump = t >= 2.2f && t < 2.27f };
            float worstPlayback = 0, worstAgainstLive = 0; int checkedFrames = 0;
            void CheckAda(int frame)
            {
                if (!StudioRun.Recording || StudioRun.LeadIn) return;
                if (!stage.playback.TryGetValue(ada.id, out var shown) || !shown || !shown.activeSelf || frame >= adaPath.Count) return;
                worstPlayback = Mathf.Max(worstPlayback, Vector3.Distance(shown.transform.position, adaPath[frame]) * 1000);
                if (frame < adaLive.Count) worstAgainstLive = Mathf.Max(worstAgainstLive, Vector3.Distance(shown.transform.position, adaLive[frame]) * 1000);
                checkedFrames++;
                if (frame % 3 == 0) StudioCamera.SavePng(Path.Combine(Folder, "two-actors", $"{frame / 3:D05}.png"));
            }
            StudioCamera.Target = adaFeet + Vector3.up; StudioCamera.Distance = 9; StudioCamera.Place();
            StudioRun.FrameShown += CheckAda;
            StudioRun.Record(PerformPass(stage, ben));
            foreach (int wait in UntilHeld()) yield return wait;
            StudioRun.FrameShown -= CheckAda;
            var benTake = tape.Find(ben.id)?.ActiveTake;
            Result("1b. Record actor B while A plays back; A matches its take", benTake != null && benTake.frames.Count == frames && checkedFrames > frames / 2 && worstPlayback <= 1 && worstAgainstLive <= 60,
                $"Ben take: {benTake?.frames.Count ?? 0}/{frames} frames. Ada's played-back position was compared on {checkedFrames} frames: worst {worstPlayback:F3} mm from her take, " +
                $"worst {worstAgainstLive:F1} mm from where she actually stood when recorded. Ada's take was not changed: {adaTrack.ActiveTake == adaTake}. Pictures: two-actors/.");

            // ---- Test 3: punch in at 2.0 s on the 6 s take ----
            int punch = 2 * fps;
            performer.Reset();
            performer.script = t => new CastleBotCommand { move = t < 4f ? Quaternion.Euler(0, 90, 0) * run * .4f : Vector3.zero, look = Quaternion.Euler(0, 90, 0) * run };
            var punchPass = PerformPass(stage, ada);
            StudioPerform.PunchIn = true;
            punchPass = StudioPerform.BuildPass(stage, punch);
            punchPass.fixedClock = true;
            StudioRun.Record(punchPass);
            foreach (int wait in UntilHeld()) yield return wait;
            StudioPerform.PunchIn = false;
            var joined = adaTrack.ActiveTake;
            var joinedPath = joined == null ? new List<Vector3>() : Positions(adaTrack, joined);
            float before = 0, across = 0, keptError = 0;
            for (int f = 1; f < joinedPath.Count; f++)
            {
                float step = Vector3.Distance(joinedPath[f], joinedPath[f - 1]);
                if (f < punch - 1) before = Mathf.Max(before, step);
                if (f >= punch - 1 && f <= punch + 8) across = Mathf.Max(across, step);
                if (f < punch && f < adaPath.Count) keptError = Mathf.Max(keptError, Vector3.Distance(joinedPath[f], adaPath[f]));
            }
            float divergence = joinedPath.Count > 0 && adaPath.Count > 0 ? Vector3.Distance(joinedPath[joinedPath.Count - 1], adaPath[adaPath.Count - 1]) : 0;
            Result("3. Punch in at 2.0 s on a 6 s take; the join does not pop",
                joined != null && joined != adaTake && joined.start == 0 && joined.frames.Count == frames && keptError < .001f && across <= Mathf.Max(before * 1.6f, .06f) && divergence > .2f,
                $"New take {joined?.frames.Count ?? 0} frames from frame {joined?.start}. Before 2.0 s it equals the old take (largest difference {keptError * 1000:F2} mm). " +
                $"Largest move in one frame: {before * 100:F1} cm before the join, {across * 100:F1} cm across it. After the join the performance is new (ends {divergence:F2} m from the old ending). " +
                $"The old take is still there: {adaTrack.takes.Contains(adaTake)}; takes on the track: {adaTrack.takes.Count}.");

            // A comp from two takes.
            string compNote;
            bool compOk = false;
            try
            {
                var comp = StudioTakes.Comp(adaTrack, adaTake, 3 * fps, Mathf.RoundToInt(.2f * fps));
                compOk = comp.frames.Count == frames && adaTrack.ActiveTake == comp;
                compNote = $"Comp \"{comp.name}\": {comp.frames.Count} frames, now active; source takes untouched ({adaTrack.takes.Count} takes on the track).";
            }
            catch (Exception error) { compNote = error.Message; }
            Result("Takes: comp from two takes at 3.0 s", compOk, compNote);

            // ---- Test 2: twelve actors, some cloned ----
            int made = 0;
            for (int i = 0; i < 10; i++)
            {
                StudioTakes.Clone(set, tape, i % 2 == 0 ? ada : ben, new StudioTakes.CloneOptions
                    { offset = new Vector3(.6f * (i / 2 + 1), 0, i % 2 == 0 ? 1.1f : -1.1f), turn = i * 12, delay = .15f * i, mirror = i % 3 == 0, jitter = .5f, seed = 100 + i });
                made++;
            }
            StudioLibrary.Save(set);
            StudioRun.SaveTape();
            StudioRun.Refresh();
            StudioCamera.Target = stage.anchor + Vector3.up; StudioCamera.Distance = 14; StudioCamera.Place();
            int actorTracks = tape.tracks.Count(t => t.type == SurvivorTrack.Id && t.ActiveTake != null);
            int shownMost = 0;
            void CountShown(int frame)
            {
                shownMost = Mathf.Max(shownMost, stage.playback.Values.Count(o => o && o.activeSelf));
                if (frame % 6 == 0) StudioCamera.SavePng(Path.Combine(Folder, "twelve", $"{frame / 6:D05}.png"));
            }
            StudioRun.FrameShown += CountShown;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            StudioRun.Play(0, -1, true);
            foreach (int wait in UntilHeld()) yield return wait;
            StudioRun.FrameShown -= CountShown;
            double took = clock.Elapsed.TotalSeconds;
            Result("2. Twelve actors (two performed, ten cloned) play back", actorTracks == 12 && shownMost == 12 && (took <= set.length * 1.25f + .5f || StudioRun.RealTimeWarning),
                $"{actorTracks} actor tracks with takes; {shownMost} actors on screen at once. A real-time playback of the {set.length:0} s tape took {took:F2} s in this batch editor. " +
                (StudioRun.RealTimeWarning ? "The Studio showed its \"Slower than real time\" warning." : "No slow-playback warning was needed.") + " Pictures: twelve/.");

            // ---- Test 5: a live-physics pass re-records the world; older takes go stale at the right frame ----
            var hand = set.AddMark(StudioRole.Hand, stage.ToStage(stage.anchor + new Vector3(0, 5, -9)));
            hand.actor = "Hand";
            set.handBot = true;
            // Make sure the world really differs even if the hand bot grabs late: one more shove at 2.5 s.
            set.cues.Add(new StudioCue { at = 2.5f, action = "collapse", strength = 5, point = new Vector3(0, 0, 0) });
            StudioLibrary.Save(set);
            StudioRun.Refresh();
            var worldAfter = new Dictionary<int, Vector3[]>();
            float handMoved = 0; Vector3? handStart = null;
            void SampleAgain(int frame)
            {
                worldAfter[frame] = stage.blocks.Select(b => b ? b.transform.position : Vector3.zero).ToArray();
                var body = stage.Live(hand.id);
                var actor = body ? body.GetComponentInChildren<INetworkActor>(true) : null;
                if (actor == null) return;
                handStart ??= actor.NetworkPosition;
                handMoved = Mathf.Max(handMoved, Vector3.Distance(handStart.Value, actor.NetworkPosition));
            }
            var live = new StudioPass { label = "Live physics with the hand bot", liveWorld = true, fixedClock = true };
            foreach (var track in tape.tracks)
                if (track.type != SurvivorTrack.Id && StudioTrackTypes.Get(track.type) != null) live.record.Add(track.id);
            StudioRun.FrameShown += SampleAgain;
            StudioRun.Record(live);
            foreach (int wait in UntilHeld()) yield return wait;
            StudioRun.FrameShown -= SampleAgain;
            var secondWorld = tape.FindType(WorldTrack.Id)?.ActiveTake;
            int expected = -1;
            for (int f = 0; f < frames && expected < 0; f++)
                if (worldBefore.TryGetValue(f, out var a) && worldAfter.TryGetValue(f, out var b))
                    for (int i = 0; i < a.Length && i < b.Length; i++)
                        if (Vector3.Distance(a[i], b[i]) > .012f) { expected = f; break; }
            int reported = StudioTrackNotes.Of(adaTrack).staleFrame;
            var handTake = tape.Find(hand.id)?.ActiveTake;
            Result("5. Live-physics pass re-records the world; older survivor takes get the stale badge at the first difference",
                secondWorld != null && secondWorld != firstWorld && expected >= 0 && reported >= 0 && Mathf.Abs(reported - expected) <= 2 && adaTrack.badge.StartsWith("Stale"),
                $"World takes: {tape.FindType(WorldTrack.Id).takes.Count}. Hand bot take: {handTake?.frames.Count ?? 0} frames, the hand travelled {handMoved:F1} m. " +
                $"Comparing the two runs block by block, they first differ by more than 12 mm at frame {expected}; the Studio reported frame {reported}. " +
                $"Ada's badge: \"{adaTrack.badge}\". Tracks flagged stale: {tape.tracks.Count(t => StudioTrackNotes.Of(t).staleFrame >= 0)}.");

            // ---- Slow motion: the same jump at full and half speed ----
            var heights = new float[2]; var lengths = new int[2];
            for (int run2 = 0; run2 < 2; run2++)
            {
                performer.Reset();
                performer.script = t => new CastleBotCommand { jump = t >= 1.0f && t < 1.1f };
                var slow = PerformPass(stage, ben, false);
                slow.speed = run2 == 0 ? 1 : .5f; slow.to = 3 * fps;
                StudioRun.Record(slow);
                foreach (int wait in UntilHeld()) yield return wait;
                heights[run2] = performer.highest;
                lengths[run2] = tape.Find(ben.id).ActiveTake.frames.Count;
            }
            Result("Slow-motion performing: a jump at half world speed", heights[0] > .3f && Mathf.Abs(heights[1] - heights[0]) <= heights[0] * .15f + .05f && Mathf.Abs(lengths[1] - lengths[0]) <= 2,
                $"Jump height {heights[0]:F2} m at real speed and {heights[1]:F2} m at half speed. Both takes are stored at real speed: {lengths[0]} and {lengths[1]} frames for the same 3 s. " +
                "Climbing at reduced speed was not exercised by this script.");

            // ---- Test 4: the VR path without a headset ----
            StudioWristPanel.ForceVisible = true;
            performer.Reset();
            performer.script = t => default;
            var vr = PerformPass(stage, ada);
            vr.to = fps;
            int panelKeys = 0; bool pressed = false;
            void PressPanel(int frame)
            {
                var panel = StudioDrivers.Wrist;
                if (!panel.Built) return;
                panelKeys = panel.KeyCount;
                if (frame == fps / 2 && !pressed) { pressed = true; panel.PressForTest(4); }   // the slow-motion key
            }
            float speedBefore = StudioPerform.Speed;
            StudioRun.FrameShown += PressPanel;
            StudioRun.Record(vr);
            foreach (int wait in UntilHeld()) yield return wait;
            StudioRun.FrameShown -= PressPanel;
            StudioWristPanel.ForceVisible = false;
            bool panelWorked = panelKeys == 5 && pressed && !Mathf.Approximately(StudioPerform.Speed, speedBefore);
            StudioPerform.Speed = 1;
            var vrFrame = ActorFrame.Read(adaTrack.type, adaTrack.ActiveTake.frames[adaTrack.ActiveTake.frames.Count / 2]);
            bool headAndHands = vrFrame.visible && vrFrame.body.head.y > vrFrame.body.feet.y + .5f &&
                vrFrame.body.left.position != vrFrame.body.right.position && vrFrame.body.left.position != Vector3.zero;
            Result("4. A pass records head and hands; the wrist panel works (no headset in this run)", headAndHands && panelWorked,
                $"Recorded frame holds a head {vrFrame.body.head.y - vrFrame.body.feet.y:F2} m above the feet and two separate hand poses (left {vrFrame.body.left.position}, right {vrFrame.body.right.position}), " +
                $"so tracked head and hands are stored as they are. Wrist panel built with {panelKeys} keys; touching the slow-motion key changed the speed: {panelWorked}. " +
                "NOT covered here: the XR simulator and a real headset. This batch editor has no XR session, so the rig ran in desktop mode with the VR panel forced on.");

            StudioRun.LeaveBody();
            yield return 0;
            Result("Runtime errors during the Studio tests", exceptions == 0, exceptions == 0 && errorLines.Count == 0 ? "None." : $"{exceptions} exceptions. First messages: {string.Join(" // ", errorLines)}");
            StudioRun.SaveTape();
        }
    }
}
