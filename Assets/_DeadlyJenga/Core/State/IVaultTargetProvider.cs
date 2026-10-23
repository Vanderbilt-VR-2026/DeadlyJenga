using UnityEngine;

namespace DeadlyJenga.Core
{
    public interface IVaultTargetProvider
    {
        // True only when the player can start a vault now. Points are world-space.
        bool TryGetVaultTarget(out Collider surface, out Vector3 edgePoint, out Vector3 landingPoint);
    }

    public interface IVaultLookTargetProvider
    {
        // A visible ledge that could be vaulted from a nearby approach, not permission to vault now.
        bool TryGetLookVaultTarget(float maxDistance, out Collider surface, out Vector3 edgePoint, out Vector3 landingPoint);
    }
}
