using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    internal static class TwinCalderaRock
    {
        // Eight clipped corners and a broken crown give each stone a distinct
        // silhouette. Both the visible rock and collision use these faces.
        internal static void Fit(CastleBlock block,TwinCalderaLayout.Piece piece,int index,Material material)
        {
            var go=block.gameObject;
            go.name=piece.name;
            block.appearance=null;
            foreach(Transform child in block.visualRoot) child.gameObject.SetActive(false);
            var box=go.GetComponent<BoxCollider>();
            block.UseOriginalTraversal();
            box.enabled=false;
            var mesh=Shape(piece,index);
            var hull=go.GetComponent<MeshCollider>();
            hull.sharedMaterial=box.sharedMaterial;
            hull.convex=true;
            hull.sharedMesh=mesh;
            hull.excludeLayers=(1<<Layers.Survivor)|(1<<Layers.Surface);

            hull.layerOverridePriority=11;
            var skin=new GameObject("Survivor contact");
            skin.transform.SetParent(go.transform,false);
            skin.layer=Layers.Surface;
            var traversal=skin.AddComponent<MeshCollider>();
            traversal.convex=true;
            traversal.sharedMesh=mesh;
            traversal.excludeLayers=~(1<<Layers.Survivor);
            traversal.includeLayers=1<<Layers.Survivor;
            traversal.layerOverridePriority=12;
            var stone=new GameObject("Visual");
            stone.transform.SetParent(block.visualRoot,false);
            stone.AddComponent<MeshFilter>().sharedMesh=mesh;
            stone.AddComponent<MeshRenderer>().sharedMaterial=material;
            var cleanup=stone.AddComponent<TwinCalderaMeshLifetime>();
            cleanup.mesh=mesh;

        }

        static Mesh Shape(TwinCalderaLayout.Piece p,int seed)
        {
            var v=new List<Vector3>(48);
            var tri=new List<int>(84);
            void Face(params Vector3[] points)
            {
                int start=v.Count;
                v.AddRange(points);
                for(int i=1;i<points.Length-1;i++)
                {tri.Add(start);tri.Add(start+i);tri.Add(start+i+1);}
            }
            float cut=.055f+.018f*(seed%5);
            Vector2[] outline={new(-.5f+cut,-.5f),new(-.5f,-.5f+cut),new(-.5f,.5f-cut),
                new(-.5f+cut,.5f),new(.5f-cut,.5f),new(.5f,.5f-cut),new(.5f,-.5f+cut),new(.5f-cut,-.5f)};
            var lo=new Vector3[8]; var hi=new Vector3[8];
            for(int i=0;i<8;i++)
            {
                Vector2 uv=outline[i];
                lo[i]=new Vector3(uv.x,-.5f+p.bottomRise*uv.y/p.placement.Size.y,uv.y);
                hi[i]=new Vector3(uv.x*p.taper+p.lean,.5f+p.topRise*uv.y/p.placement.Size.y,uv.y*p.taper);
            }
            Face(hi);
            for(int i=0;i<8;i++){int j=(i+1)%8;Face(lo[i],lo[j],hi[j],hi[i]);}
            System.Array.Reverse(lo);Face(lo);
            var mesh=new Mesh {name=p.name};
            mesh.SetVertices(v);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            return mesh;
        }
    }

    internal sealed class TwinCalderaMeshLifetime : MonoBehaviour
    {
        internal Mesh mesh;

        void OnDestroy()
        {

            if(mesh)Destroy(mesh);
        }
    }
}
