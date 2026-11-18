using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Performing: you play one actor while everything already recorded plays back around you.
    // The Perform tab, the in-game keys and the VR wrist panel all call the methods here.
    public static class StudioPerform
    {
        const string Key = "DeadlyJenga.TrailerStudio.Perform.";
        static bool GetBool(string name, bool fallback) => SessionState.GetBool(Key + name, fallback);
        static void SetBool(string name, bool value) => SessionState.SetBool(Key + name, value);

        // ---- Choices (kept for the editor session) ----
        public static string Actor { get => SessionState.GetString(Key + "Actor", ""); set => SessionState.SetString(Key + "Actor", value ?? ""); }
        public static bool Countdown { get => GetBool("Countdown", true); set => SetBool("Countdown", value); }
        public static float PreRoll { get => SessionState.GetFloat(Key + "PreRoll", 0); set => SessionState.SetFloat(Key + "PreRoll", Mathf.Clamp(value, 0, 10)); }
        public static float Speed { get => SessionState.GetFloat(Key + "Speed", 1); set => SessionState.SetFloat(Key + "Speed", Mathf.Clamp(value, .25f, 1)); }
        public static bool Loop { get => GetBool("Loop", false); set => SetBool("Loop", value); }
        public static bool LivePhysics { get => GetBool("LivePhysics", false); set => SetBool("LivePhysics", value); }
        public static bool Paths { get => GetBool("Paths", true); set => SetBool("Paths", value); }
        public static bool PunchIn { get => GetBool("PunchIn", false); set => SetBool("PunchIn", value); }
        public static bool GhostsHidden { get => GetBool("GhostsHidden", false); set => SetBool("GhostsHidden", value); }

        static Action pending;            // something to do as soon as the current run has stopped
        static bool hooked;

        public static StudioMark ActorMark(StudioSet set) => set?.Mark(Actor);

        // The take number the next recording of this actor will get.
        public static int NextTake(StudioTape tape, StudioMark mark) => (tape?.Find(mark.id)?.takes.Count ?? 0) + 1;

        public static bool CanPunch(StudioTape tape, StudioMark mark, int frame)
        {
            var take = tape?.Find(mark.id)?.ActiveTake;
            return take != null && frame > take.start && take.Covers(frame - 1);
        }

        public static StudioPass BuildPass(StudioStage stage, int playhead)
        {
            var set = stage.set;
            var mark = ActorMark(set) ?? throw new InvalidOperationException("Pick who to play first (click an actor in the list).");
            if (mark.role == StudioRole.Hand && set.handBot)
                throw new InvalidOperationException("This Set's hand is flown by the hand bot. Switch it to \"Performed\" in the Perform tab to fly it yourself.");
            bool punch = PunchIn && CanPunch(stage.tape, mark, playhead);
            var pass = new StudioPass
            {
                label = (punch ? "Punch in " : "Record ") + mark.actor,
                performer = mark.id, fixedClock = false, speed = Speed, loop = Loop && !punch,
                countdown = Countdown ? 3 : 0, preRoll = Mathf.RoundToInt(PreRoll * set.fps),
                punchIn = punch, from = punch ? playhead : 0, paths = Paths, liveWorld = LivePhysics
            };
            pass.record.Add(mark.id);
            if (LivePhysics)
            {
                // This actor changes the world (pulls, punches, explosions): simulate it and record it as a new world take.
                pass.from = 0; pass.punchIn = false;
                foreach (var track in stage.tape.tracks)
                    if (!track.locked && track.type != SurvivorTrack.Id && track.type != HandTrack.Id && StudioTrackTypes.Get(track.type) != null)
                        pass.record.Add(track.id);
            }
            return pass;
        }

        public static void Record(int playhead)
        {
            if (StudioRun.State != StudioState.Held) throw new InvalidOperationException("Open the stage to record.");
            StudioRun.Record(BuildPass(StudioRun.Stage, playhead));
        }

        // Stop what is rolling (the take is kept) and go again from the same place.
        public static void Retake()
        {
            var last = StudioRun.LastPass;
            if (last == null || string.IsNullOrEmpty(last.performer)) { WhenHeld(() => Record(0)); return; }
            WhenHeld(() => StudioRun.Record(Copy(last)));
        }

        // "Back 3 s and punch in": keep the take just made up to three seconds before where it stopped.
        public static void PunchBack(float seconds = 3)
        {
            int stoppedAt = StudioRun.Frame;
            WhenHeld(() =>
            {
                var stage = StudioRun.Stage;
                var mark = ActorMark(stage.set) ?? throw new InvalidOperationException("Pick who to play first.");
                int frame = Mathf.Max(1, stoppedAt - Mathf.RoundToInt(seconds * stage.Fps));
                if (!CanPunch(stage.tape, mark, frame)) throw new InvalidOperationException("There is no take to punch into yet.");
                var pass = BuildPass(stage, frame);
                pass.punchIn = true; pass.from = frame; pass.loop = false; pass.liveWorld = false;
                pass.record.Clear(); pass.record.Add(mark.id);
                pass.label = "Punch in " + mark.actor;
                StudioRun.Record(pass);
            });
        }

        static StudioPass Copy(StudioPass pass)
        {
            var copy = new StudioPass { label = pass.label, fixedClock = pass.fixedClock, from = pass.from, to = pass.to, liveWorld = pass.liveWorld,
                speed = Speed, performer = pass.performer, preRoll = pass.preRoll, countdown = pass.countdown, punchIn = pass.punchIn, loop = pass.loop, paths = pass.paths };
            copy.record.AddRange(pass.record);
            return copy;
        }

        // Run "action" now if the stage is holding still, otherwise stop the run and do it right after.
        static void WhenHeld(Action action)
        {
            if (!hooked) { StudioRun.Changed += Drain; hooked = true; }
            pending = action;
            if (StudioRun.State == StudioState.Running) StudioRun.Stop();
            Drain();
        }

        static void Drain()
        {
            if (pending == null || StudioRun.State != StudioState.Held) return;
            var action = pending; pending = null;
            // Wait one editor tick so the finished run has fully settled.
            EditorApplication.delayCall += () =>
            {
                try { if (StudioRun.State == StudioState.Held) action(); }
                catch (Exception error) { Debug.LogWarning("Trailer Studio: " + error.Message); }
            };
        }

        // Cycle slow motion: full speed, half, quarter.
        public static void CycleSpeed() => Speed = Speed > .75f ? .5f : Speed > .4f ? .25f : 1;

        public static string SpeedName => Speed > .75f ? "1×" : Speed > .4f ? "½×" : "¼×";

        // Show or hide every played-back actor for the performer (the wrist panel's "ghosts" button).
        public static void ToggleGhosts()
        {
            GhostsHidden = !GhostsHidden;
            var tape = StudioRun.Stage?.tape;
            if (tape == null) return;
            foreach (var track in tape.tracks.Where(t => t.type == SurvivorTrack.Id || t.type == HandTrack.Id))
                StudioTrackNotes.Update(track, n => n.ghost = GhostsHidden ? 2 : 0);
        }
    }
}
