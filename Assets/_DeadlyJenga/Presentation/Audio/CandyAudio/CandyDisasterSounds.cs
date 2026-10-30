using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CandyAudio
{
    // Original synthesized placeholders. Bank recordings override every cue independently.
    public static class CandyDisasterSounds
    {
        public static readonly CandySound[] Sounds = {
            CandySound.DisasterHorn,CandySound.DisasterTension,CandySound.WaveImpact,CandySound.WaveSpray,
            CandySound.PuddleDrain,CandySound.AcidSizzle,CandySound.AcidBurn,CandySound.CycloneWind,
            CandySound.EarthRumble,CandySound.FireCrackle,CandySound.MeteorWhistle,CandySound.MeteorImpact,
            CandySound.PowerDown,CandySound.PowerUp,CandySound.FingerFlex,CandySound.HandSwirl,
            CandySound.PushImpact,CandySound.ClaimWin,CandySound.IslandSink,CandySound.VampireSizzle,
            CandySound.LobbyLaunch,CandySound.SpawnSlam };
        static readonly Dictionary<CandySound,AudioClip> clips = new Dictionary<CandySound,AudioClip>();
        public static CandyAudioCue DefaultCue(CandySound sound)
        {
            bool loop = sound==CandySound.DisasterTension||sound==CandySound.AcidSizzle||sound==CandySound.CycloneWind||
                sound==CandySound.EarthRumble||sound==CandySound.FireCrackle||sound==CandySound.HandSwirl||
                sound==CandySound.IslandSink||sound==CandySound.VampireSizzle;
            if(!clips.TryGetValue(sound,out var clip)||!clip)clips[sound]=clip=Make(sound,loop);
            bool travel=sound==CandySound.LobbyLaunch||sound==CandySound.SpawnSlam;
            return new CandyAudioCue { sound=sound,variants=new[]{clip},bus=travel?CandyBus.Character:CandyBus.World,loop=loop,
                volume=travel?.9f:sound==CandySound.FingerFlex?.12f:.45f,priority=travel?20:sound==CandySound.DisasterHorn?25:90,
                maxVoices=sound==CandySound.FingerFlex?2:4,cooldown=sound==CandySound.FingerFlex?.18f:.12f,
                pitch=new Vector2(.97f,1.03f),minDistance=3,maxDistance=100 };
        }
        static AudioClip Make(CandySound kind,bool loop)
        {
            const int rate=22050; float seconds=loop?2.4f:kind==CandySound.DisasterHorn?2.3f:kind==CandySound.LobbyLaunch?1.05f:kind==CandySound.PowerDown||kind==CandySound.PowerUp?1.1f:.7f;
            var pcm=new float[Mathf.CeilToInt(rate*seconds)];var random=new System.Random(4201+(int)kind*37);float low=0,mid=0;
            for(int i=0;i<pcm.Length;i++)
            {
                float t=i/(float)rate,u=t/seconds,noise=(float)(random.NextDouble()*2-1);
                low+=.035f*(noise-low);mid+=.38f*(noise-mid);
                float air=mid-low,body=low*3,signal;
                switch(kind)
                {
                    case CandySound.PushImpact: signal=(body*1.8f+air*.8f)*Mathf.Exp(-t*30)+Mathf.Sin(t*2*Mathf.PI*115)*.42f*Mathf.Exp(-t*28)+Mathf.Sin(t*2*Mathf.PI*730)*.1f*Mathf.Exp(-t*85);break;
                    case CandySound.LobbyLaunch: signal=(air*.6f+body*.3f+Mathf.Sin(2*Mathf.PI*(65*t+310*t*t))*.12f)*(.2f+.8f*u);break;
                    case CandySound.SpawnSlam: signal=(body*.75f+air*.45f)*Mathf.Exp(-t*17)+Mathf.Sin(2*Mathf.PI*(42*t+18*(1-Mathf.Exp(-t*18))))*.6f*Mathf.Exp(-t*8);break;
                    case CandySound.DisasterHorn: signal=.36f*Mathf.Sin(t*2*Mathf.PI*91)+.18f*Mathf.Sin(t*2*Mathf.PI*137)+body*.12f;break;
                    case CandySound.DisasterTension: signal=(Mathf.Sin(t*2*Mathf.PI*69.3f)+.4f*Mathf.Sin(t*2*Mathf.PI*103.8f))*.17f*(.45f+.55f*Mathf.Pow(.5f+.5f*Mathf.Sin(t*2*Mathf.PI*1.25f),4));break;
                    case CandySound.PowerDown: case CandySound.PowerUp:
                        float f=kind==CandySound.PowerDown?Mathf.Lerp(240,24,u):Mathf.Lerp(24,240,u);signal=Mathf.Sin(t*f*2*Mathf.PI)*.23f+air*.14f;break;
                    case CandySound.ClaimWin: signal=(Mathf.Sin(t*2*Mathf.PI*523)+Mathf.Sin(t*2*Mathf.PI*659)+Mathf.Sin(t*2*Mathf.PI*784))*.14f;break;
                    case CandySound.FingerFlex: signal=(air*.28f+Mathf.Sin(t*2*Mathf.PI*(210+85*Mathf.Sin(t*38)))*.13f)*Mathf.Exp(-u*7);break;
                    case CandySound.EarthRumble: case CandySound.MeteorImpact: signal=body*.9f+Mathf.Sin(t*2*Mathf.PI*37)*.17f;break;
                    case CandySound.AcidSizzle: case CandySound.AcidBurn: case CandySound.VampireSizzle: signal=air*(.23f+.2f*Mathf.Abs(Mathf.Sin(t*71)))+noise*.08f;break;
                    case CandySound.FireCrackle: signal=body*.12f+air*(random.NextDouble()>.997?.9f:.12f);break;
                    case CandySound.PuddleDrain: case CandySound.IslandSink: signal=body*.5f+Mathf.Sin(t*2*Mathf.PI*(110+50*Mathf.Sin(t*15)))*.14f*(.5f+.5f*Mathf.Sin(t*23));break;
                    case CandySound.MeteorWhistle: signal=air*.14f+Mathf.Sin(t*2*Mathf.PI*(440+550*u))*.16f;break;
                    default: signal=body*.4f+air*.5f;break;
                }
                float envelope=loop?Mathf.Min(1,Mathf.Min(u*35,(1-u)*35)):Mathf.Min(1,u*30)*Mathf.Pow(1-u,kind==CandySound.DisasterHorn?.4f:1.5f);
                pcm[i]=Mathf.Clamp(signal*envelope,-.85f,.85f);
            }
            var clip=AudioClip.Create("Original candy "+kind,pcm.Length,1,rate,false);clip.SetData(pcm,0);return clip;
        }
    }
}
