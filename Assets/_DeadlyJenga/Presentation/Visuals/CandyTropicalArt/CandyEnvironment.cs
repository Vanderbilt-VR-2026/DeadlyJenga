using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyArt
{
    [ExecuteAlways]
    public sealed class CandyEnvironment : MonoBehaviour
    {
        public CandyArtProfile profile;
        public Transform foundation;
        public Renderer waterRenderer, sandRenderer;
        public Light sun;
        public bool applyLighting;
        MaterialPropertyBlock properties;
        DeadlyJenga.Core.IWorldQuery activeWorld;
        bool openWater,coastReady;
        CandyCatwalksCoast catwalksCoast;
        IWorldQuery coastWorld;
        Shader coastShader;
        static readonly int CoastEnabled = Shader.PropertyToID("_CatwalksCoast");
        internal DeadlyJenga.Core.IWorldQuery WorldOverride { get; set; }

        internal static CandyEnvironment ForMap(Component map)
        {
            if (!map) return null;
            foreach (var environment in FindObjectsByType<CandyEnvironment>(FindObjectsSortMode.None))
                if (environment.WorldOverride == null && environment.gameObject.scene == map.gameObject.scene)
                    return environment;
            return null;
        }
        void OnEnable() { if(Application.isPlaying){if(!GetComponent<CandyWaveContactVisual>())gameObject.AddComponent<CandyWaveContactVisual>();if(!GetComponent<CandyTsunamiVisuals>())gameObject.AddComponent<CandyTsunamiVisuals>();if(!GetComponent<CandyDisasterVisuals>())gameObject.AddComponent<CandyDisasterVisuals>();if(!GetComponent<CandySceneSkins>())gameObject.AddComponent<CandySceneSkins>();} Refresh(); }
        void Update() { if(Application.isPlaying){coastReady=true;if(!GetComponent<CandyHazardLighting>())gameObject.AddComponent<CandyHazardLighting>();if(!GetComponent<CandyWaveFeedback>())gameObject.AddComponent<CandyWaveFeedback>();}Refresh(); }
        void OnDisable()
        {
            coastReady=false;
            ReleaseCoast();
        }

        void ReleaseCoast()
        {
            catwalksCoast?.Dispose();
            catwalksCoast=null;
            coastWorld=null;
            if(!waterRenderer) return;
            properties ??=new MaterialPropertyBlock();
            waterRenderer.GetPropertyBlock(properties);
            properties.SetFloat(CoastEnabled,0);
            waterRenderer.SetPropertyBlock(properties);
        }

        bool UsesCastleCoast => Application.isPlaying && WorldOverride==null &&
            MatchChoices.MapId is "castle" or "mode-castle-gaps" or "mode-castle-catwalks" or "mode-castle-four-keeps";

        void RefreshCoast(IWorldQuery world,CandySandbar terrain,Vector3 origin,float waterLevel)
        {
            bool enabled=UsesCastleCoast && coastReady && isActiveAndEnabled && !openWater &&
                terrain && terrain.HeightMap && world!=null;
            if(!enabled || !ReferenceEquals(coastWorld,world))
            {
                if(catwalksCoast!=null) ReleaseCoast();
                if(!enabled) return;
                coastWorld=world;
                if(!coastShader) coastShader=Resources.Load<Shader>("CandyScenery/CandyCatwalksCoast");
                if(coastShader) catwalksCoast=new CandyCatwalksCoast(transform,terrain,origin,waterLevel,coastShader);
            }
            catwalksCoast?.Animate();
        }

        public void Refresh()
        {
            if(!profile || !waterRenderer || !sandRenderer) return;
            var world=WorldOverride??DeadlyJenga.Core.Services.World;
            if (WorldOverride==null && world is Component candidate && candidate.gameObject.scene!=gameObject.scene)
            {
                if(catwalksCoast!=null) ReleaseCoast();
                return;
            }
            if(Application.isPlaying && world!=null && (!ReferenceEquals(activeWorld,world) ||
                (openWater && (!foundation || !foundation.gameObject.activeInHierarchy))))
            {
                activeWorld=world;
                openWater=false;
                if(world is MonoBehaviour map)
                {
                    float area=0;Transform floor=null;
                    float ceiling=world.WaterHeight(world.Center)+Mathf.Max(6,world.StepHeight*3);
                    foreach(var col in map.GetComponentsInChildren<Collider>())
                        if(!col.isTrigger && !col.attachedRigidbody && (col.gameObject.layer==DeadlyJenga.Core.Layers.Surface||col.gameObject.layer==0))
                        {var bounds=col.bounds;float surfaceArea=bounds.size.x*bounds.size.z;if(bounds.max.y<=ceiling&&surfaceArea>area){area=surfaceArea;floor=col.transform;}}
                    var scenery=map.GetComponent<DeadlyJenga.Presentation.VillageFlatScenery>();
                    openWater=scenery&&scenery.openWater;
                    foundation=floor;
                    var baseline=GetComponent<CandyArtBaseline>();if(baseline)baseline.map=map;
                    transform.position=world.Center;
                }
            }
            float radius=profile.fitIslandToFoundation && foundation ? Mathf.Max(foundation.lossyScale.x,foundation.lossyScale.z)*.5f : profile.islandRadius;
            radius=Mathf.Max(1,radius);
            var origin=transform.position;
            float waterLevel=world!=null?world.WaterHeight(origin):origin.y+profile.waterHeight;
            waterRenderer.transform.localPosition=new Vector3(0,waterLevel-origin.y,0);
            waterRenderer.transform.localScale=new Vector3(profile.oceanSize,1,profile.oceanSize);
            var ocean=waterRenderer.GetComponent<CandyOceanSurface>();
            if(ocean)
            {
                ocean.profile=profile;
                ocean.TrackStorms=WorldOverride==null;
                ocean.DetailedExtent=openWater&&world!=null?world.Radius*1.45f+20:0;
                ocean.Refresh();
            }
            float sandTop=origin.y+profile.sandHeight;
            if(profile.alignSandToFoundation&&foundation)
            {
                var support=foundation.GetComponent<Collider>();
                if(support)sandTop=support.bounds.max.y;
            }
            sandRenderer.transform.position=new Vector3(origin.x,sandTop,origin.z);
            var terrain=sandRenderer.GetComponent<CandySandbar>();
            if(terrain&&!openWater)
            {
                terrain.CatwalksBeach=UsesCastleCoast;
                terrain.BeachWaterOffset=waterLevel-sandTop;
                terrain.profile=profile;var baseline=GetComponent<CandyArtBaseline>();
                if(baseline)terrain.map=baseline.map;
                else if(foundation)terrain.map=foundation.GetComponentInParent<DeadlyJenga.Core.IMapFootprint>() as MonoBehaviour;
                terrain.Refresh();sandRenderer.transform.localScale=Vector3.one;
            }
            else sandRenderer.transform.localScale=new Vector3(radius*2*profile.islandAspect.x,1,radius*2*profile.islandAspect.y);
            RefreshCoast(world,terrain,origin,waterLevel);
            if(waterRenderer.sharedMaterial!=profile.water)waterRenderer.sharedMaterial=profile.water;
            if(sandRenderer.sharedMaterial!=profile.sand)sandRenderer.sharedMaterial=profile.sand;
            properties ??=new MaterialPropertyBlock();
            properties.SetVector("_Island",new Vector4(origin.x,origin.z,radius*profile.islandAspect.x,radius*profile.islandAspect.y));
            properties.SetFloat("_Submersion",waterLevel-sandTop);
            properties.SetFloat("_SandTop",sandTop);
            properties.SetFloat("_SandDepth",catwalksCoast!=null?6.2f:profile.sandDepth);
            properties.SetFloat("_Plateau",profile.sandPlateau);
            properties.SetFloat("_WaterHeight",waterLevel);
            properties.SetFloat("_CoastalDetail",openWater?1:0);
            if(openWater)
            {
                properties.SetFloat("_SandTop",waterLevel-profile.sandDepth);
                properties.SetFloat("_HasTerrain",0);
            }
            else if(terrain&&terrain.HeightMap)
            {
                properties.SetTexture("_TerrainHeight",terrain.HeightMap);
                properties.SetVector("_TerrainBounds",new Vector4(origin.x,origin.z,terrain.HalfSize.x*2,terrain.HalfSize.y*2));
                properties.SetFloat("_HasTerrain",1);
            }
            else properties.SetFloat("_HasTerrain",0);
            properties.SetFloat("_WaveExtent",ocean?ocean.ActiveExtent:profile.waveExtent);
            properties.SetFloat("_WaveHeight",profile.waveHeight);
            properties.SetFloat("_WaveSpeed",profile.waveSpeed);
            properties.SetFloat("_WaveChop",profile.waveChop);
            if(profile.sky&&profile.sky.HasProperty("_Horizon"))properties.SetColor("_Horizon",profile.sky.GetColor("_Horizon"));
            if(catwalksCoast!=null) catwalksCoast.SetWaterProperties(properties);
            else properties.SetFloat(CoastEnabled,0);
            sandRenderer.SetPropertyBlock(properties);
            waterRenderer.SetPropertyBlock(properties);
            if(!applyLighting) return;
            Material sky=catwalksCoast!=null && CandySceneSkins.Selected==CandySceneSkin.TropicalDay?
                catwalksCoast.DaySky(profile.sky):profile.sky;
            if(RenderSettings.skybox!=sky)RenderSettings.skybox=sky;
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.55f,.72f,.9f);
            RenderSettings.ambientEquatorColor=new Color(.43f,.57f,.64f);
            RenderSettings.ambientGroundColor=new Color(.3f,.3f,.36f);
            RenderSettings.ambientIntensity=1;
            if(sun){sun.transform.rotation=Quaternion.Euler(profile.sunEuler);sun.color=profile.sunColor;sun.intensity=profile.sunIntensity;}
        }
    }
}
