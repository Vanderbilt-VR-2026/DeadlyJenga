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
            var bark = Surface("Fir bark", "Pine Scan", new Color(.78f, .68f, .53f),
                .15f, new Vector2(2f, 1.25f), 0f, .76f);
            var forest = FirFoliage("Fir needles in shade", new Color(1.0f, 1.3f, .92f));
            var crown = FirFoliage("Fir needles in light", new Color(1.4f, 1.65f, 1.12f));
            var trunkAnchor = world.Root.Find("Underneath the Tree Layout/Fir trunk collision");
            var treeCenter = trunkAnchor ? Vector3.ProjectOnPlane(art.InverseTransformPoint(trunkAnchor.position), Vector3.up) :
                new Vector3(-37f, 0f, -35.7f);
            var random = new System.Random(241215);
            var stems = new FirMesh();
            for (int y = 0; y < 8; y++)
            {
                float low = y / 8f;
                float high = (y + 1) / 8f;
                var a = treeCenter + new Vector3(Mathf.Sin(low * 3.5f) * 1.45f, low * 138f, Mathf.Sin(low * 6f));
                var b = treeCenter + new Vector3(Mathf.Sin(high * 3.5f) * 1.45f, high * 138f, Mathf.Sin(high * 6f));
                stems.Tube(a, b, Mathf.Lerp(4.6f, .2f, low), Mathf.Lerp(4.6f, .2f, high), 24);
            }

            for (int i = 0; i < 11; i++)
            {
                float angle = i * Mathf.PI * 2f / 11f + Range(random, -.15f, .15f);
                var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                float length = Range(random, 5.2f, 6.25f);
                var start = treeCenter + Vector3.up * 3.6f + radial * 2.8f;
                var joint = treeCenter + Vector3.up * 1.12f + radial * (length - 1.3f);
                var end = treeCenter + Vector3.up * .56f + radial * length;
                stems.Tube(start, joint, Range(random, .62f, 1.46f), .58f, 10);
                stems.Tube(joint, end, .58f, .055f, 10);
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
                    float y = 42f + t * 91f;
                    float reach = Mathf.Lerp(43f, 2.8f, Mathf.Pow(t, .86f));
                    int count = tier >= 14 ? 10 : 17;
                    float phase = tier * .462f + .26f;
                    for (int i = 0; i < count; i++)
                    {
                        float angle = phase + i * Mathf.PI * 2f / count + Range(random, -.115f, .115f);
                        var axis = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                        var side = Vector3.Cross(Vector3.up, axis);
                        float length = reach * Range(random, .86f, 1.12f);
                        // Keep the lowest boughs clear of the nearby summit and rail route.
                        if (tier < 5 && Vector3.Dot(axis, new Vector3(-.58f, 0f, -.81f)) > .25f)
                            length *= .58f;
                        var root = treeCenter + Vector3.up * (y + Range(random, -1.4f, 1.4f));
                        var bend = root + axis * (length * .47f) + Vector3.down * (.08f * length);
                        var end = root + axis * length + Vector3.down * (.135f * length) + side * Range(random, -2.5f, 2.5f);
                        float girth = Mathf.Max(.09f, length * .025f);
                        stems.Tube(root, bend, girth, girth * .6f, 7);
                        stems.Tube(bend, end, girth * .6f, .08f, 6);
                        if (tier < 13)
                            tips.Add(new FirTip(Vector3.Lerp(bend, end, .90f), tier, i));
                        int brushCount = mobile ? 10 : 15;
                        for (int j = 0; j < brushCount; j++)
                        {
                            float v = (.92f * j / (brushCount - 1)) + .08f;
                            var basePoint = v < .47f ? Vector3.Lerp(root, bend, v / .47f) : Vector3.Lerp(bend, end, (v - .47f) / .53f);
                            float span = Mathf.Max(1.1f, length * (.26f - .135f * v));
                            for (int sign = -1; sign <= 1; sign += 2)
                            {
                                var shoot = basePoint + side * (sign * span * Range(random, .85f, 1.38f)) +
                                    axis * (span * Range(random, .82f, 1.55f)) + Vector3.up * (span * Range(random, -.45f, .38f));
                                var fill = (j + i + sign) % 4 == 0 ? bright : shaded;
                                var needled = shoot - basePoint;
                                var twigSide = Vector3.Cross(Vector3.up, needled).normalized;
                                int tile = (j + i + tier + sign + 4) % FirTiles.Length;
                                fill.Twig(basePoint, shoot, twigSide + Vector3.up * Range(random, -.45f, .55f),
                                    span * Range(random, .7f, 1.03f), FirTiles[tile]);
                                if (!mobile || j % 2 == 0)
                                    shaded.Twig(basePoint + Vector3.up * .17f, shoot + Vector3.up * (span * .18f),
                                        Vector3.up + twigSide * .45f, span * .53f, FirTiles[(tile + 1) % FirTiles.Length]);
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

        static readonly Rect[] FirTiles =
        {
            new Rect(0f, 0f, 1f, 1f)
        };

        Material FirFoliage(string name, Color tint)
        {
            var material = Surface(name, "Noble Fir Sprig", tint, .24f, Vector2.one);
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", .36f);
            material.SetFloat("_Cull", 0f);
            material.SetFloat("_EnvironmentReflections", 0f);
            material.SetFloat("_SpecularHighlights", 0f);
            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.EnableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.renderQueue = (int)RenderQueue.AlphaTest;
            return material;
        }

        void DressFir(List<FirTip> tips, System.Random random)
        {
            var bulbColors = new[] { new Color(1f, .45f, .035f), new Color(1f, .005f, .005f),
                new Color(.01f, .85f, .65f), new Color(.015f, .10f, 1f) };
            var ornamentColors = new[] { new Color(.78f, .05f, .08f), new Color(.95f, .62f, .20f),
                new Color(.07f, .20f, .78f), new Color(.82f, .79f, .76f) };
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
                var bulb = tip.position + Vector3.down * Range(random, 1.1f, 1.9f);
                var tint = (tip.tier + tip.ordinal * 3) % bulbs.Length;
                // Bulbs and baubles are sized to read from across the room, like the approved picture.
                bulbs[tint].Add(Part(sphere, bulb, new Vector3(1.5f, 2.1f, 1.5f) * Range(random, .85f, 1.2f)));
                beadCaps.Add(Part(sphere, bulb + Vector3.up * 1.2f, new Vector3(.9f, .7f, .9f)));
                if ((tip.ordinal + tip.tier) % 3 == 0)
                {
                    Vector3 center = tip.position + Vector3.down * Range(random, 3.2f, 4.6f);
                    float diameter = Range(random, 3.4f, 5.4f);
                    ornaments[random.Next(ornaments.Length)].Add(Part(sphere, center, Vector3.one * diameter));
                    beadCaps.Add(Part(sphere, center + Vector3.up * (diameter * .48f), new Vector3(1.0f, .8f, 1.0f)));
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
                var glass = Surface("Lacquered glass ornament " + i, null, ornamentColors[i], .94f,
                    Vector2.one, .80f, 0f);
                Combined("Glass baubles " + i, ornaments[i], glass, ShadowCastingMode.On);
                Combined("Vintage colored bulbs " + i, bulbs[i],
                    Glow("Colored enamel bulb " + i, bulbColors[i], 16f), ShadowCastingMode.Off);
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
                    faces.Add(a); faces.Add(a + 1); faces.Add(a + 2);
                    faces.Add(a + 2); faces.Add(a + 1); faces.Add(a + 3);
                }
            }

            public void Twig(Vector3 stem, Vector3 tip, Vector3 spread, float halfWidth, Rect region)
            {
                spread = spread.normalized * halfWidth;
                int first = vertices.Count;
                vertices.Add(stem - spread * .82f);
                vertices.Add(stem + spread * .82f);
                vertices.Add(tip + spread);
                vertices.Add(tip - spread);
                uv.Add(new Vector2(region.xMin, region.yMin));
                uv.Add(new Vector2(region.xMax, region.yMin));
                uv.Add(new Vector2(region.xMax, region.yMax));
                uv.Add(new Vector2(region.xMin, region.yMax));
                faces.Add(first); faces.Add(first + 1); faces.Add(first + 2);
                faces.Add(first); faces.Add(first + 2); faces.Add(first + 3);
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
