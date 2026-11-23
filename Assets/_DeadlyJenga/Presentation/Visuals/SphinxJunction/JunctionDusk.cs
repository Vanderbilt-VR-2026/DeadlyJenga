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
        public Shader steamShader;
        Material steamMaterial;
        GameObject sheet;
        CandyEnvironment environment;
        bool previousWater,previousSand;
        Light sun;
        Quaternion oldSunRotation;
        Color oldSunColor;
        float oldSunIntensity;
        int decoratedRevision=-1;
        void AddLettering(ISyncedWorld world)
        {
            if(decoratedRevision==world.Revision)return;
            var facade=world.Root.Find("Sphinx Junction/Saloon false front and cornice/Visual");
            if(!facade)return;
            var board=new GameObject("Painted saloon lettering");
            board.transform.SetParent(facade,false);
            board.transform.localPosition=new Vector3(0,1.14f,-3.21f);
            var text=board.AddComponent<TextMesh>();
            text.text="S A L O O N";text.anchor=TextAnchor.MiddleCenter;
            text.alignment=TextAlignment.Center;text.fontSize=80;text.characterSize=.107f;
            text.color=new Color(.21f,.10f,.07f);
            var engine=world.Root.Find("Sphinx Junction/Copper Canyon locomotive/Visual");
            if(engine && steamMaterial)
            {
                var stack=new GameObject("Chimney steam");
                stack.transform.SetParent(engine,false);
                stack.transform.localPosition=new Vector3(0,2.46f,1.08f);
                var plume=stack.AddComponent<ParticleSystem>();
                plume.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=plume.main;
                main.startLifetime=1.6f; main.startSize=.31f; main.startSpeed=0;
                main.maxParticles=22; main.simulationSpace=ParticleSystemSimulationSpace.World;
                main.startColor=new Color(.85f,.78f,.74f,.44f);
                var shape=plume.shape; shape.enabled=false;
                var flow=plume.velocityOverLifetime; flow.enabled=true;
                flow.space=ParticleSystemSimulationSpace.World;
                flow.x=-.36f; flow.y=1.68f; flow.z=.2f;
                var emission=plume.emission; emission.rateOverTime=9;
                var size=plume.sizeOverLifetime; size.enabled=true;
                size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,3.1f));
                var fade=plume.colorOverLifetime;fade.enabled=true;
                var gradient=new Gradient();
                gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                    new[]{new GradientAlphaKey(.65f,0),new GradientAlphaKey(0,1)});
                fade.color=gradient;
                plume.GetComponent<ParticleSystemRenderer>().sharedMaterial=steamMaterial;
                plume.Play();
            }
            decoratedRevision=world.Revision;
        }
        void Start()
        {
            previousSky=RenderSettings.skybox;
            sky=new Material(skyShader);
            sky.SetTexture("_MainTex",panorama);
            sand=new Material(sandShader);
            if(steamShader)steamMaterial=new Material(steamShader);
            sheet=GameObject.CreatePrimitive(PrimitiveType.Plane);
            var col=sheet.GetComponent<Collider>();
            col.enabled=false; Destroy(col);
            sheet.name="Flowing quicksand";
            sheet.transform.SetParent(transform,false);
            sheet.transform.localPosition=Vector3.down;
            sheet.transform.localScale=new Vector3(48,1,48);
            sheet.GetComponent<Renderer>().sharedMaterial=sand;
        }
        void LateUpdate()
        {
            if(!sky) return;
            RenderSettings.skybox=sky;
            if(Services.World is ISyncedWorld world && world.Root==transform)AddLettering(world);
            if(!environment)
            {
                environment=CandyEnvironment.ForMap(this);
                if(environment)
                {
                    sun=environment.sun;
                    if(sun)
                    {
                        oldSunRotation=sun.transform.rotation;
                        oldSunColor=sun.color;
                        oldSunIntensity=sun.intensity;
                    }
                    previousWater=environment.waterRenderer.enabled;
                    previousSand=environment.sandRenderer.forceRenderingOff;
                }
            }
            if(environment)
            {
                if(sun)
                {
                    sun.transform.rotation=Quaternion.LookRotation(new Vector3(.47f,-.42f,-.78f));
                    sun.color=new Color(1,.83f,.62f);
                    sun.intensity=1.35f;
                }
                environment.waterRenderer.enabled=false;
                environment.sandRenderer.forceRenderingOff=true;
            }
        }
        void OnDisable()
        {
            if(environment)
            {
                environment.waterRenderer.enabled=previousWater;
                environment.sandRenderer.forceRenderingOff=previousSand;
            }
            if(sun)
            {
                sun.transform.rotation=oldSunRotation;sun.color=oldSunColor;sun.intensity=oldSunIntensity;
            }
            if(sky && RenderSettings.skybox==sky) RenderSettings.skybox=previousSky;
        }
        void OnDestroy(){if(sky)Destroy(sky);if(sand)Destroy(sand);if(steamMaterial)Destroy(steamMaterial);}
    }
}
