using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // One Jenga structure on the campus, placed by the builder from gameplay.json.
    [Serializable]
    public sealed class CampusTower
    {
        public string id = "tower";
        [Tooltip("tower = one Jenga tower (CastleLayout single tower); castle = a small block castle (CastleLayout castle).")]
        public string kind = "tower";
        [Tooltip("Map-local position; y = top of the plinth (the highest ground under the footprint).")]
        public Vector3 position;
        public float yaw;
        [Tooltip("tower: courses (2-48). castle: keep courses (wall and corner towers are derived from it).")]
        public int size = 18;
    }

    // Towers and castles use the existing castle blocks and CastleLayout, the same way CastleGenerator does.
    // Each one stands on a flat plinth (a static Surface box) so uneven lawns never tip the bottom course.
    public sealed partial class VanderbiltCampusMap
    {
        [Header("Towers (data: gameplay.json)")]
        public CampusTower[] towers = Array.Empty<CampusTower>();
        [Tooltip("The unit castle block prefab (Rigidbody + BoxCollider + CastleBlock), as CastleGenerator uses.")]
        public GameObject blockPrefab;
        public Material cornerMaterial, wallMaterial, keepMaterial, plinthMaterial;
        public Vector3 blockSize = new Vector3(6, 2, 2);
        public float blockMass = 40;

        readonly List<Rigidbody> bodies = new List<Rigidbody>();
        Transform generated;
        bool towersBuilt, replica;
        int retireCursor;

        public IReadOnlyList<Rigidbody> Blocks => bodies;

        // The CastleLayout settings for one structure. Pure data, so every peer computes the same blocks.
        public CastleSettings SettingsFor(CampusTower tower)
        {
            var s = new CastleSettings { blockSize = blockSize, moduleGap = .08f };
            if (tower.kind == "castle")
            {
                int keep = Mathf.Clamp(tower.size, 8, 30);
                s.modulesPerSide = 5; s.keepCourses = keep;
                s.wallCourses = Mathf.Clamp(keep / 2, 2, 40); s.cornerCourses = Mathf.Clamp(keep * 2 / 3, 2, 48);
            }
            else { s.singleTower = true; s.modulesPerSide = 3; s.cornerCourses = Mathf.Clamp(tower.size, 2, 48); }
            return s;
        }

        // Footprint half-width of one structure (for the plinth and spacing).
        public float HalfWidth(CampusTower tower) => CastleLayout.Footprint(SettingsFor(tower)) * .5f + 1;

        public int ExpectedBlockCount()
        {
            int count = 0;
            if (towers != null) foreach (var t in towers) if (t != null) count += CastleLayout.Build(SettingsFor(t)).Count;
            return count;
        }

        float TowersTop
        {
            get
            {
                float top = float.NegativeInfinity;
                if (towers != null)
                    foreach (var t in towers)
                        if (t != null) top = Mathf.Max(top, t.position.y + CastleLayout.HeightCourses(SettingsFor(t)) * blockSize.y);
                return top;
            }
        }

        // A short stable text for SaveLayout (FNV-1a hash; string.GetHashCode can differ between machines).
        string TowerToken()
        {
            uint hash = 2166136261;
            void Add(string text) { foreach (char c in text) { hash ^= c; hash *= 16777619; } }
            if (towers != null)
                foreach (var t in towers)
                    if (t != null) Add($"{t.id}|{t.kind}|{t.size}|{t.position.x:F2}|{t.position.y:F2}|{t.position.z:F2}|{t.yaw:F1};");
            return "towers" + (towers?.Length ?? 0) + "-" + hash.ToString("x8");
        }

        // Runtime only: the prefab never stores blocks.
        void BuildTowers()
        {
            if (!Application.isPlaying) return;
            if (generated) { generated.gameObject.SetActive(false); Destroy(generated.gameObject); }
            bodies.Clear(); retireCursor = 0; towersBuilt = true;
            generated = new GameObject("Generated campus towers").transform;
            generated.SetParent(transform, false);
            if (towers == null || towers.Length == 0) return;
            if (!blockPrefab || !blockPrefab.GetComponent<Rigidbody>() || !blockPrefab.GetComponent<BoxCollider>())
            { Debug.LogError("Campus towers need the castle block prefab (Rigidbody + BoxCollider).", this); return; }
            foreach (var tower in towers) if (tower != null) BuildOne(tower);
            SetReplica(replica);
        }

        void BuildOne(CampusTower tower)
        {
            var settings = SettingsFor(tower);
            var root = new GameObject("Tower " + tower.id).transform;
            root.SetParent(generated, false);
            root.localPosition = tower.position; root.localRotation = Quaternion.Euler(0, tower.yaw, 0);
            Plinth(root, HalfWidth(tower));
            foreach (var p in CastleLayout.Build(settings))
            {
                var go = Instantiate(blockPrefab, root);
                go.name = $"{tower.id}_{p.Section}_Course{p.Course:00}_Block{bodies.Count:000}";
                go.transform.localPosition = p.Position; go.transform.localRotation = p.Rotation;
                var size = p.BlockSize(settings); go.transform.localScale = size;
                var body = go.GetComponent<Rigidbody>();
                body.position = go.transform.position; body.rotation = go.transform.rotation;
                var block = go.GetComponent<CastleBlock>();
                if (block)
                {
                    block.sectionMaterial = p.Section == CastleSection.Corner ? cornerMaterial : p.Section == CastleSection.Keep ? keepMaterial : wallMaterial;
                    block.RefreshAppearance(); block.SyncSurface();
                }
                body.mass = Mathf.Max(1, blockMass * size.x * size.y * size.z / (blockSize.x * blockSize.y * blockSize.z));
                body.solverIterations = 20; body.solverVelocityIterations = 8; body.sleepThreshold = .005f;
                bodies.Add(body);
            }
        }

        // A flat static slab whose top is the tower's base, sunk 1.5 m into the lawn.
        void Plinth(Transform root, float half)
        {
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "Plinth"; slab.layer = Layers.Surface;
            slab.transform.SetParent(root, false);
            slab.transform.localPosition = new Vector3(0, -.75f, 0); slab.transform.localScale = new Vector3(half * 2, 1.5f, half * 2);
            if (plinthMaterial) slab.GetComponent<Renderer>().sharedMaterial = plinthMaterial;
        }

        // On peers the host's blocks are followed, not simulated (same rules as CastleGenerator).
        public void SetReplica(bool value)
        {
            replica = value;
            var prefabBody = blockPrefab ? blockPrefab.GetComponent<Rigidbody>() : null;
            foreach (var body in bodies)
            {
                if (!body) continue;
                if (value) body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                body.isKinematic = value;
                if (!value && prefabBody) body.collisionDetectionMode = prefabBody.collisionDetectionMode;
                body.interpolation = value ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
            }
        }

        // A few blocks per tick: blocks that fell below the kill floor are switched off.
        void RetireFallenBlocks()
        {
            if (replica || bodies.Count == 0) return;
            float floor = WaterHeight(transform.position) - 3;
            for (int n = 0; n < Mathf.Min(32, bodies.Count); n++)
            {
                var body = bodies[retireCursor++ % bodies.Count];
                if (body && body.gameObject.activeSelf && body.position.y < floor) body.gameObject.SetActive(false);
            }
        }
    }
}
