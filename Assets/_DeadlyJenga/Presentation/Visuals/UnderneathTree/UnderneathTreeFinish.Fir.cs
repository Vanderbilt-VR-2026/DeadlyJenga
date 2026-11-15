using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyArt
{
    public sealed partial class UnderneathTreeFinish
    {
        readonly struct FirTip
        {
            public readonly Vector3 position;
            public readonly int tier, ordinal;

            public FirTip(Vector3 position, int tier, int ordinal)
            {
                this.position = position;
                this.tier = tier;
                this.ordinal = ordinal;
            }
        }

        void BuildFir()
        {
            var bark = Surface("Fir bark", "Mahogany", new Color(.24f, .128f, .066f),
                .25f, new Vector2(2f, 4f), 0f, .70f);
            var forest = Surface("Fir needles in shade", "Paper", new Color(.102f, .214f, .115f),
                .37f, Vector2.one, 0f, .26f);
            var crown = Surface("Fir needles in light", "Paper", new Color(.24f, .382f, .174f),
                .39f, Vector2.one, 0f, .26f);
            forest.SetFloat("_Cull", 0f);
            crown.SetFloat("_Cull", 0f);
            var treeCenter = new Vector3(-35f, 0f, -35f);
            var random = new System.Random(241215);
            var stems = new FirMesh();
            for (int y = 0; y < 8; y++)
            {
                float low = y / 8f;
                float high = (y + 1) / 8f;
                var a = treeCenter + new Vector3(Mathf.Sin(low * 3.5f) * 1.45f, low * 138f, Mathf.Sin(low * 6f));
                var b = treeCenter + new Vector3(Mathf.Sin(high * 3.5f) * 1.45f, high * 138f, Mathf.Sin(high * 6f));
                stems.Tube(a, b, Mathf.Lerp(5.9f, .2f, low), Mathf.Lerp(5.9f, .2f, high), 12);
            }

            const int whorls = 17;
            bool mobile = Application.platform == RuntimePlatform.Android;
            var tips = new List<FirTip>();
            for (int band = 0; band < 4; band++)
            {
                var shaded = new FirMesh();
                var bright = new FirMesh();
                for (int tier = band * 5; tier < Mathf.Min(whorls, (band + 1) * 5); tier++)
                {
                    float t = tier / (float)(whorls - 1);
                    float y = 25f + t * 108f;
                    float reach = Mathf.Lerp(44f, 2.8f, Mathf.Pow(t, 1.03f));
                    int count = tier >= 14 ? 6 : 10;
                    float phase = tier * .462f + .26f;
                    for (int i = 0; i < count; i++)
                    {
                        float angle = phase + i * Mathf.PI * 2f / count + Range(random, -.115f, .115f);
                        var axis = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                        var side = Vector3.Cross(Vector3.up, axis);
                        float length = reach * Range(random, .86f, 1.12f);
                        var root = treeCenter + Vector3.up * (y + Range(random, -1.4f, 1.4f));
                        var bend = root + axis * (length * .47f) + Vector3.down * (.08f * length);
                        var end = root + axis * length + Vector3.down * (.135f * length) + side * Range(random, -2.5f, 2.5f);
                        float girth = Mathf.Max(.17f, length * .045f);
                        stems.Tube(root, bend, girth, girth * .6f, 7);
                        stems.Tube(bend, end, girth * .6f, .08f, 6);
                        if (tier < 13)
                            tips.Add(new FirTip(Vector3.Lerp(bend, end, .90f), tier, i));
                        int brushCount = mobile ? 9 : 13;
                        for (int j = 0; j < brushCount; j++)
                        {
                            float v = (.92f * j / (brushCount - 1)) + .08f;
                            var basePoint = v < .47f ? Vector3.Lerp(root, bend, v / .47f) : Vector3.Lerp(bend, end, (v - .47f) / .53f);
                            float span = Mathf.Max(1.1f, length * (.21f - .125f * v));
                            for (int sign = -1; sign <= 1; sign += 2)
                            {
                                var shoot = basePoint + side * (sign * span * Range(random, .8f, 1.2f)) + axis * (span * .74f) +
                                    Vector3.up * (span * Range(random, -.24f, .36f));
                                var fill = (j + i + sign) % 4 == 0 ? bright : shaded;
                                NeedleSpray(fill, basePoint, shoot, span, mobile ? 6 : 10, random);
                                if (!mobile && j % 2 == 0)
                                {
                                    var upper = basePoint + side * (sign * span * .40f) + axis * (span * .9f) + Vector3.up * (span * .76f);
                                    NeedleSpray(bright, basePoint, upper, span * .72f, 7, random);
                                }
                            }
                        }
                    }
                }
                AddMesh("Fir needle boughs " + band, shaded.Build(), forest, ShadowCastingMode.TwoSided);
                AddMesh("Fir highlights " + band, bright.Build(), crown, ShadowCastingMode.TwoSided);
            }
            AddMesh("Fir trunk and branches", stems.Build(), bark, ShadowCastingMode.On);
            DressFir(tips, random);
        }

        static void NeedleSpray(FirMesh mesh, Vector3 root, Vector3 tip, float span, int needleCount, System.Random random)
        {
            var spine = (tip - root).normalized;
            var side = Vector3.Cross(Vector3.up, spine).normalized;
            var up = Vector3.Cross(spine, side);
            float size = Mathf.Clamp(span * .68f, .9f, 3.8f);
            for (int j = 0; j < needleCount; j++)
            {
                float p = (j + .32f) / needleCount;
                var attach = Vector3.Lerp(root, tip, p);
                float length = size * Mathf.Lerp(1f, .44f, p);
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    float pitch = Range(random, -.44f, .66f);
                    var destination = attach + spine * (length * Range(random, .43f, .93f)) +
                        side * (sign * length * Range(random, .65f, 1.15f)) + up * (length * pitch);
                    mesh.Needle(attach, destination, spine, Mathf.Max(.13f, length * .145f));
                }
            }
        }

        void DressFir(List<FirTip> tips, System.Random random)
        {
            var bulbColors = new[] { new Color(1, .55f, .16f), new Color(.98f, .09f, .05f),
                new Color(.11f, .82f, .31f), new Color(.17f, .36f, 1f) };
            var ornamentColors = new[] { new Color(.67f, .105f, .135f), new Color(.89f, .59f, .22f),
                new Color(.18f, .34f, .54f), new Color(.75f, .69f, .61f) };
            var sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            var ornaments = new List<CombineInstance>[4];
            var bulbs = new List<CombineInstance>[4];
            for (int i = 0; i < 4; i++)
            {
                ornaments[i] = new List<CombineInstance>();
                bulbs[i] = new List<CombineInstance>();
            }
            var beadCaps = new List<CombineInstance>();
            var wire = new FirMesh();
            for (int i = 0; i < tips.Count; i++)
            {
                var tip = tips[i];
                var bulb = tip.position + Vector3.down * Range(random, .65f, 1.25f);
                var tint = (tip.tier + tip.ordinal * 3) % bulbs.Length;
                bulbs[tint].Add(Part(sphere, bulb, new Vector3(.84f, 1.39f, .84f) * Range(random, .83f, 1.25f)));
                beadCaps.Add(Part(sphere, bulb + Vector3.up * .65f, new Vector3(.50f, .45f, .50f)));
                if ((tip.ordinal + tip.tier) % 3 == 0)
                {
                    Vector3 center = tip.position + Vector3.down * Range(random, 2.35f, 3.55f);
                    float diameter = Range(random, 2.6f, 4.60f);
                    ornaments[(tint + i) % 4].Add(Part(sphere, center, Vector3.one * diameter));
                    beadCaps.Add(Part(sphere, center + Vector3.up * (diameter * .48f), new Vector3(.75f, .60f, .75f)));
                    wire.Tube(tip.position, center + Vector3.up * (diameter * .48f), .049f, .046f, 4);
                }
                if (i + 1 < tips.Count && tips[i + 1].tier == tip.tier)
                {
                    var next = tips[i + 1].position;
                    var sag = (tip.position + next) * .5f + Vector3.down * Mathf.Min(1.8f, Vector3.Distance(tip.position, next) * .105f);
                    wire.Tube(tip.position, sag, .075f, .075f, 4);
                    wire.Tube(sag, next, .075f, .075f, 4);
                }
            }

            for (int i = 0; i < 4; i++)
            {
                var glass = Surface("Lacquered glass ornament " + i, "Paper", ornamentColors[i], .89f,
                    Vector2.one, .34f, .29f);
                Combined("Glass baubles " + i, ornaments[i], glass, ShadowCastingMode.On);
                Combined("Vintage colored bulbs " + i, bulbs[i],
                    Glow("Colored enamel bulb " + i, bulbColors[i], 4.5f), ShadowCastingMode.Off);
            }
            Combined("Gold ornament fittings", beadCaps,
                Surface("Brass ornament fittings", "Paper", new Color(.78f, .54f, .24f), .70f, Vector2.one, .52f),
                ShadowCastingMode.Off);
            AddMesh("Fir light strings", wire.Build(),
                Surface("Green vintage light cable", null, new Color(.08f, .099f, .067f), .43f, Vector2.one),
                ShadowCastingMode.Off);
        }

        static CombineInstance Part(Mesh mesh, Vector3 center, Vector3 size) => new CombineInstance
        {
            mesh = mesh,
            transform = Matrix4x4.TRS(center, Quaternion.identity, size)
        };

        void Combined(string label, List<CombineInstance> parts, Material material, ShadowCastingMode mode)
        {
            if (parts.Count == 0)
                return;
            var mesh = new Mesh { name = label, indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(parts.ToArray(), true, true);
            AddMesh(label, mesh, material, mode);
        }

        sealed class FirMesh
        {
            readonly List<Vector3> vertices = new List<Vector3>();
            readonly List<Vector2> uv = new List<Vector2>();
            readonly List<int> faces = new List<int>();

            public void Tube(Vector3 start, Vector3 end, float startRadius, float endRadius, int sides)
            {
                var axis = (end - start).normalized;
                if (axis == Vector3.zero)
                    return;
                var side = Vector3.Cross(axis, Mathf.Abs(axis.y) > .89f ? Vector3.right : Vector3.up).normalized;
                var cross = Vector3.Cross(side, axis);
                int first = vertices.Count;
                for (int i = 0; i <= sides; i++)
                {
                    float u = i / (float)sides;
                    float angle = u * Mathf.PI * 2f;
                    var radius = side * Mathf.Cos(angle) + cross * Mathf.Sin(angle);
                    vertices.Add(start + radius * startRadius);
                    vertices.Add(end + radius * endRadius);
                    uv.Add(new Vector2(u, 0));
                    uv.Add(new Vector2(u, 1));
                    if (i == sides)
                        continue;
                    int a = first + i * 2;
                    faces.Add(a); faces.Add(a + 2); faces.Add(a + 1);
                    faces.Add(a + 2); faces.Add(a + 3); faces.Add(a + 1);
                }
            }

            public void Needle(Vector3 attach, Vector3 tip, Vector3 along, float width)
            {
                var side = Vector3.Cross(tip - attach, along).normalized * width;
                int first = vertices.Count;
                vertices.Add(attach - side);
                vertices.Add(attach + side);
                vertices.Add(tip);
                uv.Add(Vector2.zero);
                uv.Add(Vector2.right);
                uv.Add(new Vector2(.5f, 1f));
                faces.Add(first);
                faces.Add(first + 1);
                faces.Add(first + 2);
            }

            public Mesh Build()
            {
                var mesh = new Mesh { indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.SetVertices(vertices);
                mesh.SetUVs(0, uv);
                mesh.SetTriangles(faces, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
                return mesh;
            }
        }
    }
}
