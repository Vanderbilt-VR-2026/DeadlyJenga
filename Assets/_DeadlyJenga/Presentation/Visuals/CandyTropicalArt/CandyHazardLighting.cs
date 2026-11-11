using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyArt
{
    [DefaultExecutionOrder(800)]
    public sealed partial class CandyHazardLighting : MonoBehaviour
    {
        const int VariantLimit=128,RendererLimit=8192;
        const float OutageFogEnd=4.5f;
        static readonly Color OutageFogColor=new Color(.006f,.004f,.016f);
        sealed class Look { public Material[] before,applied;public int seen; }
        readonly Dictionary<Renderer,Look> looks=new Dictionary<Renderer,Look>();
        readonly Dictionary<(Material,int,Vector3,Vector3),Material> neon=new Dictionary<(Material,int,Vector3,Vector3),Material>();
        readonly Dictionary<int,int> selected=new Dictionary<int,int>();
        readonly List<Renderer> stale=new List<Renderer>();
        readonly List<Material> slots=new List<Material>();
        Shader shader;float scanAt,exposure;bool lit;int generation;
        Light sun,outageSun;Material darkSky,baselineSky,vampireSky;Vector4 originalSunDirection,writtenSunDirection;
        Color baselineSkyColor,baselineEquator,baselineGround,writtenSky,writtenEquator,writtenGround;
        float baselineAmbient,writtenAmbient,baselineIntensity,writtenIntensity,baselineOutage;
        AmbientMode baselineMode;bool globalsWritten;
        bool savedFog; FogMode savedFogMode; Color savedFogColor; float savedFogStart,savedFogEnd;
        Renderer darkWater;MaterialPropertyBlock waterTint;
        static readonly string[] WaterNames={"_BaseColor","_Accent","_Shallow","_Deep","_Reflection","_Horizon"};
        static readonly Color[] WaterColors={new Color(.025f,.075f,.12f),new Color(.035f,.28f,.32f),new Color(.035f,.19f,.23f),new Color(.008f,.025f,.065f),new Color(.10f,.17f,.28f),new Color(.08f,.12f,.23f)};
        readonly Color[] waterBaseline=new Color[6];bool waterWritten;
        void Awake(){waterTint=new MaterialPropertyBlock();shader=Resources.Load<Shader>("CandyScenery/CandyNeon");}
        void LateUpdate()
        {
            DisasterFrame outage=null,vampire=null;
            foreach(var state in DisasterStates.Active){var f=state.Capture();if(f==null||f.warning>0)continue;if(f.kind=="PowerOutage")outage=f;if(f.kind=="Vampire")vampire=f;}
            if(MatchRuntime.Current?.Frame.phase==RoundPhase.Victory||MatchRuntime.Current?.Frame.phase==RoundPhase.Results){outage=null;vampire=null;}
            var environment=GetComponent<CandyEnvironment>();sun=environment?environment.sun:null;
            if(outage!=null)
            {
                EndVampireLighting();RestoreVampireSun();
                ApplyOutage();DarkenOcean(environment);
                if(Time.unscaledTime>=scanAt){scanAt=Time.unscaledTime+.5f;Outage(outage);}
            }
            else
            {
                EndOutage();
                if(vampire!=null)Vampire(vampire);else{EndVampireLighting();RestoreVampireSun();}
            }
            exposure=0;Shader.SetGlobalFloat("_CandyHeat",0);
        }
        void ApplyOutage()
        {
            if(!lit)
            {
                lit=true;baselineOutage=Shader.GetGlobalFloat("_CandyOutage");scanAt=0;
                savedFog=RenderSettings.fog;savedFogMode=RenderSettings.fogMode;savedFogColor=RenderSettings.fogColor;
                savedFogStart=RenderSettings.fogStartDistance;savedFogEnd=RenderSettings.fogEndDistance;
            }
            if(!globalsWritten||RenderSettings.skybox!=darkSky)baselineSky=RenderSettings.skybox;
            if(!globalsWritten||RenderSettings.ambientSkyColor!=writtenSky)baselineSkyColor=RenderSettings.ambientSkyColor;
            if(!globalsWritten||RenderSettings.ambientEquatorColor!=writtenEquator)baselineEquator=RenderSettings.ambientEquatorColor;
            if(!globalsWritten||RenderSettings.ambientGroundColor!=writtenGround)baselineGround=RenderSettings.ambientGroundColor;
            if(!globalsWritten||!Mathf.Approximately(RenderSettings.ambientIntensity,writtenAmbient))baselineAmbient=RenderSettings.ambientIntensity;
            if(!globalsWritten||RenderSettings.ambientMode!=AmbientMode.Trilight)baselineMode=RenderSettings.ambientMode;
            if(Shader.GetGlobalFloat("_CandyOutage")!=1)baselineOutage=Shader.GetGlobalFloat("_CandyOutage");
            if(outageSun!=sun){RestoreOutageSun();outageSun=sun;if(outageSun)baselineIntensity=outageSun.intensity;}
            else if(outageSun&&(!globalsWritten||!Mathf.Approximately(outageSun.intensity,writtenIntensity)))baselineIntensity=outageSun.intensity;
            if(!darkSky){var skyShader=Resources.Load<Shader>("CandyScenery/CandyScenicSky");if(skyShader){darkSky=new Material(skyShader);darkSky.SetColor("_Zenith",new Color(.005f,.004f,.013f));darkSky.SetColor("_Horizon",OutageFogColor);darkSky.SetColor("_Cloud",new Color(.009f,.007f,.022f));darkSky.SetColor("_SunColor",Color.black);darkSky.SetFloat("_CloudCover",.2f);darkSky.SetFloat("_Stars",.04f);}}
            if(darkSky)RenderSettings.skybox=darkSky;
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientIntensity=writtenAmbient=1;
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;
            RenderSettings.fogColor=OutageFogColor;RenderSettings.fogStartDistance=.65f;RenderSettings.fogEndDistance=OutageFogEnd;
            RenderSettings.ambientSkyColor=writtenSky=new Color(.014f,.017f,.030f);
            RenderSettings.ambientEquatorColor=writtenEquator=new Color(.009f,.012f,.024f);
            RenderSettings.ambientGroundColor=writtenGround=new Color(.007f,.006f,.014f);
            if(outageSun)outageSun.intensity=writtenIntensity=Mathf.Max(.025f,baselineIntensity*.018f);
            Shader.SetGlobalFloat("_CandyOutage",1);globalsWritten=true;
        }
        void RestoreOutageSun(){if(outageSun&&Mathf.Approximately(outageSun.intensity,writtenIntensity))outageSun.intensity=baselineIntensity;outageSun=null;}
        void EndOutage()
        {
            if(!lit)return;lit=false;
            if(RenderSettings.fogMode==FogMode.Linear && Mathf.Approximately(RenderSettings.fogEndDistance,OutageFogEnd))
            {
                RenderSettings.fog=savedFog;RenderSettings.fogMode=savedFogMode;RenderSettings.fogColor=savedFogColor;
                RenderSettings.fogStartDistance=savedFogStart;RenderSettings.fogEndDistance=savedFogEnd;
            }
            if(RenderSettings.skybox==darkSky)RenderSettings.skybox=baselineSky;
            if(RenderSettings.ambientSkyColor==writtenSky)RenderSettings.ambientSkyColor=baselineSkyColor;
            if(RenderSettings.ambientEquatorColor==writtenEquator)RenderSettings.ambientEquatorColor=baselineEquator;
            if(RenderSettings.ambientGroundColor==writtenGround)RenderSettings.ambientGroundColor=baselineGround;
            if(Mathf.Approximately(RenderSettings.ambientIntensity,writtenAmbient))RenderSettings.ambientIntensity=baselineAmbient;
            if(RenderSettings.ambientMode==AmbientMode.Trilight)RenderSettings.ambientMode=baselineMode;
            if(Shader.GetGlobalFloat("_CandyOutage")==1)Shader.SetGlobalFloat("_CandyOutage",baselineOutage);
            RestoreOutageSun();RestoreOcean();Restore();globalsWritten=false;
            // Another visual owner may retain a variant. Retire it to its source shader instead of invalidating it.
            foreach(var pair in neon)if(pair.Key.Item1&&pair.Value){pair.Value.shader=pair.Key.Item1.shader;pair.Value.CopyPropertiesFromMaterial(pair.Key.Item1);}
        }
        Color WaterValue(Renderer r,int i){return waterTint.HasColor(WaterNames[i])?waterTint.GetColor(WaterNames[i]):r.sharedMaterial&&r.sharedMaterial.HasProperty(WaterNames[i])?r.sharedMaterial.GetColor(WaterNames[i]):Color.black;}
        void DarkenOcean(CandyEnvironment environment)
        {
            var next=environment?environment.waterRenderer:null;
            if(next!=darkWater){RestoreOcean();darkWater=next;}
            if(!darkWater)return;darkWater.GetPropertyBlock(waterTint);
            for(int i=0;i<6;i++){var current=WaterValue(darkWater,i);if(!waterWritten||current!=WaterColors[i])waterBaseline[i]=current;waterTint.SetColor(WaterNames[i],WaterColors[i]);}
            darkWater.SetPropertyBlock(waterTint);waterWritten=true;
        }
        void RestoreOcean()
        {
            if(darkWater&&waterWritten){darkWater.GetPropertyBlock(waterTint);for(int i=0;i<6;i++)if(waterTint.HasColor(WaterNames[i])&&waterTint.GetColor(WaterNames[i])==WaterColors[i])waterTint.SetColor(WaterNames[i],waterBaseline[i]);darkWater.SetPropertyBlock(waterTint);}
            darkWater=null;waterWritten=false;
        }
        void Outage(DisasterFrame frame)
        {
            if(!shader)return;generation++;selected.Clear();if(frame.blocks!=null)foreach(int index in frame.blocks)selected[index]=1;
            var world=Services.World;float reach=frame.values!=null&&frame.values.Length>1?frame.values[1]:9;
            if(world!=null)for(int i=0;i<world.Blocks.Count;i++){var body=world.Blocks[i];var visual=body?body.GetComponent<IClimbable>()?.Visual:null;if(visual)foreach(var r in visual.GetComponentsInChildren<Renderer>())Neon(r,selected.ContainsKey(i)?1:0,reach);}
            foreach(var mover in Tracked.Movers)if(mover is Component c&&c){var actor=c.GetComponent<IModeActor>();if(actor?.Visual)foreach(var r in actor.Visual.GetComponentsInChildren<Renderer>())Neon(r,4,reach);}
            foreach(var flyer in Tracked.Flyers)if(flyer is Component c&&c)foreach(var r in c.GetComponentsInChildren<Renderer>())Neon(r,4,reach);
            stale.Clear();foreach(var pair in looks)if(!pair.Key||pair.Value.seen!=generation){RestoreLook(pair.Key,pair.Value);stale.Add(pair.Key);}foreach(var r in stale)looks.Remove(r);
        }
        internal Material ResolveSourceMaterial(Material candidate)
        {
            foreach(var pair in neon)if(pair.Value==candidate)return pair.Key.Item1;
            return candidate;
        }
        static bool Eligible(Material m){return m&&m.renderQueue<2500&&m.HasProperty("_BaseColor")&&!m.HasProperty("_BurnAmount")&&!m.HasProperty("_Health")&&!m.HasProperty("_Wetness")&&!m.HasProperty("_Dissolve");}
        void Neon(Renderer r,int style,float reach)
        {
            if(!r||(!(r is MeshRenderer)&&!(r is SkinnedMeshRenderer))||r.GetComponent<TextMesh>())return;
            r.GetSharedMaterials(slots);if(!looks.TryGetValue(r,out var look)){if(looks.Count>=RendererLimit)return;look=new Look{before=slots.ToArray(),applied=new Material[slots.Count]};looks.Add(r,look);}
            look.seen=generation;
            if(look.before.Length!=slots.Count){RestoreLook(r,look);r.GetSharedMaterials(slots);look.before=slots.ToArray();look.applied=new Material[slots.Count];}
            bool changed=false;
            for(int i=0;i<slots.Count;i++)
            {
                var source=slots[i]==look.applied[i]?look.before[i]:slots[i];look.before[i]=source;
                var center=style==4?Vector3.zero:r.localBounds.center;var size=style==4?Vector3.one:r.localBounds.size;
                if(!Eligible(source)){if(look.applied[i]&&slots[i]==look.applied[i]){slots[i]=source;changed=true;}look.applied[i]=null;continue;}
                var key=(source,style,center,size);
                if(!neon.TryGetValue(key,out var material)){if(neon.Count>=VariantLimit){if(slots[i]==look.applied[i]){slots[i]=source;changed=true;}look.applied[i]=null;continue;}material=new Material(shader);neon.Add(key,material);}
                material.shader=shader;material.SetColor("_BaseColor",source.GetColor("_BaseColor"));
                if(source.HasProperty("_BaseMap")){material.SetTexture("_BaseMap",source.GetTexture("_BaseMap"));material.SetTextureScale("_BaseMap",source.GetTextureScale("_BaseMap"));material.SetTextureOffset("_BaseMap",source.GetTextureOffset("_BaseMap"));}
                bool normalMapped=source.HasProperty("_BumpMap")&&source.IsKeywordEnabled("_NORMALMAP")&&source.GetTexture("_BumpMap");
                material.SetFloat("_BumpScale",normalMapped?(source.HasProperty("_BumpScale")?source.GetFloat("_BumpScale"):1):0);
                if(normalMapped)
                {
                    material.SetTexture("_BumpMap",source.GetTexture("_BumpMap"));
                    material.SetTextureScale("_BumpMap",source.GetTextureScale("_BumpMap"));
                    material.SetTextureOffset("_BumpMap",source.GetTextureOffset("_BumpMap"));
                }
                material.SetFloat("_Smoothness",source.HasProperty("_Smoothness")?source.GetFloat("_Smoothness"):.6f);
                material.SetFloat("_Metallic",source.HasProperty("_Metallic")?source.GetFloat("_Metallic"):0f);
                material.SetFloat("_Filled",style==1?1:0);material.SetFloat("_Character",style==4?1:0);material.SetFloat("_Rim",reach);material.SetVector("_BoundsCenter",center);material.SetVector("_BoundsSize",size);material.enableInstancing=true;
                if(slots[i]!=material){slots[i]=material;changed=true;}look.applied[i]=material;
            }
            if(changed)r.SetSharedMaterials(slots);
        }
        void RestoreLook(Renderer r,Look look)
        {
            if(!r)return;r.GetSharedMaterials(slots);bool changed=false;for(int i=0;i<slots.Count&&i<look.applied.Length;i++)if(look.applied[i]&&slots[i]==look.applied[i]){slots[i]=look.before[i];changed=true;}if(changed)r.SetSharedMaterials(slots);
        }
        void Restore(){foreach(var pair in looks)RestoreLook(pair.Key,pair.Value);looks.Clear();}
        void Vampire(DisasterFrame frame)
        {
            var view=Services.Session?.ActiveCamera;if(!view){EndVampireLighting();RestoreVampireSun();return;}
            float elevation=frame.values!=null&&frame.values.Length>2?frame.values[2]:45;float angle=elevation*Mathf.Deg2Rad;Vector3 direction=new Vector3(.6f*Mathf.Cos(angle),Mathf.Sin(angle),.8f*Mathf.Cos(angle));
            BeginVampireLighting();if(sun)sun.transform.rotation=Quaternion.LookRotation(-direction,Mathf.Abs(direction.y)>.99f?Vector3.forward:Vector3.up);
            var sky=RenderSettings.skybox;if(vampireSky!=sky)RestoreVampireSun();
            if(sky&&sky.HasProperty("_SunDirection")){var current=sky.GetVector("_SunDirection");if(!vampireSky||current!=writtenSunDirection)originalSunDirection=current;vampireSky=sky;sky.SetVector("_SunDirection",direction);writtenSunDirection=sky.GetVector("_SunDirection");}
            exposure=0;DrawVampireShade(view);
        }
        void RestoreVampireSun(){if(vampireSky&&vampireSky.GetVector("_SunDirection")==writtenSunDirection)vampireSky.SetVector("_SunDirection",originalSunDirection);vampireSky=null;}
        void OnDisable(){EndVampireLighting();RestoreVampireSun();EndOutage();Shader.SetGlobalFloat("_CandyHeat",0);}
        void OnDestroy(){OnDisable();DestroyVampireShade();foreach(var material in neon.Values)if(material)Destroy(material);if(darkSky)Destroy(darkSky);}
    }
}
