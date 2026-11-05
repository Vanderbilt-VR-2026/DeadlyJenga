using System.Collections.Generic;
using DeadlyJenga.Core;
using DeadlyJenga.CandyAudio;
using UnityEngine;

namespace DeadlyJenga.CandyArt
{
    public sealed class CandyWaveFeedback : MonoBehaviour
    {
        readonly HashSet<IWaveState> known=new HashSet<IWaveState>(),impact=new HashSet<IWaveState>();float nextScan;bool hadWave;
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
                if(known.Add(wave))audio.Play(CandySound.DisasterHorn,f.center,.85f,GetInstanceID(),1,true);
                if(f.warningRemaining>0){audio.Loop(CandySound.DisasterTension,transform,.45f,true,Mathf.Lerp(.8f,1.3f,1-Mathf.Clamp01(f.warningRemaining/8)));continue;}
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
                    audio.Play(CandySound.WaveSpray,point,.65f,GetInstanceID());
                    if(audio.Listener&&(audio.Listener.position-point).sqrMagnitude<18*18)CandyFeedback.Pulse(.15f,.16f);
                }
            }
            expiredWaves.Clear();
            foreach(var pair in roarAnchors)if(!audibleWaves.Contains(pair.Key))expiredWaves.Add(pair.Key);
            foreach(var wave in expiredWaves){if(roarAnchors[wave])Destroy(roarAnchors[wave].gameObject);roarAnchors.Remove(wave);}
            if(hadWave&&!active){Puddle();known.Clear();impact.Clear();}hadWave=active;
        }
        void ClearAnchors()
        {
            foreach(var anchor in roarAnchors.Values)if(anchor)Destroy(anchor.gameObject);
            roarAnchors.Clear();audibleWaves.Clear();expiredWaves.Clear();
        }
        void OnDisable(){ClearAnchors();known.Clear();impact.Clear();hadWave=false;}
        void Puddle()
        {
            var world=Services.World;if(world==null)return;var environment=GetComponent<CandyEnvironment>();float surface=environment&&environment.sandRenderer?environment.sandRenderer.transform.position.y:world.Center.y;var go=new GameObject("Visual - tsunami soaking puddles");go.transform.position=world.Center;go.AddComponent<CandySoakingPuddle>().Build(world.Center,world.Radius,surface+.02f);
            CandyAudioEngine.Instance.Play(CandySound.PuddleDrain,world.Center,.5f,GetInstanceID());
        }
    }
    public sealed class CandySoakingPuddle : MonoBehaviour
    {
        readonly List<Transform> drops=new List<Transform>();readonly List<Vector3> sizes=new List<Vector3>();Material material;float born;
        public void Build(Vector3 center,float radius,float water)
        {
            born=Time.time;var shader=Resources.Load<Shader>("CandyScenery/CandySpirit");material=new Material(shader);material.SetColor("_BaseColor",new Color(.03f,.6f,.72f));
            for(int i=0;i<9;i++)
            {
                float a=i*2.399963f,r=Mathf.Sqrt(i/9f)*radius*.6f;var go=new GameObject("Soaking patch",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(transform,false);go.transform.position=new Vector3(center.x+Mathf.Cos(a)*r,water,center.z+Mathf.Sin(a)*r);go.GetComponent<MeshFilter>().sharedMesh=Resources.GetBuiltinResource<Mesh>("Sphere.fbx");go.GetComponent<MeshRenderer>().sharedMaterial=material;go.GetComponent<MeshRenderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;Vector3 size=new Vector3(radius*.55f,.014f,radius*.4f);go.transform.localScale=size;drops.Add(go.transform);sizes.Add(size);
            }
        }
        void Update(){float t=Mathf.Clamp01((Time.time-born)/14);for(int i=0;i<drops.Count;i++)if(drops[i])drops[i].localScale=sizes[i]*Mathf.Pow(1-t,1+i*.08f);if(t>=1)Destroy(gameObject);}
        void OnDestroy(){if(material)Destroy(material);}
    }
}
