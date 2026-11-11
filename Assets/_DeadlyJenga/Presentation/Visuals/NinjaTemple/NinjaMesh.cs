using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.NinjaArt
{
    // Vertex-colored geometry. Alpha < 1 marks emissive paper, or sway weight on foliage materials.
    public sealed class NinjaMesh
    {
        readonly List<Vector3> vertices = new List<Vector3>(), normals = new List<Vector3>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int> triangles = new List<int>();
        public int Count => vertices.Count;
        public Matrix4x4 Frame = Matrix4x4.identity;

        public void Tri(Vector3 a, Vector3 b, Vector3 c, Color ca, Color cb, Color cc)
        {
            a = Frame.MultiplyPoint3x4(a); b = Frame.MultiplyPoint3x4(b); c = Frame.MultiplyPoint3x4(c);
            var n = Vector3.Cross(b - a, c - a).normalized; int i = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); normals.Add(n); normals.Add(n); normals.Add(n);
            colors.Add(ca.linear); colors.Add(cb.linear); colors.Add(cc.linear); triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
        }
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color) { Tri(a, b, c, color, color, color); Tri(a, c, d, color, color, color); }
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color ca, Color cb, Color cc, Color cd) { Tri(a, b, c, ca, cb, cc); Tri(a, c, d, ca, cc, cd); }
        public void SmoothQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd, Color ca, Color cb, Color cc, Color cd)
        {
            int i = vertices.Count;
            vertices.Add(Frame.MultiplyPoint3x4(a)); vertices.Add(Frame.MultiplyPoint3x4(b)); vertices.Add(Frame.MultiplyPoint3x4(c)); vertices.Add(Frame.MultiplyPoint3x4(d));
            normals.Add(Frame.MultiplyVector(na).normalized); normals.Add(Frame.MultiplyVector(nb).normalized); normals.Add(Frame.MultiplyVector(nc).normalized); normals.Add(Frame.MultiplyVector(nd).normalized);
            colors.Add(ca.linear); colors.Add(cb.linear); colors.Add(cc.linear); colors.Add(cd.linear);
            triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2); triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 3);
        }
        public void Box(Vector3 center, Vector3 size, Quaternion rotation, Color color) => Box(center, size, rotation, color, color);
        public void Box(Vector3 center, Vector3 size, Quaternion rotation, Color top, Color side)
        {
            var h = size * .5f; var p = new Vector3[8];
            for (int i = 0; i < 8; i++) p[i] = center + rotation * Vector3.Scale(h, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            var shade = side * .82f; shade.a = side.a; var under = side * .55f; under.a = side.a;
            Quad(p[2], p[6], p[7], p[3], top); Quad(p[0], p[1], p[5], p[4], under);
            Quad(p[0], p[2], p[3], p[1], side); Quad(p[4], p[5], p[7], p[6], side);
            Quad(p[0], p[4], p[6], p[2], shade); Quad(p[1], p[3], p[7], p[5], shade);
        }
        // Lathe around +Y. Profile points are (radius, height).
        public void Lathe(Vector3 center, Vector2[] profile, int sides, Color[] colors, float twist = 0)
        {
            for (int s = 0; s < sides; s++)
            {
                float a0 = (s + twist) * Mathf.PI * 2 / sides, a1 = (s + 1 + twist) * Mathf.PI * 2 / sides;
                var d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)); var d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                for (int k = 0; k < profile.Length - 1; k++)
                {
                    var p0 = profile[k]; var p1 = profile[k + 1]; var c0 = colors[Mathf.Min(k, colors.Length - 1)]; var c1 = colors[Mathf.Min(k + 1, colors.Length - 1)];
                    var a = center + d0 * p0.x + Vector3.up * p0.y; var b = center + d1 * p0.x + Vector3.up * p0.y;
                    var c = center + d1 * p1.x + Vector3.up * p1.y; var d = center + d0 * p1.x + Vector3.up * p1.y;
                    var slope = new Vector2(p1.y - p0.y, -(p1.x - p0.x)).normalized;
                    var n0 = (d0 * slope.x + Vector3.up * slope.y); var n1 = (d1 * slope.x + Vector3.up * slope.y);
                    if (p0.x < .001f && p1.x < .001f) continue;
                    SmoothQuad(a, d, c, b, n0, n0, n1, n1, c0, c1, c1, c0);
                }
            }
        }
        public void Cylinder(Vector3 a, Vector3 b, float ra, float rb, int sides, Color color, Color tip)
        {
            var axis = (b - a); float length = axis.magnitude; if (length < .0001f) return;
            var rotation = Quaternion.FromToRotation(Vector3.up, axis / length);
            var saved = Frame; Frame = saved * Matrix4x4.TRS(a, rotation, Vector3.one);
            Lathe(Vector3.zero, new[] { new Vector2(0, 0), new Vector2(ra, 0), new Vector2(rb, length), new Vector2(0, length) }, sides, new[] { color, color, tip, tip });
            Frame = saved;
        }
        // Low-poly blob: perturbed ellipsoid used for rocks and foliage pads.
        public void Blob(Vector3 center, Vector3 radii, int rings, int sides, Color top, Color bottom, int seed, float roughness, float bottomCut = -1)
        {
            var rng = new System.Random(seed); var grid = new Vector3[rings + 1, sides]; var tone = new Color[rings + 1];
            for (int r = 0; r <= rings; r++)
            {
                float v = Mathf.Lerp(bottomCut < 0 ? -1 : bottomCut, 1, r / (float)rings);
                float ring = Mathf.Sqrt(Mathf.Max(0, 1 - v * v));
                tone[r] = Color.Lerp(bottom, top, v * .5f + .5f);
                for (int s = 0; s < sides; s++)
                {
                    float a = s * Mathf.PI * 2 / sides; float j = 1 + ((float)rng.NextDouble() - .5f) * roughness * (r == 0 || r == rings ? .2f : 1);
                    grid[r, s] = center + Vector3.Scale(new Vector3(Mathf.Cos(a) * ring * j, v, Mathf.Sin(a) * ring * j), radii);
                }
            }
            for (int r = 0; r < rings; r++) for (int s = 0; s < sides; s++)
            {
                int t = (s + 1) % sides;
                Quad(grid[r, s], grid[r + 1, s], grid[r + 1, t], grid[r, t], tone[r], tone[r + 1], tone[r + 1], tone[r]);
            }
        }
        public Mesh Build(string name)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds(); mesh.UploadMeshData(true); return mesh;
        }
    }

    // Billboard halos and world-space light cards for the additive glow shader.
    public sealed class NinjaGlowMesh
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<Vector4> data = new List<Vector4>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int> triangles = new List<int>();
        public int Count => vertices.Count;
        static readonly Vector2[] Corners = { new Vector2(-1, -1), new Vector2(-1, 1), new Vector2(1, 1), new Vector2(1, -1) };
        public void Halo(Vector3 center, float size, Color color, float flicker = 0, float shape = 0)
        {
            int i = vertices.Count;
            for (int k = 0; k < 4; k++) { vertices.Add(center); uv.Add(Corners[k]); data.Add(new Vector4(size, 1, flicker, shape)); colors.Add(color); }
            triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2); triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 3);
        }
        public void Card(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color, float shape)
        {
            int i = vertices.Count; var p = new[] { a, b, c, d };
            for (int k = 0; k < 4; k++) { vertices.Add(p[k]); uv.Add(Corners[k]); data.Add(new Vector4(1, 0, 0, shape)); colors.Add(color); }
            triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2); triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 3);
        }
        public Mesh Build(string name, float pad)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetUVs(1, data); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds(); var b = mesh.bounds; b.Expand(pad * 2); mesh.bounds = b; mesh.UploadMeshData(true); return mesh;
        }
    }

    public static class NinjaPalette
    {
        public static readonly Color Vermilion = new Color(.62f, .075f, .045f), DeepRed = new Color(.36f, .035f, .03f), Lacquer = new Color(.045f, .04f, .05f),
            Gold = new Color(.86f, .62f, .26f), Tile = new Color(.075f, .08f, .1f), TileDark = new Color(.05f, .055f, .07f), Soffit = new Color(.2f, .11f, .07f),
            Stone = new Color(.2f, .21f, .25f), StoneDark = new Color(.12f, .125f, .15f), Gravel = new Color(.25f, .25f, .29f), Moss = new Color(.12f, .19f, .14f),
            Bamboo = new Color(.2f, .38f, .3f), BambooLeaf = new Color(.12f, .26f, .2f), Pine = new Color(.08f, .16f, .13f), Maple = new Color(.62f, .07f, .06f),
            Wood = new Color(.24f, .14f, .09f);
        public static Color Paper(Color c, float glow) { c.a = 1 - glow; return c; }
        public static Color Sway(Color c, float weight) { c.a = weight; return c; }
        public static Color Vary(Color c, System.Random rng, float amount)
        {
            float k = 1 + ((float)rng.NextDouble() - .5f) * 2 * amount; var r = new Color(c.r * k, c.g * k, c.b * k, c.a); return r;
        }
    }
}
