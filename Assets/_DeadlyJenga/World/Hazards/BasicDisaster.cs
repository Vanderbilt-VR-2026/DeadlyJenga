using System;
using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.World.Hazards
{
    public abstract partial class BasicDisaster : MapHazard, IDemoHazard, IDisasterState
    {
        protected abstract string Kind { get; }
        DisasterParameter[] parameters;
        float[] values;
        protected IWorldQuery world;
        protected Bounds area;
        float age, nextTick, strength;
        protected Collider[] hits = new Collider[2048];
        readonly HashSet<object> receivers = new HashSet<object>();
        readonly Dictionary<Collider, PhysicsMaterial> originals = new Dictionary<Collider, PhysicsMaterial>();
        PhysicsMaterial ice;
        public bool Toggle => Kind=="Icey";
        public IReadOnlyList<DisasterParameter> Parameters => parameters ?? (parameters=MakeParameters());
        protected float V(string id)
        {
            EnsureValues();
            for(int i=0;i<parameters.Length;i++)if(parameters[i].id==id)return values[i];
            return 0;
        }
        void EnsureValues()
        { var p=Parameters; if(values==null){values=new float[p.Count];for(int i=0;i<p.Count;i++)values[i]=p[i].initial;} }
        public void Configure(float[] input)
        {
            if(Active||input==null||input.Length!=Parameters.Count)throw new ArgumentException("Disaster settings do not match the parameter schema.");
            for(int i=0;i<input.Length;i++)if(float.IsNaN(input[i])||float.IsInfinity(input[i]))throw new ArgumentException("Disaster settings must be finite.");
            values=new float[input.Length];for(int i=0;i<input.Length;i++)values[i]=parameters[i].Clamp(input[i]);
        }
        DisasterParameter[] MakeParameters()
        {
            var p=new List<DisasterParameter>();
            switch(Kind)
            {
                case "MeteorShower":
                    p.Add(new DisasterParameter("size","Average diameter",.5f,15,3,"m"));
                    p.Add(new DisasterParameter("deviation","Size deviation",0,6,1,"m"));
                    p.Add(new DisasterParameter("frequency","Meteors per second",.5f,40,12,"/s"));
                    p.Add(new DisasterParameter("force","Impact force",0,40,12,"m/s"));break;
                case "PlanetKillerMeteor":p.Add(new DisasterParameter("size","Meteor diameter",10,150,45,"m"));break;
                case "Tornado":
                    p.Add(new DisasterParameter("height","Height",10,160,65,"m"));
                    p.Add(new DisasterParameter("size","Funnel diameter",3,50,16,"m"));
                    p.Add(new DisasterParameter("force","Ferocity",0,40,12,"m/s²"));
                    p.Add(new DisasterParameter("reach","Pull-in distance",5,120,38,"m"));break;
                case "Fire":
                    p.Add(new DisasterParameter("damage","Contact damage",0,50,8,"HP/s"));
                    p.Add(new DisasterParameter("contact","Contact time to spread",.2f,15,2,"s"));
                    p.Add(new DisasterParameter("seeds","Initially burning blocks",1,100,8,"",true));break;
                case "Icey":p.Add(new DisasterParameter("slip","Slipperiness",.5f,1,1));break;
                case "Earthquake":
                    p.Add(new DisasterParameter("force","Shake acceleration",0,25,8,"m/s²"));
                    p.Add(new DisasterParameter("frequency","Shake frequency",.2f,8,2,"Hz"));
                    p.Add(new DisasterParameter("vertical","Vertical shake",0,1,.25f));break;
                case "AcidRain":
                    p.Add(new DisasterParameter("damage","Exposed damage",0,40,6,"HP/s"));
                    p.Add(new DisasterParameter("density","Rain density",10,600,180,"drops/s"));break;
                case "AntiGravity":
                    p.Add(new DisasterParameter("lift","Upward acceleration",0,40,24,"m/s²"));
                    p.Add(new DisasterParameter("drift","Sideways drift",0,8,2,"m/s²"));break;
            }
            p.Add(new DisasterParameter("warning","Warning time",0,15,3,"s"));
            if(!Toggle&&Kind!="PlanetKillerMeteor")p.Add(new DisasterParameter("duration","Duration",3,120,Kind=="Fire"?60:25,"s"));
            return p.ToArray();
        }
        protected override void BeginHazard(IWorldQuery map,Bounds bounds,float intensity,float seconds)
        {
            EnsureValues();world=map;area=bounds;strength=intensity;age=nextTick=0;started=false;cached=null;capturedAt=-1;
            DisasterStates.Active.Add(this);
        }
        bool started;
        void FixedUpdate()
        {
            if(!Active)return;
            capturedAt=-1;
            if(Services.Session is IWorldAuthority authority&&!authority.HasWorldAuthority){End();return;}
            float dt=Time.fixedDeltaTime;age+=dt;
            if(!Toggle&&Kind!="PlanetKillerMeteor"&&age>=V("warning")+V("duration")){End();return;}
            if(age<V("warning"))return;
            if(!started){started=true;if(Kind=="Fire")SeedFire();}
            if(Kind=="MeteorShower"||Kind=="PlanetKillerMeteor")TickMeteors(dt);
            if(age<nextTick)return;
            nextTick=age+.1f;
            switch(Kind)
            {
                case "Icey":ApplyIce();break;
                case "Fire":TickFire(.1f);break;
                case "Tornado":TickTornado(.1f);break;
                case "Earthquake":TickEarthquake(.1f);break;
                case "AcidRain":TickRain(.1f);break;
                case "AntiGravity":TickGravity(.1f);break;
            }
        }
        protected int Query(Vector3 center,float radius)
        {
            int n;
            do {n=Physics.OverlapSphereNonAlloc(center,radius,hits,(1<<Layers.Block)|(1<<Layers.Survivor)|(1<<Layers.Surface),QueryTriggerInteraction.Collide);if(n<hits.Length||hits.Length>=32768)break;Array.Resize(ref hits,hits.Length*2);}while(true);
            return n;
        }
        protected void Affect(Collider collider,Vector3 impulse,float damage)
        {
            if(!collider)return;
            var push=collider.GetComponentInParent<IPushable>();var health=collider.GetComponentInParent<IDamageable>();var body=collider.attachedRigidbody;
            object key=(object)push??(object)health??body;
            if(key==null||!receivers.Add(key))return;
            if(impulse.sqrMagnitude>0)
            {if(push!=null)push.Push(Vector3.ClampMagnitude(impulse*strength,30),gameObject);else if(body&&!body.isKinematic)body.AddForce(Vector3.ClampMagnitude(impulse*strength,30),ForceMode.VelocityChange);}
            if(damage>0)health?.Damage(damage*strength,gameObject);
        }
        void TickTornado(float dt)
        {
            Vector3 c=TornadoCenter;float radius=V("reach"),height=V("height");int n=Query(c+Vector3.up*height*.5f,Mathf.Sqrt(radius*radius+height*height*.25f));receivers.Clear();
            for(int i=0;i<n;i++)
            {
                var p=hits[i].bounds.center;var delta=Vector3.ProjectOnPlane(c-p,Vector3.up);float d=delta.magnitude;
                if(d>radius||p.y<c.y-2||p.y>c.y+height)continue;
                var inward=d>.01f?delta/d:Vector3.right;float k=1-Mathf.Clamp01(d/radius);
                var swirl=Vector3.Cross(Vector3.up,inward);
                Affect(hits[i],(inward*.6f+swirl+Vector3.up*(.45f+k))*V("force")*dt*k,0);
            }
        }
        Vector3 TornadoCenter=>new Vector3(area.center.x+Mathf.Sin(age*.18f)*area.extents.x*.35f,world.WaterHeight(area.center),area.center.z+Mathf.Cos(age*.14f)*area.extents.z*.35f);
        void TickEarthquake(float dt)
        {
            float phase=(age-V("warning"))*V("frequency")*Mathf.PI*2;
            var impulse=new Vector3(Mathf.Sin(phase),Mathf.Sin(phase*1.31f)*V("vertical"),Mathf.Cos(phase*.83f))*V("force")*dt;
            foreach(var b in world.Blocks)if(b&&!b.isKinematic){b.AddForce(impulse*strength,ForceMode.VelocityChange);b.AddTorque(new Vector3(impulse.z,0,-impulse.x)*.08f,ForceMode.VelocityChange);}
            AffectPlayers(impulse*.45f,0,false);
        }
        void TickRain(float dt)=>AffectPlayers(Vector3.zero,V("damage")*dt,true);
        void TickGravity(float dt)
        {
            foreach(var b in world.Blocks)if(b&&!b.isKinematic)
            {var drift=new Vector3(Mathf.Sin(age*.7f+b.position.z),0,Mathf.Cos(age*.6f+b.position.x))*V("drift");b.AddForce((-Physics.gravity+Vector3.up*V("lift")+drift)*dt*strength,ForceMode.VelocityChange);}
            AffectPlayers(Vector3.up*V("lift")*dt,0,false);
        }
        void AffectPlayers(Vector3 impulse,float damage,bool sheltered)
        {
            int n=Physics.OverlapBoxNonAlloc(area.center,area.extents+Vector3.one*5,hits,Quaternion.identity,1<<Layers.Survivor,QueryTriggerInteraction.Collide);receivers.Clear();
            for(int i=0;i<n;i++)if(!sheltered||!world.IsSheltered(hits[i].bounds.center))Affect(hits[i],impulse,damage);
        }
        void ApplyIce()
        {
            if(!ice)ice=new PhysicsMaterial("Disaster ice (temporary)"){staticFriction=Mathf.Lerp(.04f,.005f,V("slip")),dynamicFriction=Mathf.Lerp(.02f,.002f,V("slip")),frictionCombine=PhysicsMaterialCombine.Minimum};
            foreach(var body in world.Blocks)if(body)MakeSlippery(body.GetComponent<Collider>());
            int n;
            do {n=Physics.OverlapBoxNonAlloc(area.center,area.extents+Vector3.one*5,hits,Quaternion.identity,1<<Layers.Surface,QueryTriggerInteraction.Ignore);if(n<hits.Length||hits.Length>=32768)break;Array.Resize(ref hits,hits.Length*2);}while(true);
            for(int i=0;i<n;i++)MakeSlippery(hits[i]);
        }
        void MakeSlippery(Collider collider){if(collider&&!originals.ContainsKey(collider)){originals.Add(collider,collider.sharedMaterial);collider.sharedMaterial=ice;}}
        DisasterFrame cached;int capturedAt=-1;
        public DisasterFrame Capture()
        {
            if(!Active)return null;
            if(capturedAt==Time.frameCount&&cached!=null)return cached;
            capturedAt=Time.frameCount;
            return cached=new DisasterFrame{sampledAt=Time.time,kind=Kind,label=Type?Type.name:Kind,center=Kind=="Tornado"?TornadoCenter:area.center,size=area.size,age=age,warning=Mathf.Max(0,V("warning")-age),remaining=Toggle||Kind=="PlanetKillerMeteor"?-1:Mathf.Max(0,V("duration")+V("warning")-age),values=values,points=meteors.ToArray(),blocks=burning.ToArray()};
        }
        protected override void EndHazard()
        {
            DisasterStates.Active.Remove(this);
            foreach(var pair in originals)if(pair.Key&&pair.Key.sharedMaterial==ice)pair.Key.sharedMaterial=pair.Value;
            originals.Clear();if(ice)Destroy(ice);ice=null;meteors.Clear();burning.Clear();heat.Clear();lastContact.Clear();lastFireDamage.Clear();blockIds.Clear();ignited.Clear();meteorClock=0;launched=false;
        }
    }
}
