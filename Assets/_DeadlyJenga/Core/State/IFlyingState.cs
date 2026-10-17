using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.Core
{
    // How a flying hand is moving right now. Read-only.
    public interface IFlyingState
    {
        Vector3 Velocity { get; }
        Transform Anchor { get; }                       // where the hand is
        bool Interactive { get; }                       // can touch blocks right now
        IReadOnlyList<Rigidbody> ContactBodies { get; } // the parts that touch things (may be null)
    }
}
