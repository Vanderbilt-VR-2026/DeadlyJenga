using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.XR;

namespace DeadlyJenga.CandyAudio
{
    // Bounded controller output. Bare-hand tracking safely has no haptic device.
    public sealed partial class CandyFeedback : MonoBehaviour
    {
        static CandyFeedback instance;
        [Range(0,1)] public float hapticStrength=.65f;
        public bool hapticsEnabled=true;
        float impulse,until,nextPulse,nextScan,nextShot,nextRubble,nextHeavyImpact,leftShot,leftUntil,rightShot,rightUntil;readonly List<InputDevice> devices=new List<InputDevice>();
        sealed class HazardAudio
        {
            public Transform anchor;
            public int fireCursor,nearestFire=-1;
            public bool warned,started,nearTornado;
            public float coverMix;
        }
        readonly Dictionary<string,HazardAudio> anchors=new Dictionary<string,HazardAudio>();
        readonly Dictionary<string,int> hazardCounts=new Dictionary<string,int>();
        readonly HashSet<string> seen=new HashSet<string>();
        readonly List<string> expired=new List<string>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if(instance)return;var go=new GameObject("Candy event audio and controller feedback");DontDestroyOnLoad(go);instance=go.AddComponent<CandyFeedback>();
        }
        public static void Pulse(float amount,float duration=.08f)
        {if(!instance)return;instance.impulse=Mathf.Max(instance.impulse,Mathf.Clamp01(amount));instance.until=Mathf.Max(instance.until,Time.unscaledTime+Mathf.Clamp(duration,.02f,.3f));}
        internal static void MenuPress(bool left)
        {
            if (!instance || !instance.hapticsEnabled || instance.hapticStrength <= 0) return;
            var device = InputDevices.GetDeviceAtXRNode(left ? XRNode.LeftHand : XRNode.RightHand);
            if (device.isValid && device.TryGetHapticCapabilities(out var capabilities) && capabilities.supportsImpulse)
                device.SendHapticImpulse(0, Mathf.Clamp01(instance.hapticStrength * .30f), .045f);
        }

