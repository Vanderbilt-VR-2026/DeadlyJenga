using System;
using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    // What the island's terminals show and do (Core's ILobbyControls). Presentation draws them.
    public sealed partial class IslandLobby : ILobbyControls
    {
        string enteredCode="";
        bool showingNearby,codeFocused,codeOwnsMenu;
        int nearbyPage;
        float scanningUntil;

        public MonoBehaviour EnvironmentPrefab=>catalog?catalog.environmentPrefab:null;
        public ScriptableObject Art=>catalog?catalog.art:null;
        readonly System.Collections.Generic.List<GameObject> layoutCollision=new();
        partial void MayhemMarinaFloor(GameObject floor,Mesh mesh,Transform pose);
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
            foreach(var previous in layoutCollision)if(previous){previous.SetActive(false);Destroy(previous);}
            layoutCollision.Clear();
            var floor=Collider("Island floor (sand shape)",pose);floor.AddComponent<MeshCollider>().sharedMesh=mesh;MayhemMarinaFloor(floor,mesh,pose);
            if(flatFloor){flatFloor.SetActive(false);Destroy(flatFloor);}
        }
        public void AddBlocker(Transform pose,Vector3 center,Vector3 size)
        {if(!pose)return;var box=Collider("Island blocker · "+pose.name,pose).AddComponent<BoxCollider>();box.center=center;box.size=size;}
        GameObject Collider(string name,Transform pose)
        {
            var go=new GameObject(name);go.layer=pose.gameObject.layer;   // world-space copy of the pose; unloads with the scene
            go.transform.SetPositionAndRotation(pose.position,pose.rotation);go.transform.localScale=pose.lossyScale;go.transform.SetParent(transform,true);layoutCollision.Add(go);return go;
        }

        // Join terminal
        public bool JoinEnabled=>!VisitingLobby&&Room&&!Room.Busy&&!Room.LaunchPending;
        public bool JoinStationEnabled=>JoinEnabled;
        public bool SetupStationEnabled=>VisitingLobby||Room&&Room.CanConfigure;
        public bool ShowingNearby=>showingNearby;
        public bool CodeFocused=>codeFocused&&JoinEnabled;
        public string EnteredCode{get=>enteredCode;set=>enteredCode=value??"";}
        public bool CodeValid=>IslandRoomAddress.TryParse(enteredCode,Room.Region,out _,out _);
        public void FocusCode()
        {
            if (!JoinEnabled || codeFocused) return;
            showingNearby = false;
            codeFocused = true;
            codeOwnsMenu = !Player.IsVR && !MenuOpen;
            if (codeOwnsMenu) SetMenu(true);
        }
        public void UnfocusCode()
        {
            codeFocused = false;
            bool closeMenu = codeOwnsMenu;
            codeOwnsMenu = false;
            if (closeMenu && MenuOpen) SetMenu(false);
        }
        public void TypeCode(char value)
        {if(!JoinEnabled)return;value=char.ToUpperInvariant(value);if((value>='A'&&value<='Z'||value>='0'&&value<='9'||value=='-')&&enteredCode.Length<12)enteredCode+=value;}
        public void EraseCode(){if(enteredCode.Length>0)enteredCode=enteredCode.Substring(0,enteredCode.Length-1);}
        public void Join(){if(JoinEnabled&&CodeValid)_=Room.Join(enteredCode);}
        public void DetectNearby(){if(VisitingLobby)return;Room.ScanNearby();showingNearby=true;UnfocusCode();nearbyPage=0;scanningUntil=Time.unscaledTime+2;}
        public void ShowCodeEntry(){showingNearby=false;FocusCode();}
        IslandDiscovery.Entry NearbyEntry(int slot)=>Room.Discovery?.JoinableAt(nearbyPage,3,slot);
        public bool HasNearby(int slot)=>NearbyEntry(slot)!=null;
        public string NearbyTitle(int slot)
        {
            var entry=NearbyEntry(slot);
            if(entry==null)return "";
            string prefix=entry==Room.Discovery.Invitation?"INVITE · ":"";
            return prefix+entry.message.name+" · "+entry.message.count+" players\nJOIN "+entry.message.code;
        }
        public void JoinNearby(int slot){var entry=NearbyEntry(slot);if(entry==null||!JoinEnabled)return;enteredCode=entry.message.code;_=Room.Join(enteredCode);}
        public bool HasMoreNearby=>(Room.Discovery?.Joinable.Count??0)>3;
        public void NextNearbyPage()=>nearbyPage++;
        public string InvitationFrom=>Room.Discovery?.Invitation?.message.name;
        public void AcceptInvitation(){if(!VisitingLobby)Room.AcceptInvite();}
        public bool CanLeave=>!VisitingLobby&&(Room.Connected||Room.Busy);
        public bool Busy=>Room.Busy;
        public void Leave(){if(CanLeave)_=Room.Leave();}
        public string JoinHint{get;private set;}=""; 

        // Host terminal
        public bool HostActive=>Room.LocalIsLobbyHost;
        public bool CanHost=>!VisitingLobby&&(!Room.Busy&&!Room.Connected);
        public void Host(){if(CanHost)_=Room.Host();}
        public string HostCode=>Room.Busy?"CONNECTING…":Room.Connected?Room.Code:"PRIVATE SOLO ISLAND";
        public string Region=>Room.Region;
        public void NextRegion(){if(CanHost)Room.Region=IslandRoomAddress.Regions[(Array.IndexOf(IslandRoomAddress.Regions,Room.Region)+1)%IslandRoomAddress.Regions.Length];}
        public bool CanInvite=>!VisitingLobby&&(!Room.Busy&&!Room.LaunchPending);
        public void InviteNearby(){if(CanInvite)Room.InviteNearby();}
        public bool DemoSelected=>catalog.modes[Room.ModeIndex].id=="demo";
        public bool SharedControls=>Room.SharedDemoControls;
        public void ToggleSharedControls(){if(CanConfigure)Room.SetSharedControls(!Room.SharedDemoControls);}
        public string HostHint{get;private set;}="";

        // Map + mode terminal
        public bool IsHost=>Room.LocalIsLobbyHost;
        public bool Connected=>Room.Connected;
        public bool CanConfigure=>!VisitingLobby&&Room.CanConfigure&&(!ExperienceProvider.PlayerPlaylist||ExperienceProvider.CanChooseFirstRound);
        bool AllowsMode(string id)=>!ExperienceProvider.PlayerPlaylist||ExperienceProvider.CanChoosePlayMode(id);
        bool AllowsMap(IslandLobbyCatalog.Map map,string mode)=>map.SupportsMode(mode)&&
            (!ExperienceProvider.PlayerPlaylist||ExperienceProvider.CanChoosePlayPair(map.id,mode));
        public string MapTitle=>catalog.maps[Room.MapIndex].title;
        public string ModeTitle=>catalog.modes[Room.ModeIndex].title;
        public void CycleMap(int step)
        {
            if(!CanConfigure)return;int at=Room.MapIndex;for(int i=0;i<catalog.maps.Length;i++){at=(at+step+catalog.maps.Length)%catalog.maps.Length;if(!AllowsMap(catalog.maps[at],SelectedModeId))continue;Room.SelectMap(at);return;}
        }
        public void CycleMode(int step)
        {
            if(!CanConfigure)return;int at=Room.ModeIndex;for(int i=0;i<catalog.modes.Length;i++){at=(at+step+catalog.modes.Length)%catalog.modes.Length;if(!catalog.modes[at].available||!AllowsMode(catalog.modes[at].id)||!Array.Exists(catalog.maps,m=>AllowsMap(m,catalog.modes[at].id)))continue;Room.SelectMode(at);return;}
        }
        public bool SelectMap(string id){int i=Array.FindIndex(catalog.maps,m=>m.id==id);if(i<0||!CanConfigure||!AllowsMap(catalog.maps[i],SelectedModeId))return false;Room.SelectMap(i);return true;}
        public bool SelectMode(string id){int i=Array.FindIndex(catalog.modes,m=>m.id==id);if(i<0||!CanConfigure||!catalog.modes[i].available||!AllowsMode(id)||!Array.Exists(catalog.maps,m=>AllowsMap(m,id)))return false;Room.SelectMode(i);return true;}
        public bool CanStart=>VisitingLobby||(ExperienceProvider.PlayerPlaylist?
            ExperienceProvider.CanChooseFirstRound&&Room.CanLaunch:Room.CanLaunch||Room.LaunchPending&&Room.LocalIsLobbyHost);
        public bool StartPending=>!VisitingLobby&&Room.LaunchPending;
        public void StartOrCancel(){if(VisitingLobby){ReturnToMatch();return;}if(!CanStart)return;if(Room.LaunchPending)Room.CancelLaunch();else Room.Launch();}
        public string SetupHint{get;private set;}="";

        void UpdateControls()
        {
            if(!JoinEnabled)UnfocusCode();
            var inviteStatus=Room.Discovery?.InviteStatus;
            HostHint=Room.Busy||Room.Status.StartsWith("Could not transfer host",StringComparison.Ordinal)?Room.Status:!string.IsNullOrEmpty(inviteStatus)?inviteStatus:
                Room.DiscoveryNotice.Length>0?Room.DiscoveryNotice:
                Room.Connected?(Room.LocalIsLobbyHost?"You control this island.":"Guest · invite friends or request host."):
                "Start solo now, or create a code / invite nearby.";
            SetupHint=Room.LaunchPending?"Waiting for players to confirm…":!catalog.modes[Room.ModeIndex].available?"This mode is coming later.":Room.LocalIsLobbyHost?"Ready when you are.":"The host chooses the map, mode and settings.";
            if(ExperienceProvider.PlayerPlaylist)SetupHint=ExperienceProvider.GameStatus;
            JoinHint=Room.Busy?Room.Status:showingNearby?
                (Room.Discovery?.HasJoinable==true?"Pick a room, or enter a code above.":
                    Room.DiscoveryNotice.Length>0?Room.DiscoveryNotice:
                    Time.unscaledTime<scanningUntil?"Detecting nearby islands…":"No rooms found. Open the game on both devices on the same local network, or enter a room code."):
                Room.Connected?"Joining another room leaves this island.":"Enter six room characters, such as ABC234.\nUse a region prefix only for a different region.";
            if(!Room.Busy&&!Room.Connected&&Room.Status.StartsWith("Connection failed",StringComparison.Ordinal))JoinHint=HostHint=Room.Status;
            else if(JoinEnabled&&!showingNearby&&IslandRoomAddress.TryParse(enteredCode,Room.Region,out var region,out var address))
                JoinHint="Join "+address+" · "+(enteredCode.Contains("-")?"region specified":"using "+region.ToUpperInvariant()+" as the default region");
            else if(JoinEnabled&&!showingNearby&&enteredCode.Length>=6&&!CodeValid)JoinHint="Check the code. Example: US-ABC234.\nUse the host's region prefix, or six room characters.";
        }
    }
}