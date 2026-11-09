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
        bool openWater;
        void OnEnable() { if(Application.isPlaying){if(!GetComponent<CandyWaveContactVisual>())gameObject.AddComponent<CandyWaveContactVisual>();if(!GetComponent<CandyTsunamiVisuals>())gameObject.AddComponent<CandyTsunamiVisuals>();if(!GetComponent<CandyDisasterVisuals>())gameObject.AddComponent<CandyDisasterVisuals>();if(!GetComponent<CandySceneSkins>())gameObject.AddComponent<CandySceneSkins>();} Refresh(); }
        void Update() { if(Application.isPlaying){if(!GetComponent<CandyHazardLighting>())gameObject.AddComponent<CandyHazardLighting>();if(!GetComponent<CandyWaveFeedback>())gameObject.AddComponent<CandyWaveFeedback>();}Refresh(); }
        public void Refresh()
        {
            if(!profile || !waterRenderer || !sandRenderer) return;
            var world=DeadlyJenga.Core.Services.World;
            if(Application.isPlaying && world!=null && (!ReferenceEquals(activeWorld,world) ||
                (openWater && (!foundation || !foundation.gameObject.activeInHierarchy))))
            {
                activeWorld=world;
                openWater=false;
                if(world is MonoBehaviour map)
                {
                    float area=0;Transform floor=null;
                    foreach(var col in map.GetComponentsInChildren<Collider>())
                        if(!col.isTrigger && !col.attachedRigidbody && (col.gameObject.layer==DeadlyJenga.Core.Layers.Surface||col.gameObject.layer==0))
                        {float candidate=col.bounds.size.x*col.bounds.size.z;if(candidate>area){area=candidate;floor=col.transform;}}
                    var scenery=map.GetComponent<DeadlyJenga.Presentation.VillageFlatScenery>();
                    openWater=scenery&&scenery.openWater;
                    if(floor)foundation=floor;
                    var baseline=GetComponent<CandyArtBaseline>();if(baseline)baseline.map=map;
                    transform.position=world.Center;
                }
            }
            float radius=profile.fitIslandToFoundation && foundation ? Mathf.Max(foundation.lossyScale.x,foundation.lossyScale.z)*.5f : profile.islandRadius;
            radius=Mathf.Max(1,radius);
            var origin=transform.position;
            float waterLevel=DeadlyJenga.Core.Services.World!=null?DeadlyJenga.Core.Services.World.WaterHeight(origin):origin.y+profile.waterHeight;
            waterRenderer.transform.localPosition=new Vector3(0,waterLevel-origin.y,0);
            waterRenderer.transform.localScale=new Vector3(profile.oceanSize,1,profile.oceanSize);
            var ocean=waterRenderer.GetComponent<CandyOceanSurface>();
            if(ocean)
            {
                ocean.profile=profile;
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
                terrain.profile=profile;var baseline=GetComponent<CandyArtBaseline>();
                if(baseline)terrain.map=baseline.map;
                else if(foundation)terrain.map=foundation.GetComponentInParent<DeadlyJenga.Core.IMapFootprint>() as MonoBehaviour;
                terrain.Refresh();sandRenderer.transform.localScale=Vector3.one;
            }
            else sandRenderer.transform.localScale=new Vector3(radius*2*profile.islandAspect.x,1,radius*2*profile.islandAspect.y);
            if(waterRenderer.sharedMaterial!=profile.water)waterRenderer.sharedMaterial=profile.water;
            if(sandRenderer.sharedMaterial!=profile.sand)sandRenderer.sharedMaterial=profile.sand;
            properties ??=new MaterialPropertyBlock();
            properties.SetVector("_Island",new Vector4(origin.x,origin.z,radius*profile.islandAspect.x,radius*profile.islandAspect.y));
            properties.SetFloat("_Submersion",waterLevel-sandTop);
            properties.SetFloat("_SandTop",sandTop);
            properties.SetFloat("_SandDepth",profile.sandDepth);
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
            waterRenderer.SetPropertyBlock(properties);
            sandRenderer.SetPropertyBlock(properties);
            if(!applyLighting) return;
            if(RenderSettings.skybox!=profile.sky)RenderSettings.skybox=profile.sky;
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.55f,.72f,.9f);
            RenderSettings.ambientEquatorColor=new Color(.43f,.57f,.64f);
            RenderSettings.ambientGroundColor=new Color(.3f,.3f,.36f);
            RenderSettings.ambientIntensity=1;
            if(sun){sun.transform.rotation=Quaternion.Euler(profile.sunEuler);sun.color=profile.sunColor;sun.intensity=profile.sunIntensity;}
        }
    }
}
