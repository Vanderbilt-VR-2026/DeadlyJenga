using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CandyAudio
{
    public sealed partial class CandyFeedback
    {
        ISessionInfo disasterSession;
        IWorldQuery contactWorld;
        string scheduledWarning;
        string lastWarning;
        float lastWarningAt=-100,lastAcidTick=-100;
        bool acidActive,wasRewinding,wasSunExposed;
        Bounds acidBounds;
        float acidTickDamage;
        readonly Dictionary<string,Transform> meteorAnchors=new Dictionary<string,Transform>();
        readonly HashSet<string> audibleMeteors=new HashSet<string>();
        readonly Dictionary<string,int> lastMeteorContact=new Dictionary<string,int>();

        bool BeginDisasterScan(CandyAudioEngine engine,ISessionInfo session)
        {
            bool same=ReferenceEquals(disasterSession,session);
            if(!same)
            {
                disasterSession=session;
                foreach(var item in anchors.Values)if(item.anchor)Destroy(item.anchor.gameObject);
                anchors.Clear();
                scheduledWarning=lastWarning=null;
                lastWarningAt=lastAcidTick=-100;
                wasRewinding=wasSunExposed=waveWarning=false;
                lastMeteorContact.Clear();
            }

            if(!ReferenceEquals(contactWorld,Services.World)){contactWorld=Services.World;lastMeteorContact.Clear();}
            acidActive=false;
            audibleMeteors.Clear();
            var frame=MatchRuntime.Current?.Frame;
            string pending=frame?.phase==RoundPhase.Playing&&frame.disasterWarningRemaining>0?frame.disasterWarning:null;
            if(string.IsNullOrEmpty(pending))scheduledWarning=null;
            else if(pending!=scheduledWarning&&Warn(engine,pending,frame.disasterWarningRemaining))scheduledWarning=pending;

            foreach(var wave in Tracked.Waves)
            {
                if(wave==null||!wave.Wave.active||wave.Wave.warningRemaining<=0)continue;
                if(!waveWarning)waveWarning=Warn(engine,"Tsunami",wave.Wave.warningRemaining);
                return same;
            }
            waveWarning=false;
            return same;
        }
        bool waveWarning;

        bool Warn(CandyAudioEngine engine,string kind,float remaining)
        {
            float deadline=Time.unscaledTime+remaining;
            if(string.Equals(lastWarning,kind,StringComparison.OrdinalIgnoreCase)&&Mathf.Abs(deadline-lastWarningAt)<1.5f)return true;
            if(!engine.Play(CandySound.DisasterWarningTone,engine.Listener.position,.86f,GetInstanceID(),local:true))return false;
            lastWarning=kind;
            lastWarningAt=deadline;
            return true;
        }

        void PlayDisasterArrival(CandyAudioEngine engine,string kind,Vector3 point)
        {
            if(kind=="PowerOutage")engine.Play(CandySound.PowerDown,engine.Listener.position,.75f,GetInstanceID(),local:true);
            if(kind=="Earthquake")engine.Play(CandySound.EarthJolt,point,.8f,GetInstanceID());
        }

        void AcidRain(CandyAudioEngine engine,HazardAudio target,DisasterFrame frame,float proximity)
        {
            acidActive=true;
            acidBounds=new Bounds(frame.center,frame.size+Vector3.one*2);
            acidTickDamage=frame.values!=null&&frame.values.Length>0?frame.values[0]*.1f:.6f;
            bool sheltered=Services.World?.IsSheltered(engine.Listener.position)==true;
            target.coverMix=StepCover(target.coverMix,sheltered,.1f);
            float bed=.24f+.60f*proximity;
            engine.Loop(CandySound.AcidSizzle,target.anchor,bed*Mathf.Lerp(1,.12f,target.coverMix),muffling:.82f*target.coverMix);
            if(target.coverMix>.005f)
                engine.Loop(CandySound.AcidRoofRain,target.anchor,bed*.7f*target.coverMix,muffling:.45f);
        }

        internal static float StepCover(float cover,bool sheltered,float elapsed)
            => Mathf.MoveTowards(cover,sheltered?1:0,elapsed*3);

        internal static bool IsAcidLikeDamage(bool inside,bool sheltered,float amount,float tickDamage)
            => inside&&!sheltered&&tickDamage>0&&amount>0&&amount<=Mathf.Max(1.2f,tickDamage*3);

        void LocalHazardDamage(GameObject who,float amount)
        {
            var point=who.GetComponent<IModeActor>()?.ViewPose.position??who.transform.position;
            bool inside=acidActive&&acidBounds.Contains(point);
            if(!IsAcidLikeDamage(inside,inside&&Services.World?.IsSheltered(point)==true,amount,acidTickDamage))
            {
                lastAcidTick=-100;
                return;
            }

            float now=Time.unscaledTime;
            if(now-lastAcidTick>.6f)
                CandyAudioEngine.Instance?.Play(CandySound.AcidBurn,point,.9f,who.GetInstanceID(),local:true);
            lastAcidTick=now;
        }

        internal static float PersonalHitGain()
        {
            if(!instance||!instance.acidActive||Time.unscaledTime-instance.lastAcidTick>.32f)return 1;
            var who=Services.Session?.Player?.gameObject;
            if(!who)return 1;
            var point=who.GetComponent<IModeActor>()?.ViewPose.position??who.transform.position;
            return instance.acidBounds.Contains(point)&&Services.World?.IsSheltered(point)!=true?.28f:1;
        }

        bool VampireExposed(DisasterFrame frame)
        {
            var who=Services.Session?.Player?.gameObject;
            if(!who||Services.Session.PlayingHand)return false;
            var actor=who.GetComponent<IModeActor>();
            if(actor==null||!actor.Alive)return false;
            var eye=actor.ViewPose.position;
            if(!new Bounds(frame.center,frame.size+Vector3.one*10).Contains(eye))return false;
            float elevation=(frame.values!=null&&frame.values.Length>2?frame.values[2]:45)*Mathf.Deg2Rad;
            var towardSun=new Vector3(.6f*Mathf.Cos(elevation),Mathf.Sin(elevation),.8f*Mathf.Cos(elevation));
            return !Physics.Raycast(eye,towardSun,1000,1<<Layers.Block,QueryTriggerInteraction.Ignore);
        }

        void Tornado(CandyAudioEngine engine,HazardAudio target,DisasterFrame frame,float proximity,float ferocity)
        {
            if(proximity>.25f)
                engine.Loop(CandySound.CycloneDebris,target.anchor,proximity*ferocity*.43f,false,Mathf.Lerp(.8f,1.2f,ferocity));
            if(frame.values==null||frame.values.Length<4)return;
            var delta=engine.Listener.position-frame.center;
            float distance=new Vector2(delta.x,delta.z).magnitude;
            float radius=Mathf.Max(1,frame.values[1]*.5f);
            bool atHeight=delta.y>=-1&&delta.y<=frame.values[0];
            if(atHeight&&distance<radius*1.5f&&!target.nearTornado)
            {
                target.nearTornado=true;
                engine.Play(CandySound.TornadoPass,target.anchor.position,.88f,target.anchor.GetInstanceID());
            }
            else if(!atHeight||distance>radius*2)target.nearTornado=false;
            float inside=atHeight?1-Mathf.Clamp01(distance/radius):0;
            if(inside>.01f)
                engine.Loop(CandySound.TornadoPressure,engine.Listener,inside*(.55f+.35f*ferocity),true,muffling:.35f);
        }

        void MeteorFlybys(CandyAudioEngine engine,string key,DisasterFrame frame)
        {
            if(frame.points==null)return;
            bool planet=frame.kind=="PlanetKillerMeteor";
            float reach=planet?240:70;
            int first=-1,second=-1;
            float near=reach*reach,next=near;
            for(int i=0;i<frame.points.Length;i++)
            {
                float distance=(frame.points[i].position-engine.Listener.position).sqrMagnitude;
                if(distance<near){second=first;next=near;first=i;near=distance;}
                else if(distance<next){second=i;next=distance;}
            }
            if(first>=0)MeteorLoop(engine,key,frame,first,near,reach,planet);
            if(!planet&&second>=0)MeteorLoop(engine,key,frame,second,next,reach,false);
        }

        void MeteorLoop(CandyAudioEngine engine,string key,DisasterFrame frame,int index,float distance,float reach,bool planet)
        {
            var point=frame.points[index];
            string id=key+"/"+point.id;
            audibleMeteors.Add(id);
            if(!meteorAnchors.TryGetValue(id,out var anchor)||!anchor)
            {
                anchor=new GameObject("Audio - moving meteor").transform;
                anchor.SetParent(transform,false);
                meteorAnchors[id]=anchor;
            }
            anchor.position=point.position+point.velocity*Mathf.Clamp(Time.time-frame.sampledAt,0,.18f);
            float gain=1-Mathf.Sqrt(distance)/reach;
            engine.Loop(planet?CandySound.PlanetApproach:CandySound.MeteorFlyby,anchor,.1f+.85f*gain,false,Mathf.Lerp(.78f,1.13f,gain));
        }

        void PlayMeteorContacts(CandyAudioEngine engine,DisasterFrame frame)
        {
            CandySound cue;
            switch(frame.kind)
            {
                case "MeteorLandContact": cue=CandySound.MeteorImpact;break;
                case "MeteorWaterContact": cue=CandySound.MeteorWaterImpact;break;
                case "PlanetLandContact": cue=CandySound.PlanetImpact;break;
                case "PlanetWaterContact": cue=CandySound.PlanetImpact;break;
                default:return;
            }
            if(frame.points==null)return;
            lastMeteorContact.TryGetValue(frame.kind,out int last);
            foreach(var point in frame.points)
            {
                if(point.id<=last)continue;
                last=point.id;
                engine.Play(cue,point.position,Mathf.Clamp(.42f+point.size*.035f,.5f,1.2f),point.id);
                if(frame.kind=="PlanetWaterContact")
                    engine.Play(CandySound.MeteorWaterImpact,point.position,1,point.id);
            }
            lastMeteorContact[frame.kind]=last;
        }

        void EndDisasterScan(CandyAudioEngine engine,bool rewinding,bool sunExposed,bool sameSession)
        {
            if(rewinding&&!wasRewinding)engine.Play(CandySound.RewindStart,engine.Listener.position,.8f,GetInstanceID(),local:true);
            if(rewinding)engine.Loop(CandySound.RewindBed,engine.Listener,.46f,true);
            if(!rewinding&&wasRewinding&&sameSession)engine.Play(CandySound.RewindRelease,engine.Listener.position,.7f,GetInstanceID(),local:true);
            if(sunExposed!=wasSunExposed&&sameSession)
                engine.Play(sunExposed?CandySound.VampireSizzle:CandySound.VampireShade,engine.Listener.position,.55f,GetInstanceID(),local:true);
            wasRewinding=rewinding;
            wasSunExposed=sunExposed;
            expired.Clear();
            foreach(var pair in meteorAnchors)
                if(!audibleMeteors.Contains(pair.Key))expired.Add(pair.Key);
            foreach(string key in expired)
            {
                if(meteorAnchors[key])Destroy(meteorAnchors[key].gameObject);
                meteorAnchors.Remove(key);
            }
        }
    }
}
