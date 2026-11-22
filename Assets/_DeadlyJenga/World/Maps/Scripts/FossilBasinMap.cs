using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class FossilBasinMap : MonoBehaviour, IWorldQuery, ISyncedWorld, IRebuildable, IMapFootprint, ITraversalCollisionWorld
    {
        public Material stone;
        const string Version="fossil-basin/7/";
        readonly List<Rigidbody> bodies=new();
        readonly List<FossilMember> decks=new();
        readonly List<Link> links=new();
        readonly List<(Collider a,Collider b)> socketsInContact=new();
        int contactCursor;
        Transform structure;
        bool replica;
        int cleanup, spawnIndex;
        float nextSweep;
        sealed class Link { public Rigidbody a,b; public Vector3 anchor,connectedAnchor; public float force,torque; public FixedJoint joint; public bool broken; }
        public int Revision { get; private set; }
        public IReadOnlyList<Rigidbody> Blocks => bodies;
        public Transform Root => transform;
        public bool UsesOriginalColliders => true;
        public Vector3 Center => transform.position;
        public float Radius => 24*transform.lossyScale.x;
        public float StepHeight => .23f;
        public Bounds PlayableBounds
        {
            get
            {
                var result=new Bounds(Center,Vector3.zero);
                foreach(int x in new[]{-1,1}) foreach(int z in new[]{-1,1}) foreach(int y in new[]{-1,18})
                    result.Encapsulate(transform.TransformPoint(new Vector3(21*x,y,15*z)));
                return result;
            }
        }
        public float TopHeight => PlayableBounds.max.y;
        public float WaterHeight(Vector3 p) => transform.position.y;
        public int FootprintVersion => 1;
        public float FootprintHalfSize => 22;
        public void GetGroundFootprints(List<Footprint> into) { } // Open water all the way between the piers.
        public bool IsSheltered(Vector3 p) => gameObject.scene.GetPhysicsScene().Raycast(p+Vector3.up*.1f,Vector3.up,out _,40,1<<Layers.Block);
        void OnEnable() => Services.World=this;
        void OnDisable() { if(ReferenceEquals(Services.World,this)) Services.World=null; }
        void Start() { if(!structure) Rebuild(); }
        internal bool Authority => !replica && !(Services.Session is IWorldAuthority a && !a.HasWorldAuthority);
        public void Rebuild()
        {
            if(structure){structure.gameObject.SetActive(false);Destroy(structure.gameObject);}
            bodies.Clear(); links.Clear(); decks.Clear(); socketsInContact.Clear();contactCursor=0; cleanup=spawnIndex=0;
            structure=new GameObject("Excavation").transform; structure.SetParent(transform,false);
            BuildExcavation();
            var tar=new GameObject("Lethal tar contact"); tar.transform.SetParent(structure,false);
            var trigger=tar.AddComponent<BoxCollider>(); trigger.isTrigger=true;
            trigger.center=new Vector3(0,-6,0);trigger.size=new Vector3(200,12,200);
            var sensor=tar.AddComponent<Rigidbody>();sensor.isKinematic=true;sensor.useGravity=false;
            tar.AddComponent<FossilTar>().map=this;
            Physics.SyncTransforms();
            foreach(var l in links) Connect(l);
            FilterInterlockingBones();
            Revision++;
        }
        FossilMember Piece(string label, Vector3 center,float mass,Action<FossilCarving> carve)
        {
            var go=new GameObject(label){layer=Layers.Block};go.transform.SetParent(structure,false);go.transform.localPosition=center;
            var rb=go.AddComponent<Rigidbody>();rb.isKinematic=replica;rb.mass=mass;rb.solverIterations=24;rb.solverVelocityIterations=8;
            rb.maxDepenetrationVelocity=.8f; rb.sleepThreshold=.018f;
            var m=go.AddComponent<FossilMember>();m.rigid=rb;
            var c=new FossilCarving(m,center);carve(c);c.Finish(stone);bodies.Add(rb);return m;
        }
        void Socket(FossilMember a,FossilMember b,Vector3 at,float strength=18000)
        {
            links.Add(new Link{a=a.Body,b=b.Body,anchor=at-a.transform.localPosition,connectedAnchor=at-b.transform.localPosition,force=strength,torque=strength*.65f});
        }
        void Connect(Link l)
        {
            if(replica||l.broken)return;
            var j=l.a.gameObject.AddComponent<FixedJoint>();
            j.autoConfigureConnectedAnchor=false;
            j.connectedBody=l.b;j.anchor=l.anchor;
            j.connectedAnchor=l.connectedAnchor;
            j.enablePreprocessing=false;
            j.breakForce=l.force;j.breakTorque=l.torque;j.enableCollision=false;l.joint=j;
        }
        void FilterInterlockingBones()
        {
            // At a fossil joint the two carved ends interlock. PhysX should not push
            // those initially interpenetrating collision approximations apart at rest.
            var neighbors=new Collider[128];
            foreach(var body in bodies)
            {
                var member=body.GetComponent<FossilMember>();
                if(member.fossil==0)continue;
                foreach(var shape in member.shapes)
                {
                    var bounds=shape.bounds;
                    int n=Physics.OverlapBoxNonAlloc(bounds.center,bounds.extents,neighbors,Quaternion.identity,1<<Layers.Block,QueryTriggerInteraction.Ignore);
                    for(int i=0;i<n;i++)
                    {
                        var other=neighbors[i];var rb=other.attachedRigidbody;
                        if(!rb || rb==body || other.GetInstanceID()<=shape.GetInstanceID() ||
                            rb.transform.parent!=structure || rb.GetComponent<FossilMember>().fossil!=member.fossil)continue;
                        if(!Physics.ComputePenetration(shape,shape.transform.position,shape.transform.rotation,other,
                            other.transform.position,other.transform.rotation,out _,out float depth)||depth<.008f)continue;
                        Physics.IgnoreCollision(shape,other,true);socketsInContact.Add((shape,other));
                    }
                }
            }
        }
        void RestoreSeparatedContacts()
        {
            int count=Mathf.Min(32,socketsInContact.Count);
            for(int i=0;i<count && socketsInContact.Count>0;i++)
            {
                contactCursor%=socketsInContact.Count;
                var (a,b)=socketsInContact[contactCursor];
                var seat=a.bounds;seat.Expand(.08f);
                if(seat.Intersects(b.bounds)){contactCursor++;continue;}
                Physics.IgnoreCollision(a,b,false);
                socketsInContact.RemoveAt(contactCursor);
            }
        }
        void TrackBreaks()
        {
            if(replica)return;
            foreach(var l in links) if(!l.broken&&!l.joint){l.broken=true;Revision++;}
        }
        void FixedUpdate()
        {
            if(!Authority || Time.time<nextSweep || bodies.Count==0)return;
            nextSweep=Time.time+.16f; TrackBreaks();RestoreSeparatedContacts();
            for(int i=0;i<24;i++)
            {
                var b=bodies[cleanup];cleanup=(cleanup+1)%bodies.Count;
                if(b.gameObject.activeSelf && b.worldCenterOfMass.y<WaterHeight(b.position)-8)
                    b.gameObject.SetActive(false);
            }
        }
        public Vector3 SafeSpawnPoint()
        {
            for(int i=0;i<decks.Count;i++)
            {
                var deck=decks[spawnIndex];
                spawnIndex=(spawnIndex+1)%decks.Count;
                if(!deck.gameObject.activeInHierarchy||deck.transform.up.y<.93f||deck.Body.linearVelocity.sqrMagnitude>1)continue;
                var pos=deck.transform.position+Vector3.up*(.17f*transform.lossyScale.x+.05f);
                if(ClearSpawn(pos))return pos;
            }
            // After the slate decks are lost, a settled flight or long fossil can still be refuge.
            foreach(var body in bodies)
            {
                if(!body.gameObject.activeSelf||body.linearVelocity.sqrMagnitude>1||body.angularVelocity.sqrMagnitude>1)continue;
                foreach(var shape in body.GetComponent<FossilMember>().shapes)
                {
                    var bounds=shape.bounds;
                    if(Mathf.Min(bounds.size.x,bounds.size.z)<.64f||bounds.max.y<WaterHeight(bounds.center)+.4f)continue;
                    var ray=new Ray(new Vector3(bounds.center.x,bounds.max.y+.1f,bounds.center.z),Vector3.down);
                    if(shape.Raycast(ray,out var hit,bounds.size.y+.2f)&&hit.normal.y>.90f&&ClearSpawn(hit.point+Vector3.up*.05f))
                        return hit.point+Vector3.up*.05f;
                }
            }
            return transform.TransformPoint(new Vector3(0,1.25f,-8));
        }
        bool ClearSpawn(Vector3 pos) => pos.y>WaterHeight(pos)+.4f &&
            !Physics.CheckCapsule(pos+Vector3.up*.25f,pos+Vector3.up*1.05f,.23f,1<<Layers.Block,QueryTriggerInteraction.Ignore);
        public string SaveLayout()
        {
            TrackBreaks();var bytes=new byte[(links.Count+7)/8];
            for(int i=0;i<links.Count;i++)if(links[i].broken)bytes[i/8]|=(byte)(1<<(i%8));
            return Version+Convert.ToBase64String(bytes);
        }
        public bool LoadLayout(string data,int expectedBlocks,Pose origin,Vector3 scale)
        {
            if(data==null||!data.StartsWith(Version,StringComparison.Ordinal)||!float.IsFinite(origin.position.sqrMagnitude)||
               !float.IsFinite(scale.sqrMagnitude)||scale.x<=0||Mathf.Abs(scale.x-scale.y)>.0001f||Mathf.Abs(scale.x-scale.z)>.0001f||
               !(Mathf.Abs(Quaternion.Dot(origin.rotation,origin.rotation)-1)<.01f))return false;
            byte[] bits;try{bits=Convert.FromBase64String(data.Substring(Version.Length));}catch(FormatException){return false;}
            if(!structure)Rebuild();
            if(expectedBlocks!=bodies.Count||bits.Length!=(links.Count+7)/8)return false;
            transform.SetPositionAndRotation(origin.position,origin.rotation);transform.localScale=scale;
            // Snapshots apply poses and activity immediately after the layout. A broken
            // bracket changes one bit without reallocating the entire excavation.
            for(int i=0;i<links.Count;i++)
            {
                var link=links[i];
                bool broken=(bits[i/8]&(1<<(i%8)))!=0;
                if(link.broken==broken)continue;
                link.broken=broken;
                if(broken)RemoveJoint(link);else Connect(link);
            }
            return true;
        }
        static void RemoveJoint(Link l){if(l.joint){l.joint.connectedBody=null;DestroyImmediate(l.joint);l.joint=null;}}
        public void SetReplica(bool value)
        {
            if(replica==value)return;
            TrackBreaks();replica=value;
            foreach(var b in bodies)b.isKinematic=value;
            foreach(var l in links){if(value)RemoveJoint(l);else Connect(l);}
        }
    }
}
