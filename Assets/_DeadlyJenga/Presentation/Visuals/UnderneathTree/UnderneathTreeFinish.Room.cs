using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyArt
{
    public sealed partial class UnderneathTreeFinish
    {
        Material Surface(string label, string tile, Color color, float smoothness, Vector2 repeat,
            float metallic = 0f, float relief = .6f)
        {
            if (finishes.TryGetValue(label, out var cached))
                return cached;
            var material = new Material(template) { name = label, color = color };
            material.DisableKeyword("_EMISSION");
            material.DisableKeyword("_NORMALMAP");
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            if (tile != null)
            {
                material.SetTexture("_BaseMap", Resources.Load<Texture2D>(ResourcePath + tile));
                material.SetTextureScale("_BaseMap", repeat);
                var normalTile = tile switch {
                    "Botanical Paper" or "Winter Print" => "Paper",
                    "Walnut Grain" => "Mahogany",
                    "Red Velvet" => "Woven",
                    _ => tile
                };
                var normal = Resources.Load<Texture2D>(ResourcePath + normalTile + " Normal");
                if (normal)
                {
                    material.SetTexture("_BumpMap", normal);
                    material.SetFloat("_BumpScale", relief);
                    material.EnableKeyword("_NORMALMAP");
                }
            }
            materials.Add(material);
            finishes.Add(label, material);
            return material;
        }

        Material FoilPaper(string label, Color color)
        {
            if (finishes.TryGetValue(label, out var cached)) return cached;
            var shader = Resources.Load<Shader>(ResourcePath + "Christmas Foil Paper");
            if (!shader) return Surface(label, "Winter Print", color, .4f, new Vector2(1.5f, 1.5f));
            if (color.b > color.r * 2f) color = new Color(.018f, .065f, .30f);
            else if (color.g > color.r * 2f) color = new Color(.025f, .26f, .105f);
            else if (color.r > color.g * 3f) color = new Color(.58f, .018f, .045f);
            var material = new Material(shader) { name = label, color = color, enableInstancing = true };
            material.SetTexture("_BaseMap", Resources.Load<Texture2D>(ResourcePath + "Gold Snowflake Foil"));
            material.SetTexture("_BumpMap", Resources.Load<Texture2D>(ResourcePath + "Paper Normal"));
            material.SetTextureScale("_BaseMap", new Vector2(1.5f, 1.5f));
            material.SetFloat("_BumpScale", .16f);
            material.SetFloat("_Smoothness", .36f);
            material.SetFloat("_Sheen", 0f);
            material.SetColor("_FoilColor", new Color(.95f, .65f, .22f));
            materials.Add(material);
            finishes.Add(label, material);
            return material;
        }

        Material Glow(string label, Color color, float brightness)
        {
            var material = Surface(label, null, color, .58f, Vector2.one);
            material.SetColor("_EmissionColor", color * brightness);
            material.EnableKeyword("_EMISSION");
            return material;
        }

        void ApplyFinish(MeshRenderer renderer)
        {
            var slotMaterials = renderer.sharedMaterials;
            var result = new Material[slotMaterials.Length];
            bool changed = false;
            for (int i = 0; i < slotMaterials.Length; i++)
            {
                var old = slotMaterials[i];
                result[i] = old;
                if (old)
                {
                    var surface = ChooseFinish(renderer.name, old);
                    if (surface)
                    {
                        result[i] = surface;
                        changed = true;
                    }
                }
            }
            if (!changed)
                return;
            originalMaterials[renderer] = slotMaterials;
            renderer.sharedMaterials = result;
        }

        Material ChooseFinish(string objectName, Material source)
        {
            string name = source.name;
            var color = (source.HasProperty("_BaseColor") || source.HasProperty("_Color")) ? source.color : Color.white;
            if (name.Contains("Skirt skull") || name.Contains("red stitching") || name.Contains("Crimson stitching"))
                return null;
            if (name.StartsWith("Wrap ") || name.StartsWith("Outer Wrap "))
            {
                int n = name[name.Length - 1] - '0';
                if (n >= 0 && n < GiftColors.Length)
                    color = GiftColors[n];
                return Surface("Lacquered " + name, n % 3 == 0 || n == 4 ? "Botanical Paper" : "Winter Print",
                    color, .38f, new Vector2(1.8f, 1.8f), 0f, .55f);
            }
            if (name.Contains("ribbon") || name.Contains("Ribbon") || name.Contains("Satin"))
            {
                bool gold = name.Contains("Gold") || name.Contains("gold");
                if (gold)
                    color = new Color(.85f, .55f, .18f);
                return Surface("Satin " + name, "Woven", color, .69f, new Vector2(2, 3), gold ? .25f : .06f, .42f);
            }
            if (name.Contains("gift wrap") || name.Contains("gift Wrap") || name.Contains("Mulberry"))
                return FoilPaper(name + " paper", color);
            if (name.StartsWith("Felt"))
                return Fabric("Cranberry tufted tree skirt", "Cranberry Rug Weave", Color.white,
                    new Vector2(12f, 12f), .12f, .65f);
            if (name.StartsWith("Fluff"))
                return Surface("Ivory fleece", "Woven", new Color(.84f, .75f, .63f), .25f, new Vector2(10, 8), 0f, 1f);
            if (objectName.StartsWith("Wood Planks") || name.Contains("Mahogany"))
                return Surface("French polished walnut", "Walnut Grain", new Color(.72f, .50f, .33f), .59f, new Vector2(2.2f, 6.1f), 0f, .42f);
            if (name.Contains("Wallpap"))
                return WallCover();
            if (name == "Brick")
                return Surface("Fireplace terracotta", "Brick", new Color(.55f, .34f, .252f), .30f, new Vector2(3, 1.7f), 0f, .75f);
            if (name.Contains("Sofa"))
                return Surface("Burgundy velvet", "Red Velvet", new Color(.85f, .61f, .65f), .25f, new Vector2(4.5f, 3f), 0f, .55f);
            if (name.StartsWith("Stocking"))
                return Surface("Knitted " + name, "Woven", color, .32f, new Vector2(2, 3));
            if (name.Contains("Dark Wood") || name.Contains("track bed") || name.Contains("Track Tie"))
                return Surface("Stained " + name, "Walnut Grain", new Color(.54f, .37f, .24f), .52f, new Vector2(1, 2));
            if (name.Contains("Track Rail") || name.Contains("brass"))
                return Surface("Polished " + name, "Paper", name.Contains("brass") ?
                    new Color(.85f, .58f, .30f) : new Color(.58f, .59f, .63f), .83f, Vector2.one, .67f, .13f);
            if (name == "Lamp ivory")
                return Glow("Locomotive headlight", new Color(1f, .79f, .48f), 2.15f);
            if (name.Contains("enamel") || name.Contains("tinplate") || name.Contains("window"))
                return Surface("Gloss " + name, "Paper", color, .72f, Vector2.one, .34f, .25f);
            if (objectName.Contains("Ceiling"))
                return Surface("Plaster ceiling", "Wallpaper", new Color(.72f, .62f, .53f), .17f, new Vector2(5, 5));
            if (name == "Trim Cream")
                return Surface("Ivory painted oak", "Paper", new Color(.79f, .69f, .56f), .45f, new Vector2(3, 2));
            if (objectName.StartsWith("Sofa") || name.Contains("Burgundy"))
                return Surface("Velvet " + name, "Woven", color, .28f, new Vector2(4, 3));
            return null;
        }

        static readonly Color[] GiftColors =
        {
            new Color(.65f, .105f, .153f), new Color(.14f, .38f, .28f),
            new Color(.86f, .76f, .59f), new Color(.27f, .134f, .35f),
            new Color(.18f, .27f, .51f), new Color(.75f, .46f, .18f), new Color(.33f, .49f, .44f)
        };

        Material WallCover() => Surface("Cobalt botanical wallcovering", "Botanical Paper", new Color(.08f, .17f, .52f),
            .28f, new Vector2(13, 5), 0f, .38f);

        void BuildRoom()
        {
            var plaster = WallCover();
            var wood = Surface("Warm oak wainscot", "Mahogany", new Color(.31f, .186f, .123f),
                .49f, new Vector2(10, 3f), 0f, .4f);
            var casing = Surface("Cream window casing", "Paper", new Color(.79f, .68f, .54f),
                .48f, new Vector2(2, 2));
            var curtain = Surface("Cranberry velvet curtain", "Woven", new Color(.48f, .075f, .106f),
                .29f, new Vector2(7, 9));
            const float z = 77f;
            const float left = -34f;
            const float right = 39f;
            const float sill = 22f;
            const float head = 78f;
            Wall("North wall left bay", -260f, left, 0f, 177f, z, plaster);
            Wall("North wall right bay", right, 82f, 0f, 177f, z, plaster);
            Wall("North wall overhead", left, right, head, 177f, z, plaster);
            Wall("North wall below window", left, right, 0f, sill, z, plaster);
            Box("West wall", new Vector3(-258f, 88.5f, -92f), new Vector3(3.0f, 177f, 343f), plaster)
                .GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            Box("West oak wainscot", new Vector3(-255.7f, 8f, -92f), new Vector3(2f, 16f, 343f), wood);
            Box("North oak wainscot", new Vector3(-88f, 7.8f, z - 2.2f), new Vector3(343f, 15.6f, 2f), wood);
            Box("North chair rail", new Vector3(-88f, 16.7f, z - 3.0f), new Vector3(343f, 1.9f, 2.5f), casing);
            Box("West chair rail", new Vector3(-254.7f, 16.7f, -92f), new Vector3(2.5f, 1.9f, 343f), casing);
            Box("North crown", new Vector3(-88f, 172.3f, z - 3.2f), new Vector3(343f, 5.2f, 4.3f), casing);
            Box("West crown", new Vector3(-254.5f, 172.3f, -92f), new Vector3(4.3f, 5.2f, 343f), casing);
            Box("Window header", new Vector3(2.5f, head, z - 2f), new Vector3(81f, 3.4f, 5.2f), casing);
            Box("Window sill", new Vector3(2.5f, sill, z - 3f), new Vector3(81f, 3.4f, 7.4f), casing);
            foreach (float x in new[] { left, right, 2.5f })
                Box("Window mullion", new Vector3(x, 50f, z - 2f), new Vector3(2.3f, 55f, 4.3f), casing);
            Box("Window meeting rail", new Vector3(2.5f, 52f, z - 2f), new Vector3(73f, 2.0f, 4.5f), casing);
            Box("Curtain rod", new Vector3(2.5f, 87f, z - 10.3f), new Vector3(111f, 1.2f, 1.4f), wood);
            Curtain("Left drape", -47f, 87f, z - 10.5f, curtain);
            Curtain("Right drape", 52f, 87f, z - 10.5f, curtain);
            NightWindow("Front window snow", new Vector3(2.5f, 50f, z + 1.3f), new Vector2(74, 57), Quaternion.identity);
            NightWindow("Side window snow", new Vector3(84.5f, 98.6f, -130f), new Vector2(106, 86), Quaternion.Euler(0f, 90f, 0f));
            BuildHearth();
        }

        void Wall(string label, float left, float right, float low, float high, float depth, Material material)
        {
            Box(label, new Vector3((right + left) * .5f, (high + low) * .5f, depth),
                new Vector3(right - left, high - low, 3f), material)
                .GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        void NightWindow(string label, Vector3 center, Vector2 size, Quaternion rotation)
        {
            var photo = Resources.Load<Texture2D>(ResourcePath + "New Snow");
            if (!photo)
                return;
            var glass = Glow("Snowy garden glass", Color.white, .95f);
            glass.SetTexture("_BaseMap", photo);
            glass.SetTexture("_EmissionMap", photo);
            glass.SetFloat("_Cull", 0f);
            var window = Primitive(label, PrimitiveType.Quad, center, new Vector3(size.x, size.y, 1), glass);
            window.localRotation = rotation;
            var renderer = window.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        void Curtain(string label, float x, float top, float z, Material material)
        {
            const int columns = 36, rows = 10;
            var vertices = new Vector3[(columns + 1) * (rows + 1)];
            var uv = new Vector2[vertices.Length];
            var tris = new int[columns * rows * 6];
            int index = 0;
            for (int r = 0; r <= rows; r++)
            for (int c = 0; c <= columns; c++)
            {
                float u = c / (float)columns, v = r / (float)rows;
                float wave = Mathf.Sin(u * Mathf.PI * 12 + .34f * v);
                vertices[index] = new Vector3(x + (u - .5f) * Mathf.Lerp(19, 25, v),
                    top - v * (86f - wave * .9f), z + wave * Mathf.Lerp(1.28f, 2.15f, v));
                uv[index++] = new Vector2(u, 1f - v);
            }
            index = 0;
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < columns; c++)
            {
                int a = r * (columns + 1) + c, b = a + columns + 1;
                tris[index++] = a; tris[index++] = a + 1; tris[index++] = b;
                tris[index++] = a + 1; tris[index++] = b + 1; tris[index++] = b;
            }
            material.SetFloat("_Cull", 0f);
            var mesh = new Mesh { name = label, vertices = vertices, uv = uv, triangles = tris };
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            AddMesh(label, mesh, material, ShadowCastingMode.TwoSided);
        }

        void BuildHearth()
        {
            var amber = Glow("Amber fireglass", new Color(1f, .24f, .036f), 3.2f);
            var center = Glow("Yellow flame", new Color(1f, .56f, .10f), 4.3f);
            var log = Surface("Charred birch", "Mahogany", new Color(.14f, .071f, .039f), .33f, new Vector2(3, 2));
            var rand = new System.Random(2114);
            for (int i = 0; i < 7; i++)
            {
                var timber = Primitive("Hearth log", PrimitiveType.Cylinder,
                    new Vector3(-168f + i * 5.8f, 8f + (i % 3) * 1.7f, -240f + (i % 2) * 3.4f),
                    new Vector3(3.1f, 8.4f, 3.1f), log);
                timber.localRotation = Quaternion.Euler(18f, 20f * i, 71f);
            }
            for (int i = 0; i < 16; i++)
            {
                var pos = new Vector3(-168f + Range(rand, 0f, 39f), Range(rand, 10f, 16f), Range(rand, -241f, -236f));
                float height = Range(rand, 5.2f, 11f);
                var flame = Primitive("Hearth flame", PrimitiveType.Sphere, pos,
                    new Vector3(Range(rand, 2.4f, 4.9f), height, Range(rand, 1.5f, 2.6f)),
                    i % 3 == 0 ? center : amber);
                flame.localRotation = Quaternion.Euler(0f, 0f, Range(rand, -16f, 16f));
                flame.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        Transform Box(string label, Vector3 position, Vector3 size, Material material) =>
            Primitive(label, PrimitiveType.Cube, position, size, material);

        Transform Primitive(string label, PrimitiveType type, Vector3 position, Vector3 size, Material material)
        {
            var obj = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
            obj.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>(type + ".fbx");
            obj.transform.SetParent(art, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = size;
            obj.GetComponent<MeshRenderer>().sharedMaterial = material;
            return obj.transform;
        }

        MeshRenderer AddMesh(string label, Mesh mesh, Material material, ShadowCastingMode shadows)
        {
            var obj = new GameObject(label);
            obj.transform.SetParent(art, false);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = obj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows;
            if (!meshes.Contains(mesh))
                meshes.Add(mesh);
            return renderer;
        }

        static float Range(System.Random random, float minimum, float maximum) =>
            Mathf.Lerp(minimum, maximum, (float)random.NextDouble());
    }
}
