using System.Collections.Generic;
using DeadlyJenga.Core;
using DeadlyJenga.CandyArt;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.Presentation
{
    public enum FantasyMapTheme { Honeycomb, Carnival, CandyGlass }

    [DefaultExecutionOrder(1200)]
    public sealed partial class FantasyMapArt : MonoBehaviour
    {
        public FantasyMapTheme theme;
        public bool texturedCarnivalSky;
        public bool texturedEnvironmentSky;
        public Material[] blockMaterials;
        public Material groundMaterial, cloudMaterial, goldMaterial, pinkMaterial, blueMaterial, darkMaterial, skyMaterial;
        public Renderer ground;
        public float halfWidth=190, halfDepth=185;
        readonly List<Mesh> ownedMeshes=new();
        readonly List<GameObject> ownedObjects=new();
        readonly Dictionary<Renderer,Material[]> originals=new();
        readonly Dictionary<MeshFilter,Mesh> originalShapes=new();
        CandyEnvironment environment;
        CandySceneSkins sceneSkins;
        bool oldLighting,oldWaterOff,oldSandOff,oldSkinsEnabled,active;
        Material oldSky;
        Color oldAmbientSky,oldAmbientEquator,oldAmbientGround,oldSunColor;
        float oldAmbientIntensity,oldSunIntensity;
        Quaternion oldSunRotation;
        AmbientMode oldAmbientMode;
        int revision=-1;
        float retry;

        void LateUpdate()
        {
            if(!(Services.World is ISyncedWorld world)||world.Root!=transform)return;
            if(!active)Activate();
            if(!active)return;
            if(!environment&&Time.unscaledTime>retry){retry=Time.unscaledTime+1;BindEnvironment();}
            if(environment){if(environment.waterRenderer)environment.waterRenderer.forceRenderingOff=true;if(environment.sandRenderer)environment.sandRenderer.forceRenderingOff=true;}
            UpdateLighting();
            if(world.Revision!=revision&&Services.World.Blocks.Count>0)
            {
                ClearBodyArt();revision=world.Revision;
                int count=0;
                foreach(var body in Services.World.Blocks)
                {
                    if(!body)continue;
                    var climb=body.GetComponent<IClimbable>();var visual=climb?.Visual;
                    if(!visual)continue;
                    foreach(var renderer in visual.GetComponentsInChildren<MeshRenderer>())
                    {
                        if(renderer.name=="Fantasy decorations")continue;
                        if(!originals.ContainsKey(renderer))originals[renderer]=renderer.sharedMaterials;
                        var replacement=new Material[renderer.sharedMaterials.Length];
                        for(int i=0;i<replacement.Length;i++)replacement[i]=blockMaterials[i%blockMaterials.Length];
                        renderer.sharedMaterials=replacement;
                        SoftenEdges(renderer,body);
                    }
                    if(theme!=FantasyMapTheme.CandyGlass)Decorate(body,visual,count++);
                }
            }
        }
        void Activate()
        {
            if(blockMaterials==null||blockMaterials.Length<4||!groundMaterial||!skyMaterial)return;
            active=true;oldSky=RenderSettings.skybox;oldAmbientMode=RenderSettings.ambientMode;
            oldAmbientSky=RenderSettings.ambientSkyColor;oldAmbientEquator=RenderSettings.ambientEquatorColor;
            oldAmbientGround=RenderSettings.ambientGroundColor;oldAmbientIntensity=RenderSettings.ambientIntensity;
            BindEnvironment();BeginLighting();
            RenderSettings.skybox=skyMaterial;RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=theme==FantasyMapTheme.Honeycomb?new Color(.91f,.74f,.45f):theme==FantasyMapTheme.Carnival?new Color(.68f,.73f,1):new Color(.66f,.83f,1);
            RenderSettings.ambientEquatorColor=theme==FantasyMapTheme.Honeycomb?new Color(.62f,.40f,.17f):theme==FantasyMapTheme.Carnival?new Color(.87f,.62f,.75f):new Color(.64f,.76f,.88f);
            RenderSettings.ambientGroundColor=theme==FantasyMapTheme.Honeycomb?new Color(.28f,.15f,.055f):new Color(.32f,.35f,.49f);RenderSettings.ambientIntensity=1;
            if(ground){originals[ground]=ground.sharedMaterials;ground.sharedMaterial=groundMaterial;}
            if(theme==FantasyMapTheme.Honeycomb){if(!texturedEnvironmentSky)BuildHive();}else BuildCloud();
            if(theme==FantasyMapTheme.Carnival&&!texturedCarnivalSky)BuildCarnival();
        }
        void BindEnvironment()
        {
            environment=FindFirstObjectByType<CandyEnvironment>();if(!environment)return;
            oldLighting=environment.applyLighting;environment.applyLighting=false;
            sceneSkins=environment.GetComponent<CandySceneSkins>();
            if(sceneSkins){oldSkinsEnabled=sceneSkins.enabled;sceneSkins.enabled=false;}
            if(environment.waterRenderer)oldWaterOff=environment.waterRenderer.forceRenderingOff;
            if(environment.sandRenderer)oldSandOff=environment.sandRenderer.forceRenderingOff;
            if(environment.sun)
            {
                oldSunColor=environment.sun.color;oldSunIntensity=environment.sun.intensity;oldSunRotation=environment.sun.transform.rotation;
                environment.sun.color=theme==FantasyMapTheme.Honeycomb?new Color(1,.95f,.83f):theme==FantasyMapTheme.Carnival?new Color(1,.94f,.90f):new Color(1,.98f,.96f);
                environment.sun.intensity=theme==FantasyMapTheme.Honeycomb?1.65f:theme==FantasyMapTheme.Carnival?1.45f:1.7f;
                environment.sun.transform.rotation=theme==FantasyMapTheme.Honeycomb?Quaternion.Euler(48,140,0):theme==FantasyMapTheme.Carnival?Quaternion.Euler(18,180,0):Quaternion.Euler(52,140,0);
            }
        }
        void ClearBodyArt()
        {
            foreach(var pair in originalShapes)if(pair.Key)pair.Key.sharedMesh=pair.Value;originalShapes.Clear();
            foreach(var pair in originals)if(pair.Key&&pair.Key!=ground)pair.Key.sharedMaterials=pair.Value;
            var remove=new List<Renderer>();foreach(var pair in originals)if(pair.Key!=ground)remove.Add(pair.Key);
            foreach(var key in remove)originals.Remove(key);
            foreach(var go in bodyObjects)if(go)Release(go);bodyObjects.Clear();
            foreach(var mesh in bodyMeshes)if(mesh)Release(mesh);bodyMeshes.Clear();
        }
        void OnDisable()
        {
            if(!active)return;
            EndLighting();ClearBodyArt();foreach(var pair in originals)if(pair.Key)pair.Key.sharedMaterials=pair.Value;originals.Clear();
            foreach(var go in ownedObjects)if(go)Release(go);ownedObjects.Clear();
            foreach(var mesh in ownedMeshes)if(mesh)Release(mesh);ownedMeshes.Clear();
            if(environment)
            {
                environment.applyLighting=oldLighting;
                if(environment.waterRenderer)environment.waterRenderer.forceRenderingOff=oldWaterOff;
                if(environment.sandRenderer)environment.sandRenderer.forceRenderingOff=oldSandOff;
                if(environment.sun){environment.sun.color=oldSunColor;environment.sun.intensity=oldSunIntensity;environment.sun.transform.rotation=oldSunRotation;}
            }
            if(sceneSkins)sceneSkins.enabled=oldSkinsEnabled;
            RenderSettings.skybox=oldSky;RenderSettings.ambientMode=oldAmbientMode;
            RenderSettings.ambientSkyColor=oldAmbientSky;RenderSettings.ambientEquatorColor=oldAmbientEquator;
            RenderSettings.ambientGroundColor=oldAmbientGround;RenderSettings.ambientIntensity=oldAmbientIntensity;
            active=false;revision=-1;environment=null;sceneSkins=null;
        }
        static void Release(Object item){if(!item)return;if(Application.isPlaying)Destroy(item);else DestroyImmediate(item);}
        GameObject Model(string name,ArtMesh builder,Transform parent,Material[] materials,bool body=false)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            var mesh=builder.Finish(name);go.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterials=materials;renderer.shadowCastingMode=ShadowCastingMode.Off;
            if(body){bodyObjects.Add(go);bodyMeshes.Add(mesh);}else{ownedObjects.Add(go);ownedMeshes.Add(mesh);}return go;
        }
    }
}
