using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using DeadlyJenga.Core;
using DeadlyJenga.CandyArt;
using DeadlyJenga.CandyPlayers;
using P=DeadlyJenga.CastlePrototype.CastleLanProtocol;

namespace DeadlyJenga.CastlePrototype
{
    [DefaultExecutionOrder(200)]
    public sealed partial class IslandLobby : MonoBehaviour, ISessionInfo, IWorldQuery
    {
        public IslandLobbyCatalog catalog;
        public static IslandLobby Current {get;private set;}
        public CastleSurvivor Player {get;private set;}
        public CandyEnvironment Environment {get;private set;}
        public IslandRoomConnection Room {get;private set;}
        public bool MenuOpen {get;private set;}
        public string EnteredCode="";
        public IReadOnlyList<Rigidbody> Blocks=>Array.Empty<Rigidbody>();
        public Vector3 Center=>Vector3.zero;
        public float TopHeight=>.45f;
        public float Radius=>9;
        public Bounds PlayableBounds=>new Bounds(Center+Vector3.up*2,new Vector3(18,4,18));
        public int PeerCount=>peers.Count;
        public DeadlyJenga.CastleSocial.SocialWorld Voice {get;private set;}
        public float WaterHeight(Vector3 p)=>-.12f;
        public bool IsSheltered(Vector3 p)=>false;
        public Vector3 SafeSpawnPoint()=>new Vector3(0,.5f,-3.5f);
        sealed class Peer {public CandyPlayerRig rig;public CandyAvatarFrame target,frame;}
        sealed class Choice {public string id;public Button button;public BoxCollider collider;public Text label;public Action action;public Func<bool> enabled;public Func<string> title;public Func<bool> visible;public bool physical;public Transform keyVisual;public Renderer keyRenderer;public MaterialPropertyBlock keyProperties;public float pressedUntil;public Color keyColor;}
        readonly List<Choice> choices=new List<Choice>();
        readonly Dictionary<string,Peer> peers=new Dictionary<string,Peer>();
        CandyArtProfile localArt;CandyPlayerAvatar avatar;InputAction select,menu;bool focused=true;
        public void Awake()
        {
            Current=this;Services.Session=this;Services.World=this;
            if(!catalog||!catalog.survivorPrefab||!catalog.environmentPrefab){Debug.LogError("Island lobby catalog is incomplete.",this);enabled=false;return;}
            Room=IslandRoomConnection.Ensure();bool firstVisit=!Room.Catalog;Room.Catalog=catalog;Room.StartDiscovery();if(firstVisit)Room.Region=catalog.defaultRegion;
            BuildIsland();Player=Instantiate(catalog.survivorPrefab);Player.name="Lobby survivor (current character prefab)";
            Player.viewCamera.clearFlags=CameraClearFlags.Skybox;
            Player.Teleport(SafeSpawnPoint(),Quaternion.identity);avatar=Player.GetComponent<CandyPlayerAvatar>();
            Voice=gameObject.AddComponent<DeadlyJenga.CastleSocial.SocialWorld>();Voice.Initialize(this);
            select=new InputAction("Lobby select",InputActionType.Button,"<XRController>{RightHand}/trigger");select.Enable();
            menu=new InputAction("Lobby controls",InputActionType.Button,"<XRController>{LeftHand}/secondaryButton");menu.Enable();
            BuildTerminals();SetMenu(false);
        }
        void BuildIsland()
        {
            localArt=Instantiate(catalog.art?catalog.art:catalog.environmentPrefab.profile);localArt.name="Lobby art (runtime copy)";
            localArt.islandRadius=8;localArt.fitIslandToFoundation=false;localArt.alignSandToFoundation=false;
            localArt.sandHeight=.45f;localArt.sandRelief=.28f;localArt.sandMargin=.65f;localArt.sandShoreWidth=2.1f;localArt.terrainResolution=96;
            Environment=Instantiate(catalog.environmentPrefab);Environment.name="Island · castle ocean, sand and sky";Environment.profile=localArt;Environment.foundation=null;Environment.applyLighting=true;
            var sun=new GameObject("Island sunlight").AddComponent<Light>();sun.type=LightType.Directional;Environment.sun=sun;
            Environment.Refresh();var sand=Environment.sandRenderer;
            var mesh=sand.GetComponent<MeshFilter>().sharedMesh;var collider=sand.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=mesh;
        }
        void Update()
        {
            if(!Player)return;
            if(Player.transform.position.y<-.9f||(Player.transform.position-Center).sqrMagnitude>900){Player.Teleport(SafeSpawnPoint(),Quaternion.identity);Player.Revive();}
            var keyboard=Keyboard.current;var mouse=Mouse.current;var tracking=Player.GetComponent<CandyTrackingSource>();
            if(menu.WasPressedThisFrame()||keyboard!=null&&keyboard.escapeKey.wasPressedThisFrame||tracking&&tracking.LeftMenuPressed)SetMenu(!MenuOpen);
            if(!Player.IsVR&&!MenuOpen&&Cursor.lockState!=CursorLockMode.Locked&&!Application.isBatchMode)SetMenu(true);
            bool pressed=select.WasPressedThisFrame()||tracking&&tracking.RightPinchPressed||keyboard!=null&&keyboard.eKey.wasPressedThisFrame||mouse!=null&&mouse.leftButton.wasPressedThisFrame;
            Ray ray=!Player.IsVR&&MenuOpen&&mouse!=null?Player.viewCamera.ScreenPointToRay(mouse.position.ReadValue()):new Ray(Player.viewCamera.transform.position,Player.viewCamera.transform.forward);
            Point(ray,pressed);
            foreach(var peer in peers.Values)
            {
                float t=1-Mathf.Exp(-20*Time.unscaledDeltaTime);var f=peer.target;
                peer.frame.feet=Vector3.Lerp(peer.frame.feet,f.feet,t);peer.frame.head=Vector3.Lerp(peer.frame.head,f.head,t);peer.frame.headRotation=Quaternion.Slerp(peer.frame.headRotation,f.headRotation,t);peer.frame.scale=f.scale;
                peer.frame.left=f.left;peer.frame.right=f.right;peer.rig.ApplyFrame(peer.frame,true,Time.unscaledDeltaTime);
            }
            UpdateStations(tracking);

        }
        public static Vector3 SpawnPoint(int slot)
        {float angle=(Math.Abs(slot)%10)*2.399963f;return new Vector3(Mathf.Sin(angle)*1.8f,.65f,-3.2f+Mathf.Cos(angle)*.8f);}
        public void SpawnAt(P.Pose pose){Player.Teleport(pose.position,pose.rotation);Player.Revive();ClearPeers();}
        public void SetMenu(bool open)
        {MenuOpen=open;if(Player){Player.InputSuspended=!focused||open;Cursor.lockState=Player.IsVR||open?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=open&&!Player.IsVR;}}
        void OnApplicationFocus(bool value){focused=value;if(!value)SetMenu(true);}
        public bool InvokeChoice(string id)
        {
            if(id=="leave") {if(!Room.Connected&&!Room.Busy)return false;_=Room.Leave();return true;}
            if(id.StartsWith("map-",StringComparison.Ordinal)&&catalog.maps.Any(m=>"map-"+m.id==id))
            {if(!Room.CanConfigure)return false;Room.SelectMap(Array.FindIndex(catalog.maps,m=>"map-"+m.id==id));return true;}
            if(id.StartsWith("mode-",StringComparison.Ordinal)&&catalog.modes.Any(m=>"mode-"+m.id==id))
            {if(!Room.CanConfigure)return false;Room.SelectMode(Array.FindIndex(catalog.modes,m=>"mode-"+m.id==id));return true;}
            var choice=choices.FirstOrDefault(c=>c.id==id);
            if(choice==null||choice.visible!=null&&!choice.visible()||choice.enabled!=null&&!choice.enabled())return false;
            if(choice.physical)choice.pressedUntil=Time.unscaledTime+.14f;
            choice.action();return true;
        }
        public void Point(Ray ray,bool pressed)
        {
            Choice nearest=null;float distance=4;
            foreach(var choice in choices)
            {
                bool shown=choice.visible==null||choice.visible();choice.button.gameObject.SetActive(shown);if(!shown)continue;
                bool usable=choice.enabled==null||choice.enabled();choice.button.interactable=usable;choice.collider.enabled=usable;
                if(choice.title!=null)choice.label.text=choice.title();
                choice.label.color=usable?(choice.physical?CandyHudVisuals.Ink:Color.white):CandyHudVisuals.Muted;
                if(usable&&Vector3.Dot(ray.direction,choice.collider.transform.forward)>0&&choice.collider.Raycast(ray,out var hit,4)&&hit.distance<distance){nearest=choice;distance=hit.distance;}
                choice.button.image.color=choice.physical?Color.clear:usable?CandyHudVisuals.Card:new Color(.11f,.14f,.2f);
                if(choice.physical)UpdatePhysicalKey(choice,false);
            }
            if(nearest!=null)
            {
                if(nearest.physical)UpdatePhysicalKey(nearest,true);else nearest.button.image.color=CandyHudVisuals.Mint;
                nearest.label.color=CandyHudVisuals.Ink;if(pressed)InvokeChoice(nearest.id);
            }
        }
        public P.Actor Capture(string id)=>new P.Actor{id=id,color=avatar.ColorIndex,avatar=avatar.ReadFrame(),active=true};
        public bool TryPeerPosition(string id,out Vector3 position)
        {if(Voice&&id==Voice.LocalParticipantId){position=Player.viewCamera.transform.position;return true;}if(id!=null&&peers.TryGetValue(id,out var peer)){position=peer.target.head;return true;}position=default;return false;}
        public void ReceivePeer(P.Actor actor)
        {
            if(actor.hand||actor.id==Room.Runner?.LocalPlayer.ToString())return;
            if(!peers.TryGetValue(actor.id,out var peer))
            {if(peers.Count>=9||!avatar.visualPrefab)return;var rig=Instantiate(avatar.visualPrefab);rig.name="Lobby guest · "+actor.id;rig.ownerCamera=null;peers[actor.id]=peer=new Peer{rig=rig,frame=actor.avatar};}
            peer.target=actor.avatar;peer.rig.SetColor(actor.color);
        }
        public void ReceivePeers(List<P.Actor> actors,string local)
        {foreach(var id in peers.Keys.ToArray())if(!actors.Any(a=>a.id==id))RemovePeer(id);foreach(var actor in actors)if(actor.id!=local)ReceivePeer(actor);}
        public void RemovePeer(string id){if(peers.TryGetValue(id,out var p)){if(p.rig)Destroy(p.rig.gameObject);peers.Remove(id);}}
        public void ClearPeers(){foreach(var id in peers.Keys.ToArray())RemovePeer(id);}
        void OnGUI()
        {
            if(!Player||Player.IsVR)return;
            GUI.Box(new Rect(15,15,460,codeFocused?95:70),"ISLAND LOBBY · WASD move · mouse look\nE / click selects · Esc releases mouse"+(codeFocused?"\nType your code · Enter joins · Backspace erases":""));

        }
        void OnDestroy()
        {
            if(codeKeyboard!=null)codeKeyboard.onTextInput-=CodeText;
            if(keyboardMaterial)Destroy(keyboardMaterial);if(keyboardMesh)Destroy(keyboardMesh);
            select?.Dispose();menu?.Dispose();ClearPeers();if(Player)Destroy(Player.gameObject);if(Environment)Destroy(Environment.gameObject);if(localArt)Destroy(localArt);
            if(Current==this)Current=null;if(ReferenceEquals(Services.Session,this))Services.Session=null;if(ReferenceEquals(Services.World,this))Services.World=null;
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
    }
}
