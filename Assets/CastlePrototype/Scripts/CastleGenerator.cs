using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public class CastleGenerator : MonoBehaviour
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

        void Start() { if (generateOnStart) Rebuild(); }

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
