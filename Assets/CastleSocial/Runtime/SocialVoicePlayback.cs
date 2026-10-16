using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastleSocial
{
    public sealed class SocialVoicePlayback : MonoBehaviour
    {
        sealed class Speaker
        {public AudioSource source;public AudioClip clip;public SocialPcmRing ring=new SocialPcmRing(32000);public bool muted;public float volume=1,duck=1,last;}
        readonly Dictionary<string,Speaker> speakers=new Dictionary<string,Speaker>();SocialWorld world;
        public int SpeakerCount=>speakers.Count;
        public bool IsMuted(string id)=>speakers.TryGetValue(id,out var s)&&s.muted;
        public float Volume(string id)=>speakers.TryGetValue(id,out var s)?s.volume:1;
        public void Initialize(SocialWorld owner){world=owner;}
        Speaker Get(string id)
        {
            if(speakers.TryGetValue(id,out var s))return s;if(speakers.Count>=64)return null;
            var go=new GameObject("Voice · "+SocialText.Clean(id,60));go.transform.SetParent(transform,false);
            s=new Speaker{source=go.AddComponent<AudioSource>()};s.source.playOnAwake=false;s.source.loop=true;s.source.dopplerLevel=0;s.source.priority=80;
            s.clip=AudioClip.Create("Independent voice stream",16000,1,16000,true,s.ring.Read);s.source.clip=s.clip;speakers.Add(id,s);return s;
        }
        public void Accept(SocialAudioPacket p)
        {
            var s=Get(p.speaker);if(s==null||s.muted)return;s.last=Time.unscaledTime;s.ring.Write(p.samples);
            if(!s.source.isPlaying&&(s.ring.Count>=1600||p.end&&s.ring.Count>0))s.source.Play();
        }
        public void Mute(string id,bool value){var s=Get(id);if(s!=null){s.muted=value;if(value){s.ring.Clear();s.source.Stop();}}}
        public void SetVolume(string id,float value){var s=Get(id);if(s!=null)s.volume=Mathf.Clamp01(value);}
        public void SetDuck(string id,float value){if(speakers.TryGetValue(id,out var s))s.duck=Mathf.Clamp01(value);}
        public void Remove(string id){if(!speakers.TryGetValue(id,out var s))return;s.source.Stop();Destroy(s.source.gameObject);Destroy(s.clip);speakers.Remove(id);}
        public void Clear(){foreach(var id in new List<string>(speakers.Keys))Remove(id);}
        void Update()
        {
            if(!world||!world.Settings)return;
            foreach(var entry in speakers)
            {
                var s=entry.Value;bool bot=entry.Key.StartsWith("social:");
                float gain=bot?(world.Config.enableVoice?world.Settings.botVolume:0):world.Settings.humanVolume;
                s.source.volume=Mathf.MoveTowards(s.source.volume,s.muted?0:s.volume*s.duck*gain,Time.unscaledDeltaTime*6);
                s.source.spatialBlend=world.Settings.spatialBlend;if(world.TryPosition(entry.Key,out var point))s.source.transform.position=point;
                if(world.Participants.TryGetValue(entry.Key,out var identity)&&identity.identity!=null)s.source.pitch=Mathf.Clamp(identity.identity.vocal.pitch,.9f,1.1f);
                if(Time.unscaledTime-s.last>1&&s.ring.Count==0)s.source.Stop();
            }
        }
        void OnDestroy(){Clear();}
    }
}
