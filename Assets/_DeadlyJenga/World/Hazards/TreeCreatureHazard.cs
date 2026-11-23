using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.World.Hazards
{
    public abstract class TreeCreatureHazard : MapHazard, IDemoHazard, IDisasterState
    {
        protected abstract string Kind { get; }
        static readonly DisasterParameter[] Schema={
            new DisasterParameter("force","Impact force",0,30,12,"m/s"),
            new DisasterParameter("damage","Contact damage",0,60,25,"HP"),
            new DisasterParameter("warning","Warning time",0,15,5,"s"),
            new DisasterParameter("duration","Duration",8,90,24,"s")};
        float[] values;
        protected IWorldQuery world;
        protected Bounds area;
        protected float age, strength;
        protected DisasterPoint[] points=Array.Empty<DisasterPoint>();
        protected readonly HashSet<object> touched=new HashSet<object>();
        Collider[] contacts=new Collider[2048];
        public IReadOnlyList<DisasterParameter> Parameters=>Schema;
        public bool Toggle=>false;
        protected float V(int i)=>values==null?Schema[i].initial:values[i];
        public void Configure(float[] input)
        {
            if(Active||input==null||input.Length!=Schema.Length)throw new ArgumentException("Invalid creature parameters");
            values=new float[input.Length];
            for(int i=0;i<input.Length;i++)
            {if(float.IsNaN(input[i])||float.IsInfinity(input[i]))throw new ArgumentException("Non-finite creature parameter");values[i]=Schema[i].Clamp(input[i]);}
        }
        protected override void BeginHazard(IWorldQuery map,Bounds bounds,float intensity,float seconds)
        {world=map;area=bounds;strength=intensity;age=0;touched.Clear();Setup();DisasterStates.Active.Add(this);}
        protected abstract void Setup();
        protected abstract void Step(float dt);
        protected float Elapsed=>Mathf.Max(0,age-V(2));
        protected float Ground(Vector3 p)
        {
            if(Physics.Raycast(new Vector3(p.x,world.Center.y+3,p.z),Vector3.down,out var hit,100,
                (1<<Layers.Surface)|1,QueryTriggerInteraction.Ignore))return hit.point.y;
            return world.Center.y;
        }
        void FixedUpdate()
        {
            if(!Active)return;
            if(Services.Session is IWorldAuthority a&&!a.HasWorldAuthority){End();return;}
            age+=Time.fixedDeltaTime;
            if(age>=V(2)+V(3)){End();return;}
            if(age>=V(2))Step(Time.fixedDeltaTime);
        }
        protected void Impact(Vector3 center,Vector3 half,Quaternion rotation,Vector3 impulse)
        {
            int count;
            do
            {
                count=Physics.OverlapBoxNonAlloc(center,half,contacts,rotation,(1<<Layers.Block)|(1<<Layers.Survivor),QueryTriggerInteraction.Ignore);
                if(count<contacts.Length||contacts.Length>=32768)break;
                Array.Resize(ref contacts,contacts.Length*2);
            }while(true);
            for(int i=0;i<count;i++)
            {
                var c=contacts[i];var push=HitAbilities.Get<IPushable>(c);var health=HitAbilities.Get<IDamageable>(c);
                var body=c.attachedRigidbody;object key=(object)push??(object)health??body;
                if(key==null||!touched.Add(key))continue;
                if(impulse.sqrMagnitude>0 && strength>0 && push!=null)push.Push(Vector3.ClampMagnitude(impulse*strength,30),gameObject);
                else if(impulse.sqrMagnitude>0 && strength>0 && body&&!body.isKinematic)body.AddForce(impulse*strength,ForceMode.VelocityChange);
                if(V(1)*strength>0)health?.Damage(V(1)*strength,gameObject);
            }
        }
        public DisasterFrame Capture()=>Active?new DisasterFrame{kind=Kind,label=Type.name,center=area.center,size=area.size,
            age=age,warning=Mathf.Max(0,V(2)-age),remaining=Mathf.Max(0,V(2)+V(3)-age),points=(DisasterPoint[])points.Clone(),
            values=new[]{V(0),V(1),V(2),V(3)},sampledAt=Time.time}:null;
        protected override void EndHazard(){DisasterStates.Active.Remove(this);touched.Clear();points=Array.Empty<DisasterPoint>();}
    }
}
