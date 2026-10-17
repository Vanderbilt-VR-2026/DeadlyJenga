using FlyingHand;
using Fusion;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    [DefaultExecutionOrder(200)]
    public sealed class CastlePlaySession : MonoBehaviour, ISessionInfo
    {
        public CastleGenerator generator;
        public CastleSurvivor survivorPrefab;
        public CastleHandActor handPrefab;
        public Camera overviewCamera;
        public bool tuneJumpToBlockHeight=true;
        public float jumpShortfall=.18f;
        public CastleSurvivor Player { get; private set; }
        public CastleHandActor Hand { get; private set; }
        public bool PlayingHand { get; private set; }
        public bool MenuOpen { get; private set; }
        public bool IsVR => PlayingHand ? Hand.motor.ActiveControlMode == ControlMode.VR : Player.IsVR;
        public Camera ActiveCamera => PlayingHand ? Hand.View : Player.viewCamera;
        public Vector3 LastSpawnFeet { get; private set; }
        public DeadlyJenga.CastleBots.CastleBotPopulation Bots {get;private set;}
        public DeadlyJenga.CastleBots.CastleHandBotPopulation HandBots {get;private set;}
        public CastleLanSession Network {get;private set;}
        InputAction switchRole, toggleMenu, select;
        CastleWorldMenu worldMenu;
        bool wasVR, focused = true;
        Vector2 menuScroll;

        void Start()
        {
            Services.Session = this;
            GameEvents.Eliminated += OnEliminated;
            if (overviewCamera) overviewCamera.gameObject.SetActive(false);
            Player = Instantiate(survivorPrefab); Player.name = "Survivor (local)"; Player.gameObject.AddComponent<CandyStaminaHud>();
            TuneJump();
            Hand = Instantiate(handPrefab); Hand.name = "Flying Hand (local)";
            Player.viewCamera.clearFlags = CameraClearFlags.Skybox;
            Hand.View.clearFlags = CameraClearFlags.Skybox;
            Hand.motor.captureDesktopCursor = false;
            switchRole = new InputAction("Switch role", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
            toggleMenu = new InputAction("Demo menu", InputActionType.Button, "<XRController>{LeftHand}/secondaryButton");
            select = new InputAction("Menu select", InputActionType.Button, "<XRController>{RightHand}/trigger");
            switchRole.Enable(); toggleMenu.Enable(); select.Enable();
            worldMenu = new GameObject("Headset demo menu").AddComponent<CastleWorldMenu>(); worldMenu.Build(this);
            SetRole(false); SetMenu(false);
            Bots=gameObject.AddComponent<DeadlyJenga.CastleBots.CastleBotPopulation>();
            HandBots=gameObject.AddComponent<DeadlyJenga.CastleBots.CastleHandBotPopulation>();
            var botWorld=new DeadlyJenga.CastleBots.BotWorld{Player=Player,Hand=Hand,survivorPrefab=survivorPrefab,handPrefab=handPrefab,Bots=Bots,HandBots=HandBots,isPlayingHand=()=>PlayingHand};
            Bots.Initialize(botWorld);HandBots.Initialize(botWorld);
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
            gameObject.AddComponent<DeadlyJenga.CastleSocial.SocialWorld>().Initialize(this);
            if(!GetComponent<CandyDemoHud>())gameObject.AddComponent<CandyDemoHud>();
        }
        void OnDestroy()
        {
            if (ReferenceEquals(Services.Session, this)) Services.Session = null;
            GameEvents.Eliminated -= OnEliminated;
            switchRole?.Dispose(); toggleMenu?.Dispose(); select?.Dispose();
            if (worldMenu) Destroy(worldMenu.gameObject);
            if (Player) Destroy(Player.gameObject);
            if (Hand) Destroy(Hand.gameObject);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
        void OnApplicationFocus(bool value) { focused = value; if (!value && Player && Hand) SetMenu(true); }
        public void SetRole(bool hand)
        {
            PlayingHand = hand;
            Player.gameObject.SetActive(!hand); Hand.gameObject.SetActive(hand);
            if (hand)
            {
                float extent = CastleLayout.Footprint(generator.settings) * .5f;
                Hand.Place(generator.transform.position + new Vector3(0, generator.settings.cornerCourses * generator.settings.blockSize.y + 3, -extent - 10), Quaternion.identity);
            }
            else RespawnPlayer();
            SetMenu(MenuOpen);
        }
        // The ocean (or any hazard) eliminated someone. In this demo the local survivor just respawns.
        void OnEliminated(GameObject who, GameObject source)
        {
            if (Player && who == Player.gameObject && !PlayingHand) RespawnPlayer();
        }

        public void RespawnPlayer()
        {
            Vector3 feet = Services.World.SafeSpawnPoint();
            Vector3 heading = Vector3.ProjectOnPlane(generator.transform.position - feet, Vector3.up);
            LastSpawnFeet = feet;
            Player.Teleport(feet, heading.sqrMagnitude > .01f ? Quaternion.LookRotation(heading) : Quaternion.identity);
            Player.Revive();
        }
        void TuneJump()
        {
            if(!tuneJumpToBlockHeight||!Player)return;
            float margin=Mathf.Max(jumpShortfall,Player.Capsule.radius+Player.Capsule.stepOffset+Player.Capsule.skinWidth+.02f);
            Player.jumpHeight=Mathf.Max(.1f,generator.settings.blockSize.y-margin);
        }
        public void ResetCastle() { if(Network&&!Network.CanEditWorld)return;generator.Rebuild(); TuneJump();SetRole(PlayingHand);if(HandBots)HandBots.CastleRebuilt();if(Bots)Bots.CastleRebuilt(); }
        public void SetMenu(bool open)
        {
            MenuOpen = open;
            Player.InputSuspended = open || !focused || (Network&&Network.WaitingForWorld);
            Hand.motor.InputSuspended = open || !focused || (Network&&Network.WaitingForWorld);
            Cursor.lockState = !open && !IsVR && focused ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = open || IsVR;
            if (worldMenu) worldMenu.Show(open && IsVR, ActiveCamera);
        }
        void Update()
        {
            if (!Player || !Hand) return;
            var k = Keyboard.current;
            var candyTracking=(PlayingHand?Hand.gameObject:Player.gameObject).GetComponent<DeadlyJenga.CandyPlayers.CandyTrackingSource>();
            if (IsVR != wasVR) { wasVR = IsVR; SetMenu(MenuOpen); }
            if (!MenuOpen&&((IsVR && switchRole.WasPressedThisFrame()) || (!IsVR && k != null && k.tabKey.wasPressedThisFrame))) SetRole(!PlayingHand);
            if ((IsVR && toggleMenu.WasPressedThisFrame()) || (!IsVR && k != null && k.escapeKey.wasPressedThisFrame)) SetMenu(!MenuOpen);
            if(IsVR&&candyTracking&&candyTracking.LeftMenuPressed)SetMenu(!MenuOpen);
            if (!IsVR && Cursor.lockState != CursorLockMode.Locked && !MenuOpen) SetMenu(true);
            if (MenuOpen && IsVR) worldMenu.Point(ActiveCamera, select.WasPressedThisFrame()||(candyTracking&&candyTracking.RightPinchPressed));
        }
        void OnGUI()
        {
            if(GetComponent<CandyDemoHud>())return;
            if (!Player || !Hand || IsVR) return;
            if (!MenuOpen)
            {
                GUI.Box(new Rect(14, 14, 520, 74), PlayingHand ? "FLYING HAND | WASD fly · Q/E down/up · Mouse steer\nTab: become survivor · Esc: menu"
                    : "SURVIVOR | Space: jump/vault · Hold near wall: scramble\nWASD: move / climb · Release Space: drop · Grip "+Mathf.CeilToInt(Player.GripRemaining*100)+"%\n" + Player.ActionStatus);
                GUI.Label(new Rect(Screen.width / 2f - 5, Screen.height / 2f - 12, 20, 24), "+");
                return;
            }
            GUILayout.BeginArea(new Rect(20, 20, 410, Mathf.Min(650,Screen.height-40)), GUI.skin.box);
            menuScroll=GUILayout.BeginScrollView(menuScroll);
            GUILayout.Label("CASTLE PLAY DEMO — " + (PlayingHand ? "FLYING HAND" : "SURVIVOR"));
            GUILayout.Label("Keyboard / mouse detected. Headset switches automatically.");
            if (GUILayout.Button("Resume controls", GUILayout.Height(32))) SetMenu(false);
            if (GUILayout.Button(PlayingHand ? "Become survivor (spawn on top)" : "Become flying hand", GUILayout.Height(32))) { SetRole(!PlayingHand); SetMenu(false); }
            if (GUILayout.Button("Respawn on top", GUILayout.Height(32))) { SetRole(false); SetMenu(false); }
            bool editWorld=!Network||Network.CanEditWorld;
            if (editWorld&&GUILayout.Button("Rebuild castle", GUILayout.Height(32))) { ResetCastle(); SetMenu(false); }
            if (Network) Network.DrawMenu();
            GUILayout.Label("Space / right A: jump; press again near a ledge to vault. Hold near a steep block to scramble; movement climbs/shimmies. Release to drop. Grip recovers on secure ground.\nVR hands: hold right pinch; left pinch climbs up. Tab / right B: switch role. Left Y: VR menu.");
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
