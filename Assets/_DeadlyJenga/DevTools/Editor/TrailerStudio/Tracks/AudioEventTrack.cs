using System;
using System.Collections.Generic;
using System.Text;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Which sound-making events happened, where and when. Presentation plays sounds when gameplay
    // raises these events, so playback raises the same events again and the same sounds play.
    public sealed class AudioEventTrack : IStudioTrackType
    {
        public const string Id = "audio-events";
        public string TypeId => Id;
        public string Label => "Audio";
        public Color Color => StudioColors.Audio;
        public int Order => 40;

        public void EnsureTracks(StudioSet set, StudioTape tape) => tape.GetOrAdd("audio-events", Id, "Sound events");
        public StudioRecorder CreateRecorder(StudioStage stage, StudioTrack track) => new Recorder();
        public StudioPlayer CreatePlayer(StudioStage stage, StudioTrack track, StudioTake take) => new Player(take);

        [Serializable] public sealed class Sound
        {
            public int kind;              // 0 = block hit, 1 = match sound, 2 = block broken
            public Vector3 position, velocity;
            public float strength;
            public int cue;
        }
        [Serializable] sealed class Frame { public Sound[] sounds; }

        // True while playback is raising events, so the recorder never records its own playback.
        static bool replaying;

        sealed class Recorder : StudioRecorder
        {
            readonly List<Sound> heard = new List<Sound>();

            public Recorder()
            {
                GameEvents.BlockHit += Hit;
                GameEvents.MatchSound += Match;
                GameEvents.BlockBroken += Broken;
            }

            void Hit(Vector3 position, float strength) { if (!replaying) heard.Add(new Sound { kind = 0, position = position, strength = strength }); }
            void Match(MatchSoundCue cue, Vector3 position) { if (!replaying) heard.Add(new Sound { kind = 1, position = position, cue = (int)cue }); }
            void Broken(GameObject block, Vector3 position, Vector3 velocity) { if (!replaying) heard.Add(new Sound { kind = 2, position = position, velocity = velocity }); }

            public override byte[] Capture(int frame, bool key)
            {
                if (heard.Count == 0) return Array.Empty<byte>();
                // A collapse can make hundreds of hits in one frame. Keep match sounds and the 64 loudest hits.
                if (heard.Count > 64)
                {
                    heard.Sort((a, b) => (b.kind == 0 ? b.strength : float.MaxValue).CompareTo(a.kind == 0 ? a.strength : float.MaxValue));
                    heard.RemoveRange(64, heard.Count - 64);
                }
                byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Frame { sounds = heard.ToArray() }));
                heard.Clear();
                return bytes;
            }

            public override void Dispose()
            {
                GameEvents.BlockHit -= Hit;
                GameEvents.MatchSound -= Match;
                GameEvents.BlockBroken -= Broken;
            }
        }

        sealed class Player : StudioPlayer
        {
            readonly StudioTake take;
            int last = -1;

            public Player(StudioTake take) { this.take = take; }

            public override void Show(int frame, bool running)
            {
                // Sounds only play while the tape runs forward, never while scrubbing.
                if (!running || frame == last) { last = frame; return; }
                // A slow editor skips tape frames in real-time playback. Their sounds still have to be heard,
                // so play every frame since the last one shown (but not a whole jump's worth).
                int first = frame > last && frame - last <= 15 ? last + 1 : frame;
                last = frame;
                replaying = true;
                try { for (int f = first; f <= frame; f++) Play(f); }
                finally { replaying = false; }
            }

            void Play(int frame)
            {
                if (!take.Covers(frame)) return;
                byte[] data = take.frames[frame - take.start];
                if (data == null || data.Length == 0) return;
                var sample = JsonUtility.FromJson<Frame>(Encoding.UTF8.GetString(data));
                if (sample?.sounds == null) return;
                foreach (var sound in sample.sounds)
                {
                    if (sound.kind == 0) GameEvents.RaiseBlockHit(sound.position, sound.strength);
                    else if (sound.kind == 1) GameEvents.RaiseMatchSound((MatchSoundCue)sound.cue, sound.position);
                    // Block-broken effects need the block object itself, so they are stored but not replayed yet.
                }
            }
        }
    }
}
