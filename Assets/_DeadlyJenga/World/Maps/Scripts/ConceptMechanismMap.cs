using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Six small experimental layouts. All moving pieces use real rigidbody physics.
    public sealed class ConceptMechanismMap : MonoBehaviour, IWorldQuery, IMapFootprint, ISyncedWorld, IRebuildable, ITraversalCollisionWorld
    {
        public ConceptMapKind kind=ConceptMapKind.PeelAwayScreen;
        public bool generateOnStart=true;
        public Material[] palette;
        public PhysicsMaterial friction;
        readonly List<Rigidbody> bodies=new();
        readonly List<MayhemModule> modules=new();
        readonly List<bool> fixedBodies=new();
        readonly Collider[] clearance=new Collider[32];
        readonly Dictionary<int,float> submerged=new();
        Transform generated;
        Material[] ownedPalette;
        Bounds authored;
        bool replica,collisionOwned,oldIgnored;
        int spawnCursor;
        float nextCleanup;
        const string Prefix="concept-mechanisms-v1:";
        public int Revision {get;private set;}
        public Transform Root=>transform;
        public IReadOnlyList<Rigidbody> Blocks=>bodies;
        public IReadOnlyList<MayhemModule> Authored=>modules;
        public bool UsesOriginalColliders=>true;
        public Vector3 Center=>transform.position;
        public float TopHeight=>PlayableBounds.max.y;
        public float Radius=>new Vector2(PlayableBounds.extents.x,PlayableBounds.extents.z).magnitude;
        public float StepHeight=>.75f;
        public int FootprintVersion=>9100+(int)kind;
        public float FootprintHalfSize {get{var size=ConceptMapLayout.FoundationSize(kind);return Mathf.Max(size.x,size.y)*.5f;}}
        public Bounds PlayableBounds
        {
            get
            {
                var size=ConceptMapLayout.FoundationSize(kind);
                var local=authored.size.sqrMagnitude>0?authored:ConceptMapLayout.Bounds(ConceptMapLayout.Build(kind));
                local.Encapsulate(new Vector3(-size.x*.5f,-.1f,-size.y*.5f));
                local.Encapsulate(new Vector3(size.x*.5f,1,size.y*.5f));
                var world=new Bounds(transform.TransformPoint(local.center),Vector3.zero);
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                    world.Encapsulate(transform.TransformPoint(local.center+Vector3.Scale(local.extents,new Vector3(x,y,z))));
                return world;
            }
        }
        void OnEnable(){Services.World=this;if(generated)EnableCollision();}
        void Start(){if(generateOnStart&&!generated)Rebuild();}
        void OnDisable()
        {
            if(collisionOwned){Physics.IgnoreLayerCollision(Layers.Survivor,Layers.Block,oldIgnored);collisionOwned=false;}
            if(ReferenceEquals(Services.World,this))Services.World=null;
        }
        public float WaterHeight(Vector3 p)=>transform.position.y+.08f;
        public bool IsSheltered(Vector3 p)=>gameObject.scene.GetPhysicsScene().Raycast(p+Vector3.up*.1f,Vector3.up,out _,200,1<<Layers.Block,QueryTriggerInteraction.Ignore);
        public void GetGroundFootprints(List<Footprint> into)
        {into.Add(new Footprint{center=Vector2.zero,halfSize=ConceptMapLayout.FoundationSize(kind)*.5f,rotation=Quaternion.identity});}
        public bool TryGetBody(string purpose,out Rigidbody body)
        {
            for(int i=0;i<modules.Count;i++)if(modules[i].purpose==purpose){body=bodies[i];return body;}
            body=null;return false;
        }
        public void Rebuild()
        {
            var layout=ConceptMapLayout.Build(kind);
            if(generated){generated.gameObject.SetActive(false);Dispose(generated.gameObject);}
            bodies.Clear();modules.Clear();fixedBodies.Clear();submerged.Clear();spawnCursor=0;
            modules.AddRange(layout);authored=ConceptMapLayout.Bounds(layout);
            generated=new GameObject("Experimental mechanism geometry").transform;generated.SetParent(transform,false);
            EnsurePalette();BuildFoundation();
            var lookup=new Dictionary<MayhemModule,Rigidbody>();
            foreach(var m in layout)
            {
                var go=new GameObject(m.purpose);go.layer=Layers.Block;go.transform.SetParent(generated,false);
                go.transform.localPosition=m.position;go.transform.localRotation=m.rotation*m.shapes[0].rotation;
                var body=go.AddComponent<Rigidbody>();body.solverIterations=20;body.solverVelocityIterations=12;
                body.maxDepenetrationVelocity=3;body.sleepThreshold=.01f;
                bool anchored=m.purpose.StartsWith("Fixed ",StringComparison.Ordinal);
                body.useGravity=!anchored;body.isKinematic=replica||anchored;
                body.interpolation=replica||anchored?RigidbodyInterpolation.None:RigidbodyInterpolation.Interpolate;
                go.AddComponent<MayhemStructuralMember>().Build(m,ownedPalette??palette,friction);
                bodies.Add(body);fixedBodies.Add(anchored);lookup.Add(m,body);
            }
            foreach(var m in layout)if(m.hingeSupport!=null)
            {
                var body=lookup[m];var joint=body.gameObject.AddComponent<HingeJoint>();joint.connectedBody=lookup[m.hingeSupport];
                joint.autoConfigureConnectedAnchor=false;var pivot=transform.TransformPoint(m.pivot);
                joint.anchor=body.transform.InverseTransformPoint(pivot);joint.connectedAnchor=joint.connectedBody.transform.InverseTransformPoint(pivot);
                joint.axis=body.transform.InverseTransformDirection(transform.up);joint.useLimits=true;
                joint.limits=new JointLimits{min=-65,max=65,bounciness=0,contactDistance=1};joint.enableCollision=false;
                // Coplanar landing lips must not bind the supported yaw hinge.
                foreach(var other in bodies)if(other.name=="Fixed west landing"||other.name=="Fixed east landing")
                    IgnorePair(body,other);
            }
            if(kind==ConceptMapKind.PeelAwayScreen)
                foreach(var body in bodies)if(body.name.StartsWith("Screen ",StringComparison.Ordinal))
                {
                    bool west=body.name=="Screen west";
                    var joint=body.gameObject.AddComponent<HingeJoint>();joint.autoConfigureConnectedAnchor=false;
                    var pivot=body.transform.TransformPoint(new Vector3(-2,0,0));
                    joint.anchor=body.transform.InverseTransformPoint(pivot);joint.connectedAnchor=pivot;
                    joint.axis=body.transform.InverseTransformDirection(transform.forward);joint.useLimits=true;
                    joint.limits=new JointLimits{min=west?0:-100,max=west?100:0,bounciness=0,contactDistance=1};
                }
            if(kind==ConceptMapKind.MovingShelter&&TryGetBody("Moving roof",out var roof)&&TryGetBody("Fixed roof frame A",out var frame))
            {
                var joint=roof.gameObject.AddComponent<ConfigurableJoint>();joint.connectedBody=frame;joint.autoConfigureConnectedAnchor=false;
                joint.anchor=Vector3.zero;joint.connectedAnchor=frame.transform.InverseTransformPoint(roof.position);
                joint.axis=roof.transform.InverseTransformDirection(transform.right);joint.secondaryAxis=roof.transform.InverseTransformDirection(transform.up);
                joint.xMotion=ConfigurableJointMotion.Limited;joint.yMotion=joint.zMotion=ConfigurableJointMotion.Locked;
                joint.angularXMotion=joint.angularYMotion=joint.angularZMotion=ConfigurableJointMotion.Locked;
                joint.linearLimit=new SoftJointLimit{limit=2.5f*transform.lossyScale.x,contactDistance=.02f};joint.enableCollision=false;
            }
            EnableCollision();Physics.SyncTransforms();Revision++;
        }
        static void IgnorePair(Rigidbody a,Rigidbody b)
        {
            foreach(var ca in a.GetComponentsInChildren<Collider>())foreach(var cb in b.GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(ca,cb,true);
        }
        void BuildFoundation()
        {
            var size=ConceptMapLayout.FoundationSize(kind);var floor=new GameObject("Fixed fallback deck");
            floor.transform.SetParent(generated,false);floor.transform.localPosition=new Vector3(0,.15f,0);
            var box=floor.AddComponent<BoxCollider>();box.size=new Vector3(size.x,.5f,size.y);box.sharedMaterial=friction;
            // Default layer keeps this independent bypass out of the fire contact graph.
            var visual=GameObject.CreatePrimitive(PrimitiveType.Cube);visual.name="Visual";visual.transform.SetParent(floor.transform,false);
            visual.transform.localScale=box.size;visual.GetComponent<Collider>().enabled=false;
            Dispose(visual.GetComponent<Collider>());visual.GetComponent<Renderer>().sharedMaterial=(ownedPalette??palette)[3];
        }
        void EnsurePalette()
        {
            if(palette!=null&&palette.Length>=4&&Array.TrueForAll(palette,m=>m))return;
            if(ownedPalette!=null)return;
            var shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            if(!shader)throw new InvalidOperationException("A graybox surface shader is required.");
            var colors=new[]{new Color(.48f,.8f,.75f),new Color(.92f,.65f,.68f),new Color(.7f,.6f,.86f),new Color(.77f,.77f,.7f)};
            ownedPalette=new Material[4];for(int i=0;i<4;i++)ownedPalette[i]=new Material(shader){name="Concept graybox "+i,color=colors[i]};
        }
        void EnableCollision()
        {if(!collisionOwned){oldIgnored=Physics.GetIgnoreLayerCollision(Layers.Survivor,Layers.Block);collisionOwned=true;}Physics.IgnoreLayerCollision(Layers.Survivor,Layers.Block,false);}
        public Vector3 SafeSpawnPoint()
        {
            Physics.SyncTransforms();var physics=gameObject.scene.GetPhysicsScene();var candidates=ConceptMapLayout.SpawnCandidates(kind);
            for(int n=0;n<candidates.Length;n++)
            {
                var p=transform.TransformPoint(candidates[spawnCursor++%candidates.Length]);
                if(!physics.Raycast(p+Vector3.up*.3f,Vector3.down,out var hit,.8f,(1<<Layers.Block)|1,QueryTriggerInteraction.Ignore)||hit.normal.y<.8f)continue;
                p=hit.point+Vector3.up*.06f;
                if(physics.OverlapCapsule(p+Vector3.up*.2f,p+Vector3.up*.9f,.18f,clearance,(1<<Layers.Block)|(1<<Layers.Surface)|1,QueryTriggerInteraction.Ignore)==0)return p;
            }
            throw new InvalidOperationException("No supported clear spawn remains on "+kind+".");
        }
        [Serializable] sealed class State {public int version=1,kind;}
        public string SaveLayout()=>Prefix+JsonUtility.ToJson(new State{kind=(int)kind});
        public bool LoadLayout(string text,int expectedBlocks,Pose origin,Vector3 scale)
        {
            if(text==null||!text.StartsWith(Prefix,StringComparison.Ordinal)||!Finite(origin.position)||!Finite(scale)||(scale-Vector3.one).sqrMagnitude>.000001f)return false;
            float length=Quaternion.Dot(origin.rotation,origin.rotation);if(!Finite(length)||Mathf.Abs(length-1)>.001f)return false;
            if(Vector3.Dot(origin.rotation*Vector3.up,Vector3.up)<.999f)return false;
            State state;try{state=JsonUtility.FromJson<State>(text.Substring(Prefix.Length));}catch(ArgumentException){return false;}
            if(state==null||state.version!=1||!Enum.IsDefined(typeof(ConceptMapKind),state.kind))return false;
            var incoming=(ConceptMapKind)state.kind;if(ConceptMapLayout.Build(incoming).Count!=expectedBlocks)return false;
            kind=incoming;transform.SetPositionAndRotation(origin.position,origin.rotation);transform.localScale=scale;Rebuild();return true;
        }
        static bool Finite(float n)=>!float.IsNaN(n)&&!float.IsInfinity(n);
        static bool Finite(Vector3 p)=>Finite(p.x)&&Finite(p.y)&&Finite(p.z);
        public void SetReplica(bool value)
        {
            replica=value;
            for(int i=0;i<bodies.Count;i++)if(bodies[i])
            {var body=bodies[i];body.collisionDetectionMode=CollisionDetectionMode.Discrete;body.isKinematic=value||fixedBodies[i];body.useGravity=!fixedBodies[i];body.interpolation=value||fixedBodies[i]?RigidbodyInterpolation.None:RigidbodyInterpolation.Interpolate;}
        }
        void FixedUpdate()
        {
            if(replica||Time.time<nextCleanup||(Services.Session is IWorldAuthority a&&!a.HasWorldAuthority))return;
            nextCleanup=Time.time+.25f;
            for(int i=0;i<bodies.Count;i++)
            {
                var body=bodies[i];if(fixedBodies[i]||!body||!body.gameObject.activeSelf)continue;
                var shapes=body.GetComponent<MayhemStructuralMember>().Shapes;var bounds=shapes[0].bounds;
                foreach(var shape in shapes)bounds.Encapsulate(shape.bounds);
                if(bounds.max.y>WaterHeight(bounds.center)-.2f){submerged.Remove(i);continue;}
                if(!submerged.TryGetValue(i,out float began))submerged[i]=Time.time;
                else if(Time.time-began>=3)body.gameObject.SetActive(false);
            }
        }
        static void Dispose(UnityEngine.Object obj){if(Application.isPlaying)Destroy(obj);else DestroyImmediate(obj);}
        void OnDestroy(){if(ownedPalette!=null)foreach(var material in ownedPalette)if(material)Dispose(material);}
    }
}
