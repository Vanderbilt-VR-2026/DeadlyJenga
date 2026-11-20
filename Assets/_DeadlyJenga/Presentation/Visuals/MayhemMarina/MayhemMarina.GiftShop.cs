using UnityEngine;
using F=DeadlyJenga.CandyArt.Marina.MarinaFinish;

namespace DeadlyJenga.CandyArt.Marina
{
    // Seaside Souvenirs: a beach-resort gift shop. A pastel clapboard cottage with a striped roof,
    // a cupola, awnings and a porch, full of souvenirs (plush crabs, sun hats, towels, mini block towers).
    //
    // It is NOT in the lobby yet. To add it, call it once in Build(), after Planting():
    //     GiftShop(new Vector3(17, .38f, 4.5f), 90);
    // floor = the ground under the middle of the shop (the east beach top is 0.38 m).
    // yaw   = which way the front door faces, in steps of 90 degrees (0 faces -z, 90 faces -x).
    // Walls, porch posts, the counter and the table are solid. The floor is only 3 cm high,
    // so players walk straight in.
    public sealed partial class MayhemMarina
    {
        const float ShopWidth = 8f, ShopDepth = 6f, ShopWall = 3.1f;

        void GiftShop(Vector3 floor, float yaw)
        {
            yaw = Mathf.Round(yaw / 90f) * 90f;
            var q = Quaternion.Euler(0, yaw, 0);
            float hw = ShopWidth * .5f, hd = ShopDepth * .5f, front = -hd + .12f;
            Vector3 P(float x, float y, float z) => floor + q * new Vector3(x, y, z);
            void B(float x, float y, float z, Vector3 size, F finish, float bevel = .04f, bool solid = false, float tiltX = 0, float turnY = 0)
                => mesh.Box(P(x, y, z), size, finish, bevel, solid, q * Quaternion.Euler(tiltX, turnY, 0));
            void Ball(float x, float y, float z, Vector3 radius, F finish) => mesh.Ellipsoid(P(x, y, z), radius, finish, q, 7, 12);
            F[] candy = { F.CandyCoral, F.CandyMint, F.CandyLilac, F.Red, F.Green, F.Purple, F.Teal, F.CandyCream };

            // Floor: cream tiles with a mint checker, and a plank porch out front.
            B(0, .015f, 0, new Vector3(ShopWidth, .03f, ShopDepth), F.CandyCream, .01f, true);
            for (int i = 0; i < 8; i++)
                for (int j = 0; j < 6; j++)
                    if ((i + j) % 2 == 0) B(-3.5f + i, .032f, -2.5f + j, new Vector3(.96f, .004f, .96f), F.CandyMint, .002f);
            B(0, .015f, -hd - .75f, new Vector3(ShopWidth + .4f, .03f, 1.5f), F.Wood, .01f, true);
            for (float x = -hw + .1f; x < hw + .2f; x += .3f) B(x, .032f, -hd - .75f, new Vector3(.02f, .004f, 1.5f), F.DarkWood, .002f);

            // Mint clapboard walls with cream lap lines.
            B(0, ShopWall * .5f, hd - .12f, new Vector3(ShopWidth, ShopWall, .24f), F.CandyMint, .05f, true);
            foreach (float s in new[] { -1f, 1f })
                B(s * (hw - .12f), ShopWall * .5f, 0, new Vector3(.24f, ShopWall, ShopDepth), F.CandyMint, .05f, true);
            for (float y = .35f; y < ShopWall; y += .32f)
            {
                B(0, y, hd + .005f, new Vector3(ShopWidth, .035f, .03f), F.Ivory, .01f);
                foreach (float s in new[] { -1f, 1f }) B(s * (hw + .005f), y, 0, new Vector3(.03f, .035f, ShopDepth), F.Ivory, .01f);
            }

            // Front wall: a wide door in the middle and a display window on each side.
            B(0, (2.3f + ShopWall) * .5f, front, new Vector3(ShopWidth, ShopWall - 2.3f, .24f), F.CandyMint, .05f, true);
            foreach (float s in new[] { -1f, 1f })
            {
                B(s * 1.2f, 1.15f, front, new Vector3(.4f, 2.3f, .24f), F.CandyMint, .05f, true);
                B(s * 3.8f, 1.15f, front, new Vector3(.4f, 2.3f, .24f), F.CandyMint, .05f, true);
                B(s * 2.5f, .4f, front, new Vector3(2.2f, .8f, .24f), F.CandyMint, .05f, true);
                B(s * 2.5f, 1.55f, front, new Vector3(2.2f, 1.5f, .06f), F.Glass, .01f, true);
                B(s * 2.5f, 1.55f, -hd - .01f, new Vector3(.06f, 1.5f, .05f), F.Ivory, .01f);
                B(s * 2.5f, .83f, -hd - .03f, new Vector3(2.4f, .08f, .16f), F.Ivory, .02f);
                B(s * 2.5f, 2.3f, -hd - .01f, new Vector3(2.3f, .08f, .06f), F.Ivory, .02f);
                B(s * 1.02f, 1.15f, -hd - .01f, new Vector3(.1f, 2.3f, .08f), F.Ivory, .02f);

                // Window display: a wood stand with a mini block tower and a plush crab.
                B(s * 2.5f, .86f, front + .4f, new Vector3(2f, .08f, .5f), F.Wood, .02f);
                MiniTower(P(s * 2.5f - .45f, .9f, front + .4f), q, .55f);
                Crab(P(s * 2.5f + .45f, .99f, front + .4f), q * Quaternion.Euler(0, 180, 0), s > 0 ? F.CandyCoral : F.Red);

                // Striped awning over the window, with a scalloped edge.
                for (int i = 0; i < 8; i++)
                {
                    float x = s * 2.5f - 1.05f + i * .3f;
                    var color = i % 2 == 0 ? F.CandyCoral : F.CandyCream;
                    B(x, 2.55f, -hd - .36f, new Vector3(.3f, .04f, .8f), color, .01f, false, -25);
                    Ball(x, 2.34f, -hd - .73f, new Vector3(.15f, .09f, .04f), color);
                }
            }
            B(0, 2.33f, -hd - .01f, new Vector3(2.14f, .1f, .08f), F.Ivory, .02f);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    B(sx * (hw - .02f), ShopWall * .5f, sz * (hd - .02f), new Vector3(.3f, ShopWall, .3f), F.Ivory, .06f);
            B(0, ShopWall + .08f, 0, new Vector3(ShopWidth + .35f, .16f, ShopDepth + .35f), F.Ivory, .05f, true);

            // Striped barrel roof that reaches over the porch, held up by two porch posts.
            bool across = Mathf.Abs(Mathf.Repeat(yaw, 180)) < 1;
            float roofWide = ShopWidth + .9f, roofDeep = ShopDepth + 1.8f;
            mesh.CurvedRoof(P(0, ShopWall + .16f, -.5f), across ? roofWide : roofDeep, across ? roofDeep : roofWide, 1.3f, F.CandyCoral, striped: true);
            foreach (float s in new[] { -1f, 1f })
            {
                B(s * 3.9f, 1.8f, -hd - 1.3f, new Vector3(.2f, 3.6f, .2f), F.Ivory, .05f, true);
                B(s * 3.9f, .1f, -hd - 1.3f, new Vector3(.32f, .2f, .32f), F.Ivory, .05f);
            }

            // Cupola with a brass fish weather vane.
            B(0, 4.95f, 0, new Vector3(1.1f, .9f, 1.1f), F.Ivory, .06f);
            foreach (float s in new[] { -1f, 1f })
            {
                B(0, 5f, s * .56f, new Vector3(.7f, .5f, .04f), F.Glass, .01f);
                B(s * .56f, 5f, 0, new Vector3(.04f, .5f, .7f), F.Glass, .01f);
            }
            B(0, 5.45f, 0, new Vector3(1.35f, .12f, 1.35f), F.CandyCoral, .04f);
            Ball(0, 5.55f, 0, new Vector3(.6f, .55f, .6f), F.CandyMint);
            mesh.Beam(P(0, 6f, 0), P(0, 6.9f, 0), .03f, F.Brass);
            Ball(0, 6.08f, 0, Vector3.one * .08f, F.Brass);
            Ball(0, 6.62f, 0, new Vector3(.32f, .12f, .04f), F.Brass);
            B(.4f, 6.62f, 0, new Vector3(.18f, .18f, .03f), F.Brass, .01f, false, 0, 0);

            // Shop sign over the porch.
            B(0, 3.75f, -hd - 1.47f, new Vector3(4.4f, .8f, .12f), F.Teal, .06f);
            B(0, 3.75f, -hd - 1.43f, new Vector3(4.6f, .95f, .06f), F.Brass, .03f);
            var cream = new Color(.95f, .88f, .69f);
            Sign("SEASIDE SOUVENIRS", P(0, 3.84f, -hd - 1.54f), .034f, cream, new Vector3(0, yaw, 0));
            Sign("GIFTS  ·  SWEETS  ·  SUN HATS", P(0, 3.53f, -hd - 1.54f), .016f, cream, new Vector3(0, yaw, 0));

            // Porch: lamps and planters by the door.
            foreach (float s in new[] { -1f, 1f })
            {
                DeckLight(P(s * 1.6f, .03f, -hd - 1.2f));
                Pot(P(s * 3.3f, .03f, -hd - .7f), .32f, .55f, s > 0 ? 31 : 32);
            }

            // Outside, left: a rack of beach towels.
            float rackX = -hw - .7f;
            foreach (float z in new[] { -1.2f, 1.2f }) mesh.Beam(P(rackX, 0, z), P(rackX, 2f, z), .04f, F.DarkWood);
            mesh.Beam(P(rackX, 1.95f, -1.25f), P(rackX, 1.95f, 1.25f), .035f, F.Brass);
            F[] towels = { F.CandyCoral, F.CandyMint, F.CandyLilac, F.Teal };
            for (int i = 0; i < 4; i++)
            {
                float z = -.9f + i * .6f;
                B(rackX, 1.38f, z, new Vector3(.05f, 1.1f, .5f), towels[i], .01f);
                B(rackX, 1.15f, z, new Vector3(.07f, .1f, .5f), F.Ivory, .005f);
                B(rackX, 1.6f, z, new Vector3(.07f, .1f, .5f), F.Ivory, .005f);
            }

            // Outside, right: a sunglasses spinner and a crate of beach balls.
            var spin = new Vector3(hw + .8f, 0, -hd - .6f);
            mesh.Disk(P(spin.x, 0, spin.z), .3f, .05f, F.Metal, false, 18);
            mesh.Beam(P(spin.x, 0, spin.z), P(spin.x, 1.85f, spin.z), .03f, F.Metal);
            for (int tier = 0; tier < 3; tier++)
            {
                float y = .75f + tier * .4f;
                mesh.Disk(P(spin.x, y, spin.z), .3f, .03f, F.Ivory, false, 18);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * 45f + tier * 20f, r = a * Mathf.Deg2Rad;
                    var turn = q * Quaternion.Euler(0, -a + 90, 0);
                    var spot = P(spin.x + Mathf.Cos(r) * .3f, y + .1f, spin.z + Mathf.Sin(r) * .3f);
                    mesh.Box(spot, new Vector3(.17f, .06f, .015f), i % 2 == 0 ? F.CandyCoral : F.CandyMint, .01f, false, turn);
                    mesh.Box(spot + Vector3.up * .035f, new Vector3(.19f, .015f, .02f), F.DarkWood, .004f, false, turn);
                }
            }
            B(hw + .8f, .3f, -.6f, new Vector3(.9f, .6f, .7f), F.Wood, .05f, true);
            B(hw + .8f, .3f, -.96f, new Vector3(.8f, .12f, .02f), F.DarkWood, .01f);
            for (int i = 0; i < 5; i++)
            {
                float x = hw + .55f + (i % 3) * .25f, z = -.75f + (i / 3) * .3f;
                Ball(x, .72f + (i / 3) * .08f, z, Vector3.one * .19f, candy[i]);
                Ball(x, .72f + (i / 3) * .08f, z, new Vector3(.195f, .05f, .195f), F.Ivory);
            }

