using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    // The island lobby as Core's session and map while the Lobby scene is open. There is no flying hand,
    // no bots and no shared map to rebuild here; Presentation draws and points the island's terminals.
    public sealed partial class IslandLobby
    {
        public bool PlayingHand => false;
        public void SetRole(bool hand) { }
        public IPlayableCharacter Hand => null;
        public bool IsVR => NetworkActors.Alive(Player) && Player.IsVR;
        public Camera ActiveCamera => NetworkActors.Alive(Player) ? Player.ViewCamera : null;
        public bool MenuSelectPressed => false;
        public bool MenuSelectHeld => false;
        public bool CanEditWorld => Room&&Room.LocalIsLobbyHost;
        public void ResetMap() { }
        public ISurvivorBotPopulation SurvivorBots => lobbyBots;
        public IBotPopulation HandBots => null;
        public IMultiplayerControls Multiplayer => null;
        public IDemoControls Demo => null;
        public ISocialControls Social => null;
        public float StepHeight => TopHeight;
    }
}
