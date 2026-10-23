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
    public sealed class CastlePlaySession : MonoBehaviour, ISessionInfo, IWorldAuthority, IPostGameResults, ISeaSpiritControls
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
        public bool IsVR => NetworkActors.Alive(Active) && Active.IsVR;
        public Camera ActiveCamera => Modes && Modes.OverrideCamera ? Modes.OverrideCamera : NetworkActors.Alive(Active) ? Active.ViewCamera : null;
        public DeadlyJenga.Modes.MatchModeRuntime Modes { get; private set; }
        public PostGameReport LatestReport => Modes ? Modes.LatestReport : null;
        ISeaSpiritControls seaSpirit;
        public bool SeaSpiritActive=>seaSpirit?.SeaSpiritActive??false;
        public float SeaSpiritReach=>seaSpirit?.SeaSpiritReach??24;
        public float SeaSpiritOrbit=>seaSpirit?.SeaSpiritOrbit??0;
        public void SetSeaSpirit(bool active)
        {
            if(!Ready)return;
            if(active&&Modes&&Modes.Rules.mode!=GameModeKind.Demo)return;
            var previousView=ActiveCamera;
            var entrance=previousView?new Pose(previousView.transform.position,previousView.transform.rotation):default;
            if(active&&!PlayingHand)SetRole(true);
            if(active&&!SeaSpiritActive&&previousView&&Hand.ViewCamera)
            {
                var camera=Hand.ViewCamera;var rig=camera.transform.parent;
                if(rig){rig.rotation=entrance.rotation*Quaternion.Inverse(camera.transform.localRotation);rig.position+=entrance.position-camera.transform.position;}
            }
            if(seaSpirit==null&&active)seaSpirit=SeaSpiritSystems.Create?.Invoke(Hand.gameObject);
            seaSpirit?.SetSeaSpirit(active);
            if(active&&SeaSpiritActive)SetMenu(false);
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
        public bool CanEditWorld => !Network || Network.CanEditWorld;
        InputAction switchRole, toggleMenu, select;
        bool wasVR, focused = true;
        bool arrivalPending,travelInputHeld;float travelReleaseAt=-1;

        void Start()
        {
            Services.Session = this;
            arrivalPending=true;travelInputHeld=MatchSetup.FromLobby;
            GameEvents.Eliminated += OnEliminated;
            MatchSetup.ApplyMap();
            if (overviewCamera) overviewCamera.gameObject.SetActive(false);
            Player = Instantiate(survivorPrefab).GetComponent<IPlayableCharacter>(); Player.gameObject.name = "Survivor (local)";
            TuneJump();
            Hand = Instantiate(handPrefab).GetComponent<IPlayableCharacter>(); Hand.gameObject.name = "Flying Hand (local)";
            Player.PrepareForSession(); Hand.PrepareForSession();
            switchRole = new InputAction("Switch role", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
            toggleMenu = new InputAction("Demo menu", InputActionType.Button, "<XRController>{LeftHand}/secondaryButton");
            select = new InputAction("Menu select", InputActionType.Button, "<XRController>{RightHand}/trigger");
            switchRole.Enable(); toggleMenu.Enable(); select.Enable();
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
            MatchSetup.Apply(this);
            MatchSetup.FillRecommended(this);
            var social = gameObject.AddComponent<DeadlyJenga.CastleSocial.SocialWorld>(); social.Initialize(this);
            Demo = new SessionDemoControls(this);
            Social = new DeadlyJenga.CastleSocial.SocialControls(social);
            Modes=gameObject.AddComponent<DeadlyJenga.Modes.MatchModeRuntime>();
            Modes.Initialize(this);
            // Menus, HUDs and the stamina bar attach themselves (Presentation listens for this).
            GameEvents.RaiseSessionStarted(gameObject);
        }
        void OnDestroy()
        {
            seaSpirit?.SetSeaSpirit(false);
            if (ReferenceEquals(Services.Session, this)) Services.Session = null;
            GameEvents.Eliminated -= OnEliminated;
            switchRole?.Dispose(); toggleMenu?.Dispose(); select?.Dispose();
            if (NetworkActors.Alive(Player)) Destroy(Player.gameObject);
            if (NetworkActors.Alive(Hand)) Destroy(Hand.gameObject);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
        bool Ready => NetworkActors.Alive(Player) && NetworkActors.Alive(Hand);
        void OnApplicationFocus(bool value) { focused = value; if (!value && Ready) SetMenu(true); }
        public void SetRole(bool hand)
        {
            if(Modes&&Modes.DemoSpectating)Modes.SetDemoSpectating(false);
            if(!hand&&SeaSpiritActive)seaSpirit.SetSeaSpirit(false);
            PlayingHand = hand;
            Player.gameObject.SetActive(!hand); Hand.gameObject.SetActive(hand);
            if(Player.ViewCamera)Player.ViewCamera.enabled=!hand;
            if(Hand.ViewCamera)Hand.ViewCamera.enabled=hand;
            if (hand)
            {
                // Float beside the map, a little above its top.
                var world = Services.World;
                Hand.Place(world.Center + new Vector3(0, world.TopHeight - world.Center.y + 3, -world.Radius - 10), Quaternion.identity);
            }
            else RespawnPlayer();
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
        public void ResetMap()
        {
            // Clients ask the host; the host stops disasters and rebuilds for everyone.
            if (Network && Network.IsClient) { CastleDemoCommands.Run(this, CastleDemoAction.Rebuild); return; }
            DemoDisasters.StopAll(this);
            (Services.World as IRebuildable)?.Rebuild();
            TuneJump(); SetRole(PlayingHand);
            HandBots?.MapRebuilt(); SurvivorBots?.MapRebuilt();
            if(Modes)Modes.MapRebuilt();
        }
        public void ResetCastle() => ResetMap();
        public void SetMenu(bool open)
        {
            MenuOpen = open;
            bool suspended = travelInputHeld || open || !focused || (Network && Network.WaitingForWorld) || (Modes && Modes.DemoSpectating);
            Player.InputSuspended = suspended;
            Hand.InputSuspended = suspended;
            Cursor.lockState = !open && !IsVR && focused ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = open || IsVR;
        }
        void Update()
        {
            if (!Ready) return;
            if(arrivalPending&&(!Network||!Network.WaitingForWorld))
            {
                arrivalPending=false;if(travelInputHeld)travelReleaseAt=Time.unscaledTime+.8f;
                GameEvents.RaiseEmoted(gameObject,"TravelArrived");
            }
            if(travelInputHeld&&travelReleaseAt>=0&&Time.unscaledTime>=travelReleaseAt){travelInputHeld=false;SetMenu(MenuOpen);}
            var k = Keyboard.current;
            if (IsVR != wasVR) { wasVR = IsVR; SetMenu(MenuOpen); }
            if ((!Modes||Modes.Rules.mode==GameModeKind.Demo)&&!(Modes&&Modes.DemoSpectating)&&!MenuOpen&&((IsVR && switchRole.WasPressedThisFrame()) || (!IsVR && k != null && k.tabKey.wasPressedThisFrame))) SetRole(!PlayingHand);
            if ((IsVR && toggleMenu.WasPressedThisFrame()) || (!IsVR && k != null && k.escapeKey.wasPressedThisFrame)) SetMenu(!MenuOpen);
            if (IsVR && Active.MenuPressed) SetMenu(!MenuOpen);
            if (!IsVR && Cursor.lockState != CursorLockMode.Locked && !MenuOpen) SetMenu(true);
            if (!PlayingHand && NetworkActors.Alive(Player) && Player.transform.position.y < -6)
            {
                if(Modes)Player.gameObject.GetComponent<IDamageable>()?.Eliminate(gameObject);
                else RespawnPlayer();
            }
        }
    }
}
