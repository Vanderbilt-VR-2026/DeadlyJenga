using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DeadlyJenga.Core;
using Fusion.Photon.Realtime;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public static class NearbyPlayerProfile
    {
        const string Key="DeadlyJenga.PlayerName.v1";
        public static string Clean(string value)
        {
            string normalized;try{normalized=(value??"").Normalize(NormalizationForm.FormC);}catch(ArgumentException){return "";}
            var result=new StringBuilder();
            foreach(char c in normalized)
            {
                if(result.Length>=24)break;
                if(char.IsLetterOrDigit(c)||c==' '||c=='-'||c=='_'||c=='\'')result.Append(c);
            }
            return result.ToString().Trim();
        }
        public static string Name
        {
            get
            {
                string name=Clean(PlayerPrefs.GetString(Key,""));
                if(name.Length>0)return name;
                name="Player "+Guid.NewGuid().ToString("N").Substring(0,4).ToUpperInvariant();
                PlayerPrefs.SetString(Key,name);PlayerPrefs.Save();return name;
            }
            set {string name=Clean(value);if(name.Length==0)return;PlayerPrefs.SetString(Key,name);PlayerPrefs.Save();}
        }
    }

    // Lives with the room connection, including while the host is playing a round.
    public sealed class NearbyRoomService : MonoBehaviour, INearbyRoomControls
    {
        [Serializable] sealed class Event {public string kind,value;}
        [Serializable] sealed class Room {public string room,name,protocol;public int players,capacity;}
        [Serializable] sealed class Request {public string id,name;public float seconds;}
        [Serializable] sealed class Requests {public Request[] items;}
        sealed class Seen {public NearbyRoomInfo info;public float at;}
        readonly Dictionary<string,Seen> rooms=new();
        NearbyRoomInfo[] roomRows=Array.Empty<NearbyRoomInfo>();
        NearbyJoinRequest[] requestRows=Array.Empty<NearbyJoinRequest>();
        IslandRoomConnection owner;
        string roomId=Guid.NewGuid().ToString("N"),roomCode="",lastConfiguration="";
        bool enabledNearby,waiting,permissionRequested,scanWhenReady;
        float nextPoll;
        public string PlayerName {get=>NearbyPlayerProfile.Name;set{NearbyPlayerProfile.Name=value;owner.RefreshNearbyPlayerName();}}
        public bool NearbyEnabled
        {
            get=>enabledNearby;
            set
            {
                enabledNearby=value;PlayerPrefs.SetInt("DeadlyJenga.NearbyBluetooth",value?1:0);PlayerPrefs.Save();lastConfiguration="";
                if(!value){waiting=false;scanWhenReady=false;requestRows=Array.Empty<NearbyJoinRequest>();rooms.Clear();roomRows=Array.Empty<NearbyRoomInfo>();JoinRequestStatus="";NearbyStatus="Bluetooth nearby is off. Room codes still work.";}
                else EnsurePermission();
            }
        }
        public bool CanAdvertise=>owner&&owner.Connected&&owner.IsHost&&!owner.Busy&&!owner.Migrating&&!owner.Departing&&!owner.SwitchingMap&&!owner.RoomSceneBusy&&owner.HumanCount<10;
        public string NearbyStatus {get;private set;}="Bluetooth nearby is off. Internet is still needed to join.";
        public string JoinRequestStatus {get;private set;}="";
        public IReadOnlyList<NearbyRoomInfo> NearbyRooms=>roomRows;
        public IReadOnlyList<NearbyJoinRequest> JoinRequests=>requestRows;
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject bridge;
#endif
        void Awake(){owner=GetComponent<IslandRoomConnection>();enabledNearby=PlayerPrefs.GetInt("DeadlyJenga.NearbyBluetooth",0)!=0;}
        void EnsurePermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using var version=new AndroidJavaClass("android.os.Build$VERSION");
            int sdk=version.GetStatic<int>("SDK_INT");
            string[] permissions=sdk>=31?new[]{"android.permission.BLUETOOTH_SCAN","android.permission.BLUETOOTH_ADVERTISE","android.permission.BLUETOOTH_CONNECT"}:new[]{"android.permission.ACCESS_FINE_LOCATION"};
            if(permissions.All(UnityEngine.Android.Permission.HasUserAuthorizedPermission))return;
            NearbyStatus="Allow Nearby devices to find and share rooms. You can still use room codes.";
            if(permissionRequested)return;permissionRequested=true;
            var callbacks=new UnityEngine.Android.PermissionCallbacks();
            callbacks.PermissionDenied+=_=>{permissionRequested=false;NearbyEnabled=false;NearbyStatus="Nearby devices permission denied. Enable it in app permissions, then try again.";};
            callbacks.PermissionGranted+=_=>{permissionRequested=!permissions.All(UnityEngine.Android.Permission.HasUserAuthorizedPermission);lastConfiguration="";};
            UnityEngine.Android.Permission.RequestUserPermissions(permissions,callbacks);
#else
            NearbyStatus="Bluetooth rooms require the Android APK. Room codes and Wi-Fi discovery still work here.";
#endif
        }
        bool Ready()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if(!enabledNearby)return false;
            using var version=new AndroidJavaClass("android.os.Build$VERSION");
            int sdk=version.GetStatic<int>("SDK_INT");
            if(sdk>=31&&new[]{"android.permission.BLUETOOTH_SCAN","android.permission.BLUETOOTH_ADVERTISE","android.permission.BLUETOOTH_CONNECT"}.Any(p=>!UnityEngine.Android.Permission.HasUserAuthorizedPermission(p)))return false;
            if(sdk<31&&!UnityEngine.Android.Permission.HasUserAuthorizedPermission("android.permission.ACCESS_FINE_LOCATION"))return false;
            if(bridge==null){using var unity=new AndroidJavaClass("com.unity3d.player.UnityPlayer");using var activity=unity.GetStatic<AndroidJavaObject>("currentActivity");bridge=new AndroidJavaObject("com.deadlyjenga.nearby.NearbyBluetooth",activity);}
            return true;
