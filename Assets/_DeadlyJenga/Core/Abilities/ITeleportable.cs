using UnityEngine;

namespace DeadlyJenga.Core
{
    // Can be moved instantly.
    public interface ITeleportable
    {
        void Teleport(Vector3 position, Quaternion rotation);
    }
}
