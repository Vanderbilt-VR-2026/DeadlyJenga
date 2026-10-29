using System;
using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    [DefaultExecutionOrder(-50)]
    public sealed class BreakwaterVillageMap : MonoBehaviour, IWorldQuery, IMapFootprint, ISyncedWorld, IRebuildable
    {
        public VillagePiece[] pieces = Array.Empty<VillagePiece>();
        public Transform arrival;
        readonly List<Rigidbody> bodies = new List<Rigidbody>();
        bool initialized, replica;
        int lastBroken;
        public int Revision { get; private set; }
        public Transform Root => transform;
        public IReadOnlyList<Rigidbody> Blocks { get { Initialize(); return bodies; } }
        public Vector3 Center => transform.position;
        public float TopHeight => PlayableBounds.max.y;
        public float Radius => 51 * transform.lossyScale.x;
        public float StepHeight => .4f;
        public int BrokenConnections { get { int n=0; foreach(var p in pieces) if(p)n+=p.BrokenConnections; return n; } }
        public Bounds PlayableBounds
        {
            get
            {
                var b = new Bounds(transform.position, Vector3.zero);
                for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)for(int y=0;y<=1;y++)
                    b.Encapsulate(transform.TransformPoint(new Vector3(x*37,y==0?-1:15,z*35)));
                return b;
            }
        }
        void Awake() => Initialize();
        void OnEnable() { Services.World = this; }
        void Start() { if(Application.isPlaying)Rebuild(); }
        void OnDisable() { if(ReferenceEquals(Services.World,this))Services.World=null; }
        void OnDestroy() { foreach(var p in pieces)if(p)p.Fractured-=Fracture; }
        void Initialize()
        {
            if(initialized)return;initialized=true;bodies.Clear();
            foreach(var p in pieces)if(p){bodies.Add(p.Body);p.Fractured+=Fracture;}
        }
        public void Rebuild()
        {
            Initialize();foreach(var p in pieces)if(p)p.ClearConnectionsForRebuild();
            foreach(var p in pieces)if(p)p.RestorePose(replica);
            Physics.SyncTransforms();foreach(var p in pieces)if(p)p.RestoreConnections();
            lastBroken=0;Revision++;
        }
        void Fracture(VillagePiece piece)
        {
            foreach(var p in pieces)if(p)p.CutConnectionsTo(piece);
            Revision++;
        }
        void FixedUpdate()
        {
            if(replica)return;
            int count=BrokenConnections;if(count!=lastBroken){lastBroken=count;Revision++;}
        }
        public Vector3 SafeSpawnPoint() => arrival ? arrival.position : transform.TransformPoint(new Vector3(0,.12f,-26));
        public float WaterHeight(Vector3 p) => transform.position.y-3;
        public bool IsSheltered(Vector3 p) => Physics.Raycast(p+Vector3.up*.12f,Vector3.up,30,1<<Layers.Block,QueryTriggerInteraction.Ignore);
        [Serializable] sealed class State { public int version=1; public bool[] broken; public float[] damage; }
        public string SaveLayout()
        {
            var state=new List<bool>();foreach(var p in pieces)foreach(var c in p.connections)state.Add(c.broken||(c.created&&!c.joint));
            var health=new float[pieces.Length];for(int i=0;i<pieces.Length;i++)health[i]=pieces[i].AccumulatedDamage;
            return JsonUtility.ToJson(new State{broken=state.ToArray(),damage=health});
        }
        public bool LoadLayout(string text,int expectedBlocks,Pose origin,Vector3 scale)
        {
            State s;try{s=JsonUtility.FromJson<State>(text);}catch(ArgumentException){return false;}
            int count=0;foreach(var p in pieces)count+=p.connections.Length;
            if(s==null||s.version!=1||s.broken==null||s.broken.Length!=count||s.damage==null||s.damage.Length!=pieces.Length||expectedBlocks!=pieces.Length||!Finite(origin.position)||!Finite(scale)||
                scale.x<=0||Mathf.Abs(scale.x-scale.y)>.0001f||Mathf.Abs(scale.x-scale.z)>.0001f||
                !Finite(new Vector3(origin.rotation.x,origin.rotation.y,origin.rotation.z))||!Finite(origin.rotation.w)||Mathf.Abs(Quaternion.Dot(origin.rotation,origin.rotation)-1)>.01f)return false;
            for(int i=0;i<s.damage.Length;i++)if(!Finite(s.damage[i])||s.damage[i]<0||s.damage[i]>pieces[i].MaxHealth)return false;
            transform.SetPositionAndRotation(origin.position,origin.rotation);transform.localScale=scale;Rebuild();int cursor=0;
            for(int i=0;i<pieces.Length;i++){pieces[i].ApplyBroken(s.broken,ref cursor);pieces[i].RestoreDamage(s.damage[i]);}lastBroken=BrokenConnections;return true;
        }
        public void SetReplica(bool value){Initialize();replica=value;foreach(var p in pieces)if(p)p.SetReplica(value);}
        static bool Finite(float n)=>!float.IsNaN(n)&&!float.IsInfinity(n);
        static bool Finite(Vector3 p)=>Finite(p.x)&&Finite(p.y)&&Finite(p.z);
        public int FootprintVersion=>1;
        public float FootprintHalfSize=>37;
        public void GetGroundFootprints(List<Footprint> into)=>into.Add(new Footprint{center=Vector2.zero,halfSize=new Vector2(37,35),rotation=Quaternion.identity});
    }
}
