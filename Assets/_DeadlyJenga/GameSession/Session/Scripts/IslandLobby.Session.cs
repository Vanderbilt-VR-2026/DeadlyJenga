using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    // The island lobby as Core's session and map while the Lobby scene is open. There is no flying hand,
    // no bots and no shared map to rebuild here; the island draws and points its own terminals.
    public sealed partial class IslandLobby
    {
        public bool PlayingHand => false;
        public void SetRole(bool hand) { }
        IPlayableCharacter ISessionInfo.Player => Player;
        public IPlayableCharacter Hand => null;
        public bool IsVR => Player && Player.IsVR;
        public Camera ActiveCamera => Player ? Player.viewCamera : null;
        public bool MenuSelectPressed => false;
        public bool MenuSelectHeld => false;
        public bool CanEditWorld => true;
        public void ResetMap() { }
        public ISurvivorBotPopulation SurvivorBots => null;
        public IBotPopulation HandBots => null;
        public IMultiplayerControls Multiplayer => null;
        public IDemoControls Demo => null;
        public ISocialControls Social => null;
        public float StepHeight => TopHeight;
    }
}
