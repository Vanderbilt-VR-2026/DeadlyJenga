using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyArt.Marina
{
    public enum MarinaFinish { Ivory, Stone, Wood, DarkWood, Coral, Teal, Red, Green, Purple, Leaf, LeafLight, Sand, Rock, Metal, Brass, Glow, CreamFabric, MintFabric, CoralFabric, Glass }

    // Static art is combined by finish. The lobby receives a separate simplified solid mesh.
    public sealed class MarinaMesh : IDisposable
    {
        sealed class Batch
        {
            public readonly List<Vector3> vertices=new(),normals=new();
            public readonly List<Vector2> uv=new();
            public readonly List<int> indices=new();
            public int Vertex(Vector3 p,Vector3 n,Vector2 t){int i=vertices.Count;vertices.Add(p);normals.Add(n);uv.Add(t);return i;}
            public void Triangle(int a,int b,int c,Vector3 normal)
            {if(Vector3.Dot(Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]),normal)<0)(b,c)=(c,b);indices.Add(a);indices.Add(b);indices.Add(c);}
        }
        readonly Dictionary<MarinaFinish,Batch> batches=new();
        readonly Batch collision=new();
        readonly List<Mesh> meshes=new();
        readonly List<Material> materials=new();
        public int RenderTriangles {get;private set;}
        public int CollisionTriangles=>collision.indices.Count/3;
        public Mesh CollisionMesh {get;private set;}
        Batch Get(MarinaFinish finish){if(!batches.TryGetValue(finish,out var b))batches[finish]=b=new Batch();return b;}
        static void Quad(Batch b,Vector3 a,Vector3 c,Vector3 d,Vector3 e,Vector3 n)
        {
            Vector3 u=(c-a).normalized,v=(d-a).normalized;
            int s=b.Vertex(a,n,new Vector2(Vector3.Dot(a,u),Vector3.Dot(a,v))*.25f);
            b.Vertex(c,n,new Vector2(Vector3.Dot(c,u),Vector3.Dot(c,v))*.25f);
            b.Vertex(d,n,new Vector2(Vector3.Dot(d,u),Vector3.Dot(d,v))*.25f);
            b.Vertex(e,n,new Vector2(Vector3.Dot(e,u),Vector3.Dot(e,v))*.25f);
            b.Triangle(s,s+1,s+2,n);b.Triangle(s+1,s+3,s+2,n);
        }
        public void Box(Vector3 center,Vector3 size,MarinaFinish finish,float bevel=.07f,bool solid=false,Quaternion? rotation=null)
        {
            var b=Get(finish);var q=rotation??Quaternion.identity;Vector3 half=size*.5f;
            float r=Mathf.Min(bevel,Mathf.Min(half.x,Mathf.Min(half.y,half.z))*.8f);
            void Face(Vector3 normal,Vector3 u,Vector3 v)
            {
                float hu=Vector3.Dot(half,new Vector3(Mathf.Abs(u.x),Mathf.Abs(u.y),Mathf.Abs(u.z)));
                float hv=Vector3.Dot(half,new Vector3(Mathf.Abs(v.x),Mathf.Abs(v.y),Mathf.Abs(v.z)));
                float hn=Vector3.Dot(half,new Vector3(Mathf.Abs(normal.x),Mathf.Abs(normal.y),Mathf.Abs(normal.z)));
                float[] xs={-hu,-hu+r*.293f,-hu+r,hu-r,hu-r*.293f,hu};
                float[] ys={-hv,-hv+r*.293f,-hv+r,hv-r,hv-r*.293f,hv};
                if(r<=.002f||Mathf.Min(size.x,Mathf.Min(size.y,size.z))<.02f)
                {
                    Vector3 a=center+q*(normal*hn-u*hu-v*hv),c=center+q*(normal*hn+u*hu-v*hv),d=center+q*(normal*hn-u*hu+v*hv),e=center+q*(normal*hn+u*hu+v*hv);
                    Quad(b,a,c,d,e,q*normal);if(solid)Quad(collision,a,c,d,e,q*normal);return;
                }
                if(Mathf.Min(size.x,Mathf.Min(size.y,size.z))<.25f){xs=new[]{-hu,-hu+r,hu-r,hu};ys=new[]{-hv,-hv+r,hv-r,hv};}
                int nx=xs.Length,ny=ys.Length,start=b.vertices.Count;
                for(int y=0;y<ny;y++)for(int x=0;x<nx;x++)
                {
                    Vector3 p=normal*hn+u*xs[x]+v*ys[y],inner=new(Mathf.Clamp(p.x,-half.x+r,half.x-r),Mathf.Clamp(p.y,-half.y+r,half.y-r),Mathf.Clamp(p.z,-half.z+r,half.z-r));
                    Vector3 n=(p-inner).normalized;
                    b.Vertex(center+q*(inner+n*r),q*n,new Vector2(xs[x],ys[y])*.28f);
                }
                for(int y=0;y<ny-1;y++)for(int x=0;x<nx-1;x++){int i=start+y*nx+x;b.Triangle(i,i+1,i+nx,q*normal);b.Triangle(i+1,i+nx+1,i+nx,q*normal);}
                if(solid)Quad(collision,center+q*(normal*hn-u*hu-v*hv),center+q*(normal*hn+u*hu-v*hv),center+q*(normal*hn-u*hu+v*hv),center+q*(normal*hn+u*hu+v*hv),q*normal);
            }
            Face(Vector3.up,Vector3.right,Vector3.forward);Face(Vector3.down,Vector3.right,Vector3.forward);
            Face(Vector3.forward,Vector3.right,Vector3.up);Face(Vector3.back,Vector3.right,Vector3.up);
            Face(Vector3.right,Vector3.forward,Vector3.up);Face(Vector3.left,Vector3.forward,Vector3.up);
        }
        public void Ellipsoid(Vector3 center,Vector3 radius,MarinaFinish finish,Quaternion? rotation=null,int rings=9,int sides=16,bool solid=false)
        {
            var b=Get(finish);var q=rotation??Quaternion.identity;int start=b.vertices.Count,firstIndex=b.indices.Count;
            for(int y=0;y<=rings;y++)for(int x=0;x<=sides;x++)
            {
                float a=x*Mathf.PI*2/sides,t=y*Mathf.PI/rings;Vector3 n=new(Mathf.Sin(t)*Mathf.Cos(a),Mathf.Cos(t),Mathf.Sin(t)*Mathf.Sin(a));
                Vector3 normal=new Vector3(n.x/radius.x,n.y/radius.y,n.z/radius.z).normalized;
                b.Vertex(center+q*Vector3.Scale(n,radius),q*normal,new Vector2(x/(float)sides,y/(float)rings));
            }
            for(int y=0;y<rings;y++)for(int x=0;x<sides;x++){int i=start+y*(sides+1)+x;var n=(b.normals[i]+b.normals[i+1]+b.normals[i+sides+1]).normalized;b.Triangle(i,i+1,i+sides+1,n);b.Triangle(i+1,i+sides+2,i+sides+1,n);}
            if(solid)
            {
                int offset=collision.vertices.Count;
                for(int i=start;i<b.vertices.Count;i++)collision.Vertex(b.vertices[i],b.normals[i],b.uv[i]);
                for(int i=firstIndex;i<b.indices.Count;i++)collision.indices.Add(offset+b.indices[i]-start);
            }
        }
        public void ArcSlab(Vector3 center,Vector2 inner,Vector2 outer,float height,float from,float to,MarinaFinish finish,bool solid=true,int segments=48)
        {
            var b=Get(finish);
            Vector3 P(float angle,Vector2 r,float y){float a=angle*Mathf.Deg2Rad;return center+new Vector3(Mathf.Sin(a)*r.x,y,Mathf.Cos(a)*r.y);}
            for(int i=0;i<segments;i++)
            {
                float a=Mathf.Lerp(from,to,i/(float)segments),d=Mathf.Lerp(from,to,(i+1f)/segments);
                Vector3 i0=P(a,inner,0),i1=P(d,inner,0),o0=P(a,outer,0),o1=P(d,outer,0),up=Vector3.up*height;
                void Q(Vector3 v0,Vector3 v1,Vector3 v2,Vector3 v3,Vector3 n){Quad(b,v0,v1,v2,v3,n);if(solid)Quad(collision,v0,v1,v2,v3,n);}
                Q(i0+up,i1+up,o0+up,o1+up,Vector3.up);Q(o0,o1,i0,i1,Vector3.down);
                Vector3 outward=Vector3.ProjectOnPlane((o0+o1)*.5f-center,Vector3.up).normalized;
                Q(o0,o1,o0+up,o1+up,outward);Q(i1,i0,i1+up,i0+up,-outward);
                if(i==0)Q(i0,o0,i0+up,o0+up,(i0-i1).normalized);
                if(i==segments-1)Q(o1,i1,o1+up,i1+up,(i1-i0).normalized);
            }
        }
        public void Disk(Vector3 center,float radius,float height,MarinaFinish finish,bool solid=true,int sides=48)
        {ArcSlab(center,Vector2.zero,Vector2.one*radius,height,0,360,finish,solid,sides);}
        public void Tube(IList<Vector3> points,float radius,MarinaFinish finish,int sides=10,bool solid=false,float endRadius=-1)
        {
            var b=Get(finish);int start=b.vertices.Count;
            for(int i=0;i<points.Count;i++)
            {
                var tangent=(points[Mathf.Min(i+1,points.Count-1)]-points[Mathf.Max(0,i-1)]).normalized;
                var u=Vector3.Cross(tangent,Mathf.Abs(tangent.y)>.95f?Vector3.forward:Vector3.up).normalized;var v=Vector3.Cross(tangent,u);
                float r=endRadius>=0?Mathf.Lerp(radius,endRadius,i/(float)(points.Count-1)):radius;
                for(int j=0;j<=sides;j++){float a=j*Mathf.PI*2/sides;var n=u*Mathf.Cos(a)+v*Mathf.Sin(a);b.Vertex(points[i]+n*r,n,new Vector2(j/(float)sides,i*.15f));}
            }
            for(int i=0;i<points.Count-1;i++)for(int j=0;j<sides;j++)
            {
                int n=start+i*(sides+1)+j;Vector3 normal=(b.normals[n]+b.normals[n+1]).normalized;
                b.Triangle(n,n+1,n+sides+1,normal);b.Triangle(n+1,n+sides+2,n+sides+1,normal);
                if(solid)Quad(collision,b.vertices[n],b.vertices[n+1],b.vertices[n+sides+1],b.vertices[n+sides+2],normal);
            }
            for(int end=0;end<2;end++)
            {
                int row=start+(end==0?0:points.Count-1)*(sides+1);var c=points[end==0?0:points.Count-1];var n=(points[end==0?0:points.Count-1]-points[end==0?1:points.Count-2]).normalized;
                for(int j=0;j<sides;j++)Quad(b,c,b.vertices[row+j],c,b.vertices[row+j+1],n);
            }
        }
        public void Beam(Vector3 a,Vector3 b,float radius,MarinaFinish finish,bool solid=false)=>Tube(new[]{a,b},radius,finish,10,solid);
        public void Leaf(Vector3 root,Vector3 direction,float length,float width,float arch,MarinaFinish finish)
        {
            var b=Get(finish);var side=Vector3.Cross(Vector3.up,direction).normalized;int start=b.vertices.Count;
            for(int i=0;i<=6;i++)
            {
                float t=i/6f,w=Mathf.Sin(t*Mathf.PI)*width;var p=root+direction*length*t+Vector3.up*(Mathf.Sin(t*Mathf.PI)*arch-.4f*t*t*length);
                Vector3 n=(Vector3.up-direction*.2f).normalized;
                b.Vertex(p-side*w,n,new Vector2(0,t));b.Vertex(p+Vector3.up*w*.16f,n,new Vector2(.5f,t));b.Vertex(p+side*w,n,new Vector2(1,t));
            }
            for(int i=0;i<6;i++)for(int j=0;j<2;j++){int a=start+i*3+j;b.Triangle(a,a+1,a+3,Vector3.up);b.Triangle(a+1,a+4,a+3,Vector3.up);b.indices.Add(a+3);b.indices.Add(a+1);b.indices.Add(a);b.indices.Add(a+3);b.indices.Add(a+4);b.indices.Add(a+1);}
        }
        public void CurvedRoof(Vector3 center,float width,float depth,float rise,MarinaFinish finish)
        {
            var b=Get(finish);const int nx=40,nz=12;int start=b.vertices.Count;
            Vector3 P(float x,float z)=>center+new Vector3(x*width*.5f,rise*Mathf.Pow(Mathf.Max(0,1-x*x),.62f)-.12f*z*z,z*depth*.5f);
            for(int iz=0;iz<=nz;iz++)for(int ix=0;ix<=nx;ix++)
            {
                float x=ix*2f/nx-1,z=iz*2f/nz-1;var p=P(x,z);
                var dx=P(Mathf.Clamp(x+.002f,-1,1),z)-P(Mathf.Clamp(x-.002f,-1,1),z);var dz=P(x,z+.002f)-P(x,z-.002f);
                b.Vertex(p,Vector3.Cross(dz,dx).normalized,new Vector2(ix/(float)nx*3,iz/(float)nz*2));
            }
            for(int z=0;z<nz;z++)for(int x=0;x<nx;x++){int i=start+z*(nx+1)+x;b.Triangle(i,i+1,i+nx+1,Vector3.up);b.Triangle(i+1,i+nx+2,i+nx+1,Vector3.up);Quad(collision,b.vertices[i],b.vertices[i+1],b.vertices[i+nx+1],b.vertices[i+nx+2],Vector3.up);Quad(collision,b.vertices[i]-Vector3.up*.14f,b.vertices[i+1]-Vector3.up*.14f,b.vertices[i+nx+1]-Vector3.up*.14f,b.vertices[i+nx+2]-Vector3.up*.14f,Vector3.down);Quad(Get(MarinaFinish.Ivory),b.vertices[i]-Vector3.up*.14f,b.vertices[i+1]-Vector3.up*.14f,b.vertices[i+nx+1]-Vector3.up*.14f,b.vertices[i+nx+2]-Vector3.up*.14f,Vector3.down);}
            for(int side=-1;side<=1;side+=2)
            {
                var line=new List<Vector3>();
                for(int i=0;i<=nx;i++){var p=P(i*2f/nx-1,side);line.Add(p);}
                Tube(line,.12f,MarinaFinish.Ivory,10);
            }
            for(int rib=0;rib<=4;rib++)
            {
                float z=Mathf.Lerp(-1,1,rib/4f);var line=new List<Vector3>();for(int i=0;i<=32;i++)line.Add(P(i/16f-1,z)-Vector3.up*.20f);
                Tube(line,.13f,MarinaFinish.Wood,10);
            }
            for(int panel=1;panel<8;panel++)
            {
                float x=panel/4f-1;var line=new List<Vector3>();for(int i=0;i<=nz;i++)line.Add(P(x,i*2f/nz-1)+Vector3.up*.012f);
                Tube(line,.018f,MarinaFinish.Ivory,6);
            }
        }
        public void Flush(Transform root)
        {
            foreach(var entry in batches)
            {
                var mesh=Make(entry.Value,"Marina ÃƒÆ’Ã¢â‚¬Å¡· "+entry.Key);meshes.Add(mesh);RenderTriangles+=mesh.triangles.Length/3;
                var go=new GameObject(mesh.name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;
                var material=MaterialFor(entry.Key);materials.Add(material);go.GetComponent<MeshRenderer>().sharedMaterial=material;
            }
            CollisionMesh=Make(collision,"Marina walkable architecture");meshes.Add(CollisionMesh);batches.Clear();
        }
        static Mesh Make(Batch b,string name)
        {var m=new Mesh{name=name,indexFormat=IndexFormat.UInt32};m.SetVertices(b.vertices);m.SetNormals(b.normals);m.SetUVs(0,b.uv);m.SetTriangles(b.indices,0);m.RecalculateBounds();return m;}
        public static Material MaterialFor(MarinaFinish finish)
        {
            Color[] colors={new(1,.91f,.77f),new(1,.91f,.81f),Color.white,new(.32f,.13f,.047f),new(1,.25f,.32f),new(.10f,.47f,.44f),new(.95f,.045f,.06f),new(.075f,.79f,.29f),new(.57f,.12f,.85f),new(.075f,.28f,.09f),new(.22f,.52f,.095f),new(.80f,.63f,.40f),new(.53f,.49f,.42f),new(.10f,.16f,.19f),new(.69f,.40f,.12f),new(1,.74f,.34f),new(.93f,.77f,.56f),new(.35f,.66f,.58f),new(.95f,.37f,.32f),new(.15f,.56f,.64f,.19f)};
            var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Marina "+finish,enableInstancing=true};m.SetColor("_BaseColor",colors[(int)finish]);
            float gloss=finish==MarinaFinish.Coral||finish==MarinaFinish.Red||finish==MarinaFinish.Green||finish==MarinaFinish.Purple ? .78f:finish==MarinaFinish.Wood ? .48f:finish==MarinaFinish.Stone ? .72f:finish>=MarinaFinish.CreamFabric ? .16f:.32f;
            m.SetFloat("_Smoothness",gloss);m.SetFloat("_Metallic",finish==MarinaFinish.Metal||finish==MarinaFinish.Brass ? .72f:.02f);
            if(finish==MarinaFinish.Wood||finish==MarinaFinish.DarkWood)m.SetTexture("_BaseMap",Resources.Load<Texture2D>("MayhemMarina/MarinaWood"));
            if(finish==MarinaFinish.Stone||finish==MarinaFinish.Ivory)m.SetTexture("_BaseMap",Resources.Load<Texture2D>("MayhemMarina/MarinaStone"));
            if(finish==MarinaFinish.Glass)
            {
                m.SetFloat("_Surface",1);m.SetFloat("_ZWrite",0);m.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);m.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);m.SetFloat("_Smoothness",.88f);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.SetOverrideTag("RenderType","Transparent");m.renderQueue=3000;
            }
            if(finish==MarinaFinish.Glow){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",new Color(1,.56f,.16f)*2.0f);}
            return m;
        }
        public void Dispose(){foreach(var m in meshes)if(m)UnityEngine.Object.Destroy(m);foreach(var m in materials)if(m)UnityEngine.Object.Destroy(m);meshes.Clear();materials.Clear();}
    }
}
