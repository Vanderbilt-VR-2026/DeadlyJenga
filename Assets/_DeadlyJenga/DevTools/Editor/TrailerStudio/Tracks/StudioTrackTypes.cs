using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // A kind of track. To add one, write a class that implements this in a NEW file; the Studio
    // finds it by itself (see progress/EXTENSION-POINTS.md). Nothing else needs editing.
    public interface IStudioTrackType
    {
        string TypeId { get; }            // written into the tape; never change it once tapes exist
        string Label { get; }             // group name in the left list
        Color Color { get; }
        int Order { get; }                // lower runs first each frame (effects before actors)

        // Add the tracks this type wants for this Set (for example one per mark). Never remove tracks here.
        void EnsureTracks(StudioSet set, StudioTape tape);

        // Null when there is nothing live to record for this track right now.
        StudioRecorder CreateRecorder(StudioStage stage, StudioTrack track);
        StudioPlayer CreatePlayer(StudioStage stage, StudioTrack track, StudioTake take);
    }

    // Writes one track while the game runs.
    public abstract class StudioRecorder : IDisposable
    {
        public virtual int KeyEvery => 1;
        public virtual string Meta => "";
        // The bytes for this frame. key = this frame must stand alone (no delta from the one before).
        public abstract byte[] Capture(int frame, bool key);
        public virtual void Dispose() { }
    }

    // Shows one take of one track.
    public abstract class StudioPlayer : IDisposable
    {
        // Show this exact tape frame. running = the tape is playing (false while scrubbing or paused).
        public abstract void Show(int frame, bool running);
        // Called every physics step while playing. time is tape time in seconds.
        public virtual void FixedStep(float time) { }
        // Called after every track has shown the frame.
        public virtual void AfterShow() { }
        // Compare what is in the scene now with the tape and add any mismatch to the report.
        public virtual void CheckDrift(int frame, StudioDrift report) { }
        public virtual void Dispose() { }
    }

    public static class StudioTrackTypes
    {
        static List<IStudioTrackType> all;

        public static IReadOnlyList<IStudioTrackType> All
        {
            get
            {
                if (all == null)
                    all = StudioRegistry.Create<IStudioTrackType>().OrderBy(t => t.Order).ToList();
                return all;
            }
        }

        public static IStudioTrackType Get(string typeId) => All.FirstOrDefault(t => t.TypeId == typeId);
        public static ICollection<string> KnownIds => All.Select(t => t.TypeId).ToList();
    }

    // Finds every class that implements one of the Studio's extension interfaces.
    public static class StudioRegistry
    {
        public static List<T> Create<T>() where T : class
        {
            var found = new List<T>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<T>())
            {
                if (type.IsAbstract || type.IsInterface || type.GetConstructor(Type.EmptyTypes) == null) continue;
                try { found.Add((T)Activator.CreateInstance(type)); }
                catch (Exception error) { Debug.LogWarning("Trailer Studio could not create " + type.Name + ": " + error.Message); }
            }
            return found;
        }
    }

    // What a playback run found when it compared the scene with the tape.
    public sealed class StudioDrift
    {
        public int samples, blocksOff;
        public float worstMillimetres, worstDegrees;
        public int worstFrame = -1;
        public string worstBlock = "";
        public const float AllowedMillimetres = 1, AllowedDegrees = .1f;

        public bool Passed => worstMillimetres <= AllowedMillimetres && worstDegrees <= AllowedDegrees;

        public void Add(int frame, string what, float millimetres, float degrees)
        {
            samples++;
            if (millimetres > AllowedMillimetres || degrees > AllowedDegrees) blocksOff++;
            if (millimetres <= worstMillimetres && degrees <= worstDegrees) return;
            worstMillimetres = Mathf.Max(worstMillimetres, millimetres);
            worstDegrees = Mathf.Max(worstDegrees, degrees);
            worstFrame = frame; worstBlock = what;
        }

        public override string ToString() => samples == 0 ? "Drift check: nothing compared." :
            $"Drift check: {samples} samples, worst {worstMillimetres:F3} mm and {worstDegrees:F4} degrees" +
            (worstFrame >= 0 ? $" ({worstBlock}, frame {worstFrame})" : "") + (Passed ? ". OK." : $". {blocksOff} samples over the limit.");
    }
}