            // Inside: the counter with a till and candy jars.
            B(2.2f, .5f, 1.6f, new Vector3(2.6f, 1f, .7f), F.Wood, .06f, true);
            B(2.2f, 1.03f, 1.6f, new Vector3(2.75f, .06f, .85f), F.CandyCream, .02f);
            B(2.2f, .5f, 1.24f, new Vector3(2.3f, .65f, .02f), F.CandyCoral, .01f);
            B(1.4f, 1.17f, 1.7f, new Vector3(.45f, .22f, .35f), F.Metal, .04f);
            B(1.4f, 1.4f, 1.83f, new Vector3(.36f, .24f, .04f), F.Teal, .02f);
            for (int i = 0; i < 3; i++)
            {
                float x = 2.2f + i * .45f;
                Ball(x, 1.25f, 1.55f, new Vector3(.12f, .12f, .12f), candy[3 + i]);
                Ball(x, 1.25f, 1.55f, new Vector3(.17f, .2f, .17f), F.Glass);
                mesh.Disk(P(x, 1.43f, 1.55f), .12f, .05f, F.Brass, false, 14);
            }

            // Back wall shelves: souvenir boxes and mini block towers.
            foreach (float x in new[] { -3.7f, 0f, 3.7f }) B(x, 1.3f, hd - .42f, new Vector3(.08f, 2.4f, .42f), F.Wood, .02f);
            int item = 0;
            foreach (float y in new[] { .9f, 1.5f, 2.1f })
            {
                B(0, y, hd - .42f, new Vector3(7.4f, .05f, .42f), F.Wood, .015f);
                for (float x = -3.45f; x < 3.5f; x += .34f, item++)
                {
                    if (Mathf.Abs(x) < .15f) continue;
                    if (item % 6 == 2) { MiniTower(P(x, y + .03f, hd - .42f), q, .3f); continue; }
                    float h = .16f + (item % 3) * .05f;
                    B(x, y + .025f + h * .5f, hd - .42f, new Vector3(.22f, h, .2f), candy[item % candy.Length], .02f);
                }
            }

