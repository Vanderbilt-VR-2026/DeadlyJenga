using System.Collections.Generic;
using UnityEngine;
using P = DeadlyJenga.NinjaArt.NinjaPalette;

namespace DeadlyJenga.NinjaArt
{
    // Pagoda roofs, spires and lantern strings ride on the physical blocks, so they fall and break with them.
    public static class NinjaTempleDecor
    {
        public enum Kind { None, Deck, Beam, Finial, TownDeck, TownRoof, BellRoof, Screen, Pillar, PagodaRoof, Terrace, Lintel }
        public const float Eave = 2.4f, Droop = 1.35f, Upturn = 1.3f, Thickness = .24f;
        static readonly Dictionary<string, Mesh> solid = new Dictionary<string, Mesh>(), glows = new Dictionary<string, Mesh>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { solid.Clear(); glows.Clear(); }

        public static Kind TownPart(Vector3 size)
        {
            if (size.y > .59f && size.y < .61f && size.z == 1) return Kind.Lintel;
            if (size.x > 36 && Mathf.Abs(size.y - 4) < .01f) return Kind.Terrace;
            if (Mathf.Abs(size.y - 1.35f) < .01f) return Kind.PagodaRoof;
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
                else if (kind == Kind.Lintel)
                {
                    int bays = Mathf.Max(1,Mathf.RoundToInt(size.x / 6.5f));
                    for (int i = 0; i <= bays; i++)
                    {
                        float x = (i/(float)bays - .5f) * (size.x-1);
                        m.Box(new Vector3(x,.02f,0),new Vector3(1.4f,.22f,1.24f),Quaternion.identity,P.Wood);
                        m.Box(new Vector3(x,.22f,0),new Vector3(1.85f,.18f,1.36f),Quaternion.identity,P.Vermilion);
                        m.Box(new Vector3(x,0,-.66f),new Vector3(.32f,.36f,.1f),Quaternion.identity,P.Gold);
                        if (i == bays || size.x < 13) continue;
                        var lamp = new Vector3(x + (size.x-1)/(2*bays),-.91f,-.66f);
                        m.Cylinder(lamp, lamp+Vector3.up*.72f,.035f,.035f,4,P.Wood,P.Wood);
                        NinjaTempleScenery.Lantern(m,lamp,.59f,new Color(1,.64f,.27f));
                        g.Halo(lamp,.85f,new Color(1,.45f,.17f,.65f),i);
                    }
                }
                else if (kind == Kind.Terrace)
                {
                    for (int side = -1; side <= 1; side += 2)
                        for (int row = 0; row < 4; row++)
                            for (float x = -size.x / 2; x < size.x / 2; )
                            {
                                float width = Mathf.Min(size.x / 2 - x, row % 2 == 1 && x == -size.x / 2 ? 1.5f : 3);
                                m.Box(new Vector3(x + width / 2, row - 1.5f, side * size.z / 2),
                                    new Vector3(width - .07f,.94f,.22f), Quaternion.identity, new Color(.51f,.46f,.46f));
                                x += width;
                            }
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
            Roof(m, glow, size.z / 2, size.y / 2, true);
            m.Frame = Matrix4x4.identity;
            if (kind == Kind.TownDeck)
            {
                if (size.x >= 30)
                {
                    // The upper gallery meets a continuous tiled skirt over the lower hall.
                    for (int edge=0; edge<4; edge++)
                    {
                        var turn = Quaternion.Euler(0,90*edge,0);
                        float across = (edge%2==0 ? size.x : size.z)/2;
                        float depth = (edge%2==0 ? size.z : size.x)/2;
                        float inner = depth*.81f, rise = (depth-inner)*.72f;
                        int tiles = Mathf.Max(8,Mathf.RoundToInt(across*4));
                        for (int i=0; i<tiles; i++)
                        {
                            float u = 2*i/(float)tiles-1, v = 2*(i+1)/(float)tiles-1;
                            var a = turn * new Vector3(u*across*.81f,.55f+rise,inner);
                            var b = turn * new Vector3(u*across,.55f,depth);
                            var c = turn * new Vector3(v*across,.55f,depth);
                            var d = turn * new Vector3(v*across*.81f,.55f+rise,inner);
                            m.Quad(a,b,c,d,new Color(.25f,.27f,.35f));
                            m.Cylinder(a,b,.055f,.07f,5,new Color(.37f,.38f,.45f),P.Tile);
                        }
                    }
                    return;
                }
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
                    new Vector3(size.x, .22f, half * Mathf.Sqrt(1 + .44f * .44f)), rotation, new Color(.27f,.29f,.36f));
            }
            for (float x = -size.x / 2 + .18f; x < size.x / 2; x += .55f)
                for (int side = -1; side <= 1; side += 2)
                    m.Cylinder(new Vector3(x, top + rise + .1f, 0), new Vector3(x, top + .1f, side * half),
                        .09f, .1f, 5, new Color(.39f,.41f,.48f), new Color(.29f,.31f,.39f));
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (size.x / 2 - .12f);
                var a = new Vector3(x,top,-half); var b = new Vector3(x,top,half); var c = new Vector3(x,top+rise,0);
                m.Tri(a,b,c,P.Wood,P.Wood,P.Wood); m.Tri(c,b,a,P.Wood,P.Wood,P.Wood);
                for (int j = -3; j <= 3; j++)
                {
                    float h = rise * (1 - Mathf.Abs(j) / 4f);
                    m.Box(new Vector3(x,top+h/2,half*j/4),new Vector3(.22f,h,.14f),Quaternion.identity,P.Vermilion);
                }
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
            {
                var bronze = new Color(.72f,.48f,.19f);
                m.Lathe(Vector3.down * 4.7f, new[] {new Vector2(0,0), new Vector2(1.65f,0),
                    new Vector2(1.65f,.25f), new Vector2(1.3f,.48f), new Vector2(1.04f,2.65f),
                    new Vector2(.68f,3.05f),new Vector2(.18f,3.2f),new Vector2(.18f,4.5f)}, 20, new[] {bronze});
            }
            else if (kind == Kind.PagodaRoof)
            {
                float baseY = top + rise;
                m.Cylinder(Vector3.up * baseY, Vector3.up * (baseY + 5.6f), .27f,.09f,10,P.Gold,P.Gold);
                for (int i = 0; i < 9; i++)
                    m.Cylinder(Vector3.up * (baseY + .55f + i * .5f), Vector3.up * (baseY + .75f + i * .5f),
                        .73f - i * .045f,.55f - i * .045f,12,P.Gold,P.Gold);
            }
            else if (size.x >= 15)
            {
                float span = size.x * .38f, front = -half - .75f, peak = top + rise + span * .32f;
                var left = new Vector3(-span,top,front); var right = new Vector3(span,top,front);
                var crest = new Vector3(0,peak,front);
                m.Tri(left, crest, right, P.Vermilion, P.Wood, P.Vermilion);
                m.Tri(right, crest, left, P.Vermilion, P.Wood, P.Vermilion);
                foreach (var corner in new[] {left, right})
                {
                    var ridge = new Vector3(0,peak,0);
                    m.Quad(corner,crest,ridge,new Vector3(corner.x,top,0),new Color(.28f,.30f,.38f));
                    m.Quad(new Vector3(corner.x,top,0),ridge,crest,corner,new Color(.28f,.30f,.38f));
                    m.Cylinder(corner,crest,.2f,.22f,6,P.Lacquer,P.Gold);
                }
                m.Cylinder(left,right,.18f,.18f,6,P.Gold,P.Gold);
                var crown = crest + Vector3.up * .34f;
                m.Blob(crown,new Vector3(.36f,.56f,.32f),3,9,P.Gold,P.Wood,9,.12f);
                for (int side = -1; side <= 1; side += 2)
                    m.Cylinder(crown,crown+new Vector3(side*.62f,.49f,0),.12f,.20f,6,P.Gold,P.Gold);
            }
        }

