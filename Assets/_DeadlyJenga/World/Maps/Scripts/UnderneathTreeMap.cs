using System;
using System.Collections.Generic;
using System.Linq;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class UnderneathTreeMap : MonoBehaviour, IWorldQuery, ISyncedWorld,
        IMapFootprint, IRebuildable, ITraversalCollisionWorld
    {
        const string Layout = "underneath-the-tree-v1";
        static readonly HashSet<string> StationaryGifts = new HashSet<string> {
            "Present 00", "Present 01", "Present 02", "Present 03", "Present 04",
            "Present 05", "Present 06", "Present 07", "Present 202", "Present 206"
        };

        [SerializeField] GameObject model = null, room = null, locomotive = null;
        [SerializeField] Material paper = null;
        readonly List<Rigidbody> bodies = new List<Rigidbody>();
        readonly List<Mesh> generatedMeshes = new List<Mesh>();
        readonly List<UnderneathTreeGiftPanel> panels = new List<UnderneathTreeGiftPanel>();
        readonly List<Material> wrapping = new List<Material>();
        Transform built;
        MeshCollider spawn, skirt;
        bool collisionOwned, oldSurvivorBlock, oldSurfaceBlock;
        Bounds playArea;
        bool replica;

        public IReadOnlyList<Rigidbody> Blocks => bodies;
        public Transform Root => transform;
        public int Revision { get; private set; }
        public bool UsesOriginalColliders => true;
        public Vector3 Center => new Vector3(playArea.center.x, transform.position.y, playArea.center.z);
        public float TopHeight => playArea.max.y;
        public float Radius => Mathf.Max(playArea.extents.x, playArea.extents.z);
        public float StepHeight => 1.2f;
        public Bounds PlayableBounds => playArea;
        public int FootprintVersion => Revision;
        public float FootprintHalfSize => Radius + 4f;

        void OnEnable() => Services.World = this;

        void OnDisable()
        {
            if (collisionOwned)
            {
                Physics.IgnoreLayerCollision(Layers.Survivor, Layers.Block, oldSurvivorBlock);
                Physics.IgnoreLayerCollision(Layers.Surface, Layers.Block, oldSurfaceBlock);
                collisionOwned = false;
            }
            if (ReferenceEquals(Services.World, this))
                Services.World = null;
        }

        void Start()
        {
            if (!built)
                Rebuild();
        }

        public void Rebuild()
        {
            if (!model || !paper)
                throw new InvalidOperationException("Assign the tree block-out and gift-wrap material.");
            if (!collisionOwned)
            {
                oldSurvivorBlock = Physics.GetIgnoreLayerCollision(Layers.Survivor, Layers.Block);
                oldSurfaceBlock = Physics.GetIgnoreLayerCollision(Layers.Surface, Layers.Block);
                collisionOwned = true;
            }
            Physics.IgnoreLayerCollision(Layers.Survivor, Layers.Block, false);
            Physics.IgnoreLayerCollision(Layers.Surface, Layers.Block, false);
            Clear();
            built = new GameObject("Underneath the Tree Layout").transform;
            built.SetParent(transform, false);
            var layout = Instantiate(model, built);
            if (room)
                Instantiate(room, built);
            var filters = layout.GetComponentsInChildren<MeshFilter>()
                .OrderBy(filter => filter.name, StringComparer.Ordinal).ToArray();
            var byName = filters.ToDictionary(filter => filter.name, StringComparer.Ordinal);
            var paperByGift = new Dictionary<string, Material>(StringComparer.Ordinal);
            var ribbonByGift = new Dictionary<string, Material>(StringComparer.Ordinal);
            var rail = new List<Vector3>();
            playArea = new Bounds(transform.position, Vector3.zero);

            foreach (var filter in filters)
            {
                string name = filter.name;
                if (name.StartsWith("Track Rail L ", StringComparison.Ordinal))
                {
                    var opposite = byName["Track Rail R " + name.Substring("Track Rail L ".Length)];
                    rail.Add(transform.InverseTransformPoint((filter.GetComponent<Renderer>().bounds.center +
                        opposite.GetComponent<Renderer>().bounds.center) * .5f));
                }

                if (name.StartsWith("Present ", StringComparison.Ordinal))
                {
                    int suffix = name.IndexOf(' ', "Present ".Length);
                    if (suffix == -1)
                        continue;
                    var giftName = name.Substring(0, suffix);
                    var part = name.Substring(suffix + 1);
                    if (!ribbonByGift.ContainsKey(giftName) &&
                        (part.StartsWith("ribbon", StringComparison.OrdinalIgnoreCase) || part == "lid"))
                        ribbonByGift.Add(giftName, filter.GetComponent<Renderer>().sharedMaterial);
                    if (part == "box" && !StationaryGifts.Contains(giftName))
                        paperByGift.Add(giftName, filter.GetComponent<Renderer>().sharedMaterial);
                }
            }

            foreach (var filter in filters)
            {
                string name = filter.name;
                if (name.StartsWith("Present ", StringComparison.Ordinal))
                {
                    int suffix = name.IndexOf(' ', "Present ".Length);
                    if (suffix == -1)
                        continue;
                    var giftName = name.Substring(0, suffix);
                    var part = name.Substring(suffix + 1);
                    playArea.Encapsulate(filter.GetComponent<Renderer>().bounds);
                    if (paperByGift.TryGetValue(giftName, out var giftPaper))
                    {
                        if (part == "box")
                        {
                            ribbonByGift.TryGetValue(giftName, out var ribbon);
                            BuildPaperPanels(filter, giftPaper, ribbon);
                        }
                        filter.gameObject.SetActive(false);
                    }
                    else if (!StationaryGifts.Contains(giftName) && part == "lid")
                        BuildRoundLid(filter);
                    else if (!part.StartsWith("ribbon", StringComparison.OrdinalIgnoreCase))
                    {
                        var surface = AddMeshSurface(filter, true);
                        if (name == "Present 206 box")
                            spawn = surface;
                    }
                    continue;
                }

                if (name.Contains("kill floor"))
                    skirt = AddMeshSurface(filter, false);
                else if (name.StartsWith("Wood Planks", StringComparison.Ordinal) || name == "Tree Skirt rim")
                    AddMeshSurface(filter, false);
            }

            BuildJunction();
            BuildTrack(rail);
            BuildApproach(rail);
            playArea.Expand(new Vector3(8f, 12f, 8f));
            Physics.SyncTransforms();
            BuildSkirtStitching();
            Revision++;
        }

        void Clear()
        {
            if (built)
            {
                built.gameObject.SetActive(false);
                Release(built.gameObject);
            }

            foreach (var material in wrapping)
                Release(material);
            wrapping.Clear();
            bodies.Clear();
            panels.Clear();
            foreach (var mesh in generatedMeshes)
                Release(mesh);
            generatedMeshes.Clear();
            spawn = null;
            skirt = null;
        }

        static void Release(UnityEngine.Object item)
        {
            if (Application.isPlaying)
                Destroy(item);
            else
                DestroyImmediate(item);
        }

        void OnDestroy()
        {
            foreach (var material in wrapping)
                Release(material);
            foreach (var mesh in generatedMeshes)
                Release(mesh);
        }

        static MeshCollider AddMeshSurface(MeshFilter filter, bool climbable)
        {
            var go = filter.gameObject;
            go.layer = Layers.Surface;
            if (climbable)
                AddBody(go);
            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            if (climbable)
                go.AddComponent<PowerupLabSurface>();
            return collider;
        }

        static Rigidbody AddBody(GameObject go)
        {
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = true;
            return body;
        }

        public Vector3 SafeSpawnPoint()
        {
            if (spawn)
                return new Vector3(spawn.bounds.center.x, spawn.bounds.max.y + .06f, spawn.bounds.center.z);
            return Center + Vector3.up * 6f;
        }

        public float WaterHeight(Vector3 position) => transform.position.y - 18f;

        public bool IsSheltered(Vector3 position) => Physics.Raycast(position + Vector3.up * .15f,
            Vector3.up, 500f, (1 << Layers.Surface) | (1 << Layers.Block), QueryTriggerInteraction.Ignore);

        public void GetGroundFootprints(List<Footprint> into)
        {
            into.Add(new Footprint {
                center = new Vector2(playArea.center.x, playArea.center.z),
                halfSize = new Vector2(playArea.extents.x, playArea.extents.z),
                rotation = Quaternion.identity
            });
        }

        public string SaveLayout() => Layout;

        public bool LoadLayout(string layout, int expectedBlocks, Pose origin, Vector3 scale)
        {
            if (layout != Layout || !IsFinite(origin.position) || !IsFinite(scale) ||
                scale.x <= 0 || Mathf.Abs(scale.x - scale.y) > .0001f ||
                Mathf.Abs(scale.x - scale.z) > .0001f ||
                !(Mathf.Abs(Quaternion.Dot(origin.rotation, origin.rotation) - 1f) <= .01f))
                return false;
            if (!built)
                Rebuild();
            if (bodies.Count != expectedBlocks)
                return false;
            transform.SetPositionAndRotation(origin.position, origin.rotation);
            transform.localScale = scale;
            Rebuild();
            return true;
        }

        static bool IsFinite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);

        public void SetReplica(bool value)
        {
            replica = value;
            foreach (var panel in panels)
                panel.SetReplica(value);
        }
    }
}
