using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;
namespace DeadlyJenga.CastleSocial
{
    public sealed class OnDeviceConversation : MonoBehaviour
    {
        sealed class Input {public int number,turn;public readonly List<float> samples=new List<float>();public bool final;public float next;}
        sealed class Reply {public SocialSpeech intent;public SocialIdentity identity;public float at,notBefore;public string direct;}
        BundledBotVoiceBank fastBanterVoices;
        SocialWorld world;OnDeviceNative worker;Coroutine loading,playing;Reply pending;
        readonly Dictionary<string,Input> inputs=new Dictionary<string,Input>();
        readonly Dictionary<string,bool> previousVoices=new Dictionary<string,bool>();
        readonly Dictionary<(int voice,float speed,string text),float[]> cache=new();readonly Queue<(int voice,float speed,string text)> cacheOrder=new();
        readonly Dictionary<string,(string speaker,string context)> history=new();
        readonly System.Random replyBeats=new System.Random();
        readonly DialogueRecency recency=new DialogueRecency();
        readonly int[] banterTurns=new int[Enum.GetValues(typeof(SocialEventKind)).Length];
        volatile int epoch;int lifecycle;bool busy,ownsMic,oldVoice;int oldConcurrent,oldResponses,sequence,nextInput;
        float oldMin,oldMax,oldHuman;float lastHuman=-100,lastReply=-100,frameMs;string activeId;
        public bool Requested {get;private set;}
        public bool Ready {get;private set;}
        public string VoiceCast {get;private set;}="";
        public string Status {get;private set;}="On-device conversation off";
        public bool BudgetPaused=>Options.frameBudgetMs>0&&frameMs>Options.frameBudgetMs;
        public int GeneratedReplies {get;private set;}
        public int RecognizedTurns {get;private set;}
        public string LastHeard {get;private set;}="";
        public string LastReplyKind {get;private set;}="No speech yet";
        public int FallbackReplies {get;private set;}
        public int ComposedReplies {get;private set;}
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
                previousVoices.Clear();inputs.Clear();history.Clear();recency.Clear();cache.Clear();cacheOrder.Clear();fastBanterVoices=null;VoiceCast="";
                if(ownsMic)world.MicrophoneInput.EnableCapture(false);ownsMic=false;Status="On-device conversation off";
            }
        }
        IEnumerator Load(int version,bool microphone)
        {
            while(worker!=null&&!worker.Stopped){Status="Releasing previous conversation models";worker.Pump();yield return null;}
            worker=null;busy=false;activeId=null;string pack=null,cast=null,error=null;
            yield return OnDeviceModelPack.Prepare(Options,s=>Status=s,(p,c,e)=>{pack=p;cast=c;error=e;},()=>!Requested||lifecycle!=version);
            if(!Requested||version!=lifecycle)yield break;
            if(error!=null){FailLoad(error);yield break;}
            string libraries=OnDeviceModelPack.Libraries();
            if(string.IsNullOrEmpty(libraries)){FailLoad("Native conversation libraries are missing from this build");yield break;}
            Status="Loading shared speech and language models";
            worker=new OnDeviceNative(pack,libraries,Options,failure=>
            {
                if(!Requested||version!=lifecycle)return;
                if(failure!=null){busy=false;FailLoad("On-device conversation unavailable: "+SocialText.Clean(failure,180));return;}
                VoiceCast=cast;
                try
                {
                    fastBanterVoices=BundledBotVoiceBank.Load("FastBanterVoices");
                }
                catch(Exception exception) when(exception is IOException||exception is InvalidDataException)
                { fastBanterVoices=null; }
                Ready=true;Status="Ready · "+VoiceCast;if(microphone)world.MicrophoneInput.EnableCapture(true);
            });
            loading=null;
        }
        void FailLoad(string failure)
        {
            SetEnabled(false);
            Status=failure;
        }

        public void Speak(SocialSpeech intent,SocialIdentity identity)
        {
            if(!Requested||!Ready||!world.IsAuthority||!world.Config.enableVoice){world.Director.AudioFinished(identity.id);return;}
            if(pending!=null||Busy){world.Director.AudioFinished(identity.id);return;}
            pending=new Reply{intent=intent,identity=identity,at=Time.unscaledTime};
        }
        internal void QueueObserved(SocialIdentity identity, ObservedSpeech observation)
        {
            if (!Ready || !Requested || !world.IsAuthority || !world.Config.enableVoice || Busy) return;
            if (pending != null)
            {
                if (IsHumanTurn(pending.intent)) return;
                if (pending.intent.observation is ObservedSpeech queued &&
                    (observation.Kind != SocialEventKind.Collapse || queued.Kind == SocialEventKind.Collapse || queued.HeardText != null))
                    return;
                world.Director.AudioFinished(pending.identity.id);
            }
            pending = new Reply {
                identity = identity,
                at = Time.unscaledTime,
                notBefore = Time.unscaledTime + (observation.HeardText == null ? 0 : .16f + .32f*(float)replyBeats.NextDouble()),
                intent = new SocialSpeech { botId = identity.id, observation = observation }
            };
        }

        static bool IsHumanTurn(SocialSpeech intent) =>
            intent.context != null && intent.context.StartsWith("HumanSpeech:", StringComparison.Ordinal);

        public void Cancel(string id)
        {
            if(pending?.identity.id==id)pending=null;
            if(activeId==id)Interrupt();
        }
        public void CancelAll(){Interrupt();inputs.Clear();}
        internal void ForgetBot(string id){recency.Remove(id);history.Remove(id);}
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
            pending=new Reply{
                identity=identity,at=Time.unscaledTime,
                direct=MatchBanter.DirectPlayerReply(text,identity.name,world.RecentObservation(identity.id)),
                intent=new SocialSpeech{botId=identity.id,recipient=speaker,context="HumanSpeech: "+SocialText.Clean(text,220)}
            };
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
            if(Time.unscaledTime-pending.at>8&&pending.intent.observation==null){world.Director.AudioFinished(pending.identity.id);pending=null;return;}
            if(playing!=null||Time.unscaledTime<pending.notBefore||Time.unscaledTime-lastHuman<Options.quietBeforeReply||Time.unscaledTime-lastReply<Options.minimumReplyGap)return;
            if(pending.intent.observation is ObservedSpeech observation &&
                (Time.unscaledTime>observation.Expires||!world.CanDeliverObserved(pending.identity.id)))
            {
                world.Director.AudioFinished(pending.identity.id);
                pending=null;
                return;
            }
            bool generate=(IsHumanTurn(pending.intent)&&pending.direct==null)||
                (pending.intent.observation!=null&&Options.improviseGameReactions);
            if(BudgetPaused){Status="Conversation waiting for frame-time budget · "+frameMs.ToString("F0")+" ms";return;}
            StartReply(pending,generate);pending=null;
        }
        void StartReply(Reply reply,bool generate)
        {
            int version=epoch;var identity=reply.identity;string id=identity.id;activeId=id;
            var observation=reply.intent.observation;
            int round=world.RoundSpeechEpoch;
            bool composed=observation!=null&&!generate;
            var recent=recency.Snapshot(id,Time.unscaledTime,observation!=null);
            string candidate=SocialText.Safe(reply.direct??reply.intent.text,world.Config.allowProfanity);
            if(composed)
            {
                candidate=null;
                int kind=(int)observation.Kind;
                for(int attempt=0;attempt<12;attempt++)
                {
                    int variant=banterTurns[kind]++;
                    bool playful=MatchBanter.CanQuip(observation,variant,identity.Trait("playfulness"));
                    string option=MatchBanter.Compose(observation,variant,playful);
                    if(DialogueRecency.Repeats(option,recent,false))continue;
                    candidate=option;
                    break;
                }
            }
            if(!generate&&DialogueRecency.Repeats(candidate,recent,false))
            {
                activeId=null;world.Director.AudioFinished(id);return;
            }
            string system=null,message=null;
            if (generate) BuildPrompt(reply, observation, out system, out message);
            float speed=observation!=null?1:(float)identity.vocal.rate;
            int voice=BundledBotVoiceBank.VoiceFor(id);
            var cacheKey=(voice,speed,candidate);
            cache.TryGetValue(cacheKey,out var cached);
            int tokens=Mathf.Clamp(Options.replyTokens,12,64),timeout=Mathf.Clamp(Options.generationTimeoutMs,1000,15000);
            float began=Time.realtimeSinceStartup;Status=generate?"Thinking · "+identity.name:"Speaking · "+identity.name;
            if(observation!=null)Status+=" · "+observation.Kind;
            bool baked=false;
            void Complete(string text,float[] audio,bool fallback)
            {
                busy=false;
                if(!Requested||version!=epoch)return;
                if(audio==null||audio.Length==0||!world.Director.bots.TryGetValue(id,out var state)||state.muted
                    ||observation!=null&&(round!=world.RoundSpeechEpoch||Time.unscaledTime>observation.Expires||!world.CanDeliverObserved(id)))
                {activeId=null;world.Director.AudioFinished(id);Status="Ready · listening for players";return;}
                LastReplySeconds=Time.realtimeSinceStartup-began;
                LastReplyKind=observation!=null
                    ? (composed?(cached!=null?"Cached banter":baked?"Baked banter":"Contextual banter"):fallback?"Contextual fallback":"Generated observation") + " · " + observation.Kind
                    : generate?(fallback?"Fallback - model timed out or returned no text":"Generated response"):
                        reply.direct!=null?"Direct player reply":"Scripted game reaction";
                if(composed)ComposedReplies++;
                if(generate)
                {
                    if(fallback)FallbackReplies++;else GeneratedReplies++;
                    if(IsHumanTurn(reply.intent))history[id]=(reply.intent.recipient,SocialText.Clean(reply.intent.context+" Reply: "+text,350));
                }
                if(observation!=null)state.nextSpeech=Time.unscaledTime+Mathf.Max(4,world.Config.minCooldown);
                if(!generate&&!cache.ContainsKey(cacheKey))
                {
                    cache[cacheKey]=audio;
                    cacheOrder.Enqueue(cacheKey);
                    while(cacheOrder.Count>Mathf.Clamp(Options.cachedVoiceClips,4,32))cache.Remove(cacheOrder.Dequeue());
                }
                recency.Remember(id,text,Time.unscaledTime,observation!=null);
                world.SpokenText(id,text);
                playing=StartCoroutine(Play(id,audio,version,text,observation));
            }
            if(!generate&&cached!=null)
            {
                Complete(candidate,cached,false);
                return;
            }
            if(!generate&&fastBanterVoices!=null&&fastBanterVoices.TryRenderLine(id,candidate,out var prepared))
            {
                baked=true;
                Complete(candidate,prepared,false);
                return;
            }
            bool allowProfanity=world.Config.allowProfanity;
            string playerSpeech=IsHumanTurn(reply.intent)?reply.intent.context.Substring("HumanSpeech:".Length).Trim():null;
            if(!worker.Submit(native=>
            {
                if(version!=epoch){native.Complete(()=>busy=false);return;}
                string text=generate?native.Generate(system,message,tokens,timeout):candidate;
                if(version!=epoch){native.Complete(()=>busy=false);return;}
                bool fallback=generate&&string.IsNullOrWhiteSpace(text);
                if(string.IsNullOrWhiteSpace(text))text="Give me a sec.";
                if(generate)
                {
                    text=SocialText.Safe(text,allowProfanity).Trim('"',' ','\n');
                    text=string.Join(" ",text.Split(new[]{' ','\n','\r'},StringSplitOptions.RemoveEmptyEntries).Take(16));
                }
                if(observation!=null&&generate&&(fallback||!GroundedDialogue.Accepts(observation,text)))
                {
                    fallback=true;
                    text=MatchBanter.Compose(observation,0,false);
                }
                if(generate&&(!fallback&&playerSpeech!=null&&!MatchBanter.IsPlayerReply(text,playerSpeech)||
                    DialogueRecency.Repeats(text,recent,true)))
                {
                    native.Complete(()=>Complete(text,null,fallback));
                    return;
                }
                var audio=native.Synthesize(text,voice,speed);
                native.Complete(()=>Complete(text,audio,fallback));
            })){activeId=null;world.Director.AudioFinished(id);return;}
            busy=true;
        }

        void BuildPrompt(Reply reply,ObservedSpeech observation,out string system,out string message)
        {
            var identity=reply.identity;
            if (observation != null)
            {
                system = "You are playing this match on voice chat. Speak up to 12 words. Short fragments are fine. Keep danger direct. "
                    + "Use the exact event and location in your observation. Never invent a cause, player or action. "
                    + "If you only heard it, say so. If another player spoke, answer their actual words without repeating them.";
                message = "Your observation: " + observation.Fact;
                if (observation.HeardText != null)
                    message += "\n" + observation.HeardFrom + " said: \"" + observation.HeardText + "\"";
                if (!string.IsNullOrEmpty(world.PublicModeName)) message += "\nMatch mode: " + world.PublicModeName;
                if (observation.HeardOnly && observation.HeardText != null)
                    message += "\nAsk for the detail you missed. You did not see the event.";
                return;
            }

            system="Reply as "+identity.name+", a teammate in Deadly Jenga voice chat. Answer in a few casual words. "
                +"If unclear, ask. Use only the match facts given. No invented personal history.";
            var speech=reply.intent.context.Substring("HumanSpeech:".Length).Trim();
            message="Player: "+speech;
            if(history.TryGetValue(identity.id,out var recent)&&recent.speaker==reply.intent.recipient)
                message="Previous turn: "+recent.context+"\n"+message;
            var fact=world.RecentObservation(identity.id);
            if(!string.IsNullOrEmpty(fact))message="Observed: "+fact+"\n"+message;
            if(!string.IsNullOrEmpty(world.PublicModeName))message="Game: "+world.PublicModeName+". "+message;
        }

        IEnumerator Play(string id,float[] audio,int version,string text,ObservedSpeech observation)
        {
            lastReply=Time.unscaledTime;Status="Speaking · "+world.Director.bots[id].identity.name;
            const int sampleRate=16000;
            const int packetSamples=800;
            const float leadSeconds=.1f;
            float started=Time.unscaledTime;
            float ends=started+audio.Length/(float)sampleRate+leadSeconds;
            int offset=0;
            bool delivered=true;
            while(offset<audio.Length||Time.unscaledTime<ends)
            {
                if(!Requested||version!=epoch||!world.Config.enableVoice||!world.Director.bots.TryGetValue(id,out var state)||state.muted
                    ||observation!=null&&!world.CanDeliverObserved(id))
                {delivered=false;break;}
                int target=Mathf.Min(audio.Length,(int)((Time.unscaledTime-started+leadSeconds)*sampleRate));
                for(int packet=0;offset<target&&packet<3;packet++)
                {
                    int count=Math.Min(packetSamples,audio.Length-offset);
                    var frame=new float[count];Array.Copy(audio,offset,frame,0,count);
                    offset+=count;
                    world.SendAudio(new SocialAudioPacket{speaker=id,sequence=++sequence,samples=frame},false);
                }
                yield return null;
            }
            world.SendAudio(new SocialAudioPacket{speaker=id,sequence=++sequence,samples=Array.Empty<float>(),end=true},false);
            world.Director.AudioFinished(id);activeId=null;playing=null;Status="Ready · listening for players";
            if(delivered&&observation!=null)world.ReplyToObserved(id,text,observation);
        }
        void OnDestroy(){if(world&&Requested)SetEnabled(false);worker?.Dispose();}
    }
}