            // Left wall shelves: plush crabs up top, folded towels below.
            for (int level = 0; level < 4; level++)
            {
                float y = .5f + level * .6f;
                B(-hw + .45f, y, .2f, new Vector3(.45f, .05f, 4f), F.Wood, .015f);
                for (int i = 0; i < 8; i++)
                {
                    float z = -1.55f + i * .5f;
                    if (level >= 2) Crab(P(-hw + .45f, y + .115f, z), q * Quaternion.Euler(0, 90, 0), candy[(i + level) % 3 == 0 ? 0 : (i % 2 == 0 ? 3 : 2)]);
                    else
                        for (int fold = 0; fold < 3; fold++)
                            B(-hw + .45f, y + .05f + fold * .06f, z, new Vector3(.36f, .055f, .4f), candy[(i + fold + level) % candy.Length], .015f);
                }
            }
            foreach (float z in new[] { -1.8f, 2.2f }) B(-hw + .45f, 1.25f, z, new Vector3(.45f, 2.5f, .06f), F.Wood, .02f);

            // Middle of the shop: a round table with a big souvenir block tower and plush crabs.
            Table(P(-1.2f, .03f, 0), .9f);
            MiniTower(P(-1.2f, .74f, 0), q, .75f);
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f + 45f, r = a * Mathf.Deg2Rad;
                Crab(P(-1.2f + Mathf.Cos(r) * .55f, .835f, Mathf.Sin(r) * .55f), q * Quaternion.Euler(0, 90 - a, 0), i % 2 == 0 ? F.CandyCoral : F.CandyLilac);
            }

