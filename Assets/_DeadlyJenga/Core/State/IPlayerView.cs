using UnityEngine;

namespace DeadlyJenga.Core
{
    // A character played on this device: its camera and stamina status. Read-only.
    // HUDs read this instead of the character's own scripts.
    public interface IPlayerView
    {
        Camera ViewCamera { get; }
        bool IsVR { get; }
        bool ControlledHere { get; }      // false for bots and remote players
        bool StaminaExhausted { get; }
        bool StaminaRecovering { get; }
    }
}
