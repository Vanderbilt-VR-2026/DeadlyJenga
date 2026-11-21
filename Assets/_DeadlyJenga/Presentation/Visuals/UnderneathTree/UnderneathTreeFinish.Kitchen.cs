using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyArt
{
    // The kitchen dressed for Christmas Eve baking: garland and colored lights along the cabinets,
    // a wreath over the hood, string lights round the ceiling, an island covered in baking
    // (stand mixer, gingerbread men, rolling pin, cocoa, a Santa cookie jar), a fridge covered in
    // kids' drawings, an advent calendar, poinsettias, a candy cane jar, cider on the stove and
    // mistletoe in the doorway. Scenery only (no collision).
    public sealed partial class UnderneathTreeFinish
    {
        static readonly Color[] BulbColors =
        {
            new Color(1f, .16f, .10f), new Color(.15f, .85f, .25f), new Color(1f, .70f, .15f), new Color(.20f, .40f, 1f)
        };

        void DeckTheKitchen(float far, float south, float north, float height)
        {
            float midZ = (south + north) * .5f;
            var sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
            var rand = new System.Random(1224);

            var red = Surface("Kitchen cranberry enamel", null, new Color(.70f, .05f, .07f), .75f, Vector2.one);
            var cream = Surface("Kitchen cream enamel", null, new Color(.93f, .89f, .80f), .7f, Vector2.one);
            var white = Surface("White icing", null, new Color(.98f, .97f, .95f), .5f, Vector2.one);
            var cookie = Surface("Baked cookie", null, new Color(.78f, .52f, .26f), .25f, Vector2.one);
            var pine = Surface("Kitchen garland pine", "Pine Scan", new Color(.70f, 1.15f, .70f), .2f, new Vector2(1f, 1f), 0f, .8f);
            var gold = Surface("Kitchen gold foil", null, new Color(.90f, .66f, .25f), .8f, Vector2.one, .85f);
            var cabinet = Surface("Sage kitchen cabinets", "Paper", new Color(.55f, .65f, .52f), .4f, new Vector2(2, 2));
            var butcher = Surface("Butcher block counter", "Walnut Grain", new Color(.70f, .48f, .28f), .5f, new Vector2(3, 1), 0f, .3f);
            var steel = Surface("Oven steel", null, new Color(.20f, .20f, .22f), .7f, Vector2.one, .7f);
            var brass = Surface("Turned lamp brass", null, new Color(.72f, .48f, .19f), .72f, Vector2.one, .65f);
            var bulbs = new Batch[BulbColors.Length];
            for (int i = 0; i < bulbs.Length; i++) bulbs[i] = new Batch();

            // Garland swagged along the bottom of the upper cabinets and across the hood.
            var needles = new Batch();
            var bows = new Batch();
            int bulb = 0;
            for (float z = south + 6f; z <= north - 6f; z += 1.6f)
            {
                float swing = Mathf.Repeat(z - south - 6f, 28f) / 28f;
                bool hood = Mathf.Abs(z - midZ) < 21f;
                var at = new Vector3(far + (hood ? 27f : 22.5f), 96f - 7f * Mathf.Sin(swing * Mathf.PI), z);
                needles.Add(sphere, at + Jitter(rand, .8f), Tumble(rand), Vector3.one * Range(rand, 3.8f, 5f));
                if (Mathf.Repeat(z, 6.4f) < 1.6f)
                    bulbs[bulb++ % bulbs.Length].Add(sphere, at + new Vector3(2.4f, -1.8f, 0f), Quaternion.identity, new Vector3(1.4f, 1.9f, 1.4f));
                if (swing < .06f)
                {
                    bows.Add(cube, at + new Vector3(2.5f, 1f, 0f), Quaternion.Euler(0f, 0f, 45f), new Vector3(1f, 3.4f, 3.4f));
                    bows.Add(cube, at + new Vector3(2.5f, -3f, -1.2f), Quaternion.Euler(20f, 0f, 0f), new Vector3(.6f, 5f, 1.4f));
                    bows.Add(cube, at + new Vector3(2.5f, -3f, 1.2f), Quaternion.Euler(-20f, 0f, 0f), new Vector3(.6f, 5f, 1.4f));
                }
            }

            // A wreath on the wall above the hood.
            var wreath = new Vector3(far + 4f, 150f, midZ);
            for (int i = 0; i < 40; i++)
            {
                float a = i / 40f * Mathf.PI * 2f;
                var p = wreath + new Vector3(0f, Mathf.Sin(a), Mathf.Cos(a)) * 13f;
                needles.Add(sphere, p + Jitter(rand, 1f), Tumble(rand), Vector3.one * Range(rand, 6f, 7.5f));
                if (i % 4 == 0)
                    bulbs[i / 4 % bulbs.Length].Add(sphere, p + new Vector3(3.5f, 0f, 0f), Quaternion.identity, Vector3.one * 1.8f);
            }
            bows.Add(cube, wreath + new Vector3(4.5f, -12f, 0f), Quaternion.Euler(45f, 0f, 0f), new Vector3(2f, 6f, 6f));
            bows.Add(cube, wreath + new Vector3(4.5f, -19f, -2.5f), Quaternion.Euler(15f, 0f, 0f), new Vector3(1f, 11f, 2.6f));
            bows.Add(cube, wreath + new Vector3(4.5f, -19f, 2.5f), Quaternion.Euler(-15f, 0f, 0f), new Vector3(1f, 11f, 2.6f));

            // String lights round the top of the walls.
            float near = RoomWest - 3f;
            void Run(Vector3 from, Vector3 to)
            {
                int count = Mathf.CeilToInt((to - from).magnitude / 9f);
                for (int i = 0; i <= count; i++)
                {
                    var p = Vector3.Lerp(from, to, i / (float)count) + Vector3.down * (2.5f * Mathf.Sin(i * .9f) + 3f);
                    bulbs[bulb++ % bulbs.Length].Add(sphere, p, Quaternion.identity, new Vector3(1.6f, 2.2f, 1.6f));
                }
            }
            float top = height - 4f;
            Run(new Vector3(far + 3f, top, south + 3f), new Vector3(far + 3f, top, north - 3f));
            Run(new Vector3(far + 3f, top, south + 3f), new Vector3(near, top, south + 3f));
            Run(new Vector3(far + 3f, top, north - 3f), new Vector3(near, top, north - 3f));

            // Kitchen island in the middle, covered in baking.
            float ix = far + 100f, islandTop = 53f;
            Box("Kitchen island", new Vector3(ix, 25f, midZ), new Vector3(34f, 50f, 76f), cabinet);
            Box("Island butcher block", new Vector3(ix, 51.5f, midZ), new Vector3(40f, 3f, 86f), butcher);
            foreach (float z in new[] { midZ - 26f, midZ, midZ + 26f })
            {
                Primitive("Bar stool seat", PrimitiveType.Cylinder, new Vector3(ix + 30f, 37f, z), new Vector3(14f, 1.5f, 14f), red);
                Primitive("Bar stool post", PrimitiveType.Cylinder, new Vector3(ix + 30f, 18f, z), new Vector3(2.4f, 18f, 2.4f), brass);
                Primitive("Bar stool foot", PrimitiveType.Cylinder, new Vector3(ix + 30f, .6f, z), new Vector3(11f, .6f, 11f), brass);
            }
            StandMixer(new Vector3(ix - 8f, islandTop, midZ - 28f), red, Surface("Mixing bowl steel", null, new Color(.82f, .83f, .86f), .8f, Vector2.one, .3f));
            // Cutting board of gingerbread men, a rolling pin and a dusting of flour.
            var board = new Vector3(ix + 4f, islandTop, midZ + 2f);
            Primitive("Flour dusting", PrimitiveType.Cylinder, board + new Vector3(-6f, .05f, -4f), new Vector3(22f, .05f, 16f), white);
            Box("Cutting board", board + Vector3.up * .6f, new Vector3(18f, 1.2f, 26f), butcher);
            var men = new Batch();
            var buttons = new Batch();
            for (int i = 0; i < 6; i++)
            {
                var m = board + new Vector3((i % 2 - .5f) * 8f, 1.5f, (i / 2 - 1f) * 8f);
                var turn = Quaternion.Euler(0f, Range(rand, -25f, 25f), 0f);
                men.Add(cylinder, m + turn * new Vector3(0f, 0f, 2.9f), turn, new Vector3(3f, .3f, 3f));
                men.Add(cube, m, turn, new Vector3(2.8f, .6f, 3.4f));
                men.Add(cube, m + turn * new Vector3(0f, 0f, .9f), turn, new Vector3(6.6f, .6f, 1.3f));
                foreach (int side in new[] { -1, 1 })
                    men.Add(cube, m + turn * new Vector3(side * .9f, 0f, -2.4f), turn * Quaternion.Euler(0f, side * 18f, 0f), new Vector3(1.3f, .6f, 3.2f));
                for (int b = 0; b < 2; b++)
                    buttons.Add(sphere, m + turn * new Vector3(0f, .35f, .6f - b * 1.2f), Quaternion.identity, new Vector3(.6f, .3f, .6f));
            }
            Draw("Gingerbread men", men, cookie, ShadowCastingMode.On);
            Draw("Icing buttons", buttons, white, ShadowCastingMode.Off);
            var pin = Primitive("Rolling pin", PrimitiveType.Cylinder, board + new Vector3(-12f, 2f, -6f), new Vector3(2.8f, 8f, 2.8f), butcher);
            pin.localRotation = Quaternion.Euler(90f, 20f, 0f);
            // Cocoa with marshmallows, a bowl of oranges, a Santa cookie jar and a poinsettia.
            var mug = new Vector3(ix + 13f, islandTop, midZ - 14f);
            Primitive("Cocoa mug", PrimitiveType.Cylinder, mug + Vector3.up * 2.6f, new Vector3(4.4f, 2.6f, 4.4f), red);
            Primitive("Cocoa", PrimitiveType.Cylinder, mug + Vector3.up * 5.1f, new Vector3(3.8f, .1f, 3.8f),
                Surface("Hot cocoa", null, new Color(.30f, .16f, .08f), .6f, Vector2.one));
            for (int i = 0; i < 4; i++)
                Box("Marshmallow", mug + new Vector3(Range(rand, -1f, 1f), 5.4f, Range(rand, -1f, 1f)), Vector3.one * 1.1f, white)
                    .localRotation = Tumble(rand);
            var bowl = new Vector3(ix - 8f, islandTop, midZ + 28f);
            Primitive("Fruit bowl", PrimitiveType.Sphere, bowl + Vector3.up * 2.5f, new Vector3(15f, 6f, 15f), cream);
            var orange = Surface("Clementine", null, new Color(.95f, .45f, .06f), .45f, Vector2.one);
            for (int i = 0; i < 7; i++)
                Primitive("Clementine", PrimitiveType.Sphere, bowl + new Vector3(Range(rand, -3.5f, 3.5f), 6f + (i > 4 ? 2.5f : 0f), Range(rand, -3.5f, 3.5f)), Vector3.one * 4.2f, orange);
            SantaJar(new Vector3(ix - 13f, islandTop, midZ + 12f), red, white);
            Poinsettia(new Vector3(ix + 12f, islandTop, midZ + 24f), gold);

            // Back counter: cider on the stove, a candy cane jar, a poinsettia and a candy-stripe towel.
            float counter = 55f, front = far + 31.5f;
            var pot = new Vector3(far + 16f, counter, midZ);
            Primitive("Cider pot", PrimitiveType.Cylinder, pot + Vector3.up * 5f, new Vector3(16f, 5f, 16f), red);
            Primitive("Cider pot lid", PrimitiveType.Sphere, pot + Vector3.up * 10f, new Vector3(16f, 3f, 16f), red);
            Primitive("Lid knob", PrimitiveType.Sphere, pot + Vector3.up * 11.8f, Vector3.one * 2.2f, brass);
            var jar = new Vector3(far + 18f, counter, midZ - 66f);
            Primitive("Candy cane jar", PrimitiveType.Cylinder, jar + Vector3.up * 6f, new Vector3(8f, 6f, 8f),
                Surface("Kitchen jar glass", null, new Color(.80f, .88f, .90f), .95f, Vector2.one));
            for (int i = 0; i < 7; i++)
            {
                float a = i / 7f * Mathf.PI * 2f;
                var stick = jar + new Vector3(Mathf.Cos(a) * 1.8f, 13f, Mathf.Sin(a) * 1.8f);
                var tint = i % 2 == 0 ? red : white;
                Primitive("Candy cane", PrimitiveType.Cylinder, stick, new Vector3(1f, 6f, 1f), tint).localRotation = Quaternion.Euler(Mathf.Sin(a) * 8f, 0f, Mathf.Cos(a) * -8f);
                Primitive("Candy cane hook", PrimitiveType.Capsule, stick + new Vector3(Mathf.Cos(a) * 2f, 6.4f, Mathf.Sin(a) * 2f), new Vector3(1f, 2f, 1f), tint)
                    .localRotation = Quaternion.LookRotation(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a))) * Quaternion.Euler(90f, 0f, 0f);
            }
            Poinsettia(new Vector3(far + 16f, counter, north - 14f), gold);
            var towel = Surface("Candy stripe towel", "Woven", new Color(.95f, .93f, .90f), .2f, new Vector2(2, 2), 0f, .6f);
            var towelAt = new Vector3(front + 3.2f, 32f, midZ + 7f);
            Box("Kitchen towel", towelAt, new Vector3(.6f, 16f, 9f), towel);
            for (int i = 0; i < 3; i++)
                Box("Towel stripe", towelAt + new Vector3(.35f, -5f + i * 5f, 0f), new Vector3(.2f, 1.6f, 9.1f), red);

            // A fridge on the south wall, covered in drawings, a Christmas card and a letter to Santa.
            var fridge = new Vector3(far + 140f, 0f, south + 16.5f);
            Box("Fridge", fridge + Vector3.up * 43f, new Vector3(34f, 86f, 28f), cream);
            Box("Fridge door line", fridge + new Vector3(0f, 58f, 14.1f), new Vector3(34f, .5f, .3f), steel);
            foreach (float y in new[] { 70f, 34f })
                Box("Fridge handle", fridge + new Vector3(13f, y, 15f), new Vector3(1.4f, 14f, 1.4f), brass);
            var paperTints = new[] { new Color(.95f, .92f, .85f), new Color(.80f, .90f, 1f), new Color(1f, .88f, .88f), new Color(.88f, 1f, .88f), new Color(1f, .95f, .75f) };
            for (int i = 0; i < 7; i++)
            {
                var drawing = Surface("Kid drawing " + (i % paperTints.Length), "Paper", paperTints[i % paperTints.Length], .3f, Vector2.one);
                var p = fridge + new Vector3(-12f + (i % 4) * 7.5f, 74f - (i / 4) * 14f - (i % 2) * 2f, 14.3f);
                Box("Fridge drawing", p, new Vector3(6.5f, 8.5f, .2f), drawing).localRotation = Quaternion.Euler(0f, 0f, Range(rand, -8f, 8f));
                // A crayon tree, a sun or a snowman scribble on each.
                Primitive("Crayon scribble", PrimitiveType.Sphere, p + new Vector3(0f, -.5f, .15f), new Vector3(3f, 3.6f, .1f), i % 3 == 0 ? red : i % 3 == 1 ? pine : gold);
                Primitive("Magnet", PrimitiveType.Cylinder, p + new Vector3(0f, 3.6f, .2f), new Vector3(1.4f, .2f, 1.4f), BulbGlow(i)).localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            Box("Christmas card", fridge + new Vector3(0f, 47f, 14.3f), new Vector3(9f, 12f, .2f), red);
            Primitive("Card tree", PrimitiveType.Capsule, fridge + new Vector3(0f, 47f, 14.5f), new Vector3(5f, 4f, .1f), pine);

            // An advent calendar on the north wall, the last few doors still shut.
            var advent = new Vector3(far + 120f, 112f, north - 2f);
            Box("Advent calendar", advent, new Vector3(40f, 44f, 1f), red);
            for (int i = 0; i < 24; i++)
            {
                var door = advent + new Vector3(-15f + (i % 6) * 6f, 15f - (i / 6) * 10f, -.7f);
                bool open = i < 23;
                var look = open ? cream : (i % 2 == 0 ? gold : pine);
                Box("Advent door", door + (open ? new Vector3(2.2f, 0f, -1.5f) : Vector3.zero), new Vector3(4.4f, 7f, .4f), look)
                    .localRotation = open ? Quaternion.Euler(0f, 70f, 0f) : Quaternion.identity;
            }

            // Mistletoe hanging just inside the archway.
            var sprig = new Vector3(RoomWest - 16f, ArchHeight - 4f, midZ);
            Box("Mistletoe ribbon", sprig + Vector3.up * ((height - ArchHeight + 4f) * .5f), new Vector3(.8f, height - ArchHeight + 4f, .8f), red);
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                Primitive("Mistletoe leaf", PrimitiveType.Sphere, sprig + new Vector3(Mathf.Cos(a) * 3f, -3f - (i % 2) * 2f, Mathf.Sin(a) * 3f), new Vector3(2f, 5f, 2f), pine)
                    .localRotation = Quaternion.Euler(Mathf.Sin(a) * 40f, 0f, Mathf.Cos(a) * -40f);
                if (i % 2 == 0)
                    Primitive("Mistletoe berry", PrimitiveType.Sphere, sprig + new Vector3(Mathf.Cos(a) * 1.2f, -4f, Mathf.Sin(a) * 1.2f), Vector3.one * 1.3f, white);
            }

            Draw("Kitchen garland", needles, pine, ShadowCastingMode.Off);
            Draw("Garland bows", bows, red, ShadowCastingMode.Off);
            for (int i = 0; i < bulbs.Length; i++)
                Draw("Kitchen string lights", bulbs[i], BulbGlow(i), ShadowCastingMode.Off);
            // A little colored glow from the lights onto the counters.
            MakeLight("Kitchen string light glow", LightType.Point, new Vector3(far + 40f, 92f, midZ), new Color(1f, .55f, .35f), 900f, 110f);
        }

        Material BulbGlow(int i) => Glow("Kitchen bulb " + (i % BulbColors.Length), BulbColors[i % BulbColors.Length], 1.8f);

        static Quaternion Tumble(System.Random rand) =>
            Quaternion.Euler(Range(rand, 0f, 360f), Range(rand, 0f, 360f), Range(rand, 0f, 360f));

        void StandMixer(Vector3 at, Material paint, Material steel)
        {
            RoundedDetail("Mixer base", at + Vector3.up * 1.5f, new Vector3(10f, 3f, 16f), .3f, paint);
            RoundedDetail("Mixer column", at + new Vector3(0f, 9f, -6f), new Vector3(6f, 14f, 5f), .4f, paint);
            RoundedDetail("Mixer head", at + new Vector3(0f, 18f, -1f), new Vector3(7.5f, 7f, 17f), .45f, paint);
            Primitive("Mixer bowl", PrimitiveType.Sphere, at + new Vector3(0f, 7f, 2f), new Vector3(10f, 9f, 10f), steel);
            var dough = Surface("Cookie dough", null, new Color(.90f, .78f, .58f), .2f, Vector2.one);
            Primitive("Cookie dough", PrimitiveType.Sphere, at + new Vector3(0f, 11f, 2f), new Vector3(7f, 3f, 7f), dough);
        }

        void SantaJar(Vector3 at, Material red, Material white)
        {
            var black = Surface("Santa belt", null, new Color(.04f, .03f, .03f), .7f, Vector2.one);
            Primitive("Santa cookie jar", PrimitiveType.Sphere, at + Vector3.up * 3.6f, new Vector3(6.6f, 7.2f, 6.6f), red);
            Primitive("Santa jar belt", PrimitiveType.Cylinder, at + Vector3.up * 3f, new Vector3(6.8f, .55f, 6.8f), black);
            Primitive("Santa jar fur trim", PrimitiveType.Cylinder, at + Vector3.up * 6.6f, new Vector3(5.1f, .6f, 5.1f), white);
            Primitive("Santa jar lid", PrimitiveType.Sphere, at + Vector3.up * 7.8f, new Vector3(4.2f, 3f, 4.2f), red);
            Primitive("Santa jar pompom", PrimitiveType.Sphere, at + Vector3.up * 9.6f, Vector3.one * 1.5f, white);
        }

        void Poinsettia(Vector3 at, Material foil)
        {
            var leaf = Surface("Poinsettia red", "Woven", new Color(.80f, .04f, .06f), .3f, Vector2.one, 0f, .4f);
            var green = Surface("Poinsettia leaf", "Woven", new Color(.10f, .30f, .12f), .3f, Vector2.one, 0f, .4f);
            var yellow = Glow("Poinsettia centre", new Color(1f, .85f, .2f), .4f);
            Primitive("Poinsettia pot", PrimitiveType.Cylinder, at + Vector3.up * 4f, new Vector3(9f, 4f, 9f), foil);
            for (int ring = 0; ring < 2; ring++)
                for (int i = 0; i < 7; i++)
                {
                    float a = (i + ring * .5f) / 7f * 360f;
                    var turn = Quaternion.Euler(0f, a, 0f);
                    var petal = Primitive(ring == 0 ? "Poinsettia leaf" : "Poinsettia bract", PrimitiveType.Sphere,
                        at + Vector3.up * (9f + ring * 1.5f) + turn * new Vector3(0f, 0f, ring == 0 ? 5.5f : 4f),
                        ring == 0 ? new Vector3(3.2f, .6f, 7f) : new Vector3(3f, .6f, 6.5f), ring == 0 ? green : leaf);
                    petal.localRotation = turn * Quaternion.Euler(-12f - ring * 8f, 0f, 0f);
                }
            Primitive("Poinsettia centre", PrimitiveType.Sphere, at + Vector3.up * 11.2f, new Vector3(2.4f, 1.2f, 2.4f), yellow);
        }
    }
}
