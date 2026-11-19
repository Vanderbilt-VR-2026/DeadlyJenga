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
            CandySound.LobbyLaunch,CandySound.SpawnSlam,CandySound.WaveRoar,CandySound.CycloneDebris,CandySound.FrostCreak,CandySound.LowGravityHum,CandySound.OutageHum,
            CandySound.AcidRoofRain,CandySound.DisasterWarningTone,CandySound.EarthJolt,
            CandySound.MeteorFlyby,CandySound.MeteorWaterImpact,CandySound.PlanetApproach,CandySound.PlanetImpact,
            CandySound.TornadoPass,CandySound.TornadoPressure,CandySound.RewindStart,CandySound.RewindRelease,CandySound.VampireShade,CandySound.RewindBed };
        static readonly Dictionary<CandySound,AudioClip> clips = new Dictionary<CandySound,AudioClip>();
        public static CandyAudioCue DefaultCue(CandySound sound)
        {
            bool loop = sound==CandySound.DisasterTension||sound==CandySound.AcidSizzle||sound==CandySound.CycloneWind||
                sound==CandySound.EarthRumble||sound==CandySound.FireCrackle||sound==CandySound.HandSwirl||
                sound==CandySound.IslandSink||sound==CandySound.WaveRoar||sound==CandySound.CycloneDebris||sound==CandySound.FrostCreak||sound==CandySound.LowGravityHum||sound==CandySound.OutageHum||
                sound==CandySound.AcidRoofRain||sound==CandySound.MeteorFlyby||sound==CandySound.PlanetApproach||sound==CandySound.TornadoPressure||sound==CandySound.RewindBed;
            if(!clips.TryGetValue(sound,out var clip)||!clip)clips[sound]=clip=Make(sound,loop);
            bool travel=sound==CandySound.LobbyLaunch||sound==CandySound.SpawnSlam;
            bool storm=sound==CandySound.WaveRoar||sound==CandySound.CycloneWind||sound==CandySound.PlanetApproach||sound==CandySound.PlanetImpact;
            bool warning=sound==CandySound.DisasterWarningTone;
            bool threat=sound==CandySound.AcidBurn||sound==CandySound.EarthJolt||sound==CandySound.TornadoPass;
            bool ambience=sound==CandySound.FrostCreak||sound==CandySound.LowGravityHum||sound==CandySound.OutageHum;
            return new CandyAudioCue { sound=sound,variants=new[]{clip},bus=warning?CandyBus.UI:travel?CandyBus.Character:CandyBus.World,loop=loop,
                volume=warning?.68f:travel?.9f:ambience?.28f:sound==CandySound.FingerFlex?.12f:threat?.62f:.45f,
                priority=warning?16:travel?20:sound==CandySound.DisasterHorn?25:threat?38:storm?50:ambience?115:90,
                maxVoices=sound==CandySound.FingerFlex?2:4,cooldown=sound==CandySound.FingerFlex?.18f:.12f,
                pitch=new Vector2(.97f,1.03f),minDistance=storm?8:3,maxDistance=storm?240:100 };
        }
        static AudioClip Make(CandySound kind,bool loop)
        {
            const int rate=22050;
            bool rain=kind==CandySound.AcidSizzle||kind==CandySound.AcidRoofRain;
            float seconds=rain?8:kind==CandySound.PlanetImpact?2.6f:kind==CandySound.TornadoPass?1.6f:kind==CandySound.WaveRoar||kind==CandySound.CycloneWind?4:kind==CandySound.WaveImpact?1.4f:loop?2.4f:kind==CandySound.DisasterHorn?2.3f:kind==CandySound.LobbyLaunch?1.05f:kind==CandySound.PowerDown||kind==CandySound.PowerUp?1.1f:.7f;
            bool smoothLoop=loop&&(rain||kind==CandySound.MeteorFlyby||kind==CandySound.PlanetApproach||kind==CandySound.TornadoPressure||kind==CandySound.RewindBed||kind==CandySound.WaveRoar||kind==CandySound.CycloneWind||kind==CandySound.CycloneDebris||kind==CandySound.FrostCreak||kind==CandySound.LowGravityHum||kind==CandySound.OutageHum);
            var pcm=new float[Mathf.CeilToInt(rate*seconds)];var random=new System.Random(4201+(int)kind*37);float low=0,mid=0,drops=0;
            for(int i=0;i<pcm.Length;i++)
            {
                float t=i/(float)rate,u=t/seconds,noise=(float)(random.NextDouble()*2-1);
                low+=.035f*(noise-low);mid+=.38f*(noise-mid);
                float air=mid-low,body=low*3,signal;
                switch(kind)
                {
                    case CandySound.FrostCreak:
                        float iceAge=t%.8f;
                        signal=body*.26f+air*.12f+Mathf.Sin(iceAge*2*Mathf.PI*(960-200*iceAge))*.13f*Mathf.Exp(-iceAge*13)+Mathf.Sin(t*2*Mathf.PI*92)*.06f;
                        break;
                    case CandySound.LowGravityHum:
                        signal=body*.14f+Mathf.Sin(t*2*Mathf.PI*52)*.16f+Mathf.Sin(t*2*Mathf.PI*78)*.08f*(.75f+.25f*Mathf.Sin(t*Mathf.PI))+air*.08f;
                        break;
                    case CandySound.OutageHum:
                        float flicker=Mathf.Pow(Mathf.Max(0,Mathf.Sin(t*2*Mathf.PI*6)),12);
                        signal=body*.12f+Mathf.Sin(t*2*Mathf.PI*48)*.15f+air*flicker*.28f;
                        break;
                    case CandySound.WaveRoar:
                        float surge=.55f+.25f*Mathf.Sin(t*2*Mathf.PI*.75f)+.12f*Mathf.Sin(t*2*Mathf.PI*2.25f);
                        signal=body*(1.1f+surge*.55f)+air*(.3f+surge*.5f)+Mathf.Sin(t*2*Mathf.PI*43)*.12f;
                        break;
                    case CandySound.WaveImpact:
                        float foam=Mathf.Clamp01(t/.08f)*Mathf.Exp(-t*1.8f);
                        signal=body*1.6f*Mathf.Exp(-t*2.2f)+air*.9f*foam+Mathf.Sin(t*2*Mathf.PI*(48-12*u))*.32f*Mathf.Exp(-t*4);
                        break;
                    case CandySound.WaveSpray:
                        signal=air*(.5f+.14f*Mathf.Sin(t*53))+noise*.07f;
                        break;
                    case CandySound.CycloneWind:
                        float gust=.6f+.25f*Mathf.Sin(t*2*Mathf.PI*1.25f)+.15f*Mathf.Sin(t*2*Mathf.PI*3.5f);
                        signal=body*1.3f+air*gust*.9f+Mathf.Sin(t*2*Mathf.PI*57)*.08f*(.5f+.5f*Mathf.Sin(t*2*Mathf.PI*7));
                        break;
                    case CandySound.CycloneDebris:
                        float rattle=Mathf.Pow(Mathf.Max(0,Mathf.Sin(t*2*Mathf.PI*11)),12);
                        signal=air*.3f+noise*rattle*.4f+Mathf.Sin(t*2*Mathf.PI*390)*rattle*.18f;
                        break;
                    case CandySound.PushImpact: signal=(body*1.8f+air*.8f)*Mathf.Exp(-t*30)+Mathf.Sin(t*2*Mathf.PI*115)*.42f*Mathf.Exp(-t*28)+Mathf.Sin(t*2*Mathf.PI*730)*.1f*Mathf.Exp(-t*85);break;
                    case CandySound.LobbyLaunch: signal=(air*.6f+body*.3f+Mathf.Sin(2*Mathf.PI*(65*t+310*t*t))*.12f)*(.2f+.8f*u);break;
                    case CandySound.SpawnSlam: signal=(body*.75f+air*.45f)*Mathf.Exp(-t*17)+Mathf.Sin(2*Mathf.PI*(42*t+18*(1-Mathf.Exp(-t*18))))*.6f*Mathf.Exp(-t*8);break;
                    case CandySound.DisasterHorn: signal=.36f*Mathf.Sin(t*2*Mathf.PI*91)+.18f*Mathf.Sin(t*2*Mathf.PI*137)+body*.12f;break;
                    case CandySound.DisasterTension: signal=(Mathf.Sin(t*2*Mathf.PI*69.3f)+.4f*Mathf.Sin(t*2*Mathf.PI*103.8f))*.17f*(.45f+.55f*Mathf.Pow(.5f+.5f*Mathf.Sin(t*2*Mathf.PI*1.25f),4));break;
                    case CandySound.PowerDown: case CandySound.PowerUp:
                        float start=kind==CandySound.PowerDown?240:24;
                        float sweep=kind==CandySound.PowerDown?-216:216;
                        signal=Mathf.Sin(t*(start+sweep*u*.5f)*2*Mathf.PI)*.23f+air*.14f;break;
                    case CandySound.ClaimWin: signal=(Mathf.Sin(t*2*Mathf.PI*523)+Mathf.Sin(t*2*Mathf.PI*659)+Mathf.Sin(t*2*Mathf.PI*784))*.14f;break;
                    case CandySound.FingerFlex: signal=(air*.28f+Mathf.Sin(t*2*Mathf.PI*(210+85*Mathf.Sin(t*38)))*.13f)*Mathf.Exp(-u*7);break;
                    case CandySound.EarthRumble: signal=body*.9f+Mathf.Sin(t*2*Mathf.PI*37)*.17f;break;
                    case CandySound.EarthJolt: signal=(body*1.15f+air*.55f)*Mathf.Exp(-t*5)+Mathf.Sin(t*2*Mathf.PI*72)*.36f*Mathf.Exp(-t*7);break;
                    case CandySound.MeteorImpact: case CandySound.PlanetImpact:
                        float heavy=kind==CandySound.PlanetImpact?1.6f:1;
                        signal=heavy*(body*.95f+Mathf.Sin(t*2*Mathf.PI*(74-18*u))*.24f)*Mathf.Exp(-t*2.2f)+air*.85f*Mathf.Exp(-t*13);break;
                    case CandySound.MeteorWaterImpact: signal=body*.85f*Mathf.Exp(-t*5)+air*.95f*Mathf.Exp(-t*2.5f);break;
                    case CandySound.AcidSizzle:
                        drops*=.965f;
                        if(random.NextDouble()<.013) drops+=.38f;
                        signal=air*(.34f+drops)+noise*.075f+body*.12f;break;
                    case CandySound.AcidRoofRain:
                        drops*=.986f;
                        if(random.NextDouble()<.006) drops+=.62f;
                        signal=body*(.42f+drops)+air*.10f;break;
                    case CandySound.AcidBurn:
                        signal=(air*.85f+noise*.10f)*Mathf.Exp(-t*4.5f)+Mathf.Sin(t*2*Mathf.PI*(870-275*u))*.18f*Mathf.Exp(-t*7);break;
                    case CandySound.VampireSizzle: signal=air*.68f*Mathf.Exp(-t*3)+Mathf.Sin(t*2*Mathf.PI*150)*.18f*Mathf.Exp(-t*6);break;
                    case CandySound.VampireShade: signal=air*.36f*Mathf.Exp(-t*6)+Mathf.Sin(t*2*Mathf.PI*(300+70*u))*.13f;break;
                    case CandySound.DisasterWarningTone:
                        float beat=t<.23f?t:t-.23f;
                        signal=(Mathf.Sin(t*2*Mathf.PI*325)+.42f*Mathf.Sin(t*2*Mathf.PI*487))*Mathf.Exp(-beat*15)*.40f;break;
                    case CandySound.MeteorFlyby:
                        signal=air*.48f+body*.21f+Mathf.Sin(t*2*Mathf.PI*420)*.10f;break;
                    case CandySound.PlanetApproach:
                        signal=body*.67f+air*.22f+Mathf.Sin(t*2*Mathf.PI*66)*.26f+Mathf.Sin(t*2*Mathf.PI*99)*.07f;break;
                    case CandySound.TornadoPass:
                        signal=(body*.84f+air*.88f)*(1-Mathf.Abs(u*2-1))+.24f*Mathf.Sin(t*2*Mathf.PI*(120-34*u));break;
                    case CandySound.TornadoPressure:
                        signal=body*.71f+Mathf.Sin(t*2*Mathf.PI*63)*.19f+air*.075f;break;
                    case CandySound.RewindBed:
                        float grain=t%.32f/.32f;
                        signal=body*.24f+air*.48f*grain*grain+Mathf.Sin(t*2*Mathf.PI*(190+28*Mathf.Sin(t*7)))*.14f;break;
                    case CandySound.RewindStart: case CandySound.RewindRelease:
                        float slope=kind==CandySound.RewindStart?-280:280;
                        signal=air*.32f*(kind==CandySound.RewindStart?u:1-u)+Mathf.Sin(t*(500+slope*u*.5f)*2*Mathf.PI)*.26f;break;
                    case CandySound.FireCrackle: signal=body*.12f+air*(random.NextDouble()>.997?.9f:.12f);break;
                    case CandySound.PuddleDrain: case CandySound.IslandSink: signal=body*.5f+Mathf.Sin(t*2*Mathf.PI*(110+50*Mathf.Sin(t*15)))*.14f*(.5f+.5f*Mathf.Sin(t*23));break;
                    case CandySound.MeteorWhistle: signal=air*.14f+Mathf.Sin(t*2*Mathf.PI*(440+550*u))*.16f;break;
                    default: signal=body*.4f+air*.5f;break;
                }
                float envelope=smoothLoop?1:loop?Mathf.Min(1,Mathf.Min(u*35,(1-u)*35)):Mathf.Min(1,u*30)*Mathf.Pow(1-u,kind==CandySound.DisasterHorn?.4f:1.5f);
                pcm[i]=smoothLoop||kind==CandySound.WaveImpact
                    ?.85f*(float)Math.Tanh(signal*envelope/.85f)
                    :Mathf.Clamp(signal*envelope,-.85f,.85f);
            }
            if(smoothLoop)
            {
                const int blend=1024;
                var joined=new float[pcm.Length-blend];
                Array.Copy(pcm,blend,joined,0,pcm.Length-blend*2);
                for(int i=0;i<blend;i++)
                {
                    float t=i/(float)(blend-1);
                    joined[pcm.Length-blend*2+i]=Mathf.Lerp(pcm[pcm.Length-blend+i],pcm[i],t);
                }
                pcm=joined;
            }
            var clip=AudioClip.Create("Original candy "+kind,pcm.Length,1,rate,false);clip.SetData(pcm,0);return clip;
        }
    }
}
