using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CandyAudio
{
    // Presentation only: never writes gameplay, transforms of actors, or physics.
    public sealed class CandyAudioEngine : MonoBehaviour
    {
        public static CandyAudioEngine Instance {get;private set;}
        public CandyAudioBank bank;
        [Range(0,1)] public float auditionDucking=1;
        public int Played {get;private set;}
        public int Culled {get;private set;}
        public int ActiveVoices {get;private set;}
        public Transform Listener {get;private set;}
        sealed class Voice
        {
            public AudioSource source;public AudioLowPassFilter filter;public CandyAudioCue cue;
            public Transform follow;public bool looping,reserved;public float gain,target,requestedAt;public int owner;
        }
        readonly Dictionary<CandySound,CandyAudioCue> cues=new Dictionary<CandySound,CandyAudioCue>();
        readonly Dictionary<CandySound,int> previousClip=new Dictionary<CandySound,int>();
        readonly Dictionary<long,float> cooldowns=new Dictionary<long,float>();
        readonly List<Voice> voices=new List<Voice>();
        CandyAudioBank current;float nextListener,tokenAt;float tokens;bool focus=true;float focusGain=1;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics(){Instance=null;}
        public void Configure(CandyAudioBank value){bank=value;Rebuild();}
        void Awake(){if(Instance&&Instance!=this){Destroy(gameObject);return;}Instance=this;}
        void Rebuild()
        {
            foreach(var voice in voices)if(voice.source)Destroy(voice.source.gameObject);voices.Clear();cues.Clear();cooldowns.Clear();previousClip.Clear();
            current=bank;if(!bank)return;
            foreach(var cue in bank.cues)if(cue!=null)cues[cue.sound]=cue;
            // Bank-authored recordings override the lightweight original breath defaults.
            foreach(var sound in CandyBreathing.Sounds)if(!cues.ContainsKey(sound))cues[sound]=CandyBreathing.DefaultCue(sound);
            foreach(var sound in CandyDisasterSounds.Sounds)if(!cues.ContainsKey(sound))cues[sound]=CandyDisasterSounds.DefaultCue(sound);
            foreach(var sound in CandyWeaponSounds.Sounds)if(!cues.ContainsKey(sound))cues[sound]=CandyWeaponSounds.DefaultCue(sound);
            foreach(var sound in CandyJaguarSounds.Sounds)if(!cues.ContainsKey(sound))cues[sound]=CandyJaguarSounds.DefaultCue(sound);
            foreach(var sound in CandyPlayerSounds.Sounds)if(!cues.ContainsKey(sound))cues[sound]=CandyPlayerSounds.DefaultCue(sound);
            for(int i=0;i<bank.oneShotVoices+bank.loopVoices;i++)
            {
                var go=new GameObject("Pooled candy voice "+i);go.transform.SetParent(transform,false);
                var source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.dopplerLevel=0;source.rolloffMode=AudioRolloffMode.Linear;
                var filter=go.AddComponent<AudioLowPassFilter>();filter.enabled=false;
                voices.Add(new Voice{source=source,filter=filter,looping=i>=bank.oneShotVoices,reserved=i>=bank.oneShotVoices&&i<bank.oneShotVoices+4});
            }
            tokens=bank.eventsPerSecond;tokenAt=Time.unscaledTime;
        }
        bool FindCue(CandySound sound,out CandyAudioCue cue)=>cues.TryGetValue(sound,out cue)&&cue.enabled&&cue.variants!=null&&cue.variants.Length>0;
        AudioClip Pick(CandyAudioCue cue)
        {
            int count=cue.variants.Length,index=Random.Range(0,count);
            if(count>1&&previousClip.TryGetValue(cue.sound,out int last)&&index==last)index=(index+1+Random.Range(0,count-1))%count;
            previousClip[cue.sound]=index;return cue.variants[index];
        }
        bool Audible(CandyAudioCue cue,Vector3 position,bool local)=>local||Listener&&(position-Listener.position).sqrMagnitude<cue.maxDistance*cue.maxDistance;
        public bool Play(CandySound sound,Vector3 position,float intensity=1,int owner=0,float pitch=1,bool local=false)
        {
            if(!bank||!FindCue(sound,out var cue)||!Audible(cue,position,local)){Culled++;return false;}
            long key=((long)owner<<8)^(int)sound;float now=Time.unscaledTime;
            if(cooldowns.TryGetValue(key,out float until)&&now<until){Culled++;return false;}
            tokens=Mathf.Min(bank.eventsPerSecond,tokens+(now-tokenAt)*bank.eventsPerSecond);tokenAt=now;
            if(tokens<1||cue.bus==CandyBus.World&&tokens<4){Culled++;return false;}
            int same=0;Voice free=null,steal=null;float weakest=float.PositiveInfinity;
            foreach(var voice in voices)
            {
                if(voice.looping)continue;
                if(!voice.source.isPlaying){if(free==null)free=voice;continue;}
                if(voice.cue==cue)same++;
                float importance=voice.source.volume*(257-voice.source.priority);
                if(importance<weakest){weakest=importance;steal=voice;}
            }
            if(same>=cue.maxVoices){Culled++;return false;}
            float gain=Mathf.Clamp(intensity,.03f,1.3f);
            if(free==null)
            {
                if(steal==null||gain*cue.volume*bank.Gain(cue.bus)*(257-cue.priority)<=weakest){Culled++;return false;}
                free=steal;
            }
            if(!Prepare(free,cue,position,local,pitch))return false;
            free.gain=gain;free.follow=null;free.source.loop=false;free.source.volume=Volume(free);free.source.Play();
            cooldowns[key]=now+cue.cooldown;tokens--;Played++;return true;
        }
        bool Prepare(Voice voice,CandyAudioCue cue,Vector3 position,bool local,float pitch)
        {
            var clip=Pick(cue);if(!clip)return false;
            voice.source.Stop();voice.cue=cue;voice.source.clip=clip;voice.source.transform.position=position;
            voice.source.spatialBlend=local?0:1;voice.source.minDistance=Mathf.Max(.1f,cue.minDistance);voice.source.maxDistance=Mathf.Max(cue.minDistance+1,cue.maxDistance);
            voice.source.priority=Mathf.Clamp(cue.priority,0,256);voice.source.outputAudioMixerGroup=cue.output;
            voice.source.pitch=Mathf.Clamp(Random.Range(cue.pitch.x,cue.pitch.y)*pitch,.5f,1.8f);voice.filter.enabled=false;return true;
        }
        // Repeated requests are smoothed into one bounded voice. Silence releases it.
        public void Loop(CandySound sound,Transform owner,float intensity,bool local=false,float pitch=1,float muffling=0)
        {
            if(!bank||!owner||intensity<.005f||!FindCue(sound,out var cue)||!Audible(cue,owner.position,local))return;
            int id=owner.GetInstanceID();Voice selected=null;
            foreach(var v in voices)if(v.looping&&v.cue==cue&&v.owner==id){selected=v;break;}
            if(selected==null)
            {
                // One breath voice per player. Fade the old state before replacing it,
                // rather than competing with surf/music or overlapping three breaths.
                if(CandyBreathing.IsBreath(sound))foreach(var v in voices)
                {
                    if(!v.looping||v.owner!=id||v.cue==null||!CandyBreathing.IsBreath(v.cue.sound))continue;
                    if(v.source.isPlaying&&v.gain>.02f){v.target=0;v.requestedAt=Time.unscaledTime;return;}
                    v.source.Stop();v.cue=null;
                }
                foreach(var v in voices)if(v.looping&&v.reserved==local&&!v.source.isPlaying){selected=v;break;}
                if(selected==null)
                {
                    // Contact friction is more informative than another distant
                    // hand's flight wind. Never steal a more important loop.
                    foreach(var v in voices)if(v.looping&&v.reserved==local&&v.cue!=null&&v.cue.priority>cue.priority&&
                        (selected==null||v.cue.priority>selected.cue.priority))selected=v;
                    if(selected==null)return;
                }
                if(!Prepare(selected,cue,owner.position,local,pitch))return;
                selected.owner=id;selected.follow=owner;selected.gain=0;selected.source.volume=0;selected.source.loop=true;selected.source.time=Random.Range(0,Mathf.Max(0,selected.source.clip.length-.05f));selected.source.Play();
            }
            selected.target=Mathf.Clamp01(intensity);selected.requestedAt=Time.unscaledTime;
            selected.source.pitch=Mathf.Clamp(pitch,.65f,1.5f);selected.filter.enabled=muffling>.03f;selected.filter.cutoffFrequency=Mathf.Lerp(18000,1800,Mathf.Clamp01(muffling));
        }
        float Volume(Voice v)=>v.gain*v.cue.volume*bank.master*bank.Gain(v.cue.bus)*focusGain*auditionDucking;
        void Update()
        {
            if(bank!=current)Rebuild();if(!bank)return;
            var activeView=DeadlyJenga.Core.Services.Session?.ActiveCamera;if(activeView)Listener=activeView.transform;
            if(Time.unscaledTime>=nextListener)
            {
                nextListener=Time.unscaledTime+.5f;
                if(!activeView){Listener=null;foreach(var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))if(listener.isActiveAndEnabled){Listener=listener.transform;break;}}
                if(cooldowns.Count>4096)cooldowns.Clear();
            }
            focusGain=Mathf.MoveTowards(focusGain,focus?1:0,Time.unscaledDeltaTime*4);ActiveVoices=0;
            foreach(var voice in voices)
            {
                // Stop can reach the audio thread after this frame; a released
                // voice must not be mixed using the cue that was just cleared.
                if(voice.cue==null){if(voice.source.isPlaying)voice.source.Stop();continue;}
                if(!voice.source.isPlaying)continue;
                if(voice.looping)
                {
                    if(!voice.follow||!voice.follow.gameObject.activeInHierarchy||Time.unscaledTime-voice.requestedAt>.25f)voice.target=0;
                    if(voice.follow&&voice.source.spatialBlend>.5f)voice.source.transform.position=voice.follow.position;
                    voice.gain=Mathf.MoveTowards(voice.gain,voice.target,Time.unscaledDeltaTime*2);
                    if(voice.target==0&&voice.gain<.001f){voice.source.Stop();voice.cue=null;continue;}
                }
                voice.source.volume=Volume(voice);ActiveVoices++;
            }
        }
        void OnApplicationFocus(bool value){focus=value;}
        void OnDestroy(){if(Instance==this)Instance=null;}
    }
}
