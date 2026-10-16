using UnityEngine;

namespace DeadlyJenga.Core
{
    // Can be shoved. force is a change in velocity (m/s).
    public interface IPushable
    {
        void Push(Vector3 force, GameObject source);
    }
}
