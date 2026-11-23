using DeadlyJenga.Core;
namespace DeadlyJenga.CastlePrototype
{
    public static class TraversalCollision
    {
        public static bool Unified => Services.World is ITraversalCollisionWorld world && world.UsesOriginalColliders;
        public static int CapsuleExclusions => (Unified ? 0 : 1 << Layers.Block) |
            (MatchChoices.MapId == "epic-candy-sky-ring-city" ? UnityEngine.Physics.IgnoreRaycastLayer : 0);
        public static int Mask => ~((1 << Layers.Survivor) | CapsuleExclusions);
        public static int ClimbMask => (1<<Layers.Surface)|(Unified?1<<Layers.Block:0);
    }
}
