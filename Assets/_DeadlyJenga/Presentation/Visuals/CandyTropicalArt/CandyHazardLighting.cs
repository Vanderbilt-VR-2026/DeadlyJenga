using System.Collections.Generic;
using DeadlyJenga.Core;
using DeadlyJenga.CandyAudio;
using UnityEngine;

namespace DeadlyJenga.CandyArt
{
    [DefaultExecutionOrder(800)]
    public sealed partial class CandyHazardLighting : MonoBehaviour
    {
        readonly Dictionary<Renderer,Material> originals=new Dictionary<Renderer,Material>();
        readonly Dictionary<Renderer,MaterialPropertyBlock> originalProperties=new Dictionary<Renderer,MaterialPropertyBlock>();
        readonly Dictionary<(Material,int,Vector3,Vector3),Material> neon=new Dictionary<(Material,int,Vector3,Vector3),Material>();
        Renderer darkWater;MaterialPropertyBlock waterBefore,waterTint;Shader shader;float scanAt,exposure,exposureAt;bool lit,previousExposed;Light sun;float intensity;Material darkSky,vampireSky;Vector4 originalSunDirection;
        void Awake(){shader=Resources.Load<Shader>("CandyScenery/CandyNeon");}
        void LateUpdate()
        {
            DisasterFrame outage=null,vampire=null;foreach(var state in DisasterStates.Active){var f=state.Capture();if(f==null||f.warning>0)continue;if(f.kind=="PowerOutage")outage=f;if(f.kind=="Vampire")vampire=f;}
            if(MatchRuntime.Current?.Frame.phase==RoundPhase.Victory||MatchRuntime.Current?.Frame.phase==RoundPhase.Results){outage=null;vampire=null;}
            if(outage!=null)
            {
                DarkenOcean();
                RenderSettings.ambientSkyColor=new Color(.013f,.003f,.04f);RenderSettings.ambientEquatorColor=new Color(.025f,.005f,.06f);RenderSettings.ambientGroundColor=new Color(.008f,.002f,.02f);
                if(!darkSky){var sky=Resources.Load<Shader>("CandyScenery/CandyScenicSky");if(sky){darkSky=new Material(sky);darkSky.SetColor("_Zenith",new Color(.009f,.001f,.025f));darkSky.SetColor("_Horizon",new Color(.025f,.003f,.05f));darkSky.SetColor("_Cloud",Color.black);darkSky.SetColor("_SunColor",Color.black);darkSky.SetFloat("_CloudCover",0);darkSky.SetFloat("_Stars",0);}}if(darkSky)RenderSettings.skybox=darkSky;
                if(!sun){var env=GetComponent<CandyEnvironment>();sun=env?env.sun:null;}if(sun){if(!lit)intensity=sun.intensity;sun.intensity=.025f;}lit=true;
                Shader.SetGlobalFloat("_CandyOutage",1);
                if(Time.unscaledTime>=scanAt){scanAt=Time.unscaledTime+.5f;Outage(outage);}
            }
            else if(lit){RestoreOcean();if(sun)sun.intensity=intensity;lit=false;Restore();Shader.SetGlobalFloat("_CandyOutage",0);}
            if(vampire!=null)Vampire(vampire);else {EndVampireLighting();RestoreVampireSun();exposure=Mathf.MoveTowards(exposure,0,Time.deltaTime*2);}
            Shader.SetGlobalFloat("_CandyHeat",exposure);
        }
        void DarkenOcean()
        {
            var environment=GetComponent<CandyEnvironment>();if(!environment||!environment.waterRenderer)return;
            if(!darkWater){darkWater=environment.waterRenderer;waterBefore=new MaterialPropertyBlock();waterTint=new MaterialPropertyBlock();darkWater.GetPropertyBlock(waterBefore);}
            darkWater.GetPropertyBlock(waterTint);waterTint.SetColor("_BaseColor",new Color(.009f,.002f,.025f));waterTint.SetColor("_Accent",new Color(.025f,.006f,.07f));waterTint.SetColor("_Shallow",new Color(.018f,.006f,.045f));waterTint.SetColor("_Deep",new Color(.004f,.001f,.014f));waterTint.SetColor("_Reflection",new Color(.022f,.006f,.055f));waterTint.SetColor("_Horizon",new Color(.025f,.003f,.05f));darkWater.SetPropertyBlock(waterTint);
        }
        void RestoreOcean(){if(darkWater)darkWater.SetPropertyBlock(waterBefore);darkWater=null;}
        void Outage(DisasterFrame frame)
        {
            var world=Services.World;if(world==null||!shader)return;var selected=new Dictionary<int,int>();if(frame.blocks!=null)for(int i=0;i<frame.blocks.Length;i++)selected[frame.blocks[i]]=i%3+1;
            for(int i=0;i<world.Blocks.Count;i++)
            {
                var body=world.Blocks[i];var visual=body?body.GetComponent<IClimbable>()?.Visual:null;if(!visual)continue;
                int bulb=selected.TryGetValue(i,out int selection)?selection:0;foreach(var renderer in visual.GetComponentsInChildren<Renderer>())Neon(renderer,bulb,frame.values!=null&&frame.values.Length>1?frame.values[1]:9);
            }
            foreach(var mover in Tracked.Movers)if(mover is Component c&&c){var actor=c.GetComponent<IModeActor>();if(actor?.Visual)foreach(var r in actor.Visual.GetComponentsInChildren<Renderer>())Neon(r,4,1);}
            foreach(var flyer in Tracked.Flyers)if(flyer is Component c&&c)foreach(var r in c.GetComponentsInChildren<Renderer>())if(!(r is ParticleSystemRenderer))Neon(r,4,1);
        }
        void Neon(Renderer r,int style,float rim)
        {
            if(!originals.TryGetValue(r,out var original)){original=r.sharedMaterial;originals[r]=original;var saved=new MaterialPropertyBlock();r.GetPropertyBlock(saved);originalProperties[r]=saved;}if(!original)return;
            var key=(original,style,r.localBounds.center,r.localBounds.size);if(!neon.TryGetValue(key,out var material))
            {
                material=new Material(shader);Color c=original.HasProperty("_BaseColor")?original.GetColor("_BaseColor"):Color.white;
                if(style==1)c=new Color(.65f,.06f,1);if(style==2)c=new Color(1,.3f,.025f);if(style==3)c=new Color(.24f,1,.035f);
                material.SetColor("_BaseColor",c);material.SetFloat("_Filled",style>0?1:0);material.SetVector("_BoundsCenter",r.localBounds.center);material.SetVector("_BoundsSize",r.localBounds.size);material.enableInstancing=true;neon[key]=material;
            }
            material.SetFloat("_Rim",rim);r.sharedMaterial=material;
        }
        void Vampire(DisasterFrame frame)
        {
            var view=Services.Session?.ActiveCamera;if(!view)return;float elevation=frame.values!=null&&frame.values.Length>2?frame.values[2]:45,delay=frame.values!=null&&frame.values.Length>1?frame.values[1]:.35f;
            float angle=elevation*Mathf.Deg2Rad;Vector3 direction=new Vector3(.6f*Mathf.Cos(angle),Mathf.Sin(angle),.8f*Mathf.Cos(angle));
            var environment=GetComponent<CandyEnvironment>();if(!sun&&environment)sun=environment.sun;
            BeginVampireLighting();
            if(sun)sun.transform.rotation=Quaternion.LookRotation(-direction,Mathf.Abs(direction.y)>.99f?Vector3.forward:Vector3.up);
            var sky=RenderSettings.skybox;
            if(sky&&sky.HasProperty("_SunDirection"))
            {if(vampireSky!=sky){RestoreVampireSun();vampireSky=sky;originalSunDirection=sky.GetVector("_SunDirection");}sky.SetVector("_SunDirection",direction);}
            bool exposed=!Physics.Raycast(view.transform.position,direction,300,1<<Layers.Block,QueryTriggerInteraction.Ignore);
            if(exposed!=previousExposed){exposureAt=Time.time;previousExposed=exposed;}
            if(Time.time-exposureAt>delay)exposure=Mathf.MoveTowards(exposure,exposed?1:0,Time.deltaTime*2);
            DrawVampireShade(view);
            if(exposure>.1f&&CandyAudioEngine.Instance)CandyAudioEngine.Instance.Loop(CandySound.VampireSizzle,view.transform,exposure*.5f,true);
        }
        void Restore(){foreach(var pair in originals)if(pair.Key){pair.Key.sharedMaterial=pair.Value;pair.Key.SetPropertyBlock(originalProperties[pair.Key]);}originals.Clear();originalProperties.Clear();}
        void RestoreVampireSun(){if(vampireSky)vampireSky.SetVector("_SunDirection",originalSunDirection);vampireSky=null;}
        void OnDisable(){EndVampireLighting();RestoreOcean();Restore();RestoreVampireSun();if(lit&&sun)sun.intensity=intensity;lit=false;Shader.SetGlobalFloat("_CandyOutage",0);Shader.SetGlobalFloat("_CandyHeat",0);}
        void OnDestroy(){DestroyVampireShade();RestoreOcean();Restore();foreach(var material in neon.Values)if(material)Destroy(material);if(darkSky)Destroy(darkSky);}
    }
}

