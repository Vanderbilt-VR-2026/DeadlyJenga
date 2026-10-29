using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyArt
{
    // Resort lighting is scoped to the lifetime of this map's visual prefab.
    [DefaultExecutionOrder(980)]
    public sealed class TikiResortAtmosphere : MonoBehaviour
    {
        public Light[] practicalLights;
        public Material resortSky;
        public Light sunlight;
        readonly Dictionary<Light,bool> sunStates=new Dictionary<Light,bool>();
        readonly Dictionary<CandyEnvironment,bool> environmentStates=new Dictionary<CandyEnvironment,bool>();
        readonly Dictionary<Renderer,bool> exteriorStates=new Dictionary<Renderer,bool>();
        Material previousSky;
        bool previousFog;
        AmbientMode ambientMode;
        Color sky,equator,ground;
        SphericalHarmonicsL2 previousProbe;
        bool captured;
        float nextUpdate;
        Renderer lagoon;
        MaterialPropertyBlock waterProperties;
        void OnEnable()
        {
            if(!Application.isPlaying)return;
            ambientMode=RenderSettings.ambientMode;sky=RenderSettings.ambientSkyColor;
            equator=RenderSettings.ambientEquatorColor;ground=RenderSettings.ambientGroundColor;previousProbe=RenderSettings.ambientProbe;
            previousSky=RenderSettings.skybox;previousFog=RenderSettings.fog;captured=true;
            lagoon=transform.Find("Turquoise waterfront")?.GetComponent<Renderer>();waterProperties=new MaterialPropertyBlock();
            Apply();
        }
        void LateUpdate()
        {
            if(!captured||Time.unscaledTime<nextUpdate)return;nextUpdate=Time.unscaledTime+.3f;Apply();
        }
        void Apply()
        {
            foreach(var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if(light.type==LightType.Directional&&!light.transform.IsChildOf(transform))
                {if(!sunStates.ContainsKey(light))sunStates.Add(light,light.enabled);light.enabled=false;}
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.52f,.75f,.94f);
            RenderSettings.ambientEquatorColor=new Color(.46f,.59f,.59f);
            RenderSettings.ambientGroundColor=new Color(.30f,.31f,.24f);
            var probe=new SphericalHarmonicsL2();probe.AddAmbientLight(new Color(.52f,.59f,.61f));RenderSettings.ambientProbe=probe;
            foreach(var environment in FindObjectsByType<CandyEnvironment>(FindObjectsSortMode.None))
            {
                if(!environmentStates.ContainsKey(environment))environmentStates.Add(environment,environment.applyLighting);environment.applyLighting=false;
                foreach(var renderer in environment.GetComponentsInChildren<Renderer>())
                {if(!exteriorStates.ContainsKey(renderer))exteriorStates.Add(renderer,renderer.enabled);renderer.enabled=false;}
            }
            RenderSettings.skybox=resortSky;RenderSettings.fog=false;
            if(sunlight)sunlight.enabled=true;
            if(lagoon)
            {
                var center=transform.position;var scale=transform.lossyScale;
                lagoon.GetPropertyBlock(waterProperties);waterProperties.SetVector("_Island",new Vector4(center.x,center.z,30*Mathf.Abs(scale.x),25*Mathf.Abs(scale.z)));
                waterProperties.SetFloat("_WaterHeight",transform.TransformPoint(Vector3.down*1.4f).y);waterProperties.SetFloat("_SandTop",transform.TransformPoint(Vector3.down*.45f).y);
                lagoon.SetPropertyBlock(waterProperties);
            }
            // Four local accent lights on Quest, plus one shared sun.
            var camera=Services.Session?.ActiveCamera;
            Vector3 p=camera?camera.transform.position:transform.position;
            for(int i=0;i<practicalLights.Length;i++)
            {
                var light=practicalLights[i];if(!light)continue;
                int closer=0;float distance=(light.transform.position-p).sqrMagnitude;
                for(int j=0;j<practicalLights.Length;j++)if(practicalLights[j]&&j!=i)
                {float other=(practicalLights[j].transform.position-p).sqrMagnitude;if(other<distance||other==distance&&j<i)closer++;}
                light.enabled=Services.Session?.IsVR!=true||closer<4;
            }
        }
        void OnDisable()
        {
            if(!captured)return;
            foreach(var pair in sunStates)if(pair.Key)pair.Key.enabled=pair.Value;sunStates.Clear();
            foreach(var pair in environmentStates)if(pair.Key)pair.Key.applyLighting=pair.Value;environmentStates.Clear();
            foreach(var pair in exteriorStates)if(pair.Key)pair.Key.enabled=pair.Value;exteriorStates.Clear();
            RenderSettings.skybox=previousSky;RenderSettings.fog=previousFog;
            RenderSettings.ambientMode=ambientMode;RenderSettings.ambientSkyColor=sky;
            RenderSettings.ambientEquatorColor=equator;RenderSettings.ambientGroundColor=ground;RenderSettings.ambientProbe=previousProbe;captured=false;
        }
    }
}
