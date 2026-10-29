using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using DeadlyJenga.Core;

namespace DeadlyJenga.Presentation
{
    public sealed partial class FantasyMapArt
    {
        Volume grade;
        VolumeProfile gradeProfile;
        readonly System.Collections.Generic.Dictionary<UniversalAdditionalCameraData,bool> cameraGrades=new();
        bool oldFog;
        FogMode oldFogMode;
        Color oldFogColor;
        float oldFogDensity;

        void BeginLighting()
        {
            oldFog=RenderSettings.fog;oldFogMode=RenderSettings.fogMode;
            oldFogColor=RenderSettings.fogColor;oldFogDensity=RenderSettings.fogDensity;
            var go=new GameObject("Visual · fantasy color grade",typeof(Volume));go.transform.SetParent(transform,false);
            grade=go.GetComponent<Volume>();grade.isGlobal=true;grade.priority=5;
            gradeProfile=ScriptableObject.CreateInstance<VolumeProfile>();grade.sharedProfile=gradeProfile;
            var bloom=gradeProfile.Add<Bloom>(true);bloom.intensity.Override(.18f);bloom.threshold.Override(1.1f);bloom.scatter.Override(.55f);
            gradeProfile.Add<Tonemapping>(true).mode.Override(TonemappingMode.None);
            var color=gradeProfile.Add<ColorAdjustments>(true);color.contrast.Override(5);color.saturation.Override(5);
            color.postExposure.Override(0);
        }

        void UpdateLighting()
        {
            bool hazard=false;
            foreach(var state in DisasterStates.Active)
            {var frame=state.Capture();if(frame!=null&&frame.warning<=0&&(frame.kind=="PowerOutage"||frame.kind=="Vampire")){hazard=true;break;}}
            bool vr=Services.Session?.IsVR??false;
            if(grade)grade.enabled=!vr&&!hazard;
            if(vr)foreach(var pair in cameraGrades)if(pair.Key)pair.Key.renderPostProcessing=pair.Value;
            if(hazard)return;
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;
            RenderSettings.fogColor=theme==FantasyMapTheme.Honeycomb?new Color(.93f,.62f,.22f):theme==FantasyMapTheme.Carnival?new Color(.91f,.72f,.84f):new Color(.75f,.88f,1);
            RenderSettings.fogDensity=theme==FantasyMapTheme.Honeycomb?.0011f:theme==FantasyMapTheme.Carnival?.0007f:.00035f;
            RenderSettings.skybox=skyMaterial;RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=theme==FantasyMapTheme.Honeycomb?new Color(.91f,.74f,.45f):theme==FantasyMapTheme.Carnival?new Color(.68f,.73f,1):new Color(.66f,.83f,1);
            RenderSettings.ambientEquatorColor=theme==FantasyMapTheme.Honeycomb?new Color(.62f,.40f,.17f):theme==FantasyMapTheme.Carnival?new Color(.87f,.62f,.75f):new Color(.64f,.76f,.88f);
            RenderSettings.ambientGroundColor=theme==FantasyMapTheme.Honeycomb?new Color(.28f,.15f,.055f):new Color(.32f,.35f,.49f);
            ConfigureCamera(Services.Session?.ActiveCamera);
        }

        public void ConfigureCamera(Camera camera)
        {
            if(!camera||!grade||Services.Session?.IsVR==true)return;
            var data=camera.GetUniversalAdditionalCameraData();
            if(!cameraGrades.ContainsKey(data))cameraGrades[data]=data.renderPostProcessing;
            data.renderPostProcessing=true;
        }

        void EndLighting()
        {
            foreach(var pair in cameraGrades)if(pair.Key)pair.Key.renderPostProcessing=pair.Value;
            cameraGrades.Clear();
            if(grade)Release(grade.gameObject);grade=null;
            if(gradeProfile){foreach(var component in gradeProfile.components)Release(component);Release(gradeProfile);}gradeProfile=null;
            RenderSettings.fog=oldFog;RenderSettings.fogMode=oldFogMode;RenderSettings.fogColor=oldFogColor;RenderSettings.fogDensity=oldFogDensity;
        }
    }
}
