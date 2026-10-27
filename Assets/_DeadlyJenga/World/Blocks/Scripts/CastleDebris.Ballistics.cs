using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CastleDebris
    {
        public bool VisualFastDebris=true;
        public float VisualSpeed=20, AccelerationThreshold=180;
        public int VisualCount => visual.Count;
        public int ConvertedCount { get; private set; }
        public int RestoredCount { get; private set; }
        readonly List<Block> visual=new List<Block>();
        readonly Collider[] overlaps=new Collider[32];
        readonly RaycastHit[] sweep=new RaycastHit[32];
        const int SolidMask=(1<<Layers.Block)|(1<<0);
        public bool IsVisual(Rigidbody body) => body && lookup.TryGetValue(body,out var block) && block.visual;
        public Vector3 Velocity(Rigidbody body) => lookup.TryGetValue(body,out var block) && block.visual ? block.velocity : body.linearVelocity;
        public bool TryPush(Rigidbody body,Vector3 force)
        {
            if(!Authoritative || !lookup.TryGetValue(body,out var block))return false;
            if(block.sinking)return true;
            if(block.visual){block.velocity+=force;return true;}
            float threshold=VisualSpeed;
            var predicted=body.linearVelocity+force;
            if(force.sqrMagnitude>=AccelerationThreshold*AccelerationThreshold*Time.fixedDeltaTime*Time.fixedDeltaTime)
                threshold=Mathf.Max(ImpactSpeed,VisualSpeed*.9f);
            return predicted.sqrMagnitude>=threshold*threshold && TryConvert(block,predicted,threshold);
        }
        static Vector3 Extents(Quaternion rotation,Vector3 half)
        {
            var x=rotation*Vector3.right*half.x;var y=rotation*Vector3.up*half.y;var z=rotation*Vector3.forward*half.z;
            return new Vector3(Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x),Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y),Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z));
        }
        bool NearPeople(Bounds path)
        {
            int humans=Services.Session?.Multiplayer?.HumanCount??0;
            var handPath=path;handPath.Expand(32);
            path.Expand(humans>1?32:16);
            var camera=Services.Session?.ActiveCamera;
            if(camera && path.Contains(camera.transform.position))return true;
            foreach(var mover in Tracked.Movers)
                if(mover is Component actor && actor && actor.gameObject.activeInHierarchy)
                {
                    var actorPath=new Bounds(actor.transform.position,Vector3.one*Mathf.Max(2,mover.Height+2*mover.Scale));
                    actorPath.Encapsulate(actor.transform.position+Vector3.up*(mover.VerticalSpeed*Time.deltaTime));
                    if(path.Intersects(actorPath))return true;
                }
            var match=MatchRuntime.Current;
            if(humans>1 && (match?.Frame?.players==null || match.Frame.players.Length<humans))return true;
            if(match?.Frame?.players!=null)
                foreach(var player in match.Frame.players)if(player.alive && path.Contains(player.position))return true;
            foreach(var hand in Tracked.Flyers)
                if(hand.Interactive)
                {
                    if(!hand.Anchor)return true;
                    var flight=new Bounds(hand.Anchor.position,Vector3.one*2);
                    flight.Encapsulate(hand.Anchor.position+hand.Velocity*Time.deltaTime);
                    if(handPath.Intersects(flight))return true;
                }
            return false;
        }
        bool TryConvert(Block block,Vector3 velocity,float threshold=-1)
        {
            if(!VisualFastDebris || !ShatterOnImpact || DisasterStates.Active.Count>0 || block.sinking || block.visual || !block.body || block.body.isKinematic || Time.time-born<2)return false;
            threshold=threshold<0?VisualSpeed:threshold;
            if(velocity.sqrMagnitude<threshold*threshold)return false;
            var delta=block.body.position-block.origin;
            if(delta.sqrMagnitude<.75f*.75f && Quaternion.Angle(block.rotation,block.body.rotation)<30)return false;
            var center=block.body.position+block.body.rotation*block.shapeCenter;
            var bounds=new Bounds(center,Extents(block.body.rotation,block.halfSize)*2);
            var end=bounds;end.center+=velocity*Mathf.Max(.02f,Time.deltaTime);bounds.Encapsulate(end);
            if(NearPeople(bounds))return false;
            int count=Physics.OverlapBoxNonAlloc(center,block.halfSize*.95f,overlaps,block.body.rotation,SolidMask,QueryTriggerInteraction.Ignore);
            bool clear=count<overlaps.Length;
            for(int i=0;i<count;i++){var hit=overlaps[i];overlaps[i]=null;if(hit && hit.attachedRigidbody!=block.body)clear=false;}
            if(!clear)return false;
            groups?.Disturb(block.body);
            block.velocity=velocity;block.spin=block.body.angularVelocity;block.visual=true;
            // A trigger keeps cheap hazard queries working without solid contacts or CCD.
            block.body.collisionDetectionMode=CollisionDetectionMode.Discrete;block.body.isKinematic=true;block.shape.isTrigger=true;block.body.interpolation=RigidbodyInterpolation.None;
            block.body.GetComponent<CastleBlock>()?.SetVisualDebris(true);
            visual.Add(block);ConvertedCount++;return true;
        }
        void Restore(Block block,bool keepKinematic=false)
        {
            if(!block.body)return;
            block.visual=false;block.shape.isTrigger=block.trigger;block.body.isKinematic=keepKinematic;block.body.detectCollisions=true;
            block.body.collisionDetectionMode=keepKinematic?CollisionDetectionMode.Discrete:block.detection;
            block.body.interpolation=keepKinematic?RigidbodyInterpolation.None:block.interpolation;
            block.body.GetComponent<CastleBlock>()?.SetVisualDebris(false);
            if(!keepKinematic){block.body.linearVelocity=block.velocity;block.body.angularVelocity=block.spin;}
            RestoredCount++;
        }
        void RestoreVisuals()
        {foreach(var block in visual)Restore(block,replica);visual.Clear();}
        void Update()
        {
            if(!Authoritative)return;
            if(!VisualFastDebris || !ShatterOnImpact || DisasterStates.Active.Count>0){RestoreVisuals();return;}
            float dt=Time.deltaTime;if(dt<=0)return;
            for(int i=visual.Count-1;i>=0;i--)
            {
                var block=visual[i];var body=block.body;
                if(!body || !body.gameObject.activeInHierarchy){visual.RemoveAt(i);continue;}
                var center=body.position+body.rotation*block.shapeCenter;
                var movement=block.velocity*dt+Physics.gravity*(dt*dt*.5f);
                var rotation=Quaternion.Euler(block.spin*Mathf.Rad2Deg*dt)*body.rotation;
                var half=Vector3.Max(Extents(body.rotation,block.halfSize),Extents(rotation,block.halfSize));
                float angle=block.spin.magnitude*dt,radius=block.halfSize.magnitude+block.shapeCenter.magnitude;
                if(angle>.25f){center=body.position;half=Vector3.one*radius;}
                else half+=Vector3.one*(radius*angle);
                half+=Vector3.one*(Physics.gravity.magnitude*dt*dt*.125f+.02f);
                var path=new Bounds(center,half*2);var end=path;end.center+=movement;path.Encapsulate(end);
                if(NearPeople(path)){Restore(block);visual.RemoveAt(i);continue;}
                float distance=movement.magnitude;
                int count=distance>.0001f?Physics.BoxCastNonAlloc(center,half,movement/distance,sweep,Quaternion.identity,distance,SolidMask,QueryTriggerInteraction.Ignore):0;
                int nearest=-1;float best=float.PositiveInfinity;
                for(int n=0;n<count;n++)if(sweep[n].distance<best){nearest=n;best=sweep[n].distance;}
                if(count==sweep.Length){Restore(block);visual.RemoveAt(i);continue;}
                if(nearest>=0)
                {
                    if(nearest<0){Restore(block);visual.RemoveAt(i);continue;}
                    var hit=sweep[nearest];float closing=Mathf.Max(0,Vector3.Dot(block.velocity,-hit.normal));
                    if(closing<ImpactSpeed){Restore(block);visual.RemoveAt(i);continue;}
                    var target=HitAbilities.Get<IPushable>(hit.collider);
                    var targetBody=Climbables.TryGet(hit.collider,out var climbable)?climbable.Body:hit.rigidbody;
                    float share=body.mass/(body.mass+(targetBody?targetBody.mass:body.mass));
                    target?.Push(-hit.normal*Mathf.Min(20,closing*share),body.gameObject);
                    GameEvents.RaiseBlockHit(hit.point,closing);ShatteredCount++;
                    body.gameObject.SetActive(false);visual.RemoveAt(i);continue;
                }
                body.position+=movement;body.rotation=rotation;body.transform.SetPositionAndRotation(body.position,rotation);
                block.velocity+=Physics.gravity*dt;
                if(body.position.y+half.y<(Services.World?.WaterHeight(body.position)??0)-.2f)
                {body.gameObject.SetActive(false);visual.RemoveAt(i);SunkCount++;}
            }
        }
    }
}
