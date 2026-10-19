using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.XR;

namespace DeadlyJenga.CandyAudio
{
    // Bounded controller output. Bare-hand tracking safely has no haptic device.
    public sealed class CandyFeedback : MonoBehaviour
    {
        static CandyFeedback instance;
        [Range(0,1)] public float hapticStrength=.65f;
        public bool hapticsEnabled=true;
        float impulse,until,nextPulse,nextScan;readonly List<InputDevice> devices=new List<InputDevice>();
        readonly Dictionary<string,Transform> anchors=new Dictionary<string,Transform>();
        readonly HashSet<string> seen=new HashSet<string>();
        readonly List<string> expired=new List<string>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if(instance)return;var go=new GameObject("Candy event audio and controller feedback");DontDestroyOnLoad(go);instance=go.AddComponent<CandyFeedback>();
        }
        public static void Pulse(float amount,float duration=.08f)
        {if(!instance)return;instance.impulse=Mathf.Max(instance.impulse,Mathf.Clamp01(amount));instance.until=Mathf.Max(instance.until,Time.unscaledTime+Mathf.Clamp(duration,.02f,.3f));}
        void OnEnable(){GameEvents.Pushed+=Push;GameEvents.Damaged+=Damage;GameEvents.Eliminated+=Death;GameEvents.HazardStarted+=Started;GameEvents.HazardEnded+=Ended;}
        void OnDisable(){GameEvents.Pushed-=Push;GameEvents.Damaged-=Damage;GameEvents.Eliminated-=Death;GameEvents.HazardStarted-=Started;GameEvents.HazardEnded-=Ended;}
        static bool Local(GameObject who)=>who&&Services.Session?.Player!=null&&who.transform.root==Services.Session.Player.transform.root;
        void Push(GameObject who,Vector3 force){var e=CandyAudioEngine.Instance;if(e&&who)e.Play(CandySound.PushImpact,who.transform.position,Mathf.Clamp01(force.magnitude/8),who.GetInstanceID());if(Local(who))Pulse(.5f);}
        void Damage(GameObject who,float amount){if(Local(who))Pulse(Mathf.Clamp(amount/30,.12f,.65f));}
        void Death(GameObject who,GameObject source){if(Local(who))Pulse(.7f,.25f);}
        void Started(HazardType type,Vector3 position)
        {var e=CandyAudioEngine.Instance;if(e)e.Play(IsPower(type)?CandySound.PowerDown:CandySound.DisasterHorn,position,.8f,type?type.GetInstanceID():0,1,true);}
        void Ended(HazardType type,Vector3 position)
        {var e=CandyAudioEngine.Instance;if(e&&IsPower(type))e.Play(CandySound.PowerUp,position,.7f,0,1,true);}
        static bool IsPower(HazardType t)=>t&&(t.name.ToLowerInvariant().Contains("power")||t.name.ToLowerInvariant().Contains("outage"));
        void Update()
        {
            if(Time.unscaledTime>=nextScan){nextScan=Time.unscaledTime+.1f;Hazards();}
            if(Time.unscaledTime<nextPulse)return;nextPulse=Time.unscaledTime+.05f;
            if(!hapticsEnabled||Time.unscaledTime>until){impulse=0;return;}
            devices.Clear();InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller|InputDeviceCharacteristics.HeldInHand,devices);
            foreach(var device in devices)if(device.TryGetHapticCapabilities(out var cap)&&cap.supportsImpulse)device.SendHapticImpulse(0,impulse*hapticStrength,.055f);
        }
        void Hazards()
        {
            var e=CandyAudioEngine.Instance;if(!e||!e.Listener)return;seen.Clear();
            foreach(var source in DisasterStates.Active)
            {
                var f=source.Capture();if(f==null||f.residualOnly)continue;string key=f.kind+":"+source.GetHashCode();seen.Add(key);
                if(!anchors.TryGetValue(key,out var anchor)||!anchor){anchor=new GameObject("Audio - "+f.kind).transform;anchor.SetParent(transform);anchors[key]=anchor;}
                anchor.position=f.center;
                float distance=Vector3.Distance(e.Listener.position,f.center),reach=Mathf.Max(12,f.size.magnitude*.65f),proximity=1-Mathf.Clamp01(distance/reach);
                if(f.warning>0){e.Loop(CandySound.DisasterTension,anchor,.22f+.35f*proximity,false,Mathf.Lerp(.8f,1.3f,1-Mathf.Clamp01(f.warning/10)));continue;}
                CandySound? sound=f.kind=="Tornado"?CandySound.CycloneWind:f.kind=="Earthquake"?CandySound.EarthRumble:f.kind=="Fire"?CandySound.FireCrackle:f.kind=="AcidRain"?CandySound.AcidSizzle:null;
                if(sound.HasValue)e.Loop(sound.Value,anchor,.22f+.65f*proximity,false,Mathf.Lerp(.85f,1.15f,proximity));
                if(proximity>.1f&&(f.kind=="Tornado"||f.kind=="Earthquake"))Pulse(proximity*.17f,.12f);
                if(f.points!=null&&(f.kind=="MeteorShower"||f.kind=="PlanetKillerMeteor"))
                {for(int i=0;i<Mathf.Min(f.points.Length,6);i++)if((f.points[i].position-e.Listener.position).sqrMagnitude<2500)e.Play(CandySound.MeteorWhistle,f.points[i].position,.3f,f.points[i].id);}
            }
            foreach(var wave in Tracked.Waves)
            {
                if(wave==null)continue;
                // The existing tsunami visual/audio observes its own contact surface; rumble remains local and bounded.
                e.Loop(CandySound.DisasterTension,e.Listener,.12f,true,.85f);
            }
            expired.Clear();foreach(var pair in anchors)if(!seen.Contains(pair.Key))expired.Add(pair.Key);
            foreach(var key in expired){if(anchors[key])Destroy(anchors[key].gameObject);anchors.Remove(key);}
        }
        void OnDestroy(){if(instance==this)instance=null;}
    }
}
