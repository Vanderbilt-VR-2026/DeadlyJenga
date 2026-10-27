using System.Collections.Generic;
using System.Text;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CastlePrototype
{
    // One physical section, exact open compound collision and a single batched model.
    public sealed class MayhemStructuralMember : MonoBehaviour,IClimbable,IPushable,IHazardMotion,IBlockOwnership
    {
        public int District {get;private set;}
        public string Purpose {get;private set;}
        public int OwnerTeam {get;private set;}=-1;
        public void SetOwner(int team,Color color){OwnerTeam=team;}
        public bool CanClimb=>isActiveAndEnabled;
        public float Grip=>1;
        public Transform Anchor=>transform;
        public Rigidbody Body {get;private set;}
        public Transform Visual {get;private set;}
        public Vector3 HazardVelocity=>Body?Body.linearVelocity:Vector3.zero;
        public readonly List<Collider> Shapes=new();
        readonly List<string> collisionMeshes=new();
        Mesh model;
        string modelKey;
        static readonly Dictionary<string,(Mesh mesh,int users)> models=new();
        static readonly Dictionary<string,(Mesh mesh,int users)> contacts=new();
        public static int CachedModelCount=>models.Count;
        public static int CachedCollisionMeshCount=>contacts.Count;
        public void Build(MayhemModule module,Material[] palette,PhysicsMaterial friction)
        {Build(module,palette,friction,false);}
        public void Build(MayhemModule module,Material[] palette,PhysicsMaterial friction,bool compactAlignedBoxes)
        {
            District=module.district;Purpose=module.purpose;Body=GetComponent<Rigidbody>();
            var inverse=Quaternion.Inverse(module.shapes[0].rotation);
            var vertices=new List<Vector3>();var triangles=new List<int>[palette.Length];
            var key=new StringBuilder();
            for(int i=0;i<triangles.Length;i++)triangles[i]=new List<int>();
            float volume=0;
            for(int i=0;i<module.shapes.Count;i++)
            {
                var s=module.shapes[i];var center=inverse*s.center;var rotation=inverse*s.rotation;
                key.Append(center.ToString("R")).Append(s.size.ToString("R")).Append(rotation.ToString("R")).Append(s.finish);
                if(s.vertices!=null)foreach(var v in s.vertices)key.Append(v.ToString("R"));
                GameObject go=gameObject;
                bool separate=i>0&&(!compactAlignedBoxes||s.vertices!=null||Quaternion.Angle(rotation,Quaternion.identity)>.001f);
                if(separate){go=new GameObject("Contact shape");go.layer=Layers.Block;go.transform.SetParent(transform,false);go.transform.localPosition=center;go.transform.localRotation=rotation;}
                Collider collider;
                if(s.vertices!=null)
                {
                    var v=(Vector3[])s.vertices.Clone();
                    for(int n=0;n<v.Length;n++)v[n]=(i==0?center:Vector3.zero)+(i==0?rotation:Quaternion.identity)*v[n];
                    var signature=new StringBuilder();foreach(var point in v)signature.Append(point.ToString("R"));string contactKey=signature.ToString();
                    Mesh mesh;
                    if(contacts.TryGetValue(contactKey,out var cached)&&cached.mesh){mesh=cached.mesh;contacts[contactKey]=(mesh,cached.users+1);}
                    else{mesh=new Mesh{name="Shared convex structural shape"};mesh.vertices=v;mesh.triangles=MayhemStructureLayout.CubeTriangles;mesh.RecalculateBounds();contacts[contactKey]=(mesh,1);}
                    collisionMeshes.Add(contactKey);
                    var shape=go.AddComponent<MeshCollider>();shape.convex=true;shape.sharedMesh=mesh;collider=shape;
                }
                else
                {var box=go.AddComponent<BoxCollider>();box.size=s.size;box.center=separate?Vector3.zero:center;collider=box;}
                collider.sharedMaterial=friction;collider.excludeLayers=1<<Layers.Surface;
                Shapes.Add(collider);Climbables.Register(collider,this);
                if(s.vertices==null)volume+=s.size.x*s.size.y*s.size.z;
                else
                {
                    float signed=0;var volumeIndices=MayhemStructureLayout.CubeTriangles;
                    for(int n=0;n<volumeIndices.Length;n+=3)signed+=Vector3.Dot(s.vertices[volumeIndices[n]],Vector3.Cross(s.vertices[volumeIndices[n+1]],s.vertices[volumeIndices[n+2]]))/6;
                    volume+=Mathf.Abs(signed);
                }
                if(s.vertices==null){BeveledBox(s.size,center,rotation,s.finish%triangles.Length,vertices,triangles);continue;}
                int start=vertices.Count;var source=s.vertices;
                // Duplicate faces for crisp normals, without giving every art piece a GameObject.
                var indices=MayhemStructureLayout.CubeTriangles;
                for(int n=0;n<indices.Length;n++)
                {vertices.Add(center+rotation*source[indices[n]]);triangles[s.finish%triangles.Length].Add(start+n);}
            }
            Body.mass=module.mass>0?module.mass:Mathf.Clamp(volume*1.2f,8,450);
            Body.ResetCenterOfMass();Body.ResetInertiaTensor();
            Visual=new GameObject("Visual").transform;Visual.SetParent(transform,false);
            modelKey=key.ToString();
            if(models.TryGetValue(modelKey,out var shared)&&shared.mesh){model=shared.mesh;models[modelKey]=(model,shared.users+1);}
            else
            {
                model=new Mesh{name="Mayhem section model",indexFormat=IndexFormat.UInt32};model.SetVertices(vertices);model.subMeshCount=triangles.Length;
                for(int i=0;i<triangles.Length;i++)model.SetTriangles(triangles[i],i);
                model.RecalculateNormals();model.RecalculateBounds();models[modelKey]=(model,1);
            }
            Visual.gameObject.AddComponent<MeshFilter>().sharedMesh=model;
            Visual.gameObject.AddComponent<MeshRenderer>().sharedMaterials=palette;
        }
        public void Push(Vector3 force,GameObject source)
        {if(Body&&!Body.isKinematic){Body.WakeUp();Body.AddForce(force,ForceMode.VelocityChange);}GameEvents.RaisePushed(gameObject,force);}
        void OnEnable(){foreach(var shape in Shapes)if(shape)Climbables.Register(shape,this);}
        void OnDisable(){foreach(var shape in Shapes)if(shape)Climbables.Unregister(shape);}
        static void BeveledBox(Vector3 size,Vector3 center,Quaternion rotation,int finish,List<Vector3> vertices,List<int>[] triangles)
        {
            var half=size*.5f;float bevel=Mathf.Min(.08f,Mathf.Min(half.x,Mathf.Min(half.y,half.z))*.22f);
            void Face(Vector3[] points,Vector3 normal)
            {
                int start=vertices.Count;bool reverse=Vector3.Dot(Vector3.Cross(points[1]-points[0],points[2]-points[0]),normal)<0;
                foreach(var p in points)vertices.Add(center+rotation*p);
                for(int n=1;n<points.Length-1;n++){triangles[finish].Add(start);triangles[finish].Add(start+(reverse?n+1:n));triangles[finish].Add(start+(reverse?n:n+1));}
            }
            for(int axis=0;axis<3;axis++)for(int sign=-1;sign<=1;sign+=2)
            {
                int a=(axis+1)%3,b=(axis+2)%3;var points=new Vector3[4];int n=0;
                foreach(var uv in new[]{new Vector2(-1,-1),new Vector2(1,-1),new Vector2(1,1),new Vector2(-1,1)})
                {var p=Vector3.zero;p[axis]=half[axis]*sign;p[a]=(half[a]-bevel)*uv.x;p[b]=(half[b]-bevel)*uv.y;points[n++]=p;}
                var normal=Vector3.zero;normal[axis]=sign;Face(points,normal);
            }
            for(int axis=0;axis<3;axis++)for(int sa=-1;sa<=1;sa+=2)for(int sb=-1;sb<=1;sb+=2)
            {
                int a=(axis+1)%3,b=(axis+2)%3;var points=new Vector3[4];
                for(int n=0;n<4;n++){var p=Vector3.zero;p[axis]=(half[axis]-bevel)*(n<2?-1:1);p[a]=(half[a]-(n==0||n==3?0:bevel))*sa;p[b]=(half[b]-(n==0||n==3?bevel:0))*sb;points[n]=p;}
                var normal=Vector3.zero;normal[a]=sa;normal[b]=sb;Face(points,normal);
            }
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
            {
                var points=new Vector3[3];for(int axis=0;axis<3;axis++){var p=half-Vector3.one*bevel;p[axis]=half[axis];points[axis]=Vector3.Scale(p,new Vector3(x,y,z));}Face(points,new Vector3(x,y,z));
            }
        }
        void OnDestroy()
        {
            foreach(var shape in Shapes)if(shape)Climbables.Unregister(shape);
            if(modelKey!=null&&models.TryGetValue(modelKey,out var shared))
            {if(shared.users<=1){models.Remove(modelKey);if(shared.mesh)Destroy(shared.mesh);}else models[modelKey]=(shared.mesh,shared.users-1);}
            foreach(var key in collisionMeshes)if(contacts.TryGetValue(key,out var cached))
            {if(cached.users<=1){contacts.Remove(key);if(cached.mesh)Destroy(cached.mesh);}else contacts[key]=(cached.mesh,cached.users-1);}
        }
    }
}
