using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
namespace DeadlyJenga.CastleSocial
{
    public sealed class OnDeviceConversation : MonoBehaviour
    {
        sealed class Input {public int number,turn;public readonly List<float> samples=new List<float>();public bool final;public float next;}
        sealed class Reply {public SocialSpeech intent;public SocialIdentity identity;public float at;}
        SocialWorld world;OnDeviceNative worker;Coroutine loading,playing;Reply pending;
        readonly Dictionary<string,Input> inputs=new Dictionary<string,Input>();
        readonly Dictionary<string,bool> previousVoices=new Dictionary<string,bool>();
        readonly Dictionary<string,float[]> cache=new Dictionary<string,float[]>();readonly Queue<string> cacheOrder=new Queue<string>();
        readonly Dictionary<string,string> history=new Dictionary<string,string>();
        volatile int epoch;int lifecycle;bool busy,ownsMic,oldVoice;int oldConcurrent,oldResponses,sequence,nextInput;
        float oldMin,oldMax,oldHuman;float lastHuman=-100,lastReply=-100,frameMs;string activeId;
        public bool Requested {get;private set;}
        public bool Ready {get;private set;}
        public string Status {get;private set;}="On-device conversation off";
        public bool BudgetPaused=>Options.frameBudgetMs>0&&frameMs>Options.frameBudgetMs;
        public int GeneratedReplies {get;private set;}
        public int RecognizedTurns {get;private set;}
        public string LastHeard {get;private set;}="";
        public string LastReplyKind {get;private set;}="No speech yet";
        public int FallbackReplies {get;private set;}
        public float LastReplySeconds {get;private set;}
        public bool Busy=>busy||playing!=null;
        OnDeviceConversationSettings Options=>world.Settings.onDevice??(world.Settings.onDevice=new OnDeviceConversationSettings());
        public static void SetEditorLibraries(string path){OnDeviceModelPack.EditorLibraries=path;}
        public void Initialize(SocialWorld value){world=value;}
        public void SetEnabled(bool enabled,bool microphone=true)
        {
            if(enabled==Requested||!world||enabled&&!world.IsAuthority)return;
            if(enabled)
            {
                world.Speech.CancelAll();Requested=true;Ready=false;epoch++;lifecycle++;Status="Preparing on-device conversation";
                var c=world.Config;oldVoice=c.enableVoice;oldConcurrent=c.maxConcurrentSpeech;oldResponses=c.typicalResponses;oldMin=c.minCooldown;oldMax=c.maxCooldown;oldHuman=c.respondHuman;
                c.enableVoice=true;c.maxConcurrentSpeech=1;c.typicalResponses=1;c.minCooldown=.5f;c.maxCooldown=2;c.respondHuman=1;
                foreach(var state in world.Director.bots.Values){previousVoices[state.identity.id]=state.identity.voice;state.identity.voice=true;}
                ownsMic=microphone&&!world.MicrophoneInput.Capturing;
                loading=StartCoroutine(Load(lifecycle,microphone));
            }
            else
            {
                Requested=false;Ready=false;lifecycle++;CancelAll();loading=null;
                worker?.Dispose();var c=world.Config;c.enableVoice=oldVoice;c.maxConcurrentSpeech=oldConcurrent;c.typicalResponses=oldResponses;c.minCooldown=oldMin;c.maxCooldown=oldMax;c.respondHuman=oldHuman;
                foreach(var state in world.Director.bots.Values)if(previousVoices.TryGetValue(state.identity.id,out bool voice))state.identity.voice=voice;
                previousVoices.Clear();inputs.Clear();history.Clear();cache.Clear();cacheOrder.Clear();
                if(ownsMic)world.MicrophoneInput.EnableCapture(false);ownsMic=false;Status="On-device conversation off";
            }
        }
        IEnumerator Load(int version,bool microphone)
        {
            while(worker!=null&&!worker.Stopped){Status="Releasing previous conversation models";worker.Pump();yield return null;}
            worker=null;busy=false;activeId=null;string pack=null,error=null;
            yield return OnDeviceModelPack.Prepare(Options,s=>Status=s,(p,e)=>{pack=p;error=e;},()=>!Requested||lifecycle!=version);
            if(!Requested||version!=lifecycle)yield break;
            if(error!=null){Status=error;loading=null;yield break;}
            string libraries=OnDeviceModelPack.Libraries();
            if(string.IsNullOrEmpty(libraries)){Status="Native conversation libraries are missing from this build";loading=null;yield break;}
            Status="Loading shared speech and language models";
            worker=new OnDeviceNative(pack,libraries,Options,failure=>
            {
                if(!Requested||version!=lifecycle)return;
                if(failure!=null){Ready=false;busy=false;Status="On-device conversation unavailable: "+SocialText.Clean(failure,180);return;}
                Ready=true;Status="Ready · talk to the existing AI players";if(microphone)world.MicrophoneInput.EnableCapture(true);
            });
            loading=null;
        }
        public void Speak(SocialSpeech intent,SocialIdentity identity)
        {
            if(!Requested||!Ready||!world.IsAuthority||!world.Config.enableVoice){world.Director.AudioFinished(identity.id);return;}
            if(pending!=null||Busy){world.Director.AudioFinished(identity.id);return;}
            pending=new Reply{intent=intent,identity=identity,at=Time.unscaledTime};
        }
        public void Cancel(string id)
        {
            if(pending?.identity.id==id)pending=null;
            if(activeId==id)Interrupt();
        }
        public void CancelAll(){Interrupt();inputs.Clear();}
        void Interrupt()
        {
            epoch++;worker?.Cancel();pending=null;
            if(playing!=null){StopCoroutine(playing);playing=null;}
            if(activeId!=null)world?.Director?.AudioFinished(activeId);activeId=null;
            if(world)world.InterruptBotAudio();
        }
        public void HumanAudio(string id,SocialAudioPacket packet)
        {
            if(!Requested||!Ready||!world.IsAuthority)return;
            if(!inputs.TryGetValue(id,out var input)){if(inputs.Count>=16)return;inputs[id]=input=new Input{number=++nextInput};}
            if(packet.samples.Length>0)
            {
                if(Time.unscaledTime-lastHuman>.3f&&(busy||playing!=null||pending!=null))Interrupt();
                lastHuman=Time.unscaledTime;
                // Never splice two utterances if a final packet is waiting for the worker.
                if(input.final)return;
                if(input.samples.Count+packet.samples.Length<=96000)input.samples.AddRange(packet.samples);
            }
            if(packet.end)input.final=true;
        }
        public void ForgetHuman(string id)
        {
            if(!inputs.TryGetValue(id,out var input))return;inputs.Remove(id);
            if(worker!=null&&Ready)worker.Submit(native=>native.Forget(input.number));
        }
        public void Respond(SocialIdentity identity,string speaker,string text)
        {
            if(!Ready||!Requested||!world.Config.enableVoice)return;
            pending=new Reply{identity=identity,at=Time.unscaledTime,intent=new SocialSpeech{botId=identity.id,recipient=speaker,text="Give that block a second.",context="HumanSpeech: "+SocialText.Clean(text,220)}};
        }
        public void SubmitText(string text)
        {
            if(!Ready||!Requested||string.IsNullOrWhiteSpace(text))return;
            Interrupt();lastHuman=Time.unscaledTime;LastHeard="Text test: "+SocialText.Clean(text,220);world.HumanTranscript(world.LocalParticipantId,SocialText.Clean(text,220),Guid.NewGuid().ToString("N"),true);
        }
        void Update()
        {
            worker?.Pump();if(!world||!Requested)return;
            if(!world.IsAuthority){SetEnabled(false);return;}
            frameMs=Mathf.Lerp(frameMs,Mathf.Min(100,Time.unscaledDeltaTime*1000),.025f);
            if(!Ready)return;
            if(activeId!=null)world.Director.AudioPlaying(activeId,Time.unscaledTime+1);
            if(busy)return;
            foreach(var pair in inputs)
            {
                var input=pair.Value;if(input.samples.Count==0&&!input.final||Time.unscaledTime<input.next)continue;
                string id=pair.Key;int number=input.number,turn=input.turn;bool final=input.final;var samples=input.samples.ToArray();int version=lifecycle;
                if(!worker.Submit(native=>
                {
                    string transcript=native.Listen(number,samples,final);
                    native.Complete(()=>{busy=false;if(!Requested||version!=lifecycle)return;if(final&&!string.IsNullOrWhiteSpace(transcript)){RecognizedTurns++;LastHeard=SocialText.Clean(transcript,220);world.HumanTranscript(id,SocialText.Safe(transcript,world.Config.allowProfanity),turn.ToString(),true);}});
                }))break;
                busy=true;input.samples.Clear();input.final=false;input.next=Time.unscaledTime+.25f;if(final)input.turn++;return;
            }
            if(pending==null)return;
            if(Time.unscaledTime-pending.at>8){world.Director.AudioFinished(pending.identity.id);pending=null;return;}
            if(playing!=null||Time.unscaledTime-lastHuman<Options.quietBeforeReply||Time.unscaledTime-lastReply<Options.minimumReplyGap)return;
            bool generate=pending.intent.context!=null&&pending.intent.context.StartsWith("HumanSpeech:",StringComparison.Ordinal);
            if(BudgetPaused){Status="Conversation waiting for frame-time budget · "+frameMs.ToString("F0")+" ms";return;}
            StartReply(pending,generate);pending=null;
        }
        void StartReply(Reply reply,bool generate)
        {
            int version=epoch;var identity=reply.identity;string id=identity.id;activeId=id;
            string candidate=SocialText.Safe(reply.intent.text,world.Config.allowProfanity);
            string system="You are "+identity.name+", a player in Deadly Jenga, climbing candy blocks over an ocean with flying hands and disasters. Speak naturally to another player in one short sentence, under 24 words. No narration, role labels, reasoning, or invented abilities. Answer their actual question. You cannot control the game by speaking. Your traits: "+string.Join(", ",identity.traitIds.Zip(identity.traits,(name,value)=>name+"="+(int)value));
            string memories=world.Director.bots.TryGetValue(id,out var bot)?string.Join("; ",bot.Recall(reply.intent.recipient,Time.unscaledTime,2).Select(m=>m.summary)):"";
            string message="Recent game events: "+SocialText.Clean(memories,160)+"\nRecent conversation: "+(history.TryGetValue(id,out var recent)?recent:"")+"\nPlayer now says: "+SocialText.Clean((reply.intent.context??"").Replace("HumanSpeech:",""),220);
            string cacheKey=identity.vocal.speaker+":"+candidate;cache.TryGetValue(cacheKey,out var cached);float speed=(float)identity.vocal.rate;
            int voice=(identity.id.GetHashCode()&int.MaxValue)%8;int tokens=Mathf.Clamp(Options.replyTokens,12,64),timeout=Mathf.Clamp(Options.generationTimeoutMs,1000,15000);
            float began=Time.realtimeSinceStartup;Status=generate?"Thinking · "+identity.name:"Speaking · "+identity.name;
            if(!worker.Submit(native=>
            {
                if(version!=epoch){native.Complete(()=>busy=false);return;}
                string text=generate?native.Generate(system,message,tokens,timeout):candidate;
                if(version!=epoch){native.Complete(()=>busy=false);return;}
                bool fallback=generate&&string.IsNullOrWhiteSpace(text);
                if(string.IsNullOrWhiteSpace(text))text="Give me a second. I'm watching these blocks.";
                text=SocialText.Safe(text).Trim('"',' ','\n');text=string.Join(" ",text.Split(new[]{' ','\n','\r'},StringSplitOptions.RemoveEmptyEntries).Take(28));
                float[] audio=!generate&&cached!=null?cached:native.Synthesize(text,voice,speed);
                native.Complete(()=>
                {
                    busy=false;if(!Requested||version!=epoch||audio==null||!world.Director.bots.TryGetValue(id,out var state)||state.muted)return;
                    LastReplySeconds=Time.realtimeSinceStartup-began;LastReplyKind=generate?(fallback?"Fallback - model timed out or returned no text":"Generated response"):"Scripted game reaction";if(generate){if(fallback)FallbackReplies++;else GeneratedReplies++;history[id]=SocialText.Clean(message.Substring(message.LastIndexOf("Player now says:",StringComparison.Ordinal))+" Reply: "+text,350);}
                    else if(!cache.ContainsKey(cacheKey)){cache[cacheKey]=audio;cacheOrder.Enqueue(cacheKey);while(cacheOrder.Count>Mathf.Clamp(Options.cachedVoiceClips,4,32))cache.Remove(cacheOrder.Dequeue());}
                    world.SpokenText(id,text);playing=StartCoroutine(Play(id,audio,version));
                });
            })){activeId=null;return;}
            busy=true;
        }
        IEnumerator Play(string id,float[] audio,int version)
        {
            lastReply=Time.unscaledTime;Status="Speaking · "+world.Director.bots[id].identity.name;
            for(int at=0;at<audio.Length;at+=800)
            {
                if(!Requested||version!=epoch||!world.Config.enableVoice||!world.Director.bots.TryGetValue(id,out var state)||state.muted)break;
                int count=Math.Min(800,audio.Length-at);var frame=new float[count];Array.Copy(audio,at,frame,0,count);
                world.SendAudio(new SocialAudioPacket{speaker=id,sequence=++sequence,samples=frame},false);yield return new WaitForSecondsRealtime(.05f);
            }
            world.SendAudio(new SocialAudioPacket{speaker=id,sequence=++sequence,samples=Array.Empty<float>(),end=true},false);
            world.Director.AudioFinished(id);activeId=null;playing=null;Status="Ready · listening for players";
        }
        void OnDestroy(){if(world&&Requested)SetEnabled(false);worker?.Dispose();}
    }
}
