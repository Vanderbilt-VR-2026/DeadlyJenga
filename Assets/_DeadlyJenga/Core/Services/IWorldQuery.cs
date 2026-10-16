using UnityEngine;

namespace DeadlyJenga.Core
{
    // Questions about the current map. The active map answers them.
    public interface IWorldQuery
    {
        float WaterHeight(Vector3 position);
        bool IsSheltered(Vector3 position);
        Vector3 SafeSpawnPoint();
    }
}
