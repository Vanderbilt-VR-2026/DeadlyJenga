using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadlyJenga.CastlePrototype
{
    // Naturally sleeping chunks keep their full mass. Any lost support restores independent blocks.
    [DefaultExecutionOrder(-300)]
    public sealed class CastlePhysicsGroups : MonoBehaviour
    {
        sealed class Member
        {
            public Rigidbody body;
            public bool covered;
            public BoxCollider shape;
            public CastleBlock block;
            public Vector3 origin, rest, localPosition;
            public Quaternion rotation, restRotation, localRotation;
            public CollisionDetectionMode detection;
            public RigidbodyInterpolation interpolation;
        }
        sealed class Group
        {
            public readonly List<Member> members=new List<Member>();
            public readonly List<Group> above=new List<Group>(),beside=new List<Group>();
            public readonly List<Collider> shapes=new List<Collider>();
            public Bounds bounds;
            public Rigidbody aggregate;
            public GameObject surfaceRoot;
            public Vector3 center;
            public Quaternion rotation;
            public bool dormant,released,supportReleased;
        }
        readonly List<Group> groups=new List<Group>();
        readonly Dictionary<Rigidbody,Group> owners=new Dictionary<Rigidbody,Group>();
        readonly Dictionary<Collider,Member> contacts=new Dictionary<Collider,Member>();
        readonly Queue<Group> pending=new Queue<Group>();
        bool replica,shuttingDown;
        PhysicsScene physics;
        readonly Collider[] overhead=new Collider[32];
        GameObject forceSource;
        bool waveSource;
        float nextScan;
        public int DormantBodies { get; private set; }
        public int ReleasedBodies { get; private set; }
        public int GroupCount => groups.Count;
        public void Initialize(IReadOnlyList<Rigidbody> bodies)
        {
            shuttingDown=false;physics=gameObject.scene.GetPhysicsScene();
            var cells=new Dictionary<Vector3Int,Group>();
            Vector3 size=new Vector3(12,4,12);
            foreach(var body in bodies)if(body && body.TryGetComponent<BoxCollider>(out var shape))size=Vector3.Max(size,shape.bounds.size*2);
            foreach(var body in bodies)
            {
                if(!body || !body.TryGetComponent<BoxCollider>(out var shape) || shape.isTrigger)continue;
                var center=shape.bounds.center;
                var key=new Vector3Int(Mathf.FloorToInt(center.x/size.x),Mathf.FloorToInt(center.y/size.y),Mathf.FloorToInt(center.z/size.z));
                if(!cells.TryGetValue(key,out var group)){group=new Group{bounds=shape.bounds};cells.Add(key,group);groups.Add(group);}
                group.bounds.Encapsulate(shape.bounds);
                var block=body.GetComponent<CastleBlock>();
                group.members.Add(new Member{body=body,shape=shape,block=block,origin=body.position,rotation=body.rotation,detection=body.collisionDetectionMode,interpolation=body.interpolation});
                owners.Add(body,group);if(block)block.RegisterPhysicsGroup(this);
            }
            foreach(var cell in cells)
            for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)
            {
                if((x!=0 || z!=0) && cells.TryGetValue(cell.Key+new Vector3Int(x,0,z),out var beside))cell.Value.beside.Add(beside);
                if(!cells.TryGetValue(cell.Key+new Vector3Int(x,1,z),out var above))continue;
                var lower=cell.Value.bounds;var upper=above.bounds;
                if(upper.min.x<=lower.max.x+.5f && upper.max.x>=lower.min.x-.5f && upper.min.z<=lower.max.z+.5f && upper.max.z>=lower.min.z-.5f)cell.Value.above.Add(above);
            }
            nextScan=Time.time+.5f;
        }
        public bool IsDormant(Rigidbody body)=>body && owners.TryGetValue(body,out var group) && group.dormant;
        public Vector3 Velocity(Rigidbody body)
        {
            if(body && owners.TryGetValue(body,out var group) && group.dormant && group.aggregate)
            {
                foreach(var member in group.members)if(member.body==body)
                    return group.aggregate.GetPointVelocity(group.aggregate.position+group.aggregate.rotation*member.localPosition);
            }
            return body?body.linearVelocity:Vector3.zero;
        }
        public bool TryPush(Rigidbody body,Vector3 velocity,GameObject source)
        {
            if(replica || shuttingDown || !body || !source || !owners.TryGetValue(body,out var group) || !group.dormant || !group.aggregate || velocity.sqrMagnitude>.25f)return false;
            if(forceSource!=source){forceSource=source;waveSource=source && source.GetComponent<IWaveState>()!=null;}
            if(!waveSource)return false;
            foreach(var member in group.members)if(member.body==body)
            {
                if(!member.covered)return false;
                var point=group.aggregate.position+group.aggregate.rotation*member.localPosition;
                group.aggregate.AddForceAtPosition(velocity*body.mass,point,ForceMode.Impulse);
                return true;
            }
            return false;
        }
        bool Covered(Member member)
        {
            var bounds=member.shape.bounds;
            for(int course=0;course<2;course++)
            {
                var center=new Vector3(bounds.center.x,bounds.max.y+.08f+course*bounds.size.y,bounds.center.z);
                int count=physics.OverlapBox(center,new Vector3(bounds.extents.x*.9f,.06f,bounds.extents.z*.9f),overhead,Quaternion.identity,1<<Layers.Block,QueryTriggerInteraction.Ignore);
                if(count==overhead.Length)return false;
                bool found=false;
                for(int i=0;i<count;i++)if(overhead[i] && overhead[i]!=member.shape && overhead[i].bounds.center.y>bounds.center.y+bounds.size.y*.5f){found=true;break;}
                if(!found)return false;
            }
            return true;
        }
        public void Disturb(Rigidbody body)=>Disturb(body,false);
        internal void Disturb(Rigidbody body,bool supportRemoved)
        {
            if(replica || shuttingDown || !body || !owners.TryGetValue(body,out var group))return;
            Release(group,supportRemoved);
            foreach(var beside in group.beside)Release(beside,supportRemoved);
            Drain();
        }
        void Release(Group group,bool above)
        {
            if(above && !group.supportReleased){group.supportReleased=true;pending.Enqueue(group);}
            if(group.released)return;
            group.released=true;ReleasedBodies+=group.members.Count;
            if(!group.dormant)
            {foreach(var member in group.members)if(member.body){member.rest=member.body.position;member.restRotation=member.body.rotation;}return;}
            group.dormant=false;
            foreach(var member in group.members)
            {
                if(!member.body)continue;
                var aggregate=group.aggregate;
                var position=aggregate?aggregate.position+aggregate.rotation*member.localPosition:member.body.position;
                var rotation=aggregate?aggregate.rotation*member.localRotation:member.body.rotation;
                var velocity=aggregate?aggregate.GetPointVelocity(position):Vector3.zero;var spin=aggregate?aggregate.angularVelocity:Vector3.zero;
                member.body.position=position;member.body.rotation=rotation;member.body.transform.SetPositionAndRotation(position,rotation);
                member.body.isKinematic=false;member.body.detectCollisions=true;member.body.collisionDetectionMode=member.detection;member.body.interpolation=member.interpolation;
                member.body.linearVelocity=velocity;member.body.angularVelocity=spin;member.body.WakeUp();
                member.rest=position;member.restRotation=rotation;
                if(member.block){member.block.SetGroupedSurface(false);member.block.SyncSurface();}
            }
            RemoveShapes(group);DormantBodies-=group.members.Count;
        }
        void Drain(){while(pending.Count>0)foreach(var above in pending.Dequeue().above)Release(above,true);}
        void RemoveShapes(Group group)
        {
            foreach(var shape in group.shapes){Climbables.Unregister(shape);contacts.Remove(shape);}
            group.shapes.Clear();
            if(group.aggregate){group.aggregate.gameObject.SetActive(false);Destroy(group.aggregate.gameObject);group.aggregate=null;}
            if(group.surfaceRoot){group.surfaceRoot.SetActive(false);Destroy(group.surfaceRoot);group.surfaceRoot=null;}
        }
        public void ReleaseAll()
        {if(replica || shuttingDown)return;foreach(var group in groups)Release(group,true);Drain();}
        public void SetReplica(bool value)
        {if(value){ReleaseAll();replica=true;}else{replica=false;ReleaseAll();}}
        bool RequiresNativePhysics()
        {
            if(DisasterStates.Active.Count>0)return true;
            foreach(var hand in Tracked.Flyers)if(hand.Interactive)return true;
            return false;
        }
        void Update(){if(replica || shuttingDown)return;if(RequiresNativePhysics())ReleaseAll();else CheckSupports();}
        void FixedUpdate()=>Evaluate(Time.time);
        public void Evaluate(float simulationTime)
        {
            if(replica || shuttingDown || ReleasedBodies>=owners.Count)return;
            if(RequiresNativePhysics()){ReleaseAll();return;}
            CheckSupports();
            foreach(var source in Tracked.Waves)WakeWave(source.Wave);
            if(simulationTime<nextScan)return;
            nextScan=simulationTime+.1f;
            int suspended=0;
            foreach(var group in groups)
            {
                if(group.released || group.dormant)continue;
                bool asleep=true,intact=true;
                foreach(var member in group.members)
                {
                    if(!member.body || !member.body.gameObject.activeInHierarchy || (member.body.position-member.origin).sqrMagnitude>.1225f || Quaternion.Angle(member.body.rotation,member.rotation)>4){intact=false;break;}
                    if(!member.body.IsSleeping())asleep=false;
                }
                if(!intact){Release(group,true);continue;}
                if(!asleep || suspended+group.members.Count>128)continue;
                Suspend(group);suspended+=group.members.Count;
            }
            Drain();
        }
        void CheckSupports()
        {
            foreach(var group in groups)
            {
                if(group.dormant)
                {
                    if(!group.aggregate || (group.aggregate.position-group.center).sqrMagnitude>.0004f || Quaternion.Angle(group.aggregate.rotation,group.rotation)>.25f){Release(group,true);continue;}
                    foreach(var member in group.members)if(!member.body || !member.body.gameObject.activeInHierarchy){Release(group,true);break;}
                }
                else if(group.released && !group.supportReleased)
                {
                    foreach(var member in group.members)
                    {
                        if(!member.body || !member.body.gameObject.activeInHierarchy || (member.body.position-member.rest).sqrMagnitude>.0004f || Quaternion.Angle(member.body.rotation,member.restRotation)>.25f){Release(group,true);break;}
                    }
                }
            }
            Drain();
        }
        public void WakeWave(WaveFrame wave)
        {
            if(replica || !wave.active)return;
            var forward=wave.direction.sqrMagnitude>.01f?wave.direction.normalized:Vector3.forward;var side=new Vector3(forward.z,0,-forward.x);
            float travel=Mathf.Abs(wave.speed)*Time.fixedDeltaTime+2;
            var extent=new Vector3(Mathf.Abs(side.x)*wave.width*.5f+Mathf.Abs(forward.x)*(wave.depth*.5f+travel),0,Mathf.Abs(side.z)*wave.width*.5f+Mathf.Abs(forward.z)*(wave.depth*.5f+travel));
            float height=Mathf.Max(0,wave.height)+Mathf.Abs(wave.heightRate)*Time.fixedDeltaTime+4;
            var area=new Bounds(new Vector3(wave.center.x,wave.waterHeight+height*.5f-2,wave.center.z),new Vector3(extent.x*2,height,extent.z*2));
            foreach(var group in groups)if(!group.released && area.Intersects(group.bounds))
            {
                if(!group.dormant){Release(group,false);continue;}
                foreach(var member in group.members)if(!member.covered && area.Intersects(member.shape.bounds)){Release(group,false);break;}
            }
            Drain();
        }
        GameObject Root(string name,Vector3 position,Quaternion rotation)
        {var root=new GameObject(name);SceneManager.MoveGameObjectToScene(root,gameObject.scene);root.transform.SetPositionAndRotation(position,rotation);return root;}
        void Suspend(Group group)
        {
            if(group.members.Count<2){Release(group,false);return;}
            foreach(var member in group.members)member.covered=Covered(member);
            float mass=0;Vector3 center=Vector3.zero;
            foreach(var member in group.members){mass+=member.body.mass;center+=member.body.worldCenterOfMass*member.body.mass;member.rest=member.body.position;member.restRotation=member.body.rotation;}
            center/=mass;group.center=center;group.rotation=group.members[0].body.rotation;
            var root=Root("Resting block mass group",center,group.rotation);root.layer=Layers.Block;
            group.aggregate=root.AddComponent<Rigidbody>();var aggregate=group.aggregate;aggregate.mass=mass;aggregate.centerOfMass=Vector3.zero;
            aggregate.linearDamping=group.members[0].body.linearDamping;aggregate.angularDamping=group.members[0].body.angularDamping;
            int iterations=1,velocityIterations=1;
            foreach(var member in group.members){iterations=Mathf.Max(iterations,member.body.solverIterations);velocityIterations=Mathf.Max(velocityIterations,member.body.solverVelocityIterations);}
            aggregate.solverIterations=iterations;aggregate.solverVelocityIterations=velocityIterations;aggregate.sleepThreshold=.005f;
            var observer=root.AddComponent<CastleRestingContacts>();observer.owner=this;
            group.surfaceRoot=Root("Grouped survivor surfaces",center,group.rotation);group.surfaceRoot.layer=Layers.Surface;
            var surfaceBody=group.surfaceRoot.AddComponent<Rigidbody>();surfaceBody.isKinematic=true;surfaceBody.useGravity=false;
            var solidFrames=new Dictionary<Vector3Int,Transform>();var surfaceFrames=new Dictionary<Vector3Int,Transform>();
            foreach(var member in group.members)
            {
                member.localPosition=Quaternion.Inverse(group.rotation)*(member.body.position-center);member.localRotation=Quaternion.Inverse(group.rotation)*member.body.rotation;
                AddShape(group,member,root.transform,solidFrames,false);
                if(member.block){AddShape(group,member,group.surfaceRoot.transform,surfaceFrames,true);member.block.SetGroupedSurface(true);}
                member.body.collisionDetectionMode=CollisionDetectionMode.Discrete;member.body.isKinematic=true;member.body.detectCollisions=false;member.body.interpolation=RigidbodyInterpolation.None;
            }
            group.dormant=true;DormantBodies+=group.members.Count;aggregate.Sleep();
        }
        void AddShape(Group group,Member member,Transform root,Dictionary<Vector3Int,Transform> frames,bool surface)
        {
            var angles=member.body.rotation.eulerAngles;
            var key=new Vector3Int(Mathf.RoundToInt(angles.x*100)%36000,Mathf.RoundToInt(angles.y*100)%36000,Mathf.RoundToInt(angles.z*100)%36000);
            if(!frames.TryGetValue(key,out var frame))
            {
                if(frames.Count==0)frame=root;
                else{frame=new GameObject("Collider orientation").transform;frame.SetParent(root,false);frame.gameObject.layer=root.gameObject.layer;frame.rotation=member.body.rotation;}
                frames.Add(key,frame);
            }
            var scale=member.body.transform.lossyScale;scale=new Vector3(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z));
            var shape=frame.gameObject.AddComponent<BoxCollider>();shape.size=Vector3.Scale(member.shape.size,scale);
            var center=member.body.position+member.body.rotation*Vector3.Scale(member.shape.center,scale);
            shape.center=frame.InverseTransformPoint(center);shape.sharedMaterial=member.shape.sharedMaterial;
            shape.excludeLayers=surface?~(1<<Layers.Survivor):member.shape.excludeLayers;shape.layerOverridePriority=surface?10:member.shape.layerOverridePriority;
            if(member.block)Climbables.Register(shape,member.block);
            group.shapes.Add(shape);if(!surface)contacts.Add(shape,member);
        }
        internal void Contact(Collision collision)
        {
            if(collision.contactCount==0 || collision.relativeVelocity.sqrMagnitude<.25f)return;
            var contact=collision.GetContact(0);
            if(!contacts.TryGetValue(contact.thisCollider,out var member))return;
            Disturb(member.body,true);
            if(member.block)member.block.RestingImpact(collision);
        }
        void OnDisable(){ReleaseAll();shuttingDown=true;}
    }
    public sealed class CastleRestingContacts : MonoBehaviour
    {
        internal CastlePhysicsGroups owner;
        void OnCollisionEnter(Collision collision){if(owner)owner.Contact(collision);}
    }
}
