using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class GingerbreadKingdom : MonoBehaviour, IWorldQuery, ISyncedWorld, IRebuildable, IMapFootprint
    {
        public Material[] finishes;
        public PhysicsMaterial friction;
        const int Biscuit=0, Caramel=1, Cocoa=2, Ice=3, Snow=4, Pink=5, Violet=6, Red=7, Green=8, Glow=9, Gold=10, Stone=11;
        readonly List<Rigidbody> pieces=new();
        readonly List<Mesh> meshes=new();
        readonly Collider[] fallen=new Collider[128];
        float nextFallCheck;
        Transform town,plot;
        bool replica;
        public IReadOnlyList<Rigidbody> Blocks=>pieces;
        public Transform Root=>transform;
        public int Revision {get;private set;}
        public Vector3 Center=>transform.position;
        public float TopHeight=>builtBlender?transform.TransformPoint(blenderBounds.max).y:transform.TransformPoint(V(0,ClockCrown,-8)).y;
        public float Radius=>(builtBlender?60:32)*transform.lossyScale.x;
        public float StepHeight=>.18f;
        public Bounds PlayableBounds=>builtBlender?new Bounds(transform.TransformPoint(blenderBounds.center),Vector3.Scale(blenderBounds.size+V(4,4,4),transform.lossyScale)):new Bounds(Center+Vector3.up*10.5f*transform.lossyScale.y,Vector3.Scale(V(55,31,64),transform.lossyScale));
        public int FootprintVersion=>1;
        public float FootprintHalfSize=>32;
        public void GetGroundFootprints(List<Footprint> into)=>into.Add(new Footprint{center=Vector2.zero,halfSize=new Vector2(27,32),rotation=Quaternion.identity});
        public float WaterHeight(Vector3 position)=>transform.position.y-5*transform.lossyScale.y;
        public bool IsSheltered(Vector3 position)=>Physics.Raycast(position+Vector3.up*.15f,Vector3.up,25,1<<Layers.Block);
        public Vector3 SafeSpawnPoint()=>builtBlender?transform.TransformPoint(blenderSpawn+V(0,.12f,0)):transform.TransformPoint(V(0,.12f,-23));
        public string SaveLayout()=>builtBlender||UseBlenderPhysicsTown?"gingerbread-kingdom-blender-physics-v1":"gingerbread-kingdom-v2";
        public bool LoadLayout(string layout,int expectedBlocks,Pose origin,Vector3 scale)
        {
            if(layout!=SaveLayout() || !float.IsFinite(origin.position.sqrMagnitude) || !float.IsFinite(scale.sqrMagnitude) ||
               scale.x<=0 || (scale-Vector3.one*scale.x).sqrMagnitude>.00001f ||
               !(Mathf.Abs(Quaternion.Dot(origin.rotation,origin.rotation)-1)<.001f)) return false;
            if(pieces.Count==0)Rebuild();
            if(expectedBlocks!=pieces.Count)return false;
            transform.SetPositionAndRotation(origin.position,origin.rotation);transform.localScale=scale;Rebuild();return true;
        }
        public void SetReplica(bool value)
        {
            replica=value;
            foreach(var body in pieces)body.isKinematic=value;
        }
        void FixedUpdate()
        {
            FinishBlenderSettle();
            if(Time.time<nextFallCheck)return;
            nextFallCheck=Time.time+.1f;
            Vector3 center=transform.TransformPoint(V(0,-56,0));
            int mask=~((1<<Layers.Block)|(1<<Layers.Surface));
            int count=Physics.OverlapBoxNonAlloc(center,Vector3.Scale(V(125,50,125),transform.lossyScale),
                fallen,transform.rotation,mask,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                var actor=fallen[i].GetComponentInParent<IDamageable>();
                if(actor!=null && actor.Health>0)actor.Eliminate(gameObject);
                fallen[i]=null;
            }
        }
        void OnEnable()=>Services.World=this;
        void OnDisable(){if(ReferenceEquals(Services.World,this))Services.World=null;}
        void FreeMeshes(){foreach(var mesh in meshes)Destroy(mesh);meshes.Clear();}
        void OnDestroy()=>FreeMeshes();
        static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
        Transform Group(string label,Transform parent,Vector3 at,float yaw=0)
        {
            var t=new GameObject(label).transform;t.SetParent(parent,false);
            t.localPosition=at;t.localRotation=Quaternion.Euler(0,yaw,0);return t;
        }
        void Mount(Transform owner,GingerbreadMesh mesh)=>meshes.Add(mesh.Mount(owner,finishes));
        Transform Part(string label,Vector3 at,Vector3 size,int finish,bool loose=true,Action<GingerbreadMesh> garnish=null,Quaternion tilt=default)
        {
            var part=Group(label,plot,at);
            if(tilt!=default)part.localRotation=tilt;
            var box=part.gameObject.AddComponent<BoxCollider>();box.size=size;box.sharedMaterial=friction;
            var mesh=new GingerbreadMesh();mesh.Box(finish,Vector3.zero,size);garnish?.Invoke(mesh);Mount(part,mesh);
            if(!loose)return part;
            var body=part.gameObject.AddComponent<Rigidbody>();body.mass=Mathf.Clamp(size.x*size.y*size.z*2,4,55);
            body.solverIterations=16;body.solverVelocityIterations=6;body.isKinematic=replica;
            var block=part.gameObject.AddComponent<CastleBlock>();block.visualRoot=part.Find("Visual");pieces.Add(body);
            if(label=="Pullable bridge bearing")part.gameObject.AddComponent<GingerbreadBearing>();
            return part;
        }
        public void Rebuild()
        {
            if(town){town.gameObject.SetActive(false);Destroy(town.gameObject);}
            pieces.Clear();FreeMeshes();town=Group("Sugar valley",transform,Vector3.zero);plot=town;builtBlender=false;
            if(UseBlenderPhysicsTown){BuildBlenderTown();Physics.SyncTransforms();Revision++;return;}
            Landscape();
            House(-1,-16,8,3.8f,6.4f,2,4);
            House(-9,-11,-18,4,5.4f,3,7);
            House(-10,-4,-12,3.5f,5.4f,2,2);
            House(-12,3,-22,3.4f,4.8f,2,5);
            House(-22,-2,60,4,5.6f,1,1);
            House(-22,7,85,4,5.6f,2,6);
            House(-18,13,-30,4,5,2,2);
            House(-13,16,-18,3.8f,5,2,5);
            House(9,-12,16,4.6f,6.2f,2,3);
            House(11,-5,12,3.5f,5.6f,2,6);
            House(12,2,8,3.4f,5.8f,2,0);
            House(20,-15.35f,-22,4.4f,6.2f,2,2);
            House(23,2,-10,4,6,2,1);
            House(23,10,-25,3.8f,5.5f,2,4);
            House(21,17,-30,3.8f,5,2,0);
            Palace();Market();Bridge(15,-11);Bridge(-16,2);BuildSkyline();Physics.SyncTransforms();Revision++;
        }
    }
}
