using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CandyAudio
{
    // Original, deterministic creature Foley in the candy palette. Bank clips can override it.
    public static class CandyJaguarSounds
    {
        public static readonly CandySound[] Sounds={CandySound.JaguarIdle,CandySound.JaguarChuff,CandySound.JaguarGrowl,
            CandySound.JaguarAlert,CandySound.JaguarAttack,CandySound.JaguarHurt,CandySound.JaguarDeath,CandySound.JaguarFootfall,CandySound.JaguarLand};
        static readonly Dictionary<CandySound,AudioClip[]> clips=new Dictionary<CandySound,AudioClip[]>();
        public static CandyAudioCue DefaultCue(CandySound sound)
        {
            if(!clips.TryGetValue(sound,out var variants)||!variants[0])
            {
                variants=new AudioClip[3];for(int i=0;i<variants.Length;i++)variants[i]=Make(sound,i);clips[sound]=variants;
            }
            bool foot=sound==CandySound.JaguarFootfall,death=sound==CandySound.JaguarDeath;
            return new CandyAudioCue{sound=sound,variants=variants,bus=CandyBus.Character,volume=foot?.20f:death?.85f:.64f,
                cooldown=foot?.18f:sound==CandySound.JaguarAlert?5f:sound==CandySound.JaguarHurt?.38f:.7f,
                maxVoices=foot?3:2,priority=death?42:foot?150:80,minDistance=foot?1:2,maxDistance=foot?20:death?60:45,
                pitch=new Vector2(.96f,1.04f)};
        }
        static AudioClip Make(CandySound sound,int variant)
        {
            const int rate=22050;
            float length=sound==CandySound.JaguarDeath?1.65f:sound==CandySound.JaguarGrowl?1.2f:sound==CandySound.JaguarAlert?.9f:
                sound==CandySound.JaguarIdle?.75f:sound==CandySound.JaguarChuff?.48f:sound==CandySound.JaguarFootfall?.17f:sound==CandySound.JaguarLand?.32f:.50f;
            length*=1+variant*.045f;var samples=new float[Mathf.CeilToInt(length*rate)];
            var rng=new System.Random(551107+(int)sound*37+variant*901);float low=0,air=0,phase=0,peak=0;
            for(int i=0;i<samples.Length;i++)
            {
                float t=i/(float)rate,u=t/length,noise=(float)rng.NextDouble()*2-1;
                low+=.045f*(noise-low);air+=.42f*(noise-air);
                float f=65+variant*4,breath=air-low,signal;
                if(sound==CandySound.JaguarFootfall||sound==CandySound.JaguarLand)
                {
                    float landing=sound==CandySound.JaguarLand?1:0;
                    signal=(low*2.8f+Mathf.Sin(2*Mathf.PI*(102*t-60*t*t))*.35f)*Mathf.Exp(-t*(32-landing*13));
                    signal+=breath*.18f*Mathf.Exp(-Mathf.Abs(t-.025f)*75);
                }
                else if(sound==CandySound.JaguarChuff)
                {
                    float puff=Mathf.Exp(-Mathf.Pow((t-.075f)/.037f,2))+Mathf.Exp(-Mathf.Pow((t-.225f)/.06f,2));
                    signal=breath*puff*.8f+low*puff*.7f;
                }
                else
                {
                    bool dying=sound==CandySound.JaguarDeath,attack=sound==CandySound.JaguarAttack,hurt=sound==CandySound.JaguarHurt;
                    if(dying)f=Mathf.Lerp(132,38,Mathf.Pow(u,.65f));
                    else if(attack)f=112+65*Mathf.Sin(Mathf.PI*u);
                    else if(hurt)f=155+70*Mathf.Sin(Mathf.PI*u);
                    else if(sound==CandySound.JaguarAlert)f=92+50*Mathf.Sin(Mathf.PI*u);
                    else if(sound==CandySound.JaguarIdle)f=52+7*Mathf.Sin(t*11);
                    f*=1+.025f*Mathf.Sin(t*71+variant);phase+=2*Mathf.PI*f/rate;
                    float voice=Mathf.Sin(phase)*.40f+Mathf.Sin(phase*2+.4f)*.23f+Mathf.Sin(phase*3)*.15f+Mathf.Sin(phase*5)*.065f;
                    float rasp=(.55f+.45f*Mathf.Sin(phase*.51f))*breath;
                    float throat=.7f+.3f*Mathf.Sin(t*(dying?18:26)+Mathf.Sin(t*9));
                    signal=(voice+rasp*(attack||hurt?.85f:.52f)+low*.7f)*throat;
                    if(sound==CandySound.JaguarIdle)signal*=.5f+.5f*Mathf.Pow(Mathf.Sin(t*18),2);
                    if(dying)signal*=Mathf.Pow(1-u,.55f);
                }
                float fadeIn=Mathf.Min(1,t/.014f),fadeOut=Mathf.Min(1,(length-t)/.06f);
                float envelope=fadeIn*fadeOut*Mathf.Pow(1-u,sound==CandySound.JaguarGrowl?.25f:.6f);
                samples[i]=(float)Math.Tanh(signal*1.25f)*envelope;peak=Mathf.Max(peak,Mathf.Abs(samples[i]));
            }
            float normalization=peak>.001f?.82f/peak:1;
            for(int i=0;i<samples.Length;i++)samples[i]*=normalization;
            var clip=AudioClip.Create("Original candy jaguar - "+sound+" "+(variant+1),samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }
    }
}
