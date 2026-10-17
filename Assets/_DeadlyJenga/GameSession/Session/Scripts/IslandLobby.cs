using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
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
        public IslandLobbyCatalog catalog;
        public static IslandLobby Current {get;private set;}
        public IPlayableCharacter Player {get;private set;}
        public IslandRoomConnection Room {get;private set;}
        public bool MenuOpen {get;private set;}
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
        // Other players on the island, shown by Characters' remote views (Core's INetworkActorView).
        readonly Dictionary<string,INetworkActorView> peers=new Dictionary<string,INetworkActorView>();
        InputAction menu;bool focused=true;
        public void Awake()
        {
            Current=this;Services.Session=this;Services.World=this;
            if(!catalog||!catalog.survivorPrefab||!catalog.environmentPrefab){Debug.LogError("Island lobby catalog is incomplete.",this);enabled=false;return;}
            Room=IslandRoomConnection.Ensure();bool firstVisit=!Room.Catalog;Room.Catalog=catalog;Room.StartDiscovery();if(firstVisit)Room.Region=catalog.defaultRegion;
            Player=Instantiate(catalog.survivorPrefab).GetComponent<IPlayableCharacter>();
            if(Player==null){Debug.LogError("Island lobby survivor prefab needs an IPlayableCharacter.",this);enabled=false;return;}
            Player.gameObject.name="Lobby survivor (current character prefab)";
            Player.ViewCamera.clearFlags=CameraClearFlags.Skybox;
            BuildFlatFloor();
            GameEvents.RaiseLobbyStarted(gameObject);   // Presentation builds the island and terminals
            Player.Place(SafeSpawnPoint(),Quaternion.identity);
            Voice=gameObject.AddComponent<DeadlyJenga.CastleSocial.SocialWorld>();Voice.Initialize(this);
            menu=new InputAction("Lobby controls",InputActionType.Button,"<XRController>{LeftHand}/secondaryButton");menu.Enable();
            SetMenu(false);
        }
        void Update()
        {
            if(!NetworkActors.Alive(Player))return;
            var feet=Player.transform.position;
            if(feet.y<-.9f||(feet-Center).sqrMagnitude>900)Player.Place(SafeSpawnPoint(),Quaternion.identity);
            var keyboard=Keyboard.current;
            if(menu.WasPressedThisFrame()||keyboard!=null&&keyboard.escapeKey.wasPressedThisFrame||Player.MenuPressed)SetMenu(!MenuOpen);
            if(!Player.IsVR&&!MenuOpen&&Cursor.lockState!=CursorLockMode.Locked&&!Application.isBatchMode)SetMenu(true);
            foreach(var peer in peers.Values)peer.Render(Time.unscaledDeltaTime);
            UpdateControls();
        }
        public static Vector3 SpawnPoint(int slot)
        {float angle=(Math.Abs(slot)%10)*2.399963f;return new Vector3(Mathf.Sin(angle)*1.8f,.65f,-3.2f+Mathf.Cos(angle)*.8f);}
        public void SpawnAt(P.Pose pose){Player.Place(pose.position,pose.rotation);ClearPeers();}
        public void SetMenu(bool open)
        {MenuOpen=open;if(NetworkActors.Alive(Player)){Player.InputSuspended=!focused||open;Cursor.lockState=Player.IsVR||open?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=open&&!Player.IsVR;}}
        void OnApplicationFocus(bool value){focused=value;if(!value)SetMenu(true);}
        public P.Actor Capture(string id)=>P.Capture(id,Player.gameObject.GetComponent<INetworkActor>());
        public bool TryPeerPosition(string id,out Vector3 position)
        {if(Voice&&id==Voice.LocalParticipantId){position=Player.ViewCamera.transform.position;return true;}if(id!=null&&peers.TryGetValue(id,out var peer)){position=peer.NetworkPosition+Vector3.up*1.6f;return true;}position=default;return false;}
        public void ReceivePeer(P.Actor actor)
        {
            if(actor.kind!=NetworkKinds.Survivor||actor.id==Room.Runner?.LocalPlayer.ToString())return;
            if(!peers.TryGetValue(actor.id,out var peer))
            {
                if(peers.Count>=9)return;
                var root=new GameObject("Lobby guest · "+actor.id);
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
            menu?.Dispose();ClearPeers();if(NetworkActors.Alive(Player))Destroy(Player.gameObject);
            if(Current==this)Current=null;if(ReferenceEquals(Services.Session,this))Services.Session=null;if(ReferenceEquals(Services.World,this))Services.World=null;
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
    }
}
