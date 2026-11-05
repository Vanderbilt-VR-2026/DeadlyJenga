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
        static void Reset() => AutofillLobby = true;
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
            try
            {
                AutofillLobby = false;
                if (host) { MatchSetup.BotTarget = 1; if (room.Connected) await room.Leave(); if (!this) return; await room.Host(); }
                else await room.Join(address);
                if (!this) return;
                if (room.Connected)
                {
                    if(!room.HasRoomScene)SceneManager.LoadScene(lobbyScene);
                }
                else message = room.Status;
            }
            catch (Exception error) { message = "Unable to connect: " + error.Message; }
            finally { loading = false; }
        }
        public void RandomLobby() { if (!Busy) _ = OpenLocal(false); }
        public void Demo() { if (!Busy) _ = OpenLocal(true); }
        async Task OpenLocal(bool demo)
        {
            loading = true;
            try
            {
                if (room.Connected) await room.Leave();
                if (!this) return;
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
            catch (Exception error) { message = "Unable to open game: " + error.Message; }
            finally { loading = false; }
        }
    }
}
