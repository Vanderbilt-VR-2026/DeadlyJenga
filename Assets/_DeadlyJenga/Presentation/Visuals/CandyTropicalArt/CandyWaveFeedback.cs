using System.Collections.Generic;
using DeadlyJenga.Core;
using DeadlyJenga.CandyAudio;
using UnityEngine;

namespace DeadlyJenga.CandyArt
{
    public sealed class CandyWaveFeedback : MonoBehaviour
    {
        readonly HashSet<IWaveState> impact=new HashSet<IWaveState>();float nextScan,nextSpray;bool hadWave;
        CandyWaveContactVisual contacts;
        readonly Dictionary<IWaveState,Transform> roarAnchors=new Dictionary<IWaveState,Transform>();
        readonly HashSet<IWaveState> audibleWaves=new HashSet<IWaveState>();
        readonly List<IWaveState> expiredWaves=new List<IWaveState>();
        void Update()
        {
            if(Time.time<nextScan)return;nextScan=Time.time+.15f;var audio=CandyAudioEngine.Instance;var world=Services.World;if(!audio||world==null){ClearAnchors();return;}bool active=false;audibleWaves.Clear();
            foreach(var wave in Tracked.Waves)
            {
                if(wave==null)continue;
                var f=wave.Wave;if(!f.active)continue;active=true;
                if(f.warningRemaining>0)continue;
                if(f.height<=0||f.width<=0||f.depth<=0)continue;
                audibleWaves.Add(wave);
                if(audio.Listener)
                {
                    if(!roarAnchors.TryGetValue(wave,out var anchor)||!anchor)
                    {
                        anchor=new GameObject("Audio - moving wave front").transform;
                        anchor.SetParent(transform,false);roarAnchors[wave]=anchor;
                    }
                    Vector3 direction=Vector3.ProjectOnPlane(f.direction,Vector3.up).normalized;
                    if(direction.sqrMagnitude<.5f)direction=Vector3.forward;
                    Vector3 side=Vector3.Cross(Vector3.up,direction);
                    float across=Mathf.Clamp(Vector3.Dot(audio.Listener.position-f.center,side),-f.width*.5f,f.width*.5f);
                    anchor.position=new Vector3(f.center.x,f.waterHeight+f.height*.3f,f.center.z)+side*across;
                    float distance=Vector3.Distance(audio.Listener.position,anchor.position);
                    float reach=Mathf.Clamp(f.height*4+f.depth,45,220);
                    float proximity=1-Mathf.Clamp01(distance/reach);
                    if(proximity>0)audio.Loop(CandySound.WaveRoar,anchor,.12f+.88f*proximity,false,Mathf.Lerp(.78f,1.08f,Mathf.Clamp01(f.speed/35)));
                }
                if(!contacts)contacts=GetComponent<CandyWaveContactVisual>();
                if(contacts&&contacts.TryGetImpact(out var point))
                {
                    if(impact.Add(wave))audio.Play(CandySound.WaveImpact,point,1,GetInstanceID());
                    if(Time.time>=nextSpray){nextSpray=Time.time+.55f;audio.Play(CandySound.WaveSpray,point,.54f,GetInstanceID());}
                    if(audio.Listener&&(audio.Listener.position-point).sqrMagnitude<18*18)CandyFeedback.Pulse(.15f,.16f);
                }
            }
            expiredWaves.Clear();
            foreach(var pair in roarAnchors)if(!audibleWaves.Contains(pair.Key))expiredWaves.Add(pair.Key);
            foreach(var wave in expiredWaves){if(roarAnchors[wave])Destroy(roarAnchors[wave].gameObject);roarAnchors.Remove(wave);}
            if(hadWave&&!active){Puddle();impact.Clear();}hadWave=active;
        }
        void ClearAnchors()
        {
            foreach(var anchor in roarAnchors.Values)if(anchor)Destroy(anchor.gameObject);
            roarAnchors.Clear();audibleWaves.Clear();expiredWaves.Clear();
        }
        void OnDisable(){ClearAnchors();impact.Clear();hadWave=false;}
        void Puddle()
        {
            var world=Services.World;
            if(world==null)return;
            var environment=GetComponent<CandyEnvironment>();
            var ocean=environment?environment.waterRenderer:null;
            if(ocean&&ocean.sharedMaterial&&ocean.sharedMaterial.HasProperty("_SoakingPool"))
            {
                float surface=environment.sandRenderer?environment.sandRenderer.transform.position.y:world.Center.y;
                var go=new GameObject("Visual - tsunami soaking puddles");
                go.transform.SetParent(transform,false);
                go.transform.position=world.Center;
                go.AddComponent<CandySoakingPuddle>().Build(world.Center,world.Radius,surface+.02f,ocean);
            }
            CandyAudioEngine.Instance.Play(CandySound.PuddleDrain,world.Center,.5f,GetInstanceID());
        }
    }
    public sealed class CandySoakingPuddle : MonoBehaviour
    {
        static readonly int Pool=Shader.PropertyToID("_SoakingPool");
        readonly List<(MeshRenderer renderer,Vector3 size)> drops=new();
        MaterialPropertyBlock properties;
        Mesh disk;
        float born;

        public void Build(Vector3 center,float radius,float water,Renderer ocean)
        {
            born=Time.time;
            disk=BuildDisk();
            properties=new MaterialPropertyBlock();
            ocean.GetPropertyBlock(properties);
            properties.SetVector(Pool,new Vector4(1,.55f,1,0));
            for(int i=0;i<9;i++)
            {
                float a=i*2.399963f,r=Mathf.Sqrt(i/9f)*radius*.6f;
                var go=new GameObject("Soaking patch",typeof(MeshFilter),typeof(MeshRenderer));
                go.transform.SetParent(transform,false);
                go.transform.position=new Vector3(center.x+Mathf.Cos(a)*r,water,center.z+Mathf.Sin(a)*r);
                go.GetComponent<MeshFilter>().sharedMesh=disk;
                var renderer=go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial=ocean.sharedMaterial;
                renderer.SetPropertyBlock(properties);
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                var size=new Vector3(radius*.55f,1,radius*.4f);
                go.transform.localScale=size;
                drops.Add((renderer,size));
            }
        }

        static Mesh BuildDisk()
        {
            const int sides=40;
            var vertices=new Vector3[sides+1];
            var triangles=new int[sides*3];
            for(int i=0;i<sides;i++)
            {
                float angle=i*Mathf.PI*2/sides;
                vertices[i+1]=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*.5f;
                triangles[i*3+1]=(i+1)%sides+1;
                triangles[i*3+2]=i+1;
            }
            var mesh=new Mesh{name="Tsunami pool surface",vertices=vertices,triangles=triangles};
            mesh.RecalculateBounds();
            return mesh;
        }

        void Update()
        {
            if(properties==null)return;
            float t=Mathf.Clamp01((Time.time-born)/14);
            if(t>=1){Destroy(gameObject);return;}
            properties.SetVector(Pool,new Vector4(1,.55f,1-Mathf.SmoothStep(0,1,t),0));
            for(int i=0;i<drops.Count;i++)
            {
                var (renderer,size)=drops[i];
                if(!renderer)continue;
                renderer.transform.localScale=size*Mathf.Pow(1-t,1+i*.08f);
                renderer.SetPropertyBlock(properties);
            }
        }
        void OnDestroy(){if(disk)Destroy(disk);}
    }
}
