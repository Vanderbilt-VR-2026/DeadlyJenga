using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.Presentation
{
    public sealed class CandyWaterContactEffects:MonoBehaviour
    {
        sealed class Wet
        {public Renderer original,overlay;public float until,lastSplash;public MaterialPropertyBlock properties=new MaterialPropertyBlock();}
        public static CandyWaterContactEffects Current {get;private set;}
        public int WetRendererCount=>wet.Count;
        readonly Dictionary<Renderer,Wet> wet=new Dictionary<Renderer,Wet>();
        readonly List<Renderer> remove=new List<Renderer>();
        readonly Collider[] overlaps=new Collider[96];readonly RaycastHit[] sweep=new RaycastHit[96];
        readonly HashSet<Rigidbody> touched=new HashSet<Rigidbody>();
        Material coating,sprayMaterial;ParticleSystem spray;IWorldQuery world;int cursor,seed;float nextScan,nextDrip;int budgetFrame,newCoatings,splashes;
        const int MaxCoatings=192;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset()=>Current=null;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen()=>GameEvents.SessionStarted+=go=>{if(!go.GetComponent<CandyWaterContactEffects>())go.AddComponent<CandyWaterContactEffects>();};
        void Awake()
        {
            Current=this;var shader=Resources.Load<Shader>("CandyScenery/CandyWetSurface");
            if(shader){coating=new Material(shader){name="Shared drying water coating",enableInstancing=true};}
            var go=new GameObject("Visual - pooled water contact spray");go.transform.SetParent(transform,false);spray=go.AddComponent<ParticleSystem>();spray.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=spray.main;main.playOnAwake=false;main.loop=false;main.maxParticles=240;main.startLifetime=.8f;main.startSize=new ParticleSystem.MinMaxCurve(.08f,.25f);main.simulationSpace=ParticleSystemSimulationSpace.World;main.gravityModifier=.45f;
            var emission=spray.emission;emission.enabled=false;var shape=spray.shape;shape.enabled=false;var collision=spray.collision;collision.enabled=false;
            var template=Resources.Load<Material>("Tsunami/Spray");if(template){sprayMaterial=new Material(template);spray.GetComponent<ParticleSystemRenderer>().sharedMaterial=sprayMaterial;}
            var color=spray.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(new Color(.4f,.9f,1),0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(.8f,0),new GradientAlphaKey(0,1)});color.color=gradient;
        }
        public void HandSweep(Vector3 previous,Vector3 position,float radius)
        {
            if(!coating||!isActiveAndEnabled)return;touched.Clear();
            int count=Physics.OverlapSphereNonAlloc(position,radius,overlaps,1<<Layers.Block,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++){var body=overlaps[i].attachedRigidbody;if(body&&touched.Add(body))Soak(body,overlaps[i].ClosestPoint(position),true);}
            var delta=position-previous;
            if(delta.sqrMagnitude>.01f&&delta.magnitude<30)
            {
                if(Time.time>=nextDrip){nextDrip=Time.time+.15f;Splash(position);}
                count=Physics.SphereCastNonAlloc(previous,radius,delta.normalized,sweep,delta.magnitude,1<<Layers.Block,QueryTriggerInteraction.Ignore);
                for(int i=0;i<count;i++){var body=sweep[i].rigidbody;if(body&&touched.Add(body))Soak(body,sweep[i].point,true);}
            }
        }
        public void Soak(Rigidbody body,Vector3 point,bool splash)
        {
            if(!coating||!body)return;
            if(budgetFrame!=Time.frameCount){budgetFrame=Time.frameCount;newCoatings=splashes=0;}
            var visual=body.GetComponent<IClimbable>()?.Visual;if(!visual)return;
            bool emit=false;
            foreach(var renderer in visual.GetComponentsInChildren<MeshRenderer>())
            {
                if(renderer.name=="Visual - drying water stain")continue;
                var filter=renderer.GetComponent<MeshFilter>();if(!filter||!filter.sharedMesh)continue;
                if(!wet.TryGetValue(renderer,out var item))
                {
                    if(newCoatings>=12)continue;
                    if(wet.Count>=MaxCoatings){EvictOldest();if(wet.Count>=MaxCoatings)continue;}
                    newCoatings++;
                    var go=new GameObject("Visual - drying water stain",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(renderer.transform,false);go.transform.localScale=Vector3.one*1.002f;
                    go.GetComponent<MeshFilter>().sharedMesh=filter.sharedMesh;var overlay=go.GetComponent<MeshRenderer>();overlay.sharedMaterial=coating;overlay.shadowCastingMode=ShadowCastingMode.Off;overlay.receiveShadows=false;
                    item=new Wet{original=renderer,overlay=overlay,lastSplash=-100};wet.Add(renderer,item);
                }
                item.until=Time.time+25;
                if(splash&&Time.time-item.lastSplash>.3f){emit=true;item.lastSplash=Time.time;}
                item.properties.SetFloat("_Wetness",1);item.overlay.SetPropertyBlock(item.properties);
            }
            if(emit)Splash(point);
        }
        void Splash(Vector3 point)
        {
            if(!spray||!sprayMaterial||splashes>=8)return;splashes++;
            for(int i=0;i<8;i++)
            {
                float a=(++seed*2.399963f);float speed=1.5f+(seed%5)*.5f;
                var parameters=new ParticleSystem.EmitParams{position=point,velocity=new Vector3(Mathf.Cos(a)*speed,2+(seed%4)*.6f,Mathf.Sin(a)*speed),startLifetime=.5f+(seed%4)*.12f,startSize=.08f+(seed%5)*.035f};
                spray.Emit(parameters,1);
            }
        }
        void EvictOldest()
        {
            Renderer oldest=null;float time=float.PositiveInfinity;
            foreach(var pair in wet)if(pair.Value.until<time){time=pair.Value.until;oldest=pair.Key;}
            if(!ReferenceEquals(oldest,null)&&time<Time.time+22)Remove(oldest);
        }
        void Remove(Renderer renderer){if(!wet.TryGetValue(renderer,out var item))return;if(item.overlay)Destroy(item.overlay.gameObject);wet.Remove(renderer);}
        void Clear(){foreach(var item in wet.Values)if(item.overlay)Destroy(item.overlay.gameObject);wet.Clear();cursor=0;}
        void LateUpdate()
        {
            if(!ReferenceEquals(world,Services.World)){Clear();world=Services.World;}
            remove.Clear();
            foreach(var pair in wet)
            {
                var item=pair.Value;if(!item.original||!item.overlay||Time.time>=item.until){remove.Add(pair.Key);continue;}
                item.overlay.enabled=item.original.enabled&&item.original.gameObject.activeInHierarchy;
                item.properties.SetFloat("_Wetness",Mathf.Clamp01((item.until-Time.time)/8));item.overlay.SetPropertyBlock(item.properties);
            }
            foreach(var renderer in remove)Remove(renderer);
            if(world==null||Time.time<nextScan||Tracked.Waves.Count==0)return;nextScan=Time.time+.08f;
            var blocks=world.Blocks;if(blocks==null||blocks.Count==0)return;
            int count=Mathf.Min(384,blocks.Count);
            for(int i=0;i<count;i++)
            {
                var body=blocks[cursor++%blocks.Count];if(!body)continue;var point=body.worldCenterOfMass;
                foreach(var source in Tracked.Waves)
                {
                    var wave=source.Wave;if(!wave.active||wave.warningRemaining>0||wave.width<=0||wave.depth<=0)continue;
                    float surface=WaveSurface(wave,point);
                    if(surface>=point.y-.4f){Soak(body,new Vector3(point.x,Mathf.Min(surface,point.y+.4f),point.z),true);break;}
                }
            }
        }
        // Same longitudinal crest (.72) and edge taper (.85) as the wave contract.
        public static float WaveSurface(WaveFrame wave,Vector3 point)
        {
            var delta=point-wave.center;float x=Vector3.Dot(delta,Vector3.Cross(Vector3.up,wave.direction));float t=Vector3.Dot(delta,wave.direction)/wave.depth+.5f;
            if(t<=0||t>=1||Mathf.Abs(x)>=wave.width*.5f)return float.NegativeInfinity;
            float shape=t<.72f?Mathf.SmoothStep(0,1,t/.72f):1-Mathf.SmoothStep(0,1,(t-.72f)/.28f);
            float edge=1-Mathf.SmoothStep(0,1,(Mathf.Abs(x)/(wave.width*.5f)-.85f)/.15f);
            return wave.waterHeight+wave.height*shape*edge;
        }
        void OnDisable(){Clear();if(spray)spray.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);if(Current==this)Current=null;}
        void OnEnable(){Current=this;}
        void OnDestroy(){Clear();if(coating)Destroy(coating);if(sprayMaterial)Destroy(sprayMaterial);}
    }
}
