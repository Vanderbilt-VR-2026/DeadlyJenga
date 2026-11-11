using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using DeadlyJenga.Core;
using P=DeadlyJenga.CastlePrototype.CastleLanProtocol;

namespace DeadlyJenga.CastlePrototype
{
    // The island lobby: the local survivor, the room connection, other players and AI voices.
    // The island art, terminals and giant keyboard are Presentation's; they attach on
    // GameEvents.LobbyStarted and work through Core's ILobbyControls (IslandLobby.Controls.cs).
    [DefaultExecutionOrder(200)]
    public sealed partial class IslandLobby : MonoBehaviour, ISessionInfo, IWorldQuery
    {
        Vector3 lobbyLayoutCenter=Vector3.zero,lobbyLayoutSpawn=new Vector3(0,.5f,-3.5f);
        float lobbyLayoutTop=.45f,lobbyLayoutRadius=9,lobbyLayoutReturnDistance=30;
        Bounds lobbyLayoutBounds=new Bounds(Vector3.up*2,new Vector3(18,4,18));
        partial void ConfigureMayhemMarinaLobby();
        static partial void MayhemMarinaSpawn(int slot,ref Vector3 point);
        public IslandLobbyCatalog catalog;
        public static IslandLobby Current {get;private set;}
        public IPlayableCharacter Player {get;private set;}
        public IslandRoomConnection Room {get;private set;}
        public bool MenuOpen {get;private set;}
        bool departing;float departureAt,departureSeconds;Pose departurePose;
        bool refreshInputOnReturn;
        public void BeginDeparture(float seconds)
        {
            if(departing||!NetworkActors.Alive(Player))return;
            GetComponent<MarinaDockActivities>()?.ReturnRiderToDock();
            SetMenu(false);departing=true;departureAt=Time.unscaledTime;departureSeconds=seconds;
            departurePose=new Pose(Player.transform.position,Player.transform.rotation);Player.InputSuspended=true;
        }
        public void CancelDeparture()
        {
            if(!departing)return;departing=false;
            if(NetworkActors.Alive(Player))Player.Place(departurePose.position,departurePose.rotation);
            SetMenu(MenuOpen);
        }
        public IReadOnlyList<Rigidbody> Blocks=>Array.Empty<Rigidbody>();
        public Vector3 Center=>transform.TransformPoint(lobbyLayoutCenter);
        public float TopHeight=>transform.position.y+lobbyLayoutTop;
        public float Radius=>lobbyLayoutRadius;
        public Bounds PlayableBounds=>new Bounds(transform.TransformPoint(lobbyLayoutBounds.center),lobbyLayoutBounds.size);
        public int PeerCount=>peers.Count;
        public DeadlyJenga.CastleSocial.SocialWorld Voice {get;private set;}
        public float WaterHeight(Vector3 p)=>-.12f;
        public bool IsSheltered(Vector3 p)=>false;
        public Vector3 SafeSpawnPoint()=>transform.TransformPoint(lobbyLayoutSpawn);
        // Other players on the island, shown by Characters' remote views (Core's INetworkActorView).
        readonly Dictionary<string,INetworkActorView> peers=new Dictionary<string,INetworkActorView>();
        InputAction menu;bool focused=true;
        ISurvivorBotPopulation lobbyBots;IBotPopulation lobbyHands;float nextLobbyPopulation;
        bool lobbyWildlifeSeeded;
        void PopulateLobbyWildlife()
        {
            if (lobbyWildlifeSeeded || MatchResident || !Retained) return;
            var wildlife=GetComponent<IWildlifeControls>();
            if(wildlife?.Available!=true)return;
            lobbyWildlifeSeeded=true;
            wildlife.Spawn("fox",false);
            wildlife.Spawn("tortoise",false);
            wildlife.Spawn("frog",false);
            wildlife.Spawn("crab",false);
        }