        static Vector3 RoofPoint(float half, float top, float u, float v, bool town = false)
        {
            float eave = town ? Mathf.Clamp(half * .24f,.65f,1.7f) : Eave;
            float o = half + eave * v;
            float y = top - Droop * Mathf.Sqrt(v) + Upturn * Mathf.Pow(Mathf.Abs(u), 6) * Mathf.Pow(v, 1.5f);
            if (town) y = top - eave * .38f * v + eave * .24f * Mathf.Pow(Mathf.Abs(u),5) * v;
            return new Vector3(u * o, y, o);
        }
        static void Roof(NinjaMesh m, NinjaGlowMesh g, float half, float top, bool town = false)
        {
            const int U = 16, V = 4;
            for (int side = 0; side < 4; side++)
            {
                var rot = Quaternion.Euler(0, side * 90, 0);
                for (int iu = 0; iu < U; iu++) for (int iv = 0; iv < V; iv++)
                {
                    float u0 = -1 + 2f * iu / U, u1 = -1 + 2f * (iu + 1) / U, v0 = iv / (float)V, v1 = (iv + 1f) / V;
                    var a = rot * RoofPoint(half, top, u0, v0, town); var b = rot * RoofPoint(half, top, u0, v1, town); var c = rot * RoofPoint(half, top, u1, v1, town); var d = rot * RoofPoint(half, top, u1, v0, town);
                    var tone = town ? new Color(.27f,.29f,.36f) : iu % 2 == 0 ? P.Tile : P.TileDark; var lower = tone * (1 + .25f * v1); lower.a = 1;
                    m.Quad(a, b, c, d, tone, lower, lower, tone);
                    var drop = Vector3.down * Thickness * (iv == 0 ? .2f : 1);
                    var soffit = Color.Lerp(P.Soffit, P.Vermilion * .6f, v0 * .5f); soffit.a = 1;
                    m.Quad(a + Vector3.down * Thickness * .2f, d + Vector3.down * Thickness * .2f, c + Vector3.down * Thickness, b + Vector3.down * Thickness, soffit);
                }
                for (int iu = 0; iu < U; iu++)
                {
                    float u0 = -1 + 2f * iu / U, u1 = -1 + 2f * (iu + 1) / U;
                    var a = rot * RoofPoint(half, top, u0, 1, town); var b = rot * RoofPoint(half, top, u1, 1, town);
                    m.Quad(b + Vector3.down * Thickness, b + Vector3.up * .06f, a + Vector3.up * .06f, a + Vector3.down * Thickness, P.Vermilion);
                    m.Quad(a + Vector3.up * .06f, b + Vector3.up * .06f, b + Vector3.up * .06f + rot * Vector3.back * .12f, a + Vector3.up * .06f + rot * Vector3.back * .12f, P.Gold * .8f);
                }
                if (town)
                {
                    int ribs = Mathf.CeilToInt(half * 4);
                    for (int rib = 0; rib <= ribs; rib++)
                    {
                        float u = 2f * rib / ribs - 1;
                        for (int part = 0; part < 3; part++)
                        {
                            var start = rot * RoofPoint(half,top,u,part / 3f, town) + Vector3.up * .05f;
                            var end = rot * RoofPoint(half,top,u,(part + 1) / 3f, town) + Vector3.up * .05f;
                            // One strip per tile seam, batched into the roof mesh.
                            var lip = rot * Vector3.right * .055f;
                            m.Quad(start-lip,end-lip,end+lip,start+lip,new Color(.37f,.39f,.46f));
                        }
                    }
                }
                var tip = rot * RoofPoint(half, top, 1, 1, town);
                for (int k = 0; k < 6; k++)
                {
                    var r0 = rot * RoofPoint(half, top, 1, k / 6f, town); var r1 = rot * RoofPoint(half, top, 1, (k + 1) / 6f, town);
                    m.Cylinder(r0 + Vector3.up * .04f, r1 + Vector3.up * .04f, .13f, .13f, 5, P.Lacquer, P.Lacquer);
                }
                m.Blob(tip + Vector3.up * .12f, new Vector3(.22f, .3f, .22f), 2, 6, P.Gold, P.Gold * .6f, side, .2f);
                if (town) continue;
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
