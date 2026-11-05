using UnityEngine;

namespace DeadlyJenga.CandyArt
{
    public sealed partial class CandyHazardLighting
    {
        bool vampireLighting;
        Color vampireAmbientSky,vampireAmbientEquator,vampireAmbientGround,vampireSunColor;
        float vampireSunIntensity;
        Quaternion vampireSunRotation;
        Light vampireLight;
        bool vampireApplied,vampireLightApplied;
        Color vampireWrittenSky,vampireWrittenEquator,vampireWrittenGround,vampireWrittenSunColor;
        float vampireWrittenSunIntensity;
        Quaternion vampireWrittenSunRotation;
        void BeginVampireLighting()
        {
            if(!vampireLighting)
            {
                vampireLighting=true;
                vampireAmbientSky=RenderSettings.ambientSkyColor;
                vampireAmbientEquator=RenderSettings.ambientEquatorColor;
                vampireAmbientGround=RenderSettings.ambientGroundColor;
            }
            else if(vampireApplied)
            {
                if(RenderSettings.ambientSkyColor!=vampireWrittenSky)vampireAmbientSky=RenderSettings.ambientSkyColor;
                if(RenderSettings.ambientEquatorColor!=vampireWrittenEquator)vampireAmbientEquator=RenderSettings.ambientEquatorColor;
                if(RenderSettings.ambientGroundColor!=vampireWrittenGround)vampireAmbientGround=RenderSettings.ambientGroundColor;
            }
            if(vampireLight!=sun)
            {
                RestoreVampireLight();
                vampireLight=sun;
                if(vampireLight)
                {
                    vampireSunIntensity=vampireLight.intensity;
                    vampireSunRotation=vampireLight.transform.rotation;
                    vampireSunColor=vampireLight.color;
                }
            }
            else if(vampireLight&&vampireLightApplied)
            {
                if(!Mathf.Approximately(vampireLight.intensity,vampireWrittenSunIntensity))vampireSunIntensity=vampireLight.intensity;
                if(vampireLight.color!=vampireWrittenSunColor)vampireSunColor=vampireLight.color;
                if(Quaternion.Angle(vampireLight.transform.rotation,vampireWrittenSunRotation)>.01f)vampireSunRotation=vampireLight.transform.rotation;
            }
        }
        void DrawVampireShade(Camera view)
        {
            if(!vampireLighting)BeginVampireLighting();
            RenderSettings.ambientSkyColor=ReadableVampireAmbient(vampireAmbientSky,new Color(.18f,.23f,.30f));
            RenderSettings.ambientEquatorColor=ReadableVampireAmbient(vampireAmbientEquator,new Color(.12f,.17f,.21f));
            RenderSettings.ambientGroundColor=ReadableVampireAmbient(vampireAmbientGround,new Color(.08f,.11f,.14f));
            if(vampireLight)
            {
                vampireLight.intensity=Mathf.Max(.75f,vampireSunIntensity*.95f);
                vampireLight.color=Color.Lerp(vampireSunColor,new Color(1,.80f,.55f),.28f);
                vampireWrittenSunIntensity=vampireLight.intensity;
                vampireWrittenSunColor=vampireLight.color;
                vampireWrittenSunRotation=vampireLight.transform.rotation;
                vampireLightApplied=true;
            }
            vampireWrittenSky=RenderSettings.ambientSkyColor;
            vampireWrittenEquator=RenderSettings.ambientEquatorColor;
            vampireWrittenGround=RenderSettings.ambientGroundColor;
            vampireApplied=true;
        }
        static Color ReadableVampireAmbient(Color original,Color floor)
        {
            return new Color(Mathf.Max(floor.r,original.r*.70f),Mathf.Max(floor.g,original.g*.70f),Mathf.Max(floor.b,original.b*.70f));
        }
        void EndVampireLighting()
        {
            exposure=0;
            if(!vampireLighting)return;
            vampireLighting=false;
            if(vampireApplied)
            {
                if(RenderSettings.ambientSkyColor==vampireWrittenSky)RenderSettings.ambientSkyColor=vampireAmbientSky;
                if(RenderSettings.ambientEquatorColor==vampireWrittenEquator)RenderSettings.ambientEquatorColor=vampireAmbientEquator;
                if(RenderSettings.ambientGroundColor==vampireWrittenGround)RenderSettings.ambientGroundColor=vampireAmbientGround;
            }
            vampireApplied=false;
            RestoreVampireLight();
        }
        void RestoreVampireLight()
        {
            if(vampireLight&&vampireLightApplied)
            {
                if(Mathf.Approximately(vampireLight.intensity,vampireWrittenSunIntensity))vampireLight.intensity=vampireSunIntensity;
                if(Quaternion.Angle(vampireLight.transform.rotation,vampireWrittenSunRotation)<=.01f)vampireLight.transform.rotation=vampireSunRotation;
                if(vampireLight.color==vampireWrittenSunColor)vampireLight.color=vampireSunColor;
            }
            vampireLight=null;
            vampireLightApplied=false;
        }
        void DestroyVampireShade(){EndVampireLighting();}
    }
}
