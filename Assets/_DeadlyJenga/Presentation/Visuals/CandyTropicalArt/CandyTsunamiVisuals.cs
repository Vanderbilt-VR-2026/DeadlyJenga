using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using DeadlyJenga.Core;

namespace DeadlyJenga.CandyArt
{
    [DisallowMultipleComponent]
    public sealed class CandyTsunamiVisuals : MonoBehaviour
    {
        CandyEnvironment environment;
        GameObject visual;
        Mesh mesh;
        Texture2D foamDetail;
        MeshRenderer water;
        Material material, sprayMaterial;
        ParticleSystem spray,spindrift;
        MaterialPropertyBlock properties;
        bool initialized,ownsFoamDetail;
        WaveFrame wave;
        float sprayCredit,foamCredit;
        int emissionSeed;

        void Awake() { environment = GetComponent<CandyEnvironment>(); properties = new MaterialPropertyBlock(); }
        void Build()
        {
            initialized = true;
            var template = Resources.Load<Material>("Tsunami/Water");
            var mist = Resources.Load<Material>("Tsunami/Spray");
            if (!template || !mist) { Debug.LogError("Tsunami presentation materials are missing.", this); return; }
            visual = new GameObject("Visual - Tsunami");
            var filter = visual.AddComponent<MeshFilter>(); water = visual.AddComponent<MeshRenderer>();
            material = new Material(template); foamDetail=Resources.Load<Texture2D>("CandyScenery/BreakingFoam");
            if(!foamDetail){foamDetail=BuildFoamDetail();ownsFoamDetail=true;}
            material.SetTexture("_FoamDetail",foamDetail); water.sharedMaterial = material;
            water.shadowCastingMode = ShadowCastingMode.Off; water.receiveShadows = false;
            var vertices=new List<Vector3>();var triangles=new List<int>();
            AddSurface(vertices,triangles,128,128,0);
            AddSurface(vertices,triangles,128,128,2);
            AddSurface(vertices,triangles,80,32,1);
            AddWaveCap(vertices,triangles,false);AddWaveCap(vertices,triangles,true);
            mesh=new Mesh{name="Thick rolling swell, curling face and whitewater",vertices=vertices.ToArray(),triangles=triangles.ToArray()};
            filter.sharedMesh = mesh;
            var sprayObject = new GameObject("Crest spray (pooled)"); sprayObject.transform.SetParent(visual.transform, false);
            spray = sprayObject.AddComponent<ParticleSystem>(); spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = spray.main; main.loop = true; main.playOnAwake = false; main.maxParticles = 384;
            main.startLifetime = 1.5f; main.startSpeed = 8f; main.startSize = new ParticleSystem.MinMaxCurve(.12f, .65f);
            main.startColor = new Color(.87f, 1, .97f, .55f); main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = .55f; main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
            var emission = spray.emission; emission.enabled=false;
            var shape = spray.shape; shape.shapeType = ParticleSystemShapeType.Box;
            var collision = spray.collision; collision.enabled = false;
            var color = spray.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .15f), new GradientAlphaKey(0, 1) }); color.color = gradient;
            sprayMaterial = new Material(mist);
            var cloud=Resources.Load<Texture2D>("CandyScenery/StormCloudPuff");
            if(cloud){sprayMaterial.SetTexture("_CloudTexture",cloud);sprayMaterial.SetFloat("_UseCloudTexture",1);}
            var renderer = spray.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = sprayMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            var mistObject=new GameObject("Breaking lip spindrift");mistObject.transform.SetParent(spray.transform,false);
            spindrift=mistObject.AddComponent<ParticleSystem>();spindrift.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var mistMain=spindrift.main;mistMain.playOnAwake=false;mistMain.loop=true;mistMain.maxParticles=384;
            mistMain.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;mistMain.startLifetime=2f;mistMain.startSpeed=9f;mistMain.startSize=new ParticleSystem.MinMaxCurve(1.5f,4f);
            mistMain.startColor=new Color(.95f,1,1,.7f);mistMain.gravityModifier=.9f;mistMain.simulationSpace=ParticleSystemSimulationSpace.World;
            var mistEmission=spindrift.emission;mistEmission.enabled=false;
            var mistShape=spindrift.shape;mistShape.shapeType=ParticleSystemShapeType.Box;mistShape.scale=new Vector3(20,.15f,.5f);
            var mistColor=spindrift.colorOverLifetime;mistColor.enabled=true;mistColor.color=gradient;
            var mistRenderer=spindrift.GetComponent<ParticleSystemRenderer>();mistRenderer.sharedMaterial=sprayMaterial;mistRenderer.shadowCastingMode=ShadowCastingMode.Off;

        }

        static float FoamJitter(int x,int y,int salt)
        {
            unchecked{uint h=(uint)(x*374761393+y*668265263+salt*1442695041);h=(h^(h>>13))*1274126177;return (h^(h>>16))*(1f/uint.MaxValue);}
        }
        static Texture2D BuildFoamDetail()
        {
            const int size=256,cells=16;var pixels=new Color32[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float px=x*(cells/(float)size),py=y*(cells/(float)size);int ix=Mathf.FloorToInt(px),iy=Mathf.FloorToInt(py);
                float first=100,second=100;
                for(int oy=-1;oy<=1;oy++)for(int ox=-1;ox<=1;ox++)
                {
                    int cx=(ix+ox+cells)%cells,cy=(iy+oy+cells)%cells;
                    float dx=ix+ox+FoamJitter(cx,cy,0)-px,dy=iy+oy+FoamJitter(cx,cy,1)-py,d=dx*dx+dy*dy;
                    if(d<first){second=first;first=d;}else if(d<second)second=d;
                }
                float edge=1-Mathf.SmoothStep(0,1,(Mathf.Sqrt(second)-Mathf.Sqrt(first)-.02f)/.11f);
                float bead=1-Mathf.SmoothStep(0,1,(Mathf.Sqrt(first)-.03f)/.25f);
                byte foam=(byte)Mathf.RoundToInt(Mathf.Clamp01(edge*.85f+bead*.25f)*255);
                pixels[x+y*size]=new Color32(foam,(byte)Mathf.RoundToInt(Mathf.Clamp01(Mathf.Sqrt(first))*255),0,255);
            }
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,true,true){name="Periodic cellular breaking foam",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear};
            texture.SetPixels32(pixels);texture.Apply(true,true);return texture;
        }

        static void AddSurface(List<Vector3> vertices,List<int> triangles,int columns,int rows,int kind)
        {
            int first=vertices.Count;
            for(int z=0;z<=rows;z++)for(int x=0;x<=columns;x++)
                vertices.Add(new Vector3((x/(float)columns-.5f)*(kind==1?1.5f:1.3f),kind,z/(float)rows-.5f));
            for(int z=0;z<rows;z++)for(int x=0;x<columns;x++)
            {
                int a=first+z*(columns+1)+x,b=a+columns+1;
                triangles.Add(a);triangles.Add(b);triangles.Add(a+1);triangles.Add(a+1);triangles.Add(b);triangles.Add(b+1);
            }
        }

        static Vector2 CapProfile(float t,bool back)
        {
            float angle=Mathf.Lerp(Mathf.PI,.68f,(t-.42f)/.58f);
            if(!back)
            {
                if(t<.42f){angle=t/.42f*Mathf.PI*.5f;return new Vector2(.34f*Mathf.Sin(angle)-.5f,.43f*(1-Mathf.Cos(angle)));}
                return new Vector2(.17f+.33f*Mathf.Cos(angle),.43f+.57f*Mathf.Sin(angle));
            }
            float v=1-t;Vector2 tip=CapProfile(1,false);
            return new Vector2(v*v*v*-1.6f*.45f+3*v*v*t*-.45f*.45f+3*v*t*t*.65f+t*t*t*tip.x,3*v*v*t*1.5f+3*v*t*t*1.32f+t*t*t*tip.y);
        }
        static float CapCross(Vector2 a,Vector2 b,Vector2 c)=>(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);
        static void AddWaveCap(List<Vector3> vertices,List<int> triangles,bool right)
        {
            const int samples=128;int first=vertices.Count;var outline=new List<Vector2>(samples*2+1);var remaining=new List<int>(samples*2+1);
            for(int i=0;i<=samples;i++){float t=i/(float)samples;vertices.Add(new Vector3(t,right?4:3,0));outline.Add(CapProfile(t,false));}
            for(int i=samples-1;i>=0;i--){float t=i/(float)samples;vertices.Add(new Vector3(t,right?4:3,1));outline.Add(CapProfile(t,true));}
            float area=0;for(int i=0;i<outline.Count;i++){var a=outline[i];var b=outline[(i+1)%outline.Count];area+=a.x*b.y-b.x*a.y;remaining.Add(i);}
            if(area<0)remaining.Reverse();
            while(remaining.Count>3)
            {
                bool found=false;
                for(int i=0;i<remaining.Count;i++)
                {
                    int a=remaining[(i+remaining.Count-1)%remaining.Count],b=remaining[i],c=remaining[(i+1)%remaining.Count];
                    if(CapCross(outline[a],outline[b],outline[c])<=.00000001f)continue;
                    bool contains=false;
                    foreach(int q in remaining)
                    {
                        if(q==a||q==b||q==c)continue;
                        if(CapCross(outline[a],outline[b],outline[q])>=0&&CapCross(outline[b],outline[c],outline[q])>=0&&CapCross(outline[c],outline[a],outline[q])>=0){contains=true;break;}
                    }
                    if(contains)continue;
                    triangles.Add(first+a);triangles.Add(first+b);triangles.Add(first+c);remaining.RemoveAt(i);found=true;break;
                }
                if(!found)throw new System.InvalidOperationException("Wave side outline could not be triangulated.");
            }
            triangles.Add(first+remaining[0]);triangles.Add(first+remaining[1]);triangles.Add(first+remaining[2]);
        }

        void LateUpdate()
        {
            if (!environment || !environment.waterRenderer) return;
            if (properties == null) properties = new MaterialPropertyBlock();
            wave = default;
            foreach (var source in Tracked.Waves) if (source.Wave.active) { wave = source.Wave; break; }
            if (wave.active && !initialized) Build();
            var ocean=environment.waterRenderer.GetComponent<CandyOceanSurface>();
            if(ocean)ocean.Refresh();
            environment.waterRenderer.GetPropertyBlock(properties);
            if(ocean)properties.SetFloat("_WaveExtent",ocean.ActiveExtent);
            SetWave(properties, visual ? wave : default);
            environment.waterRenderer.SetPropertyBlock(properties);
            if (!visual) return;
            bool shown = wave.active && wave.height > .02f;
            water.enabled = shown;
            if (!shown) { spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); return; }
            material.CopyPropertiesFromMaterial(environment.waterRenderer.sharedMaterial);
            material.SetTexture("_FoamDetail",foamDetail);
            material.renderQueue = environment.waterRenderer.sharedMaterial.renderQueue + 1;
            sprayMaterial.renderQueue=material.renderQueue+1;
            visual.transform.SetPositionAndRotation(wave.center, Quaternion.LookRotation(wave.direction));
            visual.transform.localScale = Vector3.one;
            mesh.bounds = new Bounds(new Vector3(0,wave.height*.75f,-wave.depth*.8f),new Vector3(wave.width*1.5f+4,wave.height*1.8f+8,wave.depth*3.6f+8));
            water.SetPropertyBlock(properties);
            spray.transform.localPosition = Vector3.zero;
            spray.transform.localRotation = Quaternion.identity;
            var shape = spray.shape; shape.scale = new Vector3(wave.width * .8f, .1f, .5f);
            if(spindrift){var mistShape=spindrift.shape;mistShape.scale=shape.scale;}
            var camera = Camera.main;
            bool close = camera && Vector3.Distance(camera.transform.position, visual.transform.position) < Mathf.Max(160,wave.width*2);
            if (wave.height > 1 && close) { if (!spray.isPlaying) spray.Play(); EmitCrest(Time.deltaTime); }
            else spray.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        Vector3 CurlPoint(float across,float t)
        {
            float blend=Mathf.SmoothStep(0,1,(t-.35f)/.65f);
            float phase=Mathf.Clamp01(t+.09f*Mathf.Sin(across*.075f-Time.time*.5f)*Mathf.Sin(Mathf.PI*t)*blend);
            float z,y;
            if(phase<.42f){float angle=phase/.42f*Mathf.PI*.5f;z=.34f*Mathf.Sin(angle);y=.43f*(1-Mathf.Cos(angle));}
            else{float angle=Mathf.Lerp(Mathf.PI,.68f,(phase-.42f)/.58f);z=.67f+.33f*Mathf.Cos(angle);y=.43f+.57f*Mathf.Sin(angle);}
            float scale=1+.18f*Mathf.Sin(across*.063f)+.055f*Mathf.Sin(across*.17f+1);
            return new Vector3(across,y*wave.height*scale,(z-.5f)*wave.depth);
        }
        void EmitCrest(float dt)
        {
            if(!wave.active||!spray||!spindrift)return;
            dt=Mathf.Min(.1f,dt);sprayCredit+=dt*300;foamCredit+=dt*180;
            while(sprayCredit>=1){EmitLip(spray,false);sprayCredit--;}
            while(foamCredit>=1){EmitLip(spindrift,true);foamCredit--;}
        }
        void EmitLip(ParticleSystem system,bool foam)
        {
            int seed=emissionSeed++;float across=(FoamJitter(seed,1,9)-.5f)*wave.width*1.16f;
            float t=.86f+FoamJitter(seed,2,7)*.14f;
            Vector3 local=CurlPoint(across,t),tangent=(CurlPoint(across,Mathf.Min(1,t+.005f))-CurlPoint(across,t-.005f)).normalized;
            var particle=new ParticleSystem.EmitParams();
            particle.position=visual.transform.TransformPoint(local);
            particle.velocity=visual.transform.TransformDirection(tangent*(foam?7:10)+Vector3.up*(1+FoamJitter(seed,3,6)*3)+new Vector3((FoamJitter(seed,4,8)-.5f)*3,0,3));
            particle.startLifetime=1.2f+FoamJitter(seed,5,2)*1.2f;
            particle.startSize=foam?1f+FoamJitter(seed,6,3)*2f:.1f+FoamJitter(seed,6,3)*.5f;
            particle.rotation=FoamJitter(seed,7,8)*360;
            system.Emit(particle,1);
        }
        static void SetWave(MaterialPropertyBlock block, WaveFrame frame)
        {
            block.SetVector("_SurgeOrigin", frame.center);
            block.SetVector("_SurgeDirection", frame.direction);
            block.SetFloat("_SurgeWidth", frame.width); block.SetFloat("_SurgeDepth", frame.depth);
            block.SetFloat("_SurgeHeight", frame.active ? frame.height : 0);
        }
        void OnDisable()
        {
            if (water) water.enabled = false;
            if (spray) spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (environment && environment.waterRenderer && properties != null)
            { environment.waterRenderer.GetPropertyBlock(properties); SetWave(properties, default); environment.waterRenderer.SetPropertyBlock(properties); }
        }
        void OnDestroy()
        {
            Release(visual); Release(mesh); Release(material); Release(sprayMaterial); if(ownsFoamDetail)Release(foamDetail);
        }
        static void Release(Object value)
        { if (value) { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); } }
    }
}
