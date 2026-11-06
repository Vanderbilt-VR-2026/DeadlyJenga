using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class IslandLobbyView
    {
        Mesh cabinetMesh;
        void BuildArcadeCabinet()
        {
            var root=new GameObject("Grounded red arcade cabinet",typeof(MeshFilter),typeof(MeshRenderer));root.transform.SetParent(transform,false);
            root.transform.SetPositionAndRotation(new Vector3(piano.position.x,0,piano.position.z),Quaternion.Euler(0,piano.eulerAngles.y,0));
            var corners=new Vector3[8];int n=0;
            foreach(var p in new[]{new Vector2(-840,-370),new Vector2(840,-370),new Vector2(840,370),new Vector2(-840,370)})
            {
                var top=piano.TransformPoint(new Vector3(p.x,p.y,100));corners[n]=root.transform.InverseTransformPoint(top);
                top.y=-.15f;corners[n+4]=root.transform.InverseTransformPoint(top);n++;
            }
            var vertices=new List<Vector3>();var triangles=new List<int>();
            void Face(int a,int b,int c,int d)
            {
                int start=vertices.Count;vertices.Add(corners[a]);vertices.Add(corners[b]);vertices.Add(corners[c]);vertices.Add(corners[d]);triangles.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
            }
            Face(0,3,2,1);Face(4,5,6,7);Face(0,1,5,4);Face(1,2,6,5);Face(2,3,7,6);Face(3,0,4,7);
            cabinetMesh=new Mesh{name="Vertical arcade cabinet with sloped top"};cabinetMesh.SetVertices(vertices);cabinetMesh.SetTriangles(triangles,0);cabinetMesh.RecalculateNormals();cabinetMesh.RecalculateBounds();
            root.GetComponent<MeshFilter>().sharedMesh=cabinetMesh;var renderer=root.GetComponent<Renderer>();renderer.sharedMaterial=keyboardMaterial;
            var properties=new MaterialPropertyBlock();properties.SetColor("_BaseColor",new Color(.72f,.012f,.045f));renderer.SetPropertyBlock(properties);
            // Lobby-owned static slices approximate the angled top without a new collision contract.
            float front=corners[0].z,back=corners[3].z;
            for(int i=0;i<8;i++)
            {
                float t0=i/8f,t1=(i+1)/8f,y=Mathf.Min(Mathf.Lerp(corners[0].y,corners[3].y,t0),Mathf.Lerp(corners[0].y,corners[3].y,t1));
                lobby.AddBlocker(root.transform,new Vector3(0,(y-.15f)*.5f,Mathf.Lerp(front,back,(t0+t1)*.5f)),new Vector3(corners[1].x-corners[0].x,y+.15f,Mathf.Abs(back-front)/8));
            }
        }
    }
}
