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
        const string Layout = "underneath-the-tree-v6-underpass";

        [SerializeField] GameObject model = null, room = null, locomotive = null;
        [SerializeField] Material paper = null;
        readonly List<Rigidbody> bodies = new List<Rigidbody>();
        readonly List<Mesh> generatedMeshes = new List<Mesh>();
        readonly List<UnderneathTreeGiftPanel> panels = new List<UnderneathTreeGiftPanel>();
        readonly List<Material> wrapping = new List<Material>();
        Transform built;
        readonly List<Collider> spawnTops = new List<Collider>();
        MeshCollider skirt;
        Vector3 fallbackSpawn;
        float nextDebrisCleanup;
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
            var rail = new List<Vector3>();
            var gifts = new SortedDictionary<string, List<MeshFilter>>(StringComparer.Ordinal);
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
                else if (name.StartsWith("Present ", StringComparison.Ordinal))
                {
                    int suffix = name.IndexOf(' ', "Present ".Length);
                    if (suffix == -1)
                        continue;
                    var giftName = name.Substring(0, suffix);
                    if (!gifts.TryGetValue(giftName, out var parts))
                        gifts.Add(giftName, parts = new List<MeshFilter>());
                    parts.Add(filter);
                    playArea.Encapsulate(filter.GetComponent<Renderer>().bounds);
                }
                else if (name.Contains("kill floor"))
                {
                    ResizeApprovedRug(filter);
                    skirt = AddMeshSurface(filter);
                }
                else if (name.StartsWith("Wood Planks", StringComparison.Ordinal) || name == "Tree Skirt rim")
                    AddMeshSurface(filter);
            }

            // The imported model supplies the room and rug; the approved course owns every gift.
            foreach (var pair in gifts)
                foreach (var part in pair.Value)
                    part.gameObject.SetActive(false);
            foreach (var filter in filters)
                if (filter.name.StartsWith("Track ", StringComparison.Ordinal) || filter.name.StartsWith("Gap present", StringComparison.Ordinal))
                    filter.gameObject.SetActive(false);
            BuildGiftRoutes();
            Physics.SyncTransforms();
            BuildApprovedTrack();
            ScaleApprovedCourse(layout.transform);
            BuildToys();
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
            giftBearings.Clear();
            toyHolds.Clear();
            foreach (var mesh in generatedMeshes)
                Release(mesh);
            generatedMeshes.Clear();
            spawnTops.Clear();
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

        static MeshCollider AddMeshSurface(MeshFilter filter)
        {
            var go = filter.gameObject;
            go.layer = Layers.Surface;
            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
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
            int surface = (1 << Layers.Block) | (1 << Layers.Surface);
            foreach (var top in spawnTops)
            {
                if (!top || !top.gameObject.activeInHierarchy || !top.attachedRigidbody.isKinematic)
                    continue;
                var bounds = top.bounds;
                var start = new Vector3(bounds.center.x, bounds.max.y + .95f, bounds.center.z);
                if (Physics.Raycast(start, Vector3.down, out var hit, 1.12f, surface, QueryTriggerInteraction.Ignore) &&
                    hit.collider == top && hit.normal.y > .92f)
                    return hit.point + Vector3.up * .07f;
            }
            return transform.TransformPoint(fallbackSpawn);
        }

        void FixedUpdate()
        {
            if (replica || (Services.Session is IWorldAuthority authority && !authority.HasWorldAuthority))
                return;
            foreach (var bearing in giftBearings)
                bearing.Check();
            CheckToyHolds();
            if (Time.time < nextDebrisCleanup)
                return;
            nextDebrisCleanup = Time.time + .8f;
            foreach (var panel in panels)
                if (panel && panel.IsLoose && panel.gameObject.activeSelf &&
                    panel.Body.position.y < WaterHeight(panel.Body.position) - 22f)
                    panel.gameObject.SetActive(false);
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
