using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CandyAudio
{
    // Recorded sound suites for acid rain (also lava and dry-ice rain), earthquake, fire and tornado
    // (Resources/CandyAudio/<Disaster>), approved by Evan from the preview page on 25 Nov. They replace the
    // synthesized placeholders in CandyDisasterSounds and add the new triggers. A cue in the audio bank still overrides.
    static class CandyDisasterSuite
    {
        struct Spec
        {
            public CandySound sound; public string folder, file; public int count; public float volume, near, far, cooldown; public int priority, voices; public bool loop;
        }

        static Spec S(CandySound sound, string folder, string file, int count, float volume, float near, float far, int priority, int voices, float cooldown, bool loop = false)
            => new Spec { sound = sound, folder = folder, file = file, count = count, volume = volume, near = near, far = far, priority = priority, voices = voices, cooldown = cooldown, loop = loop };

        static readonly Spec[] Specs =
        {
            S(CandySound.AcidSizzle,      "AcidRain",   "RainBed",         1, .7f,  6,  120, 40, 1, .1f, true),
            S(CandySound.AcidRoofRain,    "AcidRain",   "RoofRain",        1, .6f,  3,  40,  45, 1, .1f, true),
            S(CandySound.AcidBurn,        "AcidRain",   "Burn",            3, .85f, 1,  15,  30, 1, .5f),
            S(CandySound.AcidSplat,       "AcidRain",   "Splat",           4, .55f, 2,  20,  65, 3, .12f),
            S(CandySound.AcidThunder,     "AcidRain",   "ToxicThunder",    2, .85f, 60, 900, 35, 1, 6f),
            S(CandySound.EarthRumble,     "Earthquake", "Rumble",          1, .9f,  10, 300, 25, 1, .1f, true),
            S(CandySound.EarthJolt,       "Earthquake", "Jolt",            3, 1f,   10, 400, 15, 2, .8f),
            S(CandySound.EarthCrack,      "Earthquake", "GroundCrack",     3, .85f, 4,  80,  40, 2, 1f),
            S(CandySound.EarthGroan,      "Earthquake", "TowerGroan",      3, .75f, 6,  90,  45, 1, 2f),
            S(CandySound.EarthRattle,     "Earthquake", "BlockRattle",     1, .6f,  6,  70,  55, 1, .1f, true),
            S(CandySound.EarthAftershock, "Earthquake", "Aftershock",      2, 1f,   40, 800, 20, 1, 2f),
            S(CandySound.FireCrackle,     "Fire",       "Crackle",         1, .6f,  4,  60,  50, 2, .1f, true),
            S(CandySound.FireRoar,        "Fire",       "Roar",            1, .85f, 6,  90,  35, 1, .1f, true),
            S(CandySound.FireIgnite,      "Fire",       "Ignite",          3, .8f,  4,  70,  40, 2, .4f),
            S(CandySound.FireSugarPop,    "Fire",       "SugarPop",        4, .55f, 2,  25,  60, 2, .6f),
            S(CandySound.FireCollapse,    "Fire",       "BurningCollapse", 2, .9f,  5,  90,  35, 2, 1f),
            S(CandySound.FireSmoulder,    "Fire",       "Smoulder",        1, .5f,  2,  25,  70, 1, .1f, true),
            S(CandySound.CycloneWind,     "Tornado",    "Howl",            1, .95f, 15, 350, 20, 1, .1f, true),
            S(CandySound.TornadoPressure, "Tornado",    "Roar",            1, 1f,   2,  60,  15, 1, .1f, true),
            S(CandySound.CycloneDebris,   "Tornado",    "DebrisWhip",      1, .7f,  6,  90,  40, 1, .1f, true),
            S(CandySound.TornadoPass,     "Tornado",    "Pass",            2, .95f, 8,  150, 25, 1, 2f),
            S(CandySound.TornadoBlockRip, "Tornado",    "BlockRip",        3, .75f, 3,  50,  50, 2, .6f),
            S(CandySound.TornadoThunder,  "Tornado",    "Thunder",         3, .95f, 40, 900, 30, 1, 3f),
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
                    var clip = Resources.Load<AudioClip>("CandyAudio/" + spec.folder + "/" + spec.file + "-" + i.ToString("00"));
                    if (clip) clips.Add(clip);
                }
                if (clips.Count == 0) continue;   // fall back to the synthesized placeholder
                cues.Add(spec.sound, new CandyAudioCue {
                    sound = spec.sound, variants = clips.ToArray(), bus = CandyBus.World, loop = spec.loop,
                    volume = spec.volume, priority = spec.priority, maxVoices = spec.voices, cooldown = spec.cooldown,
                    minDistance = spec.near, maxDistance = spec.far, pitch = spec.loop ? new Vector2(.98f, 1.02f) : new Vector2(.93f, 1.07f)
                });
            }
        }
    }
}
