using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // A map made in Blender and dropped in as a model. Every mesh becomes solid, non-moving ground.
    // Names from Blender decide roles:
    //   "Kill*" or "*(kill floor)"  bare island ground: the session's island rule (deadly sand) applies
    //   "Floor*"                    also bare island ground, ignored for the map's size
    //   "Spawn*"                    spawn points (otherwise the present top nearest spawnHeight)
    //   anything else               safe ground: gets a non-moving (kinematic) rigidbody like a block
    public sealed class BlenderModelMap : MonoBehaviour, IWorldQuery, ISyncedWorld, IMapFootprint, IRebuildable
    {
        public GameObject model;
        public float waterHeight = -20, spawnHeight = 2.5f, stepHeight = 1.2f;
        readonly List<Rigidbody> bodies = new List<Rigidbody>();
        readonly List<Collider> ground = new List<Collider>();
        readonly List<Transform> spawns = new List<Transform>();
        Transform built;
        Bounds area;

        public IReadOnlyList<Rigidbody> Blocks => bodies;
        public Transform Root => transform;
        public int Revision { get; private set; }
        public Vector3 Center => new Vector3(area.center.x, transform.position.y, area.center.z);
        public float TopHeight => area.max.y;
        public float Radius => Mathf.Max(area.extents.x, area.extents.z);
        public float StepHeight => stepHeight;
        public Bounds PlayableBounds { get { var b = area; b.Expand(8); return b; } }
        public int FootprintVersion => 3000 + Revision;
        public float FootprintHalfSize => Radius + 4;

        void OnEnable() { Services.World = this; }
        void OnDisable() { if (ReferenceEquals(Services.World, this)) Services.World = null; }
        void Start() { if (!built) Rebuild(); }

        public void Rebuild()
        {
            if (built) { built.gameObject.SetActive(false); if (Application.isPlaying) Destroy(built.gameObject); else DestroyImmediate(built.gameObject); }
            ground.Clear(); spawns.Clear();
            if (!model) { area = new Bounds(transform.position, Vector3.one * 10); return; }
            built = Instantiate(model, transform).transform;
            built.name = "Model (" + model.name + ")";
            bool any = false;
            foreach (var child in built.GetComponentsInChildren<Transform>())
            {
                string n = child.name.ToLowerInvariant();
                if (n.StartsWith("spawn")) { spawns.Add(child); continue; }
                var filter = child.GetComponent<MeshFilter>();
                if (!filter || !filter.sharedMesh) continue;
                child.gameObject.layer = Layers.Surface;
                child.gameObject.isStatic = true;
                var shape = child.GetComponent<MeshCollider>();
                if (!shape) shape = child.gameObject.AddComponent<MeshCollider>();
                shape.sharedMesh = filter.sharedMesh;
                bool island = n.StartsWith("kill") || n.Contains("kill floor") || n.StartsWith("floor");
                if (!island)
                {
                    var body = child.gameObject.AddComponent<Rigidbody>();
                    body.isKinematic = true; body.useGravity = false;
                    ground.Add(shape);
                }
                if (n.StartsWith("floor")) continue;
                var b = child.GetComponent<Renderer>() ? child.GetComponent<Renderer>().bounds : shape.bounds;
                if (!any) { area = b; any = true; } else area.Encapsulate(b);
            }
            if (!any) area = new Bounds(transform.position, Vector3.one * 10);
            Physics.SyncTransforms();
            Revision++;
        }

        public float WaterHeight(Vector3 position) => waterHeight;
        public bool IsSheltered(Vector3 position) => Physics.Raycast(position + Vector3.up * .1f, Vector3.up, 500, (1 << Layers.Block) | (1 << Layers.Surface), QueryTriggerInteraction.Ignore);

        public Vector3 SafeSpawnPoint()
        {
            if (spawns.Count > 0) return spawns[UnityEngine.Random.Range(0, spawns.Count)].position + Vector3.up * .05f;
            Vector3 best = Center + Vector3.up * 2; float bestScore = float.MaxValue;
            foreach (var shape in ground)
            {
                if (!shape || !shape.name.StartsWith("Present", StringComparison.OrdinalIgnoreCase)) continue;
                var b = shape.bounds; var top = new Vector3(b.center.x, b.max.y + 2, b.center.z);
                if (!Physics.Raycast(top, Vector3.down, out var hit, 3, 1 << Layers.Surface, QueryTriggerInteraction.Ignore) || hit.normal.y < .9f) continue;
                float score = Mathf.Abs(hit.point.y - spawnHeight);
                if (score < bestScore) { bestScore = score; best = hit.point + Vector3.up * .05f; }
            }
            return best;
        }

        public void GetGroundFootprints(List<Footprint> into)
        {
            foreach (var shape in ground)
            {
                if (!shape) continue; var b = shape.bounds;
                if (b.min.y > transform.position.y + .1f) continue;
                into.Add(new Footprint { center = new Vector2(b.center.x, b.center.z), halfSize = new Vector2(b.extents.x, b.extents.z), rotation = Quaternion.identity });
            }
        }

        // Static maps have no moving blocks, so the layout is just the model's name.
        const string Prefix = "blender-model-v1:";
        public string SaveLayout() => Prefix + (model ? model.name : "");
        public bool LoadLayout(string layout, int expectedBlocks, Pose origin, Vector3 scale)
        {
            if (layout == null || !layout.StartsWith(Prefix, StringComparison.Ordinal) || expectedBlocks != 0) return false;
            if (model && layout.Substring(Prefix.Length) != model.name) return false;
            transform.SetPositionAndRotation(origin.position, origin.rotation); transform.localScale = scale;
            Rebuild(); return true;
        }
        public void SetReplica(bool value) { }
    }
}
