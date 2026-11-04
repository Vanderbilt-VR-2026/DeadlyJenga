using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.Destruction
{
    // Procedural crack art, made once at startup.
    //  - Textures: fractures start at the edges and run inward in kinked straight segments, branch, and
    //    taper to hairlines. Dark core, thin chipped rim, faint darkening around big cracks.
    //    Each pixel remembers when it cracks, so one pattern is baked into growth stages.
    //  - Overlay meshes: a copy of a block's own mesh pushed out slightly, with box-projected UVs that keep
    //    the block's proportions, so cracks hug rounded edges and never stretch.
    public static class CrackArt
    {
        public static readonly float[] StageReveal = { .3f, .55f, .8f, 1.01f };

        public static Texture2D[] Stages(int seed, int size = 512)
        {
            int S = size;
            var rng = new System.Random(seed);
            float R() => (float)rng.NextDouble();
            var core = new float[S * S]; var rim = new float[S * S]; var halo = new float[S * S]; var ord = new float[S * S];
            for (int i = 0; i < ord.Length; i++) ord[i] = 2;

            // Walker: x, y, heading, travelled, width, max length
            var walkers = new List<float[]>();
            for (int s = 0; s < 6; s++)
            {
                int edge = s % 3; float x, y, a;
                if (edge == 0) { x = (.08f + R() * .84f) * S; y = 0; a = 90; }
                else if (edge == 1) { x = (.08f + R() * .84f) * S; y = S - 1; a = 270; }
                else { x = R() < .5f ? 0 : S - 1; y = (.25f + R() * .5f) * S; a = x < 1 ? 0 : 180; }
                walkers.Add(new[] { x, y, a + (R() - .5f) * 70, 0, 6.5f * S / 512f, S * (.5f + R() * .5f) });
            }
            int guard = 0;
            while (walkers.Count > 0 && guard++ < 600)
            {
                var w = walkers[walkers.Count - 1]; walkers.RemoveAt(walkers.Count - 1);
                float x = w[0], y = w[1], heading = w[2], travelled = w[3], width = w[4], maxLen = w[5];
                while (travelled < maxLen)
                {
                    float seg = (9 + R() * 24) * S / 512f;
                    float rad = (heading + (R() - .5f) * 64) * Mathf.Deg2Rad;
                    float nx = x + Mathf.Cos(rad) * seg, ny = y + Mathf.Sin(rad) * seg;
                    Segment(S, core, rim, halo, ord, x, y, nx, ny, travelled / maxLen, (travelled + seg) / maxLen, width);
                    x = nx; y = ny; travelled += seg;
                    if (x < -5 || y < -5 || x > S + 5 || y > S + 5) break;
                    if (R() < .26f && width > 1.4f)
                        walkers.Add(new[] { x, y, heading + (R() < .5f ? -1 : 1) * (25 + R() * 35), travelled, width * .55f, maxLen * (.7f + R() * .3f) });
                    heading += (R() - .5f) * 18;
                }
            }

            var coreC = new Color(.05f, .02f, .02f); var rimC = new Color(1, .93f, .9f); var haloC = new Color(.18f, .05f, .05f);
            var result = new Texture2D[StageReveal.Length];
            var px = new Color32[S * S];
            for (int st = 0; st < result.Length; st++)
            {
                for (int i = 0; i < px.Length; i++)
                {
                    px[i] = default;
                    float o = ord[i]; if (o > 1.5f) continue;
                    float f = Mathf.Clamp01((StageReveal[st] - o) / .04f);
                    float ca = core[i] * f, ra = rim[i] * .28f * f, ha = halo[i] * .22f * f;
                    Color col = haloC; float a = ha;
                    float na = ra + a * (1 - ra); if (na > .0001f) col = (rimC * ra + col * a * (1 - ra)) / na; a = na;
                    na = ca + a * (1 - ca); if (na > .0001f) col = (coreC * ca + col * a * (1 - ca)) / na; a = na;
                    if (a > .003f) px[i] = new Color(col.r, col.g, col.b, a);
                }
                var tex = new Texture2D(S, S, TextureFormat.RGBA32, true) { name = "Cracks stage " + st, wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
                tex.SetPixels32(px); tex.Apply(true, true);
                result[st] = tex;
            }
            return result;
        }

        static void Segment(int S, float[] core, float[] rim, float[] halo, float[] ord, float x, float y, float nx, float ny, float t0, float t1, float width)
        {
            int pad = (int)(width * 2.5f) + 6;
            int minX = Mathf.Max(0, (int)Mathf.Min(x, nx) - pad), maxX = Mathf.Min(S - 1, (int)Mathf.Max(x, nx) + pad);
            int minY = Mathf.Max(0, (int)Mathf.Min(y, ny) - pad), maxY = Mathf.Min(S - 1, (int)Mathf.Max(y, ny) + pad);
            float dx = nx - x, dy = ny - y, len2 = Mathf.Max(.001f, dx * dx + dy * dy);
            for (int py = minY; py <= maxY; py++)
            for (int px = minX; px <= maxX; px++)
            {
                float qx = px + .5f - x, qy = py + .5f - y, h = Mathf.Clamp01((qx * dx + qy * dy) / len2);
                float ex = qx - dx * h, ey = qy - dy * h, d = Mathf.Sqrt(ex * ex + ey * ey), t = Mathf.Lerp(t0, t1, h);
                float wc = Mathf.Max(.8f, width * Mathf.Pow(1 - t, .85f));
                float c = Mathf.Clamp01(wc * .5f + .5f - d);
                float r = Mathf.Clamp01(wc * .5f + .5f + .35f * wc - d) * Mathf.Clamp01((wc - 1.5f) / 3f);
                float hl = Mathf.Clamp01(1 - (d - wc * .5f) / (2 + wc * 1.6f)) * Mathf.Clamp01(wc / 4f);
                int i = py * S + px;
                if (c > core[i]) core[i] = c;
                if (r > rim[i]) rim[i] = r;
                if (hl > halo[i]) halo[i] = hl;
                if ((c > .01f || hl > .05f) && t < ord[i]) ord[i] = t;
            }
        }

        static readonly Dictionary<(Mesh, Vector3Int), Mesh> overlays = new Dictionary<(Mesh, Vector3Int), Mesh>();

        // A crack shell for `mesh` drawn at world scale `scale`. Shared by every block with the same mesh and size.
        public static Mesh Overlay(Mesh mesh, Vector3 scale)
        {
            var key = (mesh, Vector3Int.RoundToInt(scale * 100));
            if (overlays.TryGetValue(key, out var cached) && cached) return cached;
            var v = mesh.vertices; var n = mesh.normals;
            if (n == null || n.Length != v.Length) { mesh = Object.Instantiate(mesh); mesh.RecalculateNormals(); n = mesh.normals; }
            float big = Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
            var uv = new Vector2[v.Length]; var pushed = new Vector3[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                var nn = n[i]; var p = Vector3.Scale(v[i], scale);
                float ax = Mathf.Abs(nn.x), ay = Mathf.Abs(nn.y), az = Mathf.Abs(nn.z);
                var u = ax >= ay && ax >= az ? new Vector2(p.z, p.y) : ay >= az ? new Vector2(p.x, p.z) : new Vector2(p.x, p.y);
                uv[i] = u / big + new Vector2(.5f, .5f);
                pushed[i] = v[i] + new Vector3(nn.x / scale.x, nn.y / scale.y, nn.z / scale.z) * .006f;
            }
            var shell = new Mesh { name = "Crack shell" };
            if (v.Length > 65000) shell.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            shell.vertices = pushed; shell.normals = n; shell.uv = uv; shell.triangles = mesh.triangles;
            shell.RecalculateBounds();
            return overlays[key] = shell;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => overlays.Clear();
    }
}
