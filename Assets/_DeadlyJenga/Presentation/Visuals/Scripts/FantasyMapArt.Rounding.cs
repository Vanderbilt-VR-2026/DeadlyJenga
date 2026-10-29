using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.Presentation
{
    public sealed partial class FantasyMapArt
    {
        void SoftenEdges(MeshRenderer renderer,Rigidbody body)
        {
            var filter=renderer.GetComponent<MeshFilter>();
            if(!filter||!filter.sharedMesh||originalShapes.ContainsKey(filter))return;
            var source=filter.sharedMesh;
            var shapes=body.GetComponentsInChildren<Collider>();
            int expected=0;
            foreach(var shape in shapes)expected+=shape is BoxCollider?96:shape is MeshCollider?36:0;
            // Only replace the known compound box model; custom art keeps its original mesh.
            if(source.vertexCount!=expected)return;
            var finishes=new int[source.vertexCount];
            for(int sub=0;sub<source.subMeshCount;sub++)foreach(int vertex in source.GetTriangles(sub))finishes[vertex]=sub;
            var vertices=new List<Vector3>();var normals=new List<Vector3>();
            var indices=new List<int>[source.subMeshCount];for(int sub=0;sub<indices.Length;sub++)indices[sub]=new List<int>();
            int offset=0;var sourceVertices=source.vertices;var sourceNormals=source.normals;
            foreach(var shape in shapes)
            {
                if(!(shape is BoxCollider box))
                {
                    if(shape is MeshCollider)
                    {
                        int start=vertices.Count;
                        for(int i=0;i<36;i++){vertices.Add(sourceVertices[offset+i]);var n=sourceNormals[offset+i];normals.Add(n.sqrMagnitude>.5f?n:Vector3.up);}
                        for(int sub=0;sub<indices.Length;sub++)foreach(int i in source.GetTriangles(sub))if(i>=offset&&i<offset+36)indices[sub].Add(start+i-offset);
                        offset+=36;
                    }
                    continue;
                }
                int finish=finishes[offset];offset+=96;
                var half=box.size*.5f;
                float radius=Mathf.Min(theme==FantasyMapTheme.CandyGlass?.32f:.22f,Mathf.Min(half.x,Mathf.Min(half.y,half.z))*.40f);
                var inner=half-Vector3.one*radius;
                var transformToMesh=filter.transform.worldToLocalMatrix*box.transform.localToWorldMatrix;
                float[] Steps(int axis)=>new[]{-half[axis],-inner[axis]-radius*.5f,-inner[axis],inner[axis],inner[axis]+radius*.5f,half[axis]};
                for(int axis=0;axis<3;axis++)for(int sign=-1;sign<=1;sign+=2)
                {
                    int u=(axis+1)%3,v=(axis+2)%3,start=vertices.Count;
                    var us=Steps(u);var vs=Steps(v);
                    for(int y=0;y<6;y++)for(int x=0;x<6;x++)
                    {
                        var p=Vector3.zero;p[axis]=half[axis]*sign;p[u]=us[x];p[v]=vs[y];
                        var core=new Vector3(Mathf.Clamp(p.x,-inner.x,inner.x),Mathf.Clamp(p.y,-inner.y,inner.y),Mathf.Clamp(p.z,-inner.z,inner.z));
                        var delta=p-core;float length=delta.magnitude;var normal=length>0?delta/length:Vector3.zero;if(length<=0)normal[axis]=sign;
                        vertices.Add(transformToMesh.MultiplyPoint3x4(box.center+core+normal*radius));
                        normals.Add(transformToMesh.inverse.transpose.MultiplyVector(normal).normalized);
                    }
                    for(int y=0;y<5;y++)for(int x=0;x<5;x++)
                    {
                        int a=start+y*6+x,b=a+1,c=a+7,d=a+6;
                        if(sign>0)indices[finish].AddRange(new[]{a,b,c,a,c,d});
                        else indices[finish].AddRange(new[]{a,c,b,a,d,c});
                    }
                }
            }
            var mesh=new Mesh{name="Rounded fantasy surface",indexFormat=IndexFormat.UInt32};
            mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.subMeshCount=indices.Length;
            for(int sub=0;sub<indices.Length;sub++)mesh.SetTriangles(indices[sub],sub);
            mesh.RecalculateBounds();originalShapes[filter]=source;filter.sharedMesh=mesh;bodyMeshes.Add(mesh);
        }
    }
}
