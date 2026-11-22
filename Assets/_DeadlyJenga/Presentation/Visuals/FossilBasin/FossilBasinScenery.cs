using DeadlyJenga.CandyArt;
using UnityEngine;

namespace DeadlyJenga.Presentation
{
    public sealed class FossilBasinScenery : MonoBehaviour
    {
        public Material sky,tar;
        CandyEnvironment environment;
        bool waterOff,sandOff;
        void Start()
        {
            Surface("Tar",PrimitiveType.Plane,new Vector3(55,1,55),tar);
            Surface("Excavation sunset",PrimitiveType.Sphere,Vector3.one*420,sky);
        }
        void Surface(string label,PrimitiveType type,Vector3 scale,Material finish)
        {
            // Borrow the built-in mesh without creating a physics surface in the scene.
            var go=GameObject.CreatePrimitive(type);go.name=label;
            var col=go.GetComponent<Collider>();col.enabled=false;Destroy(col);
            go.transform.SetParent(transform,false);go.transform.localScale=scale;
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=finish;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        void LateUpdate()
        {
            if(environment)return;
            environment=CandyEnvironment.ForMap(this);
            if(!environment)return;
            waterOff=environment.waterRenderer.forceRenderingOff;sandOff=environment.sandRenderer.forceRenderingOff;
            environment.waterRenderer.forceRenderingOff=true;environment.sandRenderer.forceRenderingOff=true;
        }
        void OnDisable()
        {
            if(!environment)return;
            environment.waterRenderer.forceRenderingOff=waterOff;environment.sandRenderer.forceRenderingOff=sandOff;
            environment=null;
        }
    }
}
