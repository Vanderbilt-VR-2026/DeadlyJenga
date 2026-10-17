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
    public sealed class CastlePlaySession : MonoBehaviour, ISessionInfo
    {
        [Tooltip("The survivor prefab (any component on it that implements IPlayableCharacter).")]
        public MonoBehaviour survivorPrefab;
        [Tooltip("The flying hand prefab (any component on it that implements IPlayableCharacter).")]
        public MonoBehaviour handPrefab;
        public Camera overviewCamera;
        public bool tuneJumpToBlockHeight=true;
        public float jumpShortfall=.18f;
        public IPlayableCharacter Player { get; private set; }
        public IPlayableCharacter Hand { get; private set; }
        public bool PlayingHand { get; private set; }
        public bool MenuOpen { get; private set; }
        IPlayableCharacter Active => PlayingHand ? Hand : Player;
        public bool IsVR => NetworkActors.Alive(Active) && Active.IsVR;
        public Camera ActiveCamera => NetworkActors.Alive(Active) ? Active.ViewCamera : null;
        public bool MenuSelectPressed => MenuOpen && IsVR && select != null && (select.WasPressedThisFrame() || Active.SelectPressed);
        public Vector3 LastSpawnFeet { get; private set; }
        public ISurvivorBotPopulation SurvivorBots { get; private set; }
        public IBotPopulation HandBots { get; private set; }
        public CastleLanSession Network { get; private set; }
        public IMultiplayerControls Multiplayer => Network;
        public bool CanEditWorld => !Network || Network.CanEditWorld;
        InputAction switchRole, toggleMenu, select;
        bool wasVR, focused = true;

        void Start()
        {
            Services.Session = this;
            GameEvents.Eliminated += OnEliminated;
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
            var runner = NetworkRunner.Instances.FirstOrDefault(r => r != null && r.IsRunning);
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
            // Menus, HUDs and the stamina bar attach themselves (Presentation listens for this).
            GameEvents.RaiseSessionStarted(gameObject);
        }
        void OnDestroy()
        {
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
            PlayingHand = hand;
            Player.gameObject.SetActive(!hand); Hand.gameObject.SetActive(hand);
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
            if (Ready && who == Player.gameObject && !PlayingHand) RespawnPlayer();
        }

        public void RespawnPlayer()
        {
            Vector3 feet = Services.World.SafeSpawnPoint();
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
            if (!CanEditWorld) return;
            (Services.World as IRebuildable)?.Rebuild();
            TuneJump(); SetRole(PlayingHand);
            HandBots?.MapRebuilt(); SurvivorBots?.MapRebuilt();
        }
        public void ResetCastle() => ResetMap();
        public void SetMenu(bool open)
        {
            MenuOpen = open;
            bool suspended = open || !focused || (Network && Network.WaitingForWorld);
            Player.InputSuspended = suspended;
            Hand.InputSuspended = suspended;
            Cursor.lockState = !open && !IsVR && focused ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = open || IsVR;
        }
        void Update()
        {
            if (!Ready) return;
            var k = Keyboard.current;
            if (IsVR != wasVR) { wasVR = IsVR; SetMenu(MenuOpen); }
            if (!MenuOpen&&((IsVR && switchRole.WasPressedThisFrame()) || (!IsVR && k != null && k.tabKey.wasPressedThisFrame))) SetRole(!PlayingHand);
            if ((IsVR && toggleMenu.WasPressedThisFrame()) || (!IsVR && k != null && k.escapeKey.wasPressedThisFrame)) SetMenu(!MenuOpen);
            if (IsVR && Active.MenuPressed) SetMenu(!MenuOpen);
            if (!IsVR && Cursor.lockState != CursorLockMode.Locked && !MenuOpen) SetMenu(true);
            if (!PlayingHand && NetworkActors.Alive(Player) && Player.transform.position.y < -6) RespawnPlayer();
        }
    }
}
