using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CandyAudio
{
    static class CandySoundSuite
    {
        internal static bool Contains(CandySound sound)
            => sound>=CandySound.ScoreboardOpen&&sound<=CandySound.HealthRegenerating;

        // The quieter UI and recovery recordings need more lift than impact transients.
        internal static float MixGain(CandySound sound) => sound switch
        {
            CandySound.ScoreboardOpen or CandySound.PointScored or CandySound.RoundResults or
            CandySound.StaminaLow or CandySound.StaminaUsable or CandySound.HealthRegenerating => 2f,
            CandySound.HitConfirm or CandySound.KillConfirm => 1.3f,
            _ => Contains(sound) ? 1.65f : 1f
        };

        public static void AddDefaults(Dictionary<CandySound,CandyAudioCue> cues)
        {
            for(int index=(int)CandySound.ScoreboardOpen;index<=(int)CandySound.HealthRegenerating;index++)
            {
                var sound=(CandySound)index;
                if(cues.ContainsKey(sound))continue;
                var clip=Resources.Load<AudioClip>("CandyAudio/SoundSuite/"+sound);
                if(!clip)continue;
                bool nearby=sound==CandySound.NearbyHit||sound==CandySound.NearbyShot||sound==CandySound.NearbyDeath;
                cues.Add(sound,new CandyAudioCue
                {
                    sound=sound,variants=new[]{clip},bus=nearby?CandyBus.Character:CandyBus.UI,
                    volume=nearby?.8f:1,pitch=Vector2.one,minDistance=nearby?3f:1.4f,maxDistance=42,
                    cooldown=0,maxVoices=8,priority=nearby?58:26
                });
            }
        }

        public static void Local(CandySound sound)
            => CandyAudioEngine.Instance?.Play(sound,Vector3.zero,local:true);

        public static CandySound FromMatch(MatchSoundCue cue) => cue switch
        {
            MatchSoundCue.HitConfirm=>CandySound.HitConfirm,
            MatchSoundCue.KillConfirm=>CandySound.KillConfirm,
            MatchSoundCue.PersonalHit=>CandySound.PersonalHit,
            MatchSoundCue.PersonalShot=>CandySound.PersonalShot,
            MatchSoundCue.NearbyHit=>CandySound.NearbyHit,
            MatchSoundCue.NearbyShot=>CandySound.NearbyShot,
            MatchSoundCue.PlayerDeath=>CandySound.PlayerKilled,
            MatchSoundCue.IslandDeath=>CandySound.IslandDeath,
            MatchSoundCue.DrowningDeath=>CandySound.DrowningDeath,
            _=>CandySound.NearbyDeath
        };
    }
}
