using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
namespace DeadlyJenga.CastlePrototype
{
    // Uses the ordinary debris rules, with full compound bounds and occupancy guards.
    public sealed class CityDebrisCleanup:MonoBehaviour
    {
        sealed class Entry
        {
            public Rigidbody body; public Collider[] shapes; public Vector3 origin,velocity,spin;
            public Quaternion rotation; public Bounds localBounds; public bool visual,sinking; public float ground,began;
            public CollisionDetectionMode detection; public RigidbodyInterpolation interpolation;
        }
        readonly List<Entry> entries=new(); readonly Dictionary<Rigidbody,Entry> lookup=new();
        readonly Collider[] nearby=new Collider[128]; readonly RaycastHit[] casts=new RaycastHit[64];
        static readonly Unity.Profiling.ProfilerMarker CleanupMarker=new Unity.Profiling.ProfilerMarker("DJ.CityCleanup");
        bool replica; float born,nextScan; int cursor;
        public int ConvertedCount {get;private set;} public int ShatteredCount {get;private set;} public int SunkCount {get;private set;} public int RestoredCount {get;private set;}
        bool Authority=>!replica&&!(Services.Session is IWorldAuthority a&&!a.HasWorldAuthority);
        public void Initialize(IReadOnlyList<Rigidbody> bodies)
        {
            born=Time.time;
            foreach(var b in bodies)
            {
                var all=b.GetComponentsInChildren<Collider>();var solid=new List<Collider>();
                foreach(var c in all)if(c.enabled&&!c.isTrigger&&c.attachedRigidbody==b)solid.Add(c);
                var e=new Entry{body=b,shapes=solid.ToArray(),origin=b.position,rotation=b.rotation,detection=b.collisionDetectionMode,interpolation=b.interpolation};
                var bounds=new Bounds();bool first=true;
                foreach(var c in e.shapes)
                {
                    var bb=c.bounds;for(int x=0;x<2;x++)for(int y=0;y<2;y++)for(int z=0;z<2;z++)
                    {var p=b.transform.InverseTransformPoint(new Vector3(x==0?bb.min.x:bb.max.x,y==0?bb.min.y:bb.max.y,z==0?bb.min.z:bb.max.z));if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}
                }
                e.localBounds=bounds;entries.Add(e);lookup.Add(b,e);
                b.GetComponent<CastleBlock>()?.RegisterCityDebris(this);b.GetComponent<CityStructuralMember>()?.RegisterCityDebris(this);
            }
        }
        public bool IsVisual(Rigidbody body)=>lookup.TryGetValue(body,out var e)&&e.visual;
        public Vector3 Velocity(Rigidbody body)=>lookup.TryGetValue(body,out var e)&&e.visual?e.velocity:body.linearVelocity;
        Bounds BoundsOf(Entry e)
        {
            var b=e.shapes[0].bounds;for(int i=1;i<e.shapes.Length;i++)b.Encapsulate(e.shapes[i].bounds);return b;
        }
        bool NearPeople(Bounds path)
        {
            if((Services.Session?.Multiplayer?.HumanCount??0)>1)return true;
            path.Expand(16);var camera=Services.Session?.ActiveCamera;if(camera&&path.Contains(camera.transform.position))return true;
            foreach(var mover in Tracked.Movers)if(mover is Component c&&c&&c.gameObject.activeInHierarchy)
            {
                var b=new Bounds(c.transform.position,Vector3.one*Mathf.Max(2,mover.Height+2*mover.Scale));b.Encapsulate(c.transform.position+Vector3.up*mover.VerticalSpeed*Mathf.Max(Time.deltaTime,.02f));
                if(path.Intersects(b))return true;
            }
            path.Expand(16);
            foreach(var hand in Tracked.Flyers)if(hand.Interactive)
            {if(!hand.Anchor)return true;var b=new Bounds(hand.Anchor.position,Vector3.one*2);b.Encapsulate(hand.Anchor.position+hand.Velocity*Mathf.Max(Time.deltaTime,.02f));if(path.Intersects(b))return true;}
            return false;
        }
        bool Clear(Entry e,Bounds bounds)
        {
            int n=Physics.OverlapBoxNonAlloc(bounds.center,bounds.extents*.98f,nearby,Quaternion.identity,1<<Layers.Block,QueryTriggerInteraction.Ignore);
            if(n==nearby.Length)return false;
            for(int i=0;i<n;i++)if(nearby[i].attachedRigidbody!=e.body)return false;
            return true;
        }
        public bool TryPush(Rigidbody body,Vector3 force)
        {
            if(!Authority||!lookup.TryGetValue(body,out var e))return false;
            if(e.sinking)return true;if(e.visual){e.velocity+=force;return true;}
            var v=body.linearVelocity+force;float threshold=force.magnitude/Mathf.Max(.001f,Time.fixedDeltaTime)>=180?18:20;
            return v.magnitude>=threshold&&Convert(e,v);
        }
        bool Convert(Entry e,Vector3 velocity)
        {
            if(Time.time-born<2||e.body.isKinematic||DisasterStates.Active.Count>0)return false;
            if((e.body.position-e.origin).sqrMagnitude<.5625f&&Quaternion.Angle(e.rotation,e.body.rotation)<30)return false;
            var bounds=BoundsOf(e);var path=bounds;path.Encapsulate(new Bounds(bounds.center+velocity*Mathf.Max(.02f,Time.deltaTime),bounds.size));
            if(NearPeople(path)||!Clear(e,bounds))return false;
            e.velocity=velocity;e.spin=e.body.angularVelocity;e.visual=true;e.body.collisionDetectionMode=CollisionDetectionMode.Discrete;e.body.isKinematic=true;e.body.interpolation=RigidbodyInterpolation.None;
            foreach(var c in e.shapes)c.isTrigger=true;ConvertedCount++;return true;
        }
        void Restore(Entry e,bool keepKinematic=false)
        {
            if(!e.visual||!e.body)return;e.visual=false;foreach(var c in e.shapes)if(c)c.isTrigger=false;
            e.body.isKinematic=keepKinematic;e.body.interpolation=keepKinematic?RigidbodyInterpolation.None:e.interpolation;
            e.body.collisionDetectionMode=keepKinematic?CollisionDetectionMode.Discrete:e.detection;
            if(!keepKinematic){e.body.linearVelocity=e.velocity;e.body.angularVelocity=e.spin;}RestoredCount++;
        }
        public void SetReplica(bool value){replica=value;foreach(var e in entries){Restore(e,value);if(e.sinking&&e.body){e.sinking=false;if(!value)e.body.isKinematic=false;}}}
        public void Impact(Rigidbody body,Collision collision)
        {
            if(!Authority||!body.gameObject.activeSelf||Time.time-born<2||collision.contactCount==0||!lookup.TryGetValue(body,out var e)||e.sinking)return;
            float speed=Mathf.Abs(Vector3.Dot(collision.relativeVelocity,collision.GetContact(0).normal));
            if(speed<18||collision.impulse.magnitude/Mathf.Max(1,body.mass)<6||NearPeople(BoundsOf(e)))return;
            GameEvents.RaiseBlockHit(collision.GetContact(0).point,speed);body.gameObject.SetActive(false);ShatteredCount++;
        }
        void FixedUpdate()
        {
            using var measurement=CleanupMarker.Auto();
            if(!Authority)return;
            foreach(var e in entries)if(e.sinking&&e.body&&e.body.gameObject.activeSelf)
            {
                if(NearPeople(BoundsOf(e))||!Clear(e,BoundsOf(e))){e.sinking=false;e.body.isKinematic=false;e.body.collisionDetectionMode=e.detection;continue;}
                e.body.MovePosition(e.body.position+Vector3.down*2*Time.fixedDeltaTime);
                if(BoundsOf(e).max.y<e.ground-.2f||Time.time-e.began>12){e.body.gameObject.SetActive(false);e.sinking=false;SunkCount++;}
            }
            if(Time.time-born<2||Time.time<nextScan||entries.Count==0)return;nextScan=Time.time+.1f;
            int budget=Mathf.Min(entries.Count,Mathf.Max(32,Mathf.CeilToInt(entries.Count/8f)));
            for(int i=0;i<budget;i++)
            {
                var e=entries[cursor++%entries.Count];var b=e.body;if(!b||!b.gameObject.activeSelf||b.isKinematic||e.visual||e.sinking)continue;
                if((b.position-e.origin).sqrMagnitude<.5625f&&Quaternion.Angle(e.rotation,b.rotation)<30)continue;
                if(b.linearVelocity.sqrMagnitude>=400&&Convert(e,b.linearVelocity))continue;
                var bounds=BoundsOf(e);if(b.linearVelocity.y>1||NearPeople(bounds)||!Clear(e,bounds))continue;
                if(!Physics.Raycast(bounds.center+Vector3.up*.5f,Vector3.down,out var hit,bounds.extents.y+1,1,QueryTriggerInteraction.Ignore)||hit.rigidbody||bounds.min.y>hit.point.y+.35f)continue;
                e.sinking=true;e.ground=hit.point.y;e.began=Time.time;b.collisionDetectionMode=CollisionDetectionMode.Discrete;b.isKinematic=true;
            }
            if(cursor>100000000)cursor=0;
        }
        void Update()
        {
            using var measurement=CleanupMarker.Auto();
            if(!Authority)return;float dt=Time.deltaTime;if(dt<=0)return;
            foreach(var e in entries)if(e.visual&&e.body&&e.body.gameObject.activeSelf)
            {
                var b=e.body;var bounds=BoundsOf(e);var movement=e.velocity*dt+Physics.gravity*(dt*dt*.5f);
                var half=bounds.extents+Vector3.one*(e.localBounds.extents.magnitude*e.spin.magnitude*dt+Physics.gravity.magnitude*dt*dt*.125f+.02f);
                var path=new Bounds(bounds.center,half*2);path.Encapsulate(new Bounds(bounds.center+movement,half*2));
                if(DisasterStates.Active.Count>0||NearPeople(path)){Restore(e);continue;}
                float distance=movement.magnitude;int count=distance>.0001f?Physics.BoxCastNonAlloc(bounds.center,half,movement/distance,casts,Quaternion.identity,distance,(1<<Layers.Block)|1,QueryTriggerInteraction.Ignore):0;
                int nearest=-1;float best=float.PositiveInfinity;
                for(int n=0;n<count;n++)if(casts[n].collider.attachedRigidbody!=b&&casts[n].distance<best){nearest=n;best=casts[n].distance;}
                if(count==casts.Length){Restore(e);continue;}
                if(nearest>=0)
                {
                    var hit=casts[nearest];float closing=Mathf.Max(0,Vector3.Dot(e.velocity,-hit.normal));if(closing<18){Restore(e);continue;}
                    if(Climbables.TryGet(hit.collider,out var target)&&target.Body)target.Body.GetComponent<IPushable>()?.Push(-hit.normal*Mathf.Min(20,closing*b.mass/(b.mass+target.Body.mass)),b.gameObject);
                    GameEvents.RaiseBlockHit(hit.point,closing);b.gameObject.SetActive(false);e.visual=false;ShatteredCount++;continue;
                }
                b.position+=movement;b.rotation=Quaternion.Euler(e.spin*Mathf.Rad2Deg*dt)*b.rotation;b.transform.SetPositionAndRotation(b.position,b.rotation);e.velocity+=Physics.gravity*dt;
                if(BoundsOf(e).max.y<(Services.World?.WaterHeight(b.position)??0)-.2f){b.gameObject.SetActive(false);e.visual=false;SunkCount++;}
            }
        }
        void OnDisable(){foreach(var e in entries){Restore(e,replica);if(e.sinking&&e.body&&!replica)e.body.isKinematic=false;e.sinking=false;}}
    }
}
