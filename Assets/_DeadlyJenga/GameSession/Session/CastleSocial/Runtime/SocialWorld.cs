using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DeadlyJenga.CastleBots;
using DeadlyJenga.CastlePrototype;

namespace DeadlyJenga.CastleSocial
{
    [Serializable] public sealed class SocialNotice
    {public string kind,id,actor,name,text,topic;public SocialIdentity identity;public bool speaking;public double at;}
    [DefaultExecutionOrder(300)]
    public sealed class SocialWorld : MonoBehaviour
    {
        public CastlePlaySession Session {get;private set;}
        public IslandLobby Lobby {get;private set;}
        IslandRoomConnection lobbyRoom;
        public SocialSettings Settings {get;private set;}
        public SocialConfiguration Config=>Settings.configuration;
        public SocialDirector Director {get;private set;}
        public bool IsAuthority=>Lobby?Lobby.Room.IsHost:Session&&(!Session.Network||!Session.Network.IsClient);
        readonly string standaloneId="local:"+Guid.NewGuid().ToString("N");
        public string LocalParticipantId=>Lobby?(Lobby.Room.Connected?Lobby.Room.Runner.LocalPlayer.ToString():standaloneId):Session&&Session.Network?Session.Network.LocalParticipantId:standaloneId;
        public readonly List<SocialBotAgent> Agents=new List<SocialBotAgent>();
        public readonly Dictionary<string,SocialNotice> Participants=new Dictionary<string,SocialNotice>();
        public string Caption {get;private set;}="Social simulation ready";
        public string LastError {get;private set;}="";
        public SocialSpeechClient Speech {get;private set;}
        public SocialVoicePlayback Playback {get;private set;}
        public SocialMicrophone MicrophoneInput {get;private set;}
        List<SocialIdentity> pool;SocialConfiguration lastValid;int nextIdentity,eventSerial,metadataIndex;float nextScan,nextTick,nextMetadata,nextThreat;bool lastAuthority=true;
        readonly Dictionary<string,int> sequences=new Dictionary<string,int>();readonly Dictionary<string,float> lastVoice=new Dictionary<string,float>();
        readonly Dictionary<string,(float start,int count)> rates=new Dictionary<string,(float,int)>();
        public void Initialize(CastlePlaySession session)
        {
            Session=session;var authored=Resources.Load<SocialSettings>("CastleSocial/Default");Settings=authored?Instantiate(authored):ScriptableObject.CreateInstance<SocialSettings>();
            Playback=gameObject.AddComponent<SocialVoicePlayback>();Playback.Initialize(this);
            Speech=gameObject.AddComponent<SocialSpeechClient>();Speech.Initialize(this);
            MicrophoneInput=gameObject.AddComponent<SocialMicrophone>();MicrophoneInput.Initialize(this);
            if(session.Network){session.Network.SocialReceived+=Receive;session.Network.ParticipantLeft+=Left;}
            if(!ApplyConfiguration()){Settings.ResetDefaults();ApplyConfiguration();}
            gameObject.AddComponent<SocialDemonstrationRecorder>().Initialize(this);
        }
        public void Initialize(IslandLobby lobby)
        {
            Lobby=lobby;var authored=Resources.Load<SocialSettings>("CastleSocial/Default");Settings=authored?Instantiate(authored):ScriptableObject.CreateInstance<SocialSettings>();
            Settings.mode=SocialOperatingMode.Offline;Settings.microphoneEnabled=false;
            Playback=gameObject.AddComponent<SocialVoicePlayback>();Playback.Initialize(this);
            Speech=gameObject.AddComponent<SocialSpeechClient>();Speech.Initialize(this);
            MicrophoneInput=gameObject.AddComponent<SocialMicrophone>();MicrophoneInput.Initialize(this);
            Director=new SocialDirector(Config,new List<SocialIdentity>());
            lobbyRoom=lobby.Room;lobbyRoom.SocialReceived+=Receive;lobbyRoom.ParticipantLeft+=Left;
        }
        public void ResetLobbyAudio(){MicrophoneInput.StopCapture();Playback.Clear();sequences.Clear();rates.Clear();lastVoice.Clear();}
        void SendPacket(byte[] bytes,string except=null)
        {if(Lobby)Lobby.Room.SendSocial(bytes,except);else if(Session&&Session.Network)Session.Network.SendSocial(bytes,except);}
        public bool ApplyConfiguration()
        {
            try
            {
                Config.Validate();if(Config.maxPopulation<Agents.Count)throw new ArgumentException("Clear some survivors before lowering population capacity below the current live count.");var generated=SocialPopulation.Generate(Config,Config.maxPopulation);
                foreach(var agent in Agents.ToArray())if(agent)Speech.Cancel(agent.State.identity.id);
                pool=generated;Director=new SocialDirector(Config,pool);Director.Speak+=OnSpeech;Director.Perception=Perceive;nextIdentity=0;
                foreach(var agent in Agents)if(agent)Bind(agent,agent.Brain);
                lastValid=JsonUtility.FromJson<SocialConfiguration>(JsonUtility.ToJson(Config));LastError="";return true;
            }
            catch(Exception e){if(lastValid!=null){Settings.configuration=JsonUtility.FromJson<SocialConfiguration>(JsonUtility.ToJson(lastValid));ApplyConfiguration();}LastError=e.Message;Debug.LogWarning("Social configuration rejected: "+e.Message,this);return false;}
        }
        void Bind(SocialBotAgent agent,CastleBotBrain brain)
        {
            if(pool==null||nextIdentity>=pool.Count)return;var identity=pool[nextIdentity++];
            if(!Director.bots.TryGetValue(identity.id,out var state))Director.bots[identity.id]=state=new SocialState{identity=identity};
            agent.Initialize(this,brain,state);Publish(SocialEventKind.Greeting,identity.id,.6);
        }
        void Update()
        {
            if(!Session||Director==null)return;
            bool authority=IsAuthority;
            if(authority!=lastAuthority)
            {Speech.CancelAll();Playback.Clear();sequences.Clear();lastVoice.Clear();rates.Clear();Participants.Clear();lastAuthority=authority;if(authority)ApplyConfiguration();}
            if(!authority)return;
            if(Time.unscaledTime>=nextScan)
            {
                nextScan=Time.unscaledTime+.25f;Agents.RemoveAll(a=>!a);
                foreach(var brain in Session.Bots.Bots)
                {
                    if(!brain||brain.GetComponent<SocialBotAgent>()||Agents.Count>=Config.maxPopulation)continue;
                    // Recycle a free stable identity, never overwrite a live player's.
                    nextIdentity=pool.FindIndex(p=>!Agents.Any(a=>a&&a.State.identity.id==p.id));if(nextIdentity<0)break;
                    var agent=brain.gameObject.AddComponent<SocialBotAgent>();Bind(agent,brain);Agents.Add(agent);
                }
            }
            foreach(var id in lastVoice.Where(p=>Time.unscaledTime-p.Value>.35f).Select(p=>p.Key).ToArray()){Director.HumanActivity(id,false,Time.unscaledTime);lastVoice.Remove(id);}
            if(Time.unscaledTime>=nextTick)
            {nextTick=Time.unscaledTime+Config.tickSeconds;if(Config.enableBots)Director.Tick(Time.unscaledTime);}
            if(Time.unscaledTime>=nextThreat)
            {
                nextThreat=Time.unscaledTime+1;int moving=0;float strongest=0;
                foreach(var body in Session.generator.Bodies)if(body&&body.gameObject.activeInHierarchy){float speed=body.linearVelocity.magnitude;if(speed>3)moving++;strongest=Mathf.Max(strongest,speed);}
                if(moving>12)Publish(SocialEventKind.Collapse,"tower",Mathf.Clamp01(moving/50f));else if(moving>2&&strongest>3)Publish(SocialEventKind.Wobble,"tower",.55);
            }
            if(Time.unscaledTime>=nextMetadata&&Agents.Count>0)
            {
                nextMetadata=Time.unscaledTime+.15f;metadataIndex%=Agents.Count;var a=Agents[metadataIndex++];
                if(a){var notice=new SocialNotice{kind="identity",id=a.State.identity.id,actor=a.NetworkId,name=a.State.identity.name,identity=a.State.identity};Participants[notice.id]=notice;SendNotice(notice);}
            }
        }
        double Perceive(string bot,SocialEvent e)
        {
            var agent=Agents.FirstOrDefault(a=>a&&a.State.identity.id==bot);if(!agent)return 0;
            if(e.speaker=="tower")return Session.Bots.ThreatCost(agent.transform.position)>.2f?1:.4;
            if(TryPosition(e.speaker,out var target))
            {
                float distance=Vector3.Distance(target,agent.transform.position);if(distance>40)return .05;
                var origin=agent.transform.position+Vector3.up*.7f;bool blocked=Physics.Linecast(origin,target,CastleBotNavigation.Mask,QueryTriggerInteraction.Ignore);
                return Math.Max(.1,(blocked?.45:1)*(1-distance/50));
            }
            return e.human?.65:1;
        }
        public SocialBotAgent NearestFriend(SocialBotAgent self)=>Agents.Where(a=>a&&a!=self&&a.State.identity.group==self.State.identity.group&&!a.Brain.Respawning).OrderBy(a=>(a.transform.position-self.transform.position).sqrMagnitude).FirstOrDefault();
        public bool TryPosition(string id,out Vector3 point)
        {
            if(Lobby)return Lobby.TryPeerPosition(id,out point);
            var a=Agents.FirstOrDefault(x=>x&&x.State.identity.id==id);if(a){point=a.transform.position+Vector3.up*.7f;return true;}
            if(id==LocalParticipantId&&Session.Player){point=Session.Player.viewCamera.transform.position;return true;}
            if(Session.Network&&Session.Network.TryParticipantPosition(Participants.TryGetValue(id??"",out var n)?n.actor:id,out point))return true;point=default;return false;
        }
        public void Publish(SocialEventKind kind,string speaker,double salience=1,string text=null,string recipient=null)
        {if(!IsAuthority||Director==null)return;if(kind==SocialEventKind.Collapse||kind==SocialEventKind.Wobble||kind==SocialEventKind.NearMiss)salience*=.5+Config.chaos;Director.Publish(new SocialEvent{id="game:"+(++eventSerial),kind=kind,speaker=speaker,recipient=recipient,salience=salience,text=text,time=Time.unscaledTime,human=kind==SocialEventKind.HumanSpeech,topic=recipient??"castle"});}
        public void HumanTranscript(string speaker,string text,string utterance,bool final)
        {
            if(!IsAuthority)return;
            Director.Publish(new SocialEvent{id="human:"+speaker+":"+utterance,kind=SocialEventKind.HumanSpeech,human=true,speaker=speaker,text=text,time=Time.unscaledTime,salience=final?1:.45,topic="humans"});
        }
        void OnSpeech(SocialSpeech intent)
        {
            if(!Director.bots.TryGetValue(intent.botId,out var state))return;
            Caption=state.identity.name+" [AI]: "+intent.text;SendNotice(new SocialNotice{kind="caption",id=intent.botId,text=intent.text,name=state.identity.name,topic=intent.topic});
            Speech.Speak(intent,state.identity);
            var agent=Agents.FirstOrDefault(a=>a&&a.State==state);if(agent){var emote=agent.GetComponent<CastleBotEmotes>();if(emote)emote.TryPlay(state.emotion.fear>.6?CastleBotEmotes.Emote.NopeNopeNope:CastleBotEmotes.Emote.FriendlyWave);}
        }
        public void SpokenText(string id,string text)
        {
            if(!Director.bots.TryGetValue(id,out var state))return;text=SocialText.Safe(text,Config.allowProfanity);Caption=state.identity.name+" [AI]: "+text;SendNotice(new SocialNotice{kind="caption",id=id,name=state.identity.name,text=text});
        }
        public void SendAudio(SocialAudioPacket packet,bool human)
        {
            if(human){packet.speaker=LocalParticipantId;Activity(packet.speaker,packet);}
            var bytes=packet.Encode();SendPacket(bytes);
            if(!human)Playback.Accept(packet);
        }
        void Activity(string id,SocialAudioPacket p)
        {
            double rms=0;foreach(float x in p.samples)rms+=x*x;rms=Math.Sqrt(rms/Math.Max(1,p.samples.Length));
            if(rms>.009&&!p.end){lastVoice[id]=Time.unscaledTime;Director?.HumanActivity(id,true,Time.unscaledTime);}
            if(p.end){Director?.HumanActivity(id,false,Time.unscaledTime);lastVoice.Remove(id);}
            if(IsAuthority)Speech.HumanAudio(id,p);
        }
        void Receive(string source,byte[] bytes)
        {
            try
            {
                if(!Rate(source))return;
                if(bytes[0]==5)
                {
                    var p=SocialAudioPacket.Decode(bytes);if(IsAuthority)p.speaker=source;
                    if(sequences.TryGetValue(p.speaker,out int last)&&p.sequence<=last)return;sequences[p.speaker]=p.sequence;
                    // The host alone can originate bot speech; client-supplied IDs
                    // are replaced with the authenticated connection's human ID.
                    Playback.Accept(p);if(IsAuthority){Activity(source,p);SendPacket(p.Encode(),source);}
                }
                else if(bytes[0]==6&&!IsAuthority)
                {
                    var n=JsonUtility.FromJson<SocialNotice>(Encoding.UTF8.GetString(bytes,1,bytes.Length-1));if(n==null||string.IsNullOrEmpty(n.id)||n.id.Length>96)return;
                    if(n.kind=="identity"&&Participants.Count<100)Participants[n.id]=n;
                    if(n.kind=="caption")Caption=SocialText.Clean(n.name,32)+" [AI]: "+SocialText.Safe(n.text);
                    if(n.kind=="removed"){Participants.Remove(n.id);Playback.Remove(n.id);}
                }
            }
            catch(Exception e){LastError="Discarded social packet: "+e.Message;}
        }
        bool Rate(string source)
        {
            if(!rates.TryGetValue(source,out var rate)||Time.unscaledTime-rate.start>=1)rate=(Time.unscaledTime,0);
            rate.count++;rates[source]=rate;return rate.count<=(source=="host"?1024:80);
        }
        void SendNotice(SocialNotice notice)
        {byte[] json=Encoding.UTF8.GetBytes(JsonUtility.ToJson(notice));if(json.Length>15000)return;var packet=new byte[json.Length+1];packet[0]=6;Buffer.BlockCopy(json,0,packet,1,json.Length);SendPacket(packet);}
        void Left(string id){Director?.RemoveHuman(id);Speech.ForgetHuman(id);Playback.Remove(id);sequences.Remove(id);lastVoice.Remove(id);rates.Remove(id);}
        public void Detach(SocialBotAgent agent)
        {
            Agents.Remove(agent);if(agent.State==null)return;var id=agent.State.identity.id;Director?.Remove(id);Speech?.Cancel(id);Playback?.Remove(id);Participants.Remove(id);if(IsAuthority)SendNotice(new SocialNotice{kind="removed",id=id});
        }
        public void Mute(string id,bool muted){if(Director.bots.TryGetValue(id,out var b)){b.muted=muted;if(muted){Director.Cancel(id);Speech.Cancel(id);}}Playback.Mute(id,muted);}
        void OnDestroy()
        {if(Session&&Session.Network){Session.Network.SocialReceived-=Receive;Session.Network.ParticipantLeft-=Left;}if(lobbyRoom){lobbyRoom.SocialReceived-=Receive;lobbyRoom.ParticipantLeft-=Left;}if(Settings)Destroy(Settings);}
    }
}
