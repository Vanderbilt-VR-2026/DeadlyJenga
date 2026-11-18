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
        static bool running, fixedClock;
        static int firstFrame, lastFrame, capturedTo;
        static float runStart = -1;
        static StudioDrift drift;

        public static bool RealTimeWarning { get; private set; }   // the last real-time run could not keep up
        public static int FirstFrame => firstFrame;
        public static int LastFrame => lastFrame;

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
            routine = RecordRoutine(pass);
            Notify();
        }

        // realTime = play at the speed a viewer would see. Otherwise every frame is shown once (exact, used for checks and renders).
        public static void Play(int from = 0, int to = -1, bool realTime = true, float speed = 1)
        {
            RequireHeld();
            State = StudioState.Running;
            routine = PlayRoutine(from, to, realTime, speed);
            Notify();
        }

        public static void Stop()
        {
            if (State == StudioState.Running) running = false;
        }

        public static void Scrub(int frame)
        {
            if (State != StudioState.Held || Stage == null) return;
            Frame = Mathf.Clamp(frame, 0, Stage.Frames - 1);
            ShowFrame(Frame, false);
            Notify();
        }

        // The Set or the tape's tracks changed (mute, solo, active take...): rebuild what is shown.
        public static void Refresh()
        {
            if (State != StudioState.Held || Stage == null) return;
            foreach (var type in StudioTrackTypes.All) type.EnsureTracks(Stage.set, Stage.tape);
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

        static void RequireHeld()
        {
            if (State != StudioState.Held || Stage == null)
                throw new InvalidOperationException(State == StudioState.Closed ? "Open the stage first." : "Wait for the current run to finish.");
        }

        // ---- Holding still ----

        static void Hold(int frame)
        {
            running = false; Recording = false;
            Stage.physicalPlayback = false; Stage.recording = false;
            SetClock(Stage.Fps, true, 0);          // time stands still while held
            BuildPlayers(null);
            State = StudioState.Held;
            Frame = Mathf.Clamp(frame, 0, Stage.Frames - 1);
            ShowFrame(Frame, false);
            Notify();
        }

        static void BuildPlayers(ICollection<string> except)
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
            if (!worldPlayed && except == null) SetWorldKinematic(false);
        }

        // The map's own multiplayer "replica" switch: kinematic blocks that something else moves.
        static void SetWorldKinematic(bool kinematic)
        {
            Stage.Synced?.SetReplica(kinematic);
            if (!kinematic || !Stage.physicalPlayback) return;
            // Smooth what the live player sees between physics steps.
            foreach (var body in Stage.blocks) if (body) body.interpolation = RigidbodyInterpolation.Interpolate;
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

        static IEnumerator<int> RecordRoutine(StudioPass pass)
        {
            var stage = Stage;
            var set = stage.set;
            int to = pass.to < 0 ? stage.Frames - 1 : Mathf.Min(pass.to, stage.Frames - 1);
            Status = pass.label + ": getting ready…";
            DisposePlayers();
            StudioCast.ClearLive(stage);
            CueLog.Clear();
            SetClock(stage.Fps, true, 1);

            if (pass.liveWorld)
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
                UnityEngine.Random.InitState(set.seed);
                for (int n = 0; n < Mathf.CeilToInt(set.warmup * set.fps); n++) yield return 0;
            }

            StudioCast.SpawnLive(stage, pass);
            stage.recording = true;
            stage.physicalPlayback = !pass.liveWorld && stage.live.Count > 0;
            BuildPlayers(pass.record);
            yield return 0;   // let new characters finish their first frame

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
            if (pass.liveWorld) pendingCues.AddRange(set.cues.OrderBy(c => c.at));

            Begin(pass.from, to, pass.fixedClock, pass.speed, null);
            Recording = true;
            Status = pass.label + ": recording.";
            Notify();
            while (running) yield return 0;

            // Keep every take, even a bad one. The newest becomes the active take.
            Recording = false;
            int kept = 0;
            foreach (var active in recorders)
            {
                if (active.take.frames.Count == 0) continue;
                active.take.meta = active.recorder.Meta;
                active.take.name = "Take " + (active.track.takes.Count + 1);
                active.track.takes.Add(active.take);
                active.track.active = active.track.takes.Count - 1;
                active.track.badge = "";
                kept++;
            }
            DisposeRecorders();
            if (pass.liveWorld) DemoDisasters.StopAll(stage.session);
            StudioCast.ClearLive(stage);
            stage.tape.header.frames = stage.Frames;
            SaveTape();
            Hold(pass.from);
            Status = $"{pass.label}: recorded {kept} tracks, {capturedTo - pass.from + 1} frames. Tape is {StudioTape.Size(new System.IO.FileInfo(stage.tape.path).Length)}.";
            Notify();
        }

        // ---- Playing ----

        static IEnumerator<int> PlayRoutine(int from, int to, bool realTime, float speed)
        {
            var stage = Stage;
            to = to < 0 ? stage.Frames - 1 : Mathf.Min(to, stage.Frames - 1);
            StudioCast.ClearLive(stage);
            stage.physicalPlayback = false;
            BuildPlayers(null);
            var report = new StudioDrift();
            Begin(from, to, !realTime, speed, report);
            Status = "Playing.";
            Notify();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (running) yield return 0;
            LastDrift = report;
            // Honest status: say when a real-time playback could not keep up.
            float expected = (Frame - from + 1) / (float)stage.Fps / Mathf.Max(.01f, speed);
            RealTimeWarning = realTime && watch.Elapsed.TotalSeconds > expected * 1.25f + .5f;
            int stoppedAt = Frame;
            Hold(stoppedAt);
            Status = RealTimeWarning ? "Played slower than real time on this machine. " + report : report.ToString();
            Notify();
        }

        static void Begin(int from, int to, bool useFixedClock, float speed, StudioDrift report)
        {
            firstFrame = from; lastFrame = to; capturedTo = from - 1;
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
        }

        internal static void FrameStart()
        {
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
                CueLog.Add($"{time:F3}s {StudioCues.Run(Stage, cue)}");
            }
            ShowFrame(Frame, true);
            StudioCast.SimulateStandIns(Stage);
        }

        internal static void FrameEnd()
        {
            if (!running || Stage == null || runStart < 0) return;
            if (Recording)
            {
                // In real time a slow rendered frame can span several tape frames; fill each one.
                for (int frame = capturedTo + 1; frame <= Frame; frame++)
                    foreach (var active in recorders)
                    {
                        int index = frame - active.take.start;
                        active.take.frames.Add(active.recorder.Capture(frame, index % active.take.keyEvery == 0));
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
