using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CandyAudio
{
    // Original recorded synthesis; authored bank cues override these defaults.
    public static class CandyWeaponSounds
    {
        public static readonly CandySound[] Sounds={CandySound.WeaponRifle,CandySound.WeaponShotgun,CandySound.WeaponBlaster,
            CandySound.WeaponTransform,CandySound.WeaponDraw,CandySound.WeaponHolster,CandySound.WeaponPickup,
            CandySound.WeaponSwap,CandySound.WeaponDrop,CandySound.WeaponSword,CandySound.WeaponHammer,
            CandySound.WeaponBoxing,CandySound.WeaponPlasmaImpact};
        static readonly Dictionary<CandySound,AudioClip[]> clips=new Dictionary<CandySound,AudioClip[]>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()=>clips.Clear();
        public static CandyAudioCue DefaultCue(CandySound sound)
        {
            if(!clips.TryGetValue(sound,out var variants))
            {
                variants=new AudioClip[4];
                for(int i=0;i<4;i++)variants[i]=Resources.Load<AudioClip>("CandyAudio/Weapons/"+sound+"-"+(i+1).ToString("00"));
                clips[sound]=variants;
            }
            bool shot=sound==CandySound.WeaponShotgun||sound==CandySound.WeaponRifle||sound==CandySound.WeaponBlaster;
            return new CandyAudioCue{sound=sound,variants=variants,bus=CandyBus.Character,volume=shot?.85f:.58f,
                pitch=new Vector2(.98f,1.02f),cooldown=shot?.04f:.08f,maxVoices=shot?8:4,priority=shot?45:80,
                minDistance=shot?3:1.3f,maxDistance=shot?90:24};
        }
    }
}
