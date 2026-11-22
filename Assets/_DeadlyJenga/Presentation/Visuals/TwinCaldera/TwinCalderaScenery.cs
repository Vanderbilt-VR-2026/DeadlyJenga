using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyArt
{
    public sealed class TwinCalderaScenery : MonoBehaviour
    {
        public Material lava, sky;
        Mesh surface;
        void Awake()
        {
            surface=new Mesh {name="Lava surface"};
            const int size=1600;
            surface.vertices=new[]{new Vector3(-size,0,-size),new Vector3(-size,0,size),
                new Vector3(size,0,size),new Vector3(size,0,-size)};
            surface.triangles=new[]{0,1,2,0,2,3};
            surface.RecalculateBounds();
            var sea=new GameObject("Lava ocean");
            sea.transform.SetParent(transform,false);
            sea.AddComponent<MeshFilter>().sharedMesh=surface;
            var renderer=sea.AddComponent<MeshRenderer>();
            renderer.sharedMaterial=lava;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.receiveShadows=false;
        }
        void OnDestroy(){if(surface)Destroy(surface);}
    }
}
