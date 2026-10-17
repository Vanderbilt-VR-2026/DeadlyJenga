using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.Core
{
    // Questions about the current map. The active map answers them.
    public interface IWorldQuery
    {
        float WaterHeight(Vector3 position);
        bool IsSheltered(Vector3 position);
        Vector3 SafeSpawnPoint();

        // The map's shape, for anything that needs to plan around it (bots, hazards).
        IReadOnlyList<Rigidbody> Blocks { get; }
        Vector3 Center { get; }
        float TopHeight { get; }
        float Radius { get; }
        float StepHeight { get; }   // height of one level (one block), for tuning jumps
        // World-space area used by disasters. All dimensions must be finite and positive.
        Bounds PlayableBounds { get; }
    }
}
