using UnityEngine;
using P = DeadlyJenga.NinjaArt.NinjaPalette;

namespace DeadlyJenga.NinjaArt
{
    // Static moonlit scenery, built once in map space. Ground sits at y = 0, water at waterY.
    public static class NinjaTempleScenery
    {
        public const float Half = 35, Ring = 24;
        public static Vector3 MoonFlat => new Vector3(Mathf.Sin(25 * Mathf.Deg2Rad), 0, Mathf.Cos(25 * Mathf.Deg2Rad));

        public static void Ground(NinjaMesh m, float waterY)
        {
            var rng = new System.Random(11);
            for (int x = 0; x < 35; x++) for (int z = 0; z < 35; z++)
            {
                float x0 = -Half + x * 2, z0 = -Half + z * 2; var c = new Vector2(x0 + 1, z0 + 1);
                bool path = Mathf.Abs(c.x) < 2.2f || Mathf.Abs(c.y) < 2.2f || Mathf.Abs(Mathf.Abs(c.x) - Ring) < 2.2f && Mathf.Abs(c.y) < Ring + 2 || Mathf.Abs(Mathf.Abs(c.y) - Ring) < 2.2f && Mathf.Abs(c.x) < Ring + 2;
                float r = c.magnitude; if (r > 6.5f && r < 15.5f && !path) continue;
                float edge = Mathf.Max(Mathf.Abs(c.x), Mathf.Abs(c.y));
                var tone = path ? P.Vary(P.Stone * 1.35f, rng, .12f) : P.Vary(P.Gravel, rng, .07f);
                if (!path && edge > 30) tone = Color.Lerp(tone, P.Moss, (edge - 30) / 6f * .7f);
                float gap = path ? .09f : 0, y = path ? .025f : 0;
                if (path)
                {
                    m.Quad(new Vector3(x0, 0, z0), new Vector3(x0, 0, z0 + 2), new Vector3(x0 + 2, 0, z0 + 2), new Vector3(x0 + 2, 0, z0), P.StoneDark * .8f);
                    int split = rng.Next(3);
                    if (split == 0) Slab(m, x0 + gap, z0 + gap, 2 - gap * 2, 2 - gap * 2, y, tone);
                    else { Slab(m, x0 + gap, z0 + gap, 2 - gap * 2, .95f - gap, y, tone); Slab(m, x0 + gap, z0 + 1.05f, 2 - gap * 2, .95f - gap, y, P.Vary(tone, rng, .08f)); }
                }
                else m.Quad(new Vector3(x0, 0, z0), new Vector3(x0, 0, z0 + 2), new Vector3(x0 + 2, 0, z0 + 2), new Vector3(x0 + 2, 0, z0), tone, P.Vary(tone, rng, .05f), P.Vary(tone, rng, .05f), tone);
            }
            // Raked gravel rings around the great pagoda.
            for (int ring = 0; ring < 18; ring++)
            {
                float r0 = 6.5f + ring * .5f, r1 = r0 + .5f; var tone = ring % 2 == 0 ? P.Gravel * 1.25f : P.Gravel * .85f;
                for (int s = 0; s < 64; s++)
                {
                    float a0 = s * Mathf.PI * 2 / 64, a1 = (s + 1) * Mathf.PI * 2 / 64;
                    var d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)); var d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                    if (Blocked(d0 * r0) && Blocked(d1 * r1)) continue;
                    m.Quad(d0 * r0, d0 * r1, d1 * r1, d1 * r0, tone);
                }
            }
            // Sloped ishigaki stone walls down into the lake, with a coping curb on top.
            for (int side = 0; side < 4; side++)
            {
                var rot = Quaternion.Euler(0, side * 90, 0);
                for (int row = 0; row < 4; row++)
                {
                    float y0 = -row * .55f, y1 = y0 - .55f; float o0 = Half + row * .3f, o1 = o0 + .3f;
                    float end = Half + row * .3f;
                    for (float u = -end; u < end - .05f;)
                    {
                        float w = 1.1f + (float)rng.NextDouble() * 1.3f; float u1 = u + w > end - .3f ? end : u + w;
                        var tone = P.Vary(P.Stone, rng, .18f) * (1 - row * .08f); tone.a = 1;
                        m.Quad(rot * new Vector3(u, y0 - .03f, o0 + .02f), rot * new Vector3(u1 - .06f, y0 - .03f, o0 + .02f), rot * new Vector3(u1 - .06f, y1, o1), rot * new Vector3(u, y1, o1), tone);
                        u = u1;
                    }
                }
                m.Quad(rot * new Vector3(-Half, -2.2f, Half + 1.2f), rot * new Vector3(Half + 1.2f, -2.2f, Half + 1.2f), rot * new Vector3(Half + 1.2f, waterY - 3, Half + 1.6f), rot * new Vector3(-Half, waterY - 3, Half + 1.6f), P.StoneDark * .6f);
                m.Box(rot * new Vector3(0, .06f, Half - .35f), new Vector3(Half * 2 + .2f, .14f, .7f), rot, P.Stone * 1.5f, P.Stone * 1.2f);
            }
        }
        static bool Blocked(Vector3 p) => Mathf.Abs(p.x) < 2.2f || Mathf.Abs(p.z) < 2.2f;
        static void Slab(NinjaMesh m, float x, float z, float w, float d, float y, Color c)
        {
            m.Quad(new Vector3(x, y, z), new Vector3(x, y, z + d), new Vector3(x + w, y, z + d), new Vector3(x + w, y, z), c);
            var s = c * .7f; s.a = 1;
            m.Quad(new Vector3(x, 0, z), new Vector3(x, y, z), new Vector3(x + w, y, z), new Vector3(x + w, 0, z), s);
            m.Quad(new Vector3(x + w, 0, z + d), new Vector3(x + w, y, z + d), new Vector3(x, y, z + d), new Vector3(x, 0, z + d), s);
        }

        public static void Torii(NinjaMesh m, Vector3 at, Quaternion facing, float height, float span, bool glowLamps, NinjaGlowMesh glow)
        {
            float r = height * .055f;
            for (int side = -1; side <= 1; side += 2)
            {
                var foot = at + facing * new Vector3(side * span * .5f, 0, 0);
                m.Cylinder(foot + Vector3.down * 2, foot + Vector3.up * height * .93f, r * 1.08f, r * .92f, 10, P.Vermilion, P.Vermilion);
                m.Cylinder(foot + Vector3.down * 2, foot + Vector3.up * height * .12f, r * 1.25f, r * 1.25f, 10, P.Lacquer, P.Lacquer);
            }
            m.Box(at + facing * new Vector3(0, height * .74f, 0), new Vector3(span + r * 3, r * 1.1f, r * 1.4f), facing, P.Vermilion, P.DeepRed);
            m.Box(at + facing * new Vector3(0, height * .82f, 0), new Vector3(r * 1.4f, height * .14f, r * 1.2f), facing, P.Vermilion);
            int segments = 12; float overhang = span * .5f + r * 5;
            for (int i = 0; i < segments; i++)
            {
                float u0 = Mathf.Lerp(-overhang, overhang, i / (float)segments), u1 = Mathf.Lerp(-overhang, overhang, (i + 1f) / segments);
                float lift0 = Mathf.Pow(Mathf.Abs(u0) / overhang, 3) * height * .07f, lift1 = Mathf.Pow(Mathf.Abs(u1) / overhang, 3) * height * .07f;
                for (int layer = 0; layer < 2; layer++)
                {
                    float y = height * (.9f + layer * .055f); float t = r * (layer == 0 ? 1.2f : .9f), depth = r * (layer == 0 ? 1.7f : 2.1f);
                    var col = layer == 0 ? P.Vermilion : P.Lacquer;
                    var a = at + facing * new Vector3(u0, y + lift0, 0); var b = at + facing * new Vector3(u1, y + lift1, 0);
                    var mid = (a + b) * .5f; var dir = b - a;
                    m.Box(mid, new Vector3(dir.magnitude + .02f, t, depth), Quaternion.FromToRotation(facing * Vector3.right, dir.normalized) * facing, col);
                }
            }
            if (glowLamps && glow != null) for (int side = -1; side <= 1; side += 2)
            {
                var lamp = at + facing * new Vector3(side * span * .5f, height * .66f, r * 2.4f);
                Lantern(m, lamp, .32f, P.Vermilion);
                glow.Halo(lamp, .9f, new Color(1, .45f, .18f, .6f), side * 3.1f + at.x);
            }
        }
        public static void Lantern(NinjaMesh m, Vector3 center, float size, Color paper)
        {
            var bright = P.Paper(Color.Lerp(paper, new Color(1, .62f, .3f), .55f), .85f); var dim = P.Paper(paper, .5f);
            var profile = new[] { new Vector2(0, -size * .62f), new Vector2(size * .38f, -size * .62f), new Vector2(size * .45f, -size * .52f), new Vector2(size * .62f, -size * .25f), new Vector2(size * .66f, 0), new Vector2(size * .62f, size * .25f), new Vector2(size * .45f, size * .52f), new Vector2(size * .38f, size * .62f), new Vector2(0, size * .62f) };
            m.Lathe(center, profile, 8, new[] { P.Lacquer, P.Lacquer, dim, bright, bright, bright, dim, P.Lacquer, P.Lacquer });
        }
        public static void StoneLantern(NinjaMesh m, Vector3 at, float scale, NinjaGlowMesh glow, int seed)
        {
            var rng = new System.Random(seed); var stone = P.Vary(P.Stone * 1.3f, rng, .1f); var dark = stone * .7f; dark.a = 1;
            m.Box(at + Vector3.up * .15f * scale, new Vector3(.9f, .3f, .9f) * scale, Quaternion.Euler(0, 45, 0), stone, dark);
            m.Cylinder(at + Vector3.up * .3f * scale, at + Vector3.up * 1.15f * scale, .16f * scale, .13f * scale, 8, stone, stone);
            m.Box(at + Vector3.up * 1.22f * scale, new Vector3(.7f, .16f, .7f) * scale, Quaternion.identity, stone, dark);
            var light = P.Paper(new Color(1, .7f, .38f), .95f);
            m.Box(at + Vector3.up * 1.52f * scale, new Vector3(.42f, .42f, .42f) * scale, Quaternion.identity, light, light);
            for (int k = 0; k < 4; k++) m.Box(at + Quaternion.Euler(0, k * 90 + 45, 0) * new Vector3(.25f, 0, 0) * scale + Vector3.up * 1.52f * scale, new Vector3(.1f, .44f, .1f) * scale, Quaternion.identity, stone, dark);
            m.Lathe(at + Vector3.up * 1.72f * scale, new[] { new Vector2(0, 0), new Vector2(.62f * scale, 0), new Vector2(.5f * scale, .12f * scale), new Vector2(.12f * scale, .38f * scale), new Vector2(.06f * scale, .52f * scale), new Vector2(0, .55f * scale) }, 8, new[] { dark, stone, stone, stone, stone, stone }, .5f);
            glow?.Halo(at + Vector3.up * 1.52f * scale, .8f * scale, new Color(1, .55f, .22f, .6f), seed * .37f + 1);
        }

        public static void Bamboo(NinjaMesh m, Vector3 center, float radius, int count, int seed, float baseY)
        {
            var rng = new System.Random(seed);
            for (int i = 0; i < count; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2, r = Mathf.Sqrt((float)rng.NextDouble()) * radius;
                var foot = center + new Vector3(Mathf.Cos(a) * r, baseY, Mathf.Sin(a) * r);
                float height = 9 + (float)rng.NextDouble() * 8, thick = .09f + (float)rng.NextDouble() * .07f;
                var lean = new Vector3((float)rng.NextDouble() - .5f, 0, (float)rng.NextDouble() - .5f) * .12f;
                var tone = P.Vary(P.Bamboo, rng, .2f); int nodes = Mathf.RoundToInt(height / 1.1f);
                for (int n = 0; n < nodes; n++)
                {
                    float t0 = n / (float)nodes, t1 = (n + 1f) / nodes;
                    var p0 = foot + (Vector3.up + lean * t0) * height * t0; var p1 = foot + (Vector3.up + lean * t1) * height * t1;
                    var c0 = P.Sway(tone * (1 - .15f * t0), t0); var c1 = P.Sway(tone * (1.12f - .15f * t1), t1);
                    m.Cylinder(p0, p1, thick * (1 - .35f * t0), thick * (1 - .35f * t1), 5, c0, c1);
                    if (n > 1 && n % 2 == 0) m.Cylinder(p1 - Vector3.up * .04f, p1 + Vector3.up * .04f, thick * 1.25f * (1 - .35f * t1), thick * 1.2f * (1 - .35f * t1), 5, P.Sway(tone * .7f, t1), P.Sway(tone * .7f, t1));
                }
                int sprays = 3 + rng.Next(3);
                for (int s = 0; s < sprays; s++)
                {
                    float t = .5f + (float)rng.NextDouble() * .5f; var stem = foot + (Vector3.up + lean * t) * height * t;
                    float dir = (float)rng.NextDouble() * Mathf.PI * 2; var out1 = new Vector3(Mathf.Cos(dir), 0, Mathf.Sin(dir));
                    for (int leaf = 0; leaf < 4; leaf++)
                    {
                        var spread = Quaternion.AngleAxis((leaf - 1.5f) * 26, Vector3.up) * out1; float len = 1 + (float)rng.NextDouble() * .7f;
                        var tip = stem + spread * len + Vector3.down * (.35f + (float)rng.NextDouble() * .4f); var side = Vector3.Cross(spread, Vector3.up) * .13f;
                        var leafTone = P.Sway(P.Vary(P.BambooLeaf, rng, .25f), Mathf.Min(1, t + .2f)); var tipTone = P.Sway(leafTone * 1.25f, Mathf.Min(1, t + .35f));
                        m.Tri(stem, stem + spread * len * .45f + side, tip, leafTone, leafTone, tipTone); m.Tri(stem, tip, stem + spread * len * .45f - side, leafTone, tipTone, leafTone);
                    }
                }
            }
        }
        public static void Pine(NinjaMesh m, Vector3 foot, float size, int seed)
        {
            var rng = new System.Random(seed); var trunk = P.Vary(P.Wood * .8f, rng, .1f);
            var p = foot; var bend = new Vector3((float)rng.NextDouble() - .5f, 0, (float)rng.NextDouble() - .5f).normalized;
            for (int i = 0; i < 4; i++)
            {
                var next = p + (Vector3.up * 1.6f + bend * (.6f + i * .25f)) * size * .7f; var tone = P.Sway(trunk, i * .15f);
                m.Cylinder(p, next, size * (.28f - i * .05f), size * (.23f - i * .05f), 6, tone, tone); p = next;
                if (i >= 1)
                {
                    var pad = p + Quaternion.Euler(0, rng.Next(360), 0) * Vector3.forward * size * (.7f + (float)rng.NextDouble() * .8f) + Vector3.up * size * .15f;
                    m.Cylinder(p, pad, size * .08f, size * .06f, 5, P.Sway(trunk, .3f), P.Sway(trunk, .4f));
                    var green = P.Vary(P.Pine, rng, .2f);
                    m.Blob(pad, new Vector3(1.6f, .42f, 1.3f) * size, 3, 9, P.Sway(green * 1.5f, .6f), P.Sway(green * .6f, .5f), seed + i, .35f);
                }
            }
            m.Blob(p + Vector3.up * size * .3f, new Vector3(1.4f, .5f, 1.4f) * size, 3, 9, P.Sway(P.Pine * 1.6f, .7f), P.Sway(P.Pine * .6f, .6f), seed + 9, .35f);
        }
        public static void Maple(NinjaMesh m, Vector3 foot, float size, int seed)
        {
            var rng = new System.Random(seed); var trunk = P.Wood * .7f; trunk.a = 0;
            m.Cylinder(foot, foot + Vector3.up * size * 1.8f, size * .14f, size * .1f, 6, P.Sway(trunk, 0), P.Sway(trunk, .2f));
            for (int i = 0; i < 6; i++)
            {
                var c = foot + Vector3.up * size * (2 + (float)rng.NextDouble() * 1.1f) + Quaternion.Euler(0, i * 60 + rng.Next(30), 0) * Vector3.forward * size * (.5f + (float)rng.NextDouble() * .8f);
                var tone = Color.Lerp(P.Maple, new Color(.85f, .2f, .06f), (float)rng.NextDouble() * .5f);
                m.Cylinder(foot + Vector3.up * size * 1.6f, c, size * .07f, size * .04f, 5, P.Sway(trunk, .2f), P.Sway(trunk, .4f));
                m.Blob(c, new Vector3(1.05f, .85f, 1.05f) * size * (.75f + (float)rng.NextDouble() * .45f), 5, 9, P.Sway(tone * 1.2f, .7f), P.Sway(tone * .4f, .5f), seed * 7 + i, .5f);
            }
        }
        public static void Rock(NinjaMesh m, Vector3 center, Vector3 radii, int seed, float waterY)
        {
            m.Blob(new Vector3(center.x, waterY - radii.y * .35f, center.z), radii, 4, 11, P.Stone * 1.1f, P.StoneDark * .5f, seed, .45f, -.35f);
            m.Blob(new Vector3(center.x, waterY - radii.y * .2f + radii.y * .55f, center.z), new Vector3(radii.x * .82f, radii.y * .18f, radii.z * .82f), 2, 11, P.Moss * 1.3f, P.Moss * .8f, seed + 5, .25f, 0);
        }

        // Layered ridges at increasing distance. Atmospheric perspective is baked into the colors.
        public static void Mountains(NinjaMesh m, Color horizon, Color dark)
        {
            float[] radius = { 230, 360, 560 }; float[] heights = { 34, 62, 120 }; int[] seeds = { 3, 8, 21 };
            for (int layer = 2; layer >= 0; layer--)
            {
                var rng = new System.Random(seeds[layer]); int segments = 96; float haze = layer / 2f;
                var top = Color.Lerp(dark, horizon, .2f + haze * .55f); var foot = Color.Lerp(dark, horizon, .7f + haze * .25f);
                float[] h = new float[segments + 1];
                for (int i = 0; i <= segments; i++)
                {
                    float a = i * Mathf.PI * 2 / segments; float peak = Mathf.PerlinNoise(a * 1.6f + layer * 5, layer) * .7f + Mathf.PerlinNoise(a * 5.3f + layer * 9, 3) * .3f;
                    float toward = Vector3.Dot(new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)), MoonFlat);
                    h[i] = heights[layer] * (.25f + peak * 1.1f) * (layer == 0 && toward > .85f ? .35f : 1);
                }
                h[segments] = h[0];
                for (int i = 0; i < segments; i++)
                {
                    float a0 = i * Mathf.PI * 2 / segments, a1 = (i + 1) * Mathf.PI * 2 / segments;
                    var d0 = new Vector3(Mathf.Sin(a0), 0, Mathf.Cos(a0)) * radius[layer]; var d1 = new Vector3(Mathf.Sin(a1), 0, Mathf.Cos(a1)) * radius[layer];
                    m.Quad(d0 + Vector3.down * 6, d0 + Vector3.up * h[i], d1 + Vector3.up * h[i + 1], d1 + Vector3.down * 6, foot, top, top, foot);
                }
            }
        }
        // A far castle keep on the ridge facing away from the moon, with lit windows.
        public static void FarCastle(NinjaMesh m, NinjaGlowMesh glow, Vector3 at, float scale, Color tone)
        {
            float y = 0; float[] widths = { 30, 24, 19, 14, 10 }; float[] heights = { 14, 10, 9, 8, 7 };
            for (int i = 0; i < widths.Length; i++)
            {
                float w = widths[i] * scale, h = heights[i] * scale;
                m.Box(at + Vector3.up * (y + h * .5f), new Vector3(w, h, w * .8f), Quaternion.identity, tone);
                m.Box(at + Vector3.up * (y + h * .92f), new Vector3(w * 1.3f, h * .16f, w * 1.1f), Quaternion.identity, tone * .85f);
                for (int k = 0; k < 3 && i < 4; k++) glow.Halo(at + new Vector3((k - 1) * w * .25f, y + h * .5f, -w * .42f), 2.2f * scale, new Color(1, .55f, .22f, .5f), 0);
                y += h;
            }
        }
    }
}
