using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // The campus map: static terrain, buildings and trees exported from Blender, plus Jenga towers and
    // block castles (castle blocks, built at runtime from the tower list) inside a play area.
    // The whole campus is visible and walkable; towers, spawns and objectives stay inside the play area so a
    // match on a 1.2 km map stays fair. Bots and disasters use PlayableBounds = the play area.
    // The DevTools builder fills every serialized field; towers are the only thing generated at runtime.
    public sealed partial class VanderbiltCampusMap : MonoBehaviour, IWorldQuery, ISyncedWorld, IMapFootprint, IRebuildable
    {
        [Tooltip("Map-local box around the whole campus (boundary plus margin). Used for the footprint and overhead renders.")]
        public Bounds localArea = new Bounds(Vector3.zero, new Vector3(100, 40, 100));
        [Tooltip("Map-local height of the lowest walkable ground. Water sits 3 m below it.")]
        public float lowestGround;
        [Tooltip("Spawn points inside the play area (from gameplay.json, or the pipeline's spawns as a fallback).")]
        public Transform[] spawns = Array.Empty<Transform>();
        [Tooltip("Goes up when the exported geometry changes. Part of the layout token the match layout must match.")]
        public int layoutVersion = 1;

        [Header("Play area (data: gameplay.json)")]
        [Tooltip("Map-local center of the play area; y is the ground height there.")]
        public Vector3 playAreaCenter;
        [Min(10)] public float playAreaRadius = 150;
        [Tooltip("Map-local height of the highest roof or tower inside the play area.")]
        public float playAreaTop = 40;

        float nextKillCheck;

        public int Revision { get; private set; }
        public Transform Root => transform;
        float Scale => Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
        public Vector3 Center => transform.TransformPoint(playAreaCenter);
        public float Radius => playAreaRadius * Scale;
        public float TopHeight => transform.TransformPoint(new Vector3(0, Mathf.Max(playAreaTop, TowersTop), 0)).y;
        // Real ground has kerbs and steps, not block-sized levels.
        public float StepHeight => .5f;

        // The play area as a box: its circle's square, from below the water to the highest roof or tower.
        public Bounds PlayableBounds
        {
            get
            {
                float bottom = lowestGround - 3, top = Mathf.Max(playAreaTop, TowersTop, playAreaCenter.y + 10);
                var local = new Bounds(new Vector3(playAreaCenter.x, (bottom + top) * .5f, playAreaCenter.z),
                    new Vector3(playAreaRadius * 2, Mathf.Max(10, top - bottom), playAreaRadius * 2));
                var b = new Bounds(transform.TransformPoint(local.center), Vector3.zero);
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                        for (int z = -1; z <= 1; z += 2)
                            b.Encapsulate(transform.TransformPoint(local.center + Vector3.Scale(local.extents, new Vector3(x, y, z))));
                return b;
            }
        }

        // True when a world position is inside the play-area circle (any height).
        public bool InPlayArea(Vector3 world, float margin = 0)
        {
            var local = transform.InverseTransformPoint(world);
            return new Vector2(local.x - playAreaCenter.x, local.z - playAreaCenter.z).magnitude <= playAreaRadius + margin;
        }

        void OnEnable() { Services.World = this; }
        void OnDisable() { if (ReferenceEquals(Services.World, this)) Services.World = null; }
        void Start() { if (!towersBuilt) Rebuild(); }

        public float WaterHeight(Vector3 position) => transform.TransformPoint(new Vector3(0, lowestGround - 3, 0)).y;

        public bool IsSheltered(Vector3 position) =>
            Physics.Raycast(position + Vector3.up * .1f, Vector3.up, 60, (1 << Layers.Surface) | (1 << Layers.Block), QueryTriggerInteraction.Ignore);

        public Vector3 SafeSpawnPoint()
        {
            var usable = new List<Transform>();
            foreach (var spawn in spawns) if (spawn) usable.Add(spawn);
            if (usable.Count > 0) return usable[UnityEngine.Random.Range(0, usable.Count)].position + Vector3.up * .08f;
            // No authored spawns: drop onto whatever ground is under the middle of the play area.
            var top = transform.TransformPoint(new Vector3(playAreaCenter.x, Mathf.Max(playAreaTop, playAreaCenter.y) + 50, playAreaCenter.z));
            if (Physics.Raycast(top, Vector3.down, out var hit, 500, 1 << Layers.Surface, QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * .08f;
            return Center + Vector3.up * 2;
        }

        // Kill floor: characters that fall well below the water are eliminated (same check as EpicCityMap),
        // and fallen tower blocks are switched off.
        void FixedUpdate()
        {
            if (Time.time < nextKillCheck || (Services.Session is IWorldAuthority a && !a.HasWorldAuthority)) return;
            nextKillCheck = Time.time + .25f;
            foreach (var mover in Tracked.Movers)
            {
                if (!(mover is Component actor) || !actor || actor.transform.position.y >= WaterHeight(actor.transform.position) - 3) continue;
                var target = actor.GetComponent<IDamageable>(); if (target != null && target.Health > 0) target.Eliminate(gameObject);
            }
            RetireFallenBlocks();
        }

        public void Rebuild() { BuildTowers(); Physics.SyncTransforms(); Revision++; }

        // The token names the geometry version and the exact tower list, so a peer with different
        // towers refuses the layout. Streaming never changes it.
        public string SaveLayout() => "vanderbilt-campus:" + layoutVersion + ":" + TowerToken();

        public bool LoadLayout(string layout, int expectedBlocks, Pose origin, Vector3 scale)
        {
            if (layout != SaveLayout() || expectedBlocks != ExpectedBlockCount() || !Finite(origin.position) || !Finite(scale) ||
                scale.x <= 0 || scale.y <= 0 || scale.z <= 0 ||
                !Finite(new Vector3(origin.rotation.x, origin.rotation.y, origin.rotation.z)) || !Finite(origin.rotation.w) ||
                Mathf.Abs(Quaternion.Dot(origin.rotation, origin.rotation) - 1) > .01f) return false;
            transform.SetPositionAndRotation(origin.position, origin.rotation); transform.localScale = scale; Rebuild(); return true;
        }

        static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
        static bool Finite(Vector3 p) => Finite(p.x) && Finite(p.y) && Finite(p.z);

        // IMapFootprint: the whole campus ground, for art shaped around the map.
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
