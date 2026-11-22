using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CandyArt
{
    [DefaultExecutionOrder(1800)]
    public sealed class JunctionDusk : MonoBehaviour
    {
        public Texture panorama;
        public Shader skyShader, sandShader;
        Material sky, sand, previousSky;
        GameObject sheet;
        CandyEnvironment environment;
        bool previousWater,previousSand;
        void Start()
        {
            previousSky=RenderSettings.skybox;
            sky=new Material(skyShader);
            sky.SetTexture("_MainTex",panorama);
            sand=new Material(sandShader);
            sheet=GameObject.CreatePrimitive(PrimitiveType.Plane);
            var col=sheet.GetComponent<Collider>();
            col.enabled=false; Destroy(col);
            sheet.name="Flowing quicksand";
            sheet.transform.SetParent(transform,false);
            sheet.transform.localScale=new Vector3(48,1,48);
            sheet.GetComponent<Renderer>().sharedMaterial=sand;
        }
        void LateUpdate()
        {
            if(!sky) return;
            RenderSettings.skybox=sky;
            if(!environment)
            {
                environment=CandyEnvironment.ForMap(this);
                if(environment)
                {
                    previousWater=environment.waterRenderer.forceRenderingOff;
                    previousSand=environment.sandRenderer.forceRenderingOff;
                }
            }
            if(environment)
            {
                environment.waterRenderer.forceRenderingOff=true;
                environment.sandRenderer.forceRenderingOff=true;
            }
        }
        void OnDisable()
        {
            if(environment)
            {
                environment.waterRenderer.forceRenderingOff=previousWater;
                environment.sandRenderer.forceRenderingOff=previousSand;
            }
            if(sky && RenderSettings.skybox==sky) RenderSettings.skybox=previousSky;
        }
        void OnDestroy(){if(sky)Destroy(sky);if(sand)Destroy(sand);}
    }
}
