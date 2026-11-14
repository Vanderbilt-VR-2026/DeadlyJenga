using System.Collections.Generic;
using UnityEngine;
using F=DeadlyJenga.CandyArt.Marina.MarinaFinish;

namespace DeadlyJenga.CandyArt.Marina
{
    // Showtime Plaza: the open heart of the Marina. Players arrive straight onto it, see the play
    // screen ahead, the wardrobe stage on the left and the Hall of Fame on the right.
    public sealed partial class MayhemMarina
    {
        const float PlazaTop = 1.31f;

        // Open arcs (degrees, 0 = toward the cinema) where the plaza meets other decks.
        static readonly Vector2[] PlazaOpenings = { new(-35, 35), new(88, 112), new(-135, -92) };

        static Vector3 OnPlaza(float degrees, float radius, float height = 0)
        {
            float a = degrees * Mathf.Deg2Rad;
            return PlazaCenter + new Vector3(Mathf.Sin(a) * radius, height, Mathf.Cos(a) * radius);
        }

        static bool PlazaEdgeOpen(float degrees)
        {
            foreach (var open in PlazaOpenings)
                if (degrees >= open.x && degrees <= open.y) return true;
            return false;
        }

        void ShowtimePlaza()
        {
            var floor = new Vector3(PlazaCenter.x, PlazaTop, PlazaCenter.z);
            // Rock footing and a single flat stone deck, flush within the capsule step with its neighbours.
            mesh.Disk(floor + Vector3.down * 2.2f, PlazaRadius + .3f, 2.07f, F.Rock, true, 72);
            mesh.Disk(floor + Vector3.down * .13f, PlazaRadius, .13f, F.Stone, true, 72);

            // Flush inlays: a cream medallion, a brass ring and warm wood rays (art only, no collision).
            mesh.Disk(floor, 1.55f, .006f, F.CandyCream, false, 48);
            mesh.ArcSlab(floor, Vector2.one * 1.55f, Vector2.one * 1.72f, .008f, 0, 360, F.Brass, false, 48);
            mesh.ArcSlab(floor, Vector2.one * (PlazaRadius - .62f), Vector2.one * (PlazaRadius - .48f), .008f, 0, 360, F.Brass, false, 72);
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f;
                var from = OnPlaza(a, 1.9f, PlazaTop - PlazaCenter.y + .004f);
                var to = OnPlaza(a, PlazaRadius - .8f, PlazaTop - PlazaCenter.y + .004f);
                mesh.Box((from + to) * .5f, new Vector3(.07f, .006f, Vector3.Distance(from, to)),
                    i % 2 == 0 ? F.Wood : F.DarkWood, .002f, false, Quaternion.Euler(0, a, 0));
            }

            // Rounded masonry copings only where the plaza meets open water.
            for (int i = 0; i < 64; i++)
            {
                float a = -180 + i * 5.625f;
                if (PlazaEdgeOpen(a)) continue;
                var p = OnPlaza(a, PlazaRadius - .18f, PlazaTop - PlazaCenter.y + .08f);
                mesh.Box(p, new Vector3(.86f, .2f, .42f), F.Ivory, .07f, false, Quaternion.Euler(0, a + 90, 0));
            }
            // Low lanterns ring the plaza edge so it reads as a stage at every time of day.
            foreach (float a in new[] { -160f, -60f, 44f, 80f, 140f, 180f })
                DeckLight(OnPlaza(a, PlazaRadius - .55f, PlazaTop - PlazaCenter.y));
        }

