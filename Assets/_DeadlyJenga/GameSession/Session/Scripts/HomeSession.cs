using System;
using System.Threading.Tasks;
using System.Linq;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadlyJenga.CastlePrototype
{
    public sealed class HomeSession : MonoBehaviour, IHomeMenuControls, IHomeQuickPlayControls, IBlasterControls
    {
        public bool TestingEnabled => false;
        public bool Drawn => false;
        public bool LeftHanded => PlayerPrefs.GetInt("CandyBlaster.LeftHanded", 0) != 0;
        public void SetTestingEnabled(bool enabled) { }
        public void SetDrawn(bool drawn) { }
        public void SetLeftHanded(bool left)
        { PlayerPrefs.SetInt("CandyBlaster.LeftHanded", left ? 1 : 0); PlayerPrefs.Save(); }

        public IslandLobbyCatalog catalog;
        public string lobbyScene = "Lobby", gameScene = "Game";
        IslandRoomConnection room;
        bool loading, searchingNearby;
        float nearbyScanUntil;
        string message = "Choose your next adventure.";
        public static bool AutofillLobby { get; private set; } = true;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { AutofillLobby = true; entryGeneration = 0; pendingEntry = null; }
        sealed class HomeLobbyEntry
        {
            internal IslandRoomConnection room;
            internal RoomGameExperience provider;
            internal Fusion.NetworkRunner runner;
            internal string roomId;
            internal int generation, selection;
            internal bool automatic, hosted, consumed;
        }
        static int entryGeneration;
        static HomeLobbyEntry pendingEntry;
        static bool CurrentEntry(HomeLobbyEntry entry)
        {
            if(entry==null||entry.generation!=entryGeneration||!entry.room||!entry.provider||
                !ReferenceEquals(entry.room,IslandRoomConnection.Instance)||
                !ReferenceEquals(entry.provider,RoomGameExperience.Current)||
                !ReferenceEquals(entry.runner,entry.room.Runner))return false;
            var state=entry.provider.CaptureSnapshot();
            return state!=null&&state.roomId==entry.roomId&&state.selectionVersion==entry.selection&&
                !state.demo&&state.automatic==entry.automatic&&state.pending==GameFlowEffect.None&&
                state.phase==(entry.automatic?GameFlowPhase.Choosing:GameFlowPhase.Idle)&&
                (entry.hosted?
                    entry.runner&&entry.runner.IsRunning&&entry.runner.IsServer&&entry.room.Connected&&entry.room.HostSelected:
                    ReferenceEquals(entry.runner,null)&&!entry.room.Connected);
        }
        static HomeLobbyEntry PrepareEntry(IslandRoomConnection room,bool automatic,int generation)
        {
            var provider=RoomGameExperience.Ensure(room);
            if(!provider||!provider.PrepareHomeLobbyEntry(automatic))return null;
            var state=provider.CaptureSnapshot();
            return pendingEntry=new HomeLobbyEntry{room=room,provider=provider,automatic=automatic,hosted=!automatic,generation=generation,
                roomId=state.roomId,selection=state.selectionVersion};
        }
        internal static void ConsumeLobbyEntry(IslandLobby lobby)
        {
            var entry=pendingEntry;
            if(entry==null)return;
            if(!entry.room){pendingEntry=null;return;}
            if(!lobby||!ReferenceEquals(lobby,IslandLobby.Current)||!ReferenceEquals(lobby.Room,entry.room)||
                !ReferenceEquals(Services.Session,lobby)||lobby.VisitingLobby||!NetworkActors.Alive(lobby.Player)||
                !lobby.ActiveCamera||entry.room.Busy||entry.room.Migrating||entry.room.RoomSceneBusy||
                entry.room.Departing||entry.room.SwitchingMap)return;
            if(!CurrentEntry(entry)){pendingEntry=null;Debug.LogWarning("Home entry cancelled because its room or selection changed.");return;}
            if(entry.room.GameExperienceRosterIds.Length==0)return;
            pendingEntry=null;
            entry.consumed=entry.provider.ConsumeHomeLobbyEntry(lobby,entry.automatic,entry.runner);
            if(!entry.consumed)
                Debug.LogWarning("Home entry was not applied because a newer room action replaced it.");
        }
        public bool Busy => loading || room && room.Busy;
        public string Status
        {
            get
            {
                if(room&&(room.Busy||room.Status.StartsWith("Connection",StringComparison.Ordinal)||room.Status.StartsWith("Disconnected",StringComparison.Ordinal)))return room.Status;
                var invite=room?.Discovery?.Invitation;
                if(invite!=null)return "Nearby invite from "+invite.message.name+". Open Join Game to accept.";
                if(!searchingNearby || !room)return message;
                if(room.DiscoveryNotice.Length>0)return room.DiscoveryNotice;
                if(room.Discovery?.HasJoinable==true)return "Pick a nearby room or enter a room code.";
                return Time.unscaledTime<nearbyScanUntil?"Searching this local network…":
                    "No rooms found. Open the game on both devices on the same local network, or enter a room code.";
            }
        }
        public string Region => room ? room.Region.ToUpperInvariant() : "";
        string code = "";
        public string Code { get => code; set => code = (value ?? "").Trim().ToUpperInvariant(); }
        public bool CodeValid => room && IslandRoomAddress.TryParse(Code, room.Region, out _, out _);
        System.Collections.Generic.IReadOnlyList<IslandDiscovery.Entry> NearbyRooms=>room?.Discovery?.Joinable??Array.Empty<IslandDiscovery.Entry>();
        readonly System.Collections.Generic.Dictionary<int,string> displayedNearby=new();
        public int NearbyCount=>NearbyRooms.Count;
        public string NearbyTitle(int index)
        {
            var list=NearbyRooms;
            if(index<0||index>=list.Count){displayedNearby.Remove(index);return "";}
            var entry=list[index];
            displayedNearby[index]=entry.message.code;
            return (entry==room.Discovery.Invitation?"Invite: ":"")+entry.message.name+"  "+entry.message.code;
        }
        void Start()
        {
            if (!catalog) catalog = Resources.Load<IslandLobbyCatalog>("IslandLobby/Catalog");
            if (!catalog) { Debug.LogError("Home needs the island lobby catalog."); return; }
            ++entryGeneration; pendingEntry = null;
            room = IslandRoomConnection.Ensure();
            bool first = !room.Catalog;
            room.Catalog = IslandLobbyCatalog.Runtime(catalog);
            if (first) room.Region = catalog.defaultRegion;
            room.StartDiscovery();
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            GameEvents.RaiseHomeStarted(gameObject);
        }
        public void DetectNearby()
        {
            if (Busy || !room) return;
            room.ScanNearby();
            searchingNearby = true;
            nearbyScanUntil = Time.unscaledTime + 2;
        }
        public void NextRegion()
        {
            if (Busy) return;
            int index = Array.IndexOf(IslandRoomAddress.Regions, room.Region);
            room.Region = IslandRoomAddress.Regions[(index + 1) % IslandRoomAddress.Regions.Length];
        }
        public void Host() { if (!Busy) _ = Connect(true, null); }
        public void JoinCode() { if (!Busy && CodeValid) _ = Connect(false, Code); }
        public void JoinNearby(int index)
        {
            if(Busy||!displayedNearby.TryGetValue(index,out string address))return;
            if(!NearbyRooms.Any(entry=>entry.message.code==address))
            {
                searchingNearby=false;
                message="That nearby room is no longer available. Search again or enter its code.";
                return;
            }
            _=Connect(false,address);
        }
        async Task Connect(bool host, string address)
        {
            loading = true;
            searchingNearby = false;
            int request = ++entryGeneration; pendingEntry = null;
            try
            {
                AutofillLobby = false;
                if (host)
                {
                    MatchSetup.BotTarget = 1;
                    if (room.Connected) await room.Leave();
                    if (!this || request != entryGeneration) return;
                    var entry = PrepareEntry(room, false, request);
                    if (entry == null) { message = "The previous room is still closing. Try Host again in a moment."; return; }
                    var hostTask = room.Host();
                    entry.runner = room.Runner;
                    await hostTask;
                    if (!this || request != entryGeneration) return;
                    if (entry.consumed) return;
                    if (!CurrentEntry(entry))
                    { pendingEntry = null; message = "Host entry cancelled: the requested room failed or was replaced."; return; }
                }
                else
                {
                    if (!IslandRoomAddress.TryParse(address, room.Region, out var region, out var requestedCode)) return;
                    await room.Join(address);
                    if (!this || request != entryGeneration) return;
                    if (!room.Connected || room.HostSelected || room.IsHost || room.Region != region || room.Code != requestedCode)
                    { message = "The requested room was not joined. " + room.Status; return; }
                }
                if (!this || request != entryGeneration) return;
                if (room.Connected)
                {
                    if(!room.HasRoomScene)
                    {
                        if(room.Busy||room.Migrating||room.RoomSceneBusy||room.Departing||room.SwitchingMap)
                        { message = "Waiting for the requested room scene to finish loading."; return; }
                        SceneManager.LoadScene(lobbyScene);
                    }
                }
                else message = room.Status;
            }
            catch (Exception error) { if(request==entryGeneration){pendingEntry=null;message = "Unable to connect: " + error.Message;} }
            finally { if(request==entryGeneration)loading = false; }
        }
        IslandLobbyCatalog.Mode[] PlayModes
        {
            get
            {
                if(!room||!room.Catalog)return Array.Empty<IslandLobbyCatalog.Mode>();
                var pairs=RoomGameExperience.Ensure(room).PlayPairs();
                return room.Catalog.modes.Where(mode=>mode!=null&&pairs.Any(pair=>pair.modeId==mode.id)).ToArray();
            }
        }
        int quickMode;
        CuratedGameProfile[] SelectedPairs
        {
            get
            {
                var modes=PlayModes;
                if(quickMode<0||quickMode>=modes.Length)return Array.Empty<CuratedGameProfile>();
                return RoomGameExperience.Ensure(room).PlayPairs().Where(pair=>pair.modeId==modes[quickMode].id).ToArray();
            }
        }
        public string[] QuickPlayModes=>PlayModes.Select(mode=>mode.title).ToArray();
        public string[] QuickPlayMaps=>SelectedPairs.Select(pair=>Array.Find(room.Catalog.maps,map=>map.id==pair.mapId).title).ToArray();
        public void SelectQuickPlayMode(int index)
        {
            if(!Busy&&index>=0&&index<PlayModes.Length)quickMode=index;
        }
        public void StartQuickPlay(int mapIndex)
        {
            if(Busy||!room)return;
            var pairs=mapIndex==-1?RoomGameExperience.Ensure(room).PlayPairs():SelectedPairs;
            if(pairs.Length==0){message="There are no compatible games in this build.";return;}
            int index=mapIndex==-1?UnityEngine.Random.Range(0,pairs.Length):mapIndex;
            if(index>=0&&index<pairs.Length)_=OpenLocal(pairs[index].id);
        }
        public void RandomLobby()
        {
            if (!Busy && room) _ = OpenLocal(null, playLobby:true);
        }
        public void Demo() { if (!Busy&&room) _ = OpenLocal(null); }
        async Task OpenLocal(string profileId, bool playLobby = false)
        {
            loading = true;
            int request = ++entryGeneration; pendingEntry = null;
            try
            {
                if (room.Connected) await room.Leave();
                if (!this || request != entryGeneration) return;
                var experience=RoomGameExperience.Ensure(room);
                AutofillLobby=profileId==null&&!playLobby;
                if (playLobby)
                {
                    if (!experience.PrepareHomeLobbyEntry(false))
                    {message="The previous room is still closing. Try Play again in a moment.";return;}
                    MatchSetup.BotTarget=10;
                    MatchSetup.HandCount=0;
                    SceneManager.LoadScene(lobbyScene);
                }
                else if(profileId==null)
                {
                    if(!experience.PrepareStandaloneDemo(openDevelopment:true))
                    {message="The previous room is still closing. Try Developer again in a moment.";return;}
                    room.SelectMode(Array.FindIndex(room.Catalog.modes,mode=>mode.id=="demo"));
                    MatchSetup.BotTarget=10;MatchSetup.HandCount=0;
                    SceneManager.LoadScene(lobbyScene);
                }
                else if(experience.StartQuickPlay(profileId))
                {
                    message="Loading your game…";
                    // Keep input disabled through the room's departure animation.
                    while(this&&request==entryGeneration&&experience.GamePhase==RoomGamePhase.LoadingGame)
                        await Task.Yield();
                    if(this&&experience.GamePhase==RoomGamePhase.Failed)message=experience.GameStatus;
                }
                else message="Could not start that game. Select a pair and try again.";
            }
            catch (Exception error) { if(request==entryGeneration){pendingEntry=null;message = "Unable to open game: " + error.Message;} }
            finally { if(request==entryGeneration)loading = false; }
        }
    }
}
