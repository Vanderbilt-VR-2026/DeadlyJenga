using Fusion;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    // Runs the match: creates the local survivor and hand, switches between them, respawns,
    // starts bots and networking. Talks to every other box only through Core.
    [DefaultExecutionOrder(200)]
    public sealed partial class CastlePlaySession : MonoBehaviour, ISessionInfo, IWorldAuthority, IPostGameResults, ISeaSpiritControls
    {
        [Tooltip("The survivor prefab (any component on it that implements IPlayableCharacter).")]
        public MonoBehaviour survivorPrefab;
        [Tooltip("The flying hand prefab (any component on it that implements IPlayableCharacter).")]
        public MonoBehaviour handPrefab;
        public Camera overviewCamera;
        public bool tuneJumpToBlockHeight=true;
        public float jumpShortfall=.18f;
        [Header("Temporary development stage")]
        [Tooltip("Turn OFF for release. Enables role spoilers and manual match ending for development review only; unrelated to host powers.")]
        public bool developmentReviewTools=true;
        [Header("Next tsunami")]
        [Range(.5f,30)] public float tsunamiSpeed=8;
        [Range(0,5)] public float tsunamiForce=1;
        public IPlayableCharacter Player { get; private set; }
        public IPlayableCharacter Hand { get; private set; }
        public IBotPopulation HandBots { get; private set; }
        public bool PlayingHand { get; private set; }
        public bool MenuOpen { get; private set; }
        IPlayableCharacter Active => PlayingHand ? Hand : Player;
        public bool IsVR => VisitingLobby ? visitLobby.IsVR : NetworkActors.Alive(Active) && Active.IsVR;
        public Camera ActiveCamera => VisitingLobby ? visitLobby.ActiveCamera : Modes && Modes.OverrideCamera ? Modes.OverrideCamera : NetworkActors.Alive(Active) ? Active.ViewCamera : null;
        public DeadlyJenga.Modes.MatchModeRuntime Modes { get; private set; }
        public PostGameReport LatestReport => Modes ? Modes.LatestReport : null;
        ISeaSpiritControls seaSpirit;
        public bool SeaSpiritActive=>seaSpirit?.SeaSpiritActive??false;
        public float SeaSpiritReach=>seaSpirit?.SeaSpiritReach??24;
        public float SeaSpiritOrbit=>seaSpirit?.SeaSpiritOrbit??0;
        public void SetSeaSpirit(bool active)
        {
            if(!active)
            {
                if(!SeaSpiritActive)return;
                seaSpirit.SetSeaSpirit(false);
                if(!SeaSpiritActive&&Ready)SetMenu(MenuOpen);
                return;
            }
            if(!Ready||VisitingLobby||SeaSpiritActive||!CanEditWorld)return;
            // Recheck character readiness on every entry before changing role or camera.
            var controls=SeaSpiritSystems.Create?.Invoke(Hand.gameObject);
            if(controls==null)return;
            var previousView=ActiveCamera;
            bool haveEntrance=previousView;
            var entrance=haveEntrance?new Pose(previousView.transform.position,previousView.transform.rotation):default;
            if(!PlayingHand)SetRole(true);
            if(!PlayingHand)return;
            if(Modes&&Modes.DemoSpectating)Modes.SetDemoSpectating(false);
            if(haveEntrance&&Hand.ViewCamera)
            {
                var camera=Hand.ViewCamera;var rig=camera.transform.parent;
                if(rig){var relativeRotation=Quaternion.Inverse(rig.rotation)*camera.transform.rotation;rig.rotation=entrance.rotation*Quaternion.Inverse(relativeRotation);rig.position+=entrance.position-camera.transform.position;}
            }
            seaSpirit=controls;
            seaSpirit.SetSeaSpirit(true);
            if(SeaSpiritActive)SetMenu(false);
            else
            {
                SetMenu(MenuOpen);
                Debug.LogWarning("Sea monster entry failed after switching to the flying hand.",this);
            }
        }
        public void SetSeaSpiritReach(float meters)=>seaSpirit?.SetSeaSpiritReach(meters);
        public void RecenterSeaSpirit()=>seaSpirit?.RecenterSeaSpirit();
        public bool MenuSelectPressed => MenuOpen && IsVR && select != null && (select.WasPressedThisFrame() || Active.SelectPressed);
        public bool MenuSelectHeld => MenuOpen && IsVR && select != null && (select.IsPressed() || Active.SelectHeld);
        public bool HasWorldAuthority => !Network || !Network.IsClient;
        public Vector3 LastSpawnFeet { get; private set; }
        public ISurvivorBotPopulation SurvivorBots { get; private set; }
        public CastleLanSession Network { get; private set; }
        public IMultiplayerControls Multiplayer => Network;
        public IDemoControls Demo { get; private set; }
        public ISocialControls Social { get; private set; }
        public bool CanEditWorld => SessionDevelopmentPolicy.CanRequestLocal(this);
        InputAction select;
        bool wasVR, focused = true;
        bool arrivalPending,travelInputHeld;float travelReleaseAt=-1;

        void Start()
        {
            IslandLobby.Persistent?.ParkForMatch();
            Services.Session = this;
            arrivalPending=true;travelInputHeld=MatchSetup.FromLobby;
            GameEvents.Eliminated += OnEliminated;
            MatchSetup.ApplyMap();
            if (overviewCamera) overviewCamera.gameObject.SetActive(false);
            Player = Instantiate(survivorPrefab).GetComponent<IPlayableCharacter>(); Player.gameObject.name = "Survivor (local)";
            TuneJump();
            Hand = Instantiate(handPrefab).GetComponent<IPlayableCharacter>(); Hand.gameObject.name = "Flying Hand (local)";
            Player.PrepareForSession(); Hand.PrepareForSession();
            select = new InputAction("Menu select", InputActionType.Button, "<XRController>{RightHand}/trigger");
            select.Enable();
            SetRole(false); SetMenu(false);
            BotSystems.Create(gameObject, new BotSetup { player = Player, hand = Hand, survivorPrefab = survivorPrefab, handPrefab = handPrefab, isPlayingHand = () => PlayingHand },
                out var survivorBots, out var handBots);
            SurvivorBots = survivorBots; HandBots = handBots;
            var room = IslandRoomConnection.Instance;
            room?.CompleteDeparture();
            var runner = room && room.Connected ? room.Runner : NetworkRunner.Instances.FirstOrDefault(r => r != null && r.IsRunning);
            if (runner != null)
            {
                Network = gameObject.AddComponent<CastleLanSession>();
                gameObject.AddComponent<CastleFusionSession>().Initialize(runner, this, Network);
            }
            else if(MatchSetup.StartLocalSession)
            {
                Network=gameObject.AddComponent<CastleLanSession>();
                Network.Initialize(this);
            }
            if(!room&&!MatchSetup.FromLobby&&!Network&&runner==null)
                room=IslandRoomConnection.Ensure();
            MatchSetup.Apply(this);
            if(!RoomGameExperience.Manages(this))MatchSetup.FillRecommended(this);
            var social = gameObject.AddComponent<DeadlyJenga.CastleSocial.SocialWorld>(); social.Initialize(this);
            Demo = new SessionDemoControls(this);
            Social = new DeadlyJenga.CastleSocial.SocialControls(social);
            Modes=gameObject.AddComponent<DeadlyJenga.Modes.MatchModeRuntime>();
            Modes.Initialize(this,RoomGameExperience.Manages(this));
            gameObject.AddComponent<CastleSessionMenuInput>().session=this;
            // Menus, HUDs and the stamina bar attach themselves (Presentation listens for this).
            GameEvents.RaiseSessionStarted(gameObject);
        }
        void OnDestroy()
        {
            CloseLobbyVisit(false);
            seaSpirit?.SetSeaSpirit(false);
            if (ReferenceEquals(Services.Session, this)) Services.Session = null;
            GameEvents.Eliminated -= OnEliminated;
            select?.Dispose();
            if (NetworkActors.Alive(Player)) Destroy(Player.gameObject);
            if (NetworkActors.Alive(Hand)) Destroy(Hand.gameObject);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
        bool Ready => NetworkActors.Alive(Player) && NetworkActors.Alive(Hand);
        void OnApplicationFocus(bool value) { focused = value; HoldMenuGameplayInput(); if (!value && Ready) SetMenu(true); }
        public void SetRole(bool hand)
        {
            if(Modes&&!SessionDevelopmentPolicy.DemoEnabled(this))return;
            ApplyRole(hand);
        }
        internal void SetRoleForMode(bool hand) => ApplyRole(hand);

        internal bool TryEnterTimedHand(Pose spawn)
        {
            if(!Ready||VisitingLobby||!Hand.ViewCamera||Network&&Network.WaitingForWorld)return false;
            if(!PlayingHand)ApplyRole(true,spawn);
            return PlayingHand&&Hand.gameObject.activeInHierarchy;
        }

        void ApplyRole(bool hand,Pose? handSpawn=null)
        {
            if(VisitingLobby)return;
            if(Modes&&Modes.Rules.mode!=GameModeKind.Demo&&Modes.Authority)
            {bool accepted=Modes.SetDevelopmentHand(hand);if(hand&&!accepted)return;}
            if(Modes&&Modes.DemoSpectating)Modes.SetDemoSpectating(false);
            if(!hand&&SeaSpiritActive)seaSpirit.SetSeaSpirit(false);
            if(handSpawn.HasValue)
            {
                // Place the receiving rig before removing the survivor and its camera.
                Hand.gameObject.SetActive(true);
                try { Hand.Place(handSpawn.Value.position,handSpawn.Value.rotation); }
                catch
                {
                    if(!PlayingHand)Hand.gameObject.SetActive(false);
                    throw;
                }
            }
            PlayingHand = hand;
            Player.gameObject.SetActive(!hand); Hand.gameObject.SetActive(hand);
            if(Player.ViewCamera)Player.ViewCamera.enabled=!hand;
            if(Hand.ViewCamera)Hand.ViewCamera.enabled=hand;
            if (hand && !handSpawn.HasValue)
            {
                // Float beside the map, a little above its top.
                var world = Services.World;
                Hand.Place(world.Center + new Vector3(0, world.TopHeight - world.Center.y + 3, -world.Radius - 10), Quaternion.identity);
            }
            else if (!hand) RespawnPlayer();
            SetMenu(MenuOpen);
        }
        // The ocean (or any hazard) eliminated someone. In this demo the local survivor just respawns.
        void OnEliminated(GameObject who, GameObject source)
        {
            if (Modes && Modes.HandleEliminated(who, source)) return;
            if (Ready && who == Player.gameObject && !PlayingHand) RespawnPlayer();
        }

        public void RespawnPlayer()
        {
            Vector3 feet = Modes ? Modes.SpawnPoint((Player.gameObject.GetComponent<IModeActor>()?.Team)??-1,0,"survivor") : Services.World.SafeSpawnPoint();
            Vector3 heading = Vector3.ProjectOnPlane(Services.World.Center - feet, Vector3.up);
            LastSpawnFeet = feet;
            Player.Place(feet, heading.sqrMagnitude > .01f ? Quaternion.LookRotation(heading) : Quaternion.identity);
        }
        void TuneJump()
        {
            if (!tuneJumpToBlockHeight || Services.World == null) return;
            (Player as ISurvivorSettings)?.FitJump(Services.World.StepHeight, jumpShortfall);
        }
        public void ResetMap() => CastleDemoCommands.Run(this, CastleDemoAction.Rebuild);
        internal void RebuildForDevelopment()
        {
            if(!SessionDevelopmentPolicy.CanExecuteServer(this))return;
            DemoDisasters.StopAll(this);
            (Services.World as IRebuildable)?.Rebuild();
            TuneJump(); ApplyRole(PlayingHand);
            HandBots?.MapRebuilt(); SurvivorBots?.MapRebuilt();
            if(Modes)Modes.MapRebuilt();
        }
        public void ResetCastle() => ResetMap();
        public void SetMenu(bool open)
        {
            if(VisitingLobby){visitLobby.SetMenu(open);return;}
            if(MenuOpen!=open)HoldMenuGameplayInput();
            MenuOpen = open;
            bool suspended = menuResumePending || travelInputHeld || open || !focused || (Network && Network.WaitingForWorld) || (Modes && Modes.DemoSpectating) || RoomGameExperience.ForManagedMatch(this)?.HoldsLocalParticipant(this)==true;
            Player.InputSuspended = suspended;
            Hand.InputSuspended = suspended;
            Cursor.lockState = !open && !IsVR && focused ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = open || IsVR;
        }
        void Update()
        {
            if (!Ready || VisitingLobby) return;
            if(arrivalPending&&(!Network||!Network.WaitingForWorld))
            {
                arrivalPending=false;if(travelInputHeld)travelReleaseAt=Time.unscaledTime+.8f;
                GameEvents.RaiseEmoted(gameObject,"TravelArrived");
            }
            if(travelInputHeld&&travelReleaseAt>=0&&Time.unscaledTime>=travelReleaseAt){travelInputHeld=false;SetMenu(MenuOpen);}
            if (IsVR != wasVR) { wasVR = IsVR; SetMenu(MenuOpen); }
            if (!IsVR && Cursor.lockState != CursorLockMode.Locked && !MenuOpen) SetMenu(true);
            if (!PlayingHand && NetworkActors.Alive(Player) && Player.transform.position.y < -6)
            {
                if(Modes)Player.gameObject.GetComponent<IDamageable>()?.Eliminate(gameObject);
                else RespawnPlayer();
            }
        }
    }
}
