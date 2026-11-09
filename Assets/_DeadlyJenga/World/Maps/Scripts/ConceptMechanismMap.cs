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
        PhysicsMaterial connectorSlide;
        readonly List<Mesh> finishes=new();
        Bounds authored;
        bool replica,collisionOwned,oldIgnored;
        int spawnCursor;
        float nextCleanup;
        const string Prefix="concept-mechanisms-v2:";
        public int Revision {get;private set;}
        public Transform Root=>transform;
        public IReadOnlyList<Rigidbody> Blocks=>bodies;
        public IReadOnlyList<MayhemModule> Authored=>modules;
        public bool UsesOriginalColliders=>true;
        public Vector3 Center=>transform.position;
        public float TopHeight=>PlayableBounds.max.y;
        public float Radius=>new Vector2(PlayableBounds.extents.x,PlayableBounds.extents.z).magnitude;
        public float StepHeight=>.75f;
        public int FootprintVersion=>9200+(int)kind;
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
            ReleaseFinishes();
            bodies.Clear();modules.Clear();fixedBodies.Clear();submerged.Clear();spawnCursor=0;
            modules.AddRange(layout);authored=ConceptMapLayout.Bounds(layout);
            generated=new GameObject("Concept island geometry").transform;generated.SetParent(transform,false);
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
                var member=go.AddComponent<MayhemStructuralMember>();
                member.Build(m,palette!=null&&palette.Length>=4&&Array.TrueForAll(palette,material=>material)?palette:ownedPalette,friction);
                if(kind==ConceptMapKind.BreakContactBorough&&m.purpose.StartsWith("Connector ",StringComparison.Ordinal))
                    member.Shapes[0].sharedMaterial=ConnectorSlide();
                BuildBodyFinish(member.Visual,m);
                bodies.Add(body);fixedBodies.Add(anchored);lookup.Add(m,body);
            }
            foreach(var m in layout)if(m.hingeSupport!=null)
            {
                var body=lookup[m];var joint=body.gameObject.AddComponent<HingeJoint>();joint.connectedBody=lookup[m.hingeSupport];
                joint.autoConfigureConnectedAnchor=false;var pivot=transform.TransformPoint(m.pivot);
                joint.anchor=body.transform.InverseTransformPoint(pivot);joint.connectedAnchor=joint.connectedBody.transform.InverseTransformPoint(pivot);
                bool falling=kind==ConceptMapKind.CollapseCreatedRoute;
                joint.axis=body.transform.InverseTransformDirection(falling?transform.forward:transform.up);
                joint.useLimits=true;
                joint.limits=new JointLimits{min=falling?0:-65,max=falling?28:65,bounciness=0,contactDistance=1};
                joint.enableCollision=false;
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
            var size=ConceptMapLayout.FoundationSize(kind);
            var half=size*.5f;
            var floor=new GameObject("Continuous island and outer bypass");
            floor.transform.SetParent(generated,false);
            var top=new FinishMesh();
            var cliff=new FinishMesh();
            const int cornerSteps=8;
            const float radius=2.2f;
            var outline=new List<Vector3>();
            for(int corner=0;corner<4;corner++)
            {
                float angle=(corner+.5f)*Mathf.PI*.5f;
                var center=new Vector3(Mathf.Sign(Mathf.Cos(angle))*(half.x-radius),.4f,Mathf.Sign(Mathf.Sin(angle))*(half.y-radius));
                for(int i=0;i<=cornerSteps;i++)
                {
                    angle=(corner+(float)i/cornerSteps)*Mathf.PI*.5f;
                    outline.Add(center+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius));
                }
            }
            var centerTop=new Vector3(0,.4f,0);
            for(int i=0;i<outline.Count;i++)
            {
                var a=outline[i];var b=outline[(i+1)%outline.Count];
                top.Triangle(centerTop,b,a,3);
                var middleA=a+new Vector3(a.x*.025f,-.42f,a.z*.025f);
                var middleB=b+new Vector3(b.x*.025f,-.42f,b.z*.025f);
                var bottomA=middleA+new Vector3(-a.x*.035f,-1.15f,-a.z*.035f);
                var bottomB=middleB+new Vector3(-b.x*.035f,-1.15f,-b.z*.035f);
                cliff.Quad(a,b,middleB,middleA,5);
                cliff.Quad(middleA,middleB,bottomB,bottomA,6+i%2);
                float span=Vector3.Distance(a,b);
                int blocks=Mathf.CeilToInt(span/1.45f);
                var tangent=(b-a).normalized;
                var outward=Vector3.Cross(Vector3.up,tangent);
                var turn=Quaternion.LookRotation(outward,Vector3.up);
                for(int tile=0;tile<blocks;tile++)
                {
                    var ledge=Vector3.Lerp(a,b,(tile+.5f)/blocks)+outward*.19f;
                    ledge.y=-.54f-(tile%3)*.08f;
                    cliff.Box(ledge,new Vector3(span/blocks*.89f,.94f,.52f),turn,6+(i+tile)%2);
                    ledge.y=.18f;
                    cliff.Box(ledge,new Vector3(span/blocks*.92f,.31f,.55f),turn,5);
                }
            }
            var mesh=top.Create("Continuous island collision");
            finishes.Add(mesh);
            floor.AddComponent<MeshCollider>().sharedMesh=mesh;
            floor.GetComponent<MeshCollider>().sharedMaterial=friction;
            if(kind==ConceptMapKind.BreakContactBorough)
                foreach(float x in new[]{-5f,5f})
                {
                    var bearing=new GameObject("Independent connector bearing");
                    bearing.transform.SetParent(floor.transform,false);
                    bearing.transform.localPosition=new Vector3(x,.665f,0);
                    var support=bearing.AddComponent<BoxCollider>();
                    support.size=new Vector3(1.5f,.53f,2.2f);
                    support.sharedMaterial=ConnectorSlide();
                    top.Box(bearing.transform.localPosition,new Vector3(1.5f,.53f,2.2f),Quaternion.identity,4);
                }
            AddFinish(floor.transform,top,"Island ground");
            AddFinish(floor.transform,cliff,"Molded candy shoreline");

            // A single mesh supplies the paved route and the color changes at its edge.
            var paving=new FinishMesh();
            int columns=Mathf.CeilToInt(size.x/1.6f),rows=Mathf.CeilToInt(size.y/1.6f);
            float dx=size.x/columns,dz=size.y/rows;
            for(int row=0;row<rows;row++)for(int column=0;column<columns;column++)
            {
                float x=-half.x+(column+.5f)*dx,z=-half.y+(row+.5f)*dz;
                float edge=Mathf.Min(half.x-Mathf.Abs(x),half.y-Mathf.Abs(z));
                float qx=Mathf.Max(0,Mathf.Abs(x)+dx*.5f-half.x+radius),qz=Mathf.Max(0,Mathf.Abs(z)+dz*.5f-half.y+radius);
                if(qx*qx+qz*qz>radius*radius+.01f)continue;
                bool bypass=edge<3.25f;
                bool featureLane=kind switch
                {
                    ConceptMapKind.MovingShelter=>Mathf.Abs(z+4.2f)<1.05f&&Mathf.Abs(x)<10f,
                    ConceptMapKind.PeelAwayScreen=>Mathf.Abs(x)<1.4f,
                    ConceptMapKind.CollapseCreatedRoute=>Mathf.Abs(x+6.5f)<1.35f&&z<0,
                    ConceptMapKind.PivotingCauseway=>Mathf.Abs(z-6.5f)<1.6f,
                    ConceptMapKind.RefugeExchange=>Mathf.Abs(x-6)<1.8f,
                    _=>Mathf.Abs(z+5.2f)<1.1f
                };
                int finish=bypass||featureLane?((row+column)%5==0?3:5):((row+column*3)%9==0?1:(row+column)%3==0?4:2);
                paving.Box(new Vector3(x,.405f,z),new Vector3(dx-.035f,.012f,dz-.035f),Quaternion.identity,finish);
            }
            AddFinish(floor.transform,paving,"Tiled bypass and courtyard");
        }
        void BuildBodyFinish(Transform visual,MayhemModule module)
        {
            var panels=new FinishMesh();
            var inverse=Quaternion.Inverse(module.shapes[0].rotation);
            foreach(var shape in module.shapes)
            {
                if(shape.vertices!=null)
                {
                    var v=shape.vertices;
                    var rotationOfRamp=inverse*shape.rotation;
                    var origin=inverse*shape.center+Vector3.up*.009f;
                    int strips=Mathf.CeilToInt(Vector3.Distance(v[2],v[6])/1.1f);
                    for(int strip=0;strip<strips;strip++)
                    {
                        float start=(strip+.025f)/strips,end=(strip+.975f)/strips;
                        Vector3 left=Vector3.Lerp(v[3],v[7],start),right=Vector3.Lerp(v[2],v[6],start);
                        Vector3 farLeft=Vector3.Lerp(v[3],v[7],end),farRight=Vector3.Lerp(v[2],v[6],end);
                        panels.Quad(origin+rotationOfRamp*left,origin+rotationOfRamp*farLeft,
                            origin+rotationOfRamp*farRight,origin+rotationOfRamp*right,strip%5==0?5:1);
                    }
                    continue;
                }
                var size=shape.size;
                var center=inverse*shape.center;
                var rotation=inverse*shape.rotation;
                if(size.x>2&&size.z>1.3f)
                    PanelGrid(panels,center,rotation,size.x,size.z,size.y*.5f,shape.finish,true);
                if(size.y>1.35f&&size.x>1.25f)
                {
                    PanelGrid(panels,center,rotation,size.x,size.y,size.z*.5f,shape.finish,false);
                    PanelGrid(panels,center,rotation*Quaternion.Euler(0,180,0),size.x,size.y,size.z*.5f,shape.finish,false);
                }
                if(size.y>1.35f&&size.z>1.25f)
                {
                    PanelGrid(panels,center,rotation*Quaternion.Euler(0,90,0),size.z,size.y,size.x*.5f,shape.finish,false);
                    PanelGrid(panels,center,rotation*Quaternion.Euler(0,-90,0),size.z,size.y,size.x*.5f,shape.finish,false);
                }
            }
            if(module.purpose=="Fixed causeway pivot")
                panels.Cylinder(new Vector3(0,1.24f,-1.12f),.38f,.27f,5);
            if(panels.HasFaces)AddFinish(visual,panels,"Candy block panel seams");
        }
        static void PanelGrid(FinishMesh mesh,Vector3 center,Quaternion rotation,float width,float length,float offset,int finish,bool top)
        {
            int columns=Mathf.CeilToInt(width/1.45f),rows=Mathf.CeilToInt(length/1.55f);
            float dx=width/columns,dz=length/rows;
            for(int row=0;row<rows;row++)for(int column=0;column<columns;column++)
            {
                float x=-width*.5f+(column+.5f)*dx,z=-length*.5f+(row+.5f)*dz;
                var p=top?new Vector3(x,offset+.009f,z):new Vector3(x,z,offset+.009f);
                var size=top?new Vector3(dx-.055f,.016f,dz-.055f):new Vector3(dx-.055f,dz-.055f,.016f);
                int shade=(column+row*3)%7==0?(finish==0?4:finish==1?5:1):finish;
                mesh.Box(center+rotation*p,size,rotation,shade);
            }
        }
        void AddFinish(Transform parent,FinishMesh builder,string label)
        {
            var mesh=builder.Create(label);finishes.Add(mesh);
            var visual=new GameObject(label,typeof(MeshFilter),typeof(MeshRenderer));
            visual.transform.SetParent(parent,false);
            visual.GetComponent<MeshFilter>().sharedMesh=mesh;
            visual.GetComponent<MeshRenderer>().sharedMaterials=ownedPalette;
        }
        void ReleaseFinishes()
        {
            foreach(var mesh in finishes)if(mesh)Dispose(mesh);
            finishes.Clear();
        }
        sealed class FinishMesh
        {
            const int Colors=8;
            readonly List<Vector3> vertices=new();
            readonly List<int>[] indices=Array.ConvertAll(new int[Colors],_=>new List<int>());
            public bool HasFaces=>vertices.Count>0;
            public void Triangle(Vector3 a,Vector3 b,Vector3 c,int finish)
            {
                int first=vertices.Count;
                vertices.Add(a);vertices.Add(b);vertices.Add(c);
                indices[finish].Add(first);indices[finish].Add(first+1);indices[finish].Add(first+2);
            }
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,int finish)
            {Triangle(a,b,c,finish);Triangle(a,c,d,finish);}
            public void Box(Vector3 center,Vector3 size,Quaternion rotation,int finish)
            {
                var points=MayhemStructureLayout.Cube(size);
                var triangles=MayhemStructureLayout.CubeTriangles;
                for(int i=0;i<points.Length;i++)points[i]=center+rotation*points[i];
                for(int i=0;i<triangles.Length;i+=3)
                    Triangle(points[triangles[i]],points[triangles[i+1]],points[triangles[i+2]],finish);
            }
            public void Cylinder(Vector3 center,float radius,float height,int finish)
            {
                var low=center-Vector3.up*(height*.5f);
                var high=center+Vector3.up*(height*.5f);
                const int sides=20;
                for(int i=0;i<sides;i++)
                {
                    float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;
                    var u=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius;
                    var v=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b))*radius;
                    Triangle(high,high+v,high+u,finish);
                    Quad(high+u,high+v,low+v,low+u,finish);
                }
            }
            public Mesh Create(string label)
            {
                var mesh=new Mesh{name=label,subMeshCount=Colors};
                mesh.SetVertices(vertices);
                for(int i=0;i<Colors;i++)mesh.SetTriangles(indices[i],i,false);
                mesh.RecalculateBounds();mesh.RecalculateNormals();return mesh;
            }
        }
        PhysicsMaterial ConnectorSlide()
        {
            if(!connectorSlide)
                connectorSlide=new PhysicsMaterial("Candy connector slide")
                {staticFriction=.025f,dynamicFriction=.025f,frictionCombine=PhysicsMaterialCombine.Minimum};
            return connectorSlide;
        }
        void EnsurePalette()
        {
            if(ownedPalette!=null)return;
            var shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            if(!shader)throw new InvalidOperationException("A candy surface shader is required.");
            var colors=new[]{new Color(.37f,.78f,.71f),new Color(.92f,.52f,.57f),new Color(.64f,.49f,.82f),new Color(.87f,.70f,.49f),new Color(.68f,.56f,.82f),new Color(.99f,.72f,.29f),new Color(.39f,.30f,.52f),new Color(.51f,.38f,.62f)};
            if(kind==ConceptMapKind.MovingShelter||kind==ConceptMapKind.RefugeExchange)
                colors[1]=new Color(.46f,.83f,.72f);
            ownedPalette=new Material[colors.Length];
            for(int i=0;i<colors.Length;i++)
            {
                ownedPalette[i]=new Material(shader){name="Concept candy finish "+i,color=colors[i]};
                ownedPalette[i].SetFloat("_Smoothness",i<3?.55f:.33f);
            }
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
        [Serializable] sealed class State {public int version=2,kind;}
        public string SaveLayout()=>Prefix+JsonUtility.ToJson(new State{kind=(int)kind});
        public bool LoadLayout(string text,int expectedBlocks,Pose origin,Vector3 scale)
        {
            if(text==null||!text.StartsWith(Prefix,StringComparison.Ordinal)||!Finite(origin.position)||!Finite(scale)||(scale-Vector3.one).sqrMagnitude>.000001f)return false;
            float length=Quaternion.Dot(origin.rotation,origin.rotation);if(!Finite(length)||Mathf.Abs(length-1)>.001f)return false;
            if(Vector3.Dot(origin.rotation*Vector3.up,Vector3.up)<.999f)return false;
            State state;try{state=JsonUtility.FromJson<State>(text.Substring(Prefix.Length));}catch(ArgumentException){return false;}
            if(state==null||state.version!=2||!Enum.IsDefined(typeof(ConceptMapKind),state.kind))return false;
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
        void OnDestroy(){ReleaseFinishes();if(connectorSlide)Dispose(connectorSlide);if(ownedPalette!=null)foreach(var material in ownedPalette)if(material)Dispose(material);}
    }
}
