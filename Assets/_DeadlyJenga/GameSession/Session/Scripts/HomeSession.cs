using System;
using System.Threading.Tasks;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadlyJenga.CastlePrototype
{
    public sealed class HomeSession : MonoBehaviour, IHomeMenuControls
    {
        public IslandLobbyCatalog catalog;
        public string lobbyScene = "Lobby", gameScene = "Game";
        IslandRoomConnection room;
        bool loading;
        string message = "Choose your next adventure.";
        public static bool AutofillLobby { get; private set; } = true;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => AutofillLobby = true;
        public bool Busy => loading || room && room.Busy;
        public string Status => room && (room.Busy || room.Status.StartsWith("Connection") || room.Status.StartsWith("Disconnected")) ? room.Status : message;
        public string Region => room ? room.Region.ToUpperInvariant() : "";
        string code = "";
        public string Code { get => code; set => code = (value ?? "").Trim().ToUpperInvariant(); }
        public bool CodeValid => room && IslandRoomAddress.TryParse(Code, room.Region, out _, out _);
        public int NearbyCount => room?.Discovery?.Nearby?.Count ?? 0;
        public string NearbyTitle(int index)
        {
            if (index < 0 || index >= NearbyCount) return "";
            var entry = room.Discovery.Nearby[index];
            return entry.message.name + "  " + entry.message.code;
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
            if (Busy) return;
            room.StartDiscovery(); message = "Searching your local network. Keep the host on its lobby island.";
            if (!string.IsNullOrEmpty(room.DiscoveryStatus)) message = room.DiscoveryStatus;
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
            if (!Busy && index >= 0 && index < NearbyCount) _ = Connect(false, room.Discovery.Nearby[index].message.code);
        }
        async Task Connect(bool host, string address)
        {
            loading = true;
            try
            {
                AutofillLobby = false;
                if (host) { MatchSetup.BotTarget = 1; if (room.Connected) await room.Leave(); if (!this) return; await room.Host(); }
                else await room.Join(address);
                if (!this) return;
                if (room.Connected) SceneManager.LoadScene(lobbyScene);
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
