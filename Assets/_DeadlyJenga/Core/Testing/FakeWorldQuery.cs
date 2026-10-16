using UnityEngine;

namespace DeadlyJenga.Core.Testing
{
    // Stand-in map for sandbox scenes. Registers itself as Services.World.
    public class FakeWorldQuery : MonoBehaviour, IWorldQuery
    {
        public float waterHeight = 0;
        public Vector3 spawnPoint = new Vector3(0, 2, 0);

        void OnEnable() => Services.World = this;
        void OnDisable() { if (ReferenceEquals(Services.World, this)) Services.World = null; }

        public float WaterHeight(Vector3 position) => waterHeight;
        public bool IsSheltered(Vector3 position) => Physics.Raycast(position, Vector3.up, 50, 1 << Layers.Block);
        public Vector3 SafeSpawnPoint() => spawnPoint;
    }
}
