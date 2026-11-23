using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyArt
{
    // The little stories that make the room feel lived in on Christmas Eve: a sitting area by
    // the fire, Santa's cookies and milk, a present left half wrapped, a dressed-up south
    // window and a grandfather clock at two minutes to midnight (placed in BuildDecor).
    // Scenery only (no collision).
    public sealed partial class UnderneathTreeFinish
    {
        readonly List<(Transform swing, float amplitude)> swings = new List<(Transform, float)>();

        // Middle of the sitting area, between the sofa (east wall) and the fireplace (west wall).
        static readonly Vector3 SittingCentre = new Vector3(-166f, 0f, -345f);

        void BuildHomeyDetails()
        {
            BuildSittingArea();
            BuildLateWrapping();
            DressSouthWindow();
            BuildHouseRooms();
            BuildDecor();
            BuildRoomSounds();
        }

        // Puts a piece built by the helpers (which place things under the art root) under a pivot.
        static Transform Under(Transform piece, Transform pivot)
        {
            piece.SetParent(pivot, false);
            return piece;
        }

        Transform Pivot(string label, Vector3 position, float turn)
        {
            var pivot = new GameObject(label).transform;
            pivot.SetParent(art, false);
            pivot.localPosition = position;
            pivot.localRotation = Quaternion.Euler(0f, turn, 0f);
            return pivot;
        }

        // Turn so a piece built facing +z looks at a point.
        static float Facing(Vector3 from, Vector3 to) =>
            Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg;

        // ------------------------------------------------------------------ sitting area
        void BuildSittingArea()
        {
            // Low walnut coffee table.
            var walnut = Surface("Coffee table walnut", "Walnut Grain", new Color(.42f, .27f, .17f), .55f, new Vector2(1.2f, 2f), 0f, .3f);
            var table = Pivot("Coffee table", SittingCentre, 0f);
            Under(RoundedDetail("Coffee table top", new Vector3(0f, 27f, 0f), new Vector3(50f, 3.5f, 90f), .06f, walnut), table);
            Under(RoundedDetail("Coffee table shelf", new Vector3(0f, 8f, 0f), new Vector3(42f, 2f, 80f), .05f, walnut), table);
            foreach (int x in new[] { -1, 1 })
            foreach (int z in new[] { -1, 1 })
                Under(RoundedDetail("Coffee table leg", new Vector3(x * 21f, 13f, z * 39f), new Vector3(4f, 26f, 4f), .2f, walnut), table);

            // Two armchairs at the ends of the sitting area, angled toward the fire.
            var fire = new Vector3(RoomWest + 40f, 0f, HearthZ);
            var green = Fabric("Forest velvet armchair", "Velour Scan", new Color(.35f, .75f, .45f), new Vector2(2f, 2f), .25f, .45f);
            var south = new Vector3(-190f, 0f, -488f);
            var north = new Vector3(-190f, 0f, -206f);
            var chairA = Armchair("Armchair by the fire (south)", south, Facing(south, fire), green);
            var chairB = Armchair("Armchair by the fire (north)", north, Facing(north, fire), green);
            // A knitted throw over one arm, and the bedtime story left open on the other seat.
            var knit = Surface("Red knit throw", "Woven", new Color(.75f, .10f, .10f), .2f, new Vector2(4, 4), 0f, .8f);
            Under(RoundedDetail("Knitted throw over the arm", new Vector3(36f, 44f, 4f), new Vector3(14f, 3f, 50f), .3f, knit), chairA);
            Under(RoundedDetail("Knitted throw hanging", new Vector3(43f, 28f, 4f), new Vector3(3f, 30f, 46f), .3f, knit), chairA);
            OpenBook(chairB, new Vector3(4f, 43f, 8f), 20f);

            // Rocking chair beside the hearth's north end (the clock has the south corner).
            var rockerAt = new Vector3(RoomWest + 72f, 0f, HearthZ + 150f);
            RockingChair(rockerAt, Facing(rockerAt, new Vector3(-170f, 0f, -330f)));
            // A soft warm light over the coffee table, as if from the fire and the lamps.
            MakeLight("Sitting area glow", LightType.Point, SittingCentre + Vector3.up * 110f, new Color(1f, .70f, .42f), 950f, 78f);
        }

        // A deep velvet armchair, built facing +z, about the same scale as the big sofa.
        Transform Armchair(string label, Vector3 at, float turn, Material velvet)
        {
            var walnut = Surface("Carved sofa feet", "Walnut Grain", new Color(.40f, .29f, .22f), .40f, new Vector2(1.1f, 1.6f), 0f, .28f);
            var chair = Pivot(label, at, turn);
            Under(RoundedDetail("Armchair base", new Vector3(0f, 18f, 0f), new Vector3(76f, 22f, 70f), .18f, velvet, .03f), chair);
            Under(RoundedDetail("Armchair seat cushion", new Vector3(0f, 34f, 6f), new Vector3(58f, 11f, 56f), .3f, velvet, .06f), chair);
            Under(RoundedDetail("Armchair back", new Vector3(0f, 62f, -28f), new Vector3(70f, 62f, 16f), .3f, velvet, .05f), chair)
                .localRotation = Quaternion.Euler(-8f, 0f, 0f);
            foreach (int side in new[] { -1, 1 })
            {
                Under(RoundedDetail("Armchair rolled arm", new Vector3(side * 34f, 38f, 2f), new Vector3(13f, 22f, 66f), .45f, velvet, .06f), chair);
                foreach (int z in new[] { -1, 1 })
                    Under(RoundedDetail("Armchair foot", new Vector3(side * 30f, 3.5f, z * 28f), new Vector3(7f, 7f, 7f), .4f, walnut), chair);
            }
            return chair;
        }

        void OpenBook(Transform parent, Vector3 at, float turn)
        {
            var cover = Surface("Storybook red cloth", "Woven", new Color(.55f, .05f, .06f), .3f, Vector2.one);
            var pages = Surface("Storybook pages", "Paper", new Color(.95f, .91f, .80f), .2f, Vector2.one);
            var book = new GameObject("The Night Before Christmas, left open").transform;
            book.SetParent(parent, false);
            book.localPosition = at;
            book.localRotation = Quaternion.Euler(0f, turn, 0f);
            foreach (int side in new[] { -1, 1 })
            {
                var half = new GameObject("Book half").transform;
                half.SetParent(book, false);
                half.localRotation = Quaternion.Euler(0f, 0f, side * 8f);
                Under(Box("Book cover", new Vector3(side * 6.5f, 0f, 0f), new Vector3(13f, .6f, 18f), cover), half);
                Under(Box("Book pages", new Vector3(side * 6.2f, .7f, 0f), new Vector3(12f, 1f, 17f), pages), half);
            }
        }

        void RockingChair(Vector3 at, float turn)
        {
            var wood = Surface("Honey oak rocker", "Walnut Grain", new Color(.62f, .40f, .22f), .5f, new Vector2(1f, 3f), 0f, .3f);
            var cushion = Fabric("Plaid rocker cushion", "Cranberry Rug Weave", new Color(1.2f, .8f, .8f), new Vector2(2f, 2f), .1f, .6f);
            var chair = Pivot("Rocking chair", at, turn);
            // Two curved rockers, a seat, spindles and a tall slatted back.
            var rockers = new FirMesh();
            foreach (int side in new[] { -1, 1 })
            {
                Vector3 previous = default;
                for (int i = 0; i <= 12; i++)
                {
                    float t = i / 12f, z = Mathf.Lerp(-46f, 42f, t);
                    var p = new Vector3(side * 24f, 4f + z * z / 260f, z);
                    if (i > 0) rockers.Tube(previous, p, 1.6f, 1.6f, 6);
                    previous = p;
                }
                foreach (float z in new[] { -22f, 20f })
                    rockers.Tube(new Vector3(side * 24f, 5f + z * z / 260f, z), new Vector3(side * 24f, 38f, z * .9f), 1.5f, 1.4f, 6);
                rockers.Tube(new Vector3(side * 24f, 50f, -20f), new Vector3(side * 24f, 50f, 22f), 1.6f, 1.6f, 6);
                rockers.Tube(new Vector3(side * 24f, 38f, 20f), new Vector3(side * 24f, 50f, 22f), 1.3f, 1.3f, 6);
            }
            for (int i = 0; i < 7; i++)
            {
                float x = Mathf.Lerp(-20f, 20f, i / 6f);
                rockers.Tube(new Vector3(x, 40f, -21f), new Vector3(x, 104f, -30f), 1.2f, 1.2f, 6);
            }
            Under(AddMesh("Rocking chair frame", rockers.Build(), wood, ShadowCastingMode.On).transform, chair);
            Under(RoundedDetail("Rocking chair seat", new Vector3(0f, 39f, 0f), new Vector3(50f, 3f, 46f), .1f, wood), chair);
            Under(RoundedDetail("Rocking chair top rail", new Vector3(0f, 106f, -30f), new Vector3(52f, 6f, 4f), .3f, wood), chair);
            Under(RoundedDetail("Rocking chair cushion", new Vector3(0f, 43f, 2f), new Vector3(44f, 5f, 40f), .4f, cushion, .05f), chair);
        }

        // ------------------------------------------------------------------ Santa's treats
        // Built in the fireplace's own frame (against the east wall, room toward -x); BuildRoom
        // turns the whole fireplace round onto the west wall.
        void SantaTreats()
        {
            float top = HearthTop + .3f, x = BreastFront - 22f, z = HearthZ - 28f;
            var china = Surface("White china plate", null, new Color(.96f, .95f, .92f), .85f, Vector2.one);
            var cookie = Surface("Baked cookie", null, new Color(.78f, .52f, .26f), .25f, Vector2.one);
            var chip = Surface("Chocolate chip", null, new Color(.18f, .09f, .04f), .4f, Vector2.one);
            var milk = Surface("Glass of milk", null, new Color(.97f, .97f, .95f), .6f, Vector2.one);
            var carrot = Surface("Carrot for the reindeer", null, new Color(.95f, .45f, .06f), .4f, Vector2.one);
            var leaf = Surface("Carrot top", null, new Color(.15f, .45f, .12f), .3f, Vector2.one);
            var paper = Surface("Note to Santa", "Paper", new Color(.98f, .96f, .90f), .1f, Vector2.one);
            var crayon = Surface("Red crayon", null, new Color(.85f, .05f, .05f), .3f, Vector2.one);

            Primitive("Plate for Santa", PrimitiveType.Cylinder, new Vector3(x, top + .3f, z), new Vector3(13f, .3f, 13f), china);
            var rand = new System.Random(1224);
            for (int i = 0; i < 3; i++)
            {
                var c = new Vector3(x + (i - 1) * 3.4f, top + .9f + i * .5f, z + (i == 1 ? 2.5f : -1.5f));
                // The last cookie has a bite out of it (it is just a smaller one, turned).
                float r = i == 2 ? 3.6f : 4.6f;
                Primitive("Cookie", PrimitiveType.Cylinder, c, new Vector3(r, .45f, r), cookie);
                for (int k = 0; k < 4; k++)
                    Primitive("Chocolate chip", PrimitiveType.Sphere, c + new Vector3(Range(rand, -1.5f, 1.5f), .45f, Range(rand, -1.5f, 1.5f)), Vector3.one * .7f, chip);
            }
            Primitive("Glass of milk", PrimitiveType.Cylinder, new Vector3(x + 1f, top + 4f, z + 12f), new Vector3(4.4f, 4f, 4.4f), milk);
            var cone = ConeMesh();
            meshes.Add(cone);
            var c2 = AddMesh("Carrot for the reindeer", cone, carrot, ShadowCastingMode.On).transform;
            c2.localPosition = new Vector3(x - 3f, top + 1.6f, z - 14f);
            c2.localRotation = Quaternion.Euler(0f, 0f, 90f) * Quaternion.Euler(0f, 30f, 0f);
            c2.localScale = new Vector3(1.6f, 11f, 1.6f);
            Primitive("Carrot top", PrimitiveType.Sphere, new Vector3(x - 3.4f, top + 1.6f, z - 13.4f), new Vector3(2.4f, 1.6f, 2.4f), leaf);
            // A child's note, propped against the plate, with red crayon scribbles.
            var note = Box("Note to Santa", new Vector3(x - 7f, top + 4f, z + 1f), new Vector3(.4f, 8f, 10f), paper);
            note.localRotation = Quaternion.Euler(0f, 0f, -18f);
            for (int i = 0; i < 4; i++)
                Under(Box("Crayon line", new Vector3(-.6f, .3f - i * .16f, Range(rand, -.08f, .08f)), new Vector3(.2f, .05f, Range(rand, .5f, .8f)), crayon), note);
        }

        // Names in red on the white cuffs, so each stocking belongs to someone.
        void StockingNames()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (!font)
                return;
            string[] names = { "DAD", "MOM", "JACK", "EMMA" };
            float[] offsets = { -54f, -18f, 18f, 54f };
            for (int i = 0; i < names.Length; i++)
            {
                var obj = new GameObject("Stocking name " + names[i]);
                obj.transform.SetParent(art, false);
                // Front of the cuff, facing the room (-x as built).
                obj.transform.localPosition = new Vector3(BreastFront - 13f - 3f, MantelTop - 11f, HearthZ + offsets[i]);
                obj.transform.localRotation = Quaternion.LookRotation(Vector3.right);
                var text = obj.AddComponent<TextMesh>();
                text.font = font;
                text.text = names[i];
                text.fontSize = 64;
                text.characterSize = .28f;
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.color = new Color(.75f, .02f, .04f);
                obj.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
        }

        // ------------------------------------------------------------------ late-night wrapping
        void BuildLateWrapping()
        {
            float top = 28.8f;
            var c = SittingCentre;
            var cardboard = Surface("Plain cardboard box", "Paper", new Color(.66f, .50f, .32f), .2f, Vector2.one);
            var wrap = Surface("Hearth gift wrap", "Winter Print", new Color(.70f, .07f, .09f), .45f, new Vector2(1.4f, 1.4f));
            var spool = Lacquer("Gold ribbon spool", new Color(.95f, .62f, .15f), .85f, .7f, .4f, .04f);
            var steel = Surface("Scissor steel", null, new Color(.75f, .76f, .78f), .9f, Vector2.one, .9f);
            var red = Surface("Red plastic", null, new Color(.80f, .06f, .06f), .6f, Vector2.one);
            var mug = Lacquer("Red cocoa mug", new Color(.62f, .04f, .05f), .85f, 0f, .6f, 0f);
            var cocoa = Surface("Hot cocoa", null, new Color(.30f, .16f, .08f), .7f, Vector2.one);
            var marsh = Surface("Marshmallow", null, new Color(.98f, .97f, .94f), .2f, Vector2.one);
            var tape = Surface("Tape dispenser", null, new Color(.10f, .10f, .12f), .5f, Vector2.one);

            // A present half wrapped: the box sits on its paper, which comes up one side.
            var box = new Vector3(c.x - 6f, top, c.z + 14f);
            Box("Half-wrapped present", box + Vector3.up * 8f, new Vector3(20f, 16f, 16f), cardboard).localRotation = Quaternion.Euler(0f, 12f, 0f);
            var sheet = Box("Wrapping paper sheet", box + Vector3.up * .15f, new Vector3(34f, .2f, 30f), wrap);
            sheet.localRotation = Quaternion.Euler(0f, 12f, 0f);
            var flap = Box("Paper folded up the side", box + new Vector3(-10.6f, 8f, 2f), new Vector3(.3f, 16f, 18f), wrap);
            flap.localRotation = Quaternion.Euler(0f, 12f, 0f);
            // The rest of the roll, lying across the table.
            var roll = Primitive("Wrapping paper roll", PrimitiveType.Cylinder, new Vector3(c.x + 14f, top + 3f, c.z + 6f), new Vector3(6f, 21f, 6f), wrap);
            roll.localRotation = Quaternion.Euler(90f, 0f, 0f);
            // Scissors, tape and the ribbon.
            var scissors = new GameObject("Scissors").transform;
            scissors.SetParent(art, false);
            scissors.localPosition = new Vector3(c.x - 8f, top + .3f, c.z - 14f);
            scissors.localRotation = Quaternion.Euler(0f, -30f, 0f);
            foreach (int side in new[] { -1, 1 })
            {
                Under(Box("Scissor blade", new Vector3(side * .8f, 0f, 5f), new Vector3(1f, .3f, 10f), steel), scissors).localRotation = Quaternion.Euler(0f, side * 6f, 0f);
                Under(Primitive("Scissor handle", PrimitiveType.Cylinder, new Vector3(side * 2.2f, 0f, -2.6f), new Vector3(3.4f, .3f, 4f), red), scissors);
            }
            Box("Tape dispenser", new Vector3(c.x + 12f, top + 2f, c.z - 24f), new Vector3(5f, 4f, 9f), tape);
            Primitive("Ribbon spool", PrimitiveType.Cylinder, new Vector3(c.x - 14f, top + 2.6f, c.z - 30f), new Vector3(6f, 1.6f, 6f), spool)
                .localRotation = Quaternion.Euler(0f, 0f, 90f);
            Box("Loose ribbon", new Vector3(c.x - 6f, top + .2f, c.z - 30f), new Vector3(14f, .15f, 1.2f), spool).localRotation = Quaternion.Euler(0f, 20f, 0f);
            // A mug of cocoa going cold.
            var mugAt = new Vector3(c.x + 10f, top, c.z + 32f);
            Primitive("Cocoa mug", PrimitiveType.Cylinder, mugAt + Vector3.up * 4f, new Vector3(6.4f, 4f, 6.4f), mug);
            Primitive("Cocoa", PrimitiveType.Cylinder, mugAt + Vector3.up * 7.7f, new Vector3(5.6f, .2f, 5.6f), cocoa);
            Box("Mug handle", mugAt + new Vector3(4f, 4.5f, 0f), new Vector3(2f, 4.5f, 1.2f), mug);
            for (int i = 0; i < 3; i++)
                Box("Marshmallow", mugAt + new Vector3(i - 1f, 8f, (i % 2) * .8f - .4f), Vector3.one * 1.4f, marsh);
        }

        // ------------------------------------------------------------------ south window
        void DressSouthWindow()
        {
            float mid = (SouthWindowWest + SouthWindowEast) * .5f, width = SouthWindowEast - SouthWindowWest;
            float wall = RoomSouth;            // the room is toward +z
            var velvet = Surface("Cranberry velvet curtain", "Woven", new Color(.48f, .075f, .106f), .29f, new Vector2(7, 9));
            var brass = Surface("Turned lamp brass", null, new Color(.72f, .48f, .19f), .72f, Vector2.one, .65f);
            var cream = Surface("Cream painted mantel", "Paper", new Color(.93f, .89f, .80f), .4f, new Vector2(3, 1));
            var plaid = Fabric("Plaid rocker cushion", "Cranberry Rug Weave", new Color(1.2f, .8f, .8f), new Vector2(2f, 2f), .1f, .6f);
            var pillowA = Fabric("Forest velvet armchair", "Velour Scan", new Color(.35f, .75f, .45f), new Vector2(2f, 2f), .25f, .45f);
            var pillowB = Surface("Cream knit pillow", "Woven", new Color(.92f, .87f, .78f), .2f, new Vector2(3, 3), 0f, .8f);

            // Curtain rod and drapes, floor to above the window.
            float rodY = WindowHead + 16f;
            Box("Curtain rod", new Vector3(mid, rodY, wall + 10f), new Vector3(width + 90f, 1.6f, 1.6f), brass);
            foreach (int side in new[] { -1, 1 })
                Primitive("Curtain rod finial", PrimitiveType.Sphere, new Vector3(mid + side * (width * .5f + 46f), rodY, wall + 10f), Vector3.one * 4f, brass);
            Curtain("South window drape left", SouthWindowWest - 22f, rodY - 1f, wall + 9f, velvet, rodY - 4f, 1.6f);
            Curtain("South window drape right", SouthWindowEast + 22f, rodY - 1f, wall + 9f, velvet, rodY - 4f, 1.6f);

            // A padded window seat with pillows.
            Box("Window seat", new Vector3(mid, 15f, wall + 20f), new Vector3(width - 4f, 30f, 38f), cream);
            Box("Window seat kick", new Vector3(mid, 4f, wall + 39.5f), new Vector3(width - 4f, 8f, 1.2f), cream);
            RoundedDetail("Window seat cushion", new Vector3(mid, 33.5f, wall + 20f), new Vector3(width - 8f, 7f, 36f), .25f, plaid, .04f);
            var pillows = new[] { (mid - width * .43f, pillowA, 10f), (mid - width * .36f, pillowB, -6f), (mid + width * .43f, pillowA, -12f) };
            foreach (var (x, material, tilt) in pillows)
                RoundedDetail("Window seat pillow", new Vector3(x, 42f, wall + 17f), new Vector3(25f, 17f, 9f), .45f, material, .1f)
                    .localRotation = Quaternion.Euler(-14f, 0f, tilt);

            // Lit garland around the frame.
            var path = new List<Vector3>();
            float left = SouthWindowWest - 9f, right = SouthWindowEast + 9f, top = WindowHead + 8f, z = wall + 7f;
            for (float y = WindowSill + 8f; y < top; y += 3.5f) path.Add(new Vector3(left, y, z));
            for (float x = left; x < right; x += 3.5f) path.Add(new Vector3(x, top + Mathf.Sin((x - left) / width * Mathf.PI * 4f) * 2f, z));
            for (float y = top; y > WindowSill + 8f; y -= 3.5f) path.Add(new Vector3(right, y, z));
            GarlandAlong("Window garland", path, new System.Random(1203), Vector3.forward);

            // A wreath hung in the middle of the window, and one electric candle on the sill.
            int first = art.childCount;
            BuildWreath(Vector3.zero, 18f);
            Restage("South window wreath", first, Vector3.zero, new Vector3(mid, (WindowSill + WindowHead) * .5f + 14f, wall + 6f), 90f);
            var wax = Surface("Ivory candle wax", null, new Color(.98f, .93f, .82f), .3f, Vector2.one);
            Primitive("Window candle", PrimitiveType.Cylinder, new Vector3(mid, WindowSill + 9f, wall + 5f), new Vector3(4f, 7f, 4f), wax);
            Primitive("Window candle bulb", PrimitiveType.Sphere, new Vector3(mid, WindowSill + 18f, wall + 5f), new Vector3(2f, 3.2f, 2f),
                Glow("Candle flame", new Color(1f, .62f, .2f), 6f)).GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            MakeLight("Window candle glow", LightType.Point, new Vector3(mid, WindowSill + 22f, wall + 14f), new Color(1f, .66f, .32f), 280f, 28f);
        }

        // ------------------------------------------------------------------ grandfather clock
        // Built facing +z, then turned. The hands read 11:58, two minutes to Christmas.
        void BuildGrandfatherClock(Vector3 at, float turn)
        {
            var walnut = Surface("Clock walnut", "Walnut Grain", new Color(.30f, .17f, .10f), .7f, new Vector2(1f, 3f), 0f, .3f);
            var brass = Surface("Clock brass", null, new Color(.85f, .62f, .25f), .85f, Vector2.one, .9f);
            var dial = Surface("Clock dial", "Paper", new Color(.95f, .91f, .80f), .4f, Vector2.one);
            dial.SetColor("_EmissionColor", new Color(.08f, .06f, .04f));
            dial.EnableKeyword("_EMISSION");
            var ink = Surface("Clock hands", null, new Color(.05f, .04f, .04f), .5f, Vector2.one, .3f);
            var glass = Surface("Clock glass", null, new Color(.05f, .05f, .07f), .95f, Vector2.one);
            var clock = Pivot("Grandfather clock", at, turn);

            Under(RoundedDetail("Clock plinth", new Vector3(0f, 20f, 0f), new Vector3(38f, 40f, 26f), .04f, walnut), clock);
            Under(RoundedDetail("Clock waist", new Vector3(0f, 80f, 0f), new Vector3(30f, 80f, 22f), .04f, walnut), clock);
            Under(Box("Pendulum window", new Vector3(0f, 80f, 11.2f), new Vector3(16f, 56f, .4f), glass), clock);
            Under(RoundedDetail("Clock hood", new Vector3(0f, 142f, 0f), new Vector3(40f, 44f, 28f), .04f, walnut), clock);
            Under(RoundedDetail("Clock crown", new Vector3(0f, 168f, 0f), new Vector3(44f, 8f, 30f), .1f, walnut), clock);
            foreach (int side in new[] { -1, 1 })
                Under(Primitive("Crown finial", PrimitiveType.Sphere, new Vector3(side * 16f, 175f, 8f), Vector3.one * 4f, brass), clock);

            var face = new Vector3(0f, 144f, 14.3f);
            Under(Primitive("Clock dial", PrimitiveType.Cylinder, face, new Vector3(28f, .3f, 28f), dial), clock).localRotation = Quaternion.Euler(90f, 0f, 0f);
            Under(Primitive("Dial brass ring", PrimitiveType.Cylinder, face - Vector3.forward * .2f, new Vector3(31f, .25f, 31f), brass), clock).localRotation = Quaternion.Euler(90f, 0f, 0f);
            for (int h = 0; h < 12; h++)
            {
                float a = h * 30f * Mathf.Deg2Rad;
                Under(Box("Hour mark", face + new Vector3(Mathf.Sin(a) * 11.5f, Mathf.Cos(a) * 11.5f, .4f), new Vector3(1f, h % 3 == 0 ? 3f : 1.8f, .2f), ink), clock)
                    .localRotation = Quaternion.Euler(0f, 0f, -h * 30f);
            }
            // Clockwise as seen from the front (+z) is a negative turn about z.
            Hand(clock, face, -(11f + 58f / 60f) * 30f, 7f, 1.2f, ink);
            Hand(clock, face, -58f * 6f, 11f, .8f, ink);
            Under(Primitive("Hand cap", PrimitiveType.Sphere, face + Vector3.forward * .8f, Vector3.one * 1.6f, brass), clock);

            // Swinging pendulum behind the glass.
            var swing = new GameObject("Pendulum").transform;
            swing.SetParent(clock, false);
            swing.localPosition = new Vector3(0f, 106f, 9f);
            Under(Box("Pendulum rod", new Vector3(0f, -20f, 0f), new Vector3(.8f, 40f, .4f), brass), swing);
            Under(Primitive("Pendulum bob", PrimitiveType.Cylinder, new Vector3(0f, -42f, 0f), new Vector3(9f, .5f, 9f), brass), swing)
                .localRotation = Quaternion.Euler(90f, 0f, 0f);
            swings.Add((swing, 7f));
        }

        void Hand(Transform clock, Vector3 face, float angle, float length, float width, Material ink)
        {
            var hand = new GameObject("Clock hand").transform;
            hand.SetParent(clock, false);
            hand.localPosition = face + Vector3.forward * .5f;
            hand.localRotation = Quaternion.Euler(0f, 0f, angle);
            Under(Box("Hand", new Vector3(0f, length * .5f, 0f), new Vector3(width, length, .2f), ink), hand);
        }
    }
}
