using UnityEngine;

namespace DeadlyJenga.Core
{
    // Where a tracked hand's index fingertip is in the world, for pressing things with a finger.
    public interface IFingerTips
    {
        bool TryIndexTip(bool left, out Vector3 tip);
    }
}
