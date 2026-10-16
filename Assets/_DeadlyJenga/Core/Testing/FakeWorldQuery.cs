using System.Collections.Generic;
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

        public List<Rigidbody> blocks = new List<Rigidbody>();
        public float topHeight = 10, radius = 10;
        public IReadOnlyList<Rigidbody> Blocks => blocks;
        public Vector3 Center => transform.position;
        public float TopHeight => topHeight;
        public float Radius => radius;
    }
}
