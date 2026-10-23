using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CandyAudio
{
    public static class CandyPlayerSounds
    {
        public static readonly CandySound[] Sounds={CandySound.PlayerPain,CandySound.PlayerDeath,CandySound.SuitThruster,CandySound.SuitIgnition,CandySound.SuitShutdown};
        static readonly Dictionary<CandySound,AudioClip[]> clips=new Dictionary<CandySound,AudioClip[]>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()=>clips.Clear();
        public static CandyAudioCue DefaultCue(CandySound sound)
        {
            if(System.Array.IndexOf(Sounds,sound)<0)return null;
            if(!clips.TryGetValue(sound,out var variants))
            {
                variants=new AudioClip[4];
                for(int i=0;i<variants.Length;i++)variants[i]=Resources.Load<AudioClip>("CandyAudio/Players/"+sound+"-"+(i+1).ToString("00"));
                clips.Add(sound,variants);
            }
            bool death=sound==CandySound.PlayerDeath;
            bool thruster=sound==CandySound.SuitThruster;
            return new CandyAudioCue{sound=sound,variants=variants,bus=CandyBus.Character,volume=death?.75f:.58f,
                pitch=new Vector2(.98f,1.02f),loop=thruster,cooldown=thruster?0:death?.7f:.38f,maxVoices=death?4:5,priority=death?42:60,minDistance=2,maxDistance=death?45:26};
        }
    }
}
