using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyArt
{
    // Second round of lived-in detail: a proper upright piano with Silent Night on the stand,
    // a pet bed by the fire, family photos over the sofa, sconces by the fireplace, colored
    // lights strung along the ceiling edge, greenery on the beams and paper stars hanging down.
    public sealed partial class UnderneathTreeFinish
    {
        void BuildDecor()
        {
            // Piano against the west wall's north end, facing the room; the clock beside it.
            BuildPiano(new Vector3(RoomWest + 23f, 0f, 0f), 90f);
            BuildGrandfatherClock(new Vector3(RoomWest + 55f, 0f, RoomNorth - 15f), 180f);
            PetBed(new Vector3(RoomWest + 88f, 0f, HearthZ - 150f));
            FamilyPhotos();
            Sconces();
            CeilingLights();
            BeamGreenery();
            PaperStars();
        }

        // ------------------------------------------------------------------ piano
        // Built facing +z (keys toward +z), then turned.
        void BuildPiano(Vector3 at, float turn)
        {
            var lacquer = Lacquer("Piano black", new Color(.02f, .015f, .015f), .9f, 0f, .8f, 0f);
            var ivory = Surface("Piano ivory keys", null, new Color(.95f, .93f, .86f), .7f, Vector2.one);
            var ebony = Surface("Piano ebony keys", null, new Color(.03f, .03f, .03f), .8f, Vector2.one);
            var paper = Surface("Sheet music", "Paper", new Color(.96f, .93f, .84f), .2f, Vector2.one);
            var ink = Surface("Clock hands", null, new Color(.05f, .04f, .04f), .5f, Vector2.one, .3f);
            var plush = Fabric("Red velvet bench cushion", "Velour Scan", new Color(1.1f, .4f, .42f), new Vector2(2, 2), .25f, .4f);
            var piano = Pivot("Upright piano", at, turn);
            Under(RoundedDetail("Piano case", new Vector3(0f, 52f, -6f), new Vector3(96f, 104f, 30f), .03f, lacquer), piano);
            Under(RoundedDetail("Piano key bed", new Vector3(0f, 50f, 12f), new Vector3(94f, 6f, 16f), .05f, lacquer), piano);
            Under(Box("White keys", new Vector3(0f, 53.6f, 13f), new Vector3(84f, 1.4f, 11f), ivory), piano);
            var black = new Batch();
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            // Black keys in groups of two and three across seven octaves' worth of white keys.
            for (int k = 0; k < 49; k++)
            {
                int note = k % 7;
                if (note == 2 || note == 6) continue;
                float x = -42f + (k + 1) * (84f / 50f);
                black.Add(cube, new Vector3(x, 54.8f, 10.5f), Quaternion.identity, new Vector3(.9f, 1.2f, 6.5f));
            }
            var keys = new GameObject("Black keys").transform;
            keys.SetParent(piano, false);
            var blackMesh = new Mesh { name = "Piano black keys" };
            blackMesh.CombineMeshes(black.parts.ToArray(), true, true);
            Under(AddMesh("Black keys", blackMesh, ebony, ShadowCastingMode.Off).transform, keys);
            Under(RoundedDetail("Piano legs", new Vector3(-44f, 24f, 14f), new Vector3(4f, 48f, 4f), .2f, lacquer), piano);
            Under(RoundedDetail("Piano legs", new Vector3(44f, 24f, 14f), new Vector3(4f, 48f, 4f), .2f, lacquer), piano);
            // Silent Night open on the music stand.
            var stand = Under(Box("Music stand", new Vector3(0f, 74f, 9.5f), new Vector3(40f, 16f, 1f), lacquer), piano);
            stand.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            foreach (int side in new[] { -1, 1 })
            {
                var sheet = Under(Box("Sheet music", new Vector3(side * 8.5f, 76f, 10.5f), new Vector3(16f, 20f, .3f), paper), piano);
                sheet.localRotation = Quaternion.Euler(-12f, side * -4f, 0f);
                for (int line = 0; line < 4; line++)
                    Under(Box("Staff line", new Vector3(0f, .35f - line * .18f, .6f), new Vector3(.85f, .015f, .2f), ink), sheet);
            }
            // Candles and nutcrackers on top.
            var wax = Surface("Ivory candle wax", null, new Color(.98f, .93f, .82f), .3f, Vector2.one);
            var flame = Glow("Candle flame", new Color(1f, .62f, .2f), 6f);
            foreach (int side in new[] { -1, 1 })
            {
                Under(Primitive("Piano candle", PrimitiveType.Cylinder, new Vector3(side * 40f, 110f, -6f), new Vector3(3.6f, 6f, 3.6f), wax), piano);
                var f = Under(Primitive("Piano candle flame", PrimitiveType.Sphere, new Vector3(side * 40f, 117.6f, -6f), new Vector3(1.1f, 2.6f, 1.1f), flame), piano);
                f.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                flames.Add((f, f.localScale, side * 13f));
            }
            for (int i = 0; i < 3; i++)
                Nutcracker(piano, new Vector3(-18f + i * 18f, 104f, -6f), i == 1 ? 1.2f : 1f);
            MakeLight("Piano candle glow", LightType.Point, at + new Vector3(16f, 125f, 0f), new Color(1f, .65f, .3f), 800f, 50f);
            // Bench with a cushion.
            Under(RoundedDetail("Piano bench", new Vector3(0f, 18f, 38f), new Vector3(56f, 36f, 22f), .05f, lacquer), piano);
            Under(RoundedDetail("Bench cushion", new Vector3(0f, 37.5f, 38f), new Vector3(52f, 4f, 20f), .4f, plush), piano);
        }

        void Nutcracker(Transform parent, Vector3 at, float size)
        {
            var red = Lacquer("Nutcracker red", new Color(.70f, .05f, .05f), .8f, 0f, .5f, 0f);
            var skin = Surface("Nutcracker face", null, new Color(.95f, .80f, .65f), .5f, Vector2.one);
            var black = Lacquer("Nutcracker black", new Color(.03f, .03f, .03f), .85f, 0f, .5f, 0f);
            var gold = Surface("Clock brass", null, new Color(.85f, .62f, .25f), .85f, Vector2.one, .9f);
            Under(Primitive("Nutcracker legs", PrimitiveType.Cylinder, at + new Vector3(0f, 3f, 0f) * size, new Vector3(3f, 3f, 2.4f) * size, black), parent);
            Under(Primitive("Nutcracker coat", PrimitiveType.Cylinder, at + new Vector3(0f, 8.5f, 0f) * size, new Vector3(3.4f, 2.6f, 3.4f) * size, red), parent);
            Under(Primitive("Nutcracker belt", PrimitiveType.Cylinder, at + new Vector3(0f, 6.2f, 0f) * size, new Vector3(3.5f, .4f, 3.5f) * size, gold), parent);
            Under(Primitive("Nutcracker head", PrimitiveType.Sphere, at + new Vector3(0f, 12.5f, 0f) * size, new Vector3(2.8f, 3f, 2.8f) * size, skin), parent);
            Under(Primitive("Nutcracker hat", PrimitiveType.Cylinder, at + new Vector3(0f, 16f, 0f) * size, new Vector3(3f, 2.4f, 3f) * size, black), parent);
        }

        // ------------------------------------------------------------------ pet bed
        void PetBed(Vector3 at)
        {
            var plaid = Fabric("Plaid rocker cushion", "Cranberry Rug Weave", new Color(1.2f, .8f, .8f), new Vector2(2f, 2f), .1f, .6f);
            var fleece = Surface("Ivory fleece", "Woven", new Color(.84f, .75f, .63f), .25f, new Vector2(10, 8), 0f, 1f);
            var toy = Surface("Chew toy", null, new Color(.20f, .55f, .85f), .5f, Vector2.one);
            var red = Surface("Red knit stocking", "Woven", new Color(.72f, .03f, .05f), .2f, new Vector2(2, 3), 0f, .7f);
            Primitive("Pet bed rim", PrimitiveType.Cylinder, at + Vector3.up * 5f, new Vector3(40f, 5f, 32f), plaid);
            Primitive("Pet bed cushion", PrimitiveType.Cylinder, at + Vector3.up * 8f, new Vector3(32f, 2.5f, 25f), fleece);
            // A chew bone and a little stocking of its own.
            Box("Chew bone", at + new Vector3(4f, 11f, -2f), new Vector3(9f, 1.6f, 2.4f), toy).localRotation = Quaternion.Euler(0f, 30f, 0f);
            foreach (int end in new[] { -1, 1 })
                Primitive("Chew bone end", PrimitiveType.Sphere, at + new Vector3(4f + end * 3.9f, 11f, -2f + end * 2.2f), Vector3.one * 2.6f, toy);
            RoundedDetail("Pet stocking", at + new Vector3(-6f, 11f, 5f), new Vector3(10f, 2.4f, 4.5f), .4f, red).localRotation = Quaternion.Euler(0f, -20f, 0f);
        }

        // ------------------------------------------------------------------ walls
        void FamilyPhotos()
        {
            var walnut = Surface("Clock walnut", "Walnut Grain", new Color(.30f, .17f, .10f), .7f, new Vector2(1f, 3f), 0f, .3f);
            var gilt = Surface("Clock brass", null, new Color(.85f, .62f, .25f), .85f, Vector2.one, .9f);
            var mat = Surface("Photo mat", "Paper", new Color(.94f, .91f, .84f), .3f, Vector2.one);
            // Faded family snapshots: soft warm colors, no faces.
            var tints = new[] { new Color(.62f, .48f, .36f), new Color(.38f, .45f, .55f), new Color(.70f, .55f, .40f), new Color(.45f, .52f, .40f), new Color(.66f, .40f, .38f) };
            var frames = new[] { (z: -400f, y: 150f, w: 34f, h: 26f), (z: -355f, y: 172f, w: 24f, h: 30f), (z: -320f, y: 142f, w: 30f, h: 22f),
                (z: -282f, y: 168f, w: 26f, h: 32f), (z: -350f, y: 128f, w: 20f, h: 16f) };
            float wall = RoomEast - 1.5f;
            for (int i = 0; i < frames.Length; i++)
            {
                var f = frames[i];
                var frame = i % 2 == 0 ? walnut : gilt;
                Box("Photo frame", new Vector3(wall - 1f, f.y, f.z), new Vector3(1.6f, f.h + 4f, f.w + 4f), frame);
                Box("Photo mat", new Vector3(wall - 1.9f, f.y, f.z), new Vector3(.3f, f.h, f.w), mat);
                Box("Photo", new Vector3(wall - 2.1f, f.y, f.z), new Vector3(.3f, f.h * .7f, f.w * .7f),
                    Surface("Family photo " + i, "Paper", tints[i], .3f, Vector2.one));
            }
        }

        void Sconces()
        {
            var brass = Surface("Turned lamp brass", null, new Color(.72f, .48f, .19f), .72f, Vector2.one, .65f);
            var shade = Glow("Sconce shade", new Color(1f, .78f, .48f), 1.3f);
            foreach (int side in new[] { -1, 1 })
            {
                var plate = new Vector3(RoomWest + 2f, 140f, HearthZ + side * 108f);
                Box("Sconce plate", plate, new Vector3(1f, 10f, 5f), brass);
                Box("Sconce arm", plate + new Vector3(5f, 0f, 0f), new Vector3(10f, 1.2f, 1.2f), brass);
                Primitive("Sconce shade", PrimitiveType.Cylinder, plate + new Vector3(10f, 5f, 0f), new Vector3(7f, 4f, 7f), shade)
                    .GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                MakeLight("Sconce glow", LightType.Point, plate + new Vector3(16f, 6f, 0f), new Color(1f, .70f, .40f), 2500f, 90f);
            }
        }

        // Big old-fashioned colored bulbs strung in scallops around the top of the walls.
        void CeilingLights()
        {
            var sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            var colors = new[] { new Color(1f, .08f, .05f), new Color(1f, .68f, .12f), new Color(.06f, .85f, .18f), new Color(.08f, .32f, 1f) };
            var batches = new Batch[colors.Length];
            for (int i = 0; i < batches.Length; i++) batches[i] = new Batch();
            var wire = new FirMesh();
            float y = RoomTop - 14f, inset = 4f;
            var corners = new[] { new Vector3(RoomWest + inset, y, RoomNorth - inset), new Vector3(RoomEast - inset, y, RoomNorth - inset),
                new Vector3(RoomEast - inset, y, RoomSouth + inset), new Vector3(RoomWest + inset, y, RoomSouth + inset) };
            int n = 0;
            for (int side = 0; side < 4; side++)
            {
                var a = corners[side];
                var b = corners[(side + 1) % 4];
                int steps = Mathf.CeilToInt(Vector3.Distance(a, b) / 9f);
                Vector3 previous = a;
                for (int i = 1; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    // Each scallop dips between hooks every four bulbs.
                    float dip = Mathf.Sin((i % 4) / 4f * Mathf.PI) * 9f;
                    var p = Vector3.Lerp(a, b, t) + Vector3.down * dip;
                    wire.Tube(previous, p, .35f, .35f, 4);
                    batches[n % 4].Add(sphere, p + Vector3.down * 2.4f, Quaternion.identity, new Vector3(2.2f, 3.2f, 2.2f));
                    previous = p;
                    n++;
                }
            }
            AddMesh("Ceiling light wire", wire.Build(), Surface("Green vintage light cable", null, new Color(.02f, .05f, .025f), .3f, Vector2.one), ShadowCastingMode.Off);
            for (int i = 0; i < colors.Length; i++)
                Draw("Ceiling string bulbs " + i, batches[i], Glow("Ceiling bulb " + i, colors[i], 3.5f), ShadowCastingMode.Off);
        }

        void BeamGreenery()
        {
            float midZ = (RoomSouth + RoomNorth) * .5f, span = RoomNorth - RoomSouth;
            int index = 0;
            for (float x = RoomWest + 90f; x < RoomEast; x += 120f)
            {
                var path = new List<Vector3>();
                for (float z = RoomSouth + 20f; z < RoomNorth - 20f; z += 7f)
                    path.Add(new Vector3(x, RoomTop - 20f - Mathf.Abs(Mathf.Sin((z - RoomSouth) / span * Mathf.PI * 5f)) * 8f, z));
                GarlandAlong("Beam garland " + index++, path, new System.Random(800 + index), Vector3.down);
            }
        }

        // Paper stars hanging on threads from the beams, glowing softly.
        void PaperStars()
        {
            var paper = Glow("Paper star", new Color(1f, .90f, .70f), .6f);
            var thread = Surface("Star thread", null, new Color(.85f, .85f, .85f), .3f, Vector2.one);
            var star = StarMesh(9f, 4f, 2.2f);
            meshes.Add(star);
            var rand = new System.Random(1201);
            for (float x = RoomWest + 90f; x < RoomEast; x += 120f)
                for (int i = 0; i < 4; i++)
                {
                    float z = Mathf.Lerp(RoomSouth + 80f, RoomNorth - 60f, (i + Range(rand, .1f, .9f)) / 4f);
                    // Keep clear of the tree in the north-east corner.
                    if (x > -120f && z > -130f) continue;
                    float drop = Range(rand, 30f, 70f);
                    var top = new Vector3(x + Range(rand, -10f, 10f), RoomTop - 18f, z);
                    Box("Star thread", top + Vector3.down * drop * .5f, new Vector3(.2f, drop, .2f), thread);
                    var s = AddMesh("Paper star", star, paper, ShadowCastingMode.Off).transform;
                    s.localPosition = top + Vector3.down * (drop + 9f);
                    s.localRotation = Quaternion.Euler(0f, Range(rand, 0f, 180f), 0f);
                    float size = Range(rand, .8f, 1.4f);
                    s.localScale = Vector3.one * size;
                }
        }

        static Mesh StarMesh(float outer, float inner, float depth)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int face = -1; face <= 1; face += 2)
            for (int i = 0; i < 10; i++)
            {
                float a0 = i * Mathf.PI / 5f, a1 = (i + 1) * Mathf.PI / 5f;
                float r0 = i % 2 == 0 ? outer : inner, r1 = i % 2 == 0 ? inner : outer;
                int start = vertices.Count;
                vertices.Add(new Vector3(0f, 0f, face * depth));
                vertices.Add(new Vector3(Mathf.Sin(a0) * r0, Mathf.Cos(a0) * r0, 0f));
                vertices.Add(new Vector3(Mathf.Sin(a1) * r1, Mathf.Cos(a1) * r1, 0f));
                if (face < 0) { triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2); }
                else { triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1); }
            }
            var mesh = new Mesh { name = "Paper star" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
