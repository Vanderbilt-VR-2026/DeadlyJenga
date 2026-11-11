using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class IslandLobby
    {
        public static IslandLobby Persistent {get;private set;}
        public bool MatchResident {get;private set;}
        public bool Retained {get;private set;}
        bool duplicate;
        readonly Dictionary<string,GameObject> visitingBots=new();
        readonly HashSet<string> seenVisitingBots=new();
        readonly List<string> departedVisitingBots=new();
        float nextVisitingBotRefresh;
        void RefreshVisitingBots()
        {
            if(Time.unscaledTime<nextVisitingBotRefresh)return;
            nextVisitingBotRefresh=Time.unscaledTime+1;
            var frame=VisitOwner?.Modes?.Frame;
            if(frame==null)return;
            seenVisitingBots.Clear();
            foreach(var row in frame.players)
            {
                if(!row.bot||row.alive||row.dying||row.role!="spectator"||!seenVisitingBots.Add(row.id))continue;
                if(!visitingBots.ContainsKey(row.id))
                {
                    var bot=Instantiate(catalog.survivorPrefab,transform).GetComponent<IPlayableCharacter>();
                    if(bot==null)continue;
                    var root=bot.gameObject;
                    root.name=row.name;
                    bot.InputSuspended=true;
                    if(bot.ViewCamera)bot.ViewCamera.enabled=false;
                    foreach(var listener in root.GetComponentsInChildren<AudioListener>(true))listener.enabled=false;
                    foreach(var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if(component is INetworkActor actor)NetworkActors.Local.Remove(actor);
                        if(component is IMovementState mover)Tracked.Movers.Remove(mover);
                    }
                    root.GetComponent<IModeActor>()?.SetIdentity("lobby-bot-"+row.id,row.name,-1,row.color);
                    int slot=seenVisitingBots.Count+3;
                    if(TryMarinaSpawnPose(slot,out var pose,out _,root)!=MarinaSpawnResult.Accepted)
                        pose=new CastleLanProtocol.Pose(SpawnPoint(slot),Quaternion.identity);
                    bot.Place(pose.position,pose.rotation);
                    visitingBots.Add(row.id,root);
                }
                if(seenVisitingBots.Count>=8)break;
            }
            departedVisitingBots.Clear();
            foreach(var pair in visitingBots)
                if(!seenVisitingBots.Contains(pair.Key))departedVisitingBots.Add(pair.Key);
            foreach(var id in departedVisitingBots)
            {
                if(visitingBots[id])Destroy(visitingBots[id]);
                visitingBots.Remove(id);
            }
        }
        void ClearVisitingBots()
        {
            foreach(var bot in visitingBots.Values)if(bot)Destroy(bot);
            visitingBots.Clear();
        }
        void OnEnable()=>GameEvents.HomeStarted+=HomeOpened;
        void OnDisable()=>GameEvents.HomeStarted-=HomeOpened;
        void HomeOpened(GameObject home){if(Persistent==this)ParkForMatch();}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetResidence(){Persistent=null;Current=null;}
        void Start()
        {
            if (duplicate) return;
            RetainRoots();
            // Animals keep world-space movement targets, so spawn after moving the retained lobby.
            PopulateLobbyWildlife();
        }
        void RetainRoots()
        {
            if(Retained||duplicate)return;
            var scene=gameObject.scene;
            foreach(var root in scene.GetRootGameObjects())if(root!=gameObject)root.transform.SetParent(transform,true);
            DontDestroyOnLoad(gameObject);
            transform.position+=new Vector3(1000,0,1000);
            Retained=true;
            if(MatchResident)GameEvents.RaiseEmoted(gameObject,"LobbyParked");
        }
        internal void ParkForMatch()
        {
            if(duplicate)return;
            RetainRoots();
            if(MatchResident&&!VisitOwner)return;
            GetComponent<MarinaDockActivities>()?.ReturnRiderToDock();
            CancelDeparture();MatchResident=true;VisitOwner=null;
            if(Current==this)Current=null;
            if(lobbyBots!=null){lobbyBots.AutoFill=false;lobbyBots.Running=false;lobbyBots.Clear();}
            if(lobbyHands!=null){lobbyHands.Running=false;lobbyHands.Clear();}
            ClearPeers();
            if(Voice){Voice.ResetLobbyAudio();Voice.enabled=false;}
            SetPlayerPresent(false);
            GameEvents.RaiseEmoted(gameObject,"LobbyParked");
        }
        void SetPlayerPresent(bool present)
        {
            if(!NetworkActors.Alive(Player))return;
            if(!present)Player.InputSuspended=true;
            Player.gameObject.SetActive(present);
            if(Player.ViewCamera)Player.ViewCamera.enabled=present;
            foreach(var listener in Player.gameObject.GetComponentsInChildren<AudioListener>(true))listener.enabled=present;
        }
        internal bool BeginMatchVisit(CastlePlaySession owner)
        {
            if(!owner||VisitOwner&&VisitOwner!=owner)return false;
            ParkForMatch();VisitOwner=owner;
            SetPlayerPresent(true);
            foreach(var component in Player.gameObject.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if(component is INetworkActor actor)NetworkActors.Local.Remove(actor);
                if(component is IMovementState mover)Tracked.Movers.Remove(mover);
            }
            var identity=owner.Player.gameObject.GetComponent<IModeActor>();
            if(identity!=null)Player.gameObject.GetComponent<IModeActor>()?.SetIdentity("lobby-visitor",identity.DisplayName,-1,identity.Color);
            if (owner.EliminatedLobbySpectator)
            {
                int slot=UsesMarinaSpawns?3:2;
                if (TryMarinaSpawnPose(slot,out var pose,out _,Player.gameObject)!=MarinaSpawnResult.Accepted)
                    pose=new CastleLanProtocol.Pose(SafeSpawnPoint(),Quaternion.identity);
                var tv=transform.TransformPoint(UsesMarinaSpawns?new Vector3(5.48f,1.86f,7.35f):new Vector3(-4.8f,.12f,-4.7f));
                var towards=Vector3.ProjectOnPlane(tv-pose.position,Vector3.up);
                Player.Place(pose.position,towards.sqrMagnitude>.01f?Quaternion.LookRotation(towards):pose.rotation);
            }
            else if(Player.transform.position.y<-.9f||(Player.transform.position-Center).sqrMagnitude>900)PlaceLocalLobbyArrival();
            SetupHint="The match is still running. Return whenever you are ready.";
            HostHint=JoinHint="Match in progress - room changes are locked during this visit.";
            GameEvents.RaiseEmoted(gameObject,"LobbyVisitOpened");
            SetMenu(false);return true;
        }
        internal void EndMatchVisit(CastlePlaySession owner)
        {
            if(VisitOwner!=owner)return;
            VisitOwner=null;ClearVisitingBots();SetPlayerPresent(false);
            GameEvents.RaiseEmoted(gameObject,"LobbyParked");
        }
        internal void ResumeAfterMatch()
        {
            MatchResident=false;VisitOwner=null;ClearVisitingBots();departing=false;
            Current=this;Services.Session=this;Services.World=this;
            Room.CompleteDeparture();Room.ResumeLobbyConnection();
            SetPlayerPresent(true);
            if(lobbyBots==null)CreateLobbyPopulation();
            if(!Voice){Voice=gameObject.AddComponent<DeadlyJenga.CastleSocial.SocialWorld>();Voice.Initialize(this);}else Voice.enabled=true;
            PlaceLocalLobbyArrival(returning:true);
            refreshInputOnReturn=true;
            PopulateLobbyWildlife();
            GameEvents.RaiseEmoted(gameObject,"LobbyResumed");
            UpdateControls();SetMenu(false);
        }
        void CreateLobbyPopulation()
        {
            BotSystems.Create(gameObject,new BotSetup{player=Player,survivorPrefab=catalog.survivorPrefab,isPlayingHand=()=>false},out lobbyBots,out lobbyHands);
        }
    }
}
