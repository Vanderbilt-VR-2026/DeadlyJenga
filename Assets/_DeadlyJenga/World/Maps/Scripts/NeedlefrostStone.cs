using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using Cut = DeadlyJenga.CastlePrototype.NeedlefrostLayout.Cut;

namespace DeadlyJenga.CastlePrototype
{
    internal static class NeedlefrostStone
    {
        static readonly Dictionary<Cut,Mesh> meshes = new Dictionary<Cut,Mesh>();

        public static void Dress(CastleBlock block, CastlePlacement placement)
        {
            Cut cut = (Cut)placement.Course;
            block.appearance = null;
            foreach (Transform old in block.visualRoot) old.gameObject.SetActive(false);
            var visual = new GameObject(cut.ToString());
            visual.transform.SetParent(block.visualRoot,false);
            if(cut==Cut.Vault || cut==Cut.Portal)
            {
                Arch(block,visual.transform,cut==Cut.Portal);
                return;
            }
            if (!meshes.TryGetValue(cut,out var mesh) || !mesh) meshes[cut]=mesh=Carve(cut);
            visual.AddComponent<MeshFilter>().sharedMesh=mesh;
            visual.AddComponent<MeshRenderer>().sharedMaterial=block.sectionMaterial;
            bool tapered=cut==Cut.Blade || cut==Cut.LeftBlade || cut==Cut.RightBlade || cut==Cut.Roof || cut==Cut.Window;
            if(tapered)
            {
                // Both the moving body and the survivor sweep use the actual cut crystal.
                block.GetComponent<BoxCollider>().enabled=false;
                block.SetTraversalEnabled(false);
                var solid=block.gameObject.AddComponent<MeshCollider>();
                solid.sharedMesh=mesh; solid.convex=true;
                var walk=visual.AddComponent<MeshCollider>(); walk.sharedMesh=mesh; walk.convex=true;
                visual.layer=Layers.Surface;
                Climbables.Register(walk,block);
                visual.AddComponent<NeedlefrostGrip>().shape=walk;
            }
            if(cut==Cut.Pier && Application.isPlaying)
            {
                // Ice feet are frozen into submerged rock. Each socket fractures independently.
                var socket=block.gameObject.AddComponent<FixedJoint>();
                socket.anchor=new Vector3(0,-.5f,0);
                socket.breakForce=180000;
                socket.breakTorque=80000;
            }
        }

        static readonly Mesh[] voussoirs=new Mesh[10];

        static readonly Mesh[] gateStones=new Mesh[10];

        static void Arch(CastleBlock block, Transform parent,bool gate)
        {
            var top=block.GetComponent<BoxCollider>();
            top.size=new Vector3(1,1f/12,1);
            top.center=new Vector3(0,.5f-top.size.y/2,0);
            // The thin springing lips sit on the two courts. Inside them the soffit rises
            // toward the centre. Convex stone segments leave the opening clear to physics.
            float[] xs={-.5f,-.455f,-.452f,-.34f,-.19f,0,.19f,.34f,.452f,.455f,.5f};
            float[] ys={.417f,.417f,-.5f,-.16f,.075f,.17f,.075f,-.16f,-.5f,.417f,.417f};
            if(gate)
            {
                xs=new[]{-.5f,-.42f,-.35f,-.30f,-.15f,0,.15f,.30f,.35f,.42f,.5f};
                ys=new[]{-.5f,-.5f,-.5f,.015f,.23f,.41f,.23f,.015f,-.5f,-.5f,-.5f};
            }
            var stones=gate?gateStones:voussoirs;
            for(int k=0;k<10;k++)
            {
                if(!stones[k])
                {
                    float a=xs[k],b=xs[k+1],c=ys[k],d=ys[k+1];
                    var m=new Mesh{name="Vault stone "+k};
                    m.vertices=new[]{new Vector3(a,c,-.5f),new Vector3(b,d,-.5f),new Vector3(b,.5f,-.5f),new Vector3(a,.5f,-.5f),
                        new Vector3(a,c,.5f),new Vector3(b,d,.5f),new Vector3(b,.5f,.5f),new Vector3(a,.5f,.5f)};
                    m.triangles=new[]{0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5};
                    m.RecalculateNormals();stones[k]=m;
                }
                var stone=new GameObject("Arch segment");stone.transform.SetParent(parent,false);
                stone.layer=Layers.Block;
                stone.AddComponent<MeshFilter>().sharedMesh=stones[k];
                stone.AddComponent<MeshRenderer>().sharedMaterial=block.sectionMaterial;
                if(gate || (k>0 && k<9))
                {
                    var collider=stone.AddComponent<MeshCollider>();collider.sharedMesh=stones[k];collider.convex=true;
                }
            }
        }

        static Mesh Carve(Cut cut)
        {
            bool blade=cut==Cut.Blade||cut==Cut.LeftBlade||cut==Cut.RightBlade||cut==Cut.Window;
            bool roof=cut==Cut.Roof;
            float lean=cut==Cut.LeftBlade?-.62f:cut==Cut.RightBlade?.62f:0;
            var vertices=new List<Vector3>();
            var triangles=new List<int>();
            Vector3[] Ring(float y,float radius,float bevel,float shift)
            {
                float r=radius,t=r*(1-bevel);
                return new[]{new Vector3(-t,y,-r),new Vector3(t,y,-r),new Vector3(r,y,-t),new Vector3(r,y,t),
                    new Vector3(t,y,r),new Vector3(-t,y,r),new Vector3(-r,y,t),new Vector3(-r,y,-t)}
                    .WithOffset(shift);
            }
            void Face(Vector3 a,Vector3 b,Vector3 c)
            {
                int index=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);
                triangles.Add(index);triangles.Add(index+1);triangles.Add(index+2);
            }
            var bottom=Ring(-.5f,blade?.33f:.475f,blade?.54f:.16f,0);
            var edge=Ring(blade?.12f:roof?-.38f:-.43f,.5f,blade?.62f:.13f,lean*.45f);
            var shoulder=Ring(blade?.32f:roof?.1f:.43f,blade?.35f:roof?.27f:.5f,blade?.65f:.13f,lean*.72f);
            var top=Ring(.5f,blade?.012f:roof?.16f:.475f,blade?.65f:.16f,lean);
            var rings=new[]{bottom,edge,shoulder,top};
            for(int i=0;i<8;i++)
            {
                int j=(i+1)%8;
                for(int band=0;band<3;band++)
                {
                    var lo=rings[band];var hi=rings[band+1];
                    Face(lo[i],hi[i],hi[j]);Face(lo[i],hi[j],lo[j]);
                }
                Face(Vector3.down*.5f,bottom[i],bottom[j]);
                Face(new Vector3(lean,.5f,0),top[j],top[i]);
            }
            var mesh=new Mesh{name="Carved ice "+cut};
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            return mesh;
        }

        static Vector3[] WithOffset(this Vector3[] ring,float dx)
        { for(int i=0;i<ring.Length;i++)ring[i].x+=dx;return ring; }
    }

    internal sealed class NeedlefrostGrip : MonoBehaviour
    {
        internal Collider shape;
        void OnDestroy(){if(shape)Climbables.Unregister(shape);}
    }
}
