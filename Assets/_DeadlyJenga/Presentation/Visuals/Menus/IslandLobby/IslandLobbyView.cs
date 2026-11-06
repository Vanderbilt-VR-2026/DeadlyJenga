using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using DeadlyJenga.Core;
using DeadlyJenga.CandyArt;
using DeadlyJenga.Presentation;

namespace DeadlyJenga.CastlePrototype
{
    // Draws the island lobby: ocean, sand and sky, the three terminals and the giant keyboard.
    // Attaches on GameEvents.LobbyStarted; everything it shows or does goes through Core's ILobbyControls.
    [DefaultExecutionOrder(210)]
    public sealed partial class IslandLobbyView : MonoBehaviour
    {
        partial void BuildMayhemMarina();
        partial void UpdateMayhemMarina();
        partial void DisposeMayhemMarina();
        partial void MarinaInteractionRange(ref float distance);
        ILobbyControls lobby;
        ISessionInfo session;
        IPlayableCharacter Player=>session.Player;
        public CandyEnvironment Environment {get;private set;}
        CandyArtProfile localArt;
        InputAction select,wardrobeCancel;
        sealed class Choice {public string id;public Button button;public BoxCollider collider;public Text label;public Action action;public Func<bool> enabled;public Func<string> title;public Func<bool> visible;public bool physical;public bool candy,selectionGlyph;public Color faceColor,edgeColor;public Image rim;public Func<bool> selected;public Text description;public LobbyTerminalGlyph glyph;public Transform keyVisual;public Renderer keyRenderer;public MaterialPropertyBlock keyProperties;public float pressedUntil;public Color keyColor;public Image keyGlow,keyGlowSoft;public Graphic keyIcon;public Action<float> slide;public Func<bool> slideEnabled;}
        readonly List<Choice> choices=new List<Choice>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen()=>GameEvents.LobbyStarted+=Attach;
        static void Attach(GameObject lobbyObject)
        {
            var controls=lobbyObject.GetComponent<ILobbyControls>();var info=lobbyObject.GetComponent<ISessionInfo>();
            if(controls==null||info==null||!NetworkActors.Alive(info.Player))return;
            var view=lobbyObject.GetComponent<IslandLobbyView>();if(!view)view=lobbyObject.AddComponent<IslandLobbyView>();
            view.Build(info,controls);
        }
        void Build(ISessionInfo info,ILobbyControls controls)
        {
            if(session!=null)return;
            session=info;lobby=controls;
            GameEvents.Emoted+=LobbyResidenceChanged;
            BuildIsland();
            select=new InputAction("Lobby select",InputActionType.Button,"<XRController>{RightHand}/trigger");select.AddBinding("<XRController>{LeftHand}/trigger");select.Enable();
            wardrobeCancel=new InputAction("Leave wardrobe",InputActionType.Button,"<XRController>{RightHand}/secondaryButton");
            wardrobeCancel.AddBinding("<XRController>{LeftHand}/secondaryButton");wardrobeCancel.Enable();
            BuildTerminals();
            BuildCandyDesign();
            BuildWardrobe();
            BuildTVWall();
            BuildMayhemMarina();
        }
        void BuildIsland()
        {
            var prefab=lobby.EnvironmentPrefab as CandyEnvironment;if(!prefab){Debug.LogError("Island lobby environment prefab needs a CandyEnvironment.",this);return;}
            var art=lobby.Art as CandyArtProfile;
            localArt=Instantiate(art?art:prefab.profile);localArt.name="Lobby art (runtime copy)";
            localArt.islandRadius=8;localArt.fitIslandToFoundation=false;localArt.alignSandToFoundation=false;
            localArt.sandHeight=.45f;localArt.sandRelief=.28f;localArt.sandMargin=.65f;localArt.sandShoreWidth=2.1f;localArt.terrainResolution=96;
            Environment=Instantiate(prefab);Environment.name="Island  /  castle ocean, sand and sky";Environment.profile=localArt;Environment.foundation=null;Environment.applyLighting=(lobby as ILobbyMatchVisitControls)?.VisitingLobby!=true;
            lobbySun=new GameObject("Island sunlight").AddComponent<Light>();lobbySun.type=LightType.Directional;lobbySun.enabled=Environment.applyLighting;Environment.sun=lobbySun;
            if((lobby as ILobbyMatchVisitControls)?.VisitingLobby==true)
            {
                foreach(var component in Environment.GetComponents<MonoBehaviour>())component.enabled=false;
                Environment.profile=localArt;Environment.foundation=null;
                var baseline=Environment.GetComponent<CandyArtBaseline>();if(baseline)baseline.map=null;
                var terrain=Environment.sandRenderer.GetComponent<CandySandbar>();if(terrain)terrain.map=null;
            }
            Environment.Refresh();var sand=Environment.sandRenderer;
            lobby.SetFloor(sand.GetComponent<MeshFilter>().sharedMesh,sand.transform);   // the lobby makes the floor collider
        }
        TextMesh lobbyAimDot;
        void DrawLobbyAim(Ray ray,float distance,bool hit)
        {
            if(!Player.IsVR){if(lobbyAimDot)lobbyAimDot.gameObject.SetActive(false);return;}
            if(!lobbyAimDot)
            {
                lobbyAimDot=new GameObject("Lobby controller cursor").AddComponent<TextMesh>();
                lobbyAimDot.transform.SetParent(transform,false);
                lobbyAimDot.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                lobbyAimDot.GetComponent<Renderer>().sharedMaterial=lobbyAimDot.font.material;
                lobbyAimDot.anchor=TextAnchor.MiddleCenter;
                lobbyAimDot.fontSize=48;
            }
            lobbyAimDot.gameObject.SetActive(true);
            lobbyAimDot.text=hit?"●":"·";
            lobbyAimDot.color=hit?CandyHudVisuals.Mint:Color.white;
            lobbyAimDot.characterSize=.009f*distance;
            lobbyAimDot.transform.SetPositionAndRotation(ray.GetPoint(distance-.015f),Quaternion.LookRotation(ray.direction));
        }
        bool controlsMenuWasOpen;
        void Update()
        {
            bool menuOpened=session?.MenuOpen==true&&!controlsMenuWasOpen;
            controlsMenuWasOpen=session?.MenuOpen==true;
            if(menuOpened&&Player?.IsVR==true&&!wardrobeOpen&&!DeadlyJenga.Presentation.GameExperiencePanel.Showing(session)&&!DeadlyJenga.Presentation.CandyWildlifePanel.Showing(session))
                DeadlyJenga.Presentation.CandyComfortPanel.Open(session);
            if(DeadlyJenga.Presentation.GameExperiencePanel.Showing(session)||DeadlyJenga.Presentation.CandyComfortPanel.Showing(session)||DeadlyJenga.Presentation.CandyWildlifePanel.Showing(session))
            { if(lobbyAimDot)lobbyAimDot.gameObject.SetActive(false); if(piano)piano.gameObject.SetActive(false); lobby?.UnfocusCode(); keyboardWasOpen=false; return; }
            if(lobby==null||!NetworkActors.Alive(Player)||!Player.gameObject.activeInHierarchy)return;
            var keyboard=Keyboard.current;var mouse=Mouse.current;var eye=Player.ViewCamera;
            if(!eye)return;
            UpdateMayhemMarina();
            if(UpdateWardrobe()){if(lobbyAimDot)lobbyAimDot.gameObject.SetActive(false);return;}
            UpdatePrivateKeyboard();
            if(Player.IsVR)
            {
                if(MenuPointer.AimAt(eye, Player.gameObject.GetComponent<IBlasterControls>()?.LeftHanded==true, LobbyPanelHit, out var input))
                    Point(input.Ray,input.Pressed,input.Held);
                else Point(default,false);
            }
            else
            {
                bool pressed=keyboard!=null&&keyboard.eKey.wasPressedThisFrame||mouse!=null&&mouse.leftButton.wasPressedThisFrame;
                Ray ray=session.MenuOpen&&mouse!=null?eye.ScreenPointToRay(mouse.position.ReadValue()):new Ray(eye.transform.position,eye.transform.forward);
                Point(ray,pressed,mouse!=null&&mouse.leftButton.isPressed);
            }
            UpdateStations();
        }
        readonly RoomLeaveConfirmation terminalLeave=new RoomLeaveConfirmation();
        bool ConfirmingTerminalLeave=>lobby.Connected&&!lobby.IsHost&&!lobby.Busy&&terminalLeave.Pending(lobby,lobby.HostCode,Time.unscaledTime);
        string TerminalLeaveLabel(string idle)=>ConfirmingTerminalLeave?
            "Confirm leave":idle;
        void LeaveFromTerminal()
        {
            if(lobby.CanLeave&&terminalLeave.Select(lobby,lobby.HostCode,lobby.Connected&&!lobby.IsHost&&!lobby.Busy,Time.unscaledTime))
                lobby.Leave();
        }
        public bool InvokeChoice(string id)
        {
            if(DeadlyJenga.Presentation.GameExperiencePanel.Showing(session))return false;
            if(id=="leave"){if(!RoomHasGuests||!lobby.CanLeave)return false;LeaveFromTerminal();return true;}
            if(!DeadlyJenga.Presentation.GameExperiencePanel.CanEditRawChoices(session)&&(id.StartsWith("map-",StringComparison.Ordinal)||id.StartsWith("mode-",StringComparison.Ordinal)))return false;
            if(id.StartsWith("map-",StringComparison.Ordinal)&&lobby.SelectMap(id.Substring(4)))return true;
            if(id.StartsWith("mode-",StringComparison.Ordinal)&&lobby.SelectMode(id.Substring(5)))return true;
            var choice=choices.FirstOrDefault(c=>c.id==id);
            if(choice==null||choice.visible!=null&&!choice.visible()||choice.enabled!=null&&!choice.enabled())return false;
            choice.pressedUntil=Time.unscaledTime+.14f;
            choice.action();return true;
        }
        float LobbyPanelHit(Ray ray)
        {
            if(lobby.CodeFocused)return MenuPointer.PanelHit(ray,piano);
            float range=MenuPointer.PanelRange;
            MarinaInteractionRange(ref range);
            if(wardrobeOpen)return MenuPointer.PanelHit(ray,wardrobePanel,range);
            float room=MenuPointer.PanelHit(ray,roomPage,range);
            return room>0?room:MenuPointer.PanelHit(ray,setupStation,range);
        }

