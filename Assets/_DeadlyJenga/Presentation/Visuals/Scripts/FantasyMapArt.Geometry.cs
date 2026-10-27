using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.Presentation
{
    public sealed partial class FantasyMapArt
    {
        sealed class ArtMesh
        {
            readonly List<Vector3> vertices=new(),normals=new();
            readonly List<Color> colors=new();
            readonly List<int>[] triangles;
            public ArtMesh(int materials=1){triangles=new List<int>[materials];for(int i=0;i<materials;i++)triangles[i]=new();}
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,int mat,Color color)
            {
                int start=vertices.Count;var n=Vector3.Cross(b-a,c-a).normalized;
                vertices.AddRange(new[]{a,b,c,d});for(int i=0;i<4;i++){normals.Add(n);colors.Add(color);}
                triangles[mat].AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
            }
            public void Box(Vector3 center,Vector3 size,Quaternion rotation,int mat=0,Color? tint=null)
            {
                var h=size*.5f;var color=tint??Color.white;
                for(int axis=0;axis<3;axis++)for(int sign=-1;sign<=1;sign+=2)
                {
                    int u=(axis+1)%3,v=(axis+2)%3;var p=new Vector3[4];int i=0;
                    foreach(var q in new[]{new Vector2(-1,-1),new Vector2(1,-1),new Vector2(1,1),new Vector2(-1,1)})
                    {var a=Vector3.zero;a[axis]=h[axis]*sign;a[u]=h[u]*q.x;a[v]=h[v]*q.y;p[i++]=center+rotation*a;}
                    if(sign<0)Quad(p[3],p[2],p[1],p[0],mat,color);else Quad(p[0],p[1],p[2],p[3],mat,color);
                }
            }
            public void Sphere(Vector3 center,Vector3 scale,int mat=0,Color? tint=null,int rings=8,int sides=12)
            {
                int start=vertices.Count;var color=tint??Color.white;
                for(int y=0;y<=rings;y++)for(int x=0;x<=sides;x++)
                {
                    float a=x*Mathf.PI*2/sides,b=y*Mathf.PI/rings;
                    var n=new Vector3(Mathf.Sin(b)*Mathf.Cos(a),Mathf.Cos(b),Mathf.Sin(b)*Mathf.Sin(a));
                    vertices.Add(center+Vector3.Scale(n,scale));normals.Add(new Vector3(n.x/scale.x,n.y/scale.y,n.z/scale.z).normalized);colors.Add(color);
                }
                for(int y=0;y<rings;y++)for(int x=0;x<sides;x++)
                {int a=start+y*(sides+1)+x,b=a+sides+1;triangles[mat].AddRange(new[]{a,b+1,b,a,a+1,b+1});}
            }
            public void Triangle(Vector3 a,Vector3 b,Vector3 c,int mat)
            {int n=vertices.Count;vertices.AddRange(new[]{a,b,c});var normal=Vector3.Cross(b-a,c-a).normalized;for(int i=0;i<3;i++){normals.Add(normal);colors.Add(Color.white);}triangles[mat].AddRange(new[]{n,n+1,n+2,n+2,n+1,n});}
            public void Star(Vector3 center,Quaternion rotation,float radius,int mat=0)
            {
                for(int i=0;i<10;i++)
                {
                    float a=i*Mathf.PI/5,b=(i+1)*Mathf.PI/5;
                    var p=new Vector3(Mathf.Sin(a),Mathf.Cos(a),0)*radius*(i%2==0?1:.45f);
                    var q=new Vector3(Mathf.Sin(b),Mathf.Cos(b),0)*radius*(i%2==0?.45f:1);
                    Triangle(center,center+rotation*p,center+rotation*q,mat);
                }
            }
            public void Beam(Vector3 a,Vector3 b,float width,int mat=0)
            {Box((a+b)*.5f,new Vector3(width,width,(b-a).magnitude),Quaternion.LookRotation(b-a),mat);}
            public void Hex(Vector3 center,Quaternion rotation,float radius,float depth,int mat=0)
            {
                for(int i=0;i<6;i++)
                {
                    float a=i*Mathf.PI/3,b=(i+1)*Mathf.PI/3;
                    var u=new Vector3(Mathf.Sin(a),Mathf.Cos(a),0);var v=new Vector3(Mathf.Sin(b),Mathf.Cos(b),0);
                    Vector3 P(Vector3 p)=>center+rotation*p;
                    float inner=radius*.83f;
                    Quad(P(u*radius),P(v*radius),P(v*inner),P(u*inner),mat,Color.white);
                    Quad(P(u*inner),P(v*inner),P(v*inner+Vector3.forward*depth),P(u*inner+Vector3.forward*depth),mat,new Color(.88f,.73f,.5f));
                    int start=vertices.Count;
                    vertices.AddRange(new[]{P(Vector3.forward*depth),P(v*inner+Vector3.forward*depth),P(u*inner+Vector3.forward*depth)});
                    for(int k=0;k<3;k++){normals.Add(rotation*Vector3.back);colors.Add(new Color(.69f,.42f,.16f));}
                    triangles[mat].AddRange(new[]{start,start+1,start+2,start+2,start+1,start});
                }
            }
            public Mesh Finish(string name)
            {
                var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetColors(colors);mesh.subMeshCount=triangles.Length;
                for(int i=0;i<triangles.Length;i++)mesh.SetTriangles(triangles[i],i);mesh.RecalculateBounds();return mesh;
            }
        }
        void BuildCloud()
        {
            var mesh=new ArtMesh();float rx=halfWidth,rz=halfDepth;
            var blue=theme==FantasyMapTheme.Carnival?new Color(.75f,.83f,1):new Color(.85f,.91f,1);
            var pink=theme==FantasyMapTheme.Carnival?new Color(1,.63f,.85f):Color.white;
            mesh.Sphere(new Vector3(0,-30,0),new Vector3(rx*.95f,26,rz*.95f),0,pink,14,24);
            for(int i=0;i<112;i++)
            {
                float a=i*2.399963f;float r=Mathf.Sqrt((i+.5f)/112);
                var p=new Vector3(Mathf.Cos(a)*rx*r,-13-(i%4)*1.7f,Mathf.Sin(a)*rz*r);
                mesh.Sphere(p,new Vector3(21+i%6*2,7+i%4,18+i%5*2),0,Color.Lerp(blue,Color.white,(i%5)*.25f));
            }
            for(int i=0;i<76;i++)
            {
                float a=i*Mathf.PI*2/76;var p=new Vector3(Mathf.Cos(a)*rx,-11.5f-i%3*.6f,Mathf.Sin(a)*rz);
                mesh.Sphere(p,new Vector3(20+i%4*3,10+i%4,19+i%5*2),0,i%3==0?pink:blue);
            }
            Model("Visual · sculpted cloud skirt",mesh,transform,new[]{cloudMaterial});
            if(texturedEnvironmentSky||(theme==FantasyMapTheme.Carnival&&texturedCarnivalSky))return;
            var distant=new ArtMesh();
            for(int i=0;i<24;i++)
            {
                float a=i*2.399963f,r=550+(i%5)*80;var p=new Vector3(Mathf.Cos(a)*r,-30+(i%4)*40,Mathf.Sin(a)*r);
                for(int n=0;n<4;n++)distant.Sphere(p+new Vector3(n*25,0,0),new Vector3(45,14+n%2*8,32),0,i%3==0?pink:Color.white);
            }
            Model("Visual · distant cloud banks",distant,transform,new[]{cloudMaterial});
        }
        void BuildHive()
        {
            var cells=new ArtMesh();float radius=Mathf.Max(430,Mathf.Max(halfWidth,halfDepth)*2.5f);int columns=62;
            for(int row=0;row<10;row++)for(int n=0;n<columns;n++)
            {
                float angle=(n+(row%2)*.5f)*Mathf.PI*2/columns;float r=radius*(1-Mathf.Max(0,row-4)*.075f);
                var p=new Vector3(Mathf.Cos(angle)*r,10+row*34,Mathf.Sin(angle)*r);
                cells.Hex(p,Quaternion.LookRotation(new Vector3(p.x,0,p.z).normalized),25,18);
            }
            Model("Visual · enclosing wax honeycomb",cells,transform,new[]{goldMaterial});
            var honey=new ArtMesh();
            for(int i=0;i<36;i++)
            {
                float a=i*2.399963f;var p=new Vector3(Mathf.Cos(a)*radius*.85f,-6,Mathf.Sin(a)*radius*.85f);
                honey.Sphere(p,new Vector3(24+i%4*4,8,20+i%5*3));
            }
            Model("Visual · distant honey pools",honey,transform,new[]{pinkMaterial});
            var bees=new ArtMesh(3);
            for(int i=0;i<12;i++)
            {
                var p=new Vector3(Mathf.Cos(i*2.4f)*(220+i%3*20),35+i%5*14,Mathf.Sin(i*2.4f)*(220+i%3*20));
                bees.Sphere(p,new Vector3(1.4f,1.4f,2.7f),0);bees.Sphere(p+Vector3.forward*.6f,new Vector3(1.48f,1.48f,.45f),1);
                bees.Sphere(p+new Vector3(-1.3f,1.4f,0),new Vector3(1.5f,.2f,2),2);bees.Sphere(p+new Vector3(1.3f,1.4f,0),new Vector3(1.5f,.2f,2),2);
            }
            Model("Visual · distant honey bees",bees,transform,new[]{goldMaterial,darkMaterial,blueMaterial});
        }
    }
}
