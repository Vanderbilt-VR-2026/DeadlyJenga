using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.World.Hazards
{
    // The actor point carries stage origin, facing, scale and authored animation time.
    // The second point is the authoritative swept flame segment, including its first obstruction.
    public abstract class ShrineDragonHazard : MapHazard, IDemoHazard, IDisasterState
    {
        [Serializable] sealed class Sample { public Vector3 mouth, left, right; }
        [Serializable] sealed class Clip { public int start; public Sample[] samples; }
        [Serializable] sealed class Motion { public Clip[] clips; }
        [SerializeField] TextAsset motion;
        protected abstract bool Airborne { get; }
        protected virtual void IgniteImpact(Rigidbody block) { }
        static readonly DisasterParameter[] Flight = {
            new("passes","Strafing passes",1,8,4,"",true), new("period","Seconds per pass",6,16,10,"s"),
            new("radius","Offshore orbit radius",1.2f,2.6f,1.65f,"× map"),
            new("range","Breath reach",1.5f,4,3,"× map"), new("damage","Flame damage",1,60,16,"HP/s") };
        static readonly DisasterParameter[] Siege = {
            new("cycles","Attack sequences",1,8,3,"",true), new("period","Seconds per sequence",4,12,6,"s"),
            new("force","Claw impact",2,28,12,"m/s"), new("range","Breath reach",.4f,2,1.2f,"× map"),
            new("damage","Flame damage",1,60,20,"HP/s") };
        public IReadOnlyList<DisasterParameter> Parameters => Airborne ? Flight : Siege;
        public bool Toggle => false;
        float[] settings;
        float Value(int i) => settings == null ? Parameters[i].initial : settings[i];
        public void Configure(float[] input)
        {
            if (Active || input == null || input.Length != Parameters.Count) throw new ArgumentException("Invalid dragon settings.");
            var next = new float[input.Length];
            for (int i=0;i<input.Length;i++) {
                if (!float.IsFinite(input[i])) throw new ArgumentException("Non-finite dragon setting.");
                next[i]=Parameters[i].Clamp(input[i]);
            }
            settings=next;
        }
        Motion tracks;
        IWorldQuery map;
        Bounds area;
        float age, total, radius, scale, heading, power, clock, previousClipTime, nextHit, nextBreath, nextSteam, steamUntil;
        int clipIndex, lastCycle;
        Vector3 position, forward, shore, inward, mouth, left, right, previousLeft, previousRight, flameEnd, target;
        bool flaming, gripping;
        Rigidbody grabbed;
        Vector3 gripOffset;
        readonly Collider[] contacts = new Collider[128];
        readonly HashSet<object> struck = new();
        readonly Dictionary<Rigidbody,float> heatDamage = new();
        readonly DisasterPoint[] points = new DisasterPoint[2];
        DisasterFrame frame;
        const int Mask = (1 << Layers.Block) | (1 << Layers.Survivor) | (1 << Layers.Surface);
        protected override bool ValidateSettings(out string error)
        {
            error=motion ? null : "Assign the baked dragon socket track.";
            return error==null;
        }
        protected override void BeginHazard(IWorldQuery world, Bounds bounds, float strength, float seconds)
        {
            tracks ??= JsonUtility.FromJson<Motion>(motion.text);
            map=world; area=bounds; power=strength; age=0;
            radius=new Vector2(bounds.extents.x,bounds.extents.z).magnitude;
            scale=Mathf.Clamp(radius*(Airborne ? .091f : .116f),1.2f,12);
            heading=UnityEngine.Random.Range(0f,360f)*Mathf.Deg2Rad;
            inward=-Radial(heading); target=ChooseBlock(-inward,true);
            shore=target-inward*(scale*1.6f)-Vector3.Cross(Vector3.up,inward)*(scale*1.56f); shore.y=world.WaterHeight(shore)-.15f*scale;
            lastCycle=-1; previousClipTime=0; nextHit=nextBreath=steamUntil=0;nextSteam=2.6f;
            total=(Airborne?8:6)+Value(0)*Value(1);
            frame=new DisasterFrame {kind=Airborne?"FlyingDragon":"DragonAttack",label=Type.name,center=area.center,size=area.size,points=points};
            DisasterStates.Active.Add(this); Step(0);
            previousLeft=left;previousRight=right;
        }
        static Vector3 Radial(float angle) => new(Mathf.Cos(angle),0,Mathf.Sin(angle));
        Vector3 ChooseBlock(Vector3 side,bool edge)
        {
            Vector3 goal=area.center+side*(edge?radius:UnityEngine.Random.Range(-.42f,.42f)*radius);
            goal.y=edge?map.WaterHeight(goal)+scale*.45f:area.center.y;
            float best=float.PositiveInfinity;Vector3 result=goal;
            foreach(var rb in map.Blocks) {
                if (!rb || !rb.detectCollisions || !rb.gameObject.activeInHierarchy || rb.position.y<map.WaterHeight(rb.position)-2) continue;
                float d=(rb.position-goal).sqrMagnitude;
                if(d<best){best=d;result=rb.worldCenterOfMass;}
            }
            return result;
        }
        void FixedUpdate()
        {
            if(!Active)return;
            if(!ReferenceEquals(map,Services.World) || Services.Session is IWorldAuthority auth && !auth.HasWorldAuthority){End();return;}
            age+=Time.fixedDeltaTime;
            if(age>=total){End();return;}
            Step(Time.fixedDeltaTime);
        }
        void Step(float dt)
        {
            if(Airborne)Fly();else Land();
            var samples=tracks.clips[clipIndex].samples;
            float at=Mathf.Clamp(clock*12,0,samples.Length-1);
            int i=(int)at;var a=samples[i];var b=samples[Mathf.Min(i+1,samples.Length-1)];
            Quaternion rot=Quaternion.LookRotation(forward);
            mouth=position+rot*(Vector3.Lerp(a.mouth,b.mouth,at-i)*scale);
            left=position+rot*(Vector3.Lerp(a.left,b.left,at-i)*scale);
            right=position+rot*(Vector3.Lerp(a.right,b.right,at-i)*scale);
            if(!Airborne && dt>0)Claws(dt);
            if(age>=nextSteam){steamUntil=age+.9f;nextSteam=age+UnityEngine.Random.Range(4.5f,8f);}
            if(flaming)Breath();else flameEnd=mouth;
            points[0]=new DisasterPoint{id=clipIndex,position=position,velocity=forward,size=scale,life=clock};
            points[1]=new DisasterPoint{id=flaming?1:0,position=mouth,velocity=flameEnd,size=Mathf.Clamp01((steamUntil-age)*3),life=flaming?1:0};
            frame.age=age;frame.remaining=total-age;frame.sampledAt=Time.time;
            previousClipTime=clock;previousLeft=left;previousRight=right;
        }
        void Fly()
        {
            float t=age-4,phase=t/Value(1);
            int cycle=Mathf.FloorToInt(Mathf.Max(0,phase));float lane=Mathf.Repeat(phase,1);
            float angle=heading+phase*1.95f;
            Vector3 radial=Radial(angle),tangent=Radial(angle+Mathf.PI*.5f);
            if(cycle!=lastCycle){lastCycle=cycle;target=ChooseBlock(tangent,false);}
            float run=Mathf.Clamp01(Mathf.Sin(lane*Mathf.PI));
            float orbit=radius*(Value(2)+.32f*(1-run));
            float rise=Mathf.SmoothStep(0,1,Mathf.Clamp01(age/4));
            float dive=Mathf.SmoothStep(0,1,Mathf.Clamp01((total-age)/4));
            position=area.center+radial*orbit;
            position.y=Mathf.Lerp(map.WaterHeight(position)-scale*3.8f,area.max.y+scale*(2.5f-run),rise*dive);
            flaming=t>=0 && age<total-4 && lane>.22f && lane<.79f;
            Vector3 aim=target+tangent*((lane-.5f)*radius*.85f);
            forward=Vector3.Slerp(tangent,(aim-position).normalized,run*.92f).normalized;
            // Rotate the jet across actual roofs while the huge body translates across the firing lane.
            breathAim=aim;
            // One continuous cruise stroke, with the banking orbit during repositioning.
            clipIndex=run>.54f?0:1;clock=Mathf.Repeat(age,clipIndex==0?5f:7.416667f);
        }
        Vector3 breathAim;
        void Land()
        {
            float t=age-3;int cycle=Mathf.FloorToInt(Mathf.Max(0,t)/Value(1));
            if(cycle!=lastCycle){lastCycle=cycle;target=ChooseBlock(-inward,true);grabbed=null;gripping=false;struck.Clear();}
            clipIndex=0;
            if(age<3)clock=age;
            else if(age>=total-3)clock=9+age-(total-3);
            else clock=3+6*Mathf.Repeat(t/Value(1),1);
            float air=clock<3?1-Mathf.SmoothStep(0,1,clock/3):Mathf.SmoothStep(0,1,Mathf.Clamp01((clock-9)/3));
            position=shore-inward*(air*radius*.9f);
            // The entrance breaches, arcs above the water, then settles onto the palms.
            position.y=shore.y+Mathf.Sin(air*Mathf.PI)*scale*3.8f-air*air*scale*4.5f;
            forward=inward;
            flaming=age>=3 && age<total-3 && cycle%2==0 && clock>7.72f && clock<8.72f;
            breathAim=target;
        }
        void Breath()
        {
            Vector3 d=breathAim-mouth;
            float reach=Mathf.Max(scale*2,radius*Value(3));
            if(d.sqrMagnitude<.01f){flameEnd=mouth;return;}
            bool hit=Physics.SphereCast(mouth,Mathf.Clamp(scale*.075f,.10f,.6f),d.normalized,out var h,reach,Mask,QueryTriggerInteraction.Ignore);
            flameEnd=hit?h.point:mouth+d.normalized*reach;
            if(!hit || age<nextBreath)return;
            nextBreath=age+.12f;
            h.collider.GetComponentInParent<IDamageable>()?.Damage(Value(4)*power*.12f,gameObject);
            var rb=h.rigidbody;
            if(!rb || h.collider.gameObject.layer!=Layers.Block && rb.gameObject.layer!=Layers.Block)return;
            if(Airborne)IgniteImpact(rb);
            else {
                heatDamage.TryGetValue(rb,out float damage);
                damage+=Value(4)*power*.12f;
                if(damage>=12 && rb.TryGetComponent<IBreakable>(out var block)){
                    rb.GetComponent<IPushable>()?.Push(d.normalized*2,gameObject);
                    block.Break(gameObject);damage=0;
                }
                heatDamage[rb]=damage;
            }
        }
        void Claws(float dt)
        {
            if(clock<3 || clock>=9){grabbed=null;gripping=false;return;}
            if(clock>=3.9f && previousClipTime<4.27f && clock>=previousClipTime){
                int n=Physics.OverlapCapsuleNonAlloc(previousLeft,left,scale*.60f,contacts,Mask,QueryTriggerInteraction.Collide);
                for(int i=0;i<n;i++)Strike(contacts[i]);
            }
            if(clock>=5.9f && clock<7.95f){
                if(!gripping){
                    int n=Physics.OverlapCapsuleNonAlloc(previousRight,right,scale*.65f,contacts,1<<Layers.Block,QueryTriggerInteraction.Ignore);
                    float best=float.PositiveInfinity;
                    for(int i=0;i<n;i++){
                        var rb=contacts[i].attachedRigidbody;if(!rb)continue;
                        float dist=(rb.worldCenterOfMass-right).sqrMagnitude;
                        if(dist<best){best=dist;grabbed=rb;}
                    }
                    if(grabbed){gripping=true;gripOffset=grabbed.worldCenterOfMass-right;}
                }
                if(grabbed && age>=nextHit){
                    nextHit=age+.09f;
                    Vector3 v=Vector3.ClampMagnitude((right+gripOffset-grabbed.worldCenterOfMass)*7-inward*3-grabbed.linearVelocity*.45f,Value(2))*power;
                    Push(grabbed,v);
                }
            }else {grabbed=null;gripping=false;}
        }
        void Strike(Collider c)
        {
            var rb=c.attachedRigidbody;var hp=c.GetComponentInParent<IDamageable>();
            object key=(object)hp??rb;if(key==null || !struck.Add(key))return;
            hp?.Damage(Value(2)*power,gameObject);
            if(rb)Push(rb,(inward*.55f+Vector3.down*.7f)*Value(2)*power);
        }
        void Push(Rigidbody rb,Vector3 velocity)
        {
            if(rb.TryGetComponent<IPushable>(out var p))p.Push(velocity,gameObject);
            else if(!rb.isKinematic)rb.AddForce(velocity,ForceMode.VelocityChange);
        }
        public DisasterFrame Capture()=>Active?frame:null;
        protected override void EndHazard()
        {
            DisasterStates.Active.Remove(this);grabbed=null;heatDamage.Clear();struck.Clear();map=null;
        }
    }
}
