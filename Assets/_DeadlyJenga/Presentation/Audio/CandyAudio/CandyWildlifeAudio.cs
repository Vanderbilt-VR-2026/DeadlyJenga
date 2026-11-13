
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CandyAudio
{
    public sealed class CandyWildlifeAudio : MonoBehaviour
    {
        struct Call
        {
            public readonly float pitch, end, breath, duration;
            public readonly int syllables;
            public Call(float pitch,float end,float breath,float duration,int syllables)
            {
                this.pitch=pitch;
                this.end=end;
                this.breath=breath;
                this.duration=duration;
                this.syllables=syllables;
            }
        }

        static readonly Dictionary<string,Call> Calls=new Dictionary<string,Call>
        {
            ["otter"]=new Call(830,1340,.15f,.38f,2),
            ["fox"]=new Call(530,330,.42f,.32f,2),
            ["tortoise"]=new Call(210,145,.73f,.25f,1),
            ["jaguar"]=new Call(142,93,.51f,.47f,2),
            ["penguin"]=new Call(610,365,.24f,.51f,2),
            ["ray"]=new Call(350,560,.53f,.44f,1),
            ["crab"]=new Call(930,510,.89f,.29f,3),
            ["frog"]=new Call(235,175,.32f,.46f,2),
            ["boar"]=new Call(188,105,.65f,.40f,2),
            ["monkey"]=new Call(710,1160,.19f,.52f,3),
            ["seal"]=new Call(420,255,.47f,.53f,2),
            ["beetle"]=new Call(1270,880,.80f,.34f,3),
        };
        static readonly Dictionary<string,CandyAudioCue> Cues=new Dictionary<string,CandyAudioCue>();
        public static readonly string[] Species={"otter","fox","tortoise","jaguar","penguin","ray","crab","frog","boar","monkey","seal","beetle"};
        readonly Dictionary<Transform,float> due=new Dictionary<Transform,float>();
        readonly List<Transform> expired=new List<Transform>();
        ISessionInfo session;
        float nextScan,nextCall;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            foreach(var cue in Cues.Values)foreach(var clip in cue.variants)if(clip)Destroy(clip);
            Cues.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            GameEvents.LobbyStarted+=Attach;
            GameEvents.SessionStarted+=Attach;
        }

        static void Attach(GameObject host)
        {
            if(host&&!host.GetComponent<CandyWildlifeAudio>())
                host.AddComponent<CandyWildlifeAudio>();
        }

        void Awake()=>session=GetComponent<ISessionInfo>();

        void Update()
        {
            float now=Time.unscaledTime;
            if(now<nextScan||session==null||!ReferenceEquals(session,Services.Session))return;
            nextScan=now+.24f;
            var engine=CandyAudioEngine.Instance;
            var eye=session.ActiveCamera;
            if(!engine||!eye)return;

            expired.Clear();
            foreach(var pair in due)if(!pair.Key||pair.Key.parent!=transform)expired.Add(pair.Key);
            foreach(var animal in expired)due.Remove(animal);
            if(now<nextCall)return;

            // The population parents its wildlife directly to the session object.
            Transform closest=null;
            string species=null;
            float distance=18*18;
            foreach(Transform child in transform)
            {
                if(!child.gameObject.activeInHierarchy||!child.TryGetComponent<IWildlifeActor>(out var animal)||
                    !Calls.ContainsKey(animal.Species))continue;
                if(!due.TryGetValue(child,out float ready))
                {
                    ready=now+Random.Range(.25f,1.05f);
                    due.Add(child,ready);
                }
                float candidate=(child.position-eye.transform.position).sqrMagnitude;
                if(now<ready||candidate>=distance)continue;
                closest=child;
                species=animal.Species;
                distance=candidate;
            }
            if(!closest)return;

            if(engine.PlayCue(Cue(species),closest.position+Vector3.up*.2f,.82f,closest.GetInstanceID()))
            {
                due[closest]=now+Random.Range(7,15);
                nextCall=now+1.2f;
            }
        }

        public static CandyAudioCue Cue(string species)
        {
            if(Cues.TryGetValue(species,out var cue)&&cue.variants!=null&&cue.variants.Length>0&&cue.variants[0])return cue;
            var shape=Calls[species];
            int index=System.Array.IndexOf(Species,species);
            const int rate=22050;
            var samples=new float[Mathf.CeilToInt(rate*shape.duration)];
            var random=new System.Random(2137+index*113);
            float phase=0,filtered=0;
            for(int i=0;i<samples.Length;i++)
            {
                float time=i/(float)rate;
                float elapsed=time/shape.duration;
                float syllable=Mathf.Repeat(elapsed*shape.syllables,1);
                float envelope=Mathf.SmoothStep(0,1,Mathf.Clamp01(syllable*11))*
                    (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.53f,.96f,syllable)));
                float frequency=Mathf.Lerp(shape.pitch,shape.end,syllable)+
                    Mathf.Sin(time*(32+index))*shape.pitch*.045f;
                phase+=frequency*2*Mathf.PI/rate;
                float noise=(float)random.NextDouble()*2-1;
                filtered=Mathf.Lerp(filtered,noise,.19f);
                float breath=species=="crab"||species=="beetle"?noise-filtered:filtered*1.6f;
                float voice=Mathf.Sin(phase)*.52f+Mathf.Sin(phase*2)*.17f+Mathf.Sin(phase*3)*.06f;
                samples[i]=Mathf.Clamp(Mathf.Lerp(voice,breath,shape.breath)*envelope,-.8f,.8f);
            }
            var clip=AudioClip.Create("Wildlife "+species,samples.Length,1,rate,false);
            clip.SetData(samples,0);
            cue=new CandyAudioCue
            {
                sound=(CandySound)(900+index),
                variants=new[]{clip},
                bus=CandyBus.Character,
                pitch=new Vector2(.94f,1.06f),
                volume=.65f,
                minDistance=1.7f,
                maxDistance=18,
                cooldown=1,
                maxVoices=2,
                priority=135
            };
            Cues.Add(species,cue);
            return cue;
        }
    }
}
