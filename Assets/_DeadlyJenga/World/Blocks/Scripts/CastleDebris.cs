using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Cleans up displaced debris, never the untouched bottom course of a tower.
    [DefaultExecutionOrder(-200)]
    public sealed partial class CastleDebris : MonoBehaviour
    {
        sealed class Block
        {
            public Rigidbody body;
            public Collider shape;
            public Vector3 origin;
            public Quaternion rotation;
            public CollisionDetectionMode detection;
            public float height, ground, began;
            public bool sinking;
            public bool visual;
            public Vector3 velocity, spin, halfSize, shapeCenter;
            public RigidbodyInterpolation interpolation;
            public bool trigger;
        }
        readonly List<Block> blocks=new List<Block>(), sinking=new List<Block>();
        readonly Dictionary<Rigidbody,Block> lookup=new Dictionary<Rigidbody,Block>();
        CastlePhysicsGroups groups;
        bool replica;
        int cursor;
        float nextScan, born;
        public bool SinkDebris = true, ShatterOnImpact = true;
        public float SinkSpeed = 2, ImpactSpeed = 18;
        public int SunkCount { get; private set; }
        public int ShatteredCount { get; private set; }
        public int SinkingCount => sinking.Count;
        public void Initialize(IReadOnlyList<Rigidbody> bodies, CastlePhysicsGroups physicsGroups)
        {
            groups=physicsGroups;born=Time.time;
            foreach(var body in bodies)
            {
                if(!body)continue;
                var shape=body.GetComponent<Collider>();if(!shape)continue;
                var box=shape as BoxCollider;
                var scale=body.transform.lossyScale;scale=new Vector3(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z));
                var block=new Block{body=body,shape=shape,origin=body.position,rotation=body.rotation,detection=body.collisionDetectionMode,height=shape.bounds.size.y,interpolation=body.interpolation,trigger=shape.isTrigger,halfSize=box?Vector3.Scale(box.size,scale)*.5f:shape.bounds.extents,shapeCenter=box?Vector3.Scale(box.center,scale):Vector3.zero};
                blocks.Add(block);lookup.Add(body,block);body.GetComponent<CastleBlock>()?.RegisterDebris(this);
            }
        }
        bool Authoritative => !replica && !(Services.Session is IWorldAuthority authority && !authority.HasWorldAuthority);
        public void SetReplica(bool value)
        {
            for(int i=visual.Count-1;i>=0;i--)Restore(visual[i],value);
            visual.Clear();
            replica=value;
            if(!value)foreach(var block in sinking)if(block.body && block.body.gameObject.activeSelf)
            {block.body.collisionDetectionMode=CollisionDetectionMode.Discrete;block.body.isKinematic=true;}
        }
        public void Impact(Rigidbody body, Collision collision)
        {
            if(!Authoritative || !ShatterOnImpact || Time.time-born<2 || collision.contactCount==0 || !lookup.TryGetValue(body,out var block) || block.sinking || !body.gameObject.activeSelf)return;
            var contact=collision.GetContact(0);
            float speed=Mathf.Abs(Vector3.Dot(collision.relativeVelocity,contact.normal));
            float impulse=collision.impulse.magnitude/Mathf.Max(1,body.mass);
            if(speed<Mathf.Max(8,ImpactSpeed) || impulse<6)return;
            groups?.Disturb(body);
            GameEvents.RaiseBlockHit(contact.point,speed);
            ShatteredCount++;
            body.gameObject.SetActive(false);
        }
        void FixedUpdate()
        {
            if(!Authoritative)return;
            if(DisasterStates.Active.Count>0)RestoreVisuals();
            for(int i=sinking.Count-1;i>=0;i--)
            {
                var block=sinking[i];
                if(!block.body || !block.body.gameObject.activeSelf){sinking.RemoveAt(i);continue;}
                block.body.MovePosition(block.body.position+Vector3.down*Mathf.Max(.1f,SinkSpeed)*Time.fixedDeltaTime);
                if(block.shape.bounds.max.y<block.ground-.2f || Time.time-block.began>12)
                {block.body.gameObject.SetActive(false);sinking.RemoveAt(i);SunkCount++;}
            }
            if((!SinkDebris && !VisualFastDebris) || Time.time-born<2 || Time.time<nextScan || blocks.Count==0)return;
            nextScan=Time.time+.1f;
            int budget=Mathf.Min(blocks.Count,Mathf.Max(32,Mathf.CeilToInt(blocks.Count/8f)));
            for(int n=0;n<budget;n++)
            {
                var block=blocks[cursor++%blocks.Count];var body=block.body;
                if(!body || !body.gameObject.activeInHierarchy || block.sinking || body.isKinematic)continue;
                var delta=body.position-block.origin;
                float displaced=Mathf.Max(.75f,block.height*.5f);
                if(delta.x*delta.x+delta.z*delta.z<displaced*displaced && delta.y>-block.height*.75f && Quaternion.Angle(body.rotation,block.rotation)<30)continue;
                if(TryConvert(block,body.linearVelocity))continue;
                if(!SinkDebris)continue;
                if(body.linearVelocity.y>1)continue;
                var bounds=block.shape.bounds;
                if(!Physics.Raycast(bounds.center+Vector3.up*.5f,Vector3.down,out var hit,bounds.extents.y+1,1<<0,QueryTriggerInteraction.Ignore) || hit.rigidbody || bounds.min.y>hit.point.y+.35f)continue;
                groups?.Disturb(body);
                block.ground=hit.point.y;block.began=Time.time;block.sinking=true;
                body.collisionDetectionMode=CollisionDetectionMode.Discrete;body.isKinematic=true;
                sinking.Add(block);
            }
            if(cursor>100000000)cursor=0;
        }
        void OnDisable()
        {
            RestoreVisuals();
            if(replica)return;
            foreach(var block in sinking)if(block.body)
            {block.body.isKinematic=false;block.body.collisionDetectionMode=block.detection;}
            sinking.Clear();
        }
    }
}
