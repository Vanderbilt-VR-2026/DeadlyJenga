using System;
using DeadlyJenga.Core;
using DeadlyJenga.CandyAudio;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyArt
{
    [DefaultExecutionOrder(320), DisallowMultipleComponent]
    public sealed class CandyWaveContactVisual : MonoBehaviour
    {
        const int Width=48, Rows=18, Lanes=12, Bands=6;
        static readonly ProfilerMarker Cost=new ProfilerMarker("DJ.WaveContacts");
        static CandyWaveContactVisual owner;
        static readonly int ContactActive=Shader.PropertyToID("_DJWaveContactActive");
        static readonly int ContactMap=Shader.PropertyToID("_DJWaveContactMap");
        static readonly int ContactRect=Shader.PropertyToID("_DJWaveContactRect");
        static readonly int Inside=Shader.PropertyToID("_DJWaveInside");
        static readonly int ContactOrigin=Shader.PropertyToID("_DJWaveContactOrigin");

        struct Contact
        {
            public Collider collider;
            public Vector3 point, normal, position;
            public int probe;
        }
        readonly Contact[] contacts=new Contact[Lanes*Bands];
        readonly Color[] pixels=new Color[Width*Rows];
        readonly Vector3[] splashPoints=new Vector3[3];
        readonly float[] splashScores=new float[3];
        readonly System.Random random=new System.Random(4157);
        Texture2D map;
        WaveFrame frame;
        Vector3 side, direction, impact;
        Vector4 rect;
        float nextProbe, origin, nextBurst, immersion, splash, coverage;
        bool wet, hadWave;
        int band, pass, probeSequence;
        Camera view;
        ParticleSystem spray;
        ParticleSystem[] crest;
        int[] crestCaps;
        MeshRenderer wash;
        Mesh washMesh;
        Material washMaterial;
        MaterialPropertyBlock washProperties;
        public int LastProbeCount {get;private set;}
        public int ContactCount {get;private set;}
        public float Immersion=>immersion;
        public bool EnabledEffects {get;set;}=true;
        public bool TryGetImpact(out Vector3 point){point=impact;return ContactCount>0&&coverage>.04f;}

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOwner(){owner=null;Shader.SetGlobalFloat(ContactActive,0);Shader.SetGlobalFloat(Inside,0);}
        void OnEnable(){RenderPipelineManager.beginCameraRendering+=BeforeCamera;}
        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering-=BeforeCamera;
            ResetEffect();
        }
        void OnDestroy()
        {
            if(map)Destroy(map);
            if(washMesh)Destroy(washMesh);
            if(washMaterial)Destroy(washMaterial);
        }
        void ResetEffect()
        {
            if(owner==this){owner=null;Shader.SetGlobalFloat(ContactActive,0);Shader.SetGlobalFloat(Inside,0);CandyAudioEngine.WaveImmersion=0;}
            if(wash)wash.enabled=false;
            if(spray)spray.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            if(crest!=null)for(int i=0;i<crest.Length;i++)if(crest[i]){var main=crest[i].main;main.maxParticles=crestCaps[i];}
            crest=null;crestCaps=null;
            if(hadWave)Array.Clear(contacts,0,contacts.Length);
            hadWave=wet=false;immersion=splash=coverage=0;ContactCount=LastProbeCount=0;nextProbe=nextBurst=0;band=pass=probeSequence=0;
        }
        void LateUpdate()
        {
            using var marker=Cost.Auto();
            frame=default;
            if(EnabledEffects)foreach(var source in Tracked.Waves)if(source.Wave.active){frame=source.Wave;break;}
            if(!frame.active||frame.height<=.05f||frame.width<=0||frame.depth<=0||Services.World==null)
            {if(hadWave)ResetEffect();return;}
            if(owner&&owner!=this)return;
            owner=this;hadWave=true;
            direction=Vector3.ProjectOnPlane(frame.direction,Vector3.up).normalized;
            if(direction.sqrMagnitude<.9f){ResetEffect();return;}
            side=Vector3.Cross(Vector3.up,direction);
            view=Services.Session?.ActiveCamera;
            if(Time.unscaledTime>=nextProbe)
            {
                nextProbe=Time.unscaledTime+.10f;
                Probe(Services.World.PlayableBounds);
                Upload();
                if(ContactCount>0)Emit();
            }
            UpdateImmersion();
        }
        void Probe(Bounds bounds)
        {
            LastProbeCount=0;probeSequence++;
            var e=bounds.extents;
            float w=Mathf.Abs(side.x)*e.x+Mathf.Abs(side.z)*e.z;
            float x=Vector3.Dot(bounds.center,side), middle=Vector3.Dot(frame.center,side);
            float lo=Mathf.Max(x-w-1,middle-frame.width*.65f), hi=Mathf.Min(x+w+1,middle+frame.width*.65f);
            float low=Mathf.Max(frame.waterHeight+.1f,bounds.min.y-.15f);
            float high=Mathf.Min(bounds.max.y+.3f,frame.waterHeight+frame.height*1.28f);
            rect=new Vector4(lo,low,1/Mathf.Max(.01f,hi-lo),1/Mathf.Max(.01f,high-low));
            origin=Vector3.Dot(frame.center,direction);
            float z=Vector3.Dot(bounds.center,direction);
            float along=Mathf.Abs(direction.x)*e.x+Mathf.Abs(direction.z)*e.z;
            if(hi<=lo||high<=low||Mathf.Abs(z-origin)>along+frame.depth*1.65f+4)
            {Array.Clear(contacts,0,contacts.Length);return;}
            float backtrack=Mathf.Clamp(frame.speed*.38f,2.4f,11.5f);
            int mask=(1<<Layers.Block)|(1<<Layers.Surface);
            for(int row=0;row<2;row++)
            {
                int b=band+row;
                float y=Mathf.Lerp(low,high,(b+.5f)/Bands);
                for(int lane=0;lane<Lanes;lane++)
                {
                    int slot=b*Lanes+lane;
                    float jitter=((pass+lane%2)%3-1)*.23f;
                    float across=Mathf.Lerp(lo,hi,(lane+.5f+jitter)/Lanes);
                    float front=origin+FrontDepth((y-frame.waterHeight)/(frame.height*CrestScale(across-middle)))*frame.depth;
                    var from=side*across+direction*(front-backtrack)+Vector3.up*y;
                    LastProbeCount++;
                    if(Physics.Raycast(from,direction,out var hit,backtrack+Mathf.Clamp(frame.speed*.15f,1.4f,4.5f),mask,QueryTriggerInteraction.Ignore)&&Vector3.Dot(hit.normal,direction)<-.22f)
                        contacts[slot]=new Contact{collider=hit.collider,point=hit.point,normal=hit.normal,position=hit.collider.transform.position,probe=probeSequence};

                }
            }
            band+=2;if(band>=Bands){band=0;pass++;}
        }
        void Upload()
        {
            ContactCount=0;coverage=0;
            Array.Clear(splashScores,0,splashScores.Length);
            Array.Clear(pixels,0,pixels.Length);
            foreach(var contact in contacts)
            {
                var collider=contact.collider;
                if(!collider||!collider.enabled||!collider.gameObject.activeInHierarchy||probeSequence-contact.probe>3)continue;
                Vector3 shift=collider.transform.position-contact.position;
                if(shift.sqrMagnitude>2.25f)continue;
                Vector3 point=contact.point+shift;
                float z=Vector3.Dot(point,direction)-origin;
                if(z < -frame.depth*.64f||z>frame.depth*.54f+2)continue;
                var b=collider.bounds;var e=b.extents;
                float x=Vector3.Dot(point,side),half=Mathf.Abs(side.x)*e.x+Mathf.Abs(side.z)*e.z;
                float mid=Vector3.Dot(b.center,side);
                int x0=Mathf.Clamp(Mathf.FloorToInt((mid-half-rect.x)*rect.z*Width),0,Width-1);
                int x1=Mathf.Clamp(Mathf.CeilToInt((mid+half-rect.x)*rect.z*Width)-1,0,Width-1);
                int y0=Mathf.Clamp(Mathf.FloorToInt((b.min.y-rect.y)*rect.w*Rows),0,Rows-1);
                int y1=Mathf.Clamp(Mathf.CeilToInt((b.max.y-rect.y)*rect.w*Rows)-1,0,Rows-1);
                float strength=Mathf.Clamp01(-Vector3.Dot(contact.normal,direction));
                for(int y=y0;y<=y1;y++)for(int i=x0;i<=x1;i++)
                {
                    int id=i+y*Width;
                    float u=rect.x+(i+.5f)/(Width*rect.z),v=rect.y+(y+.5f)/(Rows*rect.w);
                    float edge=Mathf.Min(half-Mathf.Abs(u-mid),Mathf.Min(v-b.min.y,b.max.y-v));
                    float weight=strength*Mathf.Clamp01(edge*Mathf.Min(Width*rect.z,Rows*rect.w)+.55f);
                    var p=pixels[id];
                    if(weight>.01f&&(p.g<.01f||z<p.r/p.g))pixels[id]=new Color(z*weight,weight,0,0);
                }
                ContactCount++;
                // Favor nearby hits at a height where the wave actually reaches the face.
                float score=VisibleHit(point)*strength;
                if(view)score/=1+(view.transform.position-point).sqrMagnitude*.0005f;
                if(score>coverage){coverage=score;impact=point;}
                int group=Mathf.Clamp((int)((x-rect.x)*rect.z*3),0,2);
                if(score>splashScores[group]){splashScores[group]=score;splashPoints[group]=point;}
            }
            if(ContactCount==0){Shader.SetGlobalFloat(ContactActive,0);return;}
            if(!map)
            {
                if(!SystemInfo.SupportsTextureFormat(TextureFormat.RGBAHalf))return;
                map=new Texture2D(Width,Rows,TextureFormat.RGBAHalf,false,true){name="Wave contact field",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            }
            map.SetPixels(pixels);map.Apply(false,false);
            Shader.SetGlobalTexture(ContactMap,map);
            Shader.SetGlobalVector(ContactRect,rect);
            Shader.SetGlobalFloat(ContactOrigin,origin);
            Shader.SetGlobalFloat(ContactActive,1);
        }
        float VisibleHit(Vector3 point)
        {
            Vector3 d=point-frame.center;
            float x=Vector3.Dot(d,side);
            float height=(point.y-frame.waterHeight)/(frame.height*CrestScale(x));
            if(height<0||height>1.04f)return 0;
            float gap=Vector3.Dot(d,direction)-FrontDepth(height)*frame.depth;
            return 1-Mathf.SmoothStep(0,1,Mathf.Abs(gap)/Mathf.Max(1.4f,frame.speed*.22f));
        }
        static float FrontDepth(float height)
        {
            if(height<.43f)
            {
                float c=1-Mathf.Clamp01(height/.43f);
                return .34f*Mathf.Sqrt(1-c*c)-.5f;
            }
            float h=Mathf.Clamp01((height-.43f)/.57f);
            return .17f+(h>=Mathf.Sin(.68f)?1:-1)*.33f*Mathf.Sqrt(1-h*h);
        }
        public bool HasContactAt(Vector3 point)
        {
            if(ContactCount==0)return false;
            float x=(Vector3.Dot(point,side)-rect.x)*rect.z;
            float y=(point.y-rect.y)*rect.w;
            return x>=0&&x<1&&y>=0&&y<1&&pixels[(int)(x*Width)+(int)(y*Rows)*Width].g>.04f;
        }
        void Emit()
        {
            if(!view||coverage<.04f||Time.unscaledTime<nextBurst)return;
            if((view.transform.position-impact).sqrMagnitude>145*145)return;
            if(crest==null)
            {
                var waveRoot=GameObject.Find("Visual - Tsunami");
                if(!waveRoot)return;
                crest=waveRoot.GetComponentsInChildren<ParticleSystem>();
                if(crest.Length==0){crest=null;return;}
                crestCaps=new int[crest.Length];
                int budget=0;
                for(int i=0;i<crest.Length;i++)
                {
                    var main=crest[i].main;crestCaps[i]=main.maxParticles;
                    int share=Mathf.Min(32,Mathf.Max(0,main.maxParticles-16));
                    main.maxParticles-=share;budget+=share;
                }
                if(!spray)
                {
                    var go=new GameObject("Wave contact spray");go.transform.SetParent(transform,false);
                    spray=go.AddComponent<ParticleSystem>();
                    spray.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main=spray.main;main.loop=true;main.playOnAwake=false;main.simulationSpace=ParticleSystemSimulationSpace.World;main.gravityModifier=.72f;
                    var emission=spray.emission;emission.enabled=false;
                    var shape=spray.shape;shape.enabled=false;
                    var renderer=spray.GetComponent<ParticleSystemRenderer>();
                    renderer.sharedMaterial=Resources.Load<Material>("Tsunami/Spray");renderer.shadowCastingMode=ShadowCastingMode.Off;
                    var colors=spray.colorOverLifetime;colors.enabled=true;
                    var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(.75f,.45f),new GradientAlphaKey(0,1)});colors.color=gradient;
                }
                var contactMain=spray.main;contactMain.maxParticles=Mathf.Max(1,budget);
                if(budget==0)return;
            }
            if(!spray||!spray.GetComponent<ParticleSystemRenderer>().sharedMaterial)return;
            nextBurst=Time.unscaledTime+.10f;
            if(!spray.isPlaying)spray.Play();
            
            float force=Mathf.Clamp(frame.speed*.45f+frame.height*.13f,3,13);
            for(int group=0;group<3;group++)
            {
                var point=splashPoints[group];
                if(splashScores[group]<.04f||(view.transform.position-point).sqrMagnitude>145*145)continue;
                int count=Mathf.Clamp(Mathf.RoundToInt(9*splashScores[group]),3,9);
                for(int i=0;i<count;i++)
                {
                float spread=(float)random.NextDouble()*2-1;
                float height=(float)random.NextDouble();
                spray.Emit(new ParticleSystem.EmitParams{
                    position=point-direction*.22f+side*spread*.7f,
                    velocity=side*(spread*force)+Vector3.up*((.45f+height)*force)-direction*(.3f+height*2),
                    startSize=i%3==0?1.15f+height*.8f:.15f+height*.6f,startLifetime=.32f+height*.42f,
                    startColor=Color.Lerp(new Color(.57f,.9f,.88f,.62f),new Color(.95f,1,.98f,.85f),height),
                    randomSeed=(uint)random.Next(1,int.MaxValue)},1);
                }
            }
        }
        static float CrestScale(float x)=>1+.18f*Mathf.Sin(x*.063f)+.055f*Mathf.Sin(x*.17f+1);
        static Vector2 Front(float t)
        {
            if(t<.42f){float a=t/.42f*Mathf.PI*.5f;return new Vector2(.34f*Mathf.Sin(a)-.5f,.43f*(1-Mathf.Cos(a)));}
            float angle=Mathf.Lerp(Mathf.PI,.68f,(t-.42f)/.58f);
            return new Vector2(.17f+.33f*Mathf.Cos(angle),.43f+.57f*Mathf.Sin(angle));
        }
        static Vector2 Back(float t,float across)
        {
            float v=1-t;var tip=Front(1);
            float footprint=.45f+.55f*Mathf.Sqrt(Mathf.Clamp01(1-across*across/(.65f*.65f)));
            return new Vector2(v*v*v*-1.6f*footprint+3*v*v*t*-.45f*footprint+3*v*t*t*.65f+t*t*t*tip.x,
                3*v*v*t*1.5f+3*v*t*t*1.32f+t*t*t*tip.y);
        }
        public static bool ContainsEye(WaveFrame wave,Vector3 eye)
        {
            if(!wave.active||wave.height<=.05f||wave.width<=0||wave.depth<=0)return false;
            var forward=Vector3.ProjectOnPlane(wave.direction,Vector3.up).normalized;
            if(forward.sqrMagnitude<.9f)return false;
            Vector3 delta=eye-wave.center;
            float x=Vector3.Dot(delta,Vector3.Cross(Vector3.up,forward));
            var p=new Vector2(Vector3.Dot(delta,forward)/wave.depth,(eye.y-wave.waterHeight)/(wave.height*CrestScale(x)));
            if(Mathf.Abs(x)>wave.width*.65f||p.y<0||p.y>1.18f||p.x< -1.6f||p.x>.51f)return false;
            bool inside=false;
            Vector2 prev=Back(0,x/wave.width);
            for(int i=0;i<=48;i++)
            {
                var next=i<=24?Front(i/24f):Back((48-i)/24f,x/wave.width);
                if((prev.y>p.y)!=(next.y>p.y)&&p.x<(next.x-prev.x)*(p.y-prev.y)/(next.y-prev.y)+prev.x)inside=!inside;
                prev=next;
            }
            return inside;
        }
        void UpdateImmersion()
        {
            float dt=Mathf.Min(.07f,Time.unscaledDeltaTime);
            bool inside=view&&ContainsEye(frame,view.transform.position+Vector3.up*(wet?-.08f:.06f));
            if(inside!=wet)
            {
                wet=inside;splash=inside?.34f:.17f;
                if(view)CandyAudioEngine.Instance?.Play(CandySound.WaveSpray,view.transform.position,.45f,GetInstanceID(),inside?.8f:1.1f,true);
            }
            immersion=Mathf.MoveTowards(immersion,inside?1:0,dt*(inside?8:5));
            splash=Mathf.MoveTowards(splash,0,dt*1.9f);
            CandyAudioEngine.WaveImmersion=immersion;
            if(!view||immersion+splash<.005f){if(wash)wash.enabled=false;return;}
            if(!wash)BuildWash();
            if(!wash)return;
            wash.transform.position=view.transform.position;
            washProperties.SetVector("_WaterWash",new Vector4(immersion,splash,Time.unscaledTime,0));
            wash.SetPropertyBlock(washProperties);
        }
        void BuildWash()
        {
            var shader=Resources.Load<Shader>("CandyScenery/CandyWaveImmersion");
            if(!shader)return;
            var go=new GameObject("Wave eye wash",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(transform,false);
            wash=go.GetComponent<MeshRenderer>();
            washMesh=new Mesh{name="Wave immersion triangle",vertices=new[]{new Vector3(-1,-1,0),new Vector3(-1,3,0),new Vector3(3,-1,0)},triangles=new[]{0,1,2}};
            washMesh.bounds=new Bounds(Vector3.zero,Vector3.one*6);
            go.GetComponent<MeshFilter>().sharedMesh=washMesh;
            washMaterial=new Material(shader);wash.sharedMaterial=washMaterial;wash.shadowCastingMode=ShadowCastingMode.Off;
            washProperties=new MaterialPropertyBlock();wash.enabled=false;
        }
        void BeforeCamera(ScriptableRenderContext context,Camera camera)
        {
            if(owner==this)Shader.SetGlobalFloat(Inside,camera==view?immersion:0);
            if(wash)wash.enabled=owner==this&&camera==view&&immersion+splash>=.005f;
        }
    }
}
