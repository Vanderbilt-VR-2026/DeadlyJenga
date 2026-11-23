using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CastlePrototype
{
    // Bake the piping, shingles and windows into the owning biscuit's mesh.
    internal sealed class GingerbreadMesh
    {
        readonly List<Vector3> vertices = new();
        readonly List<Vector2> uv = new();
        readonly SortedDictionary<int, List<int>> faces = new();
        internal void Face(int color, params Vector3[] points)
        {
            if (!faces.TryGetValue(color, out var indices)) faces[color] = indices = new();
            int start = vertices.Count;
            Vector3 right = (points[1] - points[0]).normalized;
            Vector3 normal = Vector3.Cross(right, points[2] - points[0]).normalized;
            Vector3 up = Vector3.Cross(normal, right);
            foreach (var point in points)
            {
                vertices.Add(point);
                uv.Add(new Vector2(Vector3.Dot(point,right), Vector3.Dot(point,up)) * .6f);
            }
            for (int i=1; i<points.Length-1; i++) indices.AddRange(new[]{start,start+i,start+i+1});
        }
        internal void Box(int color, Vector3 at, Vector3 size, Quaternion rotation = default)
        {
            if (rotation == default) rotation = Quaternion.identity;
            Vector3 P(int x,int y,int z) => at + rotation * Vector3.Scale(size*.5f,new Vector3(x,y,z));
            Face(color,P(-1,-1,-1),P(-1,1,-1),P(1,1,-1),P(1,-1,-1));
            Face(color,P(1,-1,1),P(1,1,1),P(-1,1,1),P(-1,-1,1));
            Face(color,P(-1,1,-1),P(-1,1,1),P(1,1,1),P(1,1,-1));
            Face(color,P(-1,-1,1),P(-1,-1,-1),P(1,-1,-1),P(1,-1,1));
            Face(color,P(-1,-1,1),P(-1,1,1),P(-1,1,-1),P(-1,-1,-1));
            Face(color,P(1,-1,-1),P(1,1,-1),P(1,1,1),P(1,-1,1));
        }
        internal void Ring(int color,Vector3 at,float bottom,float top,float height,int sides=12)
        {
            Vector3 P(int i,float radius,float h)
            {
                float angle=i*Mathf.PI*2/sides;
                return at + new Vector3(Mathf.Cos(angle)*radius,h,Mathf.Sin(angle)*radius);
            }
            for(int i=0;i<sides;i++)
            {
                Face(color,P(i,bottom,0),P(i,top,height),P(i+1,top,height),P(i+1,bottom,0));
                if(top>0) Face(color,at+Vector3.up*height,P(i+1,top,height),P(i,top,height));
            }
        }
        internal void Line(int color,Vector3 a,Vector3 b,float width)
        {
            Vector3 delta=b-a;
            Box(color,(a+b)*.5f,new Vector3(width,width,delta.magnitude+width*.25f),Quaternion.LookRotation(delta));
        }
        internal Mesh Mount(Transform root, Material[] palette)
        {
            var mesh=new Mesh{name=root.name,indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.subMeshCount=faces.Count;
            var materials=new List<Material>();
            foreach(var entry in faces)
            {
                mesh.SetTriangles(entry.Value,materials.Count); materials.Add(palette[entry.Key]);
            }
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var visual=new GameObject("Visual");visual.transform.SetParent(root,false);
            visual.AddComponent<MeshFilter>().sharedMesh=mesh;
            visual.AddComponent<MeshRenderer>().sharedMaterials=materials.ToArray();
            return mesh;
        }
    }
}
