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
        const string Version="fossil-basin/4/";
        readonly List<Rigidbody> bodies=new();
        readonly List<FossilMember> decks=new();
        readonly List<Link> links=new();
        Transform structure;
        bool replica;
        int cleanup, spawnIndex;
        float nextSweep;
        sealed class Link { public Rigidbody a,b; public Vector3 anchor; public float force,torque; public FixedJoint joint; public bool broken; }
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
                    result.Encapsulate(transform.TransformPoint(new Vector3(19*x,y,12*z)));
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
            bodies.Clear(); links.Clear(); decks.Clear(); cleanup=spawnIndex=0;
            structure=new GameObject("Excavation").transform; structure.SetParent(transform,false);
            BuildExcavation();
            var tar=new GameObject("Lethal tar contact"); tar.transform.SetParent(structure,false);
            var trigger=tar.AddComponent<BoxCollider>(); trigger.isTrigger=true;
            trigger.center=new Vector3(0,-6,0);trigger.size=new Vector3(200,12,200);
            var sensor=tar.AddComponent<Rigidbody>();sensor.isKinematic=true;sensor.useGravity=false;
            tar.AddComponent<FossilTar>().map=this;
            Physics.SyncTransforms();
            foreach(var l in links) Connect(l);
            Revision++;
        }
        FossilMember Piece(string label, Vector3 center,float mass,Action<FossilCarving> carve)
        {
            var go=new GameObject(label){layer=Layers.Block};go.transform.SetParent(structure,false);go.transform.localPosition=center;
            var rb=go.AddComponent<Rigidbody>();rb.isKinematic=replica;rb.mass=mass;rb.solverIterations=24;rb.solverVelocityIterations=8;
            rb.maxDepenetrationVelocity=2; rb.sleepThreshold=.018f;
            var m=go.AddComponent<FossilMember>();m.rigid=rb;
            var c=new FossilCarving(m,center);carve(c);c.Finish(stone);bodies.Add(rb);return m;
        }
        void Socket(FossilMember a,FossilMember b,Vector3 at,float strength=18000)
        {
            links.Add(new Link{a=a.Body,b=b.Body,anchor=at-a.transform.localPosition,force=strength,torque=strength*.65f});
        }
        void Connect(Link l)
        {
            if(replica||l.broken)return;
            var j=l.a.gameObject.AddComponent<FixedJoint>();
            j.autoConfigureConnectedAnchor=false;
            j.connectedBody=l.b;j.anchor=l.anchor;
            j.connectedAnchor=l.b.transform.InverseTransformPoint(l.a.transform.TransformPoint(l.anchor));
            j.breakForce=l.force;j.breakTorque=l.torque;j.enableCollision=false;l.joint=j;
        }
        void TrackBreaks()
        {
            if(replica)return;
            foreach(var l in links) if(!l.broken&&!l.joint){l.broken=true;Revision++;}
        }
        void FixedUpdate()
        {
            if(!Authority || Time.time<nextSweep || bodies.Count==0)return;
            nextSweep=Time.time+.16f; TrackBreaks();
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
                var deck=decks[spawnIndex++%decks.Count];
                if(!deck.gameObject.activeInHierarchy||deck.transform.up.y<.93f)continue;
                var pos=deck.transform.position+Vector3.up*(.17f*transform.lossyScale.x+.05f);
                if(pos.y<=WaterHeight(pos)+.4f || Physics.CheckCapsule(pos+Vector3.up*.25f,pos+Vector3.up*1.05f,.23f,1<<Layers.Block,QueryTriggerInteraction.Ignore))continue;
                return pos;
            }
            return transform.TransformPoint(new Vector3(0,1.25f,-8));
        }
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
            Rebuild();
            for(int i=0;i<links.Count;i++)if((bits[i/8]&(1<<(i%8)))!=0){links[i].broken=true;RemoveJoint(links[i]);}
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
