using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CandyPlayers
{
    // Baked once by the editor installer. A smooth union gives fingers and palm one continuous skin;
    // the existing tracked joints remain the deformation bones. No voxel work occurs during tracking.
    public static class CandySmoothHandMesh
    {
        struct Segment {public Vector3 a,b;public float radius;public int bone;}
        public static Mesh Build(Transform root,Transform[] bones,int side,CandyHandSkeleton? reference=null,float fingerRadius=.0085f,float blend=.0035f,float palmLength=.03f)
        {
            int palm=side==0?4:7;var skeleton=reference??CandyHandSkeleton.Rest(side==0);
            var segments=new List<Segment>();
            for(int finger=0;finger<5;finger++)for(int joint=0;joint<3;joint++)
                segments.Add(new Segment{a=skeleton.Joint(finger*4+joint),b=skeleton.Joint(finger*4+joint+1),radius=(finger==0?.012f:fingerRadius)-joint*.0012f,bone=14+side*15+finger*3+joint});
            float DistanceSegment(Vector3 p,Segment s)
            {Vector3 d=s.b-s.a;return Vector3.Distance(p,s.a+d*Mathf.Clamp01(Vector3.Dot(p-s.a,d)/Mathf.Max(.000001f,d.sqrMagnitude)));}
            float Palm(Vector3 p)
            {
                Vector3 q=p-new Vector3(0,0,.025f),r=new Vector3(.044f,.025f,palmLength);
                float k0=Vector3.Scale(q,new Vector3(1/r.x,1/r.y,1/r.z)).magnitude;
                float k1=Vector3.Scale(q,new Vector3(1/(r.x*r.x),1/(r.y*r.y),1/(r.z*r.z))).magnitude;
                return k1<.00001f?-.025f:k0*(k0-1)/k1;
            }
            float Field(Vector3 p)
            {
                float palmDistance=Palm(p),d=palmDistance;
                // Blend each digit into the palm, never into its neighbour. A global
                // smooth union joins close fingers with thin sheets that stretch on splay.
                for(int finger=0;finger<5;finger++)
                {
                    float digit=DistanceSegment(p,segments[finger*3])-segments[finger*3].radius;
                    for(int joint=1;joint<3;joint++)
                    {var s=segments[finger*3+joint];float segment=DistanceSegment(p,s)-s.radius;float merge=Mathf.Clamp01(.5f+.5f*(segment-digit)/blend);digit=Mathf.Lerp(segment,digit,merge)-blend*merge*(1-merge);}
                    float h=Mathf.Clamp01(.5f+.5f*(digit-palmDistance)/blend);
                    d=Mathf.Min(d,Mathf.Lerp(digit,palmDistance,h)-blend*h*(1-h));
                }
                return d;
            }
            const float step=.0035f;Vector3 minimum=new Vector3(-.085f,-.039f,-.039f);
            int nx=50,ny=24,nz=54;
            if(reference.HasValue)
            {
                Vector3 maximum=new Vector3(.055f,.035f,.085f);minimum=new Vector3(-.055f,-.035f,-.035f);
                for(int i=0;i<20;i++){minimum=Vector3.Min(minimum,skeleton.Joint(i)-Vector3.one*.018f);maximum=Vector3.Max(maximum,skeleton.Joint(i)+Vector3.one*.018f);}
                nx=Mathf.CeilToInt((maximum.x-minimum.x)/step);ny=Mathf.CeilToInt((maximum.y-minimum.y)/step);nz=Mathf.CeilToInt((maximum.z-minimum.z)/step);
            }
            int width=nx+1,height=ny+1,count=width*height*(nz+1);
            var positions=new Vector3[count];var values=new float[count];var gradients=new Vector3[count];
            int Index(int x,int y,int z)=>(z*height+y)*width+x;
            for(int z=0;z<=nz;z++)for(int y=0;y<=ny;y++)for(int x=0;x<=nx;x++)
            {int id=Index(x,y,z);positions[id]=minimum+new Vector3(x,y,z)*step;values[id]=Field(positions[id]);}
            for(int z=0;z<=nz;z++)for(int y=0;y<=ny;y++)for(int x=0;x<=nx;x++)
            {int id=Index(x,y,z);gradients[id]=new Vector3(values[Index(Mathf.Min(nx,x+1),y,z)]-values[Index(Mathf.Max(0,x-1),y,z)],values[Index(x,Mathf.Min(ny,y+1),z)]-values[Index(x,Mathf.Max(0,y-1),z)],values[Index(x,y,Mathf.Min(nz,z+1))]-values[Index(x,y,Mathf.Max(0,z-1))]).normalized;}
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var weights=new List<BoneWeight>();var triangles=new List<int>();var edges=new Dictionary<ulong,int>();
            Matrix4x4 matrix=root.worldToLocalMatrix*bones[palm].localToWorldMatrix;
            int Edge(int a,int b)
            {
                ulong key=((ulong)(uint)Mathf.Min(a,b)<<32)|(uint)Mathf.Max(a,b);if(edges.TryGetValue(key,out int existing))return existing;
                float t=values[a]/(values[a]-values[b]);Vector3 p=Vector3.Lerp(positions[a],positions[b],t);
                int id=vertices.Count;edges.Add(key,id);vertices.Add(matrix.MultiplyPoint3x4(p));normals.Add(matrix.MultiplyVector(Vector3.Lerp(gradients[a],gradients[b],t)).normalized);uv.Add(new Vector2(p.x/.16f+.5f,p.z/.18f));
                int[] ids={palm,-1,-1,-1};float[] influences={1/Mathf.Pow(Mathf.Max(.005f,Mathf.Abs(Palm(p))+.012f),4),0,0,0};
                int closestFinger=0;float closest=float.PositiveInfinity;
                for(int i=0;i<segments.Count;i++){float d=DistanceSegment(p,segments[i]);if(d<closest){closest=d;closestFinger=i/3;}}
                foreach(var s in segments)
                {
                    if((s.bone-14-side*15)/3!=closestFinger)continue;
                    float influence=1/Mathf.Pow(Mathf.Max(.004f,DistanceSegment(p,s)),4);
                    for(int i=0;i<4;i++)if(influence>influences[i])
                    {for(int j=3;j>i;j--){influences[j]=influences[j-1];ids[j]=ids[j-1];}influences[i]=influence;ids[i]=s.bone;break;}
                }
                float sum=influences[0]+influences[1]+influences[2]+influences[3];
                weights.Add(new BoneWeight{boneIndex0=ids[0],weight0=influences[0]/sum,boneIndex1=Mathf.Max(0,ids[1]),weight1=influences[1]/sum,boneIndex2=Mathf.Max(0,ids[2]),weight2=influences[2]/sum,boneIndex3=Mathf.Max(0,ids[3]),weight3=influences[3]/sum});return id;
            }
            void Triangle(int a,int b,int c)
            {if(Vector3.Dot(Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]),normals[a]+normals[b]+normals[c])<0){int swap=b;b=c;c=swap;}triangles.Add(a);triangles.Add(b);triangles.Add(c);}
            var inside=new int[4];var outside=new int[4];
            void Tetra(int a,int b,int c,int d)
            {
                int ni=0,no=0;
                void Classify(int id){if(values[id]<0)inside[ni++]=id;else outside[no++]=id;}
                Classify(a);Classify(b);Classify(c);Classify(d);if(ni==0||ni==4)return;
                if(ni==1){Triangle(Edge(inside[0],outside[0]),Edge(inside[0],outside[1]),Edge(inside[0],outside[2]));return;}
                if(ni==3){Triangle(Edge(outside[0],inside[0]),Edge(outside[0],inside[1]),Edge(outside[0],inside[2]));return;}
                int p0=Edge(inside[0],outside[0]),p1=Edge(inside[0],outside[1]),p2=Edge(inside[1],outside[0]),p3=Edge(inside[1],outside[1]);Triangle(p0,p1,p2);Triangle(p1,p3,p2);
            }
            for(int z=0;z<nz;z++)for(int y=0;y<ny;y++)for(int x=0;x<nx;x++)
            {
                int a=Index(x,y,z),b=Index(x+1,y,z),c=Index(x,y+1,z),d=Index(x+1,y+1,z),e=Index(x,y,z+1),f=Index(x+1,y,z+1),g=Index(x,y+1,z+1),h=Index(x+1,y+1,z+1);
                Tetra(a,b,d,h);Tetra(a,d,c,h);Tetra(a,c,g,h);Tetra(a,g,e,h);Tetra(a,e,f,h);Tetra(a,f,b,h);
            }
            var mesh=new Mesh{name="Continuous candy hand"};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.boneWeights=weights.ToArray();mesh.SetTriangles(triangles,0);return mesh;
        }
    }
}
