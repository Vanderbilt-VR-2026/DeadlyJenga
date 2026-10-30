using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DeadlyJenga.CandyArt
{
    [DefaultExecutionOrder(950)]
    public sealed class ChristmasTownAtmosphere : MonoBehaviour
    {
        public Material winterSky;
        public Light keyLight;
        public Light[] localLights=Array.Empty<Light>();
        public Volume desktopGrade;
        struct HiddenRenderer { public Renderer renderer; public bool enabled; }
        sealed class EnvironmentState
        {
            public CandyEnvironment environment;public bool apply;
            public Light sun;public Color color;public float intensity;public Quaternion rotation;
        }
        readonly List<HiddenRenderer> hidden=new List<HiddenRenderer>();
        readonly List<EnvironmentState> environments=new List<EnvironmentState>();
        readonly Dictionary<UniversalAdditionalCameraData,bool> cameras=new Dictionary<UniversalAdditionalCameraData,bool>();
        Material previousSky;
        bool previousFog,captured;
        Color previousFogColor,previousSkyColor,previousEquator,previousGround;
        float previousStart,previousEnd,nextFind;
        FogMode previousFogMode;AmbientMode previousAmbientMode;
        int[] lightOrder;
        public void ApplyPreview()
        {
            RenderSettings.skybox=winterSky;RenderSettings.fog=true;RenderSettings.fogColor=new Color(.19f,.22f,.35f);
            RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=65;RenderSettings.fogEndDistance=175;
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.32f,.4f,.65f);
            RenderSettings.ambientEquatorColor=new Color(.36f,.32f,.43f);RenderSettings.ambientGroundColor=new Color(.19f,.14f,.23f);
            SetKey(keyLight);
        }
        static void SetKey(Light light)
        {
            if(!light)return;light.color=new Color(.68f,.77f,1);light.intensity=.85f;light.transform.rotation=Quaternion.Euler(27,-35,0);
        }
        void OnEnable()
        {
            if(!Application.isPlaying)return;
            previousSky=RenderSettings.skybox;previousFog=RenderSettings.fog;previousFogColor=RenderSettings.fogColor;
            previousStart=RenderSettings.fogStartDistance;previousEnd=RenderSettings.fogEndDistance;previousFogMode=RenderSettings.fogMode;
            previousAmbientMode=RenderSettings.ambientMode;previousSkyColor=RenderSettings.ambientSkyColor;
            previousEquator=RenderSettings.ambientEquatorColor;previousGround=RenderSettings.ambientGroundColor;captured=true;nextFind=0;
            lightOrder=new int[localLights.Length];for(int i=0;i<lightOrder.Length;i++)lightOrder[i]=i;
        }
        void LateUpdate()
        {
            if(!captured)return;
            bool lightingHazard=false;
            foreach(var state in DisasterStates.Active)
            {var frame=state.Capture();if(frame!=null&&frame.warning<=0&&(frame.kind=="PowerOutage"||frame.kind=="Vampire"))lightingHazard=true;}
            bool vr=Services.Session?.IsVR??false;
            if(desktopGrade)desktopGrade.enabled=!vr&&!lightingHazard;
            if(!lightingHazard)
            {
                ApplyPreview();
                foreach(var item in environments)if(item.sun)SetKey(item.sun);
            }
            if(Time.unscaledTime>=nextFind)
            {
                nextFind=Time.unscaledTime+.5f;
                foreach(var environment in FindObjectsByType<CandyEnvironment>(FindObjectsSortMode.None))
                {
                    Hide(environment.sandRenderer);Hide(environment.waterRenderer);
                    if(!environments.Exists(s=>s.environment==environment))
                    {
                        var sun=environment.sun;
                        environments.Add(new EnvironmentState{environment=environment,apply=environment.applyLighting,sun=sun,
                            color=sun?sun.color:Color.white,intensity=sun?sun.intensity:0,rotation=sun?sun.transform.rotation:Quaternion.identity});
                    }
                    environment.applyLighting=false;
                }
                foreach(var camera in Camera.allCameras)
                {
                    var data=camera.GetComponent<UniversalAdditionalCameraData>();if(!data||camera.cameraType!=CameraType.Game)continue;
                    if(!cameras.ContainsKey(data))cameras[data]=data.renderPostProcessing;
                    if(!vr)data.renderPostProcessing=true;
                }
                var view=Services.Session?.ActiveCamera;
                var position=view?view.transform.position:transform.position;
                Array.Sort(lightOrder,(a,b)=>Distance(localLights[a],position).CompareTo(Distance(localLights[b],position)));
                for(int i=0;i<lightOrder.Length;i++)if(localLights[lightOrder[i]])localLights[lightOrder[i]].enabled=i<(vr?6:16)&&!lightingHazard;
            }
            if(lightingHazard)foreach(var light in localLights)if(light)light.enabled=false;
            if(keyLight)keyLight.enabled=!environments.Exists(s=>s.sun);
            foreach(var item in hidden)if(item.renderer)item.renderer.enabled=false;
        }
        static float Distance(Light light,Vector3 position)=>light?(light.transform.position-position).sqrMagnitude:float.PositiveInfinity;
        void Hide(Renderer renderer)
        {
            if(!renderer)return;
            foreach(var item in hidden)if(item.renderer==renderer)return;
            hidden.Add(new HiddenRenderer{renderer=renderer,enabled=renderer.enabled});renderer.enabled=false;
        }
        void OnDisable()
        {
            if(!captured)return;
            foreach(var item in hidden)if(item.renderer)item.renderer.enabled=item.enabled;hidden.Clear();
            foreach(var item in environments)
            {
                if(item.environment)item.environment.applyLighting=item.apply;
                if(item.sun){item.sun.color=item.color;item.sun.intensity=item.intensity;item.sun.transform.rotation=item.rotation;}
            }
            environments.Clear();foreach(var item in cameras)if(item.Key)item.Key.renderPostProcessing=item.Value;cameras.Clear();
            RenderSettings.skybox=previousSky;RenderSettings.fog=previousFog;RenderSettings.fogColor=previousFogColor;
            RenderSettings.fogStartDistance=previousStart;RenderSettings.fogEndDistance=previousEnd;RenderSettings.fogMode=previousFogMode;
            RenderSettings.ambientMode=previousAmbientMode;RenderSettings.ambientSkyColor=previousSkyColor;
            RenderSettings.ambientEquatorColor=previousEquator;RenderSettings.ambientGroundColor=previousGround;captured=false;
        }
    }
}
