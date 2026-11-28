using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CandyAudio
{
    // Recorded tsunami soundscape (Resources/CandyAudio/Tsunami), approved by Evan from the preview page on 25 Nov.
    // Replaces the synthesized placeholders for the existing wave cues and adds the new tsunami triggers.
    // A cue in the audio bank asset still overrides these.
    static class CandyTsunamiSounds
    {
        struct Spec
        {
            public CandySound sound; public string file; public int count; public float volume, near, far, cooldown; public int priority, voices; public bool loop; public CandyBus bus;
        }

        static readonly Spec[] Specs =
        {
            new Spec { sound = CandySound.WaveDrawback,    file = "Drawback",       count = 1, volume = .8f,  near = 20, far = 400, priority = 30, voices = 1, cooldown = 4 },
            new Spec { sound = CandySound.WaveRoar,        file = "ApproachRoar",   count = 1, volume = .9f,  near = 20, far = 320, priority = 25, voices = 2, loop = true, cooldown = .1f },
            new Spec { sound = CandySound.WaveCrest,       file = "CrestLoop",      count = 1, volume = .8f,  near = 8,  far = 140, priority = 28, voices = 1, loop = true, cooldown = .1f },
            new Spec { sound = CandySound.WaveLipCrash,    file = "LipCrash",       count = 3, volume = .9f,  near = 10, far = 180, priority = 32, voices = 2, cooldown = .9f },
            new Spec { sound = CandySound.WaveImpact,      file = "TowerSlam",      count = 3, volume = 1f,   near = 8,  far = 200, priority = 30, voices = 3, cooldown = .3f },
            new Spec { sound = CandySound.WaveSprayRain,   file = "SprayRain",      count = 1, volume = .55f, near = 3,  far = 40,  priority = 55, voices = 1, loop = true, cooldown = .1f },
            new Spec { sound = CandySound.WaveSweptBlocks, file = "SweptBlocks",    count = 2, volume = .6f,  near = 3,  far = 45,  priority = 60, voices = 2, cooldown = .45f },
            new Spec { sound = CandySound.WaveBarrel,      file = "BarrelLoop",     count = 1, volume = .85f, near = 2,  far = 30,  priority = 26, voices = 1, loop = true, cooldown = .1f, bus = CandyBus.Character },
            new Spec { sound = CandySound.WaveSubmerged,   file = "UnderwaterLoop", count = 1, volume = .8f,  near = 1,  far = 20,  priority = 24, voices = 1, loop = true, cooldown = .65f, bus = CandyBus.Character },
            new Spec { sound = CandySound.PuddleDrain,     file = "Aftermath",      count = 1, volume = .7f,  near = 10, far = 160, priority = 50, voices = 1, cooldown = 3 },
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
                    var clip = Resources.Load<AudioClip>("CandyAudio/Tsunami/" + spec.file + "-" + i.ToString("00"));
                    if (clip) clips.Add(clip);
                }
                if (clips.Count == 0) continue;   // fall back to the synthesized placeholder
                cues.Add(spec.sound, new CandyAudioCue {
                    sound = spec.sound, variants = clips.ToArray(), bus = spec.bus, loop = spec.loop,
                    volume = spec.volume, priority = spec.priority, maxVoices = spec.voices, cooldown = spec.cooldown,
                    minDistance = spec.near, maxDistance = spec.far, pitch = spec.loop ? new Vector2(.98f, 1.02f) : new Vector2(.93f, 1.07f)
                });
            }
        }
    }
}
