using System;
using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    // What the island's terminals show and do (Core's ILobbyControls). Presentation draws them.
    public sealed partial class IslandLobby : ILobbyControls
    {
        string enteredCode="";
        bool showingNearby,codeFocused;
        int nearbyPage;
        float scanningUntil;

        public MonoBehaviour EnvironmentPrefab=>catalog?catalog.environmentPrefab:null;
        public ScriptableObject Art=>catalog?catalog.art:null;
        GameObject flatFloor;
        // A flat floor at sand height, so the island stays walkable without Presentation.
        void BuildFlatFloor()
        {
            flatFloor=new GameObject("Island floor (gray box)");flatFloor.transform.SetParent(transform,false);
            var box=flatFloor.AddComponent<BoxCollider>();box.center=new Vector3(0,TopHeight-.5f,0);box.size=new Vector3(Radius*2,1,Radius*2);
        }
        public void SetFloor(Mesh mesh,Transform pose)
        {
            if(!mesh||!pose)return;
            var floor=Collider("Island floor (sand shape)",pose);floor.AddComponent<MeshCollider>().sharedMesh=mesh;
            if(flatFloor)Destroy(flatFloor);
        }
        public void AddBlocker(Transform pose,Vector3 center,Vector3 size)
        {if(!pose)return;var box=Collider("Island blocker · "+pose.name,pose).AddComponent<BoxCollider>();box.center=center;box.size=size;}
        GameObject Collider(string name,Transform pose)
        {
            var go=new GameObject(name);go.layer=pose.gameObject.layer;   // world-space copy of the pose; unloads with the scene
            go.transform.SetPositionAndRotation(pose.position,pose.rotation);go.transform.localScale=pose.lossyScale;return go;
        }

        // Join terminal
        public bool JoinEnabled=>Room&&!Room.HostSelected&&!Room.Connected&&!Room.Busy;
        public bool JoinStationEnabled=>JoinEnabled;
        public bool SetupStationEnabled=>Room&&Room.CanConfigure;
        public bool ShowingNearby=>showingNearby;
        public bool CodeFocused=>codeFocused&&JoinEnabled;
        public string EnteredCode{get=>enteredCode;set=>enteredCode=value??"";}
        public bool CodeValid=>IslandRoomAddress.TryParse(enteredCode,Room.Region,out _,out _);
        public void FocusCode(){if(!JoinEnabled)return;showingNearby=false;codeFocused=true;if(!Player.IsVR)SetMenu(true);}
        public void UnfocusCode()=>codeFocused=false;
        public void TypeCode(char value)
        {if(!JoinEnabled)return;value=char.ToUpperInvariant(value);if((value>='A'&&value<='Z'||value>='0'&&value<='9'||value=='-')&&enteredCode.Length<12)enteredCode+=value;}
        public void EraseCode(){if(enteredCode.Length>0)enteredCode=enteredCode.Substring(0,enteredCode.Length-1);}
        public void Join(){if(JoinEnabled&&CodeValid)_=Room.Join(enteredCode);}
        public void DetectNearby(){Room.StartDiscovery();showingNearby=true;codeFocused=false;nearbyPage=0;scanningUntil=Time.unscaledTime+2;}
        public void ShowCodeEntry(){showingNearby=false;FocusCode();}
        IslandDiscovery.Entry NearbyEntry(int slot)
        {
            var list=Room.Discovery?.Nearby;if(list==null||list.Count==0)return null;
            int pages=(list.Count+2)/3,index=(nearbyPage%pages)*3+slot;return index<list.Count?list[index]:null;
        }
        public bool HasNearby(int slot)=>NearbyEntry(slot)!=null;
        public string NearbyTitle(int slot){var entry=NearbyEntry(slot);return entry==null?"":entry.message.name+" · "+entry.message.count+" players\nJOIN "+entry.message.code;}
        public void JoinNearby(int slot){var entry=NearbyEntry(slot);if(entry==null||!JoinEnabled)return;enteredCode=entry.message.code;_=Room.Join(enteredCode);}
        public bool HasMoreNearby=>(Room.Discovery?.Nearby.Count??0)>3;
        public void NextNearbyPage()=>nearbyPage++;
        public string InvitationFrom=>Room.Discovery?.Invitation?.message.name;
        public void AcceptInvitation()=>Room.AcceptInvite();
        public bool CanLeave=>Room.Connected||Room.Busy;
        public bool Busy=>Room.Busy;
        public void Leave(){if(CanLeave)_=Room.Leave();}
        public string JoinHint{get;private set;}=""; 

        // Host terminal
        public bool HostActive=>Room.HostSelected||Room.IsHost;
        public bool CanHost=>!Room.Busy&&!Room.Connected;
        public void Host(){if(CanHost)_=Room.Host();}
        public string HostCode=>Room.Busy?"CREATING ISLAND…":Room.Code;
        public string Region=>Room.Region;
        public void NextRegion(){if(CanHost)Room.Region=IslandRoomAddress.Regions[(Array.IndexOf(IslandRoomAddress.Regions,Room.Region)+1)%IslandRoomAddress.Regions.Length];}
        public bool CanInvite=>Room.IsHost&&!Room.Busy;
        public void InviteNearby()=>Room.InviteNearby();
        public bool DemoSelected=>catalog.modes[Room.ModeIndex].id=="demo";
        public bool SharedControls=>Room.SharedDemoControls;
        public void ToggleSharedControls(){if(CanInvite&&DemoSelected)Room.SetSharedControls(!Room.SharedDemoControls);}
        public string HostHint{get;private set;}="";

        // Map + mode terminal
        public bool IsHost=>Room.IsHost;
        public bool Connected=>Room.Connected;
        public bool CanConfigure=>Room.CanConfigure;
        public string MapTitle=>catalog.maps[Room.MapIndex].title;
        public string ModeTitle=>catalog.modes[Room.ModeIndex].title;
        public void CycleMap(int step)=>Room.SelectMap((Room.MapIndex+step+catalog.maps.Length)%catalog.maps.Length);
        public void CycleMode(int step)=>Room.SelectMode((Room.ModeIndex+step+catalog.modes.Length)%catalog.modes.Length);
        public bool SelectMap(string id){int i=Array.FindIndex(catalog.maps,m=>m.id==id);if(i<0||!Room.CanConfigure)return false;Room.SelectMap(i);return true;}
        public bool SelectMode(string id){int i=Array.FindIndex(catalog.modes,m=>m.id==id);if(i<0||!Room.CanConfigure)return false;Room.SelectMode(i);return true;}
        public bool CanStart=>Room.CanLaunch||Room.LaunchPending&&Room.IsHost;
        public bool StartPending=>Room.LaunchPending;
        public void StartOrCancel(){if(!CanStart)return;if(Room.LaunchPending)Room.CancelLaunch();else Room.Launch();}
        public string SetupHint{get;private set;}="";

        void UpdateControls()
        {
            if(!JoinEnabled)codeFocused=false;
            HostHint=HostActive?(Room.Busy?"You can cancel while connecting.":"Your room · "+Room.HumanCount+" / 10 players\nInvite friends, then choose a map and mode."):Room.Connected?"You are a guest on the host's island.":"Create an island for your friends.\nJoining switches off while you host.";
            SetupHint=Room.LaunchPending?"Waiting for players to confirm…":!catalog.modes[Room.ModeIndex].available?"This mode is coming later.":Room.IsHost?"Ready when you are.":Room.Connected?"The host chooses. Everyone sees the same setup.":"Become host to choose.";
            JoinHint=HostActive?"JOIN IS OFF WHILE YOU HOST":Room.Busy?"Connecting… cancel above if needed.":Room.Connected?"Joined "+Room.Code:showingNearby?(Room.Discovery==null?Room.DiscoveryStatus:Time.unscaledTime<scanningUntil?"Detecting nearby islands…":(Room.Discovery.Nearby.Count==0?"No rooms yet. Same Wi-Fi + Internet required.\nYou can still enter a code.":"Tap a room to join.")):"Type, point, or play the giant keyboard.\nFull codes look like US-ABC234.";
            if(!HostActive&&!Room.Busy&&!Room.Connected&&Room.Status.StartsWith("Connection failed",StringComparison.Ordinal))JoinHint=HostHint=Room.Status;
            else if(JoinEnabled&&!showingNearby&&enteredCode.Length>=6&&!CodeValid)JoinHint="Check the code. Example: US-ABC234.\nUse the host's region prefix, or six room characters.";
        }
    }
}
