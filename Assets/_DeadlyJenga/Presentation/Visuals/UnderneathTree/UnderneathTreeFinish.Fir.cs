using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyArt
{
    public sealed partial class UnderneathTreeFinish
    {
        Texture2D firPalette;

        // The tree is a model built in Blender (ArtDirection/.../models/fir.py), one mesh per material.
        void BuildFir()
        {
            var trunkAnchor = world.Root.Find("Underneath the Tree Layout/Fir trunk collision");
            var treeCenter = trunkAnchor ? Vector3.ProjectOnPlane(art.InverseTransformPoint(trunkAnchor.position), Vector3.up) :
                new Vector3(-37f, 0f, -35.7f);

            AddModel("Fir trunk and branches", "Fir Trunk", treeCenter, ShadowCastingMode.On,
                Surface("Fir bark", "Pine Scan", new Color(.72f, .47f, .30f), .15f, new Vector2(1f, 1f), 0f, .9f));
            AddModel("Fir needle boughs", "Fir Boughs Shade", treeCenter, ShadowCastingMode.TwoSided,
                FirFoliage("Fir needles in shade", new Color(.42f, .70f, .40f)));
            AddModel("Fir highlights", "Fir Boughs Light", treeCenter, ShadowCastingMode.TwoSided,
                FirFoliage("Fir needles in light", new Color(.75f, 1.05f, .55f)));
            AddModel("Fir shadowed heart", "Fir Core", treeCenter, ShadowCastingMode.On,
                Surface("Fir shadowed heart", null, new Color(.03f, .10f, .045f), .05f, Vector2.one));

            // One glass material for every bauble: the model's UVs pick a colour from this strip.
            firPalette = new Texture2D(4, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            firPalette.SetPixels(new[] { new Color(.95f, .60f, .16f), new Color(.80f, .03f, .06f),
                new Color(.05f, .16f, .80f), new Color(.86f, .56f, .52f) });
            firPalette.Apply();
            var glass = Surface("Lacquered glass ornament", null, Color.white, .94f, Vector2.one, .55f, 0f);
            glass.SetTexture("_BaseMap", firPalette);
            // A little self-colour keeps the glass from going black where the room reflection is dark.
            glass.SetTexture("_EmissionMap", firPalette);
            glass.SetColor("_EmissionColor", new Color(.10f, .10f, .10f));
            glass.EnableKeyword("_EMISSION");
            AddModel("Glass baubles", "Fir Baubles", treeCenter, ShadowCastingMode.On, glass);
            AddModel("Gold ornament fittings", "Fir Brass", treeCenter, ShadowCastingMode.Off,
                Surface("Brass ornament fittings", null, new Color(.95f, .66f, .26f), .82f, Vector2.one, .85f));
            AddModel("Fir light strings", "Fir Wire", treeCenter, ShadowCastingMode.Off,
                Surface("Green vintage light cable", null, new Color(.02f, .05f, .025f), .3f, Vector2.one));
            LightFir(treeCenter);
        }

        void AddModel(string label, string model, Vector3 position, ShadowCastingMode shadows, Material material)
        {
            var mesh = Resources.Load<Mesh>(ResourcePath + model);
            if (!mesh)
                return;
            // Model meshes are project files, so they are not added to the list of meshes to destroy.
            var obj = new GameObject(label);
            obj.transform.SetParent(art, false);
            obj.transform.localPosition = position;
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = obj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows;
        }

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

        // Bulbs and sparkles come from "Fir Lights": one line each, "B x y z colour" or "S x y z 0".
        void LightFir(Vector3 treeCenter)
        {
            var list = Resources.Load<TextAsset>(ResourcePath + "Fir Lights");
            if (!list)
                return;
            var bulbColors = new[] { new Color(1f, .07f, .04f), new Color(1f, .70f, .22f),
                new Color(.04f, .85f, .62f), new Color(1f, .36f, .04f) };
            var sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            var bulbs = new List<CombineInstance>[bulbColors.Length];
            for (int i = 0; i < bulbs.Length; i++)
                bulbs[i] = new List<CombineInstance>();
            var halos = new HaloMesh();
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            foreach (var line in list.text.Split('\n'))
            {
                var words = line.Trim().Split(' ');
                if (words.Length < 5)
                    continue;
                var position = treeCenter + new Vector3(float.Parse(words[1], culture),
                    float.Parse(words[2], culture), float.Parse(words[3], culture));
                if (words[0] == "B")
                {
                    int tint = int.Parse(words[4], culture) % bulbColors.Length;
                    bulbs[tint].Add(Part(sphere, position, new Vector3(.75f, 1.0f, .75f)));
                    halos.Add(position, 6.5f, .08f, bulbColors[tint] * 2.2f);
                }
                else
                    halos.Add(position, 1.4f, 1f, new Color(1f, .78f, .42f) * 1.6f);
            }
            for (int i = 0; i < bulbs.Length; i++)
                Combined("Vintage colored bulbs " + i, bulbs[i],
                    Glow("Colored enamel bulb " + i, bulbColors[i], 3.5f), ShadowCastingMode.Off);

            var shader = Resources.Load<Shader>(ResourcePath + "Christmas Glow");
            if (!shader)
                return;
            var glow = new Material(shader) { name = "Fir light halos" };
            materials.Add(glow);
            AddMesh("Fir light halos", halos.Build(), glow, ShadowCastingMode.Off).receiveShadows = false;
        }

        // Each halo is four corners at the light's own position; the shader spreads them to face the camera.
        sealed class HaloMesh
        {
            readonly List<Vector3> vertices = new List<Vector3>();
            readonly List<Vector2> corners = new List<Vector2>();
            readonly List<Vector2> sizes = new List<Vector2>();
            readonly List<Color> colors = new List<Color>();
            readonly List<int> faces = new List<int>();

            // core = how much of a hot white centre the halo has (0 to 1).
            public void Add(Vector3 position, float size, float core, Color color)
            {
                int first = vertices.Count;
                for (int i = 0; i < 4; i++)
                {
                    vertices.Add(position);
                    corners.Add(new Vector2(i == 1 || i == 2 ? 1f : 0f, i >= 2 ? 1f : 0f));
                    sizes.Add(new Vector2(size, core));
                    colors.Add(color);
                }
                faces.Add(first); faces.Add(first + 1); faces.Add(first + 2);
                faces.Add(first); faces.Add(first + 2); faces.Add(first + 3);
            }

            public Mesh Build()
            {
                var mesh = new Mesh { name = "Fir light halos", indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(vertices);
                mesh.SetUVs(0, corners);
                mesh.SetUVs(1, sizes);
                mesh.SetColors(colors);
                mesh.SetTriangles(faces, 0);
                var bounds = mesh.bounds;
                bounds.Expand(12f);
                mesh.bounds = bounds;
                return mesh;
            }
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
