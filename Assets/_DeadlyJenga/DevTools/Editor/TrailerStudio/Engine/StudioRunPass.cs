using System;
using System.Collections.Generic;
using System.Linq;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // The frame loop: recording, playing and scrubbing. StudioDriverEarly/Late call in from the game loop.
    public static partial class StudioRun
    {
        sealed class ActiveRecorder
        {
            public StudioTrack track;
            public StudioRecorder recorder;
            public StudioTake take;
        }

        static readonly List<StudioPlayer> players = new List<StudioPlayer>();
        static readonly List<ActiveRecorder> recorders = new List<ActiveRecorder>();
        static readonly List<StudioCue> pendingCues = new List<StudioCue>();
        static bool running, fixedClock, stopAsked;
        static int firstFrame, lastFrame, capturedTo, recordFrom;
        static float runStart = -1;
        static StudioDrift drift;
        static StudioPass currentPass;
        static StudioTake leadTake;      // the performer's old take, followed until the punch-in frame

        public static bool RealTimeWarning { get; private set; }   // the last real-time run could not keep up
        public static int FirstFrame => firstFrame;
        public static int LastFrame => lastFrame;
        public static float Countdown { get; private set; }        // seconds left before recording starts (0 = none)
        public static StudioPass Pass => State == StudioState.Running ? currentPass : null;
        public static bool LeadIn => running && Recording && Frame < recordFrom;   // tape is rolling but not recording yet
        public static StudioPass LastPass { get; private set; }

        // ---- Public controls ----

        // Record every track the Set can record by itself: world, hazards, effects, sounds and every mark.
        public static void RecordWholeSet()
        {
            RequireHeld();
            var pass = new StudioPass { label = "Whole set", liveWorld = true, fixedClock = true };
            foreach (var track in Stage.tape.tracks)
                if (!track.locked && StudioTrackTypes.Get(track.type) != null) pass.record.Add(track.id);
            Record(pass);
        }

        public static void Record(StudioPass pass)
        {
            RequireHeld();
            foreach (string id in pass.record)
            {
                var track = Stage.tape.Find(id);
                if (track != null && track.locked) throw new InvalidOperationException(track.name + " is locked. Unlock it to record.");
            }
            State = StudioState.Running;
            stopAsked = false;
            LastPass = pass;
            routine = RecordRoutine(pass);
            Notify();
        }

        // realTime = play at the speed a viewer would see. Otherwise every frame is shown once (exact, used for checks and renders).
        public static void Play(int from = 0, int to = -1, bool realTime = true, float speed = 1)
        {
            RequireHeld();
            State = StudioState.Running;
            stopAsked = false;
            routine = PlayRoutine(from, to, realTime, speed);
            Notify();
        }

        public static void Stop()
        {
            if (State != StudioState.Running) return;
            stopAsked = true;
            running = false;
            Countdown = 0;
        }

        public static void Scrub(int frame)
        {
            if (State != StudioState.Held || Stage == null) return;
            Frame = Mathf.Clamp(frame, 0, Stage.Frames - 1);
            ShowFrame(Frame, false);
            Notify();
        }

        // Show a moment between two tape frames (4.5 = halfway from frame 4 to frame 5). The stage must be holding still.
        // Slow motion and renders use this, so any playback speed works after recording.
        public static void ShowExact(float frame)
        {
            if (State != StudioState.Held || Stage == null) return;
            frame = Mathf.Clamp(frame, 0, Stage.Frames - 1);
            int whole = Mathf.FloorToInt(frame);
            float part = frame - whole;
            Frame = whole;
            foreach (var player in players) player.ShowBetween(whole, part, false);
            foreach (var player in players) player.AfterShow();
        }

        // The Set or the tape's tracks changed (mute, solo, active take...): rebuild what is shown.
        public static void Refresh()
        {
            if (State != StudioState.Held || Stage == null) return;
            foreach (var type in StudioTrackTypes.All) type.EnsureTracks(Stage.set, Stage.tape);
            StudioEnvironment.Apply(Stage.set);
            Hold(Frame);
        }

        public static void SaveTape()
        {
            if (Stage?.tape == null) return;
            Stage.tape.Save(StudioLibrary.TapePath(Stage.set.id));
        }

        // Work on a different tape for the open Set (imports, alternate tapes).
        public static void UseTape(StudioTape tape)
        {
            RequireHeld();
            Stage.tape = tape;
            foreach (var type in StudioTrackTypes.All) type.EnsureTracks(Stage.set, tape);
            Hold(0);
        }

        // Step out of the actor you were playing, back to the director's view.
        public static void LeaveBody()
        {
            RequireHeld();
            StudioCast.LeaveBody(Stage);
            Hold(Frame);
        }

        static void RequireHeld()
        {
            if (State != StudioState.Held || Stage == null)
                throw new InvalidOperationException(State == StudioState.Closed ? "Open the stage first." : "Wait for the current run to finish.");
        }

        // In VR the headset sets the frame timing, so the fixed clock is never used there.
        static bool MustBeRealTime => Stage != null && Stage.session && Stage.session.IsVR;

        // A run ended early (stopped in the countdown, or it failed): take away what it had already put
        // on the stage, so no bot, stand-in or disaster is left next to the played-back copies.
        static void AbandonRun(bool liveWorld)
        {
            if (Stage == null) return;
            try
            {
                DisposeRecorders();
                if (liveWorld) DemoDisasters.StopAll(Stage.session);
                StudioDirector.End();
                StudioCast.ClearLive(Stage);
            }
            catch (Exception error) { Debug.LogException(error); }
        }

        // ---- Holding still ----

        static void Hold(int frame)
        {
            running = false; Recording = false; Countdown = 0;
            StudioPaths.Clear();
            Stage.physicalPlayback = false; Stage.recording = false;
            SetClock(Stage.Fps, !MustBeRealTime, 0);          // time stands still while held
            // An idle stage must not draw as fast as the graphics card allows (other editors share this PC).
            if (!MustBeRealTime) Application.targetFrameRate = Mathf.Max(30, Stage.Fps);
            RestoreInterpolation();
            BuildPlayers(Stage.performer.Length > 0 ? new[] { Stage.performer } : null, true);
            State = StudioState.Held;
            Frame = Mathf.Clamp(frame, 0, Stage.Frames - 1);
            ShowFrame(Frame, false);
            Notify();
        }

        static void BuildPlayers(ICollection<string> except, bool setWorld = false)
        {
            DisposePlayers();
            var tape = Stage.tape;
            bool anySolo = tape.tracks.Any(t => t.solo);
            bool worldPlayed = false;
            foreach (var type in StudioTrackTypes.All)
                foreach (var track in tape.tracks.Where(t => t.type == type.TypeId))
                {
                    if (except != null && except.Contains(track.id)) continue;
                    if (track.mode == TrackMode.Muted || anySolo && !track.solo) continue;
                    var take = track.ActiveTake;
                    if (take == null || take.frames.Count == 0) continue;
                    try
                    {
                        if (type.TypeId == WorldTrack.Id) { SetWorldKinematic(true); worldPlayed = true; }
                        var player = type.CreatePlayer(Stage, track, take);
                        if (player != null) players.Add(player);
                    }
                    catch (Exception error)
                    {
                        track.badge = "Could not play: " + error.Message;
                        Debug.LogWarning($"Trailer Studio: {track.name} could not be played. {error.Message}");
                    }
                }
            if (!worldPlayed && (except == null || setWorld)) SetWorldKinematic(false);
        }

        // The map's own multiplayer "replica" switch: kinematic blocks that something else moves.
        static void SetWorldKinematic(bool kinematic)
        {
            Stage.Synced?.SetReplica(kinematic);
            if (!kinematic || !Stage.physicalPlayback) return;
            // Smooth what the live player sees between physics steps. The old setting comes back in Hold.
            foreach (var body in Stage.blocks)
            {
                if (!body) continue;
                if (!oldInterpolation.ContainsKey(body)) oldInterpolation[body] = body.interpolation;
                body.interpolation = RigidbodyInterpolation.Interpolate;
            }
        }

        static readonly Dictionary<Rigidbody, RigidbodyInterpolation> oldInterpolation = new Dictionary<Rigidbody, RigidbodyInterpolation>();

        static void RestoreInterpolation()
        {
            foreach (var old in oldInterpolation) if (old.Key) old.Key.interpolation = old.Value;
            oldInterpolation.Clear();
        }

        static void DisposePlayers()
        {
            foreach (var player in players)
                try { player.Dispose(); } catch (Exception error) { Debug.LogException(error); }
            players.Clear();
        }

        static void DisposeRecorders()
        {
            foreach (var active in recorders)
                try { active.recorder.Dispose(); } catch (Exception error) { Debug.LogException(error); }
            recorders.Clear();
        }

        static void ShowFrame(int frame, bool playing)
        {
            // Hit pause (Phase 4): playback holds a contact for a few frames. Recording is never affected.
            if (!Recording) frame = StudioOverrides.Held(Stage.set, frame);
            foreach (var player in players) player.Show(frame, playing);
            foreach (var player in players) player.AfterShow();
        }

        // "Start where a previous moment ended": pose the live blocks like a frame of another Set's tape.
        static void ApplyStartState(StudioStage stage)
        {
            var set = stage.set;
            if (string.IsNullOrEmpty(set.startFromSet)) return;
            try
            {
                string file = StudioLibrary.TapePath(set.startFromSet);
                if (!StudioTape.IsTape(file)) throw new InvalidOperationException("that Set has no tape yet");
                var other = StudioTape.Load(file, new[] { WorldTrack.Id });
                var track = other.FindType(WorldTrack.Id);
                var take = track?.ActiveTake;
                if (take == null) throw new InvalidOperationException("that Set has no world take yet");
                using (var player = StudioTrackTypes.Get(WorldTrack.Id).CreatePlayer(stage, track, take))
                    player.Show(Mathf.Clamp(set.startFromFrame, take.start, take.End - 1), false);
                foreach (var body in stage.blocks)
                    if (body && !body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            }
            catch (Exception error)
            {
                Status = $"Could not start from \"{set.startFromSet}\": {error.Message}. The map starts as it loads.";
                Debug.LogWarning("Trailer Studio: " + Status);
            }
        }

        // ---- Recording ----

        const float PunchLeadSeconds = 2, JoinBlendSeconds = .2f;

        static IEnumerator<int> RecordRoutine(StudioPass pass)
        {
            var stage = Stage;
            var set = stage.set;
            currentPass = pass;
            int to = pass.to < 0 ? stage.Frames - 1 : Mathf.Min(pass.to, stage.Frames - 1);
            bool clock = pass.fixedClock && !MustBeRealTime;
            int loops = 0;

            while (true)
            {
                Status = pass.label + ": getting ready…";
                DisposePlayers();
                StudioCast.ClearLive(stage);
                CueLog.Clear();
                SetClock(stage.Fps, !MustBeRealTime, 1);

                StudioTake oldWorld = stage.tape.FindType(WorldTrack.Id)?.ActiveTake;
                bool branch = pass.liveWorld && pass.branchFrom > 0 && oldWorld != null && oldWorld.Covers(pass.branchFrom) && oldWorld.Covers(pass.branchFrom - 1);
                if (pass.liveWorld && pass.branchFrom > 0 && !branch)
                    Debug.LogWarning($"[TrailerStudio] {pass.label}: the world take does not reach frame {pass.branchFrom}, so this is a full re-record from a fresh map, not a branch.");
                bool branchRefused = pass.liveWorld && pass.branchFrom > 0 && !branch;
                if (branch)
                {
                    // Branch: the tape stays as it is before this frame; physics takes over from the tape's pose and speed.
                    // Physics runs before a frame is recorded, so the first new frame is the one after the branch frame.
                    pass.from = pass.branchFrom + 1; pass.preRoll = 0; pass.punchIn = false;
                    DemoDisasters.StopAll(stage.session);
                    // Until the tape rolls the world stays exactly on the branch frame (kinematic), so nothing drifts.
                    stage.Synced?.SetReplica(true);
                    using (var hold = StudioTrackTypes.Get(WorldTrack.Id).CreatePlayer(stage, stage.tape.FindType(WorldTrack.Id), oldWorld)) hold.Show(pass.branchFrom, false);
                }
                else if (pass.liveWorld)
                {
                    // A fresh map, so every take of the world starts from the same blocks.
                    DemoDisasters.StopAll(stage.session);
                    stage.session.ResetMap();
                    for (int n = 0; n < 3; n++) yield return 0;
                    stage.world = Services.World;
                    stage.RefreshBlocks();
                    stage.tape.header.blocks = stage.blocks.Length;
                    SetWorldKinematic(false);
                    ApplyStartState(stage);
                    StudioCast.Park(stage);
                    UnityEngine.Random.InitState(pass.seed >= 0 ? pass.seed : set.seed);
                    for (int n = 0; n < Mathf.CeilToInt(set.warmup * set.fps); n++) yield return 0;
                }

                // Who is live: bots and stand-ins for recorded marks, and the performer in the local player's body.
                // Whatever the map did while rebuilding, the Studio's own clock and camera must exist.
                StudioDrivers.Ensure();
                if (!StudioCamera.Camera) StudioCamera.Create(stage);
                StudioCast.SpawnLive(stage, pass);
                Debug.Log($"[TrailerStudio] {pass.label}: {stage.blocks.Length} blocks, {stage.live.Count} live, live world {pass.liveWorld}");
                var performer = string.IsNullOrEmpty(pass.performer) ? null : set.Mark(pass.performer);
                if (performer != null) StudioCast.Perform(stage, performer);
                else if (stage.performer.Length > 0) StudioCast.LeaveBody(stage);
                stage.recording = true;
                stage.physicalPlayback = !pass.liveWorld && stage.live.Count > 0;
                BuildPlayers(pass.record, !branch);
                StudioPaths.Show(stage, pass);

                // Punch-in: follow the performer's old take up to the punch frame, then hand over control.
                leadTake = null;
                int lead = Mathf.Max(0, pass.preRoll);
                if (pass.punchIn && performer != null)
                {
                    leadTake = stage.tape.Find(performer.id)?.ActiveTake;
                    if (leadTake == null || !leadTake.Covers(Mathf.Max(0, pass.from - 1))) leadTake = null;
                    else lead = Mathf.Max(lead, Mathf.RoundToInt(PunchLeadSeconds * stage.Fps));
                }
                int startFrame = Mathf.Max(0, pass.from - lead);
                ShowFrame(startFrame, false);
                if (leadTake != null) StudioCast.Follow(stage, performer, leadTake, startFrame);
                else if (performer != null) StudioCast.PlaceOnMark(stage, performer);
                yield return 0;   // let new characters finish their first frame

                // 3, 2, 1: time stands still while the performer gets ready.
                if (pass.countdown > 0 && performer != null)
                {
                    SetClock(stage.Fps, false, 0);
                    StudioCast.SuspendInput(stage, true);
                    float end = Time.unscaledTime + pass.countdown;
                    while (Time.unscaledTime < end && !stopAsked)
                    {
                        Countdown = end - Time.unscaledTime;
                        Notify();
                        yield return 0;
                    }
                    Countdown = 0;
                    if (stopAsked) { AbandonRun(pass.liveWorld); break; }
                }
                if (performer != null) StudioCast.SuspendInput(stage, leadTake != null);

                DisposeRecorders();
                foreach (string id in pass.record)
                {
                    var track = stage.tape.Find(id);
                    var type = track == null ? null : StudioTrackTypes.Get(track.type);
                    var recorder = type?.CreateRecorder(stage, track);
                    if (recorder == null) continue;
                    recorders.Add(new ActiveRecorder { track = track, recorder = recorder,
                        take = new StudioTake { start = pass.from, keyEvery = recorder.KeyEvery } });
                }
                pendingCues.Clear();
                // Cues that wait for another cue (chain reactions) are started by the director, not by the clock.
                if (pass.liveWorld) pendingCues.AddRange(set.cues.Where(c => string.IsNullOrEmpty(c.link) && c.at >= startFrame / (float)stage.Fps - .0001f).OrderBy(c => c.at));
                StudioDirector.Begin(stage, startFrame / (float)stage.Fps);

                if (branch)
                {
                    // Hand the blocks to physics at the last possible moment, with the tape's pose and speed.
                    StudioBranch.StartFromTape(stage, oldWorld, pass.branchFrom);
                    UnityEngine.Random.InitState(pass.seed >= 0 ? pass.seed : set.seed);
                }
                Begin(startFrame, to, clock, pass.speed, null);
                recordFrom = pass.from; capturedTo = pass.from - 1;
                Recording = true;
                Status = pass.label + ": recording.";
                Notify();
                Debug.Log($"[TrailerStudio] {pass.label}: rolling from frame {startFrame} to {to}, {recorders.Count} recorders, fixed clock {clock}");
                foreach (int wait in WaitForRun()) yield return wait;

                // Keep every take, even a bad one. The newest becomes the active take.
                Recording = false;
                int kept = 0;
                StudioTake newWorld = null;
                long stamp = DateTime.UtcNow.Ticks;   // every take of this pass carries the same time, so they can be switched together
                foreach (var active in recorders)
                {
                    if (active.take.frames.Count == 0) continue;
                    var take = active.take;
                    take.meta = active.recorder.Meta;
                    if (branch) StudioBranch.KeepEarlierFrames(active.track, take, pass);
                    bool actor = active.track.type == SurvivorTrack.Id || active.track.type == HandTrack.Id;
                    if (actor && leadTake != null && active.track.id == pass.performer)
                        StudioTakes.JoinAfterPunch(active.track.type, leadTake, take, Mathf.RoundToInt(JoinBlendSeconds * stage.Fps));
                    take.created = stamp;
                    take.name = "Take " + (active.track.takes.Count + 1);
                    active.track.takes.Add(take);
                    active.track.active = active.track.takes.Count - 1;
                    active.track.badge = "";
                    StudioTrackNotes.Update(active.track, n => n.staleFrame = -1);
                    if (active.track.type == WorldTrack.Id) newWorld = take;
                    kept++;
                }
                // Remember which world each actor take was performed against, and flag takes the new world made stale.
                var world = newWorld ?? oldWorld;
                foreach (var active in recorders)
                    if ((active.track.type == SurvivorTrack.Id || active.track.type == HandTrack.Id) && active.take.frames.Count > 0)
                        active.take.meta = StudioTakes.WorldMeta(world?.id ?? "");
                if (newWorld != null) StudioTakes.MarkStale(stage.tape, newWorld, stage.Fps, recorders.Select(r => r.track.id).ToList());
                DisposeRecorders();
                if (pass.liveWorld) DemoDisasters.StopAll(stage.session);
                StudioDirector.End();
                if (newWorld != null) StudioBranch.Describe(stage, newWorld, oldWorld, pass, branch);
                StudioCast.ClearLive(stage);
                stage.tape.header.frames = stage.Frames;
                SaveTape();
                loops++;
                Status = $"{pass.label}: recorded {kept} tracks, {Mathf.Max(0, capturedTo - pass.from + 1)} frames" + (loops > 1 ? $" (loop {loops})" : "") +
                    $". Tape is {StudioTape.Size(new System.IO.FileInfo(stage.tape.path).Length)}." +
                    (branchRefused ? " Note: the old world take did not reach the branch point, so the whole world was re-recorded from a fresh map." : "");
                Debug.Log("[TrailerStudio] " + Status);
                // Loop record: go again with a new take until someone presses stop.
                if (!pass.loop || stopAsked) break;
            }
            leadTake = null;
            string done = Status;
            Hold(pass.from);
            Status = done;
            Notify();
        }

        // ---- Playing ----

        static IEnumerator<int> PlayRoutine(int from, int to, bool realTime, float speed)
        {
            var stage = Stage;
            to = to < 0 ? stage.Frames - 1 : Mathf.Min(to, stage.Frames - 1);
            StudioCast.ClearLive(stage);
            // In the headset the person stays in their body while the tape plays (they may be filming it by hand).
            if (!MustBeRealTime) StudioCast.LeaveBody(stage);
            stage.physicalPlayback = false;
            BuildPlayers(null);
            var report = new StudioDrift();
            Begin(from, to, !realTime && !MustBeRealTime, speed, report);
            recordFrom = from;
            Status = "Playing.";
            Notify();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            foreach (int wait in WaitForRun()) yield return wait;
            LastDrift = report;
            // Honest status: say when a real-time playback could not keep up.
            float expected = (Frame - from + 1) / (float)stage.Fps / Mathf.Max(.01f, speed);
            RealTimeWarning = realTime && watch.Elapsed.TotalSeconds > expected * 1.25f + .5f;
            int stoppedAt = Frame;
            Hold(stoppedAt);
            Status = RealTimeWarning ? "Played slower than real time on this machine. " + report : report.ToString();
            Notify();
        }

        // Wait for the frame loop to finish. If the tape stops moving for 20 real seconds, say why instead of hanging.
        static IEnumerable<int> WaitForRun()
        {
            int seen = Frame; float since = Time.realtimeSinceStartup;
            while (running)
            {
                if (Frame != seen || Countdown > 0) { seen = Frame; since = Time.realtimeSinceStartup; }
                else if (Time.realtimeSinceStartup - since > 20)
                    throw new TimeoutException($"The tape stopped at frame {Frame}. Driver alive: {StudioDrivers.Alive}, " +
                        $"time scale {Time.timeScale}, started {runStart >= 0}, editor paused {UnityEditor.EditorApplication.isPaused}.");
                yield return 0;
            }
        }

        static void Begin(int from, int to, bool useFixedClock, float speed, StudioDrift report)
        {
            firstFrame = from; lastFrame = to; capturedTo = from - 1; recordFrom = from;
            fixedClock = useFixedClock; drift = report;
            runStart = -1;
            Frame = from;
            SetClock(Stage.Fps, useFixedClock, Mathf.Clamp(speed, .05f, 4));
            running = true;
        }

        // ---- Called by the drivers, inside the game loop ----

        internal static void FixedStep()
        {
            if (!running || runStart < 0 || Stage == null) return;
            float time = firstFrame / (float)Stage.Fps + (Time.fixedTime - runStart);
            foreach (var player in players) player.FixedStep(time);
            if (Recording && currentPass != null && currentPass.liveWorld) StudioDirector.FixedTick(Stage, CueLog);
        }

        internal static void FrameStart()
        {
            // The game switches listeners off when it changes roles. Keep one set of ears on:
            // the director's camera, unless someone is performing (then the player's own).
            if (Stage != null)
            {
                StudioCamera.Listen(Stage.performer.Length == 0);
                if (Stage.performer.Length == 0) StudioCamera.PlaceEars(Stage);
            }
            if (!running || Stage == null) return;
            if (runStart < 0) { runStart = Time.time; Frame = firstFrame; }
            else if (fixedClock) Frame++;
            else Frame = firstFrame + Mathf.FloorToInt((Time.time - runStart) * Stage.Fps + .0001f);
            if (Frame > lastFrame) Frame = lastFrame;

            float time = Frame / (float)Stage.Fps;
            while (pendingCues.Count > 0 && pendingCues[0].at <= time + .00001f)
            {
                var cue = pendingCues[0];
                pendingCues.RemoveAt(0);
                StudioDirector.Tick(Stage, time, CueLog);   // the director's clock must be at this cue's time
                CueLog.Add($"{time:F3}s {StudioCues.Run(Stage, cue)}");
                StudioDirector.Triggered(Stage, cue, CueLog);
            }
            if (Recording && currentPass != null)
            {
                if (currentPass.liveWorld) StudioDirector.Tick(Stage, time, CueLog);
                // Slow-time cues: only while performing in real time (a fixed clock has no speed to change).
                if (!fixedClock) Time.timeScale = Mathf.Clamp(currentPass.speed, .05f, 4) * StudioDirector.SlowFactor(time);
            }
            ShowFrame(Frame, true);
            StudioCast.SimulateStandIns(Stage);

            // Punch-in lead: the performer rides along their old take, then gets control at the punch frame.
            if (leadTake != null && currentPass != null && Recording)
            {
                var mark = Stage.set.Mark(currentPass.performer);
                if (mark != null && Frame < recordFrom) StudioCast.Follow(Stage, mark, leadTake, Frame);
                else { StudioCast.SuspendInput(Stage, false); }
            }
        }

        internal static void FrameEnd()
        {
            if (!running || Stage == null || runStart < 0) return;
            if (Recording && Frame >= recordFrom)
            {
                // In real time a slow rendered frame can span several tape frames; fill each one.
                for (int frame = Mathf.Max(capturedTo + 1, recordFrom); frame <= Frame; frame++)
                    foreach (var active in recorders)
                    {
                        int index = frame - active.take.start;
                        active.take.frames.Add(active.recorder.Capture(frame, index == 0 || frame % active.take.keyEvery == 0));
                    }
                capturedTo = Frame;
            }
            if (drift != null) foreach (var player in players) player.CheckDrift(Frame, drift);
            try { FrameShown?.Invoke(Frame); } catch (Exception error) { Debug.LogException(error); }
            if (Frame >= lastFrame) running = false;
            Notify();
        }
    }
}
