using UnityEngine;

namespace DeadlyJenga.Core
{
    // How a walking/climbing character is moving right now. Read-only.
    // Sound and HUD read this instead of the character's own scripts.
    public interface IMovementState
    {
        bool Grounded { get; }
        bool Climbing { get; }
        bool Vaulting { get; }
        float VerticalSpeed { get; }
        float Stamina { get; }      // 0..1, climbing grip left
        float Height { get; }       // standing height in metres
        float Scale { get; }
        int VaultCount { get; }     // goes up by one every vault
        bool IsLocal { get; }       // the player on this device
        bool InputPaused { get; }
        bool TryGetGrip(bool left, out Pose grip);
    }
}
