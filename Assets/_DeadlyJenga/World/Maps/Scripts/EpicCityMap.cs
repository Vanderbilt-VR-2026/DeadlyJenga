using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed class EpicCityMap : MonoBehaviour,IWorldQuery,IMapFootprint,ISyncedWorld,IRebuildable,ITraversalCollisionWorld
    {
        [Range(0,9)] public int design;
        public Material[] palette;
        public PhysicsMaterial friction;
        public Transform foundation;
        public Mesh islandCollision;
        public bool generateOnStart=true;
        [Min(1)] public int solverIterations=16,velocityIterations=8;
        [Min(.01f)] public float waterReferenceArea=12.25f;
        [Tooltip("Default-wave lift remains below gravity at the .65 lower bound.")]
        [Min(.65f)] public float minimumWaterResistance=.65f;
        EpicCityPlan plan;
        public EpicCityPlan Plan=>plan??=EpicCityLayout.Build(design);
        readonly List<Rigidbody> bodies=new();
        readonly List<MayhemModule> modules=new();
        readonly Collider[] clearance=new Collider[24];
        readonly Dictionary<int,float> submerged=new();
        Transform generated;
        Material fallback;
        Mesh runtimeGround;
        Bounds authored;
        bool replica,collisionOwned,oldIgnored;
        int spawnCursor,cleanupCursor;
        float nextCleanup;
        public int Revision {get;private set;}
        public Transform Root=>transform;
        public IReadOnlyList<Rigidbody> Blocks=>bodies;
        public IReadOnlyList<MayhemModule> Authored=>modules;
        public bool UsesOriginalColliders=>true;
        Bounds LocalBounds=>authored.size.sqrMagnitude>0?authored:MayhemStructureLayout.Bounds(Plan.modules);
        public Vector3 Center=>transform.position;
        public float TopHeight=>PlayableBounds.max.y;
        public float Radius=>new Vector2(PlayableBounds.extents.x,PlayableBounds.extents.z).magnitude;
        public float StepHeight=>3.5f;
        public int FootprintVersion=>9200+design;
        public float FootprintHalfSize=>Mathf.Max(Plan.halfSize.x,Plan.halfSize.y);
        public Bounds PlayableBounds
        {
            get
            {
                var b=LocalBounds;b.Encapsulate(new Vector3(-Plan.halfSize.x,0,-Plan.halfSize.y));b.Encapsulate(new Vector3(Plan.halfSize.x,0,Plan.halfSize.y));
                var result=new Bounds(transform.TransformPoint(b.center),Vector3.zero);
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)result.Encapsulate(transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z))));
                if(foundation)foreach(var ground in foundation.GetComponents<Collider>())if(ground.enabled)result.Encapsulate(ground.bounds);
                return result;
            }
        }
        void OnEnable(){Services.World=this;if(generated)EnableCollisionPolicy();}
        void Start(){if(generateOnStart&&!generated)Rebuild();}
        void OnDisable(){if(collisionOwned){Physics.IgnoreLayerCollision(Layers.Survivor,Layers.Block,oldIgnored);collisionOwned=false;}if(ReferenceEquals(Services.World,this))Services.World=null;}
        public float WaterHeight(Vector3 p)=>transform.position.y+.08f;
        public bool IsSheltered(Vector3 p)=>gameObject.scene.GetPhysicsScene().Raycast(p+Vector3.up*.1f,Vector3.up,out _,200,1<<Layers.Block,QueryTriggerInteraction.Ignore);
        public void GetGroundFootprints(List<Footprint> into)
        {
            into.Add(new Footprint{center=Vector2.zero,halfSize=Plan.halfSize,rotation=Quaternion.identity});
        }
        public void Rebuild()
        {
            plan=EpicCityLayout.Build(design);var layout=plan.modules;
            if(generated){generated.gameObject.SetActive(false);if(Application.isPlaying)Destroy(generated.gameObject);else DestroyImmediate(generated.gameObject);}
            modules.Clear();modules.AddRange(layout);bodies.Clear();submerged.Clear();spawnCursor=cleanupCursor=0;
            authored=MayhemStructureLayout.Bounds(layout);
            generated=new GameObject("Connected city physical sections").transform;generated.SetParent(transform,false);
            if(palette==null||palette.Length<4)
            {fallback=new Material(Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard")){color=Color.gray};palette=new[]{fallback,fallback,fallback,fallback};}
            var lookup=new Dictionary<MayhemModule,Rigidbody>();
            foreach(var m in layout)
            {
                var go=new GameObject(bodies.Count.ToString("D4")+" · "+Plan.sites[m.district].name+" · "+m.purpose);go.layer=Layers.Block;
                go.transform.SetParent(generated,false);go.transform.localPosition=m.position;go.transform.localRotation=m.rotation*m.shapes[0].rotation;
                var rb=go.AddComponent<Rigidbody>();rb.solverIterations=solverIterations;rb.solverVelocityIterations=velocityIterations;
                rb.interpolation=replica?RigidbodyInterpolation.None:RigidbodyInterpolation.Interpolate;
                rb.useGravity=true;rb.isKinematic=replica;rb.sleepThreshold=.01f;rb.maxDepenetrationVelocity=3;
                go.AddComponent<MayhemStructuralMember>().Build(m,palette,friction);
                go.AddComponent<TowerSectionWaterLoad>().Initialize(m,rb.mass,Mathf.Abs(transform.lossyScale.x),waterReferenceArea,minimumWaterResistance);
                bodies.Add(rb);lookup.Add(m,rb);
            }
            foreach(var m in layout)if(m.hingeSupport!=null)
            {
                var rb=lookup[m];var joint=rb.gameObject.AddComponent<HingeJoint>();joint.connectedBody=lookup[m.hingeSupport];joint.autoConfigureConnectedAnchor=false;
                var pivot=transform.TransformPoint(m.pivot);joint.anchor=rb.transform.InverseTransformPoint(pivot);joint.connectedAnchor=joint.connectedBody.transform.InverseTransformPoint(pivot);
                joint.axis=rb.transform.InverseTransformDirection(transform.rotation*m.rotation*Vector3.forward);joint.useLimits=true;
                joint.limits=new JointLimits{min=-80,max=80,bounciness=0,contactDistance=1};joint.enableCollision=true;
            }
            if(!foundation){foundation=new GameObject("Continuous city island ground").transform;foundation.SetParent(transform,false);}
            if(!islandCollision){if(runtimeGround)Destroy(runtimeGround);runtimeGround=EpicCityTerrain.Create(Plan);}
            if(foundation)
            {
                if(islandCollision||runtimeGround)
                {
                    var box=foundation.GetComponent<BoxCollider>();if(box)box.enabled=false;
                    var mesh=foundation.GetComponent<MeshCollider>();if(!mesh)mesh=foundation.gameObject.AddComponent<MeshCollider>();mesh.sharedMesh=islandCollision?islandCollision:runtimeGround;mesh.enabled=true;
                    foundation.localPosition=Vector3.zero;foundation.localScale=Vector3.one;
                }
                else
                {
                    var mesh=foundation.GetComponent<MeshCollider>();if(mesh)mesh.enabled=false;
                    var box=foundation.GetComponent<BoxCollider>();if(!box)box=foundation.gameObject.AddComponent<BoxCollider>();box.enabled=true;
                    foundation.localPosition=new Vector3(0,-1.02f,0);foundation.localScale=new Vector3(Plan.halfSize.x*2,2,Plan.halfSize.y*2);
                }
            }
            EnableCollisionPolicy();
            Physics.SyncTransforms();Revision++;
        }
        void EnableCollisionPolicy()
        {
            if(!collisionOwned){oldIgnored=Physics.GetIgnoreLayerCollision(Layers.Survivor,Layers.Block);collisionOwned=true;}
            Physics.IgnoreLayerCollision(Layers.Survivor,Layers.Block,false);
        }
        public Vector3 SafeSpawnPoint()
        {
            Physics.SyncTransforms();var physics=gameObject.scene.GetPhysicsScene();
            for(int attempt=0;attempt<80;attempt++)
            {
                int index=spawnCursor++;var center=Plan.spawnPoints[index%Plan.spawnPoints.Count];
                var offset=new Vector3(((index/Plan.spawnPoints.Count)%5-2)*2,0,0);
                var ray=transform.TransformPoint(center+offset+Vector3.up*120);
                if(!physics.Raycast(ray,-transform.up,out var hit,150,(1<<Layers.Block)|1,QueryTriggerInteraction.Ignore)||Vector3.Dot(hit.normal,transform.up)<.75f)continue;
                var p=hit.point+transform.up*.08f;
                if(physics.OverlapCapsule(p+transform.up*.2f,p+transform.up*.9f,.2f,clearance,(1<<Layers.Block)|(1<<Layers.Surface)|1,QueryTriggerInteraction.Ignore)==0)return p;
            }
            var c=Plan.spawnPoints[0];c.y=EpicCityLayout.GroundHeight(Plan,c)+.15f;return transform.TransformPoint(c);
        }
        [Serializable] sealed class State {public int version=1,design;}
        const string Prefix="epic-city-v1:";
        public string SaveLayout()=>Prefix+JsonUtility.ToJson(new State{design=design});
        public bool LoadLayout(string text,int expectedBlocks,Pose origin,Vector3 scale)
        {
            if(text==null||!text.StartsWith(Prefix,StringComparison.Ordinal)||!Finite(origin.position)||!Finite(scale)||scale.x<=0||Mathf.Abs(scale.x-scale.y)>.001f||Mathf.Abs(scale.x-scale.z)>.001f)return false;
            float q=Quaternion.Dot(origin.rotation,origin.rotation);if(float.IsNaN(q)||float.IsInfinity(q)||Mathf.Abs(q-1)>.001f)return false;
            State state;try{state=JsonUtility.FromJson<State>(text.Substring(Prefix.Length));}catch(ArgumentException){return false;}
            if(state==null||state.version!=1||state.design<0||state.design>9||EpicCityLayout.Build(state.design).modules.Count!=expectedBlocks)return false;
            design=state.design;transform.SetPositionAndRotation(origin.position,origin.rotation);transform.localScale=scale;Rebuild();return true;
        }
        static bool Finite(Vector3 p)=>!(float.IsNaN(p.x)||float.IsNaN(p.y)||float.IsNaN(p.z)||float.IsInfinity(p.x)||float.IsInfinity(p.y)||float.IsInfinity(p.z));
        public void SetReplica(bool value)
        {
            replica=value;
            foreach(var body in bodies)if(body){body.collisionDetectionMode=CollisionDetectionMode.Discrete;body.isKinematic=value;body.interpolation=value?RigidbodyInterpolation.None:RigidbodyInterpolation.Interpolate;}
        }
        void FixedUpdate()
        {
            if(replica||Time.time<nextCleanup||(Services.Session is IWorldAuthority a&&!a.HasWorldAuthority))return;
            nextCleanup=Time.time+.25f;
            foreach(var mover in Tracked.Movers)
            {
                if(!(mover is Component actor)||!actor||actor.transform.position.y>=WaterHeight(actor.transform.position)-6)continue;
                var target=actor.GetComponent<IDamageable>();if(target!=null&&target.Health>0)target.Eliminate(gameObject);
            }
            for(int n=0;n<Mathf.Min(32,bodies.Count);n++)
            {
                int i=cleanupCursor++%bodies.Count;var body=bodies[i];if(!body||!body.gameObject.activeSelf)continue;
                var b=body.GetComponent<MayhemStructuralMember>().Shapes[0].bounds;
                foreach(var shape in body.GetComponent<MayhemStructuralMember>().Shapes)b.Encapsulate(shape.bounds);
                if(b.max.y>WaterHeight(b.center)-.2f){submerged.Remove(i);continue;}
                if(!submerged.TryGetValue(i,out float begin)){submerged[i]=Time.time;continue;}
                if(Time.time-begin>3)body.gameObject.SetActive(false);
            }
            if(cleanupCursor>100000000)cleanupCursor=0;
        }
        void OnDestroy(){if(fallback)Destroy(fallback);if(runtimeGround)Destroy(runtimeGround);}
    }
}

