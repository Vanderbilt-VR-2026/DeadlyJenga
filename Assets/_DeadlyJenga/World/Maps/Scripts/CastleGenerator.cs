using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    public class CastleGenerator : MonoBehaviour, IWorldQuery, ISyncedWorld
    {
        public CastleSettings settings = new CastleSettings();
        public GameObject blockPrefab;
        public Material cornerMaterial, wallMaterial, keepMaterial;
        public Transform foundation;
        public bool generateOnStart = true;
        public float mass = 40;
        public readonly List<Rigidbody> Bodies = new List<Rigidbody>();
        [SerializeField, HideInInspector] Transform generatedRoot;
        public float LastBuildMilliseconds { get; private set; }
        public int Revision { get; private set; }

        [Header("World")]
        public float waterHeight = -.12f;
        [Tooltip("Anything that can be eliminated is eliminated below this height.")]
        public float oceanKillHeight = -6;
        readonly Collider[] fallen = new Collider[256];
        float nextKillCheck;

        void Start() { if (generateOnStart) Rebuild(); }
        void OnEnable() => Services.World = this;
        void OnDisable() { if (ReferenceEquals(Services.World, this)) Services.World = null; }

        // IWorldQuery
        public float WaterHeight(Vector3 position) => waterHeight;
        public bool IsSheltered(Vector3 position) =>
            Physics.Raycast(position + Vector3.up * .1f, Vector3.up, 500, 1 << Layers.Block, QueryTriggerInteraction.Ignore);

        public IReadOnlyList<Rigidbody> Blocks => Bodies;
        public Vector3 Center => transform.position;
        public float TopHeight => transform.position.y + settings.keepCourses * settings.blockSize.y;
        public float Radius => CastleLayout.Footprint(settings) * .5f;

        // ISyncedWorld (Revision is declared above)
        public Transform Root => transform;
        public string SaveLayout() => JsonUtility.ToJson(settings);
        public bool LoadLayout(string layout, int expectedBlocks, Pose origin, Vector3 scale)
        {
            var incoming = JsonUtility.FromJson<CastleSettings>(layout);
            if (incoming == null || CastleLayout.Build(incoming).Count != expectedBlocks) return false;
            settings = incoming;
            transform.SetPositionAndRotation(origin.position, origin.rotation);
            transform.localScale = scale;
            Rebuild();
            return true;
        }
        public void SetReplica(bool replica)
        {
            foreach (var body in Bodies)
            {
                if (!body) continue;
                body.isKinematic = replica;
                body.interpolation = replica ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
            }
        }

        // Top of the highest upright block, or the foundation if none are left.
        public Vector3 SafeSpawnPoint()
        {
            Physics.SyncTransforms();
            Collider best = null; float highest = float.NegativeInfinity;
            foreach (var body in Bodies)
            {
                if (!body || Vector3.Dot(body.transform.up, Vector3.up) < .95f) continue;
                var col = body.GetComponent<Collider>();
                if (col.bounds.max.y > highest) { highest = col.bounds.max.y; best = col; }
            }
            return best ? best.attachedRigidbody.position + Vector3.up * (best.bounds.extents.y + .08f)
                : foundation.position + Vector3.up * .6f;
        }

        // The ocean: eliminate anything that falls below the kill height.
        void FixedUpdate()
        {
            if (!Application.isPlaying || Time.time < nextKillCheck) return;
            nextKillCheck = Time.time + .1f;
            var center = new Vector3(transform.position.x, oceanKillHeight - 500, transform.position.z);
            int mask = ~((1 << Layers.Block) | (1 << Layers.Surface));
            int count = Physics.OverlapBoxNonAlloc(center, new Vector3(2000, 500, 2000), fallen, Quaternion.identity, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var target = fallen[i].GetComponentInParent<IDamageable>();
                if (target != null && target.Health > 0) target.Eliminate(gameObject);
            }
        }

        public void Rebuild()
        {
            Revision++;
            var layout = CastleLayout.Build(settings);
            if (!blockPrefab || !blockPrefab.GetComponent<Rigidbody>() || !blockPrefab.GetComponent<BoxCollider>())
                throw new System.InvalidOperationException("Assign a unit cube prefab with Rigidbody and BoxCollider.");
            float started = Time.realtimeSinceStartup;
            if (generatedRoot)
            {
                generatedRoot.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(generatedRoot.gameObject); else DestroyImmediate(generatedRoot.gameObject);
            }
            Bodies.Clear();
            generatedRoot = new GameObject("Generated Castle Blocks").transform;
            generatedRoot.SetParent(transform, false);
            if (foundation)
            {
                float size = CastleLayout.Footprint(settings) + 4;
                foundation.localScale = new Vector3(size, 1, size);
            }
            foreach (var p in layout)
            {
                var go = Instantiate(blockPrefab, generatedRoot);
                go.name = $"{p.Section}_Course{p.Course:00}_Block{Bodies.Count:000}";
                go.transform.localPosition = p.Position;
                go.transform.localRotation = p.Rotation;
                go.transform.localScale = settings.blockSize;
                var body = go.GetComponent<Rigidbody>();
                // Teleport the physics pose too: interpolated bodies otherwise retain the
                // prefab's origin until the first physics step, including during top spawn.
                body.position = go.transform.position; body.rotation = go.transform.rotation;
                var block = go.GetComponent<CastleBlock>();
                if (block)
                {
                    block.sectionMaterial = p.Section == CastleSection.Corner ? cornerMaterial :
                        p.Section == CastleSection.Keep ? keepMaterial : wallMaterial;
                    block.RefreshAppearance(); block.SyncSurface();
                }
                body.mass = Mathf.Max(1, mass);
                body.solverIterations = 20;
                body.solverVelocityIterations = 8;
                body.sleepThreshold = .005f;
                Bodies.Add(body);
            }
            Physics.SyncTransforms();
            // Let gravity settle the tower naturally: do not hide instability by sleeping it at spawn.
            LastBuildMilliseconds = (Time.realtimeSinceStartup - started) * 1000;
        }
    }
}
