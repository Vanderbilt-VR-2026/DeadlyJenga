using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DeadlyJenga.CandyAudio;
using DeadlyJenga.Core;
using DeadlyJenga.DevTools.TrailerStudio;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // The sound of a performed take: what the performer hears, taken from their own ears (the active
    // AudioListener) in real time. One mix, no stems. Game music is muted while it records, so the music
    // in the cut comes from the world take's sound only and is not heard twice.
    public static partial class StudioRun
    {
        static StudioEarTap earTap;
        static bool earTapAdded, performedRolling;
        static StudioWave.Writer performedWriter;
        static string performedFile = "", performedSetId = "";
        static float[] drained;
        static long tapCallbacksAtStart;
        static double performedTapeStart, performedWallStart;

        // How many times Unity handed the ears' sound over during the last performed take (0 = nothing was heard).
        public static long LastTapCallbacks { get; private set; }
        // The last performed take's sound: seconds recorded and seconds of tape it was meant to cover.
        public static double LastPerformedSeconds { get; private set; }
        public static double LastPerformedTapeSeconds { get; private set; }
        public static bool LastPerformedHasSound { get; private set; }
        // A short honest line about the last recording's sound ("" = nothing to say).
        public static string SoundNote { get; private set; } = "";

        static void StartPerformedSound(StudioStage stage)
        {
            StopPerformedSound();
            StudioRender.DropPerformedFile(stage.set.id);
            LastTapCallbacks = 0; LastPerformedHasSound = false; LastPerformedSeconds = LastPerformedTapeSeconds = 0;
            try
            {
                performedSetId = stage.set.id;
                performedFile = StudioRender.OpenPerformedFile(stage.set.id);
                performedRolling = false;
                PlaceTap(stage);
                FrameShown += PerformedSoundFrame;
            }
            catch (Exception error)
            {
                performedFile = "";
                Debug.LogWarning("[TrailerStudio] The performed take's sound could not be recorded: " + error.Message);
            }
        }

        // The tap must sit on the ears that are on now: the performer's rig listener.
        static void PlaceTap(StudioStage stage)
        {
            var listener = ActiveEars(stage);
            if (!listener || earTap && earTap.gameObject == listener.gameObject) return;
            RemoveTap();
            earTap = listener.GetComponent<StudioEarTap>();
            earTapAdded = !earTap;
            if (!earTap) { earTap = listener.gameObject.AddComponent<StudioEarTap>(); earTap.hideFlags = HideFlags.DontSave; }
            tapCallbacksAtStart = earTap.Callbacks;
            LastTapListener = listener.name;
        }

        // The name of the object whose ears the last performed take was recorded from.
        public static string LastTapListener { get; private set; } = "";

        static AudioListener ActiveEars(StudioStage stage)
        {
            var mark = stage.set.Mark(stage.performer);
            var rig = mark == null ? null : mark.role == StudioRole.Hand ? stage.session.Hand : stage.session.Player;
            var own = NetworkActors.Alive(rig) ? rig.gameObject.GetComponentsInChildren<AudioListener>(true).FirstOrDefault(l => l.isActiveAndEnabled) : null;
            return own ? own : Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).FirstOrDefault(l => l.isActiveAndEnabled);
        }

        static void RemoveTap()
        {
            if (earTap)
            {
                earTap.Recording = false;
                LastTapCallbacks += earTap.Callbacks - tapCallbacksAtStart;
                if (earTapAdded) Object.Destroy(earTap);
            }
            earTap = null; earTapAdded = false;
        }

        static void PerformedSoundFrame(int frame)
        {
            if (performedFile.Length == 0 || Stage == null) return;
            try
            {
                MuteMusic(true);
                if (!performedRolling)
                {
                    PlaceTap(Stage);
                    if (!earTap || !Recording || frame < recordFrom) return;
                    // Where the tape is right now. In real time it follows the game clock from the run's start.
                    performedTapeStart = firstFrame / (double)Stage.Fps + (Time.time - runStart);
                    performedWallStart = Time.realtimeSinceStartupAsDouble;
                    earTap.Recording = true;
                    performedRolling = true;
                    return;
                }
                if (!earTap) return;
                int count = earTap.Drain(ref drained);
                if (count == 0) return;
                if (performedWriter == null)
                {
                    // The first sound: now the channel count is known. Silence before it lines the file up with tape time 0.
                    int rate = AudioSettings.outputSampleRate;
                    performedWriter = new StudioWave.Writer(performedFile, earTap.Channels, rate);
                    performedWriter.Silence((long)Math.Round(performedTapeStart * rate));
                }
                performedWriter.Write(drained, count);
            }
            catch (Exception error)
            {
                Debug.LogWarning("[TrailerStudio] Recording the performed sound stopped: " + error.Message);
                FrameShown -= PerformedSoundFrame;
            }
        }

        // The take has finished: stop listening and finish the file (it is kept or dropped afterwards).
        static void StopPerformedSound()
        {
            FrameShown -= PerformedSoundFrame;
            MuteMusic(false);
            if (performedFile.Length == 0) { RemoveTap(); return; }
            try
            {
                if (earTap && performedWriter != null)
                {
                    int count = earTap.Drain(ref drained);
                    if (count > 0) performedWriter.Write(drained, count);
                }
                RemoveTap();
                if (performedWriter != null)
                {
                    LastPerformedSeconds = (performedWriter.Frames - Math.Round(performedTapeStart * performedWriter.rate)) / performedWriter.rate;
                    LastPerformedTapeSeconds = Math.Max(0, (capturedTo + 1) / (double)Stage.Fps - performedTapeStart);
                    performedWriter.Complete(Stage.Frames, Stage.Fps);
                    LastPerformedHasSound = true;
                    // Hitches and slow motion make the tape and the sound run apart; say how far.
                    double apart = LastPerformedSeconds - LastPerformedTapeSeconds;
                    if (Math.Abs(apart) > .1)
                        SoundNote = $"Sound: the performed sound lasts {LastPerformedSeconds:0.00} s for {LastPerformedTapeSeconds:0.00} s of tape" +
                            (currentPass != null && Mathf.Abs(currentPass.speed - 1) > .01f ? " (performed in slow motion, so the sound is slowed too)." : ", so it drifts from the picture.");
                }
                else if (performedRolling) SoundNote = "Sound: nothing was heard from the performer's ears (no audio callbacks), so this take has no sound.";
            }
            catch (Exception error) { Debug.LogWarning("[TrailerStudio] The performed sound could not be finished: " + error.Message); LastPerformedHasSound = false; }
            finally
            {
                performedWriter?.Dispose();
                performedWriter = null;
                performedRolling = false;
            }
            Debug.Log($"[TrailerStudio] Performed sound: {LastTapCallbacks} audio callbacks, {LastPerformedSeconds:0.00} s recorded for {LastPerformedTapeSeconds:0.00} s of tape, " +
                $"wall time {Time.realtimeSinceStartupAsDouble - performedWallStart:0.00} s.");
        }

        // Keep the finished file as this actor take's sound, or throw it away.
        static void KeepPerformedSound(StudioTake take, StudioTake lead, int punchFrame)
        {
            if (performedFile.Length == 0) return;
            string setId = performedSetId;
            performedFile = "";
            if (take == null || !LastPerformedHasSound) { StudioRender.DropPerformedFile(setId); LastPerformedHasSound = false; return; }
            string note = StudioRender.KeepPerformedFile(setId, take.id, lead?.id, punchFrame, Stage.Frames, Stage.Fps);
            if (note.Length > 0) SoundNote = (SoundNote + " " + note).Trim();
        }

        static void DropPerformedSound()
        {
            StopPerformedSound();
            if (performedSetId.Length > 0) StudioRender.DropPerformedFile(performedSetId);
            performedFile = "";
            LastPerformedHasSound = false;
        }

        // ---- Game music off while a performed take records ----

        static readonly HashSet<AudioSource> mutedMusic = new HashSet<AudioSource>();
        static readonly FieldInfo EngineVoices = typeof(CandyAudioEngine).GetField("voices", BindingFlags.Instance | BindingFlags.NonPublic);

        static void MuteMusic(bool on)
        {
            if (!on)
            {
                foreach (var source in mutedMusic) if (source) source.mute = false;
                mutedMusic.Clear();
                return;
            }
            var music = new HashSet<AudioSource>();
            var engine = CandyAudioEngine.Instance;
            if (engine && EngineVoices?.GetValue(engine) is IEnumerable voices)
                foreach (var voice in voices)
                {
                    var type = voice.GetType();
                    var source = type.GetField("source")?.GetValue(voice) as AudioSource;
                    var cue = type.GetField("cue")?.GetValue(voice) as CandyAudioCue;
                    if (source && cue != null && cue.bus == CandyBus.Music) music.Add(source);
                }
            foreach (var player in Object.FindObjectsByType<CandyHomeMusic>(FindObjectsSortMode.None))
                foreach (var source in player.GetComponentsInChildren<AudioSource>(true)) music.Add(source);
            foreach (var player in Object.FindObjectsByType<CandyLobbyMusic>(FindObjectsSortMode.None))
                foreach (var source in player.GetComponentsInChildren<AudioSource>(true)) music.Add(source);
            // A pooled engine voice that now plays something else gets its sound back.
            foreach (var source in mutedMusic.ToList())
                if (!music.Contains(source)) { if (source) source.mute = false; mutedMusic.Remove(source); }
            foreach (var source in music)
                if (!source.mute) { source.mute = true; mutedMusic.Add(source); }
        }
    }
}