        public void Awake()
        {
            if(Persistent&&Persistent!=this)
            {
                duplicate=true;enabled=false;
                if(Persistent.MatchResident||!ReferenceEquals(Services.Session,Persistent))Persistent.ResumeAfterMatch();
                foreach(var root in gameObject.scene.GetRootGameObjects()){root.SetActive(false);Destroy(root);}
                return;
            }
            if(!catalog||!catalog.survivorPrefab||!catalog.environmentPrefab){Debug.LogError("Island lobby catalog is incomplete.",this);enabled=false;return;}
            Persistent=this;
            var previousSession=Services.Session;var previousWorld=Services.World;
            var match=previousSession as CastlePlaySession;
            var activeScene=SceneManager.GetActiveScene();
            var existingRoots=activeScene.isLoaded?new System.Collections.Generic.HashSet<GameObject>(activeScene.GetRootGameObjects()):null;
            try
            {
                VisitOwner=match;Current=match?null:this;Services.Session=this;Services.World=this;
                catalog=IslandLobbyCatalog.Runtime(catalog);
                Room=IslandRoomConnection.Ensure();bool firstVisit=!Room.Catalog;Room.Catalog=catalog;
                if(!match){Room.StartDiscovery();if(firstVisit)Room.Region=catalog.defaultRegion;Room.CompleteDeparture();}
                Player=Instantiate(catalog.survivorPrefab).GetComponent<IPlayableCharacter>();
                if(Player==null){Debug.LogError("Island lobby survivor prefab needs an IPlayableCharacter.",this);enabled=false;return;}
                Player.gameObject.name="Persistent lobby survivor";Player.ViewCamera.clearFlags=CameraClearFlags.Skybox;
                if(!match){Room.PrepareSoloIdentity();ApplyLobbyIdentities();}
                ConfigureMayhemMarinaLobby();BuildFlatFloor();GameEvents.RaiseLobbyStarted(gameObject);marinaFloorSetupCompleted=true;
                PlaceLocalLobbyArrival();
                if(!match)
                {
                    CreateLobbyPopulation();Voice=gameObject.AddComponent<DeadlyJenga.CastleSocial.SocialWorld>();Voice.Initialize(this);
                }
                menu=new InputAction("Lobby controls",InputActionType.Button,"<XRController>{LeftHand}/secondaryButton");menu.Enable();
                SetMenu(false);
                if(existingRoots!=null)foreach(var root in activeScene.GetRootGameObjects())
                    if(root!=gameObject&&!existingRoots.Contains(root))root.transform.SetParent(transform,true);
                if(match)
                {
                    MatchResident=true;VisitOwner=null;Current=null;SetPlayerPresent(false);
                    GameEvents.RaiseEmoted(gameObject,"LobbyParked");
                }
            }
            finally
            {
                if(match){Services.Session=previousSession;Services.World=previousWorld;}
            }
        }
        void Update()
        {
            if(!NetworkActors.Alive(Player)||MatchResident&&!VisitOwner)return;
            if(departing)
            {
                float t=Mathf.Clamp01((Time.unscaledTime-departureAt)/Mathf.Max(.1f,departureSeconds));
                Player.InputSuspended=true;
                Player.transform.SetPositionAndRotation(departurePose.position+Vector3.up*(70*t*t),departurePose.rotation);
                foreach(var peer in peers.Values)peer.Render(Time.unscaledDeltaTime);
                return;
            }
            if (refreshInputOnReturn)
            {
                refreshInputOnReturn=false;
                SetMenu(MenuOpen);
            }
            var feet=Player.transform.position;
            bool outside=(feet-Center).sqrMagnitude>lobbyLayoutReturnDistance*lobbyLayoutReturnDistance;
            if (UsesMarinaSpawns)
            {
                var dock=GetComponent<MarinaDockActivities>();
                if ((outside||feet.y<WaterHeight(feet)-.025f) && !(dock&&dock.IsRiding))
                    RecoverMarinaPlayer();
            }
            else if (outside||feet.y<-.9f) PlaceLocalLobbyArrival();
            var keyboard=Keyboard.current;
            if(menu.WasPressedThisFrame()||keyboard!=null&&keyboard.escapeKey.wasPressedThisFrame||Player.MenuPressed)
            {
                if (codeFocused) UnfocusCode();
                else SetMenu(!MenuOpen);
            }
            if(!Player.IsVR&&!MenuOpen&&Cursor.lockState!=CursorLockMode.Locked&&!Application.isBatchMode)SetMenu(true);
            if(VisitOwner){RefreshVisitingBots();return;}
            HomeSession.ConsumeLobbyEntry(this);
            TickMarinaLocalArrival();
            foreach(var peer in peers.Values)peer.Render(Time.unscaledDeltaTime);
            ApplyLobbyIdentities();UpdateControls();
            if(lobbyBots!=null&&Time.unscaledTime>=nextLobbyPopulation)
            {
                nextLobbyPopulation=Time.unscaledTime+1;
                bool populate=(!Room.Connected||Room.IsHost)&&!Room.Busy&&!Room.Departing&&
                    (ExperienceProvider.IsRotatingLobby||HomeSession.AutofillLobby&&!Room.Connected&&Experience==GameExperience.Demo);
                lobbyBots.AutoFill=false;
                lobbyBots.Running=populate;
                if(!populate){if(lobbyBots.Count>0)lobbyBots.Clear();}
                else
                {
                    int humans=Room.Connected?Mathf.Max(1,Room.HumanCount):1;
                    lobbyBots.SetHumanPlayerCount(humans);
                    lobbyBots.TargetPopulation=10;
                    lobbyBots.FillToTarget();
                    if(ExperienceProvider.IsRotatingLobby)
                    {
                        int index=0;
                        foreach(var bot in lobbyBots.Members)
                            if(bot)ExperienceProvider.ApplyReturningBot(bot,index++);
                    }
                }
            }
        }
        public static Vector3 SpawnPoint(int slot)
        {float angle=(Math.Abs(slot)%10)*2.399963f;var point=(Persistent?Persistent.Center:Vector3.zero)+new Vector3(Mathf.Sin(angle)*1.8f,.65f,-3.2f+Mathf.Cos(angle)*.8f);MayhemMarinaSpawn(slot,ref point);return point;}
        public void SpawnAt(P.Pose pose)
        {
            if(UsesMarinaSpawns&&Room&&Room.Connected&&!Room.IsHost)
            {if(!PlaceLocalLobbyArrival())return;}
            else if(!ApplyReservedMarinaSpawn(pose))return;
            ClearPeers();
        }
        public void SetMenu(bool open)
        {
            MenuOpen=open;
            if (!NetworkActors.Alive(Player)) return;
            var dock=GetComponent<MarinaDockActivities>();
            Player.InputSuspended=marinaArrivalPending||departing||!focused||open||(dock&&dock.IsRiding);
            Cursor.lockState=!Player.IsVR&&focused&&!open?CursorLockMode.Locked:CursorLockMode.None;
            Cursor.visible=!Player.IsVR&&(open||!focused);
        }
        void OnApplicationFocus(bool value)
        {
            focused=value;
            if (!MatchResident||VisitOwner) SetMenu(MenuOpen);
        }
        public P.Actor Capture(string id)=>P.Capture(id,Player.gameObject.GetComponent<INetworkActor>());
        internal void CaptureLobbyBots(List<P.Actor> actors)
        {
            if(lobbyBots?.Members==null||!ExperienceProvider.IsRotatingLobby)return;
            foreach(var bot in lobbyBots.Members)
            {
                if(actors.Count>=10)break;
                if(bot&&bot.GetComponent<INetworkActor>() is { } actor)
                    actors.Add(P.Capture("lobby-bot:"+bot.GetInstanceID(),actor));
            }
        }
        public bool TryPeerPosition(string id,out Vector3 position)
        {if(Voice&&id==Voice.LocalParticipantId){position=Player.ViewCamera.transform.position;return true;}if(id!=null&&peers.TryGetValue(id,out var peer)){position=peer.NetworkPosition+Vector3.up*1.6f;return true;}position=default;return false;}
        public void ReceivePeer(P.Actor actor)
        {
            if(actor.kind!=NetworkKinds.Survivor||actor.id==Room.Runner?.LocalPlayer.ToString())return;
            if(!peers.TryGetValue(actor.id,out var peer))
            {
                if(peers.Count>=9)return;
                var root=new GameObject("Lobby guest Â· "+actor.id);
                peer=NetworkActors.CreateView(actor.kind,root,new NetworkContext{host=false,localSurvivor=Player.gameObject,actorId=actor.id});
                if(peer==null){Destroy(root);return;}
                peers[actor.id]=peer;
            }
            using(var reader=P.StateReader(actor))peer.ReadState(reader,true);
        }
        public void ReceivePeers(List<P.Actor> actors,string local)
        {foreach(var id in peers.Keys.ToArray())if(!actors.Any(a=>a.id==id))RemovePeer(id);foreach(var actor in actors)if(actor.id!=local)ReceivePeer(actor);}
        public void RemovePeer(string id){if(peers.TryGetValue(id,out var p)){if(p is Component c&&c)Destroy(c.gameObject);peers.Remove(id);}}
        public void ClearPeers(){foreach(var id in peers.Keys.ToArray())RemovePeer(id);}
        void OnDestroy()
        {
            if(duplicate)return;
            if(Persistent==this)Persistent=null;
            menu?.Dispose();lobbyBots?.Clear();lobbyHands?.Clear();ClearPeers();if(NetworkActors.Alive(Player))Destroy(Player.gameObject);
            if(Current==this)Current=null;if(ReferenceEquals(Services.Session,this))Services.Session=null;if(ReferenceEquals(Services.World,this))Services.World=null;
            if(!VisitOwner){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
        }
    }
}
