using System;
using UnityEngine;

namespace DeadlyJenga.Core
{
    // What the voice player asks the social layer about each speaker every frame.
    public interface IVoiceMix
    {
        float Gain(string speaker);                       // volume setting for this speaker (0 = silent)
        float SpatialBlend { get; }                       // 0 = flat, 1 = from the speaker's position
        bool TryPosition(string speaker, out Vector3 point);
        float Pitch(string speaker);                      // 1 = unchanged
    }

    // Plays incoming voice streams (16 kHz mono). Presentation provides it; the social layer
    // (Game Session) feeds it samples and never touches audio itself.
    public interface IVoicePlayer
    {
        int SpeakerCount { get; }
        void Accept(string speaker, float[] samples, bool end);
        bool IsMuted(string speaker);
        void Mute(string speaker, bool muted);
        float Volume(string speaker);
        void SetVolume(string speaker, float volume);
        void SetDuck(string speaker, float duck);         // extra 0..1 factor while others talk
        void Remove(string speaker);
        void Clear();
    }

    // Presentation registers how voice players are made.
    public static class VoicePlayback
    {
        static Func<GameObject, IVoiceMix, IVoicePlayer> maker;
        public static void Register(Func<GameObject, IVoiceMix, IVoicePlayer> make) => maker = make;
        public static IVoicePlayer Create(GameObject host, IVoiceMix mix) => maker?.Invoke(host, mix) ?? new SilentVoice();

        // Used when nothing plays sound (tests, servers): keeps settings, plays nothing.
        sealed class SilentVoice : IVoicePlayer
        {
            readonly System.Collections.Generic.Dictionary<string, (bool muted, float volume)> speakers = new System.Collections.Generic.Dictionary<string, (bool, float)>();
            public int SpeakerCount => speakers.Count;
            (bool muted, float volume) Get(string id) => speakers.TryGetValue(id, out var s) ? s : (false, 1);
            public void Accept(string speaker, float[] samples, bool end) { if (!speakers.ContainsKey(speaker)) speakers[speaker] = (false, 1); }
            public bool IsMuted(string speaker) => Get(speaker).muted;
            public void Mute(string speaker, bool muted) => speakers[speaker] = (muted, Get(speaker).volume);
            public float Volume(string speaker) => Get(speaker).volume;
            public void SetVolume(string speaker, float volume) => speakers[speaker] = (Get(speaker).muted, Mathf.Clamp01(volume));
            public void SetDuck(string speaker, float duck) { }
            public void Remove(string speaker) => speakers.Remove(speaker);
            public void Clear() => speakers.Clear();
        }
    }
}
