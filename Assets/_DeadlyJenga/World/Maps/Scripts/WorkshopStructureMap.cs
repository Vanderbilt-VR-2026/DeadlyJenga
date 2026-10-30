using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed class WorkshopStructureMap : MonoBehaviour, IWorldQuery, IMapFootprint, ISyncedWorld, IRebuildable
    {
        public WorkshopStructureKind kind;
        public GameObject blockPrefab;
        public Material pylonMaterial, capMaterial, deckMaterial;
        public Transform foundation;
        [Min(1)] public int positionIterations = 20, velocityIterations = 8;
        public readonly List<Rigidbody> Bodies = new List<Rigidbody>();
        public int Revision { get; private set; }
        public Transform Root => transform;
        public IReadOnlyList<Rigidbody> Blocks => Bodies;
        List<WorkshopMember> authored;
        Bounds localBounds;
        Transform generated;
        bool replica;
        float nextCleanup;
        int cleanupCursor;
        readonly Dictionary<int,float> submerged = new Dictionary<int,float>();
        public IReadOnlyList<WorkshopMember> Authored { get { var bounds=LocalBounds;return authored; } }
        Bounds LocalBounds { get { if(authored==null){authored=WorkshopStructureLayout.Build(kind);localBounds=WorkshopStructureLayout.Bounds(authored);}return localBounds; } }
        public Vector3 Center => transform.TransformPoint(new Vector3(LocalBounds.center.x,0,LocalBounds.center.z));
        public float TopHeight => PlayableBounds.max.y;
        public float Radius => new Vector2(PlayableBounds.extents.x,PlayableBounds.extents.z).magnitude;
        public float StepHeight => 1.5f;
        public Bounds PlayableBounds
        {
            get
            {
                var b=LocalBounds;var result=new Bounds(transform.TransformPoint(b.center),Vector3.zero);
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                    result.Encapsulate(transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z))));
                return result;
            }
        }
        void OnEnable(){Services.World=this;}
        void OnDisable(){if(ReferenceEquals(Services.World,this))Services.World=null;}
        void Start(){if(!generated)Rebuild();}
        public float WaterHeight(Vector3 p)=>transform.position.y+.08f;
        public bool IsSheltered(Vector3 p)=>Physics.Raycast(p+Vector3.up*.1f,Vector3.up,500,1<<Layers.Block,QueryTriggerInteraction.Ignore);
        public void Rebuild()
        {
            var layout=WorkshopStructureLayout.Build(kind);
            if(!blockPrefab||!blockPrefab.GetComponent<Rigidbody>()||!blockPrefab.GetComponent<CastleBlock>()||!blockPrefab.GetComponent<BoxCollider>())
                throw new InvalidOperationException("Assign the normal physical CastleBlock prefab.");
            if(generated){generated.gameObject.SetActive(false);if(Application.isPlaying)Destroy(generated.gameObject);else DestroyImmediate(generated.gameObject);}
            authored=layout;localBounds=WorkshopStructureLayout.Bounds(layout);Bodies.Clear();submerged.Clear();cleanupCursor=0;
            generated=new GameObject("Generated Loose Structural Members").transform;generated.SetParent(transform,false);
            if(foundation)
            {
                foundation.localPosition=new Vector3(localBounds.center.x,-.5f,localBounds.center.z);
                foundation.localRotation=Quaternion.identity;foundation.localScale=new Vector3(localBounds.size.x+12,1,localBounds.size.z+12);
            }
            foreach(var member in layout)
            {
                var go=Instantiate(blockPrefab,generated);go.name=Bodies.Count.ToString("D4")+" "+member.Purpose;
                go.transform.localPosition=member.Center;go.transform.localRotation=Quaternion.identity;go.transform.localScale=member.Size;
                var box=go.GetComponent<BoxCollider>();box.center=Vector3.zero;box.size=Vector3.one;
                var body=go.GetComponent<Rigidbody>();body.position=go.transform.position;body.rotation=go.transform.rotation;
                body.mass=Mathf.Max(2,40*member.Size.x*member.Size.y*member.Size.z/24);
                body.solverIterations=positionIterations;body.solverVelocityIterations=velocityIterations;
                body.useGravity=true;body.isKinematic=replica;
                if(replica)body.collisionDetectionMode=CollisionDetectionMode.Discrete;
                var block=go.GetComponent<CastleBlock>();
                block.sectionMaterial=member.Section==CastleSection.Corner?pylonMaterial:member.Section==CastleSection.Wall?capMaterial:deckMaterial;
                block.RefreshAppearance();block.SyncSurface();Bodies.Add(body);
            }
            Physics.SyncTransforms();Revision++;
        }
        public Vector3 SafeSpawnPoint()
        {
            Physics.SyncTransforms();float best=float.NegativeInfinity;Vector3 point=Center+Vector3.up*.1f;
            foreach(var body in Bodies)
            {
                if(!body||!body.gameObject.activeInHierarchy||Vector3.Dot(body.transform.up,Vector3.up)<.98f)continue;
                var bounds=body.GetComponent<Collider>().bounds;
                if(bounds.max.y<=best||Mathf.Min(bounds.size.x,bounds.size.z)<1)continue;
                var candidate=new Vector3(bounds.center.x,bounds.max.y+.08f,bounds.center.z);
                if(Physics.CheckCapsule(candidate+Vector3.up*.2f,candidate+Vector3.up*.75f,.18f,1<<Layers.Block,QueryTriggerInteraction.Ignore))continue;
                point=candidate;best=bounds.max.y;
            }
            return point;
        }
        [Serializable] sealed class LayoutState { public int version=1;public WorkshopStructureKind kind; }
        public string SaveLayout()=>JsonUtility.ToJson(new LayoutState{kind=kind});
        public bool LoadLayout(string text,int expectedBlocks,Pose origin,Vector3 scale)
        {
            LayoutState state;try{state=JsonUtility.FromJson<LayoutState>(text);}catch(ArgumentException){return false;}
            if(state==null||state.version!=1||!Enum.IsDefined(typeof(WorkshopStructureKind),state.kind)||!Finite(origin.position)||!Finite(scale)||
                scale.x<=0||Mathf.Abs(scale.x-scale.y)>.0001f||Mathf.Abs(scale.x-scale.z)>.0001f||
                !Finite(new Vector3(origin.rotation.x,origin.rotation.y,origin.rotation.z))||!Finite(origin.rotation.w)||Mathf.Abs(Quaternion.Dot(origin.rotation,origin.rotation)-1)>.01f||
                WorkshopStructureLayout.Build(state.kind).Count!=expectedBlocks)return false;
            kind=state.kind;transform.SetPositionAndRotation(origin.position,origin.rotation);transform.localScale=scale;Rebuild();return true;
        }
        static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
        static bool Finite(Vector3 v)=>Finite(v.x)&&Finite(v.y)&&Finite(v.z);
        public void SetReplica(bool value)
        {
            replica=value;
            foreach(var body in Bodies)
            {
                if(!body)continue;
                if(value)body.collisionDetectionMode=CollisionDetectionMode.Discrete;
                body.isKinematic=value;body.interpolation=value?RigidbodyInterpolation.None:RigidbodyInterpolation.Interpolate;
                if(!value)body.collisionDetectionMode=blockPrefab.GetComponent<Rigidbody>().collisionDetectionMode;
            }
        }
        public int FootprintVersion=>100+(int)kind;
        public float FootprintHalfSize=>Mathf.Max(LocalBounds.size.x,LocalBounds.size.z)*.5f+6;
        public void GetGroundFootprints(List<Footprint> into)
        {var b=LocalBounds;into.Add(new Footprint{center=new Vector2(b.center.x,b.center.z),halfSize=new Vector2(b.extents.x+6,b.extents.z+6),rotation=Quaternion.identity});}
        void FixedUpdate()
        {
            if(replica||Bodies.Count==0||Time.time<nextCleanup)return;
            if(Services.Session is IWorldAuthority authority&&!authority.HasWorldAuthority)return;
            nextCleanup=Time.time+.2f;
            for(int n=0;n<Mathf.Min(64,Bodies.Count);n++)
            {
                int i=cleanupCursor++%Bodies.Count;var body=Bodies[i];if(!body||!body.gameObject.activeSelf)continue;
                if(body.GetComponent<Collider>().bounds.max.y>=WaterHeight(body.position)-.15f){submerged.Remove(i);continue;}
                if(!submerged.TryGetValue(i,out float began)){submerged[i]=Time.time;continue;}
                if(Time.time-began>2)body.gameObject.SetActive(false);
            }
            if(cleanupCursor>100000000)cleanupCursor=0;
        }
    }
}
