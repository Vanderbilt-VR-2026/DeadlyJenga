using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.World.Maps
{
    // Static, gray-box parkour geometry. The optional art can be removed independently.
    public sealed class LobbyMilkshake : MonoBehaviour
    {
        [Min(3)] public float height=5.15f;
        [Min(.6f)] public float radius=1.15f;
        [Range(.2f,.8f)] public float fill=.5f;
        public Transform Geometry {get;private set;}
        readonly List<Mesh> meshes=new List<Mesh>();
        public float MilkHeight=>transform.position.y+height*fill;
        public float RimHeight=>transform.position.y+height;
        void Awake()=>Build();
        public void Build()
        {
            if(Geometry)return;
            Geometry=new GameObject("Shake collision geometry").transform;Geometry.SetParent(transform,false);
            const int sides=40;float wall=.3f;
            for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2/sides;
                Box("Glass wall "+i,new Vector3(Mathf.Cos(a)*radius,height*.5f,Mathf.Sin(a)*radius),new Vector3(wall,height,2*radius*Mathf.Tan(Mathf.PI/sides)+.015f),Quaternion.Euler(0,-a*Mathf.Rad2Deg,0));
            }
            Cylinder("Chocolate",new Vector3(0,height*fill-.06f,0),radius-.14f,.12f);
            Box("Cooler",new Vector3(2.4f,.53f,-.3f),new Vector3(1.1f,1.06f,.85f),Quaternion.Euler(0,-12,0));
            Box("Delivery crates",new Vector3(2.8f,1.28f,1.5f),new Vector3(.85f,2.56f,.85f),Quaternion.Euler(0,7,0));
            Cylinder("Parasol canopy",new Vector3(1.8f,3.65f,2.9f),.94f,.12f,true);
            Cylinder("Parasol pole",new Vector3(1.8f,1.8f,2.9f),.045f,3.6f);
            Cylinder("Cherry perch",new Vector3(-.52f,height*fill+.25f,.1f),.3f,.5f);
            StrawSegment(new Vector3(.55f,height*fill,.2f),new Vector3(.72f,height+.45f,.2f));
            StrawSegment(new Vector3(.72f,height+.45f,.2f),new Vector3(1.3f,height+.9f,.2f));
        }
        Transform Marker(string name,Vector3 p)
        {var go=new GameObject(name);go.layer=Layers.Surface;go.transform.SetParent(Geometry,false);go.transform.localPosition=p;return go.transform;}
        void Box(string name,Vector3 p,Vector3 size,Quaternion rotation)
        {
            var root=Marker(name,p);root.localRotation=rotation;root.gameObject.AddComponent<BoxCollider>().size=size;
            var visual=GameObject.CreatePrimitive(PrimitiveType.Cube);visual.name="Visual";visual.transform.SetParent(root,false);visual.transform.localScale=size;Destroy(visual.GetComponent<Collider>());
        }
        void Cylinder(string name,Vector3 p,float radius,float height,bool canopy=false)
        {
            var root=Marker(name,p);var mesh=new Mesh{name=name+" static surface"};meshes.Add(mesh);
            var v=new List<Vector3>();var t=new List<int>();const int count=40;
            for(int y=0;y<2;y++)for(int i=0;i<count;i++){float a=i*Mathf.PI*2/count;v.Add(new Vector3(Mathf.Cos(a)*radius,(y-.5f)*height-(canopy?(y==1?.12f:.08f):0),Mathf.Sin(a)*radius));}
            v.Add(Vector3.up*height*.5f);v.Add(Vector3.down*height*.5f);
            for(int i=0;i<count;i++){int j=(i+1)%count;t.AddRange(new[]{i,j,i+count,j,j+count,i+count,80,i+count,j+count,81,j,i});}
            for(int i=0;i<t.Count;i+=3){int swap=t[i];t[i]=t[i+1];t[i+1]=swap;}
            mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            root.gameObject.AddComponent<MeshCollider>().sharedMesh=mesh;
            var visual=new GameObject("Visual",typeof(MeshFilter),typeof(MeshRenderer));visual.transform.SetParent(root,false);visual.GetComponent<MeshFilter>().sharedMesh=mesh;
            // Use Unity's default gray material so this remains usable without Presentation.
            var template=GameObject.CreatePrimitive(PrimitiveType.Cube);visual.GetComponent<MeshRenderer>().sharedMaterial=template.GetComponent<Renderer>().sharedMaterial;Destroy(template);
        }
        void StrawSegment(Vector3 a,Vector3 b)
        {var root=Marker("Straw",(a+b)*.5f);root.up=(b-a).normalized;var capsule=root.gameObject.AddComponent<CapsuleCollider>();capsule.radius=.13f;capsule.height=Vector3.Distance(a,b)+.26f;}
        void OnDestroy(){foreach(var mesh in meshes)if(mesh)Destroy(mesh);}
    }
}
