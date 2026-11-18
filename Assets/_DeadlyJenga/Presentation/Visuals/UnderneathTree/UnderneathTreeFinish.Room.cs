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
            // Deep jewel colours, like the approved picture.
            if (color.b > color.r * 2f && color.b > color.g) color = new Color(.015f, .07f, .52f);
            else if (color.g > color.r * 2f && color.g > color.b) color = new Color(.01f, .30f, .08f);
            else if (color.r > color.g * 3f) color = new Color(.70f, .004f, .03f);
            else color = new Color(1f, .80f, .55f);
            // Foil film, not paper: part mirror, glossy, slightly crinkled.
            // Cream is already bright, so it is less mirror-like and keeps less of its own glow.
            bool cream = color.g > .5f;
            var material = Lacquer(label, color, .74f, cream ? .3f : .55f, .35f, cream ? .05f : .07f);
            if (!material.HasProperty("_Coat")) return material;
            material.SetFloat("_CoatSmoothness", .85f);
            material.SetTexture("_BaseMap", Resources.Load<Texture2D>(ResourcePath + "Gold Snowflake Foil"));
            material.SetTexture("_BumpMap", Resources.Load<Texture2D>(ResourcePath + "Paper Normal"));
            // A few big snowflakes on each face, like the approved picture.
            material.SetTextureScale("_BaseMap", new Vector2(1.3f, 1.3f));
            material.SetFloat("_BaseShade", .55f);
            material.SetFloat("_BumpScale", .12f);
            material.SetColor("_FoilColor", new Color(1f, .68f, .16f));
            material.SetFloat("_FoilGlow", .7f);
            material.SetFloat("_TopLight", .2f);
            material.SetFloat("_EdgeLight", .35f);
            material.SetFloat("_Foil", cream ? .45f : .55f);
            material.SetFloat("_Crinkle", .8f);
            material.SetFloat("_CrinkleSize", 1.8f);
            material.SetFloat("_EdgeCrinkle", 1.5f);
            material.SetFloat("_Tuck", .55f);
            return material;
        }

        // Glossy coated paper or satin: a colour under a clear shiny layer.
        // glow = how much of its own colour it keeps in shadow, so it never goes grey.
        Material Lacquer(string label, Color color, float smoothness, float metallic, float coat, float glow)
        {
            if (finishes.TryGetValue(label, out var cached)) return cached;
            var shader = Resources.Load<Shader>(ResourcePath + "Christmas Foil Paper");
            if (!shader) return Surface(label, null, color, smoothness, Vector2.one, metallic);
            var material = new Material(shader) { name = label, color = color, enableInstancing = true };
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Coat", coat);
            material.SetFloat("_CoatSmoothness", .78f);
            // The colour of the lamp glow the foil mirrors.
            material.SetColor("_GlossColor", new Color(1f, .80f, .58f));
            material.SetFloat("_EdgeLight", .5f);
            material.SetFloat("_TopLight", .35f);
            material.SetFloat("_Sheen", 0f);
            material.SetFloat("_BumpScale", 0f);
            material.SetColor("_EmissionColor", new Color(glow, glow, glow));
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
                // The approved picture only has gold and red ribbon, so ivory becomes pale gold.
                if (name.Contains("Gold") || name.Contains("gold"))
                    color = new Color(.95f, .52f, .08f);
                else if (name.Contains("Ivory"))
                    color = new Color(.95f, .66f, .22f);
                else if (color.r > color.g * 3f)
                    color = new Color(.55f, .008f, .03f);
                var satin = Lacquer("Satin " + name, color, .8f, .7f, .35f, .08f);
                satin.SetFloat("_Foil", .6f);
                satin.SetFloat("_Crinkle", .4f);
                return satin;
            }
            if (name.Contains("gift wrap") || name.Contains("gift Wrap") || name.Contains("Mulberry"))
                return FoilPaper(name + " paper", color);
            if (name.StartsWith("Felt"))
                return Fabric("Cranberry tufted tree skirt", "Cranberry Rug Weave", new Color(1.25f, .72f, .84f),
                    new Vector2(12f, 12f), .12f, .65f);
            if (name.StartsWith("Fluff"))
                return Surface("Ivory fleece", "Woven", new Color(.84f, .75f, .63f), .25f, new Vector2(10, 8), 0f, 1f);
            if (objectName.StartsWith("Wood Planks") || name.Contains("Mahogany"))
                return Surface("French polished walnut", "Walnut Grain", new Color(.40f, .23f, .15f), .86f, new Vector2(2.2f, 6.1f), 0f, .42f);
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

        // Warm cream walls, like the family rooms in Evan's photos.
        Material WallCover() => Surface("Cream botanical wallcovering", "Botanical Paper", new Color(.96f, .87f, .70f),
            .22f, new Vector2(13, 5), 0f, .3f);

        // The family room: the tree stands in the north-east corner, the room opens out to the
        // south and west. Map units; the map root is the origin.
        const float RoomTop = 400f;
        const float RoomWest = -640f, RoomEast = 60f, RoomSouth = -600f, RoomNorth = 58f;
        const float LoftHeight = 200f, LoftEdge = -520f;
        // Openings: tall window wall behind the tree, hallway in the south wall,
        // archway to the kitchen in the west wall, under the loft.
        const float WindowWest = -360f, WindowEast = -80f, WindowSill = 25f, WindowHead = 290f;
        const float HallWest = -210f, HallEast = -140f, HallHeight = 150f;
        const float ArchSouth = -170f, ArchNorth = -50f, ArchHeight = 170f;
        // Fireplace on the east wall, south of the rug.
        const float HearthZ = -290f;

        void BuildRoom()
        {
            var plaster = WallCover();
            var wood = Surface("Warm oak wainscot", "Mahogany", new Color(.31f, .186f, .123f),
                .49f, new Vector2(10, 3f), 0f, .4f);
            var casing = Surface("Cream window casing", "Paper", new Color(.79f, .68f, .54f),
                .48f, new Vector2(2, 2));
            var beam = Surface("Dark timber", "Walnut Grain", new Color(.30f, .19f, .12f), .35f, new Vector2(1, 6));

            WallAlongX("North wall", RoomNorth, RoomWest, RoomEast, WindowWest, WindowEast, WindowSill, WindowHead, plaster);
            WallAlongX("South wall", RoomSouth, RoomWest, RoomEast, HallWest, HallEast, 0f, HallHeight, plaster);
            WallAlongZ("West wall", RoomWest, RoomSouth, RoomNorth, ArchSouth, ArchNorth, 0f, ArchHeight, plaster);
            WallAlongZ("East wall", RoomEast, RoomSouth, RoomNorth, 0f, 0f, 0f, 0f, plaster);
            float midX = (RoomWest + RoomEast) * .5f, midZ = (RoomSouth + RoomNorth) * .5f;
            float spanX = RoomEast - RoomWest, spanZ = RoomNorth - RoomSouth;
            foreach (var side in new[] { (RoomNorth - 2.2f, true), (RoomSouth + 2.2f, true), (RoomWest + 2.2f, false), (RoomEast - 2.2f, false) })
            {
                var at = side.Item2 ? new Vector3(midX, 0f, side.Item1) : new Vector3(side.Item1, 0f, midZ);
                var length = side.Item2 ? new Vector3(spanX, 0f, 2f) : new Vector3(2f, 0f, spanZ);
                Box("Oak wainscot", at + Vector3.up * 8f, length + Vector3.up * 16f, wood);
                Box("Chair rail", at + Vector3.up * 16.7f, length * 1.0f + Vector3.up * 1.9f + (side.Item2 ? Vector3.forward : Vector3.right) * .5f, casing);
                Box("Crown", at + Vector3.up * (RoomTop - 4.7f), length + Vector3.up * 5.2f + (side.Item2 ? Vector3.forward : Vector3.right) * 2.3f, casing);
            }
            Box("Plaster ceiling", new Vector3(midX, RoomTop + 1.5f, midZ), new Vector3(spanX + 8f, 3f, spanZ + 8f),
                Surface("Plaster ceiling", "Wallpaper", new Color(.72f, .62f, .53f), .17f, new Vector2(8, 8)))
                .GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            // Timber beams across the tall ceiling, like a lodge great room.
            for (float x = RoomWest + 90f; x < RoomEast; x += 120f)
                Box("Ceiling beam", new Vector3(x, RoomTop - 9f, midZ), new Vector3(14f, 18f, spanZ), beam);

            // Tall snowy window wall behind the tree.
            Box("Window header", new Vector3((WindowWest + WindowEast) * .5f, WindowHead, RoomNorth - 2f), new Vector3(WindowEast - WindowWest + 6f, 5f, 6f), casing);
            Box("Window sill", new Vector3((WindowWest + WindowEast) * .5f, WindowSill, RoomNorth - 3f), new Vector3(WindowEast - WindowWest + 6f, 4f, 9f), casing);
            Box("Window transom", new Vector3((WindowWest + WindowEast) * .5f, 160f, RoomNorth - 2f), new Vector3(WindowEast - WindowWest, 3f, 5f), casing);
            for (float x = WindowWest; x <= WindowEast + .1f; x += (WindowEast - WindowWest) / 4f)
                Box("Window mullion", new Vector3(x, (WindowSill + WindowHead) * .5f, RoomNorth - 2f), new Vector3(3f, WindowHead - WindowSill, 5f), casing);
            // A real 3-D snowy night outside instead of a photo (UnderneathTreeFinish.Outside.cs).
            BuildOutdoorView();

            // Stone fireplace on the east wall (see UnderneathTreeFinish.Fireplace.cs).
            BuildFireplace();

            // Two-storey west side: a loft balcony on posts, stairs up along the south wall.
            Box("Loft floor", new Vector3((RoomWest + LoftEdge) * .5f, LoftHeight + 4f, midZ), new Vector3(LoftEdge - RoomWest, 8f, spanZ), wood);
            Box("Loft rail", new Vector3(LoftEdge, LoftHeight + 24f, midZ + 30f), new Vector3(3f, 3f, spanZ - 60f), beam);
            for (float z = RoomSouth + 90f; z < RoomNorth; z += 22f)
                Box("Loft baluster", new Vector3(LoftEdge, LoftHeight + 16f, z), new Vector3(1.6f, 16f, 1.6f), beam);
            for (float z = RoomSouth + 120f; z < RoomNorth - 20f; z += 160f)
                Box("Loft post", new Vector3(LoftEdge, LoftHeight * .5f, z), new Vector3(8f, LoftHeight, 8f), beam);
            const int steps = 20;
            float stairFoot = -290f, stairDepth = 50f;
            for (int i = 0; i < steps; i++)
            {
                float run = (stairFoot - LoftEdge) / steps;
                float rise = LoftHeight * (i + 1) / steps;
                Box("Stair step", new Vector3(stairFoot - run * (i + .5f), rise * .5f, RoomSouth + stairDepth * .5f + 2f),
                    new Vector3(run, rise, stairDepth), wood);
            }
            Box("Stair rail", new Vector3((stairFoot + LoftEdge) * .5f, LoftHeight * .5f + 30f, RoomSouth + stairDepth + 2f),
                new Vector3(Mathf.Sqrt((stairFoot - LoftEdge) * (stairFoot - LoftEdge) + LoftHeight * LoftHeight), 3f, 3f), beam)
                .localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(LoftHeight, stairFoot - LoftEdge) * Mathf.Rad2Deg);

            // Rooms beyond the openings: just a lit glimpse, the player never goes there.
            var glow = Glow("Warm room beyond", new Color(1f, .62f, .32f), .9f);
            Box("Hallway floor", new Vector3((HallWest + HallEast) * .5f, -.2f, RoomSouth - 70f), new Vector3(HallEast - HallWest + 20f, .4f, 140f), wood);
            Box("Hallway wall", new Vector3(HallWest - 10f, HallHeight * .5f, RoomSouth - 70f), new Vector3(3f, HallHeight, 140f), plaster);
            Box("Hallway wall", new Vector3(HallEast + 10f, HallHeight * .5f, RoomSouth - 70f), new Vector3(3f, HallHeight, 140f), plaster);
            Box("Hallway end", new Vector3((HallWest + HallEast) * .5f, HallHeight * .5f, RoomSouth - 140f), new Vector3(HallEast - HallWest + 20f, HallHeight, 3f), glow);
            Box("Kitchen floor", new Vector3(RoomWest - 90f, -.2f, (ArchSouth + ArchNorth) * .5f), new Vector3(180f, .4f, ArchNorth - ArchSouth + 60f), wood);
            Box("Kitchen wall", new Vector3(RoomWest - 180f, ArchHeight * .5f, (ArchSouth + ArchNorth) * .5f), new Vector3(3f, ArchHeight, ArchNorth - ArchSouth + 60f), glow);

            // Bookshelves and a piano by the window.
            var rand = new System.Random(77);
            Box("Bookcase", new Vector3(RoomWest + 20f, 90f, -330f), new Vector3(34f, 180f, 130f), beam);
            for (int shelf = 0; shelf < 5; shelf++)
                for (float z = -390f; z < -272f;)
                {
                    float thick = Range(rand, 4f, 8f), tall = Range(rand, 18f, 28f);
                    var book = Surface("Book cloth " + (shelf * 7 + (int)z) % 5, "Woven",
                        Color.HSVToRGB(Range(rand, 0f, 1f) < .5f ? .0f : .35f, .6f, Range(rand, .25f, .6f)), .3f, Vector2.one);
                    Box("Book", new Vector3(RoomWest + 38f, 12f + shelf * 34f + tall * .5f, z + thick * .5f), new Vector3(4f, tall, thick - .6f), book);
                    z += thick;
                }
            var lacquer = Lacquer("Piano black", new Color(.02f, .015f, .015f), .9f, 0f, .8f, 0f);
            Box("Upright piano", new Vector3(-430f, 45f, RoomNorth - 22f), new Vector3(110f, 90f, 36f), lacquer);
            Box("Piano bench", new Vector3(-430f, 18f, RoomNorth - 70f), new Vector3(60f, 36f, 26f), lacquer);
        }

        // A wall running east-west at z, with one opening from x0 to x1 between heights low and high.
        void WallAlongX(string label, float z, float west, float east, float x0, float x1, float low, float high, Material material)
        {
            if (x1 <= x0)
            {
                Wall(label, west, east, 0f, RoomTop, z, material);
                return;
            }
            Wall(label, west, x0, 0f, RoomTop, z, material);
            Wall(label, x1, east, 0f, RoomTop, z, material);
            Wall(label + " over opening", x0, x1, high, RoomTop, z, material);
            if (low > 0f)
                Wall(label + " under opening", x0, x1, 0f, low, z, material);
        }

        void WallAlongZ(string label, float x, float south, float north, float z0, float z1, float low, float high, Material material)
        {
            void Side(string name, float a, float b, float bottom, float top) =>
                Box(name, new Vector3(x, (bottom + top) * .5f, (a + b) * .5f), new Vector3(3f, top - bottom, b - a), material)
                    .GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            if (z1 <= z0)
            {
                Side(label, south, north, 0f, RoomTop);
                return;
            }
            Side(label, south, z0, 0f, RoomTop);
            Side(label, z1, north, 0f, RoomTop);
            Side(label + " over opening", z0, z1, high, RoomTop);
            if (low > 0f)
                Side(label + " under opening", z0, z1, 0f, low);
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
