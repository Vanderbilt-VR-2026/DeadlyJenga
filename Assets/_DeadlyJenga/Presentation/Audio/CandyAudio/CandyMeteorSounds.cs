using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CandyAudio
{
    // Recorded meteor soundscape (Resources/CandyAudio/Meteors). These replace the synthesized
    // placeholders in CandyDisasterSounds. A cue in the audio bank asset still overrides them.
    static class CandyMeteorSounds
    {
        struct Spec
        {
            public CandySound sound; public string file; public int count; public float volume, near, far, cooldown; public int priority, voices; public bool loop;
        }

        static readonly Spec[] Specs =
        {
            new Spec { sound = CandySound.MeteorFlyby,       file = "Flyby",          count = 1, volume = .62f, near = 4,  far = 90,   priority = 70, voices = 3, loop = true, cooldown = .1f },
            new Spec { sound = CandySound.MeteorWhistle,     file = "Incoming",       count = 3, volume = .8f,  near = 6,  far = 140,  priority = 45, voices = 3, cooldown = .25f },
            new Spec { sound = CandySound.MeteorImpact,      file = "Impact",         count = 4, volume = .95f, near = 8,  far = 220,  priority = 35, voices = 4, cooldown = .06f },
            new Spec { sound = CandySound.MeteorWaterImpact, file = "Water",          count = 3, volume = .8f,  near = 8,  far = 180,  priority = 60, voices = 3, cooldown = .08f },
            new Spec { sound = CandySound.PlanetApproach,    file = "PlanetApproach", count = 1, volume = .95f, near = 40, far = 900,  priority = 12, voices = 1, loop = true, cooldown = .1f },
            new Spec { sound = CandySound.PlanetImpact,      file = "PlanetImpact",   count = 2, volume = 1f,   near = 60, far = 1500, priority = 5,  voices = 1, cooldown = 1f },
        };

        public static bool Contains(CandySound sound)
        {
            foreach (var spec in Specs) if (spec.sound == sound) return true;
            return false;
        }

        public static void AddDefaults(Dictionary<CandySound, CandyAudioCue> cues)
        {
            foreach (var spec in Specs)
            {
                if (cues.ContainsKey(spec.sound)) continue;
                var clips = new List<AudioClip>();
                for (int i = 1; i <= spec.count; i++)
                {
                    var clip = Resources.Load<AudioClip>("CandyAudio/Meteors/" + spec.file + "-" + i.ToString("00"));
                    if (clip) clips.Add(clip);
                }
                if (clips.Count == 0) continue; // fall back to the synthesized placeholder
                cues.Add(spec.sound, new CandyAudioCue {
                    sound = spec.sound, variants = clips.ToArray(), bus = CandyBus.World, loop = spec.loop,
                    volume = spec.volume, priority = spec.priority, maxVoices = spec.voices, cooldown = spec.cooldown,
                    minDistance = spec.near, maxDistance = spec.far, pitch = spec.loop ? new Vector2(.98f, 1.02f) : new Vector2(.93f, 1.07f)
                });
            }
        }
    }
}
