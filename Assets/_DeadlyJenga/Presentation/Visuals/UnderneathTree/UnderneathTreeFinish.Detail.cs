using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyArt
{
    public sealed partial class UnderneathTreeFinish
    {
        Mesh giftBow;

        void BuildRoomDetails()
        {
            BuildFloorboards();
            BuildFleece();
            BuildBraidedRugEdge();
            // Against the east wall, where the sofa used to be: a giant retriever asleep in its bed.
            BuildDogBed();
            int first = art.childCount;
            BuildLampAndTable();
            Restage("Lamp table", first, new Vector3(-215f, 0f, -11f), LampPlace, 0f);
            BuildHomeyDetails();
        }

        static readonly Vector3 LampPlace = new Vector3(RoomEast - 19f, 0f, SofaZ + 140f);

        // Gathers the pieces just built under one pivot and moves them as a group.
        void Restage(string label, int first, Vector3 from, Vector3 to, float turn, float scale = 1f)
        {
            var pivot = new GameObject(label).transform;
            pivot.SetParent(art, false);
            pivot.localPosition = from;
            for (int i = art.childCount - 2; i >= first; i--)
                art.GetChild(i).SetParent(pivot, true);
            pivot.localPosition = to;
            pivot.localRotation = Quaternion.Euler(0f, turn, 0f);
            pivot.localScale = Vector3.one * scale;
        }

        void BuildLampAndTable()
        {
            var walnut = Surface("Side table walnut", "Walnut Grain", new Color(.45f, .36f, .28f),
                .40f, new Vector2(.8f, 1.5f), 0f, .28f);
            var brass = Surface("Turned lamp brass", null, new Color(.72f, .48f, .19f), .72f, Vector2.one, .65f);
            var linen = Surface("Warm linen lampshade", "Woven", new Color(1f, .82f, .56f), .18f, new Vector2(12f, 3f), 0f, .32f);
            linen.SetColor("_EmissionColor", new Color(.35f, .19f, .06f));
            linen.EnableKeyword("_EMISSION");
            linen.SetFloat("_Cull", 0f);
            var originals = world.Root.GetComponentsInChildren<MeshRenderer>();
            foreach (var source in originals)
            {
                if (source.transform.IsChildOf(art)) continue;
                // The block-out calls its lampshade "Cone".
                if (source.name != "Lamp Shade" && source.name != "Cone" && source.name != "Lamp Base" &&
                    source.name != "Side Table") continue;
                var center = art.InverseTransformPoint(source.bounds.center);
                var size = source.bounds.size / art.lossyScale.x;
                Hide(source);
                if (source.name == "Lamp Base")
                {
                    Primitive("Turned brass lamp base", PrimitiveType.Cylinder, center,
                        new Vector3(size.x * .55f, size.y * .5f, size.z * .55f), brass);
                    Primitive("Brass lamp foot", PrimitiveType.Sphere, center - Vector3.up * size.y * .42f,
                        new Vector3(size.x * 1.1f, size.y * .22f, size.z * 1.1f), brass);
                    continue;
                }
                if (source.name == "Side Table")
                {
                    RoundedDetail("Walnut tabletop", center + Vector3.up * size.y * .46f,
                        new Vector3(size.x, size.y * .08f, size.z), .035f, walnut);
                    RoundedDetail("Walnut drawer cabinet", center + Vector3.up * size.y * .23f,
                        new Vector3(size.x * .93f, size.y * .37f, size.z * .93f), .025f, walnut);
                    foreach (int x in new[] { -1, 1 })
                    foreach (int z in new[] { -1, 1 })
                        RoundedDetail("Tapered walnut leg", center + new Vector3(x * size.x * .39f, -size.y * .23f, z * size.z * .39f),
                            new Vector3(size.x * .10f, size.y * .55f, size.z * .10f), .10f, walnut);
                    Primitive("Brass drawer pull", PrimitiveType.Sphere,
                        center + new Vector3(size.x * .48f, size.y * .23f, 0f), Vector3.one * 1.1f, brass);
                    continue;
                }
                var vertices = new List<Vector3>();
                var uv = new List<Vector2>();
                var triangles = new List<int>();
                const int sides = 96;
                float radius = Mathf.Max(size.x, size.z) * .5f;
                for (int i = 0; i <= sides; i++)
                {
                    float u = i / (float)sides, angle = u * Mathf.PI * 2f;
                    var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    float pleat = 1f + .013f * Mathf.Cos(angle * 48f);
                    vertices.Add(center + radial * (radius * pleat) - Vector3.up * size.y * .5f);
                    vertices.Add(center + radial * (radius * .59f * pleat) + Vector3.up * size.y * .5f);
                    uv.Add(new Vector2(u, 0f)); uv.Add(new Vector2(u, 1f));
                    if (i == sides) continue;
                    int a = i * 2;
                    triangles.Add(a); triangles.Add(a + 1); triangles.Add(a + 2);
                    triangles.Add(a + 2); triangles.Add(a + 1); triangles.Add(a + 3);
                }
                var mesh = new Mesh { name = "Pleated linen shade" };
                mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals(); mesh.RecalculateTangents();
                AddMesh("Tailored linen lampshade", mesh, linen, ShadowCastingMode.Off);
            }
        }

        Material Fabric(string label, string tile, Color tint, Vector2 repeat, float sheen, float bump)
        {
            if (finishes.TryGetValue(label, out var cached))
                return cached;

            var shader = Resources.Load<Shader>(ResourcePath + "Christmas Fabric");
            if (!shader)
                return Surface(label, tile, tint, .17f, repeat, 0f, bump);

            var material = new Material(shader) { name = label, color = tint };
            material.SetTexture("_BaseMap", Resources.Load<Texture2D>(ResourcePath + tile));
            material.SetTexture("_BumpMap", Resources.Load<Texture2D>(ResourcePath + (tile == "Red Velvet" ? "Woven" : tile) + " Normal"));
            material.SetTextureScale("_BaseMap", repeat);
            material.SetFloat("_BumpScale", bump);
            material.SetFloat("_Sheen", sheen);
            if (tile == "Crimson Damask")
            {
                material.SetFloat("_TextureContrast", .20f);
                material.SetColor("_TextureMidpoint", new Color(.15f, .007f, .012f));
            }
            materials.Add(material);
            finishes.Add(label, material);
            return material;
        }

        Transform RoundedDetail(string label, Vector3 position, Vector3 size, float radius, Material material,
            float loft = 0f)
        {
            var mesh = RoundedWrap(new Bounds(Vector3.zero, size), radius, loft);
            var detail = AddMesh(label, mesh, material, ShadowCastingMode.On).transform;
            detail.localPosition = position;
            return detail;
        }

        // Flat staggered boards. Each board leans a hair differently, so the reflection
        // breaks from board to board like a real floor.
        void BuildFloorboards()
        {
            Material varnish;
            var shader = Resources.Load<Shader>(ResourcePath + "Christmas Polished Floor");
            if (shader)
            {
                varnish = new Material(shader) { name = "Polished walnut floor", color = new Color(.24f, .15f, .14f) };
                varnish.SetFloat("_Mirror", 1.3f);
                varnish.SetTexture("_BaseMap", Resources.Load<Texture2D>(ResourcePath + "Walnut Grain"));
                varnish.SetTexture("_BumpMap", Resources.Load<Texture2D>(ResourcePath + "Mahogany Normal"));
                // Amber on the boards beside the lamp, blue on the boards inside the side window.
                // Lamp = where the glint comes from; wash = centre on the floor (x, z) and size.
                varnish.SetVector("_GlowALamp", new Vector4(LampPlace.x, 59f, LampPlace.z, 0f));
                varnish.SetVector("_GlowAWash", new Vector4(LampPlace.x + 12f, LampPlace.z + 8f, 60f, 0f));
                varnish.SetColor("_GlowAColor", new Color(1f, .42f, .08f) * 1.5f);
                varnish.SetVector("_GlowBLamp", new Vector4((WindowWest + WindowEast) * .5f, WindowHead, 54f, 0f));
                varnish.SetVector("_GlowBWash", new Vector4((WindowWest + WindowEast) * .5f, -10f, 80f, 0f));
                varnish.SetColor("_GlowBColor", new Color(.06f, .22f, 1f) * 3.2f);
                materials.Add(varnish);
                floorVarnish = varnish;
            }
            else
                varnish = Surface("Polished walnut floor", "Walnut Grain", new Color(.44f, .28f, .23f), .74f,
                    new Vector2(.86f, 1.05f), 0f, .35f);

            // Unity lights each mesh with only its few nearest lamps,
            // so the floor is cut into tiles that each find their own.
            var tiles = new Dictionary<int, FloorTile>();
            var random = new System.Random(2419);
            const float width = 7.9f;
            int rows = Mathf.CeilToInt((RoomEast - RoomWest) / width);
            for (int row = 0; row < rows; row++)
            {
                float x = RoomWest + row * width;
                for (float z = RoomSouth; z < RoomNorth - .2f;)
                {
                    float length = Mathf.Min(RoomNorth - z, Range(random, 23f, 42f));
                    float shade = Range(random, .72f, 1.18f);
                    var tint = new Color(shade, shade * Range(random, .94f, 1.04f), shade * Range(random, .90f, 1.05f));
                    var lean = new Vector3(Range(random, -.006f, .006f), 1f, Range(random, -.004f, .004f)).normalized;
                    float u = Range(random, 0f, 1f), v = Range(random, 0f, 1f);
                    // Big tiles: enough for nearby lamps, without hundreds of floor meshes.
                    int key = row / 11 * 100 + Mathf.FloorToInt((z + length * .5f - RoomSouth) / 85f);
                    if (!tiles.TryGetValue(key, out var tile))
                        tiles.Add(key, tile = new FloorTile());
                    int first = tile.vertices.Count;
                    for (int i = 0; i < 4; i++)
                    {
                        float across = i == 1 || i == 2 ? 1f : 0f, along = i >= 2 ? 1f : 0f;
                        tile.vertices.Add(new Vector3(x + across * width, .034f, z + along * length));
                        tile.normals.Add(lean);
                        // The grain runs along the board; each board starts at a different place in the picture.
                        tile.grain.Add(new Vector2(u + across * .21f, v + along * length / 46f));
                        tile.board.Add(new Vector4(across * width, along * length, width, length));
                        tile.tints.Add(tint);
                    }
                    tile.faces.Add(first); tile.faces.Add(first + 3); tile.faces.Add(first + 2);
                    tile.faces.Add(first); tile.faces.Add(first + 2); tile.faces.Add(first + 1);
                    z += length;
                }
            }
            foreach (var tile in tiles.Values)
            {
                var mesh = new Mesh { name = "Staggered hardwood boards" };
                mesh.SetVertices(tile.vertices);
                mesh.SetNormals(tile.normals);
                mesh.SetUVs(0, tile.grain);
                mesh.SetUVs(1, tile.board);
                mesh.SetColors(tile.tints);
                mesh.SetTriangles(tile.faces, 0);
                AddMesh("Staggered hardwood boards", mesh, varnish, ShadowCastingMode.Off);
            }
        }

        sealed class FloorTile
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<Vector3> normals = new List<Vector3>();
            public readonly List<Vector2> grain = new List<Vector2>();
            public readonly List<Vector4> board = new List<Vector4>();
            public readonly List<Color> tints = new List<Color>();
            public readonly List<int> faces = new List<int>();
        }

        void BuildFleece()
        {
            const int around = 384, cross = 14;
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            var center = new Vector3(RugCentreX, .56f, RugCentreZ);

            for (int i = 0; i <= around; i++)
            {
                float u = i / (float)around, angle = u * Mathf.PI * 2f;
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var tangent = Vector3.Cross(Vector3.up, outward);
                for (int j = 0; j <= cross; j++)
                {
                    float v = j / (float)cross, spin = v * Mathf.PI * 2f;
                    var position = center + outward * (RugEdge + .31f * Mathf.Sin(angle * 11f) + (2.77f + .19f * Mathf.Cos(angle * 96f + spin * 3f)) * Mathf.Cos(spin)) +
                        Vector3.up * Mathf.Max(.04f, .40f + Mathf.Sin(spin) * (1.06f + .16f * Mathf.Cos(angle * 19f)));
                    vertices.Add(position);
                    uv.Add(new Vector2(u, v));
                    if (i == around || j == cross)
                        continue;
                    int a = i * (cross + 1) + j, b = a + cross + 1;
                    triangles.Add(a); triangles.Add(a + 1); triangles.Add(b);
                    triangles.Add(b); triangles.Add(a + 1); triangles.Add(b + 1);
                }
            }
            var trim = Fabric("Ivory scanned fleece edging", "Fleece Scan", new Color(1.15f, 1.15f, 1.10f),
                new Vector2(36f, 1.22f), .16f, .73f);
            var mesh = new Mesh { name = "Rolled fleece", vertices = vertices.ToArray(),
                uv = uv.ToArray(), triangles = triangles.ToArray() };
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            AddMesh("Rolled ivory skirt edge", mesh, trim, ShadowCastingMode.TwoSided);
        }

        // The rug the map builds: centre and edge radius in room units.
        const float RugCentreX = -74f, RugCentreZ = -77.7f, RugEdge = 125.4f;

        void BuildBraidedRugEdge()
        {
            var braid = new FirMesh();
            const int samples = 1536;
            for (int strand = 0; strand < 3; strand++)
            {
                Vector3 previous = default;
                for (int i = 0; i <= samples; i++)
                {
                    float angle = i * Mathf.PI * 2f / samples;
                    float twist = angle * 160f + strand * Mathf.PI * 2f / 3f;
                    float radius = RugEdge + 2.45f * Mathf.Cos(twist);
                    var point = new Vector3(RugCentreX + Mathf.Cos(angle) * radius,
                        Mathf.Max(.36f, 1.3f + 1.15f * Mathf.Sin(twist)), RugCentreZ + Mathf.Sin(angle) * radius);
                    if (i > 0) braid.Tube(previous, point, .44f, .44f, 6);
                    previous = point;
                }
            }
            var wool = Fabric("Braided ivory wool", "Fleece Scan", new Color(1.35f, 1.35f, 1.30f),
                new Vector2(1f, 1f), .24f, .65f);
            AddMesh("Interwoven ivory rug border", braid.Build(), wool, ShadowCastingMode.On);
        }

        void AddGiftBow(MeshRenderer renderer)
        {
            var panel = renderer.transform;
            if ((renderer.name != "Visual" && renderer.name != "Curved wrapping") || !panel.parent || panel.parent.name != "Lid")
                return;
            if (panel.parent.parent && panel.parent.parent.name.StartsWith("Rail Gift ", StringComparison.Ordinal))
                return;
            // Check the whole space the bow will fill (it grows with the gift), not just the middle.
            var lidSize = renderer.name == "Curved wrapping"
                ? panel.GetComponent<MeshFilter>().sharedMesh.bounds.size : panel.localScale;
            float bowSize = Mathf.Clamp(Mathf.Min(lidSize.x, lidSize.z) * .30f, .6f, 3.6f) * panel.parent.lossyScale.x;
            var bowSpace = renderer.bounds.center + Vector3.up * (renderer.bounds.extents.y + bowSize * .8f);
            foreach (var cover in Physics.OverlapBox(bowSpace, new Vector3(1.4f, .7f, 1.4f) * bowSize,
                Quaternion.identity, 1 << DeadlyJenga.Core.Layers.Block, QueryTriggerInteraction.Ignore))
                if (cover.attachedRigidbody && cover.attachedRigidbody.transform.parent != panel.parent.parent)
                    return;
            var ribbon = panel.parent.Find("Ribbon lengthwise");
            if (!ribbon || !ribbon.TryGetComponent<MeshRenderer>(out var strip) || !strip.sharedMaterial)
                return;
            if (!giftBow)
                giftBow = Resources.Load<Mesh>(ResourcePath + "Gift Bow");
            if (!giftBow)
            {
                giftBow = RibbonMesh();
                meshes.Add(giftBow);
            }

            var finish = ChooseFinish(strip.name, strip.sharedMaterial) ?? strip.sharedMaterial;
            // Solid satin: deep gold for gold and ivory ribbon, deep red for red.
            bool redBow = finish.color.g < .2f;
            var bowColor = redBow ? new Color(.62f, .01f, .03f) :
                finish.color.g > .6f ? new Color(.92f, .58f, .14f) : new Color(.88f, .46f, .05f);
            // Same foil as the ribbon, drawn on both sides of each loop.
            var bowMaterial = Lacquer("Bow " + finish.name, bowColor, .8f, redBow ? .55f : .7f, .35f, .07f);
            bowMaterial.SetFloat("_Cull", 0f);
            bowMaterial.SetFloat("_Foil", .6f);
            bowMaterial.SetFloat("_TopLight", 0f);
            bowMaterial.SetFloat("_EdgeLight", 0f);
            var obj = new GameObject("Hand-tied satin bow", typeof(MeshFilter), typeof(MeshRenderer));
            obj.GetComponent<MeshFilter>().sharedMesh = giftBow;
            var skin = obj.GetComponent<MeshRenderer>();
            skin.sharedMaterial = bowMaterial;
            skin.shadowCastingMode = ShadowCastingMode.TwoSided;
            var size = renderer.name == "Curved wrapping"
                ? panel.GetComponent<MeshFilter>().sharedMesh.bounds.size : panel.localScale;
            obj.transform.SetParent(panel.parent, false);
            obj.transform.localPosition = new Vector3(0f, .5f * size.y + .08f, 0f);
            // The model is about 2.6 wide; turn each bow a little so no two sit the same way.
            float span = Mathf.Min(size.x, size.z);
            float turn = (Mathf.Abs(panel.position.x * 7.3f + panel.position.z * 3.1f) % 50f) - 25f;
            obj.transform.localRotation = Quaternion.Euler(0f, turn + (size.x < size.z ? 90f : 0f), 0f);
            obj.transform.localScale = Vector3.one * Mathf.Clamp(span * .40f, .8f, 5f);
            mapDressings.Add(obj);
        }

        static Mesh RibbonMesh()
        {
            const int steps = 48, across = 8;
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var faces = new List<int>();
            for (int strip = 0; strip < 4; strip++)
            {
                float sign = strip % 2 == 0 ? -1f : 1f;
                bool loop = strip < 2;
                int first = v.Count;
                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    float arc = Mathf.Sin(t * Mathf.PI);
                    Vector3 center = loop
                        ? new Vector3(sign * 1.52f * arc, .28f * Mathf.Sin(t * Mathf.PI * 2f), -.85f * arc - .28f * Mathf.Sin(t * Mathf.PI * 2f))
                        : new Vector3(sign * (.07f + .95f * t), -1.65f * t, -.07f - .32f * arc);
                    float width = loop ? Mathf.Lerp(.24f, .72f, arc) : Mathf.Lerp(.18f, .37f, t);
                    for (int j = 0; j <= across; j++)
                    {
                        float u = j / (float)across, edge = u * 2f - 1f;
                        var point = center + (loop ? Vector3.up : Vector3.right) * (width * edge);
                        point.z -= .21f * (1f - edge * edge) + .09f * edge * Mathf.Sin(t * Mathf.PI * 2.7f);
                        if (!loop)
                            point.y += Mathf.Max(0f, (t - .8f) / .2f) * (.31f * (1f - Mathf.Abs(edge)));
                        v.Add(point);
                        uv.Add(new Vector2(t, u));
                        if (i == steps || j == across)
                            continue;
                        int a = first + i * (across + 1) + j;
                        int b = a + across + 1;
                        faces.Add(a); faces.Add(a + 1); faces.Add(b);
                        faces.Add(a + 1); faces.Add(b + 1); faces.Add(b);
                    }
                }
            }
            var mesh = new Mesh { name = "Folded satin bow", vertices = v.ToArray(), uv = uv.ToArray(), triangles = faces.ToArray() };
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            return mesh;
        }
    }
}
