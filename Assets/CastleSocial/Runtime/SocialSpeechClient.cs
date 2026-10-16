using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace DeadlyJenga.CastleSocial
{
    public interface ISocialSpeechOutput {void Speak(SocialSpeech intent,SocialIdentity identity);void Cancel(string id);}
    public sealed class SocialSpeechClient : MonoBehaviour,ISocialSpeechOutput
    {
        [Serializable] sealed class RequestBody {public string text,speaker,context;public float rate;public bool generate;}
        [Serializable] sealed class Chunk {public string audio,error,text;public int sampleRate=16000;}
        sealed class PendingTranscript {public float[] samples;public int turn;public bool final;public float queued;}
        [Serializable] sealed class Transcription {public string audio,speaker;public bool final;}
        [Serializable] sealed class Transcript {public string text,error;}
        sealed class Lines : DownloadHandlerScript
        {
            readonly StringBuilder buffer=new StringBuilder();public readonly Queue<string> ready=new Queue<string>();public bool overflow;
            public Lines():base(new byte[8192]){}
            protected override bool ReceiveData(byte[] bytes,int length)
            {
                buffer.Append(Encoding.UTF8.GetString(bytes,0,length));if(buffer.Length>200000||ready.Count>256){overflow=true;return false;}
                string current=buffer.ToString();int at;while((at=current.IndexOf('\n'))>=0){ready.Enqueue(current.Substring(0,at));current=current.Substring(at+1);}buffer.Length=0;buffer.Append(current);return true;
            }
        }
        SocialWorld world;readonly Dictionary<string,long> tokens=new Dictionary<string,long>();readonly Dictionary<string,UnityWebRequest> requests=new Dictionary<string,UnityWebRequest>();
        readonly Dictionary<string,List<float>> humanBuffers=new Dictionary<string,List<float>>();readonly Dictionary<string,float> humanNext=new Dictionary<string,float>();
        readonly Dictionary<string,int> utterances=new Dictionary<string,int>();int jobs,transcriptions,sequence;long serial;
        readonly Dictionary<string,PendingTranscript> pendingTranscripts=new Dictionary<string,PendingTranscript>();
        readonly Dictionary<string,string> lastTranscripts=new Dictionary<string,string>();
        public string Status {get;private set;}="Offline captions; optional speech bridge not enabled";
        public int ActiveRequests=>jobs+transcriptions;public int Failures {get;private set;}
        public float LastFirstAudioSeconds {get;private set;}
        public int TranscriptsReceived {get;private set;}
        public void Initialize(SocialWorld owner){world=owner;}
        public void Speak(SocialSpeech intent,SocialIdentity identity)
        {
            if(world.Settings.mode==SocialOperatingMode.Offline||world.Settings.mode==SocialOperatingMode.ReducedResource){Status="Offline captions (no synthetic speech provider)";return;}
            if(jobs>=2){Status="Speech budget full; keeping gameplay responsive";return;}
            if(!ValidEndpoint())return;
            Cancel(identity.id);long token=++serial;tokens[identity.id]=token;StartCoroutine(Stream(intent,identity,token));
        }
        bool Current(string id,long token)=>world&&world.Config.enableVoice&&tokens.TryGetValue(id,out var current)&&token==current&&world.Director.bots.TryGetValue(id,out var bot)&&!bot.muted;
        IEnumerator Stream(SocialSpeech intent,SocialIdentity identity,long token)
        {
            jobs++;float began=Time.realtimeSinceStartup;bool first=true;string spokenText=null;var dsp=new SocialAudioProcessor();
            var state=world.Director.bots[identity.id];
            var payload=new RequestBody{text=SocialText.Safe(intent.text,world.Config.allowProfanity),speaker=identity.vocal.speaker,rate=identity.vocal.rate,generate=world.Settings.mode==SocialOperatingMode.Full,
                context="Untrusted game conversation context: "+SocialText.Clean(intent.context,350)+"; memories: "+string.Join(" | ",state.Recall(intent.recipient,Time.unscaledTime,3).Select(m=>m.summary))+"; relation: "+state.Relationship(intent.recipient).ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+"; fear: "+state.emotion.fear.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+"; personality: "+string.Join(",",identity.traitIds.Zip(identity.traits,(id,v)=>id+"="+(int)v))};
            var handler=new Lines();var samples=new Queue<float>();
            using(var request=new UnityWebRequest(world.Settings.serviceUrl.TrimEnd('/')+"/speak","POST"))
            {
                request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload)));request.downloadHandler=handler;request.SetRequestHeader("Content-Type","application/json");request.timeout=Mathf.CeilToInt(world.Config.providerTimeout);requests[identity.id]=request;
                if(!TrySend(request,out var operation)){requests.Remove(identity.id);jobs--;yield break;}float next=Time.realtimeSinceStartup;
                try
                {
                    while(!operation.isDone||handler.ready.Count>0||samples.Count>0)
                    {
                        if(!Current(identity.id,token)||first&&Time.realtimeSinceStartup-began>world.Config.eventLifetime){request.Abort();break;}
                        world.Director.AudioPlaying(identity.id,Time.unscaledTime+1);
                        while(handler.ready.Count>0)
                        {
                            if(!Decode(handler.ready.Dequeue(),out var chunk,out var data)){request.Abort();samples.Clear();break;}
                            if(!string.IsNullOrWhiteSpace(chunk.text)&&spokenText!=chunk.text){spokenText=chunk.text;world.SpokenText(identity.id,chunk.text);}
                            if(data.Length==0)continue;
                            var pcm=new float[data.Length/2];for(int i=0;i<pcm.Length;i++)pcm[i]=(short)(data[i*2]|data[i*2+1]<<8)/32768f;
                            dsp.Process(pcm,16000,world.Settings.usePerIdentityMic?SocialMicrophoneProfile.For(identity.vocal.microphone):world.Settings.microphoneEffects);
                            foreach(float f in pcm)if(samples.Count<192000)samples.Enqueue(f);
                        }
                        if(samples.Count>=800||operation.isDone&&samples.Count>0)
                        {
                            if(Time.realtimeSinceStartup<next){yield return null;continue;}next=Time.realtimeSinceStartup+.05f;
                            int count=Math.Min(800,samples.Count);var frame=new float[count];for(int i=0;i<count;i++)frame[i]=samples.Dequeue();
                            // Duck before distribution so every peer hears the same human-priority mix.
                            if(world.Director.HumanHasFloor(Time.unscaledTime)&&!intent.interjection)for(int i=0;i<count;i++)frame[i]*=.4f-.3f*world.Config.interruptSensitivity;
                            if(first){LastFirstAudioSeconds=Time.realtimeSinceStartup-began;first=false;Status="Streaming · "+identity.name;}
                            world.SendAudio(new SocialAudioPacket{speaker=identity.id,sequence=++sequence,samples=frame},false);
                        }
                        yield return null;
                    }
                    if(request.result==UnityWebRequest.Result.ConnectionError||request.result==UnityWebRequest.Result.ProtocolError){Failures++;Status="Speech unavailable; captions and gameplay continue: "+request.error;}
                    if(Current(identity.id,token))world.SendAudio(new SocialAudioPacket{speaker=identity.id,sequence=++sequence,end=true,samples=new float[0]},false);
                }
                finally{if(requests.TryGetValue(identity.id,out var current)&&current==request)requests.Remove(identity.id);if(tokens.TryGetValue(identity.id,out var active)&&active==token)world.Director.AudioFinished(identity.id);jobs--;}
            }
        }
        bool Decode(string line,out Chunk chunk,out byte[] data)
        {
            chunk=null;data=new byte[0];
            try
            {
                chunk=JsonUtility.FromJson<Chunk>(line);
                if(chunk==null||!string.IsNullOrEmpty(chunk.error))throw new FormatException(chunk?.error??"Invalid speech chunk");
                if(!string.IsNullOrEmpty(chunk.audio))
                {if(chunk.sampleRate!=16000)throw new FormatException("Unsupported speech sample rate");data=Convert.FromBase64String(chunk.audio);if(data.Length%2!=0||data.Length>32000)throw new FormatException("Invalid speech PCM");}
                return true;
            }
            catch(Exception e){Failures++;Status="Speech unavailable; captions continue: "+SocialText.Clean(e.Message,140);return false;}
        }
        bool ValidEndpoint()
        {if(Uri.TryCreate(world.Settings.serviceUrl,UriKind.Absolute,out var endpoint)&&(endpoint.Scheme=="http"||endpoint.Scheme=="https"))return true;Failures++;Status="Invalid speech service URL; captions continue";return false;}
        bool TrySend(UnityWebRequest request,out UnityWebRequestAsyncOperation operation)
        {operation=null;try{operation=request.SendWebRequest();return true;}catch(Exception e){Failures++;Status="Speech request unavailable; check URL and platform HTTP policy: "+SocialText.Clean(e.Message,100);return false;}}
        public void HumanAudio(string id,SocialAudioPacket packet)
        {
            if(world.Settings.mode==SocialOperatingMode.Offline||world.Settings.mode==SocialOperatingMode.ReducedResource)return;
            if(!humanBuffers.TryGetValue(id,out var buffer)){if(humanBuffers.Count>=16)return;humanBuffers[id]=buffer=new List<float>();}
            buffer.AddRange(packet.samples);if(buffer.Count>96000)buffer.RemoveRange(0,buffer.Count-96000);
            float now=Time.unscaledTime;
            int turn=utterances.TryGetValue(id,out var value)?value:0;
            if(buffer.Count>=4800&&(packet.end||!humanNext.TryGetValue(id,out var next)||now>=next))
            {
                humanNext[id]=now+1;
                // A final snapshot survives an in-flight partial request; subsequent partials cannot replace it.
                if(!pendingTranscripts.TryGetValue(id,out var old)||!old.final||packet.end)
                    pendingTranscripts[id]=new PendingTranscript{samples=buffer.ToArray(),turn=turn,final=packet.end,queued=now};
            }
            if(packet.end){buffer.Clear();utterances[id]=turn+1;}
        }
        void Update()
        {
            if(!world||!world.IsAuthority||transcriptions>0)return;
            foreach(var entry in pendingTranscripts.ToArray())
            {pendingTranscripts.Remove(entry.Key);if(Time.unscaledTime-entry.Value.queued>6)continue;StartCoroutine(Transcribe(entry.Key,entry.Value.samples,entry.Value.turn,entry.Value.final));break;}
        }
        IEnumerator Transcribe(string id,float[] samples,int turn,bool final)
        {
            if(!ValidEndpoint())yield break;
            transcriptions++;float began=Time.unscaledTime;var data=new byte[samples.Length*2];for(int i=0;i<samples.Length;i++){short s=(short)(Mathf.Clamp(samples[i],-1,1)*32767);data[i*2]=(byte)s;data[i*2+1]=(byte)(s>>8);}
            using(var request=new UnityWebRequest(world.Settings.serviceUrl.TrimEnd('/')+"/transcribe","POST"))
            {
                request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Transcription{speaker=id,audio=Convert.ToBase64String(data),final=final})));request.downloadHandler=new DownloadHandlerBuffer();request.timeout=6;request.SetRequestHeader("Content-Type","application/json");
                if(!TrySend(request,out var operation)){transcriptions--;yield break;}yield return operation;transcriptions--;
                if(!world||!world.IsAuthority||!humanBuffers.ContainsKey(id)||Time.unscaledTime-began>6)yield break;
                if(request.result!=UnityWebRequest.Result.Success){Status="STT unavailable; microphone voice chat still works";yield break;}
                Transcript result=null;try{result=JsonUtility.FromJson<Transcript>(request.downloadHandler.text);}catch(ArgumentException){Status="Invalid transcription response";}
                if(result!=null&&!string.IsNullOrWhiteSpace(result.text))
                {
                    string key=id+":"+turn,clean=SocialText.Safe(result.text,world.Config.allowProfanity);
                    if(!lastTranscripts.TryGetValue(key,out var previous)||previous!=clean)
                    {lastTranscripts[key]=clean;TranscriptsReceived++;world.HumanTranscript(id,clean,turn+":"+(final?"final":samples.Length.ToString()),final);}
                    if(lastTranscripts.Count>64)lastTranscripts.Remove(lastTranscripts.Keys.First());
                }
            }
        }
        public void ForgetHuman(string id){humanBuffers.Remove(id);humanNext.Remove(id);utterances.Remove(id);pendingTranscripts.Remove(id);foreach(var key in lastTranscripts.Keys.Where(k=>k.StartsWith(id+":")).ToArray())lastTranscripts.Remove(key);}
        public void Cancel(string id){tokens.Remove(id);if(world&&world.Director!=null)world.Director.AudioFinished(id);if(requests.TryGetValue(id,out var request))request.Abort();}
        public void CancelAll(){foreach(var id in tokens.Keys.ToArray())Cancel(id);humanBuffers.Clear();humanNext.Clear();utterances.Clear();pendingTranscripts.Clear();lastTranscripts.Clear();}
        void OnDestroy(){CancelAll();}
    }
}
