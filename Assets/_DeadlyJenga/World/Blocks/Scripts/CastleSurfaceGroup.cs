using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadlyJenga.CastlePrototype
{
    // Only survivor collision copies share a body. Real block physics stays independent.
    [DefaultExecutionOrder(-90)]
    public sealed class CastleSurfaceGroup : MonoBehaviour
    {
        static readonly Dictionary<int, Dictionary<Vector3Int, List<CastleSurfaceGroup>>> scenes = new();
        static bool dirty;
        const int Capacity = 32;
        int sceneId, members;
        Vector3Int cell;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRuntime() { scenes.Clear(); dirty=false; }
        internal static void Changed() => dirty=true;
        void Update()
        {
            if(!dirty)return;
            dirty=false;
            Physics.SyncTransforms();
        }

        public static CastleSurfaceGroup Acquire(Scene scene, Vector3 position)
        {
            if (!scenes.TryGetValue(scene.handle, out var cells))
                scenes.Add(scene.handle, cells = new Dictionary<Vector3Int, List<CastleSurfaceGroup>>());
            var key = new Vector3Int(Mathf.FloorToInt(position.x / 16), Mathf.FloorToInt(position.y / 16), Mathf.FloorToInt(position.z / 16));
            if (!cells.TryGetValue(key, out var groups)) cells.Add(key, groups = new List<CastleSurfaceGroup>());
            foreach (var group in groups)
                if (group && group.members < Capacity) { group.members++; return group; }
            var root = new GameObject("Shared survivor collision body");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.layer = DeadlyJenga.Core.Layers.Surface;
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true; body.useGravity = false;
            var created = root.AddComponent<CastleSurfaceGroup>();
            created.sceneId = scene.handle; created.cell = key; created.members = 1;
            groups.Add(created);
            return created;
        }
        public void Release()
        {
            members--;
            if (members > 0) return;
            Unregister();
            Destroy(gameObject);
        }
        void Unregister()
        {
            if (!scenes.TryGetValue(sceneId, out var cells) || !cells.TryGetValue(cell, out var groups)) return;
            groups.Remove(this);
            if (groups.Count == 0) cells.Remove(cell);
            if (cells.Count == 0) scenes.Remove(sceneId);
        }
        void OnDestroy()
        {
            foreach(var collider in GetComponentsInChildren<Collider>(true)) DeadlyJenga.Core.Climbables.Unregister(collider);
            Unregister();
        }
    }
}