#else
            return false;
#endif
        }
        public void RefreshNearby()
        {
            if(!enabledNearby)NearbyEnabled=true;
            scanWhenReady=true;
            EnsurePermission();
            if(!Ready())return;
#if UNITY_ANDROID && !UNITY_EDITOR
            Configure();ScanReady();
#endif
        }
        void ScanReady()
        {
            scanWhenReady=false;rooms.Clear();roomRows=Array.Empty<NearbyRoomInfo>();
#if UNITY_ANDROID && !UNITY_EDITOR
            bridge.Call("scan");
#endif
        }
        public void RequestJoin(string id)
        {
            if(!Ready()||waiting||owner.Connected||owner.Busy){JoinRequestStatus="Leave your current room before requesting another.";return;}
            if(!rooms.TryGetValue(id,out var entry)||Time.unscaledTime-entry.at>45){JoinRequestStatus="That room expired. Refresh the list.";return;}
            if(entry.info.Players>=entry.info.Capacity){JoinRequestStatus="That room is full.";return;}
            waiting=true;JoinRequestStatus="Contacting "+entry.info.HostName+"…";
#if UNITY_ANDROID && !UNITY_EDITOR
            bridge.Call("request",id,PlayerName,Guid.NewGuid().ToString("N"));
#endif
        }
        public void CancelJoinRequest()
        {
            waiting=false;JoinRequestStatus="Request cancelled.";
#if UNITY_ANDROID && !UNITY_EDITOR
            bridge?.Call("cancel");
#endif
        }
        public void RespondToJoin(string id,bool accept)
        {
            if(!requestRows.Any(r=>r.Id==id))return;
#if UNITY_ANDROID && !UNITY_EDITOR
            Configure();bridge?.Call("respond",id,accept&&CanAdvertise);
#endif
        }
        void Configure()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if(bridge==null)return;
            string address=owner.Connected?owner.Region.ToUpperInvariant()+"-"+owner.Code:"";
            if(address!=roomCode){roomCode=address;roomId=Guid.NewGuid().ToString("N");requestRows=Array.Empty<NearbyJoinRequest>();}
            string configuration=enabledNearby+"|"+CanAdvertise+"|"+roomId+"|"+PlayerName+"|"+owner.HumanCount;
            if(configuration==lastConfiguration)return;lastConfiguration=configuration;
            bridge.Call("configure",enabledNearby,CanAdvertise,roomId,PlayerName,owner.BluetoothProtocol,address,owner.HumanCount);