        public void Point(Ray ray,bool pressed,bool held=false)
        {
            if(terminalPageChangedFrame==Time.frameCount)pressed=held=false;
            Choice nearest=null;float distance=4;MarinaInteractionRange(ref distance);Vector3 selectionPoint=default;
            foreach(var choice in choices)
            {
                if(wardrobeOpen&&Player.IsVR&&choice.button&&!choice.button.transform.IsChildOf(wardrobePanel))continue;
                if(!choice.button||!choice.button.transform.parent.gameObject.activeInHierarchy)continue;
                if(lobby.CodeFocused && piano && !choice.button.transform.IsChildOf(piano))continue;
                bool shown=choice.visible==null||choice.visible();choice.button.gameObject.SetActive(shown);if(!shown)continue;
                bool rawChoice=choice.id.StartsWith("map-",StringComparison.Ordinal)||choice.id.StartsWith("mode-",StringComparison.Ordinal);
                bool usable=(choice.enabled==null||choice.enabled())&&(!rawChoice||DeadlyJenga.Presentation.GameExperiencePanel.CanEditRawChoices(session));choice.button.interactable=usable;choice.collider.enabled=usable;
                if(choice.title!=null)choice.label.text=choice.title();
                PaintCandyChoice(choice,false,usable);
                if(usable&&Vector3.Dot(ray.direction,choice.collider.transform.forward)>0&&choice.collider.Raycast(ray,out var hit,distance)&&hit.distance<distance){nearest=choice;distance=hit.distance;selectionPoint=hit.point;}
                if(choice.physical)choice.button.image.color=Color.clear;
                if(choice.physical)UpdatePhysicalKey(choice,false);
            }
            UpdateWardrobeChoiceNotice(nearest?.id);
            if(nearest!=null)
            {
                if(nearest.physical)UpdatePhysicalKey(nearest,true);
                PaintCandyChoice(nearest,true,true);
                if(nearest.slide!=null&&(nearest.slideEnabled?.Invoke()??true)&&(pressed||held)){var rect=(RectTransform)nearest.button.transform;nearest.slide(Mathf.InverseLerp(-rect.rect.width*.5f,rect.rect.width*.5f,rect.InverseTransformPoint(selectionPoint).x));}
                else if(pressed)InvokeChoice(nearest.id);
            }
            if(nearest!=null)DrawLobbyAim(ray,distance,true);
            else if(lobbyAimDot)lobbyAimDot.gameObject.SetActive(false);
        }
        void OnGUI()
        {
            if(lobby==null||!NetworkActors.Alive(Player)||!Player.gameObject.activeInHierarchy||Player.IsVR)return;
            if(DeadlyJenga.Presentation.CandyComfortPanel.Showing(session))return;
            if(session.MenuOpen&&GUI.Button(new Rect(490,15,330,60),"Controls & comfort"))DeadlyJenga.Presentation.CandyComfortPanel.Open(session);
            bool typing=lobby.CodeFocused;
            GUI.Box(new Rect(15,15,460,typing?95:70),"ISLAND LOBBY  /  WASD move  /  mouse look\nE / click selects  /  Esc releases mouse"+(typing?"\nType your code  /  Enter joins  /  Backspace erases":""));
        }
        void OnDestroy()
        {
            GameEvents.Emoted-=LobbyResidenceChanged;
            if(lobbySun)Destroy(lobbySun.gameObject);
            DisposeMayhemMarina();
            DisposeWardrobe();
            DisposeCandyDesign();
            DisposeControllerArtwork();
            if(codeKeyboard!=null)codeKeyboard.onTextInput-=CodeText;
            if(keyboardMaterial)Destroy(keyboardMaterial);if(keyboardMesh)Destroy(keyboardMesh);if(cabinetMesh)Destroy(cabinetMesh);if(consoleSphere)Destroy(consoleSphere);
            select?.Dispose();wardrobeCancel?.Dispose();if(Environment)Destroy(Environment.gameObject);if(localArt)Destroy(localArt);
        }
    }
}