        void StyleStage()
        {
            var turn = StyleStageTurn;
            Vector3 At(float x, float y, float z) => new Vector3(Wardrobe.x, PlazaTop, Wardrobe.z) + turn * new Vector3(x, y, z);

            // Raised runway disk with a glowing rim; the wardrobe places the player on it.
            mesh.Disk(At(0, 0, 0), 1.35f, .2f, F.Stone, true, 48);
            mesh.ArcSlab(At(0, 0, 0), Vector2.one * 1.35f, Vector2.one * 1.47f, .05f, 0, 360, F.Glow, false, 48);
            mesh.ArcSlab(At(0, .2f, 0), Vector2.one * 1.12f, Vector2.one * 1.2f, .008f, 0, 360, F.Brass, false, 48);

            // Free-standing vanity: carved panel, illuminated oval mirror and a crown arch.
            mesh.Box(At(0, 1.95f, 2.3f), new Vector3(3.7f, 3.9f, .3f), F.Wood, .16f, true, turn);
            mesh.Box(At(0, .12f, 2.3f), new Vector3(4.1f, .24f, .62f), F.DarkWood, .08f, true, turn);
            var crown = new List<Vector3>();
            for (int i = 0; i <= 32; i++)
            {
                float a = Mathf.PI - i * Mathf.PI / 32;
                crown.Add(At(Mathf.Cos(a) * 1.85f, 3.9f + Mathf.Sin(a) * .62f, 2.18f));
            }
            mesh.Tube(crown, .2f, F.Ivory, 12, true);
            var ring = new List<Vector3>();
            for (int i = 0; i <= 64; i++)
            {
                float a = i * Mathf.PI / 32;
                ring.Add(At(Mathf.Cos(a) * 1.38f, 2.07f + Mathf.Sin(a) * 1.9f, 2.0f));
            }
            mesh.Tube(ring, .14f, F.Wood, 10);
            mesh.Tube(ring, .065f, F.Glow, 10);
            for (int i = 0; i < 20; i++)
            {
                float a = i * Mathf.PI / 10;
                mesh.Ellipsoid(At(Mathf.Cos(a) * 1.4f, 2.07f + Mathf.Sin(a) * 1.91f, 1.86f), Vector3.one * .06f, F.Glow, rings: 5, sides: 8);
            }

            // Angled clothing rails and hat shelves either side keep the stage open toward the plaza.
            for (int side = -1; side <= 1; side += 2)
            {
                var rack = turn * Quaternion.Euler(0, side * -38, 0);
                Vector3 R(float x, float y, float z) => At(side * 2.35f, 0, 1.15f) + rack * new Vector3(x, y, z);
                mesh.Beam(R(-.75f, 0, 0), R(-.75f, 2.05f, 0), .045f, F.Brass, true);
                mesh.Beam(R(.75f, 0, 0), R(.75f, 2.05f, 0), .045f, F.Brass, true);
                mesh.Beam(R(-.78f, 1.98f, 0), R(.78f, 1.98f, 0), .03f, F.Brass);
                mesh.Box(R(0, .08f, 0), new Vector3(1.7f, .16f, .55f), F.DarkWood, .05f, false, rack);
                for (int j = 0; j < 4; j++)
                {
                    var color = (j + (side > 0 ? 1 : 0)) % 2 == 0 ? F.MintFabric : F.CoralFabric;
                    var p = R((j - 1.5f) * .36f, 1.38f, 0);
                    mesh.Box(p, new Vector3(.3f, .85f, .12f), color, .06f, false, rack);
                    mesh.Box(p + rack * new Vector3(-.18f, .24f, 0), new Vector3(.16f, .5f, .12f), color, .05f, false, rack * Quaternion.Euler(0, 0, -14));
                    mesh.Box(p + rack * new Vector3(.18f, .24f, 0), new Vector3(.16f, .5f, .12f), color, .05f, false, rack * Quaternion.Euler(0, 0, 14));
                }
                for (int j = 0; j < 3; j++)
                {
                    var hat = R((j - 1) * .5f, 2.3f, .05f);
                    mesh.Disk(hat, .22f, .05f, F.Metal, false, 20);
                    mesh.Disk(hat + Vector3.up * .05f, .14f, .26f, (F)((int)F.Red + (j + 1) % 3), false, 20);
                }
                Pot(At(side * 2.05f, 0, 2.55f), .42f, .78f, 30 + side);
            }

            // Frame and feet behind the wardrobe screen, as in the original shop.
            var frame = StyleScreen + StyleScreenFacing * Vector3.forward * .12f;
            mesh.Box(frame, new Vector3(2.48f, 2.91f, .16f), F.DarkWood, .09f, true, StyleScreenFacing);
            for (int side = -1; side <= 1; side += 2)
            {
                var foot = frame + StyleScreenFacing * Vector3.right * (side * .95f);
                foot.y = PlazaTop + .3f;
                mesh.Box(foot, new Vector3(.16f, .6f, .2f), F.Brass, .035f, true, StyleScreenFacing);
            }
            Accent("Style stage", At(0, 3.2f, .4f), new Color(1, .79f, .61f), 3, 6);
        }

        void HallOfFame()
        {
            var turn = FameTurn;
            var baseAt = new Vector3(FameBoard.x, PlazaTop, FameBoard.z);
            Vector3 At(float x, float y, float z) => baseAt + turn * new Vector3(x, y, z);

            // Backdrop for the board: two dark posts, a brass-capped header and a low plinth.
            for (int side = -1; side <= 1; side += 2)
            {
                mesh.Box(At(side * 3.05f, 1.75f, .22f), new Vector3(.34f, 3.5f, .34f), F.DarkWood, .06f, true, turn);
                mesh.Box(At(side * 3.05f, 3.62f, .22f), new Vector3(.5f, .26f, .5f), F.Brass, .05f, false, turn);
            }
            mesh.Box(At(0, 3.98f, .22f), new Vector3(6.6f, .5f, .3f), F.DarkWood, .08f, false, turn);
            mesh.Box(At(0, 4.26f, .22f), new Vector3(6.8f, .07f, .38f), F.Brass, .02f, false, turn);
            mesh.Box(At(0, 1.2f, .32f), new Vector3(6.3f, 2.4f, .16f), F.Teal, .08f, true, turn);
            mesh.Box(At(0, .14f, .2f), new Vector3(6.9f, .28f, .9f), F.Stone, .06f, true, turn);

            // Winners' steps in front of the board. Solid, so players can hop up for a victory pose.
            (float x, float h, F finish)[] steps = { (0, .6f, F.Brass), (-1.15f, .4f, F.Metal), (1.15f, .26f, F.Coral) };
            foreach (var step in steps)
            {
                mesh.Box(At(step.x, step.h * .5f, -1.45f), new Vector3(1.1f, step.h, 1.0f), F.CandyCream, .09f, true, turn);
                mesh.Box(At(step.x, step.h + .015f, -1.45f), new Vector3(1.02f, .03f, .92f), step.finish, .02f, false, turn);
            }
            // A candy trophy on the top step.
            var cup = At(0, .66f, -1.3f);
            mesh.Disk(cup, .16f, .06f, F.DarkWood, false, 20);
            mesh.Beam(cup + Vector3.up * .06f, cup + Vector3.up * .26f, .04f, F.Brass);
            mesh.Ellipsoid(cup + Vector3.up * .4f, new Vector3(.17f, .16f, .17f), F.Brass, rings: 8, sides: 16);
            mesh.Ellipsoid(cup + Vector3.up * .62f, Vector3.one * .07f, F.Glow, rings: 6, sides: 10);
            Accent("Hall of fame", At(0, 3.4f, -1.8f), new Color(1, .86f, .62f), 2.6f, 6.5f);
        }
    }
}
