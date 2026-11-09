using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
namespace DeadlyJenga.CandyAudio
{
    // Small mono samples, made once per cue; the normal voice pool handles concurrency and distance.
    public static class CandySignatureSounds
    {
        static readonly List<AudioClip> generated=new List<AudioClip>();
        static readonly Dictionary<string,CandyAudioCue> cues=new Dictionary<string,CandyAudioCue>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){foreach(var clip in generated)if(clip)UnityEngine.Object.Destroy(clip);generated.Clear();cues.Clear();}
        public static void PlayWeapon(EquipmentActionFrame action,bool global=false)
        {
            string key=action.kind+"/"+action.action+(action.action==EquipmentAction.Shot? (action.shotgun?"/wide":"/precision"):"");bool shot=action.action==EquipmentAction.Shot,impact=action.action==EquipmentAction.Impact,reload=action.action==EquipmentAction.Reload;
            bool thunder=shot&&action.kind==EquipmentKind.LaserAxe&&action.shotgun;
            float seconds=thunder?.96f:impact?1.2f:reload?.8f:shot?(action.kind==EquipmentKind.ChainGun?.16f:.4f):.25f;
            var cue=Get(key,(int)action.kind*41+(int)action.action,seconds,false,shot,impact,reload,action.kind,thunder);
            cue.maxDistance=global?10000:action.kind==EquipmentKind.ChainGun?120:impact?100:75;cue.minDistance=global?1000:action.kind==EquipmentKind.ChainGun?6:2;
            CandyAudioEngine.Instance?.PlayCue(cue,action.position,1,action.actor?.GetHashCode()??0);
        }
        public static void PlayEmote(GameObject who,string name)
        {
            if(!who)return;int seed=StableHash(name);
            CandyAudioEngine.Instance?.PlayCue(Get("emote/"+name,seed,.45f,true,false,false,false,EquipmentKind.None),who.transform.position+Vector3.up*1.3f,.7f,who.GetInstanceID());
        }
        static int StableHash(string text){unchecked{int h=17;foreach(char c in text)h=h*31+c;return h&int.MaxValue;}}
        static CandyAudioCue Get(string key,int seed,float duration,bool emote,bool shot,bool impact,bool reload,EquipmentKind kind,bool thunder=false)
        {
            if(cues.TryGetValue(key,out var cue))return cue;
            cue=new CandyAudioCue{sound=(CandySound)(1000+StableHash(key)%1000000),bus=CandyBus.Character,volume=emote?.45f:.7f,priority=emote?150:65,cooldown=emote?.5f:.035f,maxVoices=emote?3:5,maxDistance=emote?22:75,variants=new AudioClip[5]};
            if(kind==EquipmentKind.ChainGun&&shot){var recorded=Resources.LoadAll<AudioClip>("CandyArsenal/ChainGun");if(recorded.Length>0){cue.variants=recorded;cue.pitch=Vector2.one;cue.volume=.70f;cue.maxVoices=2;cue.cooldown=.075f;cue.priority=38;cues[key]=cue;return cue;}}
            const int rate=16000;
            for(int v=0;v<5;v++)
            {
                var random=new System.Random(unchecked(seed*397+v*7919));var samples=new float[Mathf.CeilToInt(rate*duration)];float low=0,phase=0;
                float pitch=1+(v-2)*.035f;
                for(int i=0;i<samples.Length;i++)
                {
                    float t=(float)i/rate,u=t/duration,noise=(float)random.NextDouble()*2-1;low=Mathf.Lerp(low,noise,.12f);
                    float attack=Mathf.Clamp01(t/.006f),tail=Mathf.Pow(1-u,impact?1.4f:3),value;
                    if(emote){float f=(150+seed%160)*pitch*(1+.20f*Mathf.Sin(t*12+seed%5));phase+=f*2*Mathf.PI/rate;float syllable=.35f+.65f*Mathf.Pow(Mathf.Max(0,Mathf.Sin(t*(18+seed%14))),2);value=(Mathf.Sin(phase)+.30f*Mathf.Sin(phase*2.1f)+low*.3f)*syllable*.35f;}
                    else if(kind==EquipmentKind.ChainGun&&shot){float pulse=Mathf.Pow(.5f+.5f*Mathf.Sin(t*2*Mathf.PI*66.7f),3);value=(noise*.65f+Mathf.Sin(t*2*Mathf.PI*88)*.25f)*(.22f+pulse*.78f);}
                    else if(thunder)
                    {
                        float crack=Mathf.Exp(-t*32)+.42f*Mathf.Exp(-Mathf.Abs(t-.065f)*55);
                        float roll=Mathf.Exp(-t*2.8f)*Mathf.Clamp01(t/.027f);
                        phase+=(70-38*u)*2*Mathf.PI/rate;
                        value=noise*crack*.85f+(low*1.25f+Mathf.Sin(phase)*.43f)*roll;
                        tail=Mathf.Pow(1-u,.75f);
                    }
                    else if(kind==EquipmentKind.WristLaser&&shot){phase+=(310+160*u)*2*Mathf.PI/rate;value=(Mathf.Sin(phase)*.35f+Mathf.Sin(phase*1.013f)*.25f+low*.3f);}
                    else if(kind==EquipmentKind.GrenadeLauncher&&impact){value=low*1.4f+noise*(Mathf.Sin(t*113)>0.85f?.8f:.12f)+Mathf.Sin(t*2*Mathf.PI*(90-55*u))*.4f;}
                    else if(kind==EquipmentKind.RocketLauncher&&impact){value=low*2.2f+noise*.38f*Mathf.Exp(-t*8)+Mathf.Sin(t*2*Mathf.PI*(48-23*u))*.7f;}
                    else if(impact&&EquipmentSpecifications.Get(kind).melee)
                    {
                        float weight=kind==EquipmentKind.Dagger?.45f:kind==EquipmentKind.FlamingMarshmallow?.3f:1;
                        value=low*(.8f+weight)+Mathf.Sin(t*2*Mathf.PI*(90-48*u))*.38f*weight+noise*.14f;
                        if(kind==EquipmentKind.BarbedBat)value+=Mathf.Sin(t*2*Mathf.PI*790)*.14f*Mathf.Exp(-t*10);
                        if(kind==EquipmentKind.LaserClaws||kind==EquipmentKind.LaserAxe)value+=Mathf.Sin(t*2*Mathf.PI*(1100-600*u))*.23f;
                    }
                    else if(impact){value=low*1.8f+noise*.25f+Mathf.Sin(t*2*Mathf.PI*(65-40*u))*.55f;}
                    else if(reload){float click=Mathf.Exp(-Mathf.Abs(u-.08f)*90)+Mathf.Exp(-Mathf.Abs(u-.58f)*100)+Mathf.Exp(-Mathf.Abs(u-.88f)*110);value=noise*click*.9f+Mathf.Sin(t*2*Mathf.PI*(160+seed%230))*click*.3f;}
                    else if(kind==EquipmentKind.DualUzi&&shot){phase+=(1450-1100*u)*pitch*2*Mathf.PI/rate;value=Mathf.Sin(phase)*.4f+noise*.3f;}
                    else if(shot)
                    {
                        float frequency=kind==EquipmentKind.RocketLauncher?62:kind==EquipmentKind.GrenadeLauncher?155:
                            kind==EquipmentKind.NeedleDrill?1850:kind==EquipmentKind.StockPistol?280:
                            kind==EquipmentKind.SawStaff?165:kind==EquipmentKind.Chainsaw?98:
                            kind==EquipmentKind.BarbedBat?116:kind==EquipmentKind.LaserClaws?720:
                            kind==EquipmentKind.LaserAxe?175:kind==EquipmentKind.Dagger?1300:
                            kind==EquipmentKind.FlamingMarshmallow?340:120+seed%190;
                        phase+=frequency*(1-.48f*u)*pitch*2*Mathf.PI/rate;
                        value=(Mathf.Sin(phase)*.35f+low*.5f+noise*.16f);
                        if(kind==EquipmentKind.RocketLauncher)value+=low*(.4f+u)*.8f;
                        if(kind==EquipmentKind.NeedleDrill)value+=Mathf.Sin(phase*2.37f)*.24f*Mathf.Exp(-t*13);
                        if(kind==EquipmentKind.Chainsaw||kind==EquipmentKind.SawStaff)value=(Mathf.Sin(phase)+Mathf.Sin(phase*3.01f)*.25f+noise*.5f)*(.4f+.2f*Mathf.Sin(t*2*Mathf.PI*37));
                        if(kind==EquipmentKind.LaserClaws)value*=.55f+.45f*Mathf.Pow(Mathf.Sin(u*Mathf.PI*3),2);
                        if(kind==EquipmentKind.FlamingMarshmallow)value=low*.65f+noise*.18f*(.5f+.5f*Mathf.Sin(t*54));
                    }
                    else{phase+=(420+seed%700)*pitch*2*Mathf.PI/rate;value=noise*.3f+Mathf.Sin(phase)*.35f;}
                    samples[i]=Mathf.Clamp(value*attack*tail,-.95f,.95f);
                }
                var clip=AudioClip.Create(key+" variant "+(v+1),samples.Length,1,rate,false);clip.SetData(samples,0);generated.Add(clip);cue.variants[v]=clip;
            }
            cues[key]=cue;return cue;
        }
    }
}
