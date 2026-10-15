using UnityEngine;
using DeadlyJenga.CastlePrototype;

namespace DeadlyJenga.CastleBots
{
    public struct CastleBotCommand
    {
        public Vector3 move,look;
        public bool jump;
        // Optional traversal intent: existing bots keep their current jump/vault behaviour.
        public bool climbHeld;
    }
    // The same CharacterController motor consumes human input or this input, never both.
    public interface ICastleSurvivorDriver
    {
        CastleBotCommand ReadCommand(CastleSurvivor motor,float dt);
    }
    // Add an adapter beside a future grapple/climb ability on the survivor prefab.
    // Returning true supplies ordinary ability/movement input for this frame.
    public interface ICastleBotTraversalAbility
    {
        bool CanReach(CastleSurvivor motor,Vector3 destination);
        bool TryTraverse(CastleSurvivor motor,Vector3 destination,out CastleBotCommand command);
    }
}
