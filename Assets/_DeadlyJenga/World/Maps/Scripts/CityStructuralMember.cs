using System.Collections.Generic;
using System.Text;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadlyJenga.CastlePrototype
{
    // A manufactured solid section. Other sections remain independently simulated.
    [DefaultExecutionOrder(-100)]
    public sealed class CityStructuralMember : MonoBehaviour,IClimbable,IPushable,IHazardMotion
    {
        sealed class Group {public Transform root;public int members;}
        static readonly Dictionary<int,List<Group>> groups=new();
        static readonly Dictionary<string,Mesh> meshes=new();
        readonly List<Collider> shapes=new(),copies=new();
        Group group;
        Rigidbody body;
        Transform visual,packedSurface;
        Vector3 lastPosition;
        Quaternion lastRotation;
        bool synced;
        float sweepRadius;
        CityDebrisCleanup cityDebris;
        public void RegisterCityDebris(CityDebrisCleanup value){cityDebris=value;}
        void OnCollisionEnter(Collision collision){cityDebris?.Impact(body,collision);}
        public IReadOnlyList<Collider> Shapes=>shapes;
        public IReadOnlyList<Collider> SurvivorShapes=>copies;
        public bool CanClimb=>isActiveAndEnabled&&!(cityDebris&&cityDebris.IsVisual(body));
        public float Grip=>1;
        public Transform Anchor=>transform;
        public Rigidbody Body=>body;
        public Transform Visual=>visual;
        public Vector3 HazardVelocity=>cityDebris&&cityDebris.IsVisual(body)?cityDebris.Velocity(body):body.linearVelocity;
        public void Push(Vector3 force,GameObject source){if(cityDebris&&cityDebris.TryPush(body,force)){GameEvents.RaisePushed(gameObject,force);return;}if(!body.isKinematic){if(force.sqrMagnitude>36)body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;body.AddForce(force,ForceMode.VelocityChange);}GameEvents.RaisePushed(gameObject,force);}
        public Bounds CollisionBounds
        {get{var bounds=shapes[0].bounds;for(int i=1;i<shapes.Count;i++)bounds.Encapsulate(shapes[i].bounds);return bounds;}}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics(){groups.Clear();foreach(var m in meshes.Values)if(m)Destroy(m);meshes.Clear();}
        public void Build(CityPart[] parts,Material[] materials,PhysicsMaterial friction,bool packed=false,bool meshSurface=false,bool originalCollision=false)
        {
            packed=packed||meshSurface;
            body=GetComponent<Rigidbody>();gameObject.layer=Layers.Block;
            Physics.IgnoreLayerCollision(Layers.Survivor,Layers.Block,true);
            for(int layer=0;layer<32;layer++)Physics.IgnoreLayerCollision(Layers.Surface,layer,layer!=Layers.Survivor);
            float volume=0;
            foreach(var part in parts)
            {
                if(!part.Solid)continue;
                var child=packed&&!part.Ramp||shapes.Count==0?gameObject:new GameObject("Solid member surface");child.layer=Layers.Block;
                if(child!=gameObject){child.transform.SetParent(transform,false);child.transform.localPosition=part.Position;}
                Collider source;
                if(part.Ramp){var mesh=child.AddComponent<MeshCollider>();mesh.sharedMesh=RampMesh(part.Size);mesh.convex=true;source=mesh;}
                else{var box=child.AddComponent<BoxCollider>();box.size=part.Size;if(child==gameObject)box.center=part.Position;source=box;}
                source.sharedMaterial=friction;source.excludeLayers=(originalCollision?0:1<<Layers.Survivor)|(1<<Layers.Surface);shapes.Add(source);
                if(originalCollision)Climbables.Register(source,this);
                volume+=part.Size.x*(part.Ramp?.3f:part.Size.y)*part.Size.z;
            }
            sweepRadius=CollisionBounds.extents.magnitude;
            body.mass=Mathf.Max(1,volume*40/24);body.ResetCenterOfMass();body.ResetInertiaTensor();
            visual=new GameObject("Visual").transform;visual.SetParent(transform,false);
            var filter=visual.gameObject.AddComponent<MeshFilter>();filter.sharedMesh=Model(parts);
            visual.gameObject.AddComponent<MeshRenderer>().sharedMaterials=materials;
            if(Application.isPlaying&&!originalCollision)
            {
                group=Acquire(gameObject.scene);
                if(packed){packedSurface=new GameObject(gameObject.name+" packed survivor surface").transform;packedSurface.gameObject.layer=Layers.Surface;packedSurface.SetParent(group.root,false);}
                if(meshSurface&&shapes.Count>1)
                {
                    var mesh=packedSurface.gameObject.AddComponent<MeshCollider>();mesh.sharedMesh=SurfaceMesh(parts);mesh.convex=false;
                    mesh.excludeLayers=~(1<<Layers.Survivor);mesh.layerOverridePriority=10;Climbables.Register(mesh,this);copies.Add(mesh);
                }
                else foreach(var source in shapes)
                {
                    var go=packed?packedSurface.gameObject:new GameObject(gameObject.name+" surface "+copies.Count);go.layer=Layers.Surface;if(!packed)go.transform.SetParent(group.root,false);
                    Collider copy;
                    if(source is BoxCollider box){var target=go.AddComponent<BoxCollider>();target.size=box.size;target.center=box.center;copy=target;}
                    else{var target=go.AddComponent<MeshCollider>();target.sharedMesh=((MeshCollider)source).sharedMesh;target.convex=true;copy=target;}
                    copy.excludeLayers=~(1<<Layers.Survivor);copy.layerOverridePriority=10;Climbables.Register(copy,this);copies.Add(copy);
                }
                SyncSurfaces();
            }
        }
        static Group Acquire(Scene scene)
        {
            if(!groups.TryGetValue(scene.handle,out var list))groups[scene.handle]=list=new List<Group>();
            foreach(var g in list)if(g.root&&g.members<24){g.members++;return g;}
            var go=new GameObject("City shared survivor collision");SceneManager.MoveGameObjectToScene(go,scene);go.layer=Layers.Surface;
            var rb=go.AddComponent<Rigidbody>();rb.isKinematic=true;rb.useGravity=false;go.AddComponent<CitySurfaceSync>();
            var created=new Group{root=go.transform,members=1};list.Add(created);return created;
        }
        public void SyncSurfaces()
        {
            if(copies.Count==0)return;
            if(synced&&lastPosition==transform.position&&lastRotation==transform.rotation)return;
            if(packedSurface){packedSurface.SetPositionAndRotation(transform.position,transform.rotation);packedSurface.localScale=transform.lossyScale;}
            else for(int i=0;i<copies.Count;i++)if(copies[i])
            {copies[i].transform.SetPositionAndRotation(shapes[i].transform.position,shapes[i].transform.rotation);copies[i].transform.localScale=shapes[i].transform.lossyScale;}
            lastPosition=transform.position;lastRotation=transform.rotation;synced=true;CitySurfaceSync.Changed();
        }
        void Update(){SyncSurfaces();}
        void FixedUpdate()
        {
            if(!body||body.isKinematic||body.IsSleeping())return;
            var mode=body.linearVelocity.magnitude+body.angularVelocity.magnitude*sweepRadius>6?CollisionDetectionMode.ContinuousDynamic:CollisionDetectionMode.Discrete;
            if(body.collisionDetectionMode!=mode)body.collisionDetectionMode=mode;
        }
        void OnEnable(){foreach(var c in copies)if(c)c.gameObject.SetActive(true);}
        void OnDisable(){foreach(var c in copies)if(c)c.gameObject.SetActive(false);}
        void OnDestroy()
        {
            foreach(var source in shapes)if(source)Climbables.Unregister(source);
            foreach(var c in copies)if(c){Climbables.Unregister(c);if(!packedSurface)Destroy(c.gameObject);}
            if(packedSurface)Destroy(packedSurface.gameObject);
            if(group!=null&&--group.members==0&&group.root){if(groups.TryGetValue(gameObject.scene.handle,out var list))list.Remove(group);Destroy(group.root.gameObject);}
        }
        public static Mesh RampMesh(Vector3 size)
        {
            string key="ramp"+size.ToString("R");if(meshes.TryGetValue(key,out var existing)&&existing)return existing;
            var vertices=new[]{new Vector3(-size.x/2,0,-size.z/2),new Vector3(size.x/2,0,-size.z/2),new Vector3(size.x/2,size.y,size.z/2),new Vector3(-size.x/2,size.y,size.z/2),new Vector3(-size.x/2,-.3f,-size.z/2),new Vector3(size.x/2,-.3f,-size.z/2),new Vector3(size.x/2,size.y-.3f,size.z/2),new Vector3(-size.x/2,size.y-.3f,size.z/2)};
            var mesh=new Mesh{name="Solid seated flight"};mesh.vertices=vertices;
            mesh.triangles=new[]{0,3,2,0,2,1,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5};mesh.RecalculateNormals();mesh.RecalculateBounds();meshes[key]=mesh;return mesh;
        }
        static Mesh SurfaceMesh(CityPart[] parts)
        {
            var key=new StringBuilder("collision");foreach(var p in parts)if(p.Solid)key.Append(p.Position.ToString("R")).Append(p.Size.ToString("R")).Append(p.Ramp);
            string id=key.ToString();if(meshes.TryGetValue(id,out var existing)&&existing)return existing;
            var vertices=new List<Vector3>();var triangles=new List<int>();
            int[] cube={0,1,2,0,2,3,4,7,6,4,6,5,0,4,5,0,5,1,3,2,6,3,6,7,0,3,7,0,7,4,1,5,6,1,6,2};
            foreach(var p in parts)
            {
                if(!p.Solid)continue;int offset=vertices.Count;
                if(p.Ramp){var m=RampMesh(p.Size);foreach(var v in m.vertices)vertices.Add(p.Position+v);foreach(int t in m.triangles)triangles.Add(offset+t);}
                else
                {
                    var h=p.Size*.5f;
                    foreach(var v in new[]{new Vector3(-h.x,-h.y,-h.z),new Vector3(h.x,-h.y,-h.z),new Vector3(h.x,-h.y,h.z),new Vector3(-h.x,-h.y,h.z),new Vector3(-h.x,h.y,-h.z),new Vector3(h.x,h.y,-h.z),new Vector3(h.x,h.y,h.z),new Vector3(-h.x,h.y,h.z)})vertices.Add(p.Position+v);
                    foreach(int t in cube)triangles.Add(offset+t);
                }
            }
            var mesh=new Mesh{name="Exact room survivor collision"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();meshes[id]=mesh;return mesh;
        }
        static Mesh Model(CityPart[] parts)
        {
            var key=new StringBuilder();foreach(var p in parts)key.Append(p.Position.ToString("R")).Append(p.Size.ToString("R")).Append(p.Finish).Append(p.Ramp);
            string id=key.ToString();if(meshes.TryGetValue(id,out var existing)&&existing)return existing;
            var vertices=new List<Vector3>();var triangles=new List<int>[5];for(int i=0;i<5;i++)triangles[i]=new List<int>();
            foreach(var p in parts)
            {
                if(p.Ramp)
                {
                    var ramp=RampMesh(p.Size);int offset=vertices.Count;foreach(var v in ramp.vertices)vertices.Add(v+p.Position);foreach(int t in ramp.triangles)triangles[p.Finish].Add(t+offset);
                }
                else if(p.Finish==3&&!p.Solid)
                {
                    int first=vertices.Count;vertices.Add(p.Position+new Vector3(-p.Size.x/2,-p.Size.y/2,.025f));vertices.Add(p.Position+new Vector3(p.Size.x/2,-p.Size.y/2,.025f));
                    for(int i=0;i<=10;i++){float angle=i*Mathf.PI/10;vertices.Add(p.Position+new Vector3(Mathf.Cos(angle)*p.Size.x/2,p.Size.y*.15f+Mathf.Sin(angle)*p.Size.y*.35f,.025f));}
                    for(int i=1;i<12;i++){triangles[3].Add(first);triangles[3].Add(first+i);triangles[3].Add(first+i+1);}
                }
                else BevelBox(p,vertices,triangles);
            }
            var mesh=new Mesh{name="Candy structural section"};mesh.SetVertices(vertices);mesh.subMeshCount=5;
            for(int i=0;i<5;i++)mesh.SetTriangles(triangles[i],i);mesh.RecalculateNormals();mesh.RecalculateBounds();meshes[id]=mesh;return mesh;
        }
        static void BevelBox(CityPart part,List<Vector3> vertices,List<int>[] triangles)
        {
            var half=part.Size*.5f;float bevel=Mathf.Min(.09f,Mathf.Min(half.x,Mathf.Min(half.y,half.z))*.25f);
            void Polygon(Vector3[] points,Vector3 normal)
            {
                int start=vertices.Count;bool reverse=Vector3.Dot(Vector3.Cross(points[1]-points[0],points[2]-points[0]),normal)<0;
                foreach(var p in points)vertices.Add(p+part.Position);
                int finish=part.Finish==1&&normal.y>.5f?2:part.Finish;
                for(int i=1;i<points.Length-1;i++){triangles[finish].Add(start);triangles[finish].Add(start+(reverse?i+1:i));triangles[finish].Add(start+(reverse?i:i+1));}
            }
            for(int axis=0;axis<3;axis++)for(int sign=-1;sign<=1;sign+=2)
            {
                int a=(axis+1)%3,b=(axis+2)%3;var pts=new Vector3[4];int n=0;
                foreach(var uv in new[]{new Vector2(-1,-1),new Vector2(1,-1),new Vector2(1,1),new Vector2(-1,1)})
                {var v=Vector3.zero;v[axis]=half[axis]*sign;v[a]=(half[a]-bevel)*uv.x;v[b]=(half[b]-bevel)*uv.y;pts[n++]=v;}
                var normal=Vector3.zero;normal[axis]=sign;Polygon(pts,normal);
            }
            for(int axis=0;axis<3;axis++)for(int sa=-1;sa<=1;sa+=2)for(int sb=-1;sb<=1;sb+=2)
            {
                int a=(axis+1)%3,b=(axis+2)%3;var pts=new Vector3[4];
                for(int i=0;i<4;i++){var v=Vector3.zero;v[axis]=(half[axis]-bevel)*(i<2?-1:1);v[a]=(half[a]-(i==0||i==3?0:bevel))*sa;v[b]=(half[b]-(i==0||i==3?bevel:0))*sb;pts[i]=v;}
                var normal=Vector3.zero;normal[a]=sa;normal[b]=sb;Polygon(pts,normal);
            }
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
            {
                var pts=new Vector3[3];for(int axis=0;axis<3;axis++){var v=half-Vector3.one*bevel;v[axis]=half[axis];pts[axis]=Vector3.Scale(v,new Vector3(x,y,z));}
                Polygon(pts,new Vector3(x,y,z));
            }
        }
    }
}
