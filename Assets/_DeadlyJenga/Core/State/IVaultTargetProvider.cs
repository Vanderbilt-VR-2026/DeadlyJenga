using UnityEngine;

namespace DeadlyJenga.Core
{
    public interface IVaultTargetProvider
    {
        // True only when the player can start a vault now. Points are world-space.
        bool TryGetVaultTarget(out Collider surface, out Vector3 edgePoint, out Vector3 landingPoint);
    }
}
