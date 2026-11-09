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
    public sealed partial class IslandRoomConnection : MonoBehaviour
    {
        readonly long playerUniqueId=BitConverter.ToInt64(Guid.NewGuid().ToByteArray(),0)|1L;
        const int Channel=0x49534C44, Ready=1, Pose=2, State=3, Ack=4, Spawn=5;
        sealed class Peer {public P.Actor actor;public bool ready;public int waiting,sentSelection,ackedSelection=-1;public float seen,sent;}
        readonly Dictionary<PlayerRef,Peer> peers=new Dictionary<PlayerRef,Peer>();
        public static IslandRoomConnection Instance {get;private set;}
        public NetworkRunner Runner {get;private set;}
        public IslandLobbyCatalog Catalog {get;set;}
        public string Region="us", Code="", Status="Your island is ready. Start solo or invite friends.";
        public int MapIndex,ModeIndex;
        const int MapSwitchChannel = 0x4D415053;
        // The retry key carries the mode without changing the map ID payload older peers echo.
        const int MapOfferModeFlag = 1 << 30, MapOfferRetryBits = 8;
        readonly HashSet<PlayerRef> mapAcks = new HashSet<PlayerRef>();
        string pendingGameMap;
        int pendingGameMode = -1;
        int mapOffer, receivedMapOffer;
        SceneRef loadingRoomScene;
        public bool HasRoomScene=>Connected&&Runner.SceneInfo.SceneCount>0;
        internal bool RoomSceneBusy=>transitioning||Connected&&Runner.SceneManager.IsBusy;
        public bool SwitchingMap { get; private set; }
        public void SwitchGameMap(string id)
        {
            if (!IsHost || Busy || SwitchingMap || RoomSceneBusy || !Catalog) return;
            int index = Array.FindIndex(Catalog.maps, map => map.id == id);
            if (index < 0 || SceneUtility.GetBuildIndexByScenePath(Catalog.maps[index].scenePath) < 0) return;
            if(!Catalog.maps[index].SupportsMode(MatchSetup.ModeId))
            {Status="Choose a compatible mode in the lobby before changing to this map.";return;}
            int mode = Array.FindIndex(Catalog.modes, value => value.available && value.id == MatchSetup.ModeId);
            StartCoroutine(SwitchMap(index, mode >= 0 ? mode : ModeIndex));
        }
        System.Collections.IEnumerator SwitchMap(int index,int mode)
        {
            var runner=Runner;
            int offer=++mapOffer;
            string target=Catalog.maps[index].id;
            SwitchingMap=true;pendingGameMap=target;pendingGameMode=mode;mapAcks.Clear();
            Status="Changing map for everyone...";
            var packet=System.Text.Encoding.UTF8.GetBytes(target);
            int attempt=0;
            float deadline = Time.unscaledTime + 10, resend = 0;
            while (Connected && Runner==runner && IsHost && runner.ActivePlayers.Any(player => player != runner.LocalPlayer && !mapAcks.Contains(player)))
            {
                if(Time.unscaledTime>=deadline)
                {
                    foreach(var player in runner.ActivePlayers)
                        if(player!=runner.LocalPlayer)runner.SendReliableDataToPlayer(player,ReliableKey.FromInts(MapSwitchChannel,3,offer,0),packet);
                    Status="Map change cancelled: a player did not respond.";SwitchingMap=false;ClearPendingMap();yield break;
                }
                if (Time.unscaledTime >= resend)
                {
                    resend=Time.unscaledTime+1;
                    int retry=MapOfferModeFlag|(mode<<MapOfferRetryBits)|++attempt;
                    foreach(var player in runner.ActivePlayers)if(player!=runner.LocalPlayer&&!mapAcks.Contains(player))
                        runner.SendReliableDataToPlayer(player,ReliableKey.FromInts(MapSwitchChannel,1,offer,retry),packet);
                }
                yield return null;
            }
            if(Connected&&Runner==runner&&IsHost)
            {
                int previousMap=MapIndex,previousMode=ModeIndex;
                string activeMap=MatchSetup.MapId,activeMode=MatchSetup.ModeId;
                MapIndex=index;ModeIndex=mode;PublishSelection();
                if(LoadRoomScene(SceneUtility.GetBuildIndexByScenePath(Catalog.maps[index].scenePath),"map",()=>
                {
                    MapIndex=previousMap;ModeIndex=previousMode;
                    MatchSetup.MapId=activeMap;MatchSetup.ModeId=activeMode;
                    ClearPendingMap();PublishSelection();
                    if(!Connected||!IsHost)return;
                    foreach(var player in runner.ActivePlayers)
                        if(player!=runner.LocalPlayer)runner.SendReliableDataToPlayer(player,ReliableKey.FromInts(MapSwitchChannel,3,offer,0),packet);
                }))yield break;
            }
            SwitchingMap=false;ClearPendingMap();
        }
        void ClearPendingMap(){pendingGameMap=null;pendingGameMode=-1;}
        bool sharedDemoControls;
        public bool SharedDemoControls
        {
            get=>sharedDemoControls||RoomGameExperience.Current?.OpenDevelopment==true;
            private set=>sharedDemoControls=value;
        }
        public bool Migrating {get;private set;}
        public IslandDiscovery Discovery {get;private set;}
        public string DiscoveryStatus {get;private set;}="";
        public string DiscoveryNotice => DiscoveryStatus.Length > 0 ? DiscoveryStatus : Discovery?.Status ?? "";
        float nextMigrationSnapshot;bool pushingSnapshot;int nearbySelection;
        public string SelectedNearby=>SelectedRoom()?.message.name??"No nearby rooms";
        IslandDiscovery.Entry SelectedRoom(){var list=Discovery?.Nearby;return list!=null&&list.Count>0?list[nearbySelection%list.Count]:null;}
        public void NextNearby()=>nearbySelection++;
        public void JoinNearby(){var entry=SelectedRoom();if(entry!=null&&!Busy)_=Join(entry.message.code);}
        public async void InviteNearby()
        {
            if (Busy) return;
            ScanNearby();
            if (!Connected) await Host();
            if (Connected && !Busy) Discovery?.InviteAll(Code, HumanCount);
        }
        public void AcceptInvite()
        {
            if(Busy)return;
            if(Connected){Status="You are already in room "+Code+". Leave it deliberately before accepting another invitation.";return;}
            var invite=Discovery?.Invitation;if(invite==null)return;
            string code=invite.message.code;Discovery.Dismiss();_=Join(code);
        }
        public void SetSharedControls(bool value){if(CanConfigure)Act(new LobbyAction{verb="shared",index=value?1:0});}
        public void ReceiveSharedControls(bool value){if(!IsHost)SharedDemoControls=value;}
        public void ScanNearby()
        {
            if (Discovery == null) StartDiscovery();
            else
            {
                CheckDiscoveryPermission();
                Discovery.Scan();
            }
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused && Catalog) ScanNearby();
        }

        void CheckDiscoveryPermission()
        {
            try { AcquireWifi(); DiscoveryStatus = ""; }
            catch (Exception error) { DiscoveryStatus = "Wi-Fi receive permission: " + error.Message + ". Room codes still work."; }
        }
        public void StartDiscovery()
        {
            if(Discovery!=null||!Catalog)return;
            DiscoveryStatus="";
            try{var app=PhotonAppSettings.Global.AppSettings;Discovery=new IslandDiscovery(app.AppIdFusion,(app.AppVersion??"")+"/"+Catalog.NetworkVersion);}
            catch(Exception e){DiscoveryStatus="Wi-Fi discovery unavailable: "+e.Message+". Enter a code instead.";return;}
            CheckDiscoveryPermission();
            Discovery.Scan();
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
            {
                if(wifiLock!=null&&wifiLock.Call<bool>("isHeld"))return;
                wifiLock?.Dispose();
                wifiLock=wifi.Call<AndroidJavaObject>("createMulticastLock","DeadlyJengaFusionDiscovery");wifiLock.Call("setReferenceCounted",false);wifiLock.Call("acquire");
            }
#endif
        }
        public bool Busy {get;private set;}
        public bool LaunchPending {get;private set;}
        int selectionVersion,lastPublishedPopulation;float launchDeadline;
        public bool HostSelected {get;private set;}
        public bool Connected=>Runner&&Runner.IsRunning;
        public bool IsHost=>Connected&&Runner.IsServer;
        public int HumanCount=>Connected?Runner.ActivePlayers.Count():1;
        public bool CanConfigure=>!Busy&&!LaunchPending&&!Departing&&LocalIsLobbyHost;
        public event Action<string,byte[]> SocialReceived;
        public event Action<string> ParticipantLeft;
        readonly Dictionary<PlayerRef,CastleSocialChannel> socialPeers=new Dictionary<PlayerRef,CastleSocialChannel>();
        readonly CastleSocialChannel socialServer=new CastleSocialChannel();
        const int SocialChannel=0x49534C56;
        public bool CanLaunch=>LocalIsLobbyHost&&!Busy&&!LaunchPending&&!Departing&&Catalog&&ModeIndex>=0&&ModeIndex<Catalog.modes.Length&&MapIndex>=0&&MapIndex<Catalog.maps.Length&&Catalog.modes[ModeIndex].available&&Catalog.maps[MapIndex].SupportsMode(Catalog.modes[ModeIndex].id);
        NetworkDelegates callbacks;float nextSend,nextHello,lastServer;int serial,waitingPose,generation;bool leaving,transitioning,welcomed;
        bool marinaLocalSpawnPending;
        bool marinaFreshReadyPending,marinaDeferredTrusted;
        NetworkRunner marinaDeferredRunner,marinaAppliedRunner;
        int marinaDeferredGeneration,marinaDeferredFloor,marinaAppliedGeneration,marinaAppliedFloor;
        IslandLobby marinaDeferredLobby,marinaAppliedLobby;
        PlayerRef marinaDeferredSender;
        P.Pose marinaDeferredPose;
        byte[] marinaAppliedReservation;
        float nextMarinaCallback;
        NetworkRunner marinaRecoveryRunner;
        int marinaRecoveryOperation=int.MinValue;
        readonly HashSet<PlayerRef> marinaRecoverySpawns=new HashSet<PlayerRef>();
        void ResetMarinaCallbacks()
        {marinaLocalSpawnPending=marinaFreshReadyPending=marinaDeferredTrusted=false;marinaAppliedReservation=null;marinaAppliedRunner=marinaDeferredRunner=null;marinaAppliedLobby=marinaDeferredLobby=null;nextMarinaCallback=0;marinaRecoveryRunner=null;marinaRecoveryOperation=int.MinValue;marinaRecoverySpawns.Clear();}
        byte[] MarinaReservationIdentity(IslandLobby lobby,P.Pose pose)
        {
            var local=new P.Pose(lobby.transform.InverseTransformPoint(pose.position),Quaternion.Inverse(lobby.transform.rotation)*pose.rotation);
            return P.Encode(P.Player,writer=>P.Write(writer,local));
        }
        bool MarinaSpawnAlreadyApplied(NetworkRunner owner,IslandLobby lobby,byte[] reservation)
        {return ReferenceEquals(owner,marinaAppliedRunner)&&generation==marinaAppliedGeneration&&ReferenceEquals(lobby,marinaAppliedLobby)&&lobby.MarinaFloorRevision==marinaAppliedFloor&&marinaAppliedReservation!=null&&marinaAppliedReservation.SequenceEqual(reservation);}
        void ApplyMarinaSpawnOnce(NetworkRunner owner,P.Pose pose)
        {
            var lobby=IslandLobby.Current;
            if(!lobby||Busy||RoomSceneBusy||Departing||SwitchingMap||!CurrentOperation(generation,owner))return;
            var reservation=MarinaReservationIdentity(lobby,pose);
            if(MarinaSpawnAlreadyApplied(owner,lobby,reservation))return;
            lobby.SpawnAt(pose);marinaAppliedRunner=owner;marinaAppliedGeneration=generation;marinaAppliedLobby=lobby;marinaAppliedFloor=lobby.MarinaFloorRevision;marinaAppliedReservation=reservation;
        }
        bool SendReservedMarinaSpawn(NetworkRunner owner,int operation,PlayerRef participant,P.Pose pose)
        {
            if(!CurrentOperation(operation,owner)||!IsHost||!owner.ActivePlayers.Contains(participant)||!GameExperienceMemberId(owner,participant,out _))return false;
            try{Send(participant,Spawn,P.Encode(P.Player,w=>P.Write(w,pose)));marinaRecoverySpawns.Remove(participant);return true;}
            catch(Exception error)
            {if(CurrentOperation(operation,owner)){Status="Lobby spawn send failed: "+error.Message;owner.Disconnect(participant);}return false;}
        }
        void HandleMarinaReady(NetworkRunner owner,PlayerRef participant,Peer peer,LobbyParticipant preferred)
        {
            if(!CurrentOperation(generation,owner)||!IsHost||!owner.ActivePlayers.Contains(participant))return;
            peer.seen=Time.unscaledTime;
            if(Busy||Migrating||RoomSceneBusy||Departing||SwitchingMap||!GameExperienceMemberId(owner,participant,out _))
            {peer.ready=false;Status="Waiting for the authenticated lobby arrival boundary.";return;}
            var result=TryReserveMarinaSpawn(participant,out var pose,out var reason);
            if(result==MarinaSpawnResult.Wait){peer.ready=false;Status=reason;return;}
            if(result==MarinaSpawnResult.Rejected){peer.ready=false;Status="Lobby spawn rejected: "+reason;owner.Disconnect(participant);return;}
            AssignIdentity(participant.ToString(),preferred);
            peer.ready=SendReservedMarinaSpawn(owner,generation,participant,pose);
        }
        void HandleMarinaSpawn(NetworkRunner owner,PlayerRef sender,P.Pose pose)
        {
            if(!TrustedServerData(owner,sender,generation))return;
            var lobby=IslandLobby.Current;if(!lobby)return;
            if(RoomSceneBusy||Departing||SwitchingMap)
            {marinaDeferredTrusted=false;marinaFreshReadyPending=true;marinaLocalSpawnPending=true;return;}
            if(Busy)
            {marinaDeferredTrusted=true;marinaDeferredRunner=owner;marinaDeferredGeneration=generation;marinaDeferredLobby=lobby;marinaDeferredFloor=lobby.MarinaFloorRevision;marinaDeferredSender=sender;marinaDeferredPose=pose;marinaLocalSpawnPending=true;return;}
            if(MarinaSpawnAlreadyApplied(owner,lobby,MarinaReservationIdentity(lobby,pose))){marinaLocalSpawnPending=false;return;}
            var result=AcceptLocalMarinaSpawn(owner,pose,out var reason);
            if(result==MarinaSpawnResult.Rejected)throw new System.IO.InvalidDataException(reason);
            marinaLocalSpawnPending=result!=MarinaSpawnResult.Accepted;
            if(result==MarinaSpawnResult.Accepted)ApplyMarinaSpawnOnce(owner,pose);
            else{Status=reason;welcomed=false;nextHello=0;}
        }
        void TickMarinaCallbacks(float now)
        {
            if(!Connected||Busy||Migrating||leaving||RoomSceneBusy||Departing||SwitchingMap||!IslandLobby.Current||!Owns(Runner)||now<nextMarinaCallback)return;
            nextMarinaCallback=now+.25f;
            if(!IsHost)
            {
                if(marinaDeferredTrusted)
                {
                    marinaDeferredTrusted=false;
                    if(TrustedServerData(marinaDeferredRunner,marinaDeferredSender,marinaDeferredGeneration)&&ReferenceEquals(marinaDeferredLobby,IslandLobby.Current)&&marinaDeferredFloor==IslandLobby.Current.MarinaFloorRevision)
                        HandleMarinaSpawn(marinaDeferredRunner,marinaDeferredSender,marinaDeferredPose);
                    else marinaFreshReadyPending=true;
                }
                if(ConsumeMarinaSpawnRefreshRequest()||marinaFreshReadyPending){marinaFreshReadyPending=false;marinaLocalSpawnPending=true;welcomed=false;nextHello=0;}
                if(!marinaLocalSpawnPending)return;
                var result=TryGetLocalMarinaSpawn(out var pose,out var reason);
                if(result==MarinaSpawnResult.Accepted){ApplyMarinaSpawnOnce(Runner,pose);marinaLocalSpawnPending=false;}
                else if(result==MarinaSpawnResult.Rejected)throw new System.IO.InvalidDataException(reason);
                else{Status=reason;welcomed=false;}
                return;
            }
            if(!CurrentOperation(marinaRecoveryOperation,marinaRecoveryRunner))return;
            var roster=Runner.ActivePlayers.ToArray();
            if(roster.Length==0||!roster.Contains(Runner.LocalPlayer)||roster.Length>10||roster.Any(player=>!GameExperienceMemberId(Runner,player,out _)))return;
            if(MarinaHostRecoveryPending)
            {
                var result=TryCompleteMarinaHostRecovery(out bool completedNow,out var reason);
                if(result!=MarinaSpawnResult.Accepted){Status="Lobby recovery: "+reason;return;}
                if(completedNow)foreach(var player in roster)if(player!=Runner.LocalPlayer)marinaRecoverySpawns.Add(player);
            }
            foreach(var player in marinaRecoverySpawns.ToArray())
            {
                if(!Runner.ActivePlayers.Contains(player)){marinaRecoverySpawns.Remove(player);continue;}
                var result=TryGetReservedMarinaSpawn(player,out var pose,out var reason);
                if(result==MarinaSpawnResult.Accepted)SendReservedMarinaSpawn(marinaRecoveryRunner,marinaRecoveryOperation,player,pose);
                else if(result==MarinaSpawnResult.Rejected){Status="Lobby recovery spawn rejected: "+reason;Runner.Disconnect(player);}
                else Status=reason;
            }
        }
        NetworkRunner ownedRunner;
        CancellationTokenSource connecting;
        public static IslandRoomConnection Ensure()
        {if(!Instance)new GameObject("Island room connection").AddComponent<IslandRoomConnection>();return Instance;}
        void Awake(){if(Instance&&Instance!=this){Destroy(gameObject);return;}Instance=this;DontDestroyOnLoad(gameObject);}
        public void SelectMap(int index){if(CanConfigure&&Catalog&&index>=0&&index<Catalog.maps.Length&&Catalog.maps[index].SupportsMode(Catalog.modes[ModeIndex].id))Act(new LobbyAction{verb="map",index=index});}
        public void SelectMode(int index){if(CanConfigure&&Catalog&&index>=0&&index<Catalog.modes.Length&&Catalog.modes[index].available&&Catalog.maps.Any(m=>m.SupportsMode(Catalog.modes[index].id)))Act(new LobbyAction{verb="mode",index=index});}
        void PublishSelection()
        {if(IsHost){selectionVersion++;lastPublishedPopulation=MatchSetup.BotTarget;Runner.SessionInfo.UpdateCustomProperties(new Dictionary<string,SessionProperty>{{"map",MapIndex},{"mode",ModeIndex},{"population",lastPublishedPopulation}});}}
        public Task Host()=>Connect(true,IslandRoomAddress.Create(Region));
        public async Task Join(string input)
        {
            if(Busy||!Catalog)return;
            if(!IslandRoomAddress.TryParse(input,Region,out var region,out var code)){Status="Check the room code before joining.";return;}
            if(Connected&&Region==region&&Code==code){Status="You are already on this island.";return;}
            if(Connected)
            {
                int attempt=generation;await Leave();
                if(generation!=unchecked(attempt+1)||leaving||Runner!=null)return;
            }
            await Connect(false,code);
        }
        async Task Connect(bool host,string input)
        {
            if(Busy||Connected)return;
            if(!Catalog||!IslandRoomAddress.TryParse(input,Region,out var region,out var code)){Status="Enter a full code, for example US-ABC234.";return;}
            Busy=true;HostSelected=host;Region=region;Code=code;Status=host?"Creating island room...":"Joining island room...";int attempt=++generation;
            var go=CreateRunner();
            AttachCallbacks();
            try
            {
                connecting=new CancellationTokenSource(TimeSpan.FromSeconds(30));var cancellation=connecting;
                var app=PhotonAppSettings.Global.AppSettings.GetCopy();app.FixedRegion=Region;app.AppVersion=(app.AppVersion??"")+"/"+Catalog.NetworkVersion+"/pose-reliable-recovery-1/server-data-c2s-1";
                var config=MigrationConfig();
                var result=await Runner.StartGame(new StartGameArgs{GameMode=host?GameMode.Host:GameMode.Client,SessionName=Code,PlayerCount=10,PlayerUniqueId=playerUniqueId,AuthValues=new Photon.Realtime.AuthenticationValues(playerUniqueId.ToString("X16",System.Globalization.CultureInfo.InvariantCulture)),
                    EnableClientSessionCreation=false,IsVisible=false,CustomPhotonAppSettings=app,StartGameCancellationToken=cancellation.Token,
                    Config=config,Scene=LobbyStartScene(config),SceneManager=go.AddComponent<NetworkSceneManagerDefault>(),SessionProperties=host?new Dictionary<string,SessionProperty>{{"map",MapIndex},{"mode",ModeIndex},{"population",MatchSetup.BotTarget}}:null});
                if(attempt!=generation)return;
                if(!result.Ok){Status="Connection failed: "+result.ShutdownReason+". Check code, region, Internet and matching game builds, then retry.";HostSelected=false;if(Runner)Destroy(Runner.gameObject);Runner=null;}
                else{Status="Room "+Code+" - "+(host?"you are host":"connected");lastServer=Time.unscaledTime;ConnectedIdentity(host);RoomGameExperience.Ensure(this)?.TransportAfterMigration(Runner);PublishSelection();nextMigrationSnapshot=0;if(host)await PushMigration();}
            }
            catch(Exception e){if(attempt!=generation)return;Status="Connection failed: "+e.Message;HostSelected=false;if(Runner)Destroy(Runner.gameObject);Runner=null;}
            finally{if(attempt==generation){Busy=false;connecting?.Dispose();connecting=null;}}
        }
        GameObject CreateRunner()
        {var go=new GameObject("Island Fusion Runner");DontDestroyOnLoad(go);Runner=go.AddComponent<NetworkRunner>();ownedRunner=Runner;Runner.ProvideInput=false;return go;}
        public static NetworkProjectConfig MigrationConfig()
        {
            var config=NetworkProjectConfig.Deserialize(NetworkProjectConfig.Serialize(NetworkProjectConfig.Global));
            config.Network.ReliableDataTransferModes=NetworkConfiguration.ReliableDataTransfers.ClientToServer;
            config.HostMigration.EnableAutoUpdate=false;config.HostMigration.UpdateDelay=2;return config;
        }
        internal int ConnectionGeneration=>generation;
        internal bool TrustedServerData(NetworkRunner packetRunner,PlayerRef source,int expectedGeneration)
        {
            return Connected&&!Migrating&&!IsHost&&packetRunner&&packetRunner.IsClient&&!packetRunner.IsServer&&
                CurrentOperation(expectedGeneration,packetRunner)&&packetRunner.Config!=null&&
                packetRunner.Config.Network.ReliableDataTransferModes==NetworkConfiguration.ReliableDataTransfers.ClientToServer&&
                !packetRunner.LocalPlayer.IsNone&&source==packetRunner.LocalPlayer;
        }
        bool CurrentOperation(int attempt,NetworkRunner runner)=>attempt==generation&&!leaving&&ReferenceEquals(Runner,runner)&&ReferenceEquals(ownedRunner,runner);
        void AttachCallbacks()
        {
            var attached=Runner;int attempt=generation;
            callbacks=new NetworkDelegates();callbacks.OnReliableDataReceived=(runner,source,key,data)=>{if(CurrentOperation(attempt,attached)&&ReferenceEquals(runner,attached))Receive(runner,source,key,data);};
            callbacks.OnPlayerJoined=(runner,player)=>{if(CurrentOperation(attempt,attached)&&ReferenceEquals(runner,attached))nextMigrationSnapshot=0;};
            callbacks.OnPlayerLeft=(runner,player)=>{if(!CurrentOperation(attempt,attached)||!ReferenceEquals(runner,attached))return;ReleaseMarinaSpawn(player);marinaRecoverySpawns.Remove(player);DepartIdentity(player.ToString());peers.Remove(player);socialPeers.Remove(player);IslandLobby.Current?.RemovePeer(player.ToString());ParticipantLeft?.Invoke(player.ToString());};
            callbacks.OnShutdown=(ended,reason)=>{if(Runner==ended&&!Migrating&&reason!=ShutdownReason.HostMigration)Disconnected(reason.ToString());};Runner.AddCallbacks(callbacks);
            callbacks.OnSceneLoadStart=runner=>
            {
                if(!CurrentOperation(attempt,attached)||!ReferenceEquals(runner,attached))return;
                var scenes=runner.SceneInfo;
                loadingRoomScene=scenes.SceneCount>0?scenes.Scenes[scenes.SceneCount-1]:SceneRef.None;
                transitioning=true;
                if(IsLobbyScene(loadingRoomScene)){ClearPendingMap();return;}
                IslandLobby.Persistent?.ParkForMatch();
                ApplySceneSelection();
                var experience=RoomGameExperience.Ensure(this);
                if(experience&&experience.Experience==GameExperience.Game&&!experience.ApplySelectedProfile())
                {experience.ReportSceneSelectionFailure();return;}
                MatchSetup.FromLobby=true;MatchSetup.StartLocalSession=false;
                MatchSetup.MapId=Catalog.maps[MapIndex].id;MatchSetup.ModeId=Catalog.modes[ModeIndex].id;
            };
            callbacks.OnSceneLoadDone=runner=>
            {
                if(!CurrentOperation(attempt,attached)||!ReferenceEquals(runner,attached))return;
                SwitchingMap=false;transitioning=false;
                if(IsLobbyScene(loadingRoomScene))
                {
                    var lobby=IslandLobby.Persistent;
                    if(lobby&&(lobby.MatchResident||!ReferenceEquals(Services.Session,lobby)))lobby.ResumeAfterMatch();
                    if(Connected)Status="Room "+Code+" - back in the lobby";
                }
                else if(Connected)Status="Room "+Code+" - "+Catalog.maps[MapIndex].title;
            };
            callbacks.OnHostMigration=(old,token)=>{if(CurrentOperation(attempt,attached)&&ReferenceEquals(old,attached))_=Migrate(old,token);};
        }
        void ApplySceneSelection()
        {
            if(!IsHost&&Runner.SessionInfo.Properties.TryGetValue("map",out var map))
            {int index=(int)map;if(index>=0&&index<Catalog.maps.Length)MapIndex=index;}
            if(!IsHost&&Runner.SessionInfo.Properties.TryGetValue("mode",out var mode))
            {int index=(int)mode;if(index>=0&&index<Catalog.modes.Length)ModeIndex=index;}
            if(string.IsNullOrEmpty(pendingGameMap))return;
            int offeredMap=Array.FindIndex(Catalog.maps,value=>value.id==pendingGameMap);
            if(offeredMap>=0&&loadingRoomScene.IsIndex&&
                SceneUtility.GetBuildIndexByScenePath(Catalog.maps[offeredMap].scenePath)==loadingRoomScene.AsIndex)
            {
                MapIndex=offeredMap;
                if(pendingGameMode>=0)ModeIndex=pendingGameMode;
            }
            ClearPendingMap();
        }
        async Task Migrate(NetworkRunner old,HostMigrationToken token)
        {
            if(Migrating||leaving||!ReferenceEquals(old,Runner)||!ReferenceEquals(old,ownedRunner))return;
            int attempt=++generation;NetworkRunner operationRunner=old;
            BeginMarinaHostRecovery();marinaRecoveryOperation=attempt;marinaRecoveryRunner=old;marinaRecoverySpawns.Clear();marinaLocalSpawnPending=true;nextMarinaCallback=0;
            CancelDeparture();ClearPendingMap();receivedMapOffer=0;SwitchingMap=false;Migrating=Busy=true;LaunchPending=false;Status="Host left - transferring the island...";
            RoomGameExperience.Ensure(this)?.TransportBeforeMigration(old);
            var game=FindFirstObjectByType<CastleFusionSession>();game?.PauseForMigration();
            IslandLobby.Current?.Voice?.ResetLobbyAudio();
            try
            {
                old.RemoveCallbacks(callbacks);await old.Shutdown(shutdownReason:ShutdownReason.HostMigration);
                if(!CurrentOperation(attempt,operationRunner)){if(old&&!ReferenceEquals(old,Runner))Destroy(old.gameObject);return;}
                if(old)Destroy(old.gameObject);
                peers.Clear();socialPeers.Clear();socialServer.Clear();waitingPose=0;nextHello=0;welcomed=false;transitioning=false;
                var go=CreateRunner();operationRunner=Runner;marinaRecoveryRunner=operationRunner;AttachCallbacks();
                var app=PhotonAppSettings.Global.AppSettings.GetCopy();app.FixedRegion=Region;app.AppVersion=(app.AppVersion??"")+"/"+Catalog.NetworkVersion+"/pose-reliable-recovery-1/server-data-c2s-1";
                NetworkSceneInfo scenes=default;
                if(game){int index=SceneUtility.GetBuildIndexByScenePath(Catalog.maps[MapIndex].scenePath);scenes.AddSceneRef(SceneRef.FromIndex(index),LoadSceneMode.Single);}
                var result=await operationRunner.StartGame(new StartGameArgs{GameMode=token.GameMode,HostMigrationToken=token,HostMigrationResume=_=>{},PlayerUniqueId=playerUniqueId,AuthValues=new Photon.Realtime.AuthenticationValues(playerUniqueId.ToString("X16",System.Globalization.CultureInfo.InvariantCulture)),
                    CustomPhotonAppSettings=app,Config=MigrationConfig(),Scene=scenes,SceneManager=go.AddComponent<NetworkSceneManagerDefault>()});
                if(!CurrentOperation(attempt,operationRunner))return;
                if(!result.Ok)throw new InvalidOperationException(result.ShutdownReason.ToString());
                HostSelected=IsHost;lastServer=Time.unscaledTime;nextSend=0;nextMigrationSnapshot=0;transitioning=false;
                if(IsHost){foreach(var id in identities.Keys.ToArray())if(!Runner.ActivePlayers.Any(p=>p.ToString()==id))DepartIdentity(id);if(!Runner.ActivePlayers.Any(p=>p.ToString()==leaderId))leaderId=LocalLobbyId;RefreshHostFlags();}
                RoomGameExperience.Ensure(this)?.TransportAfterMigration(Runner);
                if(game)game.ResumeAfterMigration(Runner);
                Status="Room "+Code+" - "+(IsHost?"you are the new host":"host reassigned");
            }
            catch(Exception e){if(CurrentOperation(attempt,operationRunner)){Status="Host transfer failed: "+e.Message;Disconnected(Status);}}
            finally{if(CurrentOperation(attempt,operationRunner))Migrating=Busy=false;}
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
            if(!Connected||!CurrentOperation(generation,runner))return;
            if(RoomGameExperience.Ensure(this)?.TransportReceive(runner,source,key,data)==true)return;
            key.GetInts(out int channel,out int kind,out int id,out int attempt);
            if(channel==DepartureChannel)
            {
                if(TrustedServerData(runner,source,generation)&&
                    (IslandLobby.Current||Services.Session is CastlePlaySession))ReceiveDeparturePacket(kind,id,attempt,data);
                return;
            }
            if (channel == MapSwitchChannel && runner == Runner && Connected && data.Length > 0 && data.Length <= 256)
            {
                string map = System.Text.Encoding.UTF8.GetString(data);
                if (IsHost)
                {if(kind==2&&id==mapOffer&&SwitchingMap&&map==pendingGameMap&&runner.ActivePlayers.Contains(source))mapAcks.Add(source);}
                else if(kind==3&&id>=receivedMapOffer){receivedMapOffer=id;ClearPendingMap();}
                else if(kind==1&&id>=receivedMapOffer&&(id>receivedMapOffer||pendingGameMap==map)&&Catalog&&
                    Array.Exists(Catalog.maps,value=>value.id==map&&SceneUtility.GetBuildIndexByScenePath(value.scenePath)>=0))
                {
                    int mode=(attempt&MapOfferModeFlag)==0?-1:(attempt&~MapOfferModeFlag)>>MapOfferRetryBits;
                    if(mode>=0&&(mode>=Catalog.modes.Length||!Catalog.modes[mode].available))return;
                    receivedMapOffer=id;pendingGameMap=map;pendingGameMode=mode;
                    runner.SendReliableDataToServer(ReliableKey.FromInts(MapSwitchChannel,2,id,attempt),data.ToArray());
                }
                return;
            }
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
                    if(kind==LobbyCommand&&peer.ready&&data.Length<=8192){using var command=P.Reader(data.ToArray(),P.Player);ApplyLobbyAction(source.ToString(),JsonUtility.FromJson<LobbyAction>(P.Text(command,8000)));}
                    else if(kind==Ready)
                    {
                        LobbyParticipant preferred=null;
                        if(data.Length>0){using var identity=P.Reader(data.ToArray(),P.Player);preferred=JsonUtility.FromJson<LobbyParticipant>(P.Text(identity,512));if(identity.BaseStream.Position!=identity.BaseStream.Length)throw new System.IO.InvalidDataException("Trailing lobby identity");}
                        HandleMarinaReady(runner,source,peer,preferred);
                    }
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
                {if(!TrustedServerData(runner,source,generation))return;using var r=P.Reader(data.ToArray(),P.Player);var pose=P.ReadPose(r);if(r.BaseStream.Position!=r.BaseStream.Length)throw new System.IO.InvalidDataException("Trailing lobby spawn");HandleMarinaSpawn(runner,source,pose);}
                else if(kind==State)
                {
                    using var reader=P.Reader(data.ToArray(),P.World);int map=reader.ReadInt32(),mode=reader.ReadInt32();
                    if(map<0||map>=Catalog.maps.Length||mode<0||mode>=Catalog.modes.Length)throw new System.IO.InvalidDataException("Unsupported lobby selection");
                    MapIndex=map;ModeIndex=mode;int count=P.Count(reader,10);var actors=new List<P.Actor>();
                    for(int i=0;i<count;i++){var actor=P.ReadActor(reader);if(actor.kind!=NetworkKinds.Survivor)throw new System.IO.InvalidDataException("Unexpected lobby hand");actors.Add(actor);}
                    SharedDemoControls=reader.ReadBoolean();ReadLobbyState(P.Text(reader,16000));if(reader.BaseStream.Position!=reader.BaseStream.Length)throw new System.IO.InvalidDataException("Trailing lobby state");
                    IslandLobby.Current.ReceivePeers(actors,Runner.LocalPlayer.ToString());welcomed=true;lastServer=Time.unscaledTime;Send(default,Ack,Array.Empty<byte>(),id);
                }
            }
            catch(Exception e){Status="Rejected lobby message: "+e.Message;if(IsHost)runner.Disconnect(source);else _=Leave();}
        }
        void Update()
        {
            try{TickMarinaCallbacks(Time.unscaledTime);}
            catch(Exception error){Status="Lobby arrival failed: "+error.Message;}
            if(IsHost&&!Busy)ReconcileLobbyRoster();
            if(IsHost&&CanConfigure&&lastPublishedPopulation!=MatchSetup.BotTarget)PublishSelection();
            if(Connected&&!IsHost&&Runner.SessionInfo.Properties.TryGetValue("population",out var population))MatchSetup.BotTarget=Mathf.Clamp((int)population,10,100);
            if(Catalog&&Discovery==null&&DiscoveryStatus.Length==0)StartDiscovery();
            try{Discovery?.Tick(Time.unscaledTime,Connected?Code:"",HumanCount,IsHost&&!Busy);}catch(Exception e){DiscoveryStatus="Wi-Fi discovery: "+e.Message;Discovery?.Dispose();Discovery=null;}
            if(Connected&&Discovery?.Invitation!=null&&Discovery.Invitation.message.code==Code)Discovery.Dismiss();
            if(IsHost&&!Busy&&!pushingSnapshot&&Time.unscaledTime>=nextMigrationSnapshot){nextMigrationSnapshot=Time.unscaledTime+2;_=PushMigration();}
            if(Connected&&IslandLobby.Current&&!Busy&&!transitioning)CheckLaunch();
            if(!Connected||!IslandLobby.Current||Busy||transitioning)return;
            float now=Time.unscaledTime;
            if(IsHost){foreach(var p in socialPeers)p.Value.Pump(now,(id,bytes)=>SendVoice(p.Key,1,id,bytes));}
            else socialServer.Pump(now,(id,bytes)=>SendVoice(default,1,id,bytes));
            if(IsHost)
            {
                foreach(var pair in peers.ToArray())if(now-pair.Value.seen>20||pair.Value.waiting!=0&&now-pair.Value.sent>20){Runner.Disconnect(pair.Key);ReleaseMarinaSpawn(pair.Key);marinaRecoverySpawns.Remove(pair.Key);peers.Remove(pair.Key);IslandLobby.Current.RemovePeer(pair.Key.ToString());}
                if(now<nextSend)return;nextSend=now+.1f;
                var actors=new List<P.Actor>{IslandLobby.Current.Capture(Runner.LocalPlayer.ToString())};actors.AddRange(peers.Values.Where(p=>p.actor!=null).Select(p=>p.actor));
                IslandLobby.Current.CaptureLobbyBots(actors);
                byte[] bytes=P.Encode(P.World,w=>{w.Write(MapIndex);w.Write(ModeIndex);w.Write(actors.Count);foreach(var actor in actors)P.Write(w,actor);w.Write(SharedDemoControls);w.Write(LobbyStateJson());});
                foreach(var pair in peers)if(pair.Value.ready&&pair.Value.waiting==0){pair.Value.waiting=++serial;pair.Value.sent=now;pair.Value.sentSelection=selectionVersion;Send(pair.Key,State,bytes,pair.Value.waiting);}
            }
            else if(now>=nextSend)
            {
                nextSend=now+.05f;
                if(!welcomed&&now>=nextHello){nextHello=now+.5f;Send(default,Ready,P.Encode(P.Player,w=>w.Write(JsonUtility.ToJson(LocalIdentity))));}
                if(welcomed&&waitingPose==0){waitingPose=++serial;Send(default,Pose,P.Encode(P.Player,w=>P.Write(w,IslandLobby.Current.Capture(Runner.LocalPlayer.ToString()))),waitingPose);}
                if(now-lastServer>20){Status="Lobby connection lost.";_=Leave();}
            }
        }
        async Task PushMigration()
        {pushingSnapshot=true;try{if(Runner&&Runner.IsRunning)await Runner.PushHostMigrationSnapshot();}catch(Exception e){Debug.LogWarning("Host snapshot: "+e.Message);}finally{pushingSnapshot=false;}}
        public void Launch()
        {
            var experience=RoomGameExperience.Ensure(this);
            if(experience&&experience.Experience==GameExperience.Game){experience.RequestStart();return;}
            if(CanLaunch)Act(new LobbyAction{verb="launch"});
        }
        void LaunchAuthoritative()
        {
            if(Busy||LaunchPending||!Catalog||!Catalog.modes[ModeIndex].available)return;
            if(!Connected)
            {
                var map=Catalog.maps[MapIndex];
                if(SceneUtility.GetBuildIndexByScenePath(map.scenePath)<0){Status="Selected map is not enabled in this build.";return;}
                MatchSetup.FromLobby=true;MatchSetup.StartLocalSession=false;MatchSetup.ModeId=Catalog.modes[ModeIndex].id;MatchSetup.MapId=map.id;
                BeginDeparture(SceneUtility.GetBuildIndexByScenePath(map.scenePath),false);return;
            }
            LaunchPending=true;launchDeadline=Time.unscaledTime+10;Status="Waiting for players to confirm the selected mode and map...";CheckLaunch();
        }
        public void CancelLaunch()
        {
            var experience=RoomGameExperience.Ensure(this);
            if(experience&&experience.Experience==GameExperience.Game){experience.CancelGameStart();return;}
            if(LocalIsLobbyHost&&!Departing)Act(new LobbyAction{verb="cancel"});
        }
        void CheckLaunch()
        {
            if(!LaunchPending||!IsHost||Departing||MarinaHostRecoveryPending)return;
            bool ready=Runner.ActivePlayers.Where(p=>p!=Runner.LocalPlayer).All(p=>peers.TryGetValue(p,out var peer)&&peer.ready&&peer.ackedSelection==selectionVersion);
            if(!ready){if(Time.unscaledTime>launchDeadline){LaunchPending=false;Status="A player has not confirmed the lobby setup. Retry Start when everyone is ready.";}return;}
            LaunchPending=false;
            int index=SceneUtility.GetBuildIndexByScenePath(Catalog.maps[MapIndex].scenePath);
            if(index<0){Status="Selected map is not enabled in this build.";return;}
            MatchSetup.FromLobby=true;MatchSetup.StartLocalSession=false;MatchSetup.ModeId=Catalog.modes[ModeIndex].id;MatchSetup.MapId=Catalog.maps[MapIndex].id;
            Status="Starting "+Catalog.modes[ModeIndex].title+"...";BeginDeparture(index,true);
        }
        public async Task Leave()
        {
            if(leaving)return;leaving=true;int attempt=++generation;connecting?.Cancel();Busy=true;
            var retiring=Runner;
            if(retiring)
            {
                FusionMigrationCleanup.BeforeShutdown(retiring);await retiring.Shutdown();
                if(attempt==generation&&ReferenceEquals(Runner,retiring))Disconnected("Left room");
            }
            else Disconnected("Left room");
        }
        void Disconnected(string reason)
        {
            Migrating=false;ClearMarinaSpawnReservations();ResetMarinaCallbacks();
            GetComponent<RoomGameExperience>()?.TransportDisconnected();
            CancelDeparture();
            ClearPendingMap();receivedMapOffer=0;SwitchingMap=false;mapAcks.Clear();
            if(!this)return;string scene=SceneManager.GetActiveScene().name;bool inGame=!IslandLobby.Current&&scene!="Home";Status="Disconnected: "+reason;
            connecting?.Dispose();connecting=null;
            if(IslandLobby.Current&&IslandLobby.Current.Voice)IslandLobby.Current.Voice.ResetLobbyAudio();
            if(Runner){Runner.RemoveCallbacks(callbacks);Destroy(Runner.gameObject);}Runner=null;peers.Clear();socialPeers.Clear();socialServer.Clear();waitingPose=0;nextHello=0;welcomed=false;HostSelected=false;SharedDemoControls=false;LaunchPending=false;Busy=false;leaving=false;transitioning=false;
            Code="";PrepareSoloIdentity();if(inGame)SceneManager.LoadScene("Lobby");else IslandLobby.Current?.ClearPeers();
        }
        public bool Owns(NetworkRunner runner)=>ownedRunner==runner;
        void OnApplicationQuit(){FusionMigrationCleanup.BeforeShutdown(Runner);}
        void OnDestroy(){Discovery?.Dispose();
#if UNITY_ANDROID && !UNITY_EDITOR
            if(wifiLock!=null){if(wifiLock.Call<bool>("isHeld"))wifiLock.Call("release");wifiLock.Dispose();}
#endif
            if(Instance==this)Instance=null;}
    }
}
