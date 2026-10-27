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
        void OnEnable()=>GameEvents.HomeStarted+=HomeOpened;
        void OnDisable()=>GameEvents.HomeStarted-=HomeOpened;
        void HomeOpened(GameObject home){if(Persistent==this)ParkForMatch();}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetResidence(){Persistent=null;Current=null;}
        void Start(){if(!duplicate)RetainRoots();}
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
            if(Player.transform.position.y<-.9f||(Player.transform.position-Center).sqrMagnitude>900)Player.Place(SafeSpawnPoint(),Quaternion.identity);
            SetupHint="The match is still running. Return whenever you are ready.";
            HostHint=JoinHint="Match in progress - room changes are locked during this visit.";
            SetMenu(false);return true;
        }
        internal void EndMatchVisit(CastlePlaySession owner)
        {
            if(VisitOwner!=owner)return;
            VisitOwner=null;SetPlayerPresent(false);
        }
        internal void ResumeAfterMatch()
        {
            MatchResident=false;VisitOwner=null;departing=false;
            Current=this;Services.Session=this;Services.World=this;
            Room.CompleteDeparture();Room.ResumeLobbyConnection();
            SetPlayerPresent(true);
            if(lobbyBots==null)CreateLobbyPopulation();
            if(!Voice){Voice=gameObject.AddComponent<DeadlyJenga.CastleSocial.SocialWorld>();Voice.Initialize(this);}else Voice.enabled=true;
            Player.Place(SafeSpawnPoint(),Quaternion.identity);
            GameEvents.RaiseEmoted(gameObject,"LobbyResumed");
            UpdateControls();SetMenu(false);
        }
        void CreateLobbyPopulation()
        {
            BotSystems.Create(gameObject,new BotSetup{player=Player,survivorPrefab=catalog.survivorPrefab,isPlayingHand=()=>false},out lobbyBots,out lobbyHands);
        }
    }
}
