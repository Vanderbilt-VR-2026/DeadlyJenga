using UnityEngine;
using UnityEngine.UI;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    // Desktop menu section for multiplayer. Shows state and sends choices through IMultiplayerControls (Core);
    // the networking code in GameSession draws nothing itself.
    public sealed class RoomLeaveConfirmation
    {
        object owner;
        string room;
        float opened;
        public bool Pending(object session,string code,float now)
            =>ReferenceEquals(owner,session)&&owner!=null&&room==code&&now>=opened&&now-opened<8f;
        public bool Select(object session,string code,bool confirm,float now)
        {
            if(!confirm){Clear();return true;}
            if(!Pending(session,code,now)){owner=session;room=code;opened=now;return false;}
            if(now-opened<.25f)return false;
            Clear();return true;
        }
        public void Clear(){owner=null;room=null;}
    }

    // The existing multiplayer status contract carries host-authored room notices.
    public sealed class RoomNoticeHud:MonoBehaviour
    {
        TextMesh label;
        ISessionInfo owner;
        RoomConnectionTile roomTile;
        string currentCode="";
        bool expanded;
        GUIStyle roomButton,roomPanel,roomCaption,roomCode;
        string seen;
        float expires;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen()
        {
            GameEvents.SessionStarted+=Attach;
            GameEvents.LobbyStarted+=Attach;
        }
        static void Attach(GameObject root)
        {
            if(!root)return;
            var hud=root.GetComponent<RoomNoticeHud>();
            if(!hud)hud=root.AddComponent<RoomNoticeHud>();
            hud.owner=root.GetComponent<ISessionInfo>();
        }
        void LateUpdate()
        {
            var session=Services.Session;
            bool current=session!=null&&ReferenceEquals(session,owner);
            string nextCode=current?MultiplayerPanel.JoinCode(session.Multiplayer):"";
            if(nextCode!=currentCode||!CanShowCode(session))expanded=false;
            currentCode=nextCode;
            var view=current?session.ActiveCamera:null;
            bool showCode=current&&CanShowCode(session)&&session.IsVR&&view&&currentCode.Length>0;
            if(showCode&&!roomTile)
            {
                var canvasObject=new GameObject("Visual - current room connection",typeof(RectTransform),typeof(Canvas));
                canvasObject.transform.SetParent(transform,false);
                var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
                canvas.sortingOrder=90;
                roomTile=RoomConnectionTile.Create(canvasObject.transform);
                roomTile.SetIndicator();
            }
            if(roomTile)
            {
                var canvas=roomTile.transform.parent;
                canvas.gameObject.SetActive(showCode);
                roomTile.Refresh(current?session.Multiplayer:null);
                if(showCode)
                {
                    canvas.SetPositionAndRotation(view.transform.TransformPoint(new Vector3(-.39f,.31f,1.5f)),view.transform.rotation);
                    canvas.localScale=Vector3.one*.00125f;
                }
            }
            if(!current){if(label)label.gameObject.SetActive(false);return;}
            string status=session?.Multiplayer?.Status;
            if(status!=seen)
            {
                seen=status;
                if(status!=null&&(status.EndsWith(" joined the room.",System.StringComparison.Ordinal)
                    ||status.EndsWith(" left the room.",System.StringComparison.Ordinal)))
                {
                    if(!label)
                    {
                        var notice=new GameObject("Visual - room notice");notice.transform.SetParent(transform,false);
                        notice.layer=5;
                        label=notice.AddComponent<TextMesh>();label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                        var renderer=label.GetComponent<Renderer>();renderer.sharedMaterial=label.font.material;
                        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
                        label.anchor=TextAnchor.MiddleCenter;
                        label.alignment=TextAlignment.Center;label.fontSize=48;label.characterSize=.021f;
                        label.richText=false;label.color=CandyHudVisuals.Mint;
                    }
                    label.text=status;expires=Time.unscaledTime+5f;
                }
            }
            if(!label)return;
            var camera=session?.ActiveCamera;
            bool visible=camera&&Time.unscaledTime<expires;
            label.gameObject.SetActive(visible);
            if(visible)label.transform.SetPositionAndRotation(camera.ViewportToWorldPoint(new Vector3(.5f,.84f,1.6f)),camera.transform.rotation);
        }
        bool CanShowCode(ISessionInfo session)
        {
            return session!=null&&ReferenceEquals(session,owner)&&!session.MenuOpen
                &&!(session is ILobbyControls)&&(session as ILobbyMatchVisitControls)?.VisitingLobby!=true
                &&MatchRuntime.Current?.Frame.phase==RoundPhase.Playing
                &&!CharacterPicker.Showing(session)&&!DeadlyJenga.Presentation.CandyComfortPanel.Showing(session)
                &&!DeadlyJenga.Presentation.GameExperiencePanel.Showing(session)
                &&!DeadlyJenga.Presentation.CandyWildlifePanel.Showing(session);
        }
        void CacheRoomStyles()
        {
            if(roomButton!=null)return;
            roomButton=new GUIStyle(GUI.skin.button){fontSize=18,alignment=TextAnchor.MiddleCenter,richText=false,
                border=new RectOffset(12,12,12,12)};
            roomButton.normal.background=roomButton.hover.background=roomButton.active.background=CandyHudVisuals.Pill.texture;
            roomButton.normal.textColor=CandyHudVisuals.Mint;roomButton.hover.textColor=Color.white;
            roomButton.active.textColor=CandyHudVisuals.Lavender;
            roomPanel=new GUIStyle(roomButton);roomPanel.normal.textColor=Color.white;
            roomCaption=new GUIStyle(GUI.skin.label){fontSize=16,alignment=TextAnchor.MiddleCenter,richText=false};
            roomCaption.normal.textColor=CandyHudVisuals.Lavender;
            roomCode=new GUIStyle(roomCaption){fontSize=26};roomCode.normal.textColor=CandyHudVisuals.Mint;
        }
        void OnGUI()
        {
            var session=Services.Session;
            if(!CanShowCode(session)||session.IsVR){expanded=false;return;}
            string code=MultiplayerPanel.JoinCode(session.Multiplayer);
            if(code.Length==0){expanded=false;return;}
            CacheRoomStyles();
            string role=session.Multiplayer.IsClient?"GUEST":"HOST";
            float x=Mathf.Max(8,Screen.width-(expanded?268:184));
            var previous=GUI.backgroundColor;GUI.backgroundColor=CandyHudVisuals.Card;
            if(!expanded)
            {
                if(GUI.Button(new Rect(x,8,176,34),role+" · "+code,roomButton))expanded=true;
            }
            else
            {
                GUI.Box(new Rect(x,8,260,118),"",roomPanel);
                GUI.Label(new Rect(x+8,12,212,25),"CONNECTIONS · "+role,roomCaption);
                if(GUI.Button(new Rect(x+224,12,28,25),"×",roomButton))expanded=false;
                GUI.Label(new Rect(x+8,39,244,36),code,roomCode);
                if(GUI.Button(new Rect(x+16,82,228,32),"Copy current join code",roomButton))GUIUtility.systemCopyBuffer=code;
            }
            GUI.backgroundColor=previous;
        }
    }

    // Reusable read-only Connections tile for a tablet or world-space canvas.
    public sealed class RoomConnectionTile:MonoBehaviour
    {
        Text title,code;
        bool compact,indicator;
        public static RoomConnectionTile Create(Transform parent)
        {
            var root=new GameObject("Connections - current room",typeof(RectTransform),typeof(Image),typeof(RoomConnectionTile));
            root.transform.SetParent(parent,false);
            var rect=root.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(300,80);
            var background=root.GetComponent<Image>();background.sprite=CandyHudVisuals.Pill;
            background.type=Image.Type.Sliced;background.color=CandyHudVisuals.Card;background.raycastTarget=false;
            var tile=root.GetComponent<RoomConnectionTile>();
            tile.title=tile.Label("Connection role",new Vector2(0,23),18,CandyHudVisuals.Lavender);
            tile.code=tile.Label("Current join code",new Vector2(0,-10),28,CandyHudVisuals.Mint);
            return tile;
        }
        Text Label(string name,Vector2 position,int size,Color color)
        {
            var child=new GameObject(name,typeof(RectTransform),typeof(Text));child.transform.SetParent(transform,false);
            var text=child.GetComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize=size;text.color=color;text.alignment=TextAnchor.MiddleCenter;text.supportRichText=false;text.raycastTarget=false;
            text.rectTransform.sizeDelta=new Vector2(284,34);text.rectTransform.anchoredPosition=position;
            return text;
        }
        public void SetCompact(Vector2 position)
        {
            compact=true;
            var rect=(RectTransform)transform;rect.sizeDelta=new Vector2(570,35);rect.anchoredPosition=position;
            title.gameObject.SetActive(false);code.rectTransform.anchoredPosition=Vector2.zero;
            code.rectTransform.sizeDelta=new Vector2(554,35);code.fontSize=23;
        }
        public void SetIndicator()
        {
            indicator=true;
            var rect=(RectTransform)transform;rect.sizeDelta=new Vector2(210,36);
            title.gameObject.SetActive(false);code.rectTransform.anchoredPosition=Vector2.zero;
            code.rectTransform.sizeDelta=new Vector2(198,34);code.fontSize=23;
        }
        public void Refresh(IMultiplayerControls net)
        {
            string liveCode=MultiplayerPanel.JoinCode(net);
            title.text=liveCode.Length==0?"CONNECTIONS · OFFLINE":net.IsClient?"CONNECTIONS · GUEST":"CONNECTIONS · HOST";
            code.text=liveCode.Length==0?"No active room":indicator
                ?(net.IsClient?"GUEST":"HOST")+" · "+liveCode:compact
                ?"CONNECTIONS · "+(net.IsClient?"GUEST":"HOST")+" · CODE  "+liveCode
                :"JOIN CODE  "+liveCode;
        }
    }

    public static class MultiplayerPanel
    {
        public static string JoinCode(IMultiplayerControls net)
        {
            // RoomInfo is "Room: <address> · <population>" in the session UI contract.
            var info=net?.RoomInfo;
            if(string.IsNullOrEmpty(info)||!info.StartsWith("Room: ",System.StringComparison.Ordinal))return "";
            int end=info.IndexOf(' ',6);
            return end<0?info.Substring(6):info.Substring(6,end-6);
        }
        public static string LobbyAction(IMultiplayerControls net)
            => string.IsNullOrEmpty(JoinCode(net))?"Exit to Lobby":net.IsClient?"Visit Lobby":"Return Room to Lobby";
        static readonly RoomLeaveConfirmation desktopLeave=new RoomLeaveConfirmation();
        public static void ClearLeaveConfirmation()=>desktopLeave.Clear();
        public static string EndAction(IMultiplayerControls net)
            =>string.IsNullOrEmpty(JoinCode(net))?"End Game":"End Game for Room";
        public static void Draw(IMultiplayerControls net)
        {
            if (net == null) return;
            if (net.IsFusion)
            {
                GUILayout.Label(net.Status);
                if (net.RoomInfo.Length > 0)
                {
                    GUILayout.Label(net.RoomInfo);
                    string code=JoinCode(net);
                    if(code.Length>0&&GUILayout.Button("Copy join code",GUILayout.Height(30)))GUIUtility.systemCopyBuffer=code;
                    if (net.CanToggleSharedControls && GUILayout.Button("Demo controls: " + (net.SharedControls ? "EVERYONE" : "HOST ONLY"), GUILayout.Height(32))) net.ToggleSharedControls();
                    if (net.CanInviteNearby && GUILayout.Button("Invite everyone on this Wi-Fi", GUILayout.Height(32))) net.InviteNearby();
                    if (net.InvitationFrom.Length > 0)
                    {
                        GUILayout.Label("Nearby invitation from " + net.InvitationFrom + ". Leave this room to accept it on the island.");
                        if (GUILayout.Button("Dismiss invitation", GUILayout.Height(32))) net.DismissInvitation();
                    }
                    GUILayout.Label(net.DiscoveryStatus);
                }
                string room=JoinCode(net);
                float now=Time.unscaledTime;
                bool confirm=net.IsClient;
                bool pending=confirm&&desktopLeave.Pending(net,room,now);
                if(pending)GUILayout.Label("Leaving disconnects you from the host. The room will stay open.");
                if(net.CanLeave&&GUILayout.Button(pending?"Confirm leave room":net.IsClient?"Leave room":(room.Length>0?"Leave / transfer host":"Close game for everyone"),GUILayout.Height(32))
                    &&desktopLeave.Select(net,room,confirm,now))net.LeaveSession();
                if(pending&&GUILayout.Button("Cancel",GUILayout.Height(30)))desktopLeave.Clear();
                return;
            }
            GUILayout.Space(8); GUILayout.Label("LOCAL MULTIPLAYER \u00b7 same Wi-Fi"); GUILayout.Label(net.Status);
            if (!net.IsClient)
            {
                if (GUILayout.Button("Nearby: " + net.SelectedName + " (next)", GUILayout.Height(30))) net.NextNearby();
                if (GUILayout.Button("Request to join selected demo", GUILayout.Height(30))) net.RequestSelected();
                if (GUILayout.Button("Invite selected demo here", GUILayout.Height(30))) net.InviteSelected();
                GUILayout.Label(net.PendingLabel);
                if (net.HasPending)
                {
                    if (GUILayout.Button("Accept", GUILayout.Height(30))) net.AcceptPending();
                    if (GUILayout.Button("Decline", GUILayout.Height(30))) net.DeclinePending();
                }
            }
            string localRoom=JoinCode(net);float localNow=Time.unscaledTime;
            bool localPending=net.IsClient&&desktopLeave.Pending(net,localRoom,localNow);
            if(localPending)GUILayout.Label("Leaving disconnects you from the host. The room will stay open.");
            if(net.CanLeave&&GUILayout.Button(localPending?"Confirm leave room":net.IsClient?"Leave local session":"End / cancel local session",GUILayout.Height(30))
                &&desktopLeave.Select(net,localRoom,net.IsClient,localNow))net.LeaveSession();
            if(localPending&&GUILayout.Button("Cancel",GUILayout.Height(30)))desktopLeave.Clear();
        }
    }
}
