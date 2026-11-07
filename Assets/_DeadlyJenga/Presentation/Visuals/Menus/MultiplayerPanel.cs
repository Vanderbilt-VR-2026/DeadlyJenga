using UnityEngine;
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
        string seen;
        float expires;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen()
        {
            GameEvents.SessionStarted+=Attach;
            GameEvents.LobbyStarted+=Attach;
        }
        static void Attach(GameObject root)
        {if(root&&!root.GetComponent<RoomNoticeHud>())root.AddComponent<RoomNoticeHud>();}
        void LateUpdate()
        {
            var session=Services.Session;
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
            => string.IsNullOrEmpty(JoinCode(net))?"Exit to Lobby":net.CanToggleSharedControls?"Return Room to Lobby":"Visit Lobby";
        static readonly RoomLeaveConfirmation desktopLeave=new RoomLeaveConfirmation();
        public static void ClearLeaveConfirmation()=>desktopLeave.Clear();
        public static string EndAction(IMultiplayerControls net)
            =>string.IsNullOrEmpty(JoinCode(net))?"End Game":"End Game for Room";
        public static void Draw(IMultiplayerControls net,bool hostTools=true)
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
                    if (hostTools && net.CanToggleSharedControls && GUILayout.Button("Demo controls: " + (net.SharedControls ? "EVERYONE" : "HOST ONLY"), GUILayout.Height(32))) net.ToggleSharedControls();
                    if (hostTools && net.CanInviteNearby && GUILayout.Button("Invite everyone on this Wi-Fi", GUILayout.Height(32))) net.InviteNearby();
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