            // Hat stand by the right window.
            var stand = new Vector3(2.6f, 0, -1.5f);
            mesh.Disk(P(stand.x, .03f, stand.z), .3f, .05f, F.DarkWood, false, 18);
            mesh.Beam(P(stand.x, 0, stand.z), P(stand.x, 1.75f, stand.z), .035f, F.DarkWood);
            SunHat(P(stand.x, 1.76f, stand.z), F.CandyCream);
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f * Mathf.Deg2Rad;
                var tip = new Vector3(stand.x + Mathf.Cos(a) * .38f, 1.3f, stand.z + Mathf.Sin(a) * .38f);
                mesh.Beam(P(stand.x, 1.15f, stand.z), P(tip.x, tip.y, tip.z), .02f, F.DarkWood);
                SunHat(P(tip.x, tip.y + .02f, tip.z), i == 0 ? F.Ivory : i == 1 ? F.CandyLilac : F.CandyMint);
            }

            // Warm light inside.
            Lantern(P(-1.5f, 2.6f, 0), .18f);
            Lantern(P(1.5f, 2.6f, 0), .18f);
            Accent("Gift shop", P(0, 2.6f, 0), new Color(1, .8f, .56f), 1.8f, 6.5f);
        }

        // A souvenir tower of tiny candy blocks, three per layer, layers crossed.
        void MiniTower(Vector3 bottom, Quaternion q, float height)
        {
            int layers = Mathf.Max(3, Mathf.RoundToInt(height / .06f));
            float h = height / layers, w = h * 1.1f, l = w * 3.2f;
            F[] colors = { F.Red, F.Green, F.Purple };
            for (int layer = 0; layer < layers; layer++)
            {
                var turn = q * Quaternion.Euler(0, layer % 2 * 90 + (layer % 3 - 1) * 2, 0);
                for (int i = -1; i <= 1; i++)
                    mesh.Box(bottom + Vector3.up * (h * (layer + .5f)) + turn * new Vector3(i * w * 1.04f, 0, 0), new Vector3(w, h * .96f, l), colors[(layer + i + 3) % 3], h * .2f, false, turn);
            }
        }

        // A round plush crab: body, two eye stalks and two claws. q turns its face.
        void Crab(Vector3 center, Quaternion q, F color)
        {
            mesh.Ellipsoid(center, new Vector3(.15f, .1f, .12f), color, q, 7, 12);
            foreach (float s in new[] { -1f, 1f })
            {
                mesh.Ellipsoid(center + q * new Vector3(s * .05f, .13f, -.05f), Vector3.one * .04f, F.Ivory, q, 5, 8);
                mesh.Ellipsoid(center + q * new Vector3(s * .05f, .135f, -.085f), Vector3.one * .018f, F.DarkWood, q, 4, 6);
                mesh.Ellipsoid(center + q * new Vector3(s * .19f, .02f, -.05f), new Vector3(.06f, .05f, .07f), color, q, 5, 8);
            }
        }

        void SunHat(Vector3 p, F color)
        {
            mesh.Disk(p, .26f, .025f, color, false, 20);
            mesh.Ellipsoid(p + Vector3.up * .08f, new Vector3(.14f, .1f, .14f), color, rings: 6, sides: 12);
            mesh.Disk(p + Vector3.up * .025f, .148f, .04f, F.CandyCoral, false, 16);
        }
    }
}
