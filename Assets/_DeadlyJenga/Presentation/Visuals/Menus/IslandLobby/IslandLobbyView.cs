using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using DeadlyJenga.Presentation;
using UnityEngine.UI;
using DeadlyJenga.Core;
using DeadlyJenga.CandyArt;

namespace DeadlyJenga.CastlePrototype
{
    // Draws the island lobby: ocean, sand and sky, the three terminals and the giant keyboard.
    // Attaches on GameEvents.LobbyStarted; everything it shows or does goes through Core's ILobbyControls.
    [DefaultExecutionOrder(210)]
    public sealed partial class IslandLobbyView : MonoBehaviour
    {
        partial void BuildComfortControls();
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
        sealed class Choice {public string id;public Button button;public BoxCollider collider;public Text label;public Action action;public Func<bool> enabled;public Func<string> title;public Func<bool> visible;public bool physical;public bool candy,selectionGlyph;public bool focusInitialized;public Outline focusOutline;public Shadow focusGlow;public Color faceColor,edgeColor;public Image rim;public Func<bool> selected;public Text description;public LobbyTerminalGlyph glyph;public Transform keyVisual;public Renderer keyRenderer;public MaterialPropertyBlock keyProperties;public float pressedUntil;public Color keyColor;public Image keyGlow,keyGlowSoft;public Graphic keyIcon;public Action<float> slide;public Func<bool> slideEnabled;}
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
            BuildComfortControls();
            BuildWardrobe();
            BuildMayhemMarina();
            BuildTVWall();
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
                lobbyAimDot=new GameObject("Lobby gaze cursor").AddComponent<TextMesh>();
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
        bool controlsMenuWasOpen,terminalRequireRelease=true;
        bool LocalModalBlocked=>DeadlyJenga.Presentation.MenuAim.InputConsumed||DeadlyJenga.Presentation.GameExperiencePanel.Showing(session)||DeadlyJenga.Presentation.CandyComfortPanel.Showing(session)||DeadlyJenga.Presentation.CandyWildlifePanel.Showing(session)||CharacterPicker.Showing(session);
        void Update()
        {
            bool menuOpened=session?.MenuOpen==true&&!controlsMenuWasOpen;
            controlsMenuWasOpen=session?.MenuOpen==true;
            if(menuOpened&&Player?.IsVR==true&&!wardrobeOpen&&!DeadlyJenga.Presentation.GameExperiencePanel.Showing(session)&&!DeadlyJenga.Presentation.CandyWildlifePanel.Showing(session))
                DeadlyJenga.Presentation.CandyComfortPanel.Open(session);
            if(LocalModalBlocked){terminalRequireRelease=true;wardrobeDragDevice=0;draggingKeyboard=false;if(lobbyAimDot)lobbyAimDot.gameObject.SetActive(false);return;}
            if(lobby==null||!NetworkActors.Alive(Player)||!Player.gameObject.activeInHierarchy)return;
            var keyboard=Keyboard.current;var mouse=Mouse.current;var eye=Player.ViewCamera;
            UpdateMayhemMarina();
            if(UpdateWardrobe()){if(lobbyAimDot)lobbyAimDot.gameObject.SetActive(false);return;}
            bool held=select.IsPressed()||Player.SelectHeld||!lobby.CodeFocused&&keyboard?.eKey.isPressed==true||mouse?.leftButton.isPressed==true;
            if(terminalRequireRelease){if(!held)terminalRequireRelease=false;return;}
            bool pressed=select.WasPressedThisFrame()||Player.SelectPressed||!lobby.CodeFocused&&keyboard!=null&&keyboard.eKey.wasPressedThisFrame||mouse!=null&&mouse.leftButton.wasPressedThisFrame;
            bool pointerHeld=select.IsPressed()||Player.SelectHeld||mouse?.leftButton.isPressed==true;
            int pointerDevice=0;
            Ray ray;
            if (Player.IsVR)
            {
                bool preferredLeft=Player.gameObject.GetComponent<IBlasterControls>()?.LeftHanded==true;
                ray=MenuAim.Resolve(eye,eye.transform.parent,preferredLeft,false,false,out _,out _);
                if (select.IsPressed())
                {
                    if (MenuAim.TryActionPointer(eye,select,out var controllerRay,out pointerDevice))
                    {
                        ray=controllerRay;
                        pressed=select.WasPressedThisFrame();
                    }
                    else
                    {
                        pressed=false;
                        pointerHeld=false;
                    }
                }
            }
            else
                ray=session.MenuOpen&&mouse!=null?eye.ScreenPointToRay(mouse.position.ReadValue()):new Ray(eye.transform.position,eye.transform.forward);
            Point(ray,pressed,pointerHeld,pointerDevice);
            UpdateStations();
        }
        readonly RoomLeaveConfirmation terminalLeave=new RoomLeaveConfirmation();
        bool ConfirmingTerminalLeave=>lobby.Connected&&!lobby.IsHost&&!lobby.Busy&&terminalLeave.Pending(lobby,lobby.HostCode,Time.unscaledTime);
        string TerminalLeaveLabel(string idle)=>ConfirmingTerminalLeave?
            "CONFIRM LEAVE · DISCONNECT FROM HOST":idle;
        void LeaveFromTerminal()
        {
            if(lobby.CanLeave&&terminalLeave.Select(lobby,lobby.HostCode,lobby.Connected&&!lobby.IsHost&&!lobby.Busy,Time.unscaledTime))
                lobby.Leave();
        }
        public bool InvokeChoice(string id)
        {
            if(LocalModalBlocked)return false;
            if(id=="leave"){if(!lobby.CanLeave)return false;LeaveFromTerminal();return true;}
            if(!DeadlyJenga.Presentation.GameExperiencePanel.CanEditRawChoices(session)&&(id.StartsWith("map-",StringComparison.Ordinal)||id.StartsWith("mode-",StringComparison.Ordinal)))return false;
            if(id.StartsWith("map-",StringComparison.Ordinal)&&lobby.SelectMap(id.Substring(4)))return true;
            if(id.StartsWith("mode-",StringComparison.Ordinal)&&lobby.SelectMode(id.Substring(5)))return true;
            var choice=choices.FirstOrDefault(c=>c.id==id);
            if(choice==null||choice.visible!=null&&!choice.visible()||choice.enabled!=null&&!choice.enabled())return false;
            choice.pressedUntil=Time.unscaledTime+.14f;
            DeadlyJenga.Presentation.MenuAim.Consume();choice.action();return true;
        }
        public void Point(Ray ray,bool pressed,bool held=false,int pointerDevice=0)
        {
            Choice nearest=null;float distance=4;MarinaInteractionRange(ref distance);Vector3 selectionPoint=default;
            if (marina && Physics.Raycast(ray, out var obstruction, distance, 1 << Layers.Surface, QueryTriggerInteraction.Ignore))
                distance = obstruction.distance;
            foreach(var choice in choices)
            {
                if(wardrobeOpen&&Player.IsVR&&choice.button&&!choice.button.transform.IsChildOf(wardrobePanel))continue;
                if(!choice.button||!choice.button.transform.parent.gameObject.activeInHierarchy)continue;
                bool shown=choice.visible==null||choice.visible();choice.button.gameObject.SetActive(shown);if(!shown)continue;
                bool rawChoice=choice.id.StartsWith("map-",StringComparison.Ordinal)||choice.id.StartsWith("mode-",StringComparison.Ordinal);
                bool usable=(choice.enabled==null||choice.enabled())&&(!rawChoice||DeadlyJenga.Presentation.GameExperiencePanel.CanEditRawChoices(session));choice.button.interactable=usable;choice.collider.enabled=usable;
                if(choice.title!=null)choice.label.text=choice.title();
                if(usable)
                {
                    if(choice.physical)
                    {
                        if(Vector3.Dot(ray.direction,choice.collider.transform.forward)>0&&choice.collider.Raycast(ray,out var hit,distance)&&hit.distance<distance){nearest=choice;distance=hit.distance;selectionPoint=hit.point;}
                    }
                    else if(DeadlyJenga.Presentation.MenuAim.TryPoint(ray,(RectTransform)choice.button.transform,distance,out _,out var point))
                    {
                        float depth=Vector3.Distance(ray.origin,point);
                        if(depth<distance){nearest=choice;distance=depth;selectionPoint=point;}
                    }
                }
                if(choice.physical)choice.button.image.color=Color.clear;
                if(choice.physical)UpdatePhysicalKey(choice,false);
            }
            foreach(var choice in choices)
                if(choice.button&&choice.button.gameObject.activeInHierarchy)PaintCandyChoice(choice,choice==nearest,choice.button.interactable);
            PreviewMapChoice(nearest?.id);
            UpdateWardrobeChoiceNotice(nearest?.id);
            if (DragRoomCodeKeyboard(ray, pressed, held, pointerDevice, distance, out float keyboardDepth))
            {
                DrawLobbyAim(ray, keyboardDepth, true);
                return;
            }
            if (DragWardrobePreview(ray,pressed,held,pointerDevice,distance,out float dragDepth))
            {
                DrawLobbyAim(ray,dragDepth,true);
                return;
            }
            if(nearest!=null)
            {
                if(nearest.physical)UpdatePhysicalKey(nearest,true);
                if(nearest.slide!=null&&(nearest.slideEnabled?.Invoke()??true)&&(pressed||held)){var rect=(RectTransform)nearest.button.transform;nearest.slide(Mathf.InverseLerp(-rect.rect.width*.5f,rect.rect.width*.5f,rect.InverseTransformPoint(selectionPoint).x));}
                else if(pressed)InvokeChoice(nearest.id);
            }
            DrawLobbyAim(ray,nearest!=null?distance:1.5f,nearest!=null);
        }
        void OnGUI()
        {
            if(lobby==null||!NetworkActors.Alive(Player)||!Player.gameObject.activeInHierarchy||Player.IsVR)return;
            if(LocalModalBlocked)return;
            if(session.MenuOpen&&GUI.Button(new Rect(490,15,330,60),"CONTROLS / LEFT OR RIGHT HAND"))DeadlyJenga.Presentation.CandyComfortPanel.Open(session);
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
