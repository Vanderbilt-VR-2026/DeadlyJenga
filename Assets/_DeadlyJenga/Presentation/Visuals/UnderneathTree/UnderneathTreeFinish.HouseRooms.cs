using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyArt
{
    // The two rooms seen through openings in the west wall: a warm kitchen through the archway
    // (gingerbread house, a tray of cooling cookies, the oven glowing) and the front entry
    // (front door with a wreath, coats on hooks, snowy boots, a sled, and the bottom of the
    // stairs going up to where the kids are asleep). Glimpses only; players never go there.
    public sealed partial class UnderneathTreeFinish
    {
        const float SideRoomDepth = 190f;      // how far the kitchen and entry reach past the west wall

        void BuildHouseRooms()
        {
            BuildKitchen();
            BuildFrontEntry();
        }

        void SideRoomShell(string label, float south, float north, float height, Material walls, Material floor)
        {
            float far = RoomWest - SideRoomDepth, midX = RoomWest - SideRoomDepth * .5f, midZ = (south + north) * .5f;
            Box(label + " floor", new Vector3(midX, -.2f, midZ), new Vector3(SideRoomDepth, .4f, north - south), floor);
            Box(label + " back wall", new Vector3(far, height * .5f, midZ), new Vector3(3f, height, north - south), walls);
            foreach (float z in new[] { south, north })
                Box(label + " side wall", new Vector3(midX, height * .5f, z), new Vector3(SideRoomDepth, height, 3f), walls);
        }

        // ------------------------------------------------------------------ kitchen
        void BuildKitchen()
        {
            float south = ArchSouth - 30f, north = ArchNorth + 30f, height = ArchHeight + 20f, far = RoomWest - SideRoomDepth;
            var paint = Surface("Butter yellow kitchen paint", "Wallpaper", new Color(.95f, .84f, .58f), .25f, new Vector2(4, 3), 0f, .2f);
            var tile = Surface("Kitchen floor tile", "Paper", new Color(.86f, .80f, .70f), .45f, new Vector2(12, 12), 0f, .3f);
            var cabinet = Surface("Sage kitchen cabinets", "Paper", new Color(.55f, .65f, .52f), .4f, new Vector2(2, 2));
            var butcher = Surface("Butcher block counter", "Walnut Grain", new Color(.70f, .48f, .28f), .5f, new Vector2(3, 1), 0f, .3f);
            var steel = Surface("Oven steel", null, new Color(.20f, .20f, .22f), .7f, Vector2.one, .7f);
            var brass = Surface("Turned lamp brass", null, new Color(.72f, .48f, .19f), .72f, Vector2.one, .65f);
            SideRoomShell("Kitchen", south, north, height, paint, tile);
            Box("Kitchen ceiling", new Vector3(RoomWest - SideRoomDepth * .5f, height + 1.5f, (south + north) * .5f), new Vector3(SideRoomDepth + 4f, 3f, north - south + 4f), paint);

            // Base cabinets and counter along the back wall, with the oven in the middle.
            float depth = 30f, front = far + 1.5f + depth, midZ = (south + north) * .5f;
            Box("Base cabinets", new Vector3(far + 1.5f + depth * .5f, 26f, midZ), new Vector3(depth, 52f, north - south - 8f), cabinet);
            for (float z = south + 12f; z < north - 12f; z += 30f)
                if (Mathf.Abs(z + 15f - midZ) > 22f)
                    Box("Cabinet door", new Vector3(front + .4f, 26f, z + 15f), new Vector3(1f, 44f, 26f), cabinet);
            Box("Counter top", new Vector3(far + 1.5f + depth * .5f + 1f, 53.5f, midZ), new Vector3(depth + 4f, 3f, north - south - 6f), butcher);
            Box("Oven front", new Vector3(front + .6f, 26f, midZ), new Vector3(1.4f, 46f, 40f), steel);
            Box("Oven window", new Vector3(front + 1.4f, 24f, midZ), new Vector3(.4f, 20f, 28f), Glow("Oven glow", new Color(1f, .45f, .10f), 2.2f));
            Box("Oven handle", new Vector3(front + 2.5f, 40f, midZ), new Vector3(1.2f, 1.2f, 30f), brass);
            MakeLight("Oven light", LightType.Point, new Vector3(front + 20f, 22f, midZ), new Color(1f, .5f, .15f), 1500f, 70f);
            // Upper cabinets with a hood over the oven.
            foreach (int side in new[] { -1, 1 })
                Box("Upper cabinets", new Vector3(far + 1.5f + 10f, 122f, midZ + side * 48f), new Vector3(20f, 50f, 56f), cabinet);
            Box("Range hood", new Vector3(far + 1.5f + 12f, 112f, midZ), new Vector3(24f, 22f, 40f), steel);

            // Gingerbread house and a tray of cookies on the counter.
            GingerbreadHouse(new Vector3(far + 18f, 55f, midZ - 48f));
            CookieTray(new Vector3(far + 20f, 55f, midZ + 48f));
            // A warm pendant light over the middle of the kitchen.
            var shade = Surface("Warm linen lampshade", "Woven", new Color(1f, .82f, .56f), .18f, new Vector2(12f, 3f), 0f, .32f);
            Box("Pendant cord", new Vector3(RoomWest - 90f, height - 18f, midZ), new Vector3(.4f, 36f, .4f), steel);
            Primitive("Pendant shade", PrimitiveType.Cylinder, new Vector3(RoomWest - 90f, height - 40f, midZ), new Vector3(16f, 5f, 16f), shade);
            DeckTheKitchen(far, south, north, height);
        }

        void GingerbreadHouse(Vector3 at)
        {
            var ginger = Surface("Gingerbread", "Walnut Grain", new Color(.70f, .40f, .18f), .3f, new Vector2(1, 1), 0f, .4f);
            var icing = Surface("White icing", null, new Color(.98f, .97f, .95f), .5f, Vector2.one);
            var sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            Box("Gingerbread walls", at + Vector3.up * 7f, new Vector3(14f, 14f, 18f), ginger);
            foreach (int side in new[] { -1, 1 })
            {
                Box("Gingerbread roof", at + new Vector3(side * 4.2f, 17.5f, 0f), new Vector3(10f, 1.4f, 21f), icing)
                    .localRotation = Quaternion.Euler(0f, 0f, side * -38f);
            }
            Box("Gingerbread door", at + new Vector3(7.1f, 4f, 0f), new Vector3(.3f, 7f, 4f), icing);
            var candy = new Batch();
            var rand = new System.Random(2512);
            for (int i = 0; i < 14; i++)
                candy.Add(sphere, at + new Vector3(7.3f, Range(rand, 2f, 13f), Range(rand, -8f, 8f)), Quaternion.identity, Vector3.one * 1.3f);
            Draw("Gumdrops", candy, Glow("Gumdrop candy", new Color(.9f, .15f, .2f), .5f), ShadowCastingMode.Off);
        }

        void CookieTray(Vector3 at)
        {
            var tray = Surface("Baking sheet", null, new Color(.70f, .70f, .72f), .8f, Vector2.one, .8f);
            var cookie = Surface("Baked cookie", null, new Color(.78f, .52f, .26f), .25f, Vector2.one);
            var icing = Surface("White icing", null, new Color(.98f, .97f, .95f), .5f, Vector2.one);
            Box("Baking sheet", at + Vector3.up * .5f, new Vector3(22f, 1f, 32f), tray);
            for (int x = 0; x < 3; x++)
            for (int z = 0; z < 4; z++)
            {
                var c = at + new Vector3((x - 1) * 6.5f, 1.4f, (z - 1.5f) * 7f);
                Primitive("Cooling cookie", PrimitiveType.Cylinder, c, new Vector3(5f, .5f, 5f), cookie);
                if ((x + z) % 2 == 0)
                    Primitive("Iced cookie top", PrimitiveType.Cylinder, c + Vector3.up * .5f, new Vector3(3.6f, .1f, 3.6f), icing);
            }
        }

        // ------------------------------------------------------------------ front entry
        void BuildFrontEntry()
        {
            float south = RoomSouth, north = FoyerNorth + 15f, height = 180f, far = RoomWest - SideRoomDepth;
            var walls = WallCover();
            var wood = Surface("Warm oak wainscot", "Mahogany", new Color(.31f, .186f, .123f), .49f, new Vector2(10, 3f), 0f, .4f);
            var casing = Surface("Cream window casing", "Paper", new Color(.79f, .68f, .54f), .48f, new Vector2(2, 2));
            var dark = Surface("Dark upstairs", null, new Color(.02f, .02f, .03f), .1f, Vector2.one);
            SideRoomShell("Front entry", south, north, height, walls, wood);
            // Ceiling over the door half; the stair half opens into a dark stairwell going up.
            float stairSouth = north - 32f;
            Box("Front entry ceiling", new Vector3(RoomWest - SideRoomDepth * .5f, height + 1.5f, (south + stairSouth) * .5f),
                new Vector3(SideRoomDepth + 4f, 3f, stairSouth - south + 2f), walls);
            Box("Stairwell", new Vector3(RoomWest - SideRoomDepth * .5f, height + 60f, (stairSouth + north) * .5f), new Vector3(SideRoomDepth, 3f, north - stairSouth), dark);
            foreach (float z in new[] { stairSouth, north })
                Box("Stairwell wall", new Vector3(RoomWest - SideRoomDepth * .5f, height + 30f, z), new Vector3(SideRoomDepth, 60f, 3f), dark);
            Box("Stairwell end", new Vector3(far, height + 30f, (stairSouth + north) * .5f), new Vector3(3f, 60f, north - stairSouth), dark);

            // Cased opening trim on the room side.
            foreach (float z in new[] { FoyerSouth - 3f, FoyerNorth + 3f })
                Box("Entry casing", new Vector3(RoomWest + 1.5f, FoyerHeight * .5f, z), new Vector3(4f, FoyerHeight, 6f), casing);
            Box("Entry casing head", new Vector3(RoomWest + 1.5f, FoyerHeight + 3f, (FoyerSouth + FoyerNorth) * .5f), new Vector3(4f, 6f, FoyerNorth - FoyerSouth + 12f), casing);

            // Stairs rising along the north side, from near the opening toward the back wall.
            const int steps = 16;
            float bottom = RoomWest - 45f, top = far + 6f;
            for (int i = 0; i < steps; i++)
            {
                float run = (bottom - top) / steps, rise = height * (i + 1) / steps;
                Box("Stair step", new Vector3(bottom - run * (i + .5f), rise * .5f, (stairSouth + north) * .5f), new Vector3(run, rise, north - stairSouth - 2f), wood);
            }
            var rail = new FirMesh();
            float railZ = stairSouth + 1.5f;
            rail.Tube(new Vector3(bottom, 32f, railZ), new Vector3(top, height + 32f, railZ), .9f, .9f, 6);
            for (int i = 0; i < steps; i += 2)
            {
                float x = bottom - (bottom - top) * (i + .5f) / steps, y = height * (i + 1) / steps;
                rail.Tube(new Vector3(x, y, railZ), new Vector3(x, y + 32f, railZ), .45f, .45f, 5);
            }
            AddMesh("Stair rail", rail.Build(), casing, ShadowCastingMode.On);
            Box("Newel post", new Vector3(bottom + 2f, 22f, railZ), new Vector3(4f, 44f, 4f), casing);
            // A little nightlight glowing at the top of the stairs.
            Primitive("Nightlight", PrimitiveType.Sphere, new Vector3(top + 4f, height + 14f, north - 4f), Vector3.one * 2.4f,
                Glow("Nightlight glow", new Color(1f, .75f, .40f), 3f));
            MakeLight("Nightlight on the landing", LightType.Point, new Vector3(top + 10f, height + 18f, north - 12f), new Color(1f, .7f, .4f), 490f, 52f);

            // Front door on the back wall with a wreath, a mat, boots and a sled.
            float doorZ = (south + stairSouth) * .5f;
            var door = Surface("Front door green", "Paper", new Color(.10f, .26f, .16f), .55f, Vector2.one);
            var brass = Surface("Turned lamp brass", null, new Color(.72f, .48f, .19f), .72f, Vector2.one, .65f);
            Box("Front door", new Vector3(far + 2.5f, 62f, doorZ), new Vector3(2f, 124f, 52f), door);
            foreach (int row in new[] { 0, 1 })
            foreach (int col in new[] { -1, 1 })
                Box("Door panel", new Vector3(far + 3.8f, 34f + row * 52f, doorZ + col * 12f), new Vector3(.6f, 40f, 18f), door);
            Box("Door frame", new Vector3(far + 3f, 127f, doorZ), new Vector3(3f, 6f, 60f), casing);
            foreach (int side in new[] { -1, 1 })
                Box("Door frame", new Vector3(far + 3f, 63f, doorZ + side * 29f), new Vector3(3f, 126f, 5f), casing);
            Primitive("Door knob", PrimitiveType.Sphere, new Vector3(far + 5f, 60f, doorZ + 19f), Vector3.one * 3f, brass);
            int first = art.childCount;
            BuildWreath(Vector3.zero, 13f);
            Restage("Front door wreath", first, Vector3.zero, new Vector3(far + 6f, 92f, doorZ), 180f);
            Box("Coir door mat", new Vector3(far + 22f, .5f, doorZ), new Vector3(28f, 1f, 44f), Surface("Coir mat", "Woven", new Color(.55f, .40f, .22f), .1f, new Vector2(4, 4), 0f, .8f));
            Boots(new Vector3(far + 26f, 1f, doorZ - 10f), new Color(.65f, .08f, .08f), 1f);
            Boots(new Vector3(far + 28f, 1f, doorZ + 8f), new Color(.10f, .22f, .45f), .7f);
            Boots(new Vector3(far + 40f, 1f, doorZ + 2f), new Color(.20f, .14f, .10f), 1.25f);
            Coats(new Vector3(RoomWest - 60f, 112f, south + 2f));
            Sled(new Vector3(RoomWest - 25f, 0f, south + 24f));
            MakeLight("Front entry light", LightType.Point, new Vector3(RoomWest - 90f, height - 20f, doorZ), new Color(1f, .72f, .45f), 1250f, 99f);
        }

        // A pair of snowy boots on the mat. size 1 = grown-up.
        void Boots(Vector3 at, Color color, float size)
        {
            var rubber = Surface("Boot rubber " + color.r.ToString("0.00"), null, color, .6f, Vector2.one);
            var snow = Surface("Melting snow", null, new Color(.95f, .97f, 1f), .7f, Vector2.one);
            var puddle = Surface("Melt puddle", null, new Color(.10f, .10f, .12f), .95f, Vector2.one);
            Primitive("Melt puddle", PrimitiveType.Cylinder, at + Vector3.up * .1f, new Vector3(18f, .05f, 16f) * size, puddle);
            foreach (int side in new[] { -1, 1 })
            {
                var boot = at + new Vector3(0f, 0f, side * 3.6f * size);
                RoundedDetail("Boot leg", boot + new Vector3(-1f, 8f, 0f) * size, new Vector3(5f, 16f, 5f) * size, .35f, rubber);
                RoundedDetail("Boot foot", boot + new Vector3(2f, 2f, 0f) * size, new Vector3(10f, 4f, 5f) * size, .4f, rubber);
                Primitive("Snow on the boot", PrimitiveType.Sphere, boot + new Vector3(4f, 4f, 0f) * size, new Vector3(3f, 1.2f, 3f) * size, snow);
            }
        }

        void Coats(Vector3 at)
        {
            var wood = Surface("Coat rail oak", "Walnut Grain", new Color(.45f, .30f, .18f), .5f, new Vector2(3, 1));
            var brass = Surface("Turned lamp brass", null, new Color(.72f, .48f, .19f), .72f, Vector2.one, .65f);
            Box("Coat rail", at, new Vector3(110f, 8f, 2f), wood);
            var colors = new[] { new Color(.55f, .06f, .06f), new Color(.12f, .16f, .32f), new Color(.18f, .30f, .18f), new Color(.80f, .70f, .55f) };
            for (int i = 0; i < 4; i++)
            {
                var hook = at + new Vector3(-42f + i * 28f, 0f, 3f);
                Primitive("Coat hook", PrimitiveType.Sphere, hook, Vector3.one * 2f, brass);
                var cloth = Fabric("Wool coat " + i, "Woven", colors[i] * 1.3f, new Vector2(3, 4), .1f, .7f);
                float length = i == 3 ? 34f : 62f;      // the last one is a child's coat
                RoundedDetail("Hanging coat", hook + new Vector3(0f, -length * .5f, 5f), new Vector3(22f, length, 9f), .35f, cloth, .08f);
            }
            RoundedDetail("Knitted scarf", at + new Vector3(-28f, -20f, 9f), new Vector3(4f, 40f, 3f), .4f,
                Surface("Red knit throw", "Woven", new Color(.75f, .10f, .10f), .2f, new Vector2(4, 4), 0f, .8f));
        }

        void Sled(Vector3 at)
        {
            var slat = Surface("Sled slats", "Walnut Grain", new Color(.70f, .48f, .28f), .5f, new Vector2(1, 3));
            var runner = Surface("Red sled runners", null, new Color(.75f, .06f, .05f), .7f, Vector2.one, .5f);
            var sled = Pivot("Sled leaning on the wall", at, 0f);
            sled.localRotation = Quaternion.Euler(-14f, 0f, 0f);
            for (int i = 0; i < 4; i++)
                Under(Box("Sled slat", new Vector3(-12f + i * 8f, 42f, 3f), new Vector3(6f, 80f, 1.4f), slat), sled);
            foreach (int side in new[] { -1, 1 })
                Under(Box("Sled runner", new Vector3(side * 15f, 42f, 0f), new Vector3(1.6f, 86f, 4f), runner), sled);
        }
    }
}
