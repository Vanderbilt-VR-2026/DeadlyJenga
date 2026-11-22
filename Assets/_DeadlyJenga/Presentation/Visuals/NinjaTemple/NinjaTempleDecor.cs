using System.Collections.Generic;
using UnityEngine;
using P = DeadlyJenga.NinjaArt.NinjaPalette;

namespace DeadlyJenga.NinjaArt
{
    // Pagoda roofs, spires and lantern strings ride on the physical blocks, so they fall and break with them.
    public static class NinjaTempleDecor
    {
        public enum Kind { None, Deck, Beam, Finial, TownDeck, TownRoof, BellRoof, Screen, Pillar }
        public const float Eave = 2.4f, Droop = 1.35f, Upturn = 1.3f, Thickness = .24f;
        static readonly Dictionary<string, Mesh> solid = new Dictionary<string, Mesh>(), glows = new Dictionary<string, Mesh>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { solid.Clear(); glows.Clear(); }

        public static Kind TownPart(Vector3 size)
        {
            if (Mathf.Abs(size.y - 1.25f) < .01f) return Kind.TownRoof;
            if (Mathf.Abs(size.y - 1.4f) < .01f) return Kind.BellRoof;
            if (Mathf.Abs(size.y - 1) < .01f && size.z > 4) return Kind.TownDeck;
            if (Mathf.Abs(size.z - .45f) < .01f) return Kind.Screen;
            if (size.x == 1 && size.z == 1 && size.y >= 3) return Kind.Pillar;
            return Classify(size);
        }

        public static Kind Classify(Vector3 size)
        {
            if (Mathf.Abs(size.y - 1) < .02f && size.x > 6.5f && Mathf.Abs(size.x - size.z) < .02f) return Kind.Deck;
            if (Mathf.Abs(size.y - .5f) < .02f && Mathf.Abs(size.z - 1.6f) < .02f) return Kind.Beam;
            if (Mathf.Abs(size.x - 1.2f) < .02f && Mathf.Abs(size.y - 3) < .02f) return Kind.Finial;
            return Kind.None;
        }
        public static void Meshes(Kind kind, Vector3 size, out Mesh body, out Mesh glow)
        {
            string key = kind + size.ToString("F2");
            if (!solid.TryGetValue(key, out body) || !body)
            {
                var m = new NinjaMesh(); var g = new NinjaGlowMesh();
                if (kind == Kind.Deck) Roof(m, g, size.x * .5f, size.y * .5f);
                else if (kind == Kind.Beam) Beam(m, g, size.x, size.y * .5f);
                else if (kind == Kind.Finial) Finial(m, g, size.y * .5f);
                else if (kind == Kind.Screen) Screen(m, size);
                else if (kind == Kind.Pillar)
                {
                    m.Box(Vector3.down * (size.y / 2 - .22f), new Vector3(1.18f,.44f,1.18f), Quaternion.identity, P.Stone);
                    m.Box(Vector3.up * (size.y / 2 - .20f), new Vector3(1.32f,.28f,1.25f), Quaternion.identity, P.Wood);
                    m.Box(Vector3.up * (size.y / 2 - .42f), new Vector3(1.04f,.10f,1.04f), Quaternion.identity, P.Gold);
                }
                else TownRoof(m, g, size, kind);
                body = m.Build("Ninja " + key); body.hideFlags = HideFlags.DontSave; solid[key] = body;
                var halo = g.Build("Ninja glow " + key, 3); halo.hideFlags = HideFlags.DontSave; glows[key] = halo;
            }
            glow = glows[key];
        }
        static void Screen(NinjaMesh m, Vector3 size)
        {
            float width = size.x, height = size.y;
            var paper = P.Paper(new Color(.96f, .74f, .43f), .45f);
            m.Box(Vector3.zero, new Vector3(width, height, size.z + .04f), Quaternion.identity, paper);
            for (int i = 0; i <= 4; i++)
                m.Box(new Vector3((i / 4f - .5f) * (width - .1f), 0, 0),
                    new Vector3(.12f, height, size.z + .12f), Quaternion.identity, P.Wood);
            m.Box(Vector3.zero, new Vector3(width, .14f, size.z + .13f), Quaternion.identity, P.Wood);
        }

