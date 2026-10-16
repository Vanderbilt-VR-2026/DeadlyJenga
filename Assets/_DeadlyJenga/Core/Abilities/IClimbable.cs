using UnityEngine;

namespace DeadlyJenga.Core
{
    // Can be climbed or vaulted.
    public interface IClimbable
    {
        bool CanClimb { get; }
        float Grip { get; }          // 1 = normal, 0 = can't hold on
        Transform Anchor { get; }    // climbers move with this
        Rigidbody Body { get; }      // may be null for static things
        Transform Visual { get; }    // optional, used to place hands on the art
    }
}
