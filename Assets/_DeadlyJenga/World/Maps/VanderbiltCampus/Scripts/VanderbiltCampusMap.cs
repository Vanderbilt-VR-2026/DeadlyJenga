using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // The campus map: static terrain, buildings and trees exported from Blender.
    // Nothing moves, so Blocks is empty and the layout is a fixed token.
    // The DevTools builder fills every field below; nothing is generated at runtime.
    public sealed class VanderbiltCampusMap : MonoBehaviour, IWorldQuery, ISyncedWorld, IMapFootprint, IRebuildable
    {
        [Tooltip("Map-local box around the playable campus (boundary plus margin).")]
        public Bounds localArea = new Bounds(Vector3.zero, new Vector3(100, 40, 100));
        [Tooltip("Map-local height of the lowest walkable ground. Water sits 3 m below it.")]
        public float lowestGround;
        [Tooltip("Authored spawn points (Spawn_* objects and the pipeline's spawn list).")]
        public Transform[] spawns = Array.Empty<Transform>();
        [Tooltip("Goes up when the exported geometry changes. Must match the match layout's signature.")]
        public int layoutVersion = 1;

        static readonly Rigidbody[] NoBlocks = Array.Empty<Rigidbody>();
        float nextKillCheck;

        public int Revision { get; private set; }
        public Transform Root => transform;
        public IReadOnlyList<Rigidbody> Blocks => NoBlocks;
        public Vector3 Center => transform.TransformPoint(new Vector3(localArea.center.x, lowestGround, localArea.center.z));
        public float TopHeight => transform.TransformPoint(localArea.max).y;
        public float Radius => Mathf.Max(localArea.extents.x, localArea.extents.z) * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
        // Real buildings have normal steps and kerbs, not block-sized levels.
        public float StepHeight => .5f;

        public Bounds PlayableBounds
        {
            get
            {
                var area = localArea;
                if (area.size.x <= 0 || area.size.y <= 0 || area.size.z <= 0) area = new Bounds(area.center, new Vector3(100, 40, 100));
                var b = new Bounds(transform.TransformPoint(area.center), Vector3.zero);
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                        for (int z = -1; z <= 1; z += 2)
                            b.Encapsulate(transform.TransformPoint(area.center + Vector3.Scale(area.extents, new Vector3(x, y, z))));
                return b;
            }
        }

        void OnEnable() { Services.World = this; }
        void OnDisable() { if (ReferenceEquals(Services.World, this)) Services.World = null; }

        public float WaterHeight(Vector3 position) => transform.TransformPoint(new Vector3(0, lowestGround - 3, 0)).y;

        public bool IsSheltered(Vector3 position) =>
            Physics.Raycast(position + Vector3.up * .1f, Vector3.up, 60, 1 << Layers.Surface, QueryTriggerInteraction.Ignore);

        public Vector3 SafeSpawnPoint()
        {
            var usable = new List<Transform>();
            foreach (var spawn in spawns) if (spawn) usable.Add(spawn);
            if (usable.Count > 0) return usable[UnityEngine.Random.Range(0, usable.Count)].position + Vector3.up * .08f;
            // No authored spawns: drop onto whatever ground is under the middle of the map.
            var top = transform.TransformPoint(new Vector3(localArea.center.x, localArea.max.y + 10, localArea.center.z));
            if (Physics.Raycast(top, Vector3.down, out var hit, localArea.size.y + 40, 1 << Layers.Surface, QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * .08f;
            return Center + Vector3.up * 2;
        }

        // Kill floor: anything that falls well below the water is eliminated (same check as EpicCityMap).
        void FixedUpdate()
        {
            if (Time.time < nextKillCheck || (Services.Session is IWorldAuthority a && !a.HasWorldAuthority)) return;
            nextKillCheck = Time.time + .25f;
            foreach (var mover in Tracked.Movers)
            {
                if (!(mover is Component actor) || !actor || actor.transform.position.y >= WaterHeight(actor.transform.position) - 3) continue;
                var target = actor.GetComponent<IDamageable>(); if (target != null && target.Health > 0) target.Eliminate(gameObject);
            }
        }

        public void Rebuild() { Physics.SyncTransforms(); Revision++; }

        public string SaveLayout() => "vanderbilt-campus:" + layoutVersion;

        public bool LoadLayout(string layout, int expectedBlocks, Pose origin, Vector3 scale)
        {
            if (layout != SaveLayout() || expectedBlocks != 0 || !Finite(origin.position) || !Finite(scale) ||
                scale.x <= 0 || scale.y <= 0 || scale.z <= 0 ||
                !Finite(new Vector3(origin.rotation.x, origin.rotation.y, origin.rotation.z)) || !Finite(origin.rotation.w) ||
                Mathf.Abs(Quaternion.Dot(origin.rotation, origin.rotation) - 1) > .01f) return false;
            transform.SetPositionAndRotation(origin.position, origin.rotation); transform.localScale = scale; Rebuild(); return true;
        }

        static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
        static bool Finite(Vector3 p) => Finite(p.x) && Finite(p.y) && Finite(p.z);

        // Every surface is static, so replicas look the same as the host.
        public void SetReplica(bool replica) { }

        public int FootprintVersion => 7000 + layoutVersion;
        public float FootprintHalfSize => Mathf.Max(localArea.extents.x, localArea.extents.z);
        public void GetGroundFootprints(List<Footprint> into) => into.Add(new Footprint
        {
            center = new Vector2(localArea.center.x, localArea.center.z),
            halfSize = new Vector2(localArea.extents.x, localArea.extents.z),
            rotation = Quaternion.identity
        });
    }
}
