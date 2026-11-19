using UnityEngine;

namespace DeadlyJenga.CandyAudio
{
    // Stable IDs for sounds with dedicated sources rather than pooled voices.
    public static class CandyDirectAudio
    {
        public const CandySound Home = (CandySound)710, Lobby = (CandySound)711, Gulls = (CandySound)712;
        static CandyAudioBank fallback;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => fallback = null;
        public static CandyAudioCue Find(CandySound id)
        {
            var engine = CandyAudioEngine.Instance;
            if (engine && engine.bank) return engine.TryGetCue(id, out var cue) ? cue : null;
            if (!fallback) fallback = Resources.Load<CandyAudioBank>("CandyAudio/Default");
            if (fallback && fallback.cues != null) foreach (var entry in fallback.cues) if (entry != null && entry.sound == id) return entry;
            return null;
        }
        public static AudioClip Clip(CandySound id, AudioClip original)
        {
            var cue = Find(id);
            return cue != null && cue.variants != null && cue.variants.Length > 0 ? cue.variants[0] : original;
        }
        public static float Gain(CandySound id)
        {
            var cue = Find(id);
            return cue == null ? 1 : cue.enabled ? cue.volume : 0;
        }
    }
}
