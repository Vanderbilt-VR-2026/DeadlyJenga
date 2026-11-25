using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyArt
{
    public sealed class TwinCalderaScenery : MonoBehaviour
    {
        public Material lava, sky, skyline;
        Mesh surface, mountains, plumes;

        void Awake()
        {
            if (Application.isPlaying && !GetComponent<DeadlyJenga.CandyAudio.CalderaSoundscape>())
                gameObject.AddComponent<DeadlyJenga.CandyAudio.CalderaSoundscape>();
            // Same static grid as the candy ocean: dense near the map for the swells, a cheap ring to the horizon.
            const float size = 3200;
            surface = CandyOceanSurface.Create(Application.isMobilePlatform ? 64 : 96, size, 110);
            surface.name = "Lava surface";
            Show("Lava ocean", surface, lava).transform.localScale = new Vector3(size,1,size);

            // The twelve eroded volcanoes and their ash plumes, exported from Blender.
            var data = Resources.Load<TextAsset>("TwinCaldera/CalderaVolcanoes");
            if (!data) return;
            int stride = Application.isMobilePlatform ? 2 : 1;
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            var plumeVertices = new List<Vector3>();
            var plumeUv = new List<Vector2>();
            var plumeShape = new List<Vector2>();
            var plumeTriangles = new List<int>();
            foreach (var raw in data.text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;
                var f = line.Split('|');
                if (f[0] == "V") AddVolcano(f, stride, vertices, colors, triangles);
                else if (f[0] == "P") AddPlume(f, plumeVertices, plumeUv, plumeShape, plumeTriangles);
            }
            mountains = new Mesh { name = "Caldera distant volcanoes", indexFormat = IndexFormat.UInt32 };
            mountains.SetVertices(vertices);
            mountains.SetColors(colors);
            mountains.SetTriangles(triangles,0);
            mountains.RecalculateNormals();
            Show("Volcanic skyline", mountains, skyline);
            var plumeMaterial = Resources.Load<Material>("TwinCaldera/AshPlume");
            if (!plumeMaterial || plumeVertices.Count == 0) return;
            plumes = new Mesh { name = "Caldera ash plumes" };
            plumes.SetVertices(plumeVertices);
            plumes.SetUVs(0,plumeUv);
            plumes.SetUVs(1,plumeShape);
            plumes.SetTriangles(plumeTriangles,0);
            // The strips turn to face the camera in the shader, so widen the culling box.
            var bounds = plumes.bounds; bounds.Expand(300); plumes.bounds = bounds;
            Show("Ash plumes", plumes, plumeMaterial);
        }

        static float Read(string s) => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);

        // One volcano: a closed grid of rings (summit to foot) by spokes (around).
        static void AddVolcano(string[] f, int stride, List<Vector3> vertices, List<Color> colors, List<int> triangles)
        {
            int rings = int.Parse(f[2]), spokes = int.Parse(f[3]);
            var points = f[4].Split(';');
            var keptRings = new List<int>();
            for (int i = 0; i < rings; i += stride) keptRings.Add(i);
            if (keptRings[keptRings.Count-1] != rings-1) keptRings.Add(rings-1);
            int around = spokes / stride, start = vertices.Count;
            foreach (int i in keptRings)
                for (int j = 0; j < around; j++)
                {
                    var c = points[i*spokes + j*stride].Split(',');
                    vertices.Add(new Vector3(Read(c[0]), Read(c[1]), Read(c[2])));
                    colors.Add(new Color(Read(c[3]), Read(c[4]), 0, 1));
                }
            for (int i = 0; i < keptRings.Count-1; i++)
                for (int j = 0; j < around; j++)
                {
                    int a = start + i*around + j, b = start + i*around + (j+1) % around;
                    int c = b + around, d = a + around;
                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                    triangles.Add(a); triangles.Add(c); triangles.Add(d);
                }
        }

        // One ash column: a bent vertical strip that drifts downwind as it rises.
        static void AddPlume(string[] f, List<Vector3> vertices, List<Vector2> uv, List<Vector2> shape, List<int> triangles)
        {
            var b = f[2].Split(',');
            var crater = new Vector3(Read(b[0]), Read(b[1]), Read(b[2]));
            float height = Read(f[3]), r0 = Read(f[4]), r1 = Read(f[5]), glow = Read(f[8]);
            var wind = new Vector3(Read(f[6]), 0, Read(f[7]));
            const int Segments = 12;
            int start = vertices.Count;
            for (int s = 0; s <= Segments; s++)
            {
                float t = (float)s / Segments;
                var axis = crater + Vector3.up * (height * t) + wind * (t * t);
                float width = Mathf.Lerp(r0, r1, Mathf.Pow(t, .7f)) * 1.25f;
                for (int side = -1; side <= 1; side += 2)
                {
                    vertices.Add(axis); uv.Add(new Vector2(side, t)); shape.Add(new Vector2(width, glow));
                }
                if (s == 0) continue;
                int k = start + s*2;
                triangles.Add(k-2); triangles.Add(k); triangles.Add(k+1);
                triangles.Add(k-2); triangles.Add(k+1); triangles.Add(k-1);
            }
        }

        GameObject Show(string label, Mesh mesh, Material material)
        {
            var part = new GameObject(label);
            part.transform.SetParent(transform,false);
            part.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.receiveShadows=false;
            return part;
        }

        void OnDestroy()
        {
            if (surface) Destroy(surface);
            if (mountains) Destroy(mountains);
            if (plumes) Destroy(plumes);
        }
    }
}
