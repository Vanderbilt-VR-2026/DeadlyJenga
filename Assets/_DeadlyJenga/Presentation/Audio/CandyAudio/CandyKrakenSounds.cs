using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CandyAudio
{
    static class CandyKrakenSounds
    {
        public static void AddDefaults(Dictionary<CandySound,CandyAudioCue> cues)
        {
            Add(cues,CandySound.KrakenRise,"Rise",.85f,16,35,1);
            Add(cues,CandySound.KrakenIdle,"Idle",.31f,10,100,1,true);
            Add(cues,CandySound.KrakenSurface,"Surface",.56f,7,85,3);
            Add(cues,CandySound.KrakenReach,"Reach",.43f,6,95,2);
            Add(cues,CandySound.KrakenGrip,"Grip",.59f,6,75,3);
            Add(cues,CandySound.KrakenDrag,"Drag",.69f,8,65,3);
            Add(cues,CandySound.KrakenWindup,"Windup",.52f,9,64,2);
            Add(cues,CandySound.KrakenSlam,"Slam",.94f,13,30,3);
            Add(cues,CandySound.KrakenSubmerge,"Submerge",.48f,7,90,3);
            Add(cues,CandySound.KrakenRetreat,"Retreat",.75f,14,48,1);
        }

        static void Add(Dictionary<CandySound,CandyAudioCue> cues,CandySound sound,string file,
            float volume,float near,int priority,int voices,bool loop=false)
        {
            if(cues.ContainsKey(sound))return;
            var first=Resources.Load<AudioClip>("CandyAudio/Kraken/"+file+"-01");
            if(!first)return;
            var second=loop?null:Resources.Load<AudioClip>("CandyAudio/Kraken/"+file+"-02");
            cues.Add(sound,new CandyAudioCue {
                sound=sound,variants=second?new[]{first,second}:new[]{first},
                bus=CandyBus.World,loop=loop,volume=volume,priority=priority,maxVoices=voices,
                cooldown=.18f,minDistance=near,maxDistance=loop?160:180,pitch=new Vector2(.95f,1.05f)
            });
        }
    }
}
