using UnityEngine;

namespace DeadlyJenga.Core
{
    // Can crack under weight and break apart.
    public interface IBreakable
    {
        float Stress { get; }            // 0 = fine, 1 = about to break
        bool Broken { get; }
        void Break(GameObject source);   // only the world's owner (host) actually breaks things
    }
}
