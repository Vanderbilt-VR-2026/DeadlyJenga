using System;
using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CandyAudio
{
    // Plays each speaker's voice stream from an AudioSource placed where that speaker is.
    // Made through Core's VoicePlayback; the social layer only hands it samples.
    public sealed class VoicePlayer : MonoBehaviour, IVoicePlayer
    {
        sealed class Speaker
        {public AudioSource source;public AudioClip clip;public VoiceRing ring=new VoiceRing(32000);public bool muted;public float volume=1,duck=1,last;}
        readonly Dictionary<string,Speaker> speakers=new Dictionary<string,Speaker>();
        IVoiceMix mix;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()=>VoicePlayback.Register((host,mix)=>{var p=host.AddComponent<VoicePlayer>();p.mix=mix;return p;});

        public int SpeakerCount=>speakers.Count;
        public bool IsMuted(string id)=>speakers.TryGetValue(id,out var s)&&s.muted;
        public float Volume(string id)=>speakers.TryGetValue(id,out var s)?s.volume:1;
        Speaker Get(string id)
        {
            if(speakers.TryGetValue(id,out var s))return s;if(speakers.Count>=64)return null;
            var name=(id??"").Length>60?id.Substring(0,60):id;
            var go=new GameObject("Voice · "+name);go.transform.SetParent(transform,false);
            s=new Speaker{source=go.AddComponent<AudioSource>()};s.source.playOnAwake=false;s.source.loop=true;s.source.dopplerLevel=0;s.source.priority=80;
            s.clip=AudioClip.Create("Independent voice stream",16000,1,16000,true,s.ring.Read);s.source.clip=s.clip;speakers.Add(id,s);return s;
        }
        public void Accept(string speaker,float[] samples,bool end)
        {
            var s=Get(speaker);if(s==null||s.muted)return;s.last=Time.unscaledTime;s.ring.Write(samples);
            if(!s.source.isPlaying&&(s.ring.Count>=1600||end&&s.ring.Count>0))s.source.Play();
        }
        public void Mute(string id,bool value){var s=Get(id);if(s!=null){s.muted=value;if(value){s.ring.Clear();s.source.Stop();}}}
        public void SetVolume(string id,float value){var s=Get(id);if(s!=null)s.volume=Mathf.Clamp01(value);}
        public void SetDuck(string id,float value){if(speakers.TryGetValue(id,out var s))s.duck=Mathf.Clamp01(value);}
        public void Remove(string id){if(!speakers.TryGetValue(id,out var s))return;s.source.Stop();Destroy(s.source.gameObject);Destroy(s.clip);speakers.Remove(id);}
        public void Clear(){foreach(var id in new List<string>(speakers.Keys))Remove(id);}
        void Update()
        {
            if(mix==null)return;
            foreach(var entry in speakers)
            {
                var s=entry.Value;float gain=mix.Gain(entry.Key);
                s.source.volume=Mathf.MoveTowards(s.source.volume,s.muted?0:s.volume*s.duck*gain,Time.unscaledDeltaTime*6);
                s.source.spatialBlend=mix.SpatialBlend;if(mix.TryPosition(entry.Key,out var point))s.source.transform.position=point;
                s.source.pitch=Mathf.Clamp(mix.Pitch(entry.Key),.9f,1.1f);
                if(Time.unscaledTime-s.last>1&&s.ring.Count==0)s.source.Stop();
            }
        }
        void OnDestroy(){Clear();}
    }

    // Thread-safe sample ring: the network writes, Unity's audio thread reads.
    sealed class VoiceRing
    {
        readonly float[] data;int read,write,count;readonly object sync=new object();
        public VoiceRing(int samples){data=new float[Math.Max(64,samples)];}
        public int Count{get{lock(sync)return count;}}
        public void Write(float[] samples)
        {if(samples==null)return;lock(sync)foreach(var value in samples){if(count==data.Length){read=(read+1)%data.Length;count--;}data[write]=value;write=(write+1)%data.Length;count++;}}
        public void Read(float[] output)
        {lock(sync)for(int i=0;i<output.Length;i++){output[i]=count>0?data[read]:0;if(count>0){read=(read+1)%data.Length;count--;}}}
        public void Clear(){lock(sync){read=write=count=0;}}
    }
}
