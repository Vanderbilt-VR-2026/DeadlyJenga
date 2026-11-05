using System;
using System.Threading.Tasks;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadlyJenga.CastlePrototype
{
    public sealed class HomeSession : MonoBehaviour, IHomeMenuControls, IBlasterControls
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
        public int NearbyCount=>NearbyRooms.Count;
        public string NearbyTitle(int index)
        {
            var list=NearbyRooms;
            if(index<0||index>=list.Count)return "";
            var entry=list[index];
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
            var list=NearbyRooms;
            if (!Busy && index >= 0 && index < list.Count) _ = Connect(false, list[index].message.code);
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
        public void RandomLobby() { if (!Busy) _ = OpenLocal(false); }
        public void Demo() { if (!Busy) _ = OpenLocal(true); }
        async Task OpenLocal(bool demo)
        {
            loading = true;
            int request = ++entryGeneration; pendingEntry = null;
            try
            {
                if (room.Connected) await room.Leave();
                if (!this || request != entryGeneration) return;
                if (demo && !RoomGameExperience.Ensure(room).PrepareStandaloneDemo())
                { message = "The previous room is still closing. Try Demo again in a moment."; return; }
                var entry = demo ? null : PrepareEntry(room, true, request);
                if (!demo && !CurrentEntry(entry))
                { pendingEntry = null; message = "The previous room is still closing. Try Random Lobby again in a moment."; return; }
                MatchSetup.BotTarget = 10; MatchSetup.HandCount = 0;
                AutofillLobby = true;
                if (demo)
                {
                    MatchSetup.MapId = "castle"; MatchSetup.ModeId = "demo";
                    MatchSetup.FromLobby = true; MatchSetup.StartLocalSession = false;
                    SceneManager.LoadScene(gameScene);
                }
                else SceneManager.LoadScene(lobbyScene);
            }
            catch (Exception error) { if(request==entryGeneration){pendingEntry=null;message = "Unable to open game: " + error.Message;} }
            finally { if(request==entryGeneration)loading = false; }
        }
    }
}
