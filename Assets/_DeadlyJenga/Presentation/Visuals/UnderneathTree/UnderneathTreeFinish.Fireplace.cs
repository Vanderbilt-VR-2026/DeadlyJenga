using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyArt
{
    // The family room's fireplace, after Evan's photos: a pale limestone chimney breast with a
    // raised stone hearth, a cream mantel shelf, a lit garland draped down both sides, red
    // stockings, candles, a wreath above, poinsettias on the hearth and a crackling log fire.
    public sealed partial class UnderneathTreeFinish
    {
        Texture2D stoneTexture;
        Light fireLight;
        readonly List<(Transform flame, Vector3 scale, float phase)> flames = new List<(Transform, Vector3, float)>();

        // The fireplace is built against the east wall (inside face 58.5) and then turned round
        // onto the west wall in BuildRoom. BreastFront = x of the stone face as built.
        const float BreastFront = 18f;
        const float BreastHalf = 85f;             // half the breast's width along the wall
        const float HearthTop = 14f;              // raised hearth height = floor of the firebox
        const float OpeningHalf = 36f, OpeningTop = 74f;
        const float MantelTop = 105f;

        void BuildFireplace()
        {
            var stone = Surface("Limestone blocks", null, new Color(1f, .97f, .92f), .12f, new Vector2(1f, 1f), 0f, 0f);
            stone.SetTexture("_BaseMap", StoneBlocks());
            stone.SetTextureScale("_BaseMap", new Vector2(1f, 1f));
            var capStone = Surface("Pale hearth cap", null, new Color(.80f, .74f, .64f), .25f, Vector2.one);
            var soot = Surface("Sooty firebrick", "Brick", new Color(.16f, .09f, .07f), .15f, new Vector2(2, 1.4f), 0f, .8f);
            var iron = Surface("Black iron", null, new Color(.03f, .03f, .035f), .55f, Vector2.one, .6f);
            var cream = Surface("Cream painted mantel", "Paper", new Color(.93f, .89f, .80f), .4f, new Vector2(3, 1));

            float back = 58.5f, depth = back - BreastFront, midX = (BreastFront + back) * .5f;
            float z0 = HearthZ - OpeningHalf, z1 = HearthZ + OpeningHalf;
            // Chimney breast with the firebox opening left in it. The texture is laid by world size
            // (one tile = 64 units) so the blocks stay the same size on every piece.
            StoneBlock("Chimney breast left", new Vector3(midX, RoomTop * .5f, (HearthZ - BreastHalf + z0) * .5f),
                new Vector3(depth, RoomTop, z0 - (HearthZ - BreastHalf)), stone);
            StoneBlock("Chimney breast right", new Vector3(midX, RoomTop * .5f, (z1 + HearthZ + BreastHalf) * .5f),
                new Vector3(depth, RoomTop, HearthZ + BreastHalf - z1), stone);
            StoneBlock("Chimney breast over fire", new Vector3(midX, (OpeningTop + RoomTop) * .5f, HearthZ),
                new Vector3(depth, RoomTop - OpeningTop, z1 - z0), stone);
            // Long raised hearth you could sit on, wider than the breast (bottom-left photo).
            StoneBlock("Raised hearth", new Vector3(BreastFront - 15f, HearthTop * .5f - 1f, HearthZ),
                new Vector3(30f, HearthTop - 2f, 230f), stone);
            Box("Hearth cap", new Vector3(BreastFront - 15.5f, HearthTop - 1f, HearthZ), new Vector3(33f, 2.4f, 234f), capStone);

            // Firebox: sooty brick inside, black iron frame around the opening.
            Box("Firebox back", new Vector3(back - 2f, (HearthTop + OpeningTop) * .5f, HearthZ), new Vector3(2f, OpeningTop - HearthTop, z1 - z0), soot);
            foreach (float z in new[] { z0 + 1f, z1 - 1f })
                Box("Firebox side", new Vector3(midX, (HearthTop + OpeningTop) * .5f, z), new Vector3(depth, OpeningTop - HearthTop, 2f), soot);
            Box("Firebox top", new Vector3(midX, OpeningTop - 1f, HearthZ), new Vector3(depth, 2f, z1 - z0), soot);
            Box("Firebox floor", new Vector3(midX, HearthTop + .3f, HearthZ), new Vector3(depth, .6f, z1 - z0), soot);
            Box("Iron frame top", new Vector3(BreastFront - .6f, OpeningTop + 1.5f, HearthZ), new Vector3(1.6f, 3f, z1 - z0 + 6f), iron);
            foreach (float z in new[] { z0 - 1.5f, z1 + 1.5f })
                Box("Iron frame side", new Vector3(BreastFront - .6f, (HearthTop + OpeningTop + 3f) * .5f, z), new Vector3(1.6f, OpeningTop - HearthTop + 3f, 3f), iron);

            // Cream mantel shelf with a moulding under it and two corbels.
            Box("Mantel shelf", new Vector3(BreastFront - 7f, MantelTop - 3.5f, HearthZ), new Vector3(16f, 7f, 160f), cream);
            Box("Mantel moulding", new Vector3(BreastFront - 3.5f, MantelTop - 9f, HearthZ), new Vector3(9f, 4f, 152f), cream);
            foreach (float z in new[] { HearthZ - 70f, HearthZ + 70f })
                Box("Mantel corbel", new Vector3(BreastFront - 3f, MantelTop - 16f, z), new Vector3(7f, 12f, 6f), cream);

            BuildGarland();
            BuildStockings();
            BuildCandles();
            BuildWreath(new Vector3(BreastFront - 3f, 158f, HearthZ), 22f);
            foreach (float z in new[] { HearthZ - 62f, HearthZ + 62f })
                Poinsettia(new Vector3(BreastFront - 16f, HearthTop, z));
            var wrap = Surface("Hearth gift wrap", "Winter Print", new Color(.70f, .07f, .09f), .45f, new Vector2(1.4f, 1.4f));
            var ribbon = Lacquer("Hearth gift ribbon", new Color(.95f, .55f, .10f), .8f, .7f, .3f, .05f);
            HearthGift(new Vector3(BreastFront - 16f, HearthTop, HearthZ + 92f), new Vector3(14f, 11f, 14f), 12f, wrap, ribbon);
            HearthGift(new Vector3(BreastFront - 18f, HearthTop + 11f, HearthZ + 93f), new Vector3(9f, 7f, 9f), -20f, wrap, ribbon);
            BuildFire(midX);
        }

        void StoneBlock(string label, Vector3 center, Vector3 size, Material material)
        {
            // A cube whose UVs follow world size, so the stone pattern never stretches.
            var mesh = Object.Instantiate(Resources.GetBuiltinResource<Mesh>("Cube.fbx"));
            mesh.name = label;
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var uv = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                var p = Vector3.Scale(vertices[i], size) + center;
                var n = normals[i];
                uv[i] = Mathf.Abs(n.x) > .5f ? new Vector2(p.z, p.y) / 64f :
                    Mathf.Abs(n.z) > .5f ? new Vector2(p.x, p.y) / 64f : new Vector2(p.x, p.z) / 64f;
            }
            mesh.uv = uv;
            mesh.RecalculateTangents();
            var renderer = AddMesh(label, mesh, material, ShadowCastingMode.On);
            renderer.transform.localPosition = center;
            renderer.transform.localScale = size;
        }

        // Random pale limestone blocks in courses, with darker mortar joints.
        Texture2D StoneBlocks()
        {
            if (stoneTexture)
                return stoneTexture;
            const int size = 512;
            var pixels = new Color32[size * size];
            var rand = new System.Random(905);
            var tone = new float[size * size];
            int y = 0;
            while (y < size)
            {
                int course = Mathf.Min(size - y, 44 + rand.Next(70));
                if (size - y - course < 40) course = size - y;
                int x = -rand.Next(80);
                while (x < size)
                {
                    int width = 70 + rand.Next(190);
                    float shade = .74f + (float)rand.NextDouble() * .24f;
                    float warm = (float)rand.NextDouble() * .05f;
                    for (int py = y; py < y + course; py++)
                    for (int px = Mathf.Max(0, x); px < Mathf.Min(size, x + width); px++)
                    {
                        bool joint = py - y < 4 || px - x < 4;
                        float grain = Mathf.PerlinNoise(px * .05f + x, py * .05f + y) * .14f +
                            Mathf.PerlinNoise(px * .3f, py * .3f) * .06f;
                        float v = joint ? .42f : shade - grain;
                        pixels[py * size + px] = new Color(v, v * (.96f - warm), v * (.88f - warm * 2f));
                    }
                    x += width;
                }
                y += course;
            }
            stoneTexture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Limestone blocks", wrapMode = TextureWrapMode.Repeat };
            stoneTexture.SetPixels32(pixels);
            stoneTexture.Apply(true);
            return stoneTexture;
        }

        // Many small pieces of one kind become one mesh, so the garland costs a few draw calls.
        sealed class Batch
        {
            public readonly List<CombineInstance> parts = new List<CombineInstance>();
            public void Add(Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale) =>
                parts.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(position, rotation, scale) });
        }

        void Draw(string label, Batch batch, Material material, ShadowCastingMode shadows)
        {
            if (batch.parts.Count == 0) return;
            var mesh = new Mesh { name = label, indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(batch.parts.ToArray(), true, true);
            AddMesh(label, mesh, material, shadows);
        }

        static Vector3 Jitter(System.Random rand, float amount) =>
            new Vector3(Range(rand, -amount, amount), Range(rand, -amount, amount), Range(rand, -amount, amount));

        void BuildGarland()
        {
            var sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            var rand = new System.Random(1225);
            var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var core = new Batch(); var sprigs = new Batch(); var bulbs = new Batch(); var berries = new Batch(); var gold = new Batch();
            // Path: swags along the front edge of the shelf, then a cascade down each side of the breast.
            var path = new List<Vector3>();
            float x = BreastFront - 15f;
            for (float t = 0f; t <= 1f; t += .01f)
            {
                float z = Mathf.Lerp(HearthZ - 80f, HearthZ + 80f, t);
                float sag = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 3f)) * 7f;
                path.Add(new Vector3(x, MantelTop + 1f - sag, z));
            }
            foreach (int side in new[] { -1, 1 })
                for (float t = 0f; t <= 1f; t += .025f)
                    path.Add(new Vector3(x + t * 9f, MantelTop - t * 78f, HearthZ + side * (80f + Mathf.Sin(t * Mathf.PI) * 6f + t * 4f)));
            int n = 0;
            foreach (var p in path)
            {
                core.Add(sphere, p, Quaternion.identity, Vector3.one * 3.4f);
                for (int k = 0; k < 4; k++)
                {
                    float r = Range(rand, 6f, 9f);
                    sprigs.Add(quad, p + Jitter(rand, 2.5f), Random.rotationUniform, new Vector3(r, r, 1f));
                }
                for (int k = 0; k < 2; k++)
                    bulbs.Add(sphere, p + Jitter(rand, 4.2f), Quaternion.identity, Vector3.one * .75f);
                if (n % 9 == 0) berries.Add(sphere, p + Jitter(rand, 3f) + Vector3.left * 2f, Quaternion.identity, Vector3.one * 2.2f);
                if (n % 13 == 6) gold.Add(sphere, p + Jitter(rand, 3f) + Vector3.left * 2f, Quaternion.identity, Vector3.one * 2.6f);
                n++;
            }
            Draw("Garland core", core, Surface("Garland shadow", null, new Color(.02f, .06f, .03f), .05f, Vector2.one), ShadowCastingMode.Off);
            Draw("Garland fir sprigs", sprigs, FirFoliage("Garland fir needles", new Color(.45f, .72f, .40f)), ShadowCastingMode.On);
            Draw("Garland fairy lights", bulbs, Glow("Warm fairy light", new Color(1f, .80f, .48f), 7f), ShadowCastingMode.Off);
            Draw("Garland red baubles", berries, Lacquer("Garland red glass", new Color(.75f, .02f, .05f), .92f, .4f, .8f, .08f), ShadowCastingMode.Off);
            Draw("Garland gold baubles", gold, Lacquer("Garland gold glass", new Color(.95f, .62f, .15f), .9f, .85f, .6f, .05f), ShadowCastingMode.Off);
            MakeLight("Garland glow", LightType.Point, new Vector3(BreastFront - 25f, MantelTop, HearthZ), new Color(1f, .74f, .40f), 2500f, 70f);
        }

        void BuildStockings()
        {
            var red = Surface("Red knit stocking", "Woven", new Color(.72f, .03f, .05f), .2f, new Vector2(2, 3), 0f, .7f);
            var cuff = Surface("White fleece cuff", "Fleece Scan", new Color(.95f, .93f, .90f), .1f, new Vector2(1, 1), 0f, .8f);
            var hook = Surface("Brass stocking hook", null, new Color(.85f, .6f, .25f), .8f, Vector2.one, .8f);
            foreach (float dz in new[] { -54f, -18f, 18f, 54f })
            {
                float z = HearthZ + dz, x = BreastFront - 13f, top = MantelTop - 7f;
                Box("Stocking hook", new Vector3(x + 2f, top + 1f, z), new Vector3(3f, 3f, 1.5f), hook);
                RoundedDetail("Stocking cuff", new Vector3(x, top - 4f, z), new Vector3(5.5f, 7f, 11f), .3f, cuff);
                RoundedDetail("Stocking leg", new Vector3(x, top - 17f, z), new Vector3(4.2f, 20f, 9f), .3f, red);
                RoundedDetail("Stocking foot", new Vector3(x, top - 27f, z + 3.5f), new Vector3(4.4f, 8.5f, 15f), .45f, red);
            }
        }

        void BuildCandles()
        {
            var wax = Surface("Ivory candle wax", null, new Color(.98f, .93f, .82f), .3f, Vector2.one);
            wax.SetColor("_EmissionColor", new Color(.25f, .14f, .05f));
            wax.EnableKeyword("_EMISSION");
            var flame = Glow("Candle flame", new Color(1f, .62f, .2f), 6f);
            float[] heights = { 12f, 8f, 10f };
            foreach (int side in new[] { -1, 1 })
            {
                for (int i = 0; i < 3; i++)
                {
                    float z = HearthZ + side * (52f + i * 8f), h = heights[i];
                    var c = new Vector3(BreastFront - 8f, MantelTop + h * .5f, z);
                    Primitive("Candle", PrimitiveType.Cylinder, c, new Vector3(4.2f, h * .5f, 4.2f), wax);
                    var f = Primitive("Candle flame", PrimitiveType.Sphere, c + Vector3.up * (h * .5f + 1.6f), new Vector3(1.2f, 2.8f, 1.2f), flame);
                    f.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                    flames.Add((f, f.localScale, Range(new System.Random(i * 7 + side + 9), 0f, 50f)));
                }
                MakeLight("Candle glow", LightType.Point, new Vector3(BreastFront - 10f, MantelTop + 16f, HearthZ + side * 60f),
                    new Color(1f, .62f, .28f), 900f, 45f);
            }
        }

        void BuildWreath(Vector3 center, float radius)
        {
            var sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            var rand = new System.Random(2512);
            var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var core = new Batch(); var greens = new Batch(); var bulbs = new Batch(); var berries = new Batch();
            for (int i = 0; i < 90; i++)
            {
                float a = i / 90f * Mathf.PI * 2f;
                var ring = center + new Vector3(0f, Mathf.Sin(a), Mathf.Cos(a)) * radius;
                if (i % 2 == 0) core.Add(sphere, ring, Quaternion.identity, Vector3.one * 4.5f);
                for (int k = 0; k < 4; k++)
                {
                    float r = Range(rand, 9f, 13f);
                    greens.Add(quad, ring + Jitter(rand, 3f), Random.rotationUniform, new Vector3(r, r, 1f));
                }
                if (i % 2 == 0) bulbs.Add(sphere, ring + Jitter(rand, 5f) + Vector3.left * 2.5f, Quaternion.identity, Vector3.one * .8f);
                if (i % 7 == 0) berries.Add(sphere, ring + Jitter(rand, 3f) + Vector3.left * 3f, Quaternion.identity, Vector3.one * 2.4f);
            }
            Draw("Wreath core", core, Surface("Garland shadow", null, new Color(.02f, .06f, .03f), .05f, Vector2.one), ShadowCastingMode.Off);
            Draw("Wreath fir sprigs", greens, FirFoliage("Garland fir needles", new Color(.45f, .72f, .40f)), ShadowCastingMode.On);
            Draw("Wreath lights", bulbs, Glow("Warm fairy light", new Color(1f, .80f, .48f), 7f), ShadowCastingMode.Off);
            Draw("Wreath berries", berries, Lacquer("Garland red glass", new Color(.75f, .02f, .05f), .92f, .4f, .8f, .08f), ShadowCastingMode.Off);
            var velvet = Surface("Red velvet bow", "Red Velvet", new Color(.8f, .05f, .07f), .3f, Vector2.one);
            var bow = center + new Vector3(-4f, -radius, 0f);
            foreach (int side in new[] { -1, 1 })
            {
                var loop = RoundedDetail("Wreath bow loop", bow + new Vector3(0f, 2f, side * 6f), new Vector3(3f, 8f, 11f), .45f, velvet);
                loop.localRotation = Quaternion.Euler(side * 20f, 0f, 0f);
                var tail = RoundedDetail("Wreath bow tail", bow + new Vector3(0f, -9f, side * 4f), new Vector3(1.5f, 16f, 4f), .3f, velvet);
                tail.localRotation = Quaternion.Euler(side * -15f, 0f, 0f);
            }
            RoundedDetail("Wreath bow knot", bow, new Vector3(4f, 5f, 5f), .45f, velvet);
        }

        void Poinsettia(Vector3 at)
        {
            var pot = Lacquer("Gold foil pot", new Color(.85f, .55f, .15f), .8f, .9f, .4f, 0f);
            var leaf = Surface("Poinsettia red leaves", null, new Color(.95f, .05f, .07f), .35f, Vector2.one);
            leaf.SetColor("_EmissionColor", new Color(.18f, .0f, .01f));
            leaf.EnableKeyword("_EMISSION");
            var green = Surface("Poinsettia green leaves", null, new Color(.06f, .22f, .08f), .3f, Vector2.one);
            Primitive("Poinsettia pot", PrimitiveType.Cylinder, at + Vector3.up * 5f, new Vector3(11f, 5f, 11f), pot);
            var sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            var reds = new Batch(); var greens = new Batch();
            var rand = new System.Random((int)at.z);
            for (int ring = 0; ring < 3; ring++)
            for (int i = 0; i < 8; i++)
            {
                float a = (i / 8f + ring * .06f) * Mathf.PI * 2f, reach = 9f - ring * 2.5f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var rot = Quaternion.LookRotation(dir) * Quaternion.Euler(-12f - ring * 10f, 0f, 0f);
                (ring == 0 ? greens : reds).Add(sphere, at + Vector3.up * (12f + ring * 2.5f) + dir * reach * .6f, rot,
                    new Vector3(3.2f, .9f, reach + Range(rand, -1f, 1f)));
            }
            Draw("Poinsettia bracts", reds, leaf, ShadowCastingMode.On);
            Draw("Poinsettia leaves", greens, green, ShadowCastingMode.On);
            Primitive("Poinsettia heart", PrimitiveType.Sphere, at + Vector3.up * 18.5f, Vector3.one * 2f,
                Glow("Poinsettia yellow heart", new Color(.9f, .75f, .1f), .6f));
        }

        void HearthGift(Vector3 bottom, Vector3 size, float turn, Material wrap, Material ribbon)
        {
            var box = RoundedDetail("Hearth gift", bottom + Vector3.up * size.y * .5f, size, .05f, wrap);
            box.localRotation = Quaternion.Euler(0f, turn, 0f);
            var band = RoundedDetail("Hearth gift ribbon", Vector3.zero, new Vector3(size.x + .3f, size.y + .3f, size.z * .16f), .05f, ribbon);
            band.SetParent(box, false);
            var band2 = RoundedDetail("Hearth gift ribbon", Vector3.zero, new Vector3(size.x * .16f, size.y + .3f, size.z + .3f), .05f, ribbon);
            band2.SetParent(box, false);
        }

        // Logs on an iron grate, a glowing ember bed, layered flames that flicker, and rising sparks.
        void BuildFire(float midX)
        {
            var bark = Surface("Oak log bark", "Pine Scan", new Color(.35f, .22f, .14f), .2f, new Vector2(1, 2), 0f, .9f);
            var charred = Surface("Charred log", "Pine Scan", new Color(.08f, .05f, .04f), .1f, new Vector2(1, 2), 0f, .9f);
            var iron = Surface("Black iron", null, new Color(.03f, .03f, .035f), .55f, Vector2.one, .6f);
            float floor = HearthTop + .6f;
            for (int i = -2; i <= 2; i++)
                Box("Grate bar", new Vector3(midX, floor + 3f, HearthZ + i * 7f), new Vector3(16f, 1.2f, 1.2f), iron);
            Box("Grate rail", new Vector3(midX - 7f, floor + 5f, HearthZ), new Vector3(1.4f, 6f, 34f), iron);
            Box("Ember bed", new Vector3(midX + 2f, floor + .5f, HearthZ), new Vector3(10f, 1f, 40f), Glow("Glowing embers", new Color(1f, .16f, .02f), 2f));
            // Logs lie along the wall, one leaning across them (bottom-left photo).
            var logs = new[] {
                (new Vector3(midX - 3f, floor + 6.5f, HearthZ), new Vector3(90f, 0f, 0f), 5.4f, 40f, bark),
                (new Vector3(midX + 4f, floor + 7.5f, HearthZ - 2f), new Vector3(90f, 0f, 0f), 6f, 44f, charred),
                (new Vector3(midX, floor + 12.5f, HearthZ + 3f), new Vector3(90f, 30f, 25f), 4.8f, 36f, bark),
            };
            foreach (var log in logs)
            {
                var t = Primitive("Fire log", PrimitiveType.Cylinder, log.Item1, new Vector3(log.Item3, log.Item4 * .5f, log.Item3), log.Item5);
                t.localRotation = Quaternion.Euler(log.Item2);
            }
            // Flames are many soft glowing tongues that rise, lick and fade, drawn by a small
            // additive shader, so the fire has depth from every side (and in each VR eye).
            var flameShader = Resources.Load<Shader>(ResourcePath + "Christmas Flame");
            if (flameShader)
            {
                var bed = new Vector3(midX + 1f, floor + 14f, HearthZ);
                // Big outer flames: orange to deep red.
                FlameLayer("Fire outer flames", flameShader, bed, new Vector3(9f, 1f, 34f), 85f, .65f, 1.2f, 19f, 30f, 6f, 12f, 1.3f,
                    new Color(1f, .62f, .18f), new Color(1f, .28f, .04f), new Color(.6f, .05f, .01f));
                // Bright yellow-white core low on the logs.
                FlameLayer("Fire hot core", flameShader, bed + Vector3.down * 2f, new Vector3(6f, 1f, 26f), 70f, .35f, .6f, 8f, 13f, 4f, 8f, 1.8f,
                    new Color(1f, .95f, .70f), new Color(1f, .72f, .26f), new Color(1f, .30f, .05f));
                // A dim orange haze behind, so the fire fills the box with light.
                FlameLayer("Fire glow haze", flameShader, bed + Vector3.right * 4f, new Vector3(4f, 1f, 30f), 10f, 1.0f, 1.6f, 26f, 36f, 3f, 6f, .35f,
                    new Color(1f, .40f, .08f), new Color(.9f, .22f, .03f), new Color(.3f, .03f, .0f));
            }
            BuildSparks(new Vector3(midX, floor + 14f, HearthZ));
        }

        void FlameLayer(string label, Shader shader, Vector3 at, Vector3 area, float rate, float lifeMin, float lifeMax,
            float sizeMin, float sizeMax, float speedMin, float speedMax, float strength, Color young, Color middle, Color old)
        {
            var obj = new GameObject(label);
            obj.transform.SetParent(art, false);
            obj.transform.localPosition = at;
            var fire = obj.AddComponent<ParticleSystem>();
            fire.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = fire.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startRotation = new ParticleSystem.MinMaxCurve(-.15f, .15f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = Mathf.CeilToInt(rate * lifeMax) + 8;
            main.prewarm = true;
            main.loop = true;
            var emission = fire.emission;
            emission.rateOverTime = rate;
            var shape = fire.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            // Upright flame cards follow the system's own up, so the system stays level and only
            // the emitter is aimed up. In the turned shape, y runs along the room's -z.
            shape.rotation = new Vector3(-90f, 0f, 0f);
            shape.scale = new Vector3(area.x, area.z, area.y);
            var noise = fire.noise;
            noise.enabled = true;
            noise.strength = 3.5f;
            noise.frequency = 1.4f;
            noise.scrollSpeed = 1.2f;
            var size = fire.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, .55f), new Keyframe(.25f, 1f), new Keyframe(1f, .25f)));
            var tint = fire.colorOverLifetime;
            tint.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(young, 0f), new GradientColorKey(middle, .45f), new GradientColorKey(old, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .12f), new GradientAlphaKey(.8f, .6f), new GradientAlphaKey(0f, 1f) });
            tint.color = gradient;
            var material = new Material(shader) { name = label };
            material.SetFloat("_Strength", strength);
            materials.Add(material);
            var renderer = obj.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.VerticalBillboard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            fire.Play();
        }

        void BuildSparks(Vector3 at)
        {
            var obj = new GameObject("Fire sparks");
            obj.transform.SetParent(art, false);
            obj.transform.localPosition = at;
            obj.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var sparks = obj.AddComponent<ParticleSystem>();
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = sparks.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.8f, 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 16f);
            main.startSize = new ParticleSystem.MinMaxCurve(.25f, .6f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, .45f, .08f), new Color(1f, .8f, .3f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 60;
            var emission = sparks.emission;
            emission.rateOverTime = 14f;
            var shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(10f, 36f, 1f);
            var noise = sparks.noise;
            noise.enabled = true;
            noise.strength = 6f;
            noise.frequency = .8f;
            var fade = sparks.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(new Color(1f, .7f, .25f), 0f), new GradientColorKey(new Color(1f, .2f, .02f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, .6f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            obj.GetComponent<ParticleSystemRenderer>().sharedMaterial = Glow("Fire spark", new Color(1f, .5f, .1f), 6f);
            sparks.Play();
        }

        void AnimateFire()
        {
            float t = Time.time;
            for (int i = 0; i < flames.Count; i++)
            {
                var (flame, scale, phase) = flames[i];
                if (!flame) continue;
                float lick = Mathf.PerlinNoise(t * 3.1f + phase, phase * .37f);
                float sway = Mathf.PerlinNoise(phase * .51f, t * 2.3f + phase) - .5f;
                flame.localScale = new Vector3(scale.x * (.9f + lick * .2f), scale.y * (.72f + lick * .56f), scale.z * (.92f + lick * .16f));
                var euler = flame.localEulerAngles;
                flame.localEulerAngles = new Vector3(sway * 18f, euler.y, euler.z);
            }
            if (fireLight)
                fireLight.intensity = 14000f * (.78f + Mathf.PerlinNoise(t * 4.3f, 1.7f) * .44f);
        }
    }
}
