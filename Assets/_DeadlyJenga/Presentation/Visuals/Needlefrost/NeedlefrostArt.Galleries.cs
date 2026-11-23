using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.Polar
{
    public sealed partial class NeedlefrostArt
    {
        public Material amberLight;
        Mesh chandelier;
        Material bronze;
        readonly List<GameObject> fixtures=new List<GameObject>();

        void BindChandeliers()
        {
            if(!amberLight || !icebergMaterial)return;
            if(!chandelier)
            {
                chandelier=BuildChandelier();
                bronze=new Material(icebergMaterial){name="Weathered bronze"};
                bronze.SetColor("_Tint",new Color(.37f,.19f,.065f));
                bronze.SetFloat("_Metal",1);
            }
            fixtures.RemoveAll(item=>!item);
            void Hang(float x,float ceiling,float z)
            {
                var top=transform.TransformPoint(new Vector3(x,ceiling,z));
                foreach(var body in world.Blocks)
                {
                    if(!body || (body.position-top).sqrMagnitude>.20f)continue;
                    if(body.transform.Find("Gallery chandelier"))return;
                    var fixture=new GameObject("Gallery chandelier");
                    fixture.transform.position=top-transform.up*.50f;
                    fixture.transform.rotation=transform.rotation;
                    fixture.transform.SetParent(body.transform,true);
                    fixture.AddComponent<MeshFilter>().sharedMesh=chandelier;
                    fixture.AddComponent<MeshRenderer>().sharedMaterials=new[]{bronze,amberLight};
                    fixtures.Add(fixture);
                    return;
                }
            }
            Hang(-24,14.5f,17);Hang(-24,20.5f,17);
            Hang(16,15.5f,13);Hang(16,22.5f,13);
            Hang(0,15.65f,15);Hang(0,20.65f,15);
            Hang(-24,26.75f,17);Hang(16,29.4f,13);
            Hang(-7.58f,26.11f,15.27f);Hang(7.58f,26.11f,15.27f);
        }

        static Mesh BuildChandelier()
        {
            var vertices=new List<Vector3>();
            var faces=new[]{new List<int>(),new List<int>()};
            void Rod(Vector3 a,Vector3 b,float r0,float r1,int material)
            {
                var axis=(b-a).normalized;
                var side=Vector3.Cross(axis,Mathf.Abs(axis.y)>.9f?Vector3.right:Vector3.up).normalized;
                var across=Vector3.Cross(axis,side);
                int first=vertices.Count;
                const int sides=6;
                for(int i=0;i<sides;i++)
                {
                    float angle=i*(2*Mathf.PI/sides);
                    var normal=side*Mathf.Cos(angle)+across*Mathf.Sin(angle);
                    vertices.Add(a+normal*r0);vertices.Add(b+normal*r1);
                }
                var triangles=faces[material];
                for(int i=0;i<sides;i++)
                {
                    int v=first+2*i,w=first+2*((i+1)%sides);
                    triangles.Add(v);triangles.Add(w);triangles.Add(v+1);
                    triangles.Add(v+1);triangles.Add(w);triangles.Add(w+1);
                }
            }
            var hub=Vector3.down*.34f;
            Rod(Vector3.zero,hub,.055f,.065f,0);
            Vector3 Rim(int i,float y)=>new Vector3(Mathf.Cos(i*Mathf.PI/4)*1.12f,y,Mathf.Sin(i*Mathf.PI/4)*1.12f);
            for(int i=0;i<8;i++)
            {
                var cup=Rim(i,-1.45f);
                Rod(cup,Rim(i+1,-1.45f),.055f,.055f,0);
                Rod(cup+Vector3.up*.14f,Rim(i+1,-1.31f),.028f,.028f,0);
                if(i%2==0)Rod(hub,cup,.022f,.034f,0);
                Rod(cup,cup+Vector3.up*.17f,.065f,.13f,0);
                Rod(cup+Vector3.up*.17f,cup+Vector3.up*.37f,.105f,.09f,1);
                Rod(cup+Vector3.up*.37f,cup+Vector3.up*.59f,.09f,.006f,1);
            }
            var mesh=new Mesh{name="Octagonal gallery candle wheel"};
            mesh.SetVertices(vertices);mesh.subMeshCount=2;
            mesh.SetTriangles(faces[0],0);mesh.SetTriangles(faces[1],1);mesh.RecalculateNormals();
            return mesh;
        }

        void ReleaseChandeliers()
        {
            foreach(var fixture in fixtures)if(fixture)Destroy(fixture);
            fixtures.Clear();Destroy(chandelier);Destroy(bronze);
        }
    }
}
