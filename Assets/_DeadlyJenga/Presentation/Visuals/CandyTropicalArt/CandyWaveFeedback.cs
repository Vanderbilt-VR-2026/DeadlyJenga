using System.Collections.Generic;
using DeadlyJenga.Core;
using DeadlyJenga.CandyAudio;
using UnityEngine;

namespace DeadlyJenga.CandyArt
{
    public sealed class CandyWaveFeedback : MonoBehaviour
    {
        readonly HashSet<IWaveState> known=new HashSet<IWaveState>(),impact=new HashSet<IWaveState>();float nextScan;bool hadWave;
        void Update()
        {
            if(Time.time<nextScan)return;nextScan=Time.time+.15f;var audio=CandyAudioEngine.Instance;var world=Services.World;if(!audio||world==null)return;bool active=false;
            foreach(var wave in Tracked.Waves)
            {
                var f=wave.Wave;if(!f.active)continue;active=true;
                if(known.Add(wave))audio.Play(CandySound.DisasterHorn,f.center,.85f,GetInstanceID(),1,true);
                if(f.warningRemaining>0){audio.Loop(CandySound.DisasterTension,transform,.45f,true,Mathf.Lerp(.8f,1.3f,1-Mathf.Clamp01(f.warningRemaining/8)));continue;}
                Vector3 direction=Vector3.ProjectOnPlane(f.direction,Vector3.up).normalized,side=Vector3.Cross(Vector3.up,direction);bool touches=false;
                foreach(var body in world.Blocks)
                {
                    if(!body||!body.gameObject.activeInHierarchy)continue;Vector3 d=body.position-f.center;
                    if(Mathf.Abs(Vector3.Dot(d,direction))<Mathf.Max(1,f.depth*.5f)&&Mathf.Abs(Vector3.Dot(d,side))<f.width*.5f&&body.position.y<f.waterHeight+f.height){touches=true;break;}
                }
                if(touches)
                {
                    if(impact.Add(wave))audio.Play(CandySound.WaveImpact,f.center,1,GetInstanceID());
                    audio.Play(CandySound.WaveSpray,f.center,.65f,GetInstanceID());
                    if(audio.Listener&&Mathf.Abs(Vector3.Dot(audio.Listener.position-f.center,direction))<f.depth)CandyFeedback.Pulse(.15f,.16f);
                }
            }
            if(hadWave&&!active){Puddle();known.Clear();impact.Clear();}hadWave=active;
        }
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
