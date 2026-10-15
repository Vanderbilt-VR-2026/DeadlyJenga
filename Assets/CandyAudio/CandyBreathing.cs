using UnityEngine;

namespace DeadlyJenga.CandyAudio
{
    // Small original air/noise breaths, synthesized once; no per-frame PCM generation.
    // Replace through matching Audio Bank cues whenever recorded performances are ready.
    public static class CandyBreathing
    {
        public static readonly CandySound[] Sounds={CandySound.BreathExertion,CandySound.BreathRecovery,CandySound.BreathExhausted};
        static readonly AudioClip[] clips=new AudioClip[3];
        public static bool IsBreath(CandySound sound)=>sound==CandySound.BreathExertion||sound==CandySound.BreathRecovery||sound==CandySound.BreathExhausted;
        public static CandySound? State(bool climbing,float grip)
        {
            if(grip<=.001f)return CandySound.BreathExhausted;
            if(climbing)return CandySound.BreathExertion;
            return grip<.9999f?(CandySound?)CandySound.BreathRecovery:null;
        }
        public static CandyAudioCue DefaultCue(CandySound sound)
        {
            int index=System.Array.IndexOf(Sounds,sound);if(index<0)return null;
            if(!clips[index])clips[index]=Make(index);
            return new CandyAudioCue{sound=sound,bus=CandyBus.Character,variants=new[]{clips[index]},volume=.4f,loop=true,priority=50,maxVoices=1,pitch=new Vector2(1,1),minDistance=1,maxDistance=12};
        }
        static AudioClip Make(int type)
        {
            const int rate=22050;float duration=type==0?1.35f:type==1?2.1f:1.05f;
            var samples=new float[Mathf.RoundToInt(rate*duration)];var random=new System.Random(729+type);
            float low=0,body=0;
            for(int i=0;i<samples.Length;i++)
            {
                float t=(float)i/samples.Length;
                // Gentle inhalation then an airy, more emphatic exhalation; silent seam.
                float inhale=Pulse(t,.05f,.31f),exhale=Pulse(t,.40f,type==2?.9f:.83f);
                float envelope=inhale*.42f+exhale;
                float noise=(float)(random.NextDouble()*2-1);
                low+=.07f*(noise-low);body+=.43f*(noise-body);
                float air=body-low;
                float flutter=.87f+.13f*Mathf.Sin(2*Mathf.PI*(type==2?24:17)*i/rate);
                samples[i]=Mathf.Clamp((air*.78f+low*.6f)*envelope*flutter,-1,1);
            }
            var clip=AudioClip.Create("Candy "+Sounds[type]+" (original synthesized)",samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }
        static float Pulse(float t,float start,float end)
        {if(t<=start||t>=end)return 0;return Mathf.Pow(Mathf.Sin(Mathf.PI*(t-start)/(end-start)),1.4f);}
    }
}