        static void TownRoof(NinjaMesh m, NinjaGlowMesh glow, Vector3 size, Kind kind)
        {
            var scale = Matrix4x4.Scale(new Vector3(size.x / size.z, 1, 1));
            m.Frame = scale;
            Roof(m, glow, size.z / 2, size.y / 2);
            m.Frame = Matrix4x4.identity;
            if (kind == Kind.TownDeck)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float z = side * (size.z / 2 - .18f);
                    for (int i = 0; i <= 6; i++)
                        m.Box(new Vector3(size.x * (i / 6f - .5f), .88f, z), new Vector3(.16f,.76f,.16f), Quaternion.identity, P.Vermilion);
                    m.Box(new Vector3(0, 1.22f, z), new Vector3(size.x + .3f,.14f,.17f), Quaternion.identity, P.Wood);
                }
                return;
            }
            float top = size.y / 2, half = size.z / 2, rise = half * .44f;
            for (int side = -1; side <= 1; side += 2)
            {
                var rotation = Quaternion.Euler(side * Mathf.Atan(.44f) * Mathf.Rad2Deg, 0, 0);
                m.Box(new Vector3(0, top + rise / 2, side * half / 2),
                    new Vector3(size.x, .22f, half * Mathf.Sqrt(1 + .44f * .44f)), rotation, P.Tile);
            }
            for (float x = -size.x / 2 + .18f; x < size.x / 2; x += .55f)
                for (int side = -1; side <= 1; side += 2)
                    m.Cylinder(new Vector3(x, top + rise + .1f, 0), new Vector3(x, top + .1f, side * half),
                        .09f, .1f, 5, new Color(.26f,.29f,.36f), P.Tile);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (size.x / 2 - .12f);
                var a = new Vector3(x,top,-half); var b = new Vector3(x,top,half); var c = new Vector3(x,top+rise,0);
                m.Tri(a,b,c,P.Wood,P.Wood,P.Wood); m.Tri(c,b,a,P.Wood,P.Wood,P.Wood);
                m.Box(new Vector3(x,top+rise/2,0),new Vector3(.3f,rise,.22f),Quaternion.identity,P.Vermilion);
            }
            m.Box(Vector3.up * (top + rise), new Vector3(size.x + .25f, .35f, .42f), Quaternion.identity, P.Lacquer);
            if (size.x >= 8 && size.x <= 15)
            {
                var anchor = new Vector3(0,-.7f,-half);
                for (int i = -2; i <= 2; i++)
                {
                    var lamp = anchor + new Vector3(i * size.x / 6, -.55f, 0);
                    m.Cylinder(lamp, lamp + Vector3.up * .65f, .025f,.025f,4,P.Wood,P.Wood);
                    NinjaTempleScenery.Lantern(m, lamp, .36f, (i & 1) == 0 ? P.Vermilion : new Color(.96f,.73f,.43f));
                    glow.Halo(lamp,.5f,new Color(1,.45f,.16f,.32f),i+4);
                }
            }
            if (kind == Kind.BellRoof)
                m.Cylinder(Vector3.down * 3.7f, Vector3.down * .7f, 1.25f, .65f, 12, P.Gold * .65f, P.Gold);
        }

        static Vector3 RoofPoint(float half, float top, float u, float v)
        {
            float o = half + Eave * v;
            float y = top - Droop * Mathf.Sqrt(v) + Upturn * Mathf.Pow(Mathf.Abs(u), 6) * Mathf.Pow(v, 1.5f);
            return new Vector3(u * o, y, o);
        }
        static void Roof(NinjaMesh m, NinjaGlowMesh g, float half, float top)
        {
            const int U = 16, V = 4;
            for (int side = 0; side < 4; side++)
            {
                var rot = Quaternion.Euler(0, side * 90, 0);
                for (int iu = 0; iu < U; iu++) for (int iv = 0; iv < V; iv++)
                {
                    float u0 = -1 + 2f * iu / U, u1 = -1 + 2f * (iu + 1) / U, v0 = iv / (float)V, v1 = (iv + 1f) / V;
                    var a = rot * RoofPoint(half, top, u0, v0); var b = rot * RoofPoint(half, top, u0, v1); var c = rot * RoofPoint(half, top, u1, v1); var d = rot * RoofPoint(half, top, u1, v0);
                    var tone = iu % 2 == 0 ? P.Tile : P.TileDark; var lower = tone * (1 + .25f * v1); lower.a = 1;
                    m.Quad(a, b, c, d, tone, lower, lower, tone);
                    var drop = Vector3.down * Thickness * (iv == 0 ? .2f : 1);
                    var soffit = Color.Lerp(P.Soffit, P.Vermilion * .6f, v0 * .5f); soffit.a = 1;
                    m.Quad(a + Vector3.down * Thickness * .2f, d + Vector3.down * Thickness * .2f, c + Vector3.down * Thickness, b + Vector3.down * Thickness, soffit);
                }
                for (int iu = 0; iu < U; iu++)
                {
                    float u0 = -1 + 2f * iu / U, u1 = -1 + 2f * (iu + 1) / U;
                    var a = rot * RoofPoint(half, top, u0, 1); var b = rot * RoofPoint(half, top, u1, 1);
                    m.Quad(b + Vector3.down * Thickness, b + Vector3.up * .06f, a + Vector3.up * .06f, a + Vector3.down * Thickness, P.Vermilion);
                    m.Quad(a + Vector3.up * .06f, b + Vector3.up * .06f, b + Vector3.up * .06f + rot * Vector3.back * .12f, a + Vector3.up * .06f + rot * Vector3.back * .12f, P.Gold * .8f);
                }
                var tip = rot * RoofPoint(half, top, 1, 1);
                for (int k = 0; k < 6; k++)
                {
                    var r0 = rot * RoofPoint(half, top, 1, k / 6f); var r1 = rot * RoofPoint(half, top, 1, (k + 1) / 6f);
                    m.Cylinder(r0 + Vector3.up * .04f, r1 + Vector3.up * .04f, .13f, .13f, 5, P.Lacquer, P.Lacquer);
                }
                m.Blob(tip + Vector3.up * .12f, new Vector3(.22f, .3f, .22f), 2, 6, P.Gold, P.Gold * .6f, side, .2f);
                var lantern = tip + Vector3.down * 1.35f;
                m.Cylinder(tip, lantern + Vector3.up * .3f, .015f, .015f, 3, P.Lacquer, P.Lacquer);
                NinjaTempleScenery.Lantern(m, lantern, .42f, P.Vermilion);
                g.Halo(m.Frame.MultiplyPoint3x4(lantern), .95f, new Color(1, .45f, .18f, .6f), side * 2.3f + half);
            }
        }
        static void Beam(NinjaMesh m, NinjaGlowMesh g, float length, float halfThick)
        {
            int count = Mathf.Max(2, Mathf.FloorToInt(length / 2.6f)); float span = length - 2.6f;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 prev = Vector3.zero;
                for (int i = 0; i <= 16; i++)
                {
                    float x = Mathf.Lerp(-span * .5f, span * .5f, i / 16f); float sag = Mathf.Sin(i / 16f * Mathf.PI) * .55f;
                    var p = new Vector3(x, -halfThick - .1f - sag, side * .62f);
                    if (i > 0) m.Cylinder(prev, p, .035f, .035f, 4, new Color(.55f, .48f, .34f), new Color(.55f, .48f, .34f));
                    if (i > 0 && i % 2 == 0 && side < 0) { var paper = P.Paper(new Color(.92f, .9f, .85f), .25f); m.Quad(p + new Vector3(-.08f, 0, 0), p + new Vector3(.08f, 0, 0), p + new Vector3(.12f, -.32f, 0), p + new Vector3(-.04f, -.32f, 0), paper); m.Quad(p + new Vector3(.12f, -.32f, 0), p + new Vector3(.08f, 0, 0), p + new Vector3(-.08f, 0, 0), p + new Vector3(-.04f, -.32f, 0), paper); }
                    prev = p;
                }
            }
            for (int i = 0; i < count; i++)
            {
                float x = Mathf.Lerp(-span * .5f, span * .5f, (i + .5f) / count); float sag = Mathf.Sin((i + .5f) / count * Mathf.PI) * .55f;
                var hang = new Vector3(x, -halfThick - .1f - sag, 0); var lantern = hang + Vector3.down * .55f;
                m.Cylinder(hang, lantern + Vector3.up * .2f, .012f, .012f, 3, P.Lacquer, P.Lacquer);
                bool white = i % 2 == 1; NinjaTempleScenery.Lantern(m, lantern, .3f, white ? new Color(.95f, .85f, .7f) : P.Vermilion);
                g.Halo(lantern, .6f, white ? new Color(1, .7f, .45f, .45f) : new Color(1, .4f, .16f, .5f), x * 1.7f + 4);
            }
        }
        static void Finial(NinjaMesh m, NinjaGlowMesh g, float halfHeight)
        {
            var gold = P.Gold; var dark = P.Gold * .55f; dark.a = 1;
            for (int i = 0; i < 9; i++) { float y = -halfHeight + .35f + i * .3f; m.Lathe(new Vector3(0, y, 0), new[] { new Vector2(0, 0), new Vector2(.62f - i * .025f, 0), new Vector2(.62f - i * .025f, .08f), new Vector2(0, .08f) }, 10, new[] { dark, gold, gold, gold }); }
            m.Lathe(new Vector3(0, halfHeight, 0), new[] { new Vector2(0, 0), new Vector2(.32f, .05f), new Vector2(.36f, .3f), new Vector2(.22f, .55f), new Vector2(.08f, .85f), new Vector2(0, 1.05f) }, 10, new[] { gold, gold, gold, gold, P.Paper(new Color(1, .82f, .5f), .6f), P.Paper(new Color(1, .9f, .6f), .9f) });
            g.Halo(new Vector3(0, halfHeight + .8f, 0), 1.3f, new Color(1, .75f, .4f, .5f), 1.7f);
        }
    }
}
