using UnityEngine;

namespace DeadlyJenga.Core
{
    // What other boxes may ask of the running game. Game Session provides it.
    // Round info (time, survivors left, hands) gets added here when rounds exist.
    public interface ISessionInfo
    {
        bool MenuOpen { get; }
        void SetMenu(bool open);

        // Who is playing and what they control.
        bool PlayingHand { get; }
        void SetRole(bool hand);                  // false = survivor (respawns on top), true = flying hand
        IPlayableCharacter Player { get; }        // the local survivor (also ISurvivorSettings, IMovementState)
        IPlayableCharacter Hand { get; }          // the local flying hand
        bool IsVR { get; }
        Camera ActiveCamera { get; }
        bool MenuSelectPressed { get; }           // VR "select" this frame (trigger or pinch)

        // The shared map and the people in it.
        bool CanEditWorld { get; }                // false on joined machines: the host owns the map
        void ResetMap();
        ISurvivorBotPopulation SurvivorBots { get; }   // may be null
        IBotPopulation HandBots { get; }               // may be null
        IMultiplayerControls Multiplayer { get; }      // null when playing alone
    }
}
