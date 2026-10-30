using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadlyJenga.CastlePrototype
{
    public sealed class SparseStructureMap : MonoBehaviour, IWorldQuery, ISyncedWorld, IMapFootprint, IRebuildable, ITraversalCollisionWorld
    {
        public SparseStructureKind kind;
        public GameObject blockPrefab;
        public Material cornerMaterial, wallMaterial, keepMaterial;
        public Transform foundation;
        public bool generateOnStart=true;
        [Min(1)] public int solverIterations=20, solverVelocityIterations=8;
        public float waterHeight=.08f, oceanKillHeight=-6;
        readonly List<Rigidbody> bodies=new List<Rigidbody>();
        readonly Collider[] clearance=new Collider[32];
        readonly Collider[] fallen=new Collider[256];
        readonly Dictionary<int,float> submerged=new Dictionary<int,float>();
        static readonly Vector3[] SpawnOffsets={Vector3.zero,Vector3.right*.75f,Vector3.left*.75f,Vector3.forward*.75f,Vector3.back*.75f};
        Transform generated;
        Material[] cityMaterials;
        Material grayMaterial;
        Bounds authoredBounds;
        bool replica,collisionOwned,oldCollisionIgnored;
        CityDebrisCleanup debris;
        public bool UsesOriginalColliders=>kind==SparseStructureKind.FestivalCityOptimized;
        public CityDebrisCleanup Debris=>debris;
        float nextCleanup;
        public IReadOnlyList<Rigidbody> Blocks=>bodies;
        public Transform Root=>transform;
        public int Revision {get;private set;}
        public Vector3 Center=>SparseStructureLayout.IsCity(kind)?transform.position:transform.TransformPoint(authoredBounds.center.x,0,authoredBounds.center.z);
        public float TopHeight=>transform.TransformPoint(Vector3.up*authoredBounds.max.y).y;
        public float Radius=>Mathf.Max(authoredBounds.extents.x,authoredBounds.extents.z)*Mathf.Max(transform.lossyScale.x,transform.lossyScale.z);
        public float StepHeight=>2;
        public int FootprintVersion=>(int)kind+1000;
        public float FootprintHalfSize=>Mathf.Max(LayoutBounds.extents.x,LayoutBounds.extents.z)+4;
        Bounds LayoutBounds=>SparseStructureLayout.Bounds(SparseStructureLayout.Build(kind));
        public Bounds PlayableBounds
        {
            get
            {
                var source=authoredBounds.size.sqrMagnitude>0?authoredBounds:LayoutBounds;
                source.Expand(new Vector3(8,8,8));source.Encapsulate(new Vector3(source.center.x,-1,source.center.z));
                var world=new Bounds(transform.TransformPoint(source.min),Vector3.zero);
                for(int x=0;x<2;x++)for(int y=0;y<2;y++)for(int z=0;z<2;z++)
                    world.Encapsulate(transform.TransformPoint(new Vector3(x==0?source.min.x:source.max.x,y==0?source.min.y:source.max.y,z==0?source.min.z:source.max.z)));
                return world;
            }
        }
        void OnEnable(){authoredBounds=LayoutBounds;Services.World=this;if(Application.isPlaying&&UsesOriginalColliders&&bodies.Count>0)ConfigureOriginalCollision();}
        void OnDisable(){if(collisionOwned){Physics.IgnoreLayerCollision(Layers.Survivor,Layers.Block,oldCollisionIgnored);collisionOwned=false;}if(ReferenceEquals(Services.World,this))Services.World=null;}
        void Start(){if(generateOnStart&&bodies.Count==0)Rebuild();}
        public float WaterHeight(Vector3 position)=>waterHeight;
        public bool IsSheltered(Vector3 position)=>Physics.Raycast(position+Vector3.up*.1f,Vector3.up,500,1<<Layers.Block,QueryTriggerInteraction.Ignore);
        public void GetGroundFootprints(List<Footprint> into)
        {
            foreach(var piece in SparseStructureLayout.Build(kind))if(piece.Bounds.min.y<.01f)
                into.Add(new Footprint{center=new Vector2(piece.Bounds.center.x,piece.Bounds.center.z),halfSize=new Vector2(piece.Bounds.size.x,piece.Bounds.size.z)*.5f,rotation=Quaternion.identity});
        }
        public void Rebuild()
        {
            if(!blockPrefab||!blockPrefab.GetComponent<CastleBlock>()||!blockPrefab.GetComponent<Rigidbody>())throw new InvalidOperationException("Sparse map needs the ordinary physical block prefab.");
            if(generated){generated.gameObject.SetActive(false);if(Application.isPlaying)Destroy(generated.gameObject);else DestroyImmediate(generated.gameObject);}
            bodies.Clear();submerged.Clear();
            var layout=SparseStructureLayout.Build(kind);authoredBounds=SparseStructureLayout.Bounds(layout);
            generated=new GameObject("Loose structural members").transform;generated.SetParent(transform,false);
            foreach(var piece in layout)
            {
                if(piece.Parts!=null)
                {
                    var section=new GameObject(bodies.Count+" - "+piece.Role);section.transform.SetParent(generated,false);section.transform.localPosition=piece.Position;section.transform.localRotation=piece.Rotation;
                    var rb=section.AddComponent<Rigidbody>();rb.mass=40;rb.solverIterations=solverIterations;rb.solverVelocityIterations=solverVelocityIterations;rb.useGravity=true;rb.isKinematic=replica;
                    rb.interpolation=replica?RigidbodyInterpolation.None:RigidbodyInterpolation.Interpolate;
                    rb.collisionDetectionMode=CollisionDetectionMode.Discrete;
                    section.AddComponent<CityStructuralMember>().Build(piece.Parts,CityMaterials(),blockPrefab.GetComponent<BoxCollider>().sharedMaterial,kind==SparseStructureKind.FestivalCityPacked||kind==SparseStructureKind.FestivalCitySurfaceMesh||UsesOriginalColliders,kind==SparseStructureKind.FestivalCitySurfaceMesh,UsesOriginalColliders);
                    rb.position=section.transform.position;rb.rotation=section.transform.rotation;
                    bodies.Add(rb);continue;
                }
                var go=Instantiate(blockPrefab,generated);go.name=bodies.Count+" · "+piece.Role;
                go.transform.localPosition=piece.Position;go.transform.localRotation=piece.Rotation;go.transform.localScale=piece.Size;
                var block=go.GetComponent<CastleBlock>();
                block.sectionMaterial=piece.Section==CastleSection.Corner?cornerMaterial:piece.Section==CastleSection.Wall?wallMaterial:keepMaterial;
                block.RefreshAppearance();var body=go.GetComponent<Rigidbody>();
                body.position=go.transform.position;body.rotation=go.transform.rotation;
                body.mass=Mathf.Max(1,40*piece.Size.x*piece.Size.y*piece.Size.z/24);
                body.solverIterations=solverIterations;body.solverVelocityIterations=solverVelocityIterations;
                body.useGravity=true;body.isKinematic=replica;body.detectCollisions=true;
                if(replica)body.collisionDetectionMode=CollisionDetectionMode.Discrete;
                block.SyncSurface();bodies.Add(body);
            }
            if(foundation)
            {
                foundation.localPosition=new Vector3(authoredBounds.center.x,-.5f,authoredBounds.center.z);
                foundation.localScale=new Vector3(authoredBounds.size.x+8,1,authoredBounds.size.z+8);
            }
            if(Application.isPlaying&&UsesOriginalColliders)
            {
                ConfigureOriginalCollision();debris=generated.gameObject.AddComponent<CityDebrisCleanup>();debris.Initialize(bodies);debris.SetReplica(replica);
            }
            Physics.SyncTransforms();Revision++;
        }
        void ConfigureOriginalCollision()
        {
            if(!collisionOwned){oldCollisionIgnored=Physics.GetIgnoreLayerCollision(Layers.Survivor,Layers.Block);collisionOwned=true;}
            foreach(var body in bodies)body.GetComponent<CastleBlock>()?.UseOriginalTraversal();
            Physics.IgnoreLayerCollision(Layers.Survivor,Layers.Block,false);
        }
        public Vector3 SafeSpawnPoint()
        {
            Physics.SyncTransforms();var physics=gameObject.scene.GetPhysicsScene();Vector3 best=transform.position+Vector3.up;
            float bestHeight=float.NegativeInfinity;
            foreach(var body in bodies)
            {
                if(!body||!body.gameObject.activeInHierarchy||Vector3.Dot(body.transform.up,Vector3.up)<.95f)continue;
                var city=body.GetComponent<CityStructuralMember>();
                foreach(var shape in city!=null?city.Shapes:new Collider[]{body.GetComponent<BoxCollider>()})
                {
                var bounds=shape.bounds;
                foreach(var offset in SpawnOffsets)
                {
                    Vector3 p=new Vector3(bounds.center.x,bounds.max.y+.04f,bounds.center.z)+offset;
                    if(p.y<=bestHeight)continue;
                    if(physics.OverlapCapsule(p+Vector3.up*.2f,p+Vector3.up*.74f,.18f,clearance,(1<<Layers.Block)|(1<<Layers.Surface)|1,QueryTriggerInteraction.Ignore)>0)continue;
                    best=p;bestHeight=p.y;
                }
            }
            }
            return best;
        }
        Material[] CityMaterials()
        {
            if(cityMaterials!=null)return cityMaterials;
            var source=cornerMaterial;if(!source){grayMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard"));source=grayMaterial;}
            var glass=new Material(source){name="City glazed panels",color=new Color(.015f,.18f,.32f)};glass.SetFloat("_Smoothness",.9f);
            var glow=new Material(wallMaterial?wallMaterial:source){name="City lantern",color=new Color(1,.75f,.12f)};glow.EnableKeyword("_EMISSION");glow.SetColor("_EmissionColor",new Color(1,.55f,.06f)*1.5f);
            cityMaterials=new[]{cornerMaterial?cornerMaterial:source,wallMaterial?wallMaterial:source,keepMaterial?keepMaterial:source,glass,glow};return cityMaterials;
        }
        void OnDestroy(){if(grayMaterial)Destroy(grayMaterial);if(cityMaterials!=null){Destroy(cityMaterials[3]);Destroy(cityMaterials[4]);}}
        [Serializable] sealed class LayoutState {public int version=1;public int kind;}
        const string Prefix="sparse-structures-v1:";
        public string SaveLayout()=>Prefix+JsonUtility.ToJson(new LayoutState{kind=(int)kind});
        public bool LoadLayout(string layout,int expectedBlocks,Pose origin,Vector3 scale)
        {
            if(!blockPrefab||!blockPrefab.GetComponent<CastleBlock>()||layout==null||!layout.StartsWith(Prefix,StringComparison.Ordinal)||!Finite(origin.position)||!Finite(scale)||scale.x<=0||Mathf.Abs(scale.x-scale.y)>.001f||Mathf.Abs(scale.x-scale.z)>.001f)return false;
            float q=origin.rotation.x*origin.rotation.x+origin.rotation.y*origin.rotation.y+origin.rotation.z*origin.rotation.z+origin.rotation.w*origin.rotation.w;
            if(float.IsNaN(q)||float.IsInfinity(q)||Mathf.Abs(q-1)>.001f)return false;
            LayoutState state;try{state=JsonUtility.FromJson<LayoutState>(layout.Substring(Prefix.Length));}catch(ArgumentException){return false;}
            if(state==null||state.version!=1||!Enum.IsDefined(typeof(SparseStructureKind),state.kind))return false;
            var incoming=(SparseStructureKind)state.kind;if(SparseStructureLayout.Build(incoming).Count!=expectedBlocks)return false;
            kind=incoming;transform.SetPositionAndRotation(origin.position,origin.rotation);transform.localScale=scale;Rebuild();return true;
        }
        static bool Finite(Vector3 v)=>!float.IsNaN(v.x)&&!float.IsNaN(v.y)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.x)&&!float.IsInfinity(v.y)&&!float.IsInfinity(v.z);
        public void SetReplica(bool value)
        {
            replica=value;debris?.SetReplica(value);var original=blockPrefab?blockPrefab.GetComponent<Rigidbody>():null;
            foreach(var body in bodies)if(body)
            {
                if(value)body.collisionDetectionMode=CollisionDetectionMode.Discrete;
                body.isKinematic=value;
                if(!value&&original)body.collisionDetectionMode=body.GetComponent<CityStructuralMember>()?CollisionDetectionMode.Discrete:original.collisionDetectionMode;
                body.interpolation=value?RigidbodyInterpolation.None:RigidbodyInterpolation.Interpolate;
            }
        }
        void FixedUpdate()
        {
            if(replica||Time.time<nextCleanup)return;nextCleanup=Time.time+.25f;
            foreach(var body in bodies)
            {
                if(!body||!body.gameObject.activeSelf)continue;var city=body.GetComponent<CityStructuralMember>();var bounds=city?city.CollisionBounds:body.GetComponent<BoxCollider>().bounds;
                if(bounds.max.y>waterHeight-.15f){submerged.Remove(body.GetInstanceID());continue;}
                int id=body.GetInstanceID();if(!submerged.TryGetValue(id,out var began)){submerged[id]=Time.time;continue;}
                if(Time.time-began>=2)body.gameObject.SetActive(false);
            }
            int count=Physics.OverlapBoxNonAlloc(new Vector3(Center.x,oceanKillHeight-500,Center.z),new Vector3(Radius+50,500,Radius+50),fallen,Quaternion.identity,~((1<<Layers.Block)|(1<<Layers.Surface)),QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                var damage=fallen[i].GetComponentInParent<IDamageable>();if(damage!=null)damage.Eliminate(gameObject);fallen[i]=null;
            }
            // The ground only supports structures; the established session island rules still own player movement/death.
        }
    }
}
