using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastleBots
{
    // Sample real solid faces, including compound rooms and convex flights.
    public static class BotGeometrySamples
    {
        sealed class BodySamples
        {
            public Collider[] shapes;
            public readonly List<Collider> discovered=new();
            public float discoverAt;
            public readonly List<ShapeState> states=new();
            public readonly List<Vector3> points=new();
            public int version;public bool dirty=true;
        }
        struct ShapeState
        {
            public Collider shape;
            public bool active;
            public Matrix4x4 matrix;
            public Vector3 center,size;
            public Mesh mesh;
            public Bounds meshBounds;
            public int vertices;
            public bool Same(ShapeState other)=>shape==other.shape&&active==other.active&&matrix.Equals(other.matrix)&&center.Equals(other.center)&&size.Equals(other.size)&&mesh==other.mesh&&meshBounds.Equals(other.meshBounds)&&vertices==other.vertices;
        }
        static readonly Dictionary<Rigidbody,BodySamples> cache=new();
        readonly struct FaceSample
        {
            public readonly Vector3 normal,point;
            public FaceSample(Vector3 normal,Vector3 point){this.normal=normal;this.point=point;}
        }
        sealed class BoxFace {public Vector3 normal;public readonly List<Vector3> points=new();}
        static readonly Dictionary<(Vector3 center,Vector3 size,Vector3Int pattern),List<BoxFace>> boxSamples=new();
        static readonly Dictionary<Mesh,List<FaceSample>> meshSamples=new();
        static IWorldQuery world;
        static Rigidbody first;
        static int version;
        public static int MeshRevision {get;private set;}
        public static int Rebuilds {get;private set;}
        public static int Reuses {get;private set;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){cache.Clear();meshSamples.Clear();boxSamples.Clear();world=null;first=null;version=0;MeshRevision=0;Rebuilds=Reuses=0;}
        static BodySamples Entry(Rigidbody body)
        {
            var current=Services.World;var head=current!=null&&current.Blocks.Count>0?current.Blocks[0]:null;
            if(!ReferenceEquals(world,current)||first!=head){Reset();world=current;first=head;}
            if(!cache.TryGetValue(body,out var entry))cache[body]=entry=new BodySamples();
            if(entry.shapes==null||Time.time>=entry.discoverAt)
            {
                entry.discoverAt=Time.time+1;
                body.GetComponentsInChildren(true,entry.discovered);var found=entry.discovered;
                bool same=entry.shapes!=null&&entry.shapes.Length==found.Count;
                if(same)for(int i=0;i<found.Count;i++)if(entry.shapes[i]!=found[i]){same=false;break;}
                if(!same){entry.shapes=found.ToArray();entry.states.Clear();entry.dirty=true;}
            }
            return entry;
        }
        static Collider[] Shapes(Rigidbody body)=>Entry(body).shapes;
        // Call after editing mesh vertices in place; replacement meshes and collider edits are detected automatically.
        public static void Invalidate(Mesh mesh){MeshRevision++;if(mesh){meshSamples.Remove(mesh);}foreach(var e in cache.Values){e.states.Clear();e.dirty=true;}}
        public static int Version(Rigidbody body)=>cache.TryGetValue(body,out var entry)?entry.version:0;
        static ShapeState State(Collider shape,Rigidbody body)
        {
            var state=new ShapeState{shape=shape};
            if(!shape)return state;
            state.active=shape.enabled&&!shape.isTrigger&&shape.gameObject.activeInHierarchy&&shape.attachedRigidbody==body;
            state.matrix=shape.transform.localToWorldMatrix;
            if(shape is BoxCollider box){state.center=box.center;state.size=box.size;}
            if(shape is MeshCollider collider&&collider.sharedMesh){state.mesh=collider.sharedMesh;state.vertices=state.mesh.vertexCount;state.meshBounds=state.mesh.bounds;}
            return state;
        }
        public static bool Near(Rigidbody body,Vector3 feet,float above,float below,float radius)
        {
            foreach(var shape in Shapes(body))
            {
                if(!shape||!shape.enabled||shape.isTrigger||!shape.gameObject.activeInHierarchy||shape.attachedRigidbody!=body)continue;
                var bounds=shape.bounds;
                if(bounds.min.y>feet.y+above||bounds.max.y<feet.y-below)continue;
                if(Vector3.ProjectOnPlane(bounds.ClosestPoint(feet)-feet,Vector3.up).sqrMagnitude<=radius*radius)return true;
            }
            return false;
        }
        public static void Fill(Rigidbody body,List<Vector3> points)
        {
            var entry=Entry(body);bool changed=entry.dirty||entry.states.Count!=entry.shapes.Length;
            for(int i=0;i<entry.shapes.Length;i++)
            {
                var state=State(entry.shapes[i],body);
                if(i>=entry.states.Count){entry.states.Add(state);changed=true;}
                else if(!entry.states[i].Same(state))
                {
                    var old=entry.states[i];
                    if(state.mesh&&state.mesh==old.mesh&&(state.vertices!=old.vertices||!state.meshBounds.Equals(old.meshBounds)))meshSamples.Remove(state.mesh);
                    entry.states[i]=state;changed=true;
                }
            }
            if(changed){Sample(body,entry.points,entry.shapes);entry.version=++version;entry.dirty=false;Rebuilds++;}else Reuses++;
            points.Clear();points.AddRange(entry.points);
        }
        static void Sample(Rigidbody body,List<Vector3> points,Collider[] shapes)
        {
            points.Clear();
            foreach(var shape in shapes)
            {
                if(!shape||!shape.enabled||shape.isTrigger||!shape.gameObject.activeInHierarchy||shape.attachedRigidbody!=body)continue;
                var anchor=shape.transform;var matrix=anchor.localToWorldMatrix;var rotation=anchor.rotation;
                if(shape is BoxCollider box)
                {
                    var size=box.size;var center=box.center;var scale=anchor.lossyScale;var pattern=Vector3Int.zero;
                    for(int axis=0;axis<3;axis++)
                    {
                        int a=(axis+1)%3,b=(axis+2)%3;int longAxis=size[a]*scale[a]>=size[b]*scale[b]?a:b;int shortAxis=longAxis==a?b:a;
                        if(size[longAxis]*scale[longAxis]>size[shortAxis]*scale[shortAxis]*1.5f)pattern[axis]=(longAxis+1)*32+Mathf.Clamp(Mathf.CeilToInt(size[longAxis]*scale[longAxis]*.8f/4.5f),2,24);
                    }
                    var key=(center,size,pattern);
                    if(!boxSamples.TryGetValue(key,out var faces))
                    {
                        faces=new List<BoxFace>(6);
                        for(int axis=0;axis<3;axis++)for(int sign=-1;sign<=1;sign+=2)
                        {
                            var face=new BoxFace();face.normal[axis]=sign;faces.Add(face);
                            int a=(axis+1)%3,b=(axis+2)%3;
                            for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)
                            {var p=center;p[axis]+=size[axis]*.5f*sign;p[a]+=size[a]*x*.37f;p[b]+=size[b]*z*.37f;face.points.Add(p);}
                            if(pattern[axis]!=0)
                            {
                                int longAxis=pattern[axis]/32-1,steps=pattern[axis]%32;
                                for(int step=0;step<=steps;step++)
                                {var p=center;p[axis]+=size[axis]*.5f*sign;p[longAxis]+=size[longAxis]*Mathf.Lerp(-.4f,.4f,step/(float)steps);face.points.Add(p);}
                            }
                        }
                        boxSamples[key]=faces;
                    }
                    foreach(var face in faces)if((rotation*face.normal).y>=.4f)
                        foreach(var point in face.points)points.Add(matrix.MultiplyPoint3x4(point));
                }
                else if(shape is MeshCollider mesh&&mesh.sharedMesh)
                {
                    if(!meshSamples.TryGetValue(mesh.sharedMesh,out var faces))
                    {
                        var vertices=mesh.sharedMesh.vertices;var triangles=mesh.sharedMesh.triangles;faces=new List<FaceSample>();
                        for(int i=0;i<triangles.Length;i+=3)
                        {
                            var a=vertices[triangles[i]];var b=vertices[triangles[i+1]];var c=vertices[triangles[i+2]];
                            var normal=Vector3.Cross(b-a,c-a).normalized;
                            faces.Add(new FaceSample(normal,(a+b+c)/3));faces.Add(new FaceSample(normal,a*.6f+b*.2f+c*.2f));
                            faces.Add(new FaceSample(normal,a*.2f+b*.6f+c*.2f));faces.Add(new FaceSample(normal,a*.2f+b*.2f+c*.6f));
                        }
                        meshSamples[mesh.sharedMesh]=faces;
                    }
                    var normalMatrix=matrix.inverse.transpose;float winding=matrix.determinant<0?-1:1;
                    foreach(var face in faces)
                        if((normalMatrix.MultiplyVector(face.normal)*winding).normalized.y>=.4f)points.Add(matrix.MultiplyPoint3x4(face.point));
                }
            }
        }
    }
}
