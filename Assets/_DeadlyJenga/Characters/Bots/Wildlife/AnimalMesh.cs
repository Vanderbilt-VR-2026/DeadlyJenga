using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.Wildlife
{
    internal sealed class AnimalMesh
    {
        readonly List<Vector3> vertices=new List<Vector3>(),normals=new List<Vector3>();
        readonly List<Vector2> uv=new List<Vector2>();
        readonly List<int> triangles=new List<int>();
        void Vertex(Vector3 p,Vector3 n,Vector2 tex){vertices.Add(p);normals.Add(n);uv.Add(tex);}
        void Quad(int a,int b,int c,int d){triangles.AddRange(new[]{a,b,c,a,c,d});}
        public void Sphere(Vector3 center,Vector3 radii,Quaternion? rotation=null)
        {
            int start=vertices.Count;const int rings=12,sides=20;var q=rotation??Quaternion.identity;
            for(int y=0;y<=rings;y++)for(int x=0;x<=sides;x++)
            {
                float v=(float)y/rings,u=(float)x/sides,t=v*Mathf.PI,a=u*Mathf.PI*2;
                var unit=new Vector3(Mathf.Sin(t)*Mathf.Cos(a),Mathf.Cos(t),Mathf.Sin(t)*Mathf.Sin(a));
                var normal=new Vector3(unit.x/radii.x,unit.y/radii.y,unit.z/radii.z).normalized;
                Vertex(center+q*Vector3.Scale(radii,unit),q*normal,new Vector2(u,v));
            }
            for(int y=0;y<rings;y++)for(int x=0;x<sides;x++){int a=start+y*(sides+1)+x;Quad(a,a+1,a+sides+2,a+sides+1);}
        }
        public void Cone(Vector3 from,Vector3 to,float r0,float r1,int sides)
        {
            var q=Quaternion.FromToRotation(Vector3.up,(to-from).normalized);float length=(to-from).magnitude;int start=vertices.Count;
            for(int y=0;y<2;y++)for(int i=0;i<=sides;i++)
            {
                float a=i*Mathf.PI*2/sides;var radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                var normal=(radial+Vector3.up*((r0-r1)/Mathf.Max(.001f,length))).normalized;
                Vertex((y==0?from:to)+q*radial*(y==0?r0:r1),q*normal,new Vector2((float)i/sides,y));
            }
            for(int i=0;i<sides;i++)Quad(start+i,start+sides+1+i,start+sides+2+i,start+i+1);
            for(int y=0;y<2;y++)
            {
                int cap=vertices.Count;var normal=q*(y==0?Vector3.down:Vector3.up);var center=y==0?from:to;
                Vertex(center,normal,Vector2.zero);
                for(int i=0;i<=sides;i++){float a=i*Mathf.PI*2/sides;Vertex(center+q*new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*(y==0?r0:r1),normal,Vector2.zero);}
                for(int i=0;i<sides;i++){triangles.Add(cap);triangles.Add(cap+1+(y==0?i:i+1));triangles.Add(cap+1+(y==0?i+1:i));}
            }
        }
        public void Beam(Vector3 a,Vector3 b,float radius)=>Cone(a,b,radius,radius,12);
        public Mesh Mesh(string name)
        {
            var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;
        }
    }
}
