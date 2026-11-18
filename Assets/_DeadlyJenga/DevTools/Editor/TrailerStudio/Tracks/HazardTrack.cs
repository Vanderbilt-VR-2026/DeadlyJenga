using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Disasters. Every hazard already describes itself each frame for multiplayer guests
    // (DisasterStates and the wave frame). The tape stores those descriptions, and playback
    // publishes them again, so the same visuals and sounds appear without the hazard running.
    public sealed class HazardTrack : IStudioTrackType
    {
        public const string Id = "hazards";
        public string TypeId => Id;
        public string Label => "Hazards";
        public Color Color => StudioColors.Hazard;
        public int Order => 10;

        public void EnsureTracks(StudioSet set, StudioTape tape) => tape.GetOrAdd("hazards", Id, "Hazards");
        public StudioRecorder CreateRecorder(StudioStage stage, StudioTrack track) => new Recorder();
        public StudioPlayer CreatePlayer(StudioStage stage, StudioTrack track, StudioTake take) => new Player(take);

        [Serializable] sealed class Frame
        {
            public WaveFrame wave;
            public DisasterFrame[] disasters = Array.Empty<DisasterFrame>();
        }

        // A frame that only holds a wave (used to convert old Theatre tapes).
        public static byte[] Pack(WaveFrame wave) =>
            wave.active ? Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Frame { wave = wave })) : Array.Empty<byte>();

        sealed class Recorder : StudioRecorder
        {
            public override byte[] Capture(int frame, bool key)
            {
                var sample = new Frame();
                foreach (var source in Tracked.Waves)
                    if (source.Wave.active) { sample.wave = source.Wave; break; }
                var list = new List<DisasterFrame>();
                foreach (var source in DisasterStates.Active)
                {
                    var state = source.Capture();
                    if (state != null && list.Count < 32) list.Add(state);
                }
                sample.disasters = list.ToArray();
                // Most frames have no hazard at all; store nothing for those.
                if (!sample.wave.active && list.Count == 0) return Array.Empty<byte>();
                return Encoding.UTF8.GetBytes(JsonUtility.ToJson(sample));
            }
        }

        sealed class RecordedWave : IWaveState
        {
            public WaveFrame frame;
            public WaveFrame Wave => frame;
        }

        sealed class RecordedDisaster : IDisasterState
        {
            public DisasterFrame frame;
            public DisasterFrame Capture() => frame;
        }

        sealed class Player : StudioPlayer
        {
            readonly StudioTake take;
            readonly RecordedWave wave = new RecordedWave();
            readonly List<RecordedDisaster> shown = new List<RecordedDisaster>();

            public Player(StudioTake take) { this.take = take; }

            public override void Show(int frame, bool running)
            {
                Clear();
                if (!take.Covers(frame)) return;
                byte[] data = take.frames[frame - take.start];
                if (data == null || data.Length == 0) return;
                var sample = JsonUtility.FromJson<Frame>(Encoding.UTF8.GetString(data));
                if (sample == null) return;
                wave.frame = sample.wave;
                if (sample.wave.active && !Tracked.Waves.Contains(wave)) Tracked.Waves.Add(wave);
                foreach (var disaster in sample.disasters ?? Array.Empty<DisasterFrame>())
                {
                    if (disaster == null) continue;
                    disaster.sampledAt = Time.time;
                    var source = new RecordedDisaster { frame = disaster };
                    shown.Add(source);
                    DisasterStates.Active.Add(source);
                }
            }

            void Clear()
            {
                Tracked.Waves.Remove(wave);
                wave.frame = default;
                foreach (var source in shown) DisasterStates.Active.Remove(source);
                shown.Clear();
            }

            public override void Dispose() => Clear();
        }
    }
}
