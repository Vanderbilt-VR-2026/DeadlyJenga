using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Fusion;
using Fusion.Sockets;
using Fusion.Photon.Realtime;
using UnityEngine;
using DeadlyJenga.Core;
using UnityEngine.SceneManagement;
using P=DeadlyJenga.CastlePrototype.CastleLanProtocol;

namespace DeadlyJenga.CastlePrototype
{
    // Owns one runner across lobby -> game. The sample bootstrap/spawner is not involved.
    public sealed class IslandRoomConnection : MonoBehaviour
    {
        const int Channel=0x49534C44, Ready=1, Pose=2, State=3, Ack=4, Spawn=5;
        sealed class Peer {public P.Actor actor;public bool ready;public int waiting,sentSelection,ackedSelection=-1;public float seen,sent;}
        readonly Dictionary<PlayerRef,Peer> peers=new Dictionary<PlayerRef,Peer>();
        public static IslandRoomConnection Instance {get;private set;}
        public NetworkRunner Runner {get;private set;}
        public IslandLobbyCatalog Catalog {get;set;}
        public string Region="us", Code="", Status="Choose Host or Join to share this island.";
        public int MapIndex,ModeIndex;
        public bool SharedDemoControls {get;private set;}
        public bool Migrating {get;private set;}
        public IslandDiscovery Discovery {get;private set;}
        public string DiscoveryStatus {get;private set;}="";
        float nextMigrationSnapshot;bool pushingSnapshot;int nearbySelection;
        public string SelectedNearby=>SelectedRoom()?.message.name??"No nearby rooms";
        IslandDiscovery.Entry SelectedRoom(){var list=Discovery?.Nearby;return list!=null&&list.Count>0?list[nearbySelection%list.Count]:null;}
        public void NextNearby()=>nearbySelection++;
        public void JoinNearby(){var entry=SelectedRoom();if(entry!=null&&!Connected&&!Busy)_=Join(entry.message.code);}
        public void InviteNearby(){if(IsHost&&!Busy)Discovery?.InviteAll(Code,HumanCount);}
        public void AcceptInvite(){var invite=Discovery?.Invitation;if(invite!=null&&!Connected&&!Busy){string code=invite.message.code;Discovery.Dismiss();_=Join(code);}}
        public void SetSharedControls(bool value){if(IsHost&&!LaunchPending&&Catalog.modes[ModeIndex].id=="demo"){SharedDemoControls=value;selectionVersion++;}}
        public void ReceiveSharedControls(bool value){if(!IsHost)SharedDemoControls=value;}
        public void StartDiscovery()
        {
            if(Discovery!=null||!Catalog)return;
            try{var app=PhotonAppSettings.Global.AppSettings;Discovery=new IslandDiscovery(app.AppIdFusion,(app.AppVersion??"")+"/"+Catalog.NetworkVersion);AcquireWifi();}
            catch(Exception e){DiscoveryStatus="Wi-Fi discovery unavailable: "+e.Message+". Enter a code instead.";}
        }
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject wifiLock;
#endif
        void AcquireWifi()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using(var activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
            using(var context=activity.Call<AndroidJavaObject>("getApplicationContext"))
            using(var wifi=context.Call<AndroidJavaObject>("getSystemService","wifi"))
            {wifiLock=wifi.Call<AndroidJavaObject>("createMulticastLock","DeadlyJengaFusionDiscovery");wifiLock.Call("setReferenceCounted",false);wifiLock.Call("acquire");}
#endif
        }
        public bool Busy {get;private set;}
        public bool LaunchPending {get;private set;}
        int selectionVersion;float launchDeadline;
        public bool HostSelected {get;private set;}
        public bool Connected=>Runner&&Runner.IsRunning;
        public bool IsHost=>Connected&&Runner.IsServer;
        public int HumanCount=>Connected?Runner.ActivePlayers.Count():1;
        public bool CanConfigure=>HostSelected&&!Busy&&!LaunchPending&&(!Connected||IsHost);
        public event Action<string,byte[]> SocialReceived;
        public event Action<string> ParticipantLeft;
        readonly Dictionary<PlayerRef,CastleSocialChannel> socialPeers=new Dictionary<PlayerRef,CastleSocialChannel>();
        readonly CastleSocialChannel socialServer=new CastleSocialChannel();
        const int SocialChannel=0x49534C56;
        public bool CanLaunch=>IsHost&&!Busy&&!LaunchPending&&Catalog&&ModeIndex>=0&&ModeIndex<Catalog.modes.Length&&MapIndex>=0&&MapIndex<Catalog.maps.Length&&Catalog.modes[ModeIndex].available;
        NetworkDelegates callbacks;float nextSend,nextHello,lastServer;int serial,waitingPose,generation;bool leaving,transitioning,welcomed;
        NetworkRunner ownedRunner;
        CancellationTokenSource connecting;
        public static IslandRoomConnection Ensure()
        {if(!Instance)new GameObject("Island room connection").AddComponent<IslandRoomConnection>();return Instance;}
        void Awake(){if(Instance&&Instance!=this){Destroy(gameObject);return;}Instance=this;DontDestroyOnLoad(gameObject);}
        public void SelectMap(int index){if(CanConfigure&&Catalog&&index>=0&&index<Catalog.maps.Length){MapIndex=index;PublishSelection();}}
        public void SelectMode(int index){if(CanConfigure&&Catalog&&index>=0&&index<Catalog.modes.Length){ModeIndex=index;PublishSelection();}}
        void PublishSelection()
        {if(IsHost){selectionVersion++;Runner.SessionInfo.UpdateCustomProperties(new Dictionary<string,SessionProperty>{{"map",MapIndex},{"mode",ModeIndex}});}}
        public Task Host()=>Connect(true,IslandRoomAddress.Create(Region));
        public Task Join(string code)=>Connect(false,code);
        async Task Connect(bool host,string input)
        {
            if(Busy||Connected)return;
            if(!Catalog||!IslandRoomAddress.TryParse(input,Region,out var region,out var code)){Status="Enter a full code, for example US-ABC234.";return;}
            Busy=true;HostSelected=host;Region=region;Code=code;Status=host?"Creating island room…":"Joining island room…";int attempt=++generation;
            var go=CreateRunner();
            AttachCallbacks();
            try
            {
                connecting=new CancellationTokenSource(TimeSpan.FromSeconds(30));var cancellation=connecting;
                var app=PhotonAppSettings.Global.AppSettings.GetCopy();app.FixedRegion=Region;app.AppVersion=(app.AppVersion??"")+"/"+Catalog.NetworkVersion;
                var result=await Runner.StartGame(new StartGameArgs{GameMode=host?GameMode.Host:GameMode.Client,SessionName=Code,PlayerCount=10,
                    EnableClientSessionCreation=false,IsVisible=false,CustomPhotonAppSettings=app,StartGameCancellationToken=cancellation.Token,
                    Config=MigrationConfig(),Scene=new NetworkSceneInfo(),SceneManager=go.AddComponent<NetworkSceneManagerDefault>(),SessionProperties=host?new Dictionary<string,SessionProperty>{{"map",MapIndex},{"mode",ModeIndex}}:null});
                if(attempt!=generation)return;
                if(!result.Ok){Status="Connection failed: "+result.ShutdownReason+". Check code, region and Internet, then retry.";HostSelected=false;if(Runner)Destroy(Runner.gameObject);Runner=null;}
                else{Status="Room "+Code+" · "+(host?"you are host":"connected");lastServer=Time.unscaledTime;PublishSelection();nextMigrationSnapshot=0;if(host)await PushMigration();}
            }
            catch(Exception e){if(attempt!=generation)return;Status="Connection failed: "+e.Message;HostSelected=false;if(Runner)Destroy(Runner.gameObject);Runner=null;}
            finally{if(attempt==generation){Busy=false;connecting?.Dispose();connecting=null;}}
        }
        GameObject CreateRunner()
        {var go=new GameObject("Island Fusion Runner");DontDestroyOnLoad(go);Runner=go.AddComponent<NetworkRunner>();ownedRunner=Runner;Runner.ProvideInput=false;return go;}
        public static NetworkProjectConfig MigrationConfig()
        {var config=NetworkProjectConfig.Deserialize(NetworkProjectConfig.Serialize(NetworkProjectConfig.Global));config.HostMigration.EnableAutoUpdate=false;config.HostMigration.UpdateDelay=2;return config;}
        void AttachCallbacks()
        {
            callbacks=new NetworkDelegates();callbacks.OnReliableDataReceived=Receive;
            callbacks.OnPlayerJoined=(_,player)=>{nextMigrationSnapshot=0;};
            callbacks.OnPlayerLeft=(_,player)=>{peers.Remove(player);socialPeers.Remove(player);IslandLobby.Current?.RemovePeer(player.ToString());ParticipantLeft?.Invoke(player.ToString());};
            callbacks.OnShutdown=(ended,reason)=>{if(Runner==ended&&!Migrating&&reason!=ShutdownReason.HostMigration)Disconnected(reason.ToString());};Runner.AddCallbacks(callbacks);
            callbacks.OnSceneLoadStart=_=>{transitioning=true;MatchSetup.FromLobby=true;MatchSetup.StartLocalSession=false;MatchSetup.MapId=Catalog.maps[MapIndex].id;MatchSetup.ModeId=Catalog.modes[ModeIndex].id;};
            callbacks.OnSceneLoadDone=_=>{if(SceneManager.GetActiveScene().name=="Lobby")transitioning=false;};
            callbacks.OnHostMigration=(old,token)=>{if(!leaving&&old==Runner)_=Migrate(old,token);};
        }
        async Task Migrate(NetworkRunner old,HostMigrationToken token)
        {
            if(Migrating||leaving)return;
            Migrating=Busy=true;LaunchPending=false;Status="Host left · transferring the island…";
            var game=FindFirstObjectByType<CastleFusionSession>();game?.PauseForMigration();
            IslandLobby.Current?.Voice?.ResetLobbyAudio();
            try
            {
                old.RemoveCallbacks(callbacks);await old.Shutdown(shutdownReason:ShutdownReason.HostMigration);if(old)Destroy(old.gameObject);
                peers.Clear();socialPeers.Clear();socialServer.Clear();waitingPose=0;nextHello=0;welcomed=false;transitioning=false;
                var go=CreateRunner();AttachCallbacks();
                var app=PhotonAppSettings.Global.AppSettings.GetCopy();app.FixedRegion=Region;app.AppVersion=(app.AppVersion??"")+"/"+Catalog.NetworkVersion;
                NetworkSceneInfo scenes=default;
                if(game){int index=SceneUtility.GetBuildIndexByScenePath(Catalog.maps[MapIndex].scenePath);scenes.AddSceneRef(SceneRef.FromIndex(index),LoadSceneMode.Single);}
                var result=await Runner.StartGame(new StartGameArgs{GameMode=token.GameMode,HostMigrationToken=token,HostMigrationResume=_=>{},
                    CustomPhotonAppSettings=app,Config=MigrationConfig(),Scene=scenes,SceneManager=go.AddComponent<NetworkSceneManagerDefault>()});
                if(!result.Ok)throw new InvalidOperationException(result.ShutdownReason.ToString());
                HostSelected=IsHost;lastServer=Time.unscaledTime;nextSend=0;nextMigrationSnapshot=0;transitioning=false;
                if(game)game.ResumeAfterMigration(Runner);
                Status="Room "+Code+" · "+(IsHost?"you are the new host":"host reassigned");
            }
            catch(Exception e){Status="Host transfer failed: "+e.Message;Disconnected(Status);}
            finally{Migrating=Busy=false;}
        }
        void Send(PlayerRef player,int kind,byte[] data,int id=0)
        {var key=ReliableKey.FromInts(Channel,kind,id,0);if(IsHost)Runner.SendReliableDataToPlayer(player,key,data);else Runner.SendReliableDataToServer(key,data);}
        void SendVoice(PlayerRef player,int kind,int serial,byte[] bytes)
        {var key=ReliableKey.FromInts(SocialChannel,kind,serial,0);if(IsHost)Runner.SendReliableDataToPlayer(player,key,bytes);else Runner.SendReliableDataToServer(key,bytes);}
        public void SendSocial(byte[] data,string except=null)
        {
            if(!Connected||!IslandLobby.Current)return;
            if(IsHost){foreach(var p in peers)if(p.Value.ready&&p.Key.ToString()!=except){if(!socialPeers.TryGetValue(p.Key,out var flow))socialPeers[p.Key]=flow=new CastleSocialChannel();flow.Enqueue(data,Time.unscaledTime);}}
            else socialServer.Enqueue(data,Time.unscaledTime);
        }
        void Receive(NetworkRunner runner,PlayerRef source,ReliableKey key,ReadOnlySpan<byte> data)
        {
            key.GetInts(out int channel,out int kind,out int id,out _);
            if(channel==SocialChannel&&Connected&&IslandLobby.Current)
            {
                if(IsHost&&(!runner.ActivePlayers.Contains(source)||!peers.TryGetValue(source,out var p)||!p.ready))return;
                if(!socialPeers.TryGetValue(source,out var flow))socialPeers[source]=flow=new CastleSocialChannel();
                if(kind==2){(IsHost?flow:socialServer).Ack(id);return;}
                if(kind!=1||data.Length<1||data.Length>3500||data[0]!=5)return;
                SendVoice(source,2,id,Array.Empty<byte>());SocialReceived?.Invoke(IsHost?source.ToString():"host",data.ToArray());return;
            }
            if(channel!=Channel||!IslandLobby.Current||data.Length>32000)return;
            try
            {
                if(IsHost)
                {
                    if(!runner.ActivePlayers.Contains(source))return;
                    if(!peers.TryGetValue(source,out var peer))peers[source]=peer=new Peer{seen=Time.unscaledTime};
                    if(kind==Ready){if(!peer.ready)Send(source,Spawn,P.Encode(P.Player,w=>P.Write(w,new P.Pose(IslandLobby.SpawnPoint(source.RawEncoded),Quaternion.identity))));peer.ready=true;peer.seen=Time.unscaledTime;}
                    else if(kind==Ack&&peer.waiting==id){peer.waiting=0;peer.ackedSelection=peer.sentSelection;}
                    else if(kind==Pose&&peer.ready)
                    {
                        using var reader=P.Reader(data.ToArray(),P.Player);var actor=P.ReadActor(reader);
                        if(actor.kind!=NetworkKinds.Survivor||reader.BaseStream.Position!=reader.BaseStream.Length)throw new System.IO.InvalidDataException("Invalid lobby avatar");
                        actor.id=source.ToString();peer.actor=actor;peer.seen=Time.unscaledTime;IslandLobby.Current.ReceivePeer(actor);Send(source,Ack,Array.Empty<byte>(),id);
                    }
                }
                else if(kind==Ack&&waitingPose==id)waitingPose=0;
                else if(kind==Spawn)
                {using var r=P.Reader(data.ToArray(),P.Player);var pose=P.ReadPose(r);IslandLobby.Current.SpawnAt(pose);}
                else if(kind==State)
                {
                    using var reader=P.Reader(data.ToArray(),P.World);int map=reader.ReadInt32(),mode=reader.ReadInt32();
                    if(map<0||map>=Catalog.maps.Length||mode<0||mode>=Catalog.modes.Length)throw new System.IO.InvalidDataException("Unsupported lobby selection");
                    MapIndex=map;ModeIndex=mode;int count=P.Count(reader,10);var actors=new List<P.Actor>();
                    for(int i=0;i<count;i++){var actor=P.ReadActor(reader);if(actor.kind!=NetworkKinds.Survivor)throw new System.IO.InvalidDataException("Unexpected lobby hand");actors.Add(actor);}
                    SharedDemoControls=reader.ReadBoolean();if(reader.BaseStream.Position!=reader.BaseStream.Length)throw new System.IO.InvalidDataException("Trailing lobby state");
                    IslandLobby.Current.ReceivePeers(actors,Runner.LocalPlayer.ToString());welcomed=true;lastServer=Time.unscaledTime;Send(default,Ack,Array.Empty<byte>(),id);
                }
            }
            catch(Exception e){Status="Rejected lobby message: "+e.Message;if(IsHost)runner.Disconnect(source);else _=Leave();}
        }
        void Update()
        {
            if(Catalog&&Discovery==null&&DiscoveryStatus.Length==0)StartDiscovery();
            try{Discovery?.Tick(Time.unscaledTime,Code,HumanCount,IsHost&&!Busy);}catch(Exception e){DiscoveryStatus="Wi-Fi discovery: "+e.Message;Discovery?.Dispose();Discovery=null;}
            if(Connected&&Discovery?.Invitation!=null&&Discovery.Invitation.message.code==Code)Discovery.Dismiss();
            if(IsHost&&!Busy&&!pushingSnapshot&&Time.unscaledTime>=nextMigrationSnapshot){nextMigrationSnapshot=Time.unscaledTime+2;_=PushMigration();}
            if(Connected&&IslandLobby.Current&&!Busy&&!transitioning)CheckLaunch();
            if(!Connected||!IslandLobby.Current||Busy||transitioning)return;
            float now=Time.unscaledTime;
            if(IsHost){foreach(var p in socialPeers)p.Value.Pump(now,(id,bytes)=>SendVoice(p.Key,1,id,bytes));}
            else socialServer.Pump(now,(id,bytes)=>SendVoice(default,1,id,bytes));
            if(IsHost)
            {
                foreach(var pair in peers.ToArray())if(now-pair.Value.seen>20||pair.Value.waiting!=0&&now-pair.Value.sent>20){Runner.Disconnect(pair.Key);peers.Remove(pair.Key);IslandLobby.Current.RemovePeer(pair.Key.ToString());}
                if(now<nextSend)return;nextSend=now+.1f;
                var actors=new List<P.Actor>{IslandLobby.Current.Capture(Runner.LocalPlayer.ToString())};actors.AddRange(peers.Values.Where(p=>p.actor!=null).Select(p=>p.actor));
                byte[] bytes=P.Encode(P.World,w=>{w.Write(MapIndex);w.Write(ModeIndex);w.Write(actors.Count);foreach(var actor in actors)P.Write(w,actor);w.Write(SharedDemoControls);});
                foreach(var pair in peers)if(pair.Value.ready&&pair.Value.waiting==0){pair.Value.waiting=++serial;pair.Value.sent=now;pair.Value.sentSelection=selectionVersion;Send(pair.Key,State,bytes,pair.Value.waiting);}
            }
            else if(now>=nextSend)
            {
                nextSend=now+.05f;
                if(!welcomed&&now>=nextHello){nextHello=now+.5f;Send(default,Ready,Array.Empty<byte>());}
                if(welcomed&&waitingPose==0){waitingPose=++serial;Send(default,Pose,P.Encode(P.Player,w=>P.Write(w,IslandLobby.Current.Capture(Runner.LocalPlayer.ToString()))),waitingPose);}
                if(now-lastServer>20){Status="Lobby connection lost.";_=Leave();}
            }
        }
        async Task PushMigration()
        {pushingSnapshot=true;try{if(Runner&&Runner.IsRunning)await Runner.PushHostMigrationSnapshot();}catch(Exception e){Debug.LogWarning("Host snapshot: "+e.Message);}finally{pushingSnapshot=false;}}
        public void Launch()
        {if(!CanLaunch)return;LaunchPending=true;launchDeadline=Time.unscaledTime+10;Status="Waiting for players to confirm the selected mode and map…";CheckLaunch();}
        public void CancelLaunch(){if(IsHost){LaunchPending=false;Status="Start cancelled · choose mode and map.";}}
        void CheckLaunch()
        {
            if(!LaunchPending||!IsHost)return;
            bool ready=Runner.ActivePlayers.Where(p=>p!=Runner.LocalPlayer).All(p=>peers.TryGetValue(p,out var peer)&&peer.ready&&peer.ackedSelection==selectionVersion);
            if(!ready){if(Time.unscaledTime>launchDeadline){LaunchPending=false;Status="A player has not confirmed the lobby setup. Retry Start when everyone is ready.";}return;}
            LaunchPending=false;
            int index=SceneUtility.GetBuildIndexByScenePath(Catalog.maps[MapIndex].scenePath);
            if(index<0){Status="Selected map is not enabled in this build.";return;}
            transitioning=true;MatchSetup.FromLobby=true;MatchSetup.StartLocalSession=false;MatchSetup.ModeId=Catalog.modes[ModeIndex].id;MatchSetup.MapId=Catalog.maps[MapIndex].id;
            Status="Starting "+Catalog.modes[ModeIndex].title+"…";Runner.LoadScene(SceneRef.FromIndex(index),LoadSceneMode.Single);
        }
        public async Task Leave()
        {if(leaving)return;leaving=true;generation++;connecting?.Cancel();Busy=true;if(Runner){FusionMigrationCleanup.BeforeShutdown(Runner);await Runner.Shutdown();}else Disconnected("Left room");}
        void Disconnected(string reason)
        {
            if(!this)return;bool inGame=SceneManager.GetActiveScene().name!="Lobby";Status="Disconnected: "+reason;
            connecting?.Dispose();connecting=null;
            if(IslandLobby.Current&&IslandLobby.Current.Voice)IslandLobby.Current.Voice.ResetLobbyAudio();
            if(Runner){Runner.RemoveCallbacks(callbacks);Destroy(Runner.gameObject);}Runner=null;peers.Clear();socialPeers.Clear();socialServer.Clear();waitingPose=0;nextHello=0;welcomed=false;HostSelected=false;SharedDemoControls=false;LaunchPending=false;Busy=false;leaving=false;transitioning=false;
            if(inGame)SceneManager.LoadScene("Lobby");else IslandLobby.Current?.ClearPeers();
        }
        public bool Owns(NetworkRunner runner)=>ownedRunner==runner;
        void OnApplicationQuit(){FusionMigrationCleanup.BeforeShutdown(Runner);}
        void OnDestroy(){Discovery?.Dispose();
#if UNITY_ANDROID && !UNITY_EDITOR
            if(wifiLock!=null){wifiLock.Call("release");wifiLock.Dispose();}
#endif
            if(Instance==this)Instance=null;}
    }
}