        void OnEnable(){GameEvents.BlockHit+=Rubble;GameEvents.Pushed+=Push;GameEvents.Damaged+=Damage;GameEvents.Eliminated+=Death;}
        void OnDisable(){GameEvents.BlockHit-=Rubble;GameEvents.Pushed-=Push;GameEvents.Damaged-=Damage;GameEvents.Eliminated-=Death;}
        static bool Local(GameObject who)=>who&&Services.Session?.Player!=null&&who.transform.root==Services.Session.Player.transform.root;
        void Push(GameObject who,Vector3 force){var e=CandyAudioEngine.Instance;if(e&&who)e.Play(CandySound.PushImpact,who.transform.position,Mathf.Clamp01(force.magnitude/8),who.GetInstanceID());if(Local(who))Pulse(.5f);}
        void Damage(GameObject who,float amount)
        {
            if(amount<=0||!Local(who))return;
            Pulse(Mathf.Clamp(amount/30,.12f,.65f));
            if(who==Services.Session?.Player?.gameObject)LocalHazardDamage(who,amount);
        }
        void Death(GameObject who,GameObject source){if(Local(who))Pulse(.7f,.25f);}
        public static void Weapon(EquipmentActionFrame action)
        {
            var local=Services.Session;
            var match=MatchRuntime.Current;
            bool bash=action.action==EquipmentAction.Bash&&action.endpoints!=null&&action.endpoints.Length==1;
            if(!instance||local==null||!local.IsVR||match==null||(action.action!=EquipmentAction.Shot&&!bash)||
                action.actor!=match.LocalActorId||Time.unscaledTime<instance.nextShot)return;
            instance.nextShot=Time.unscaledTime+.07f;
            float amount=.32f,duration=.055f;
            if(bash){amount=.34f;duration=.09f;}
            else if(action.kind==EquipmentKind.ChainGun){amount=.24f;duration=.045f;}
            else if(action.kind==EquipmentKind.WristLaser){amount=.12f;duration=.035f;}
            else if(action.kind==EquipmentKind.RocketLauncher||action.kind==EquipmentKind.GrenadeLauncher){amount=.68f;duration=.12f;}
            else if(action.shotgun){amount=.56f;duration=.095f;}
            var equipment=(match as IEquipmentRuntime)?.EquipmentFor(action.actor);
            bool left=equipment?.leftHanded==true;
            duration=Mathf.Max(duration,.06f);
            if(left){instance.leftShot=Mathf.Max(instance.leftShot,amount);instance.leftUntil=Time.unscaledTime+duration;}
            else{instance.rightShot=Mathf.Max(instance.rightShot,amount);instance.rightUntil=Time.unscaledTime+duration;}
        }
        void Rubble(Vector3 point,float strength)
        {
            var camera=Services.Session?.ActiveCamera;if(!camera)return;
            float distanceSquared=(camera.transform.position-point).sqrMagnitude;
            bool mobile=Application.isMobilePlatform;
            float heavyRange=mobile?32f:65f;
            if(strength>=(mobile?20f:12f)&&distanceSquared<heavyRange*heavyRange&&Time.unscaledTime>=nextHeavyImpact)
            {
                nextHeavyImpact=Time.unscaledTime+(mobile?.75f:.34f);
                CandyAudioEngine.Instance?.Play(CandySound.BlockSmash,point,
                    Mathf.Clamp(strength/45,.2f,mobile?.57f:.76f),GetInstanceID());
            }
            if(strength<6||Time.unscaledTime<nextRubble||Services.Session?.IsVR!=true||distanceSquared>196)return;
            float distance=Mathf.Sqrt(distanceSquared);
            nextRubble=Time.unscaledTime+.14f;
            Pulse(Mathf.Clamp01((strength-3)/24)*(1-distance/14)*.32f,.10f);
        }
        void Update()
        {
            if(Time.unscaledTime>=nextScan){nextScan=Time.unscaledTime+.1f;Hazards();}
            if(Time.unscaledTime<nextPulse)return;nextPulse=Time.unscaledTime+.05f;
            if(Time.unscaledTime>until)impulse=0;
            if(Time.unscaledTime>leftUntil)leftShot=0;
            if(Time.unscaledTime>rightUntil)rightShot=0;
            if(!hapticsEnabled){impulse=leftShot=rightShot=0;return;}
            if(Mathf.Max(impulse,Mathf.Max(leftShot,rightShot))<=0)return;
            devices.Clear();InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller|InputDeviceCharacteristics.HeldInHand,devices);
            foreach(var device in devices)
            {
                if(!device.TryGetHapticCapabilities(out var cap)||!cap.supportsImpulse)continue;
                float shot=(device.characteristics&InputDeviceCharacteristics.Left)!=0?leftShot:
                    (device.characteristics&InputDeviceCharacteristics.Right)!=0?rightShot:0;
                float amount=Mathf.Max(impulse,shot)*hapticStrength;
                if(amount>0)device.SendHapticImpulse(0,Mathf.Clamp01(amount),.055f);
            }
        }
        void Hazards()
        {
            var e=CandyAudioEngine.Instance;if(!e||!e.Listener)return;
            var session=Services.Session;
            bool sameSession=BeginDisasterScan(e,session);
            seen.Clear();hazardCounts.Clear();
            bool rewinding=Services.Rewind?.Rewinding==true,sunExposed=false;
            foreach(var source in DisasterStates.Active)
            {
                if(source==null)continue;
                var f=source.Capture();if(f==null)continue;
                if(f.residualOnly){PlayMeteorContacts(e,f);continue;}
                if(f.kind=="TimeSlip"&&f.warning<=0){rewinding=true;continue;}
                string kind=f.kind??"Disaster";
                hazardCounts.TryGetValue(kind,out int occurrence);hazardCounts[kind]=occurrence+1;
                string key=kind+":"+occurrence;seen.Add(key);
                if(!anchors.TryGetValue(key,out var target)||!target.anchor)
                {
                    target=new HazardAudio{anchor=new GameObject("Audio - "+kind).transform};
                    target.anchor.SetParent(transform);anchors[key]=target;
                }
                var anchor=target.anchor;
                anchor.position=f.center;
                bool areaSound=f.kind=="Earthquake"||f.kind=="Icey"||f.kind=="AntiGravity"||f.kind=="PowerOutage"||f.kind=="AcidRain"||f.kind=="Vampire";
                if(areaSound)anchor.position=new Bounds(f.center,f.size).ClosestPoint(e.Listener.position);
                float reach=Mathf.Max(12,f.size.magnitude*.65f),ferocity=1;
                if(f.kind=="Tornado"&&f.values!=null&&f.values.Length>3)
                {
                    var position=f.center;
                    position.y=Mathf.Clamp(e.Listener.position.y,f.center.y,f.center.y+Mathf.Max(1,f.values[0]));
                    anchor.position=position;
                    reach=Mathf.Max(24,f.values[3]*2);
                    ferocity=Mathf.Clamp01(f.values[2]/40);
                }
                if(f.kind=="Fire"&&f.blocks!=null&&f.blocks.Length>0&&Services.World!=null)
                {
                    var bodies=Services.World.Blocks;
                    float nearest=float.PositiveInfinity;
                    if(target.nearestFire>=0&&target.nearestFire<f.blocks.Length)
                    {
                        int id=f.blocks[target.nearestFire];
                        if(id>=0&&id<bodies.Count&&bodies[id])
                        {anchor.position=bodies[id].position;nearest=(anchor.position-e.Listener.position).sqrMagnitude;}
                    }
                    target.fireCursor%=f.blocks.Length;
                    for(int i=0;i<Mathf.Min(f.blocks.Length,24);i++)
                    {
                        int index=target.fireCursor;target.fireCursor=(index+1)%f.blocks.Length;
                        int id=f.blocks[index];
                        if(id<0||id>=bodies.Count||!bodies[id])continue;
                        float squared=(bodies[id].position-e.Listener.position).sqrMagnitude;
                        if(squared>=nearest)continue;
                        nearest=squared;anchor.position=bodies[id].position;target.nearestFire=index;
                    }
                    reach=45;
                }
                float distance=Vector3.Distance(e.Listener.position,anchor.position),proximity=1-Mathf.Clamp01(distance/reach);
                if(f.warning>0)
                {
                    if(!target.warned)target.warned=Warn(e,kind,f.warning);
                    continue;
                }
                if(!target.started)
                {
                    target.started=true;
                    if(target.warned||f.age<1)PlayDisasterArrival(e,kind,anchor.position);
                }
                if(kind=="AcidRain"){AcidRain(e,target,f,proximity);continue;}
                if(kind=="Vampire")sunExposed|=VampireExposed(f);
                CandySound? sound=f.kind=="Tornado"?CandySound.CycloneWind:f.kind=="Earthquake"?CandySound.EarthRumble:f.kind=="Fire"?CandySound.FireCrackle:f.kind=="Icey"?CandySound.FrostCreak:f.kind=="AntiGravity"?CandySound.LowGravityHum:f.kind=="PowerOutage"?CandySound.OutageHum:null;
                float intensity=f.kind=="Tornado"?.5f+.5f*ferocity:1;
                if(f.values!=null&&f.values.Length>0)
                {
                    if(f.kind=="AntiGravity")intensity=Mathf.Clamp01(1-f.values[0])*.45f;
                    if(f.kind=="Earthquake")intensity=Mathf.Clamp01(f.values[0]/25);
                    if(f.kind=="Icey")intensity=Mathf.Clamp01(f.values[0])*.55f;
                }
                if(f.kind=="PowerOutage")intensity=.35f;
                if(sound.HasValue)e.Loop(sound.Value,anchor,(.22f+.65f*proximity)*intensity,false,Mathf.Lerp(.85f,1.15f,proximity));
                if(kind=="Tornado")Tornado(e,target,f,proximity,ferocity);
                if(proximity>.1f&&(f.kind=="Tornado"||f.kind=="Earthquake"))Pulse(proximity*.17f,.12f);
                if(kind=="MeteorShower"||kind=="PlanetKillerMeteor")MeteorFlybys(e,key,f);
            }
            EndDisasterScan(e,rewinding,sunExposed,sameSession);
            expired.Clear();foreach(var pair in anchors)if(!seen.Contains(pair.Key))expired.Add(pair.Key);
            foreach(var key in expired)
            {
                var state=anchors[key];
                if(sameSession&&state.started&&key.StartsWith("PowerOutage:"))
                    e.Play(CandySound.PowerUp,e.Listener.position,.75f,GetInstanceID(),local:true);
                if(state.anchor)Destroy(state.anchor.gameObject);
                anchors.Remove(key);
            }
        }
        void OnDestroy(){if(instance==this)instance=null;}
    }
}
