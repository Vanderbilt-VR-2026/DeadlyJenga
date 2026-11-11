using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;
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
                case "PowerOutage":
                    p.Add(new DisasterParameter("bulbs","Neon blocks",0,150,24,"",true));
                    p.Add(new DisasterParameter("rim","Edge glow reach",2,30,9,"m"));break;
                case "Vampire":
                    p.Add(new DisasterParameter("damage","Sunlight damage",0,40,9,"HP/s"));
                    p.Add(new DisasterParameter("delay","Exposure grace",.1f,3,.55f,"s"));
                    p.Add(new DisasterParameter("elevation","Sun elevation",10,85,45,"°"));break;
                case "MeteorShower":
                    p.Add(new DisasterParameter("size","Average diameter",.5f,15,3,"m"));
                    p.Add(new DisasterParameter("deviation","Size deviation",0,6,1,"m"));
                    p.Add(new DisasterParameter("frequency","Meteors per second",.5f,40,12,"/s"));
                    p.Add(new DisasterParameter("force","Impact force",0,40,12,"m/s"));break;
                case "PlanetKillerMeteor":p.Add(new DisasterParameter("size","Meteor diameter",10,150,45,"m"));break;
                case "Tornado":
                    p.Add(new DisasterParameter("height","Height",10,160,65,"m"));
                    p.Add(new DisasterParameter("size","Funnel diameter",3,50,16,"m"));
                    p.Add(new DisasterParameter("force","Ferocity",0,40,7,"m/s²"));
                    p.Add(new DisasterParameter("reach","Pull-in distance",5,120,28,"m"));break;
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
                    p.Add(new DisasterParameter("gravity","Gravity remaining",.05f,1,.18f,"×"));
                    p.Add(new DisasterParameter("fallSpeed","Maximum falling speed",1,20,4,"m/s"));break;
            }
            p.Add(new DisasterParameter("warning","Warning time",0,15,3,"s"));
            if(!Toggle&&Kind!="PlanetKillerMeteor")p.Add(new DisasterParameter("duration",Kind=="Fire"?"Spread duration":"Duration",3,120,Kind=="Fire"?60:25,"s"));
            if(Kind=="Fire")
            {
                p.Add(new DisasterParameter("burnSeconds","Block burn time",3,120,30,"s"));
                p.Add(new DisasterParameter("neverEnd","Never end",0,1,0,"",true));
                p.Add(new DisasterParameter("smokeSeconds","Smoke after burnout",0,90,20,"s"));
            }
            return p.ToArray();
        }
        protected override void BeginHazard(IWorldQuery map,Bounds bounds,float intensity,float seconds)
        {
            EnsureValues();world=map;area=bounds;strength=intensity;age=nextTick=0;started=false;cached=null;capturedAt=-1;
            if(Kind=="Tornado"){tornadoCursor=0;tornadoBudgetFrame=-1;tornadoPushAt.Clear();BeginTornadoRoute();}
            DisasterStates.Active.Add(this);
            if(Kind=="Vampire")GameEvents.Eliminated+=ResetSunlight;
        }
        bool started;
        void FixedUpdate()
        {
            if(!Active)return;
            capturedAt=-1;
            if(Services.Session is IWorldAuthority authority&&!authority.HasWorldAuthority){End();return;}
            float dt=Time.fixedDeltaTime;age+=dt;
            if(!Toggle&&Kind!="PlanetKillerMeteor"&&Kind!="Fire"&&age>=V("warning")+V("duration")){End();return;}
            if(age<V("warning"))return;
            if(!started){started=true;if(Kind=="Fire")SeedFire();if(Kind=="PowerOutage")SeedLights();}
            if(Kind=="MeteorShower"||Kind=="PlanetKillerMeteor")TickMeteors(dt);
            if(Kind=="AntiGravity")TickGravity(dt);
            if(Kind=="Tornado"){AdvanceTornadoRoute(dt);TickTornadoBlocks(dt);}
            if(Kind=="Earthquake")
            {
                TickEarthquake(dt);
                return;
            }
            if(age<nextTick)return;
            nextTick=age+.1f;
            switch(Kind)
            {
                case "Vampire":TickSun(.1f);break;
                case "Icey":ApplyIce();break;
                case "Fire":TickFire(.1f);if(started&&burning.Count==0)End();break;
                case "Tornado":TickTornadoPlayers(.1f);break;
                case "AcidRain":TickRain(.1f);break;
            }
        }
        readonly Dictionary<IDamageable,float> sunlight = new Dictionary<IDamageable,float>();
        readonly List<IDamageable> expiredSunlight = new List<IDamageable>();
        void SeedLights()
        {
            var ids = new List<int>();
            for(int i=0;i<world.Blocks.Count;i++)if(world.Blocks[i])ids.Add(i);
            int count=Mathf.Min(ids.Count,Mathf.RoundToInt(V("bulbs")));
            for(int i=0;i<count;i++){int j=UnityEngine.Random.Range(i,ids.Count);int swap=ids[i];ids[i]=ids[j];ids[j]=swap;burning.Add(ids[i]);}
        }
        void ResetSunlight(GameObject victim,GameObject source)
        {
            var health=victim?victim.GetComponentInParent<IDamageable>():null;
            if(health!=null)sunlight.Remove(health);
        }
        void TickSun(float dt)
        {
            float elevation=V("elevation")*Mathf.Deg2Rad;
            var towardSun=new Vector3(.6f*Mathf.Cos(elevation),Mathf.Sin(elevation),.8f*Mathf.Cos(elevation));
            int n=Physics.OverlapBoxNonAlloc(area.center,area.extents+Vector3.one*5,hits,Quaternion.identity,1<<Layers.Survivor,QueryTriggerInteraction.Collide);
            receivers.Clear();
            for(int i=0;i<n;i++)
            {
                var health=hits[i].GetComponentInParent<IDamageable>();
                if(health==null||health.Health<=0||!receivers.Add(health))continue;
                var eye=hits[i].bounds.center+Vector3.up*hits[i].bounds.extents.y*.65f;
                bool exposed=!Physics.Raycast(eye,towardSun,1000,1<<Layers.Block,QueryTriggerInteraction.Ignore);
                sunlight.TryGetValue(health,out float accumulated);
                accumulated=Mathf.MoveTowards(accumulated,exposed?V("delay")+1:0,dt);
                sunlight[health]=accumulated;
                if(exposed&&accumulated>=V("delay"))health.Damage(V("damage")*dt*strength,gameObject);
            }
            expiredSunlight.Clear();
            foreach(var pair in sunlight)
                if(!receivers.Contains(pair.Key)||pair.Key.Health<=0)expiredSunlight.Add(pair.Key);
            foreach(var health in expiredSunlight)sunlight.Remove(health);
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
            var push=HitAbilities.Get<IPushable>(collider);var health=HitAbilities.Get<IDamageable>(collider);var body=HitAbilities.Get<Rigidbody>(collider);
            object key=(object)push??(object)health??body;
            if(key==null||!receivers.Add(key))return;
            if(impulse.sqrMagnitude>0)
            {if(push!=null)push.Push(Vector3.ClampMagnitude(impulse*strength,30),gameObject);else if(body&&!body.isKinematic)body.AddForce(Vector3.ClampMagnitude(impulse*strength,30),ForceMode.VelocityChange);}
            if(damage>0)health?.Damage(damage*strength,gameObject);
        }
        const int TornadoScanBudget=192,TornadoPushBudget=32,TornadoWakeBudget=6;
        static readonly ProfilerMarker TornadoForces=new ProfilerMarker("DJ.Tornado.Forces");
        sealed class TornadoTarget
        {
            public Collider[] shapes;public int cursor;public float pushedAt=-1;
        }
        readonly Dictionary<Rigidbody,TornadoTarget> tornadoPushAt=new Dictionary<Rigidbody,TornadoTarget>();
        int tornadoCursor,tornadoBudgetFrame=-1,tornadoFrameScans,tornadoFramePushes,tornadoFrameWakes,tornadoFrameShapes,tornadoFrameDiscoveries;

        Vector3 TornadoImpulse(Vector3 center,Vector3 position,float radius,float height,float force)
        {
            if(position.y<center.y-2||position.y>center.y+height)return Vector3.zero;
            Vector3 delta=center-position;delta.y=0;
            float square=delta.sqrMagnitude;
            if(square>=radius*radius)return Vector3.zero;
            float distance=Mathf.Sqrt(square),falloff=1-distance/radius;
            Vector3 inward=distance>.01f?delta/distance:Vector3.right;
            return (inward*.6f+Vector3.Cross(Vector3.up,inward)+Vector3.up*(.45f+falloff))*force*falloff;
        }

        void TickTornadoBlocks(float dt)
        {
            float force=V("force")*strength*tornadoPower;
            if(force<=0)return;
            using var sample=TornadoForces.Auto();
            var blocks=world.Blocks;
            int count=blocks.Count;
            if(count==0)return;
            float radius=V("reach"),height=V("height");Vector3 center=TornadoCenter;
            if(tornadoBudgetFrame!=Time.frameCount)
            {
                tornadoBudgetFrame=Time.frameCount;
                tornadoFrameScans=tornadoFramePushes=tornadoFrameWakes=tornadoFrameShapes=tornadoFrameDiscoveries=0;
            }
            int scans=Mathf.Min(count,Mathf.Min(TornadoScanBudget,TornadoScanBudget*2-tornadoFrameScans));
            int pushLimit=Mathf.Min(TornadoPushBudget,TornadoPushBudget*2-tornadoFramePushes);
            int wakeLimit=Mathf.Min(TornadoWakeBudget,TornadoWakeBudget*2-tornadoFrameWakes);
            int pushes=0,wakes=0,shapes=0,discoveries=0;
            // Keep the cursor at the next body when a budget is spent. Every block gets a turn.
            for(int scanned=0;scanned<scans&&pushes<pushLimit&&wakes<wakeLimit&&shapes<256&&tornadoFrameShapes<512;scanned++)
            {
                tornadoFrameScans++;
                if(tornadoCursor>=count)tornadoCursor=0;
                var body=blocks[tornadoCursor++];
                if(!body||!body.gameObject.activeInHierarchy)continue;
                if(!tornadoPushAt.TryGetValue(body,out var target))
                {
                    // Discovery also walks a hierarchy. Defer this body when its separate budget is spent.
                    if(discoveries>=8||tornadoFrameDiscoveries>=16){tornadoCursor--;break;}
                    discoveries++;tornadoFrameDiscoveries++;
                    target=new TornadoTarget{shapes=body.GetComponentsInChildren<Collider>()};
                    tornadoPushAt.Add(body,target);
                }
                Vector3 point=body.worldCenterOfMass;
                var acceleration=TornadoImpulse(center,point,radius,height,force);
                // A long compound can cross the funnel even when its center is outside.
                if(acceleration.sqrMagnitude<.0001f)
                {
                    for(int j=0;j<Mathf.Min(8,target.shapes.Length)&&shapes<256&&tornadoFrameShapes<512;j++)
                    {
                        if(target.cursor>=target.shapes.Length)target.cursor=0;
                        var shape=target.shapes[target.cursor++];shapes++;tornadoFrameShapes++;
                        if(!shape||!shape.enabled||shape.isTrigger||shape.attachedRigidbody!=body)continue;
                        var bounds=shape.bounds;
                        if(bounds.max.y<center.y-2||bounds.min.y>center.y+height)continue;
                        point=shape.ClosestPoint(new Vector3(center.x,Mathf.Clamp(center.y+height*.5f,bounds.min.y,bounds.max.y),center.z));
                        acceleration=TornadoImpulse(center,point,radius,height,force);
                        if(acceleration.sqrMagnitude>=.0001f)break;
                    }
                }
                if(acceleration.sqrMagnitude<.0001f)continue;
                var push=body.GetComponent<IPushable>();
                // Magnets and grouped bodies release through their existing push ability.
                if(body.isKinematic&&push==null)continue;
                if(body.GetComponent<IBreakable>() is IBreakable breakable&&breakable.Broken)continue;
                if(target.pushedAt==age)continue;
                float interval=target.pushedAt>=0?Mathf.Clamp(age-target.pushedAt,dt,.18f):.1f;
                target.pushedAt=age;
                if(body.isKinematic||body.IsSleeping()){wakes++;tornadoFrameWakes++;}
                Vector3 impulse=Vector3.ClampMagnitude(acceleration*interval,30);
                if(push!=null)push.Push(impulse,gameObject);
                else body.AddForce(impulse,ForceMode.VelocityChange);
                pushes++;tornadoFramePushes++;
            }
        }

        void TickTornadoPlayers(float dt)
        {
            float force=V("force")*strength*tornadoPower;
            if(force<=0)return;
            Vector3 center=TornadoCenter;
            float radius=V("reach"),height=V("height");
            var halfHeight=(height+2)*.5f;
            int n=Physics.OverlapBoxNonAlloc(center+Vector3.up*(halfHeight-2),new Vector3(radius,halfHeight,radius),
                hits,Quaternion.identity,1<<Layers.Survivor,QueryTriggerInteraction.Collide);
            receivers.Clear();
            for(int i=0;i<n;i++)
            {
                var collider=hits[i];if(!collider)continue;
                var impulse=TornadoImpulse(center,collider.bounds.center,radius,height,force)*dt;
                if(impulse.sqrMagnitude<=0)continue;
                var push=HitAbilities.Get<IPushable>(collider);
                if(push!=null&&receivers.Add(push))push.Push(Vector3.ClampMagnitude(impulse,30),gameObject);
                else if(push==null&&collider.attachedRigidbody is Rigidbody body&&body&&!body.isKinematic&&receivers.Add(body))
                    body.AddForce(Vector3.ClampMagnitude(impulse,30),ForceMode.VelocityChange);
            }
        }
        Vector3 TornadoCenter, tornadoStart, tornadoExit, tornadoVelocity;
        float tornadoAngle, tornadoTurn, tornadoWeave, tornadoPower;

        void BeginTornadoRoute()
        {
            tornadoAngle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            tornadoTurn = UnityEngine.Random.Range(.85f, 1.65f) * Mathf.PI * 2f;
            if (UnityEngine.Random.value < .5f) tornadoTurn = -tornadoTurn;
            tornadoWeave = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            tornadoStart = Offshore(tornadoAngle);
            tornadoExit = Offshore(tornadoAngle + tornadoTurn);
            TornadoCenter = tornadoStart;
            tornadoVelocity = Vector3.zero;
            tornadoPower = 0f;
        }

        Vector3 Offshore(float angle)
        {
            var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            float envelope = new Vector2(area.extents.x, area.extents.z).magnitude;
            float step = Mathf.Max(12f, V("size"));
            float distance = Mathf.Max(envelope, world.Radius) + step;
            Vector3 point = area.center;
            for (int i = 0; i < 6; i++)
            {
                point = area.center + direction * (distance + i * step);
                point.y = world.WaterHeight(point);
                // Some islands extend past the authored play area.
                float top = Mathf.Max(area.max.y + 1f, point.y + 12f);
                var above = new Vector3(point.x, top, point.z);
                if (!Physics.Raycast(above, Vector3.down, top - point.y + .1f,
                    1 << Layers.Surface, QueryTriggerInteraction.Ignore)) break;
            }
            return point;
        }

        void AdvanceTornadoRoute(float dt)
        {
            float progress = Mathf.Clamp01((age - V("warning")) / V("duration"));
            float orbit = Mathf.InverseLerp(.24f, .8f, progress);
            float angle = tornadoAngle + tornadoTurn * orbit +
                Mathf.Sin(orbit * Mathf.PI * 2f) * .28f;
            float radius = .49f + .24f * Mathf.Sin(orbit * Mathf.PI * 5f + tornadoWeave);
            var inland = area.center + new Vector3(Mathf.Cos(angle) * area.extents.x * radius, 0f,
                Mathf.Sin(angle) * area.extents.z * radius);
            float entry = Mathf.SmoothStep(0f, 1f, progress / .24f);
            float departure = Mathf.SmoothStep(0f, 1f, (progress - .8f) / .2f);
            Vector3 next = Vector3.Lerp(Vector3.Lerp(tornadoStart, inland, entry), tornadoExit, departure);
            next.y = world.WaterHeight(next);
            tornadoVelocity = (next - TornadoCenter) / dt;
            TornadoCenter = next;
            tornadoPower = Mathf.SmoothStep(0f, 1f, progress / .12f) * (1f - departure);
        }
        void TickEarthquake(float dt)
        {
            float phase=(age-V("warning"))*V("frequency")*Mathf.PI*2;
            var impulse=new Vector3(Mathf.Sin(phase),Mathf.Sin(phase*1.31f)*V("vertical"),Mathf.Cos(phase*.83f))*V("force")*dt;
            foreach(var b in world.Blocks)
            {
                if(!b||!b.gameObject.activeInHierarchy)continue;
                if(b.isKinematic)
                {
                    if(b.TryGetComponent<IHazardMotion>(out _)&&b.TryGetComponent<IPushable>(out var held))
                        held.Push(impulse*strength,gameObject);
                    continue;
                }
                b.AddForce(impulse*strength,ForceMode.VelocityChange);
                b.AddTorque(new Vector3(impulse.z,0,-impulse.x)*.08f,ForceMode.VelocityChange);
            }
            // Shaking the support already moves riders. A combat impulse here repeatedly broke traversal.
        }
        void TickRain(float dt)=>AffectPlayers(Vector3.zero,V("damage")*dt,true);
        void TickGravity(float dt)
        {
            float scale=Mathf.Clamp(V("gravity"),.05f,1);
            foreach(var b in world.Blocks)if(b&&!b.isKinematic&&b.useGravity&&!b.IsSleeping())
            {
                // Cancel only part of natural gravity, every physics step. Never add lift.
                b.AddForce(-Physics.gravity*(1-scale),ForceMode.Acceleration);
                var velocity=b.linearVelocity;
                if(velocity.y < -V("fallSpeed")){velocity.y=-V("fallSpeed");b.linearVelocity=velocity;}
            }
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
            return cached=new DisasterFrame{sampledAt=Time.time,kind=Kind,label=Kind=="AntiGravity"?"Low Gravity":Type?Type.name:Kind,center=Kind=="Tornado"?TornadoCenter:area.center,size=area.size,age=age,warning=Mathf.Max(0,V("warning")-age),remaining=Kind=="Fire"?FireRemaining():Toggle||Kind=="PlanetKillerMeteor"?-1:Mathf.Max(0,V("duration")+V("warning")-age),values=values,points=Kind=="Tornado"?new[]{new DisasterPoint{position=TornadoCenter,velocity=tornadoVelocity}}:meteors.ToArray(),blocks=burning.ToArray(),burns=Kind=="Fire"?CaptureBurns():null};
        }
        protected override void EndHazard()
        {
            if(Kind=="Fire")foreach(int id in burning)Scorch(id);
            GameEvents.Eliminated-=ResetSunlight;
            DisasterStates.Active.Remove(this);sunlight.Clear();expiredSunlight.Clear();if(Kind=="Tornado")tornadoPushAt.Clear();
            foreach(var pair in originals)if(pair.Key&&pair.Key.sharedMaterial==ice)pair.Key.sharedMaterial=pair.Value;
            originals.Clear();if(ice)Destroy(ice);ice=null;meteors.Clear();burning.Clear();heat.Clear();lastContact.Clear();lastFireDamage.Clear();blockIds.Clear();ignited.Clear();fireStarted.Clear();fireScars=null;meteorClock=0;launched=false;
        }
    }
}
