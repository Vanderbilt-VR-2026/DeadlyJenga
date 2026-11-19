using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CandyAudio
{
    // The editor and the game resolve the same defaults. Clips are only made when requested.
    public static class CandyAudioDefaults
    {
        public static CandyAudioCue Find(CandySound sound)
        {
            if (Array.IndexOf(CandyBreathing.Sounds, sound) >= 0) return CandyBreathing.DefaultCue(sound);
            if (Array.IndexOf(CandyDisasterSounds.Sounds, sound) >= 0) return CandyDisasterSounds.DefaultCue(sound);
            if (Array.IndexOf(CandyWeaponSounds.Sounds, sound) >= 0) return CandyWeaponSounds.DefaultCue(sound);
            if (Array.IndexOf(CandyJaguarSounds.Sounds, sound) >= 0) return CandyJaguarSounds.DefaultCue(sound);
            if (Array.IndexOf(CandyPlayerSounds.Sounds, sound) >= 0) return CandyPlayerSounds.DefaultCue(sound);
            if (Array.IndexOf(CandyUnderwaterAudio.Sounds, sound) >= 0) return CandyUnderwaterAudio.DefaultCue(sound);
            var cues = new Dictionary<CandySound, CandyAudioCue>();
            if (sound >= CandySound.KrakenRise && sound <= CandySound.KrakenRetreat) CandyKrakenSounds.AddDefaults(cues);
            if (CandySoundSuite.Contains(sound)) CandySoundSuite.AddDefaults(cues);
            return cues.TryGetValue(sound, out var result) ? result : null;
        }

        public static void AddMissing(Dictionary<CandySound, CandyAudioCue> cues)
        {
            Add(CandyBreathing.Sounds, CandyBreathing.DefaultCue);
            Add(CandyDisasterSounds.Sounds, CandyDisasterSounds.DefaultCue);
            Add(CandyWeaponSounds.Sounds, CandyWeaponSounds.DefaultCue);
            Add(CandyJaguarSounds.Sounds, CandyJaguarSounds.DefaultCue);
            Add(CandyPlayerSounds.Sounds, CandyPlayerSounds.DefaultCue);
            CandyKrakenSounds.AddDefaults(cues);
            CandySoundSuite.AddDefaults(cues);
            Add(CandyUnderwaterAudio.Sounds, CandyUnderwaterAudio.DefaultCue);
            void Add(CandySound[] sounds, Func<CandySound,CandyAudioCue> factory)
            {
                foreach (var sound in sounds) if (!cues.ContainsKey(sound))
                {
                    var cue = factory(sound);
                    if (cue != null) cues.Add(sound, cue);
                }
            }
        }

        public static CandyAudioCue Copy(CandyAudioCue cue)
        {
            if (cue == null) return null;
            return new CandyAudioCue {
                sound = cue.sound, enabled = cue.enabled,
                displayName = cue.displayName, category = cue.category, triggerNotes = cue.triggerNotes,
                variants = cue.variants == null ? Array.Empty<AudioClip>() : (AudioClip[])cue.variants.Clone(),
                bus = cue.bus, volume = cue.volume, pitch = cue.pitch, cooldown = cue.cooldown,
                maxVoices = cue.maxVoices, priority = cue.priority, minDistance = cue.minDistance,
                maxDistance = cue.maxDistance, loop = cue.loop, output = cue.output
            };
        }
    }
}