#endif
        }
        void Update()
        {
            if(!owner||!owner.Catalog||Time.unscaledTime<nextPoll)return;
            nextPoll=Time.unscaledTime+.2f;
            try
            {
                if(enabledNearby&&!Ready()){if(!permissionRequested)EnsurePermission();return;}
                Configure();
#if UNITY_ANDROID && !UNITY_EDITOR
                if(bridge==null)return;
                if(scanWhenReady&&enabledNearby)ScanReady();
                for(int i=0;i<32;i++){string raw=bridge.Call<string>("poll");if(string.IsNullOrEmpty(raw))break;Handle(raw);}
                var pending=JsonUtility.FromJson<Requests>(bridge.Call<string>("requests"));
                requestRows=CanAdvertise?(pending?.items??Array.Empty<Request>()).Take(10).Select(r=>new NearbyJoinRequest(r.id,NearbyPlayerProfile.Clean(r.name),r.seconds)).ToArray():Array.Empty<NearbyJoinRequest>();
#endif
                foreach(string key in rooms.Where(p=>Time.unscaledTime-p.Value.at>45).Select(p=>p.Key).ToArray())rooms.Remove(key);
                roomRows=rooms.Values.Select(r=>r.info).OrderBy(r=>r.HostName,StringComparer.Ordinal).ThenBy(r=>r.Id,StringComparer.Ordinal).ToArray();
            }
            catch(Exception e){NearbyStatus="Bluetooth unavailable: "+e.GetType().Name+". Room codes still work.";enabledNearby=false;waiting=false;}
        }
        void Handle(string raw)
        {
            var message=JsonUtility.FromJson<Event>(raw);
            if(message.kind=="status"){if(enabledNearby)NearbyStatus=message.value;}
            else if(message.kind=="join"){JoinRequestStatus=message.value;if(!message.value.StartsWith("Waiting",StringComparison.Ordinal))waiting=false;}
            else if(message.kind=="room")
            {
                var r=JsonUtility.FromJson<Room>(message.value);
                if(r.protocol!=owner.BluetoothProtocol||string.IsNullOrEmpty(r.room)||r.room.Length!=32||rooms.Count>=16||r.capacity!=10||r.players<0||r.players>10)return;
                rooms[r.room]=new Seen{info=new NearbyRoomInfo(r.room,NearbyPlayerProfile.Clean(r.name),r.players,r.capacity),at=Time.unscaledTime};
            }
            else if(message.kind=="approved"&&waiting)
            {
                waiting=false;
                if(owner.Connected||owner.Busy||!IslandRoomAddress.TryParse(message.value,owner.Region,out _,out _)){JoinRequestStatus="Room changed. Request again when ready.";return;}
                JoinRequestStatus="Approved. Joining the room…";
                var home=FindFirstObjectByType<HomeSession>();
                if(home){home.Code=message.value;home.JoinCode();}else _=owner.Join(message.value);
            }
        }
        void OnApplicationPause(bool paused)
        {
            if(!paused){lastConfiguration="";return;}
            waiting=false;requestRows=Array.Empty<NearbyJoinRequest>();
#if UNITY_ANDROID && !UNITY_EDITOR
            bridge?.Call("close");bridge?.Dispose();bridge=null;
#endif
        }
        void OnDestroy()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            bridge?.Call("close");bridge?.Dispose();bridge=null;
#endif
        }
    }
    public sealed partial class IslandRoomConnection
    {
        internal string BluetoothProtocol=>RoomProtocol(PhotonAppSettings.Global.AppSettings.AppVersion)+"/ble-approval-1";
        internal void RefreshNearbyPlayerName(){LocalIdentity.displayName=NearbyPlayerProfile.Name;Act(new LobbyAction{verb="name",value=NearbyPlayerProfile.Name});}
    }
}
