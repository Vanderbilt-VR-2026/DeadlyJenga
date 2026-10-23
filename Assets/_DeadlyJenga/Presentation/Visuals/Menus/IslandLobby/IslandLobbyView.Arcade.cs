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
        void StyleJoinArcade()
        {
            var yaw=Quaternion.Euler(0,piano.eulerAngles.y,0);
            joinStation.SetPositionAndRotation(new Vector3(piano.position.x,StationY,StationZ),yaw);joinStation.localScale=Vector3.one*StationScale;
            Box(transform,"JOIN monitor support",new Vector3(piano.position.x,1.35f,1.12f),new Vector3(.24f,.95f,.18f),new Color(.63f,.018f,.055f));
            var red=new Color(.63f,.018f,.055f);
            Box(joinStation,"CRT red back shell",new Vector3(0,0,110),new Vector3(1140,880,70),red);
            Box(joinStation,"CRT dark glass",new Vector3(0,0,50),new Vector3(1010,770,80),new Color(.012f,.008f,.025f));
            Box(joinStation,"CRT left bezel",new Vector3(-535,0,35),new Vector3(95,880,160),red);
            Box(joinStation,"CRT right bezel",new Vector3(535,0,35),new Vector3(95,880,160),red);
            Box(joinStation,"CRT top bezel",new Vector3(0,405,35),new Vector3(1070,95,160),red);
            Box(joinStation,"CRT bottom bezel",new Vector3(0,-405,35),new Vector3(1070,95,160),red);
            joinStation.GetComponent<Image>().color=new Color(.025f,.014f,.05f);
            var stripe=joinStation.Find("Candy accent");if(stripe)stripe.gameObject.SetActive(false);
            joinFace.RearOffset=.19f;joinFace.Refresh();
            lobby.AddBlocker(joinStation,new Vector3(0,0,70),new Vector3(1140,880,210));
        }
    }
}
