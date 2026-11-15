using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.Presentation
{
    // Two expanding walls of whitewater and a lingering foam bed share one mesh per impact.
    internal sealed class CandyBlockWhitewater
    {
        const int Sectors = 48, Rows = 9, MaxBursts = 10;
        const float Lifetime = 2.35f;
        static readonly int Shape = Shader.PropertyToID("_ImpactShape");
        static readonly int Motion = Shader.PropertyToID("_ImpactMotion");
        static readonly int Grain = Shader.PropertyToID("_FoamGrain");

        readonly Transform parent;
        readonly MaterialPropertyBlock properties = new();
        readonly List<Burst> bursts = new();
        Mesh mesh;
        Material material;
        Texture2D grain;

        sealed class Burst
        {
            public MeshRenderer renderer;
            public float born;
            public Vector4 shape, motion;
        }

        public CandyBlockWhitewater(Transform parent) => this.parent = parent;

        public void Emit(Vector3 position, float radius, float energy, Vector2 drift)
        {
            if (!material && !Build()) return;
            Burst available = null;
            Burst oldest = null;
            foreach (var burst in bursts)
            {
                if (!burst.renderer.gameObject.activeSelf)
                {
                    available = burst;
                    break;
                }
                if (oldest == null || burst.born < oldest.born) oldest = burst;
            }
            var active = available ?? (bursts.Count < MaxBursts ? AddBurst() : oldest);
            active.born = Time.time;
            active.shape = new Vector4(Mathf.Clamp(radius, .32f, 2.4f), energy, Random.Range(1f, 99f), 0);
            active.motion = new Vector4(drift.x, drift.y, 0, 0);
            active.renderer.transform.position = position;
            active.renderer.gameObject.SetActive(true);
            Apply(active, 0);
        }

        public void Tick()
        {
            foreach (var burst in bursts)
            {
                if (!burst.renderer.gameObject.activeSelf) continue;
                float age = Time.time - burst.born;
                if (age >= Lifetime) burst.renderer.gameObject.SetActive(false);
                else Apply(burst, age);
            }
        }

        public void Clear()
        {
            foreach (var burst in bursts) burst.renderer.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            foreach (var burst in bursts)
                if (burst.renderer) Object.Destroy(burst.renderer.gameObject);
            bursts.Clear();
            if (mesh) Object.Destroy(mesh);
            if (material) Object.Destroy(material);
            if (grain) Object.Destroy(grain);
        }

        void Apply(Burst burst, float age)
        {
            burst.shape.w = age;
            properties.SetVector(Shape, burst.shape);
            properties.SetVector(Motion, burst.motion);
            burst.renderer.SetPropertyBlock(properties);
        }

        Burst AddBurst()
        {
            var go = new GameObject("Block impact whitewater");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            var burst = new Burst { renderer = renderer };
            bursts.Add(burst);
            return burst;
        }

        bool Build()
        {
            var shader = Resources.Load<Shader>("CandyScenery/CandyBlockWhitewater");
            if (!shader || !shader.isSupported) return false;
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var layers = new List<Vector2>();
            var triangles = new List<int>();
            for (int layer = 0; layer < 3; layer++)
            {
                int start = vertices.Count;
                for (int y = 0; y <= Rows; y++)
                for (int x = 0; x <= Sectors; x++)
                {
                    vertices.Add(Vector3.zero);
                    uv.Add(new Vector2(x / (float)Sectors, y / (float)Rows));
                    layers.Add(new Vector2(layer, 0));
                    if (x == Sectors || y == Rows) continue;
                    int i = start + y * (Sectors + 1) + x;
                    triangles.Add(i);
                    triangles.Add(i + Sectors + 1);
                    triangles.Add(i + 1);
                    triangles.Add(i + 1);
                    triangles.Add(i + Sectors + 1);
                    triangles.Add(i + Sectors + 2);
                }
            }
            mesh = new Mesh { name = "Block whitewater sheets and foam" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetUVs(1, layers);
            mesh.SetTriangles(triangles, 0, false);
            mesh.bounds = new Bounds(Vector3.up, new Vector3(27, 12, 27));
            grain = BuildGrain();
            material = new Material(shader) { name = "Block impact whitewater" };
            material.SetTexture(Grain, grain);
            return true;
        }

        static Texture2D BuildGrain()
        {
            const int side = 128;
            var texture = new Texture2D(side, side, TextureFormat.RGBA32, true, true)
            {
                name = "Whitewater grain",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Mirror
            };
            var pixels = new Color32[side * side];
            for (int y = 0; y < side; y++)
            for (int x = 0; x < side; x++)
            {
                float broad = Mathf.PerlinNoise(x * .073f + 3, y * .09f + 9);
                float fine = Mathf.PerlinNoise(x * .27f + 30, y * .39f);
                float cellular = 1 - Mathf.Clamp01(Mathf.Abs(fine - .5f) * 3.6f);
                pixels[y * side + x] = (Color32)new Color(broad, fine, cellular, 1);
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }
    }
}
