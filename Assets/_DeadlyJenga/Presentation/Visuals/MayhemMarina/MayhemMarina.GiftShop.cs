using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using F=DeadlyJenga.CandyArt.Marina.MarinaFinish;

namespace DeadlyJenga.CandyArt.Marina
{
    // Seaside Souvenirs: a flagship resort gift shop. Outside: a pastel clapboard storefront with a
    // striped barrel roof over a deep porch, double doors between two big display windows, and a cupola.
    // Inside: a wood floor with a star medallion, cream walls over wood panelling, chandeliers, a tiered
    // centrepiece with a big souvenir block tower, a turning hero suit vault, lit merchandise bays, display
    // tables with snow globes, a glass case and a checkout in front of a big window.
    //
    // GiftShopWing (Architecture) places it off the cinema's east side, level with the cinema floor.
    // floor = the ground under the middle of the shop.
    // yaw   = which way the front doors face, in steps of 90 degrees (0 faces -z, 90 faces -x).
    // Footprint: 12 m wide, 9 m deep, plus a 1.8 m porch. Walls, posts, counters, tables and the
    // centrepiece are solid. The floor is only 3 cm high, so players walk straight in.
    public sealed partial class MayhemMarina
    {
        const float ShopWidth = 12f, ShopDepth = 9f, ShopWall = 4.2f, ShopPorch = 1.8f;
        // The hero suit vault: a rounded-rectangle loop of pods. Its flat front shows just inside the shop;
        // the rest runs back through a rounded-rectangle housing outside the left wall.
        // VaultZ = its middle along the wall; VaultAlong = the housing's inside half-width along the wall;
        // VaultCentre = how far outside the wall the loop's centre is; VaultOpening = half the wall
        // opening; VaultTop = the opening's height.
        const float VaultZ = 1.0f, VaultAlong = 2.45f, VaultCentre = 2.38f, VaultOpening = 2.3f, VaultTop = 2.6f;

        static AvatarAppearance Look(AvatarHair hair, AvatarEyewear eyes, AvatarClothing clothes, AvatarAccessory extras) =>
            new AvatarAppearance { hair = hair, eyewear = eyes, clothing = clothes, accessories = extras };

        // Outfits on the hanging display bodies, in rail order.
        static readonly AvatarAppearance[] rackLooks =
        {
            Look(AvatarHair.Buzz, AvatarEyewear.None, AvatarClothing.Hoodie, AvatarAccessory.None),
            Look(AvatarHair.SidePart, AvatarEyewear.Square, AvatarClothing.BomberJacket, AvatarAccessory.None),
            Look(AvatarHair.Bun, AvatarEyewear.None, AvatarClothing.Overalls, AvatarAccessory.RobotAntenna),
            Look(AvatarHair.Spikes, AvatarEyewear.VRVisor, AvatarClothing.PufferVest, AvatarAccessory.None),
            Look(AvatarHair.Bob, AvatarEyewear.None, AvatarClothing.SailorShirt, AvatarAccessory.PropellerCap),
            Look(AvatarHair.None, AvatarEyewear.Visor, AvatarClothing.Hoodie, AvatarAccessory.RobotAntenna),
        };
        static readonly int[] rackColors = { 0, 3, 1, 6, 2, 5 };

        // Heads on the hat shelf, each showing off one head cosmetic.
        static readonly AvatarAppearance[] hatLooks =
        {
            Look(AvatarHair.SidePart, AvatarEyewear.Monocle, AvatarClothing.None, AvatarAccessory.TopHat),
            Look(AvatarHair.LongWaves, AvatarEyewear.None, AvatarClothing.None, AvatarAccessory.Crown),
            Look(AvatarHair.Buzz, AvatarEyewear.Aviator, AvatarClothing.None, AvatarAccessory.CowboyHat),
            Look(AvatarHair.Braids, AvatarEyewear.None, AvatarClothing.None, AvatarAccessory.PirateHat),
            Look(AvatarHair.Curls, AvatarEyewear.None, AvatarClothing.None, AvatarAccessory.VikingHelmet),
            Look(AvatarHair.TwinTails, AvatarEyewear.Heart, AvatarClothing.None, AvatarAccessory.CatEars),
        };
        static readonly int[] hatColors = { 4, 1, 6, 0, 3, 5 };

        // The skins standing in the hero suit vault, in pod order (one pod each).
        static readonly AvatarAppearance[] suitLooks =
        {
            new AvatarAppearance { skin = AvatarSkin.CyberSamurai },
            new AvatarAppearance { skin = AvatarSkin.NeonRobot },
            new AvatarAppearance { skin = AvatarSkin.CrystalKnight },
            new AvatarAppearance { skin = AvatarSkin.StormWizard },
            new AvatarAppearance { skin = AvatarSkin.Astronaut },
            new AvatarAppearance { skin = AvatarSkin.SpacePirate },
            new AvatarAppearance { skin = AvatarSkin.CandyKnight },
            new AvatarAppearance { skin = AvatarSkin.DeepSeaDiver },
            new AvatarAppearance { skin = AvatarSkin.DiscoAlien },
            new AvatarAppearance { skin = AvatarSkin.MushroomSprite },
            new AvatarAppearance { skin = AvatarSkin.Dragon },
            new AvatarAppearance { skin = AvatarSkin.TRex },
            new AvatarAppearance { skin = AvatarSkin.Gargoyle },
            new AvatarAppearance { skin = AvatarSkin.Werewolf },
        };
        static readonly int[] suitColors = { 2, 6, 4, 5, 0, 3, 1, 7, 4, 2, 6, 0, 5, 3 };

        void GiftShop(Vector3 floor, float yaw)
        {
            yaw = Mathf.Round(yaw / 90f) * 90f;
            var q = Quaternion.Euler(0, yaw, 0);
            float hw = ShopWidth * .5f, hd = ShopDepth * .5f, H = ShopWall, front = -hd + .12f;
            Vector3 P(float x, float y, float z) => floor + q * new Vector3(x, y, z);
            void B(float x, float y, float z, Vector3 size, F finish, float bevel = .04f, bool solid = false, float tiltX = 0, float turnY = 0)
                => mesh.Box(P(x, y, z), size, finish, bevel, solid, q * Quaternion.Euler(tiltX, turnY, 0));
            void R(float x, float y, float z, Vector3 size, F finish, Quaternion turn, float bevel = .02f)
                => mesh.Box(P(x, y, z), size, finish, bevel, false, q * turn);
            void Ball(float x, float y, float z, Vector3 radius, F finish) => mesh.Ellipsoid(P(x, y, z), radius, finish, q, 7, 12);
            void Disc(float x, float y, float z, float radius, float height, F finish, bool solid = false) => mesh.Disk(P(x, y, z), radius, height, finish, solid, 32);
            F[] candy = { F.CandyCoral, F.CandyMint, F.CandyLilac, F.Red, F.Green, F.Purple, F.Teal, F.CandyCream };
            var cream = new Color(.95f, .88f, .69f);
            var ink = new Color(.23f, .16f, .10f);

            // ---------- Floors ----------
            // Warm wood inside with a star medallion under the centrepiece; a plank porch out front.
            B(0, .015f, 0, new Vector3(ShopWidth, .03f, ShopDepth), F.Wood, .01f, true);
            for (float x = -hw + .3f; x < hw; x += .35f) B(x, .031f, 0, new Vector3(.015f, .003f, ShopDepth - .1f), F.DarkWood, .001f);
            Disc(0, .03f, .3f, 2.3f, .006f, F.CandyCream);
            Disc(0, .032f, .3f, 2.0f, .006f, F.CandyMint);
            Disc(0, .034f, .3f, 1.75f, .006f, F.CandyCream);
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f;
                var ray = Quaternion.Euler(0, a, 0);
                mesh.Box(P(0, .038f, .3f) + q * (ray * Vector3.forward * 1.0f), new Vector3(.3f, .004f, 1.5f), i % 2 == 0 ? F.CandyCoral : F.Brass, .002f, false, q * ray);
            }
            B(0, .015f, -hd - ShopPorch * .5f, new Vector3(ShopWidth + .6f, .03f, ShopPorch), F.Wood, .01f, true);
            for (float x = -hw; x < hw + .3f; x += .3f) B(x, .032f, -hd - ShopPorch * .5f, new Vector3(.02f, .004f, ShopPorch), F.DarkWood, .002f);

            // ---------- Walls ----------
            // Back wall, with a wide window behind the checkout (WinHalf either side, WinSill to WinHead high).
            const float WinHalf = 2.2f, WinSill = 1.35f, WinHead = 3.25f;
            B(0, WinSill * .5f, hd - .12f, new Vector3(ShopWidth, WinSill, .24f), F.CandyMint, .05f, true);
            B(0, (WinHead + H) * .5f, hd - .12f, new Vector3(ShopWidth, H - WinHead, .24f), F.CandyMint, .05f, true);
            foreach (float s in new[] { -1f, 1f })
                B(s * (WinHalf + hw) * .5f, (WinSill + WinHead) * .5f, hd - .12f, new Vector3(hw - WinHalf, WinHead - WinSill, .24f), F.CandyMint, .05f, true);
            B(0, (WinSill + WinHead) * .5f, hd - .12f, new Vector3(WinHalf * 2, WinHead - WinSill, .04f), F.Glass, .01f, true);
            B(0, WinSill, hd - .3f, new Vector3(WinHalf * 2 + .2f, .08f, .2f), F.Wood, .02f);
            B(0, WinHead, hd - .12f, new Vector3(WinHalf * 2 + .1f, .1f, .3f), F.Ivory, .02f);
            foreach (float s in new[] { -1f, 0f, 1f })
                B(s * WinHalf, (WinSill + WinHead) * .5f, hd - .12f, new Vector3(.08f, WinHead - WinSill, .3f), F.Ivory, .02f);
            // The left wall has a tall opening that the hero suit vault turns through (see below).
            B(hw - .12f, H * .5f, 0, new Vector3(.24f, H, ShopDepth), F.CandyMint, .05f, true);
            WallRun(-hd, VaultZ - VaultOpening, 0, H, true);
            WallRun(VaultZ + VaultOpening, hd, 0, H, true);
            WallRun(VaultZ - VaultOpening, VaultZ + VaultOpening, VaultTop, H, true);
            for (float y = .35f; y < H; y += .32f)
            {
                if (y > WinSill && y < WinHead)
                    foreach (float s in new[] { -1f, 1f }) B(s * (WinHalf + hw) * .5f, y, hd + .005f, new Vector3(hw - WinHalf, .035f, .03f), F.Ivory, .01f);
                else B(0, y, hd + .005f, new Vector3(ShopWidth, .035f, .03f), F.Ivory, .01f);
                B(hw + .005f, y, 0, new Vector3(.03f, .035f, ShopDepth), F.Ivory, .01f);
                foreach (var (z0, z1) in y > VaultTop ? new[] { (-hd, hd) } : new[] { (-hd, VaultZ - VaultAlong - .2f), (VaultZ + VaultAlong + .2f, hd) })
                    B(-hw - .005f, y, (z0 + z1) * .5f, new Vector3(.03f, .035f, z1 - z0), F.Ivory, .01f);
            }
            // One stretch of the left wall, from z0 to z1 and from y0 to y1.
            void WallRun(float z0, float z1, float y0, float y1, bool solid)
                => B(-hw + .12f, (y0 + y1) * .5f, (z0 + z1) * .5f, new Vector3(.24f, y1 - y0, z1 - z0), F.CandyMint, .05f, solid);

            // Inside the walls: cream above, wood panelling below, an ivory rail between.
            void Lining(Vector3 center, Vector3 along)
            {
                float length = along.magnitude;
                var size = Mathf.Abs(along.x) > .5f ? new Vector3(length, 1, .03f) : new Vector3(.03f, 1, length);
                B(center.x, (1.05f + H) * .5f, center.z, new Vector3(size.x, H - 1.05f, size.z), F.CandyCream, .005f);
                B(center.x, .52f, center.z, new Vector3(size.x, 1.04f, size.z) + (Mathf.Abs(along.x) > .5f ? Vector3.forward : Vector3.right) * .01f, F.Wood, .005f);
                B(center.x, 1.06f, center.z, new Vector3(size.x, .07f, size.z) + (Mathf.Abs(along.x) > .5f ? Vector3.forward : Vector3.right) * .04f, F.Ivory, .01f);
            }
            Lining(new Vector3(0, 0, hd - .255f), Vector3.right * (ShopWidth - .5f));
            float leftStart = -hd + .25f, leftEnd = hd - .25f;
            Lining(new Vector3(-hw + .255f, 0, (leftStart + VaultZ - VaultOpening) * .5f), Vector3.forward * (VaultZ - VaultOpening - leftStart));
            Lining(new Vector3(-hw + .255f, 0, (VaultZ + VaultOpening + leftEnd) * .5f), Vector3.forward * (leftEnd - VaultZ - VaultOpening));
            Lining(new Vector3(hw - .255f, 0, 0), Vector3.forward * (ShopDepth - .5f));

            // ---------- Storefront ----------
            // Double doors (2.6 m) between two big display windows.
            const float doorHalf = 1.3f, doorTop = 3f, winIn = 1.7f, winOut = 4.7f, sill = .6f;
            float winMid = (winIn + winOut) * .5f, winWide = winOut - winIn;
            B(0, (doorTop + H) * .5f, front, new Vector3(ShopWidth, H - doorTop, .24f), F.CandyMint, .05f, true);
            foreach (float s in new[] { -1f, 1f })
            {
                B(s * (doorHalf + winIn) * .5f, doorTop * .5f, front, new Vector3(winIn - doorHalf, doorTop, .24f), F.CandyMint, .05f, true);
                B(s * (winOut + hw) * .5f, doorTop * .5f, front, new Vector3(hw - winOut, doorTop, .24f), F.CandyMint, .05f, true);
                B(s * winMid, sill * .5f, front, new Vector3(winWide, sill, .24f), F.CandyMint, .05f, true);
                B(s * winMid, (sill + doorTop) * .5f, front, new Vector3(winWide, doorTop - sill, .06f), F.Glass, .01f, true);
                // Window frames: ivory with two mullions and a deep sill.
                B(s * winMid, sill + .02f, -hd - .04f, new Vector3(winWide + .2f, .08f, .2f), F.Ivory, .02f);
                B(s * winMid, doorTop, -hd - .02f, new Vector3(winWide + .1f, .1f, .08f), F.Ivory, .02f);
                foreach (float m in new[] { -1f, 1f }) B(s * winMid + m * winWide / 6f, (sill + doorTop) * .5f, -hd - .01f, new Vector3(.05f, doorTop - sill, .05f), F.Ivory, .01f);
                foreach (float edge in new[] { winIn, winOut }) B(s * edge, (sill + doorTop) * .5f, -hd - .02f, new Vector3(.1f, doorTop - sill, .08f), F.Ivory, .02f);
                // Door frame and a glass door leaf swung open against the inside wall.
                B(s * (doorHalf + .04f), doorTop * .5f, -hd - .02f, new Vector3(.12f, doorTop, .1f), F.Brass, .02f);
                B(s * (doorHalf - .03f), doorTop * .48f, front + .7f, new Vector3(.05f, doorTop * .92f, 1.25f), F.Glass, .01f);
                foreach (float y in new[] { .08f, doorTop * .92f }) B(s * (doorHalf - .03f), y, front + .7f, new Vector3(.07f, .07f, 1.27f), F.Brass, .01f);
                B(s * (doorHalf - .03f), 1.1f, front + .7f, new Vector3(.09f, .05f, .9f), F.Brass, .01f);

                // Window display: three-step riser with souvenirs, lit from behind.
                for (int step = 0; step < 3; step++)
                    B(s * winMid, sill + .05f + step * .22f, front + .45f + step * .3f, new Vector3(winWide - .3f, .1f + step * 0f, .32f), step % 2 == 0 ? F.Ivory : F.CandyCream, .02f);
                MiniTower(P(s * winMid, sill + .54f, front + 1.05f), q, .9f);
                // A mannequin in the latest look on a small stand, nearest the doors.
                float standX = s * (winMid - .9f);
                Disc(standX, sill + .1f, front + .42f, .26f, .06f, F.Ivory);
                Figure(standX, sill + .16f, front + .42f, 180, s < 0
                    ? Look(AvatarHair.Ponytail, AvatarEyewear.RoundSunglasses, AvatarClothing.SailorShirt, AvatarAccessory.FlowerCrown) : Look(AvatarHair.Pompadour, AvatarEyewear.Aviator, AvatarClothing.Tuxedo | AvatarClothing.BowTie, AvatarAccessory.TopHat),
                    s < 0 ? 4 : 2, MannequinPose.Display);
                for (int i = 0; i < 3; i++)
                {
                    float x = s * winMid + (i - 1) * .9f;
                    if (i == 1 || Mathf.Abs(x - standX) < .1f) continue;
                    Crab(P(x, sill + .2f, front + .45f), q, candy[i + (s > 0 ? 0 : 2)]);
                    SunHat(P(x, sill + .32f, front + .75f), i == 0 ? F.CandyCream : F.CandyLilac);
                }
                SnowGlobe(P(s * winMid, sill + .1f, front + .45f), q, F.Teal);

                // Striped awning with a scalloped edge over each window.
                int stripes = Mathf.RoundToInt((winWide + .2f) / .3f);
                for (int i = 0; i < stripes; i++)
                {
                    float x = s * winMid - (stripes - 1) * .15f + i * .3f;
                    var color = i % 2 == 0 ? F.CandyCoral : F.CandyCream;
                    B(x, doorTop + .3f, -hd - .42f, new Vector3(.3f, .04f, .9f), color, .01f, false, -24);
                    Ball(x, doorTop + .1f, -hd - .84f, new Vector3(.15f, .09f, .04f), color);
                }
            }
            // Fan transom over the doors: a lit half-sunburst with brass rays.
            B(0, doorTop + .02f, -hd - .02f, new Vector3(2 * doorHalf + .3f, .1f, .1f), F.Brass, .02f);
            B(0, doorTop + .5f, -hd - .012f, new Vector3(2 * doorHalf - .1f, .8f, .02f), F.Glow, .01f);
            for (int i = 1; i < 8; i++)
            {
                var turn = Quaternion.Euler(0, 0, -90 + i * 22.5f);
                var mid = turn * new Vector3(0, .45f, 0);
                R(mid.x, doorTop + .08f + mid.y, -hd - .03f, new Vector3(.04f, .9f, .03f), F.Brass, turn, .01f);
            }

            // Pilasters at the corners and a cornice with a dentil row.
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    B(sx * (hw - .02f), H * .5f, sz * (hd - .02f), new Vector3(.38f, H, .38f), F.Ivory, .06f);
                    B(sx * (hw - .02f), H - .15f, sz * (hd - .02f), new Vector3(.5f, .2f, .5f), F.Ivory, .05f);
                    B(sx * (hw - .02f), .12f, sz * (hd - .02f), new Vector3(.48f, .24f, .48f), F.Ivory, .05f);
                }
            B(0, H + .08f, 0, new Vector3(ShopWidth + .45f, .16f, ShopDepth + .45f), F.Ivory, .05f, true);
            for (float x = -hw; x <= hw + .01f; x += .3f) B(x, H - .06f, -hd - .06f, new Vector3(.12f, .12f, .1f), F.Ivory, .02f);

            // ---------- Roof, porch, cupola ----------
            bool across = Mathf.Abs(Mathf.Repeat(yaw, 180)) < 1;
            float roofWide = ShopWidth + 1f, roofDeep = ShopDepth + ShopPorch + .5f;
            mesh.CurvedRoof(P(0, H + .16f, -ShopPorch * .5f + .1f), across ? roofWide : roofDeep, across ? roofDeep : roofWide, 1.7f, F.CandyCoral, striped: true);
            foreach (float s in new[] { -1f, 1f })
            {
                B(s * (hw - .15f), 2.4f, -hd - ShopPorch + .2f, new Vector3(.26f, 4.8f, .26f), F.Ivory, .06f, true);
                B(s * (hw - .15f), .12f, -hd - ShopPorch + .2f, new Vector3(.42f, .24f, .42f), F.Ivory, .05f);
                B(s * (hw - .15f), 4.62f, -hd - ShopPorch + .2f, new Vector3(.4f, .2f, .4f), F.Brass, .05f);
            }
            B(0, 6.25f, 0, new Vector3(1.6f, 1.2f, 1.6f), F.Ivory, .08f);
            foreach (float s in new[] { -1f, 1f })
            {
                B(0, 6.3f, s * .81f, new Vector3(1f, .7f, .04f), F.Glass, .01f);
                B(s * .81f, 6.3f, 0, new Vector3(.04f, .7f, 1f), F.Glass, .01f);
            }
            B(0, 6.92f, 0, new Vector3(1.9f, .14f, 1.9f), F.CandyCoral, .05f);
            Ball(0, 7.05f, 0, new Vector3(.85f, .75f, .85f), F.CandyMint);
            mesh.Beam(P(0, 7.6f, 0), P(0, 8.7f, 0), .04f, F.Brass);
            Ball(0, 7.75f, 0, Vector3.one * .1f, F.Brass);
            Ball(0, 8.35f, 0, new Vector3(.42f, .15f, .05f), F.Brass);
            B(.52f, 8.35f, 0, new Vector3(.22f, .24f, .04f), F.Brass, .01f);

            // Porch: lamps, planters, two benches.
            foreach (float s in new[] { -1f, 1f })
            {
                DeckLight(P(s * 2.0f, .03f, -hd - ShopPorch + .35f));
                Pot(P(s * (hw - .7f), .03f, -hd - .55f), .38f, .65f, s > 0 ? 31 : 32);
                float benchX = s * 3.3f, benchZ = -hd - .4f;
                B(benchX, .42f, benchZ, new Vector3(1.6f, .07f, .45f), F.Wood, .02f, true);
                B(benchX, .75f, benchZ + .2f, new Vector3(1.6f, .4f, .06f), F.Wood, .02f);
                foreach (float e in new[] { -.7f, .7f }) B(benchX + e, .21f, benchZ, new Vector3(.08f, .42f, .4f), F.DarkWood, .02f);
            }

            // Outside, left: a rack of beach towels. Outside, right: sunglasses spinner and beach balls.
            float rackX = -hw - .8f;
            float towelZ = -3.0f;
            foreach (float z in new[] { -1.4f, 1.4f }) mesh.Beam(P(rackX, 0, towelZ + z), P(rackX, 2.1f, towelZ + z), .045f, F.DarkWood);
            mesh.Beam(P(rackX, 2.05f, towelZ - 1.45f), P(rackX, 2.05f, towelZ + 1.45f), .035f, F.Brass);
            F[] towels = { F.CandyCoral, F.CandyMint, F.CandyLilac, F.Teal, F.CandyCoral };
            for (int i = 0; i < 5; i++)
            {
                float z = towelZ - 1.1f + i * .55f;
                B(rackX, 1.45f, z, new Vector3(.05f, 1.15f, .46f), towels[i], .01f);
                B(rackX, 1.2f, z, new Vector3(.07f, .1f, .46f), F.Ivory, .005f);
                B(rackX, 1.65f, z, new Vector3(.07f, .1f, .46f), F.Ivory, .005f);
            }
            var spin = new Vector3(hw + .9f, 0, -hd - .6f);
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
            B(hw + .9f, .3f, .4f, new Vector3(.9f, .6f, .7f), F.Wood, .05f, true);
            for (int i = 0; i < 5; i++)
            {
                float x = hw + .65f + (i % 3) * .25f, z = .25f + (i / 3) * .3f;
                Ball(x, .72f + (i / 3) * .08f, z, Vector3.one * .19f, candy[i]);
                Ball(x, .72f + (i / 3) * .08f, z, new Vector3(.195f, .05f, .195f), F.Ivory);
            }

            // ---------- Ceiling ----------
            foreach (float x in new[] { -4f, -2f, 2f, 4f }) B(x, H - .09f, 0, new Vector3(.16f, .18f, ShopDepth - .5f), F.Ivory, .03f);
            foreach (float z in new[] { -2.2f, 2.2f }) B(0, H - .09f, z, new Vector3(ShopWidth - .5f, .18f, .16f), F.Ivory, .03f);
            Chandelier(P(0, H - .1f, .3f), 1.3f, .75f);
            Chandelier(P(-3.6f, H - .1f, -1.4f), .9f, .55f);
            Chandelier(P(3.6f, H - .1f, -1.4f), .9f, .55f);

            // ---------- Centrepiece ----------
            Disc(0, .03f, .3f, 1.25f, .3f, F.Ivory, true);
            Disc(0, .33f, .3f, .95f, .3f, F.CandyMint, true);
            Disc(0, .63f, .3f, .65f, .25f, F.Ivory, true);
            mesh.ArcSlab(P(0, .32f, .3f), Vector2.one * 1.2f, Vector2.one * 1.27f, .03f, 0, 360, F.Glow, false, 48);
            mesh.ArcSlab(P(0, .62f, .3f), Vector2.one * .9f, Vector2.one * .97f, .03f, 0, 360, F.Glow, false, 40);
            MiniTower(P(0, .88f, .3f), q, 1.5f);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f + 30f, r = a * Mathf.Deg2Rad;
                Crab(P(Mathf.Cos(r) * 1.08f, .43f, .3f + Mathf.Sin(r) * 1.08f), q * Quaternion.Euler(0, -90 - a, 0), candy[i % 3]);
                if (i % 2 == 0) SnowGlobe(P(Mathf.Cos(r + .5f) * .8f, .63f, .3f + Mathf.Sin(r + .5f) * .8f), q, candy[(i / 2) + 3]);
            }

            // ---------- Left wall: the hero suit vault ----------
            // Fourteen lit glass pods, each holding a player in a different skin, glide around a rounded-
            // rectangle loop. Its flat front runs along just inside the shop, so suits slide past in a row;
            // the long sides and back run deep into a housing outside, so new suits keep coming into view.
            float vx = -hw + .12f - VaultCentre, vz = VaultZ;
            float trackAlong = 1.9f, trackDeep = 3.0f, trackCorner = .9f, podRadius = .38f, podHeight = 2.05f;
            // A rounded rectangle centred on the loop, in shop axes: x from -back to +front, z within +-half,
            // corners of radius r. Filled (ring = 0) or just a wall of that thickness (ring > 0).
            void RoundRect(float y, float height, float half, float front, float back, float r, F finish, bool solid, float ring = 0)
            {
                float fx = front - r, bx = -back + r, hz = half - r;
                if (ring <= 0)
                {
                    B(vx + (fx + bx) * .5f, y + height * .5f, vz, new Vector3(fx - bx, height, half * 2), finish, .01f, solid);
                    B(vx + (front + fx) * .5f, y + height * .5f, vz, new Vector3(front - fx, height, hz * 2), finish, .01f, solid);
                    B(vx + (bx - back) * .5f, y + height * .5f, vz, new Vector3(bx + back, height, hz * 2), finish, .01f, solid);
                }
                else
                {
                    foreach (float e in new[] { -1f, 1f }) B(vx + (fx + bx) * .5f, y + height * .5f, vz + e * (half - ring * .5f), new Vector3(fx - bx, height, ring), finish, .01f, solid);
                    B(vx + front - ring * .5f, y + height * .5f, vz, new Vector3(ring, height, hz * 2), finish, .01f, solid);
                    B(vx - back + ring * .5f, y + height * .5f, vz, new Vector3(ring, height, hz * 2), finish, .01f, solid);
                }
                Corner(fx, hz, 0, y, height, r, ring, finish, solid);
                Corner(bx, hz, 90, y, height, r, ring, finish, solid);
                Corner(bx, -hz, 180, y, height, r, ring, finish, solid);
                Corner(fx, -hz, 270, y, height, r, ring, finish, solid);
            }
            // A quarter-round corner at (x, z) from the loop's centre, sweeping 90 degrees from the local
            // direction "from" (0 = +x, 90 = +z). ArcSlab angles are in lobby axes, so turn them by the shop's yaw.
            void Corner(float x, float z, float from, float y, float height, float r, float ring, F finish, bool solid)
            {
                Vector3 Dir(float deg) => q * new Vector3(Mathf.Cos(deg * Mathf.Deg2Rad), 0, Mathf.Sin(deg * Mathf.Deg2Rad));
                float a0 = Mathf.Atan2(Dir(from).x, Dir(from).z) * Mathf.Rad2Deg;
                float a1 = a0 + Mathf.DeltaAngle(a0, Mathf.Atan2(Dir(from + 90).x, Dir(from + 90).z) * Mathf.Rad2Deg);
                mesh.ArcSlab(P(vx + x, y, vz + z), Vector2.one * (ring > 0 ? r - ring : .01f), Vector2.one * r, height, Mathf.Min(a0, a1), Mathf.Max(a0, a1), finish, solid, 12);
            }
            float wallLine = VaultCentre, housingBack = trackDeep + .55f, housingCorner = trackCorner + .55f;
            // Floor under the whole loop, with a glowing rim.
            RoundRect(.03f, .12f, VaultAlong, trackDeep + .5f, housingBack, housingCorner, F.Ivory, true);
            RoundRect(.15f, .02f, VaultAlong - .04f, trackDeep + .46f, housingBack - .04f, housingCorner - .04f, F.Glow, false, .06f);
            // The dark core the pods circle around, and a brass canopy over the loop.
            RoundRect(.15f, 2.15f, trackAlong - .5f, trackDeep - .5f, trackDeep - .5f, Mathf.Max(.2f, trackCorner - .5f), F.CandyGrape, false);
            RoundRect(2.3f, .08f, trackAlong + .5f, trackDeep + .5f, trackDeep + .5f, trackCorner + .5f, F.Brass, false);
            RoundRect(2.28f, .02f, trackAlong + .5f, trackDeep + .5f, trackDeep + .5f, trackCorner + .5f, F.Glow, false, .05f);
            // The housing outside: mint walls with a teal, glow-striped inside, from the wall line round the back.
            float sideLength = wallLine + housingBack - housingCorner;
            foreach (float e in new[] { -1f, 1f })
            {
                B(vx + (wallLine - housingBack + housingCorner) * .5f, VaultTop * .5f + .03f, vz + e * (VaultAlong + .1f), new Vector3(sideLength, VaultTop, .2f), F.CandyMint, .02f, true);
                B(vx + (wallLine - housingBack + housingCorner) * .5f, VaultTop * .5f + .03f, vz + e * (VaultAlong - .01f), new Vector3(sideLength, VaultTop, .02f), F.Teal, .005f);
                for (int i = 0; i < 3; i++)
                    B(vx + wallLine - .9f - i * 1.6f, VaultTop * .5f + .1f, vz + e * (VaultAlong - .03f), new Vector3(.04f, VaultTop - .4f, .04f), F.Glow, .01f);
            }
            float backHalf = VaultAlong - housingCorner;
            B(vx - housingBack - .1f, VaultTop * .5f + .03f, vz, new Vector3(.2f, VaultTop, backHalf * 2), F.CandyMint, .02f, true);
            B(vx - housingBack + .01f, VaultTop * .5f + .03f, vz, new Vector3(.02f, VaultTop, backHalf * 2), F.Teal, .005f);
            B(vx - housingBack + .03f, VaultTop * .5f + .1f, vz, new Vector3(.04f, VaultTop - .4f, .04f), F.Glow, .01f);
            Corner(-housingBack + housingCorner, backHalf, 90, .03f, VaultTop, housingCorner + .2f, .2f, F.CandyMint, true);
            Corner(-housingBack + housingCorner, -backHalf, 180, .03f, VaultTop, housingCorner + .2f, .2f, F.CandyMint, true);
            Corner(-housingBack + housingCorner, backHalf, 90, .03f, VaultTop, housingCorner + .01f, .02f, F.Teal, false);
            Corner(-housingBack + housingCorner, -backHalf, 180, .03f, VaultTop, housingCorner + .01f, .02f, F.Teal, false);
            // Roof: from the wall line round the back, so nothing pokes into the shop and no sky shows.
            float roofBack = housingBack + .2f, roofCorner = housingCorner + .2f, roofHalf = VaultAlong + .2f;
            B(vx + (wallLine - roofBack + roofCorner) * .5f, VaultTop + .11f, vz, new Vector3(wallLine + roofBack - roofCorner, .16f, roofHalf * 2), F.CandyMint, .02f, true);
            B(vx - roofBack + roofCorner * .5f, VaultTop + .11f, vz, new Vector3(roofCorner, .16f, (roofHalf - roofCorner) * 2), F.CandyMint, .02f, true);
            Corner(-roofBack + roofCorner, roofHalf - roofCorner, 90, VaultTop + .03f, .16f, roofCorner, 0, F.CandyMint, true);
            Corner(-roofBack + roofCorner, -(roofHalf - roofCorner), 180, VaultTop + .03f, .16f, roofCorner, 0, F.CandyMint, true);
            B(vx + (wallLine - housingBack + housingCorner) * .5f, VaultTop + .025f, vz, new Vector3(sideLength, .01f, VaultAlong * 2), F.CandyGrape, .002f);
            // Inside the shop: a purple frame around the opening with the HERO SUITS header on top.
            B(-hw + .26f, (VaultTop + H) * .5f, vz, new Vector3(.06f, H - VaultTop, VaultOpening * 2 + .3f), F.CandyGrape, .03f);
            B(-hw + .3f, VaultTop + .03f, vz, new Vector3(.04f, .04f, VaultOpening * 2), F.Glow, .01f);
            foreach (float e in new[] { -1f, 1f })
            {
                B(-hw + .3f, VaultTop * .5f, vz + e * (VaultOpening + .08f), new Vector3(.14f, VaultTop, .16f), F.CandyGrape, .03f, true);
                B(-hw + .38f, VaultTop * .5f, vz + e * VaultOpening, new Vector3(.03f, VaultTop - .2f, .03f), F.Glow, .01f);
            }
            Sign("HERO SUITS", P(-hw + .3f, (VaultTop + H) * .5f, vz), .03f, cream, new Vector3(0, yaw - 90, 0));
            Accent("Hero suit vault", P(-hw + 1.6f, 2.7f, vz), new Color(.75f, .85f, 1f), 1.8f, 5f);
            Accent("Hero suit housing", P(vx - 1.2f, 2.3f, vz), new Color(.6f, .8f, 1f), 1.2f, 4.5f);

            // The moving pods. Each pod is its own object; MarinaPodTrack slides them around the loop.
            var loop = new GameObject("Hero suit vault loop").transform;
            loop.SetParent(visual, false);
            loop.localPosition = P(vx, .15f, vz);
            loop.localRotation = q;
            var track = loop.gameObject.AddComponent<MarinaPodTrack>();
            track.Setup(trackDeep, trackAlong, trackCorner, .22f, VaultCentre - .5f, VaultCentre + .5f);
            for (int i = 0; i < suitLooks.Length; i++)
            {
                var pod = new GameObject("Suit pod " + (i + 1)).transform;
                pod.SetParent(loop, false);
                var podMesh = new MarinaMesh();
                podMesh.Disk(Vector3.up * .0f, podRadius + .04f, .08f, F.Ivory, false, 28);
                podMesh.ArcSlab(Vector3.up * .08f, Vector2.one * podRadius, Vector2.one * (podRadius + .03f), .015f, 0, 360, F.Glow, false, 28);
                podMesh.Box(new Vector3(0, 1.05f, -(podRadius - .02f)), new Vector3(.62f, 1.95f, .04f), i % 2 == 0 ? F.Teal : F.CandyGrape, .02f);
                podMesh.Tube(new[] { Vector3.up * .08f, Vector3.up * podHeight }, podRadius + .02f, F.Glass, 24);
                podMesh.ArcSlab(Vector3.up * (podHeight - .04f), Vector2.one * (podRadius - .05f), Vector2.one * (podRadius + .05f), .05f, 0, 360, F.Brass, false, 28);
                podMesh.Disk(Vector3.up * (podHeight - .05f), podRadius - .05f, .02f, F.Glow, false, 24);
                podMesh.Flush(pod, 0);
                track.Add(pod, podMesh);
            }
            track.Place(0);
            FogMachine(P(-hw + .55f, .12f, vz), q, new Vector3(1.2f, .02f, VaultOpening * 1.7f));
            foreach (Transform pod in loop)
            {
                int i = pod.GetSiblingIndex();
                Mannequins.Create(pod, pod.TransformPoint(Vector3.up * .08f), pod.rotation, suitLooks[i], suitColors[i], MannequinPose.Display);
            }

            // ---------- Right wall: apparel and hats ----------
            float rx = hw - .45f;
            // One long clothes rod. Each display body hangs from it on a white stick through the top
            // to the middle of its head, like a lollipop on its stick; the body hangs limp, feet off the floor. They hang side-on, like clothes on a store rack.
            float rodX = rx - .4f, rodY = 2.4f;
            mesh.Beam(P(rodX, rodY, -3.25f), P(rodX, rodY, 1.35f), .025f, F.Brass);
            foreach (float z in new[] { -3.2f, -.95f, 1.3f }) B((rodX + hw) / 2, rodY, z, new Vector3(hw - rodX, .05f, .05f), F.Brass, .01f);
            for (int n = 0; n < 6; n++)
            {
                float hz = -2.45f + n * .6f;
                mesh.Beam(P(rodX, 1.99f, hz), P(rodX, rodY, hz), .014f, F.Ivory);
                Ball(rodX, rodY + .03f, hz, Vector3.one * .035f, F.Ivory);
                Figure(rodX, .55f, hz, 180, rackLooks[n], rackColors[n], MannequinPose.Hanging);
            }
            // Head cosmetics on show: one long shelf of lollipops whose tops are real player heads,
            // each wearing a different hat or crown.
            float shelfY = 2.5f;
            B(rx, shelfY, .2f - .4f, new Vector3(.45f, .05f, 6f), F.Wood, .015f);
            foreach (float z in new[] { -3f, -.2f, 2.6f }) B(rx + .12f, shelfY - .12f, z, new Vector3(.2f, .2f, .04f), F.Wood, .01f);
            for (int i = 0; i < hatLooks.Length; i++)
            {
                float hz = -2.7f + i * 1.05f, stickTop = shelfY + .3f;
                Disc(rx - .02f, shelfY + .025f, hz, .09f, .03f, F.Brass);
                mesh.Beam(P(rx - .02f, shelfY + .05f, hz), P(rx - .02f, stickTop, hz), .014f, F.Ivory);
                Figure(rx - .02f, stickTop - .22f, hz, -90, hatLooks[i], hatColors[i], MannequinPose.Head);
            }
            Valance(rx - .2f, -.2f, 6f, F.CandyLilac, "TEES  &  HATS", -1, 3.75f);

            // ---------- Back wall: lit bays either side of the checkout ----------
            float bz = hd - .5f;
            int item = 0;
            foreach (float s in new[] { -1f, 1f })
            {
                float cx = s * 4f;
                B(cx, 1.6f, bz + .15f, new Vector3(3.1f, 2.9f, .05f), F.CandyMint, .01f);
                foreach (float e in new[] { -1.6f, 1.6f }) B(cx + e, 1.6f, bz, new Vector3(.16f, 3f, .4f), F.Ivory, .03f);
                for (int level = 0; level < 4; level++)
                {
                    float y = .55f + level * .6f;
                    B(cx, y, bz, new Vector3(3.05f, .05f, .38f), F.Wood, .015f);
                    B(cx, y - .04f, bz - .17f, new Vector3(3.0f, .02f, .02f), F.Glow, .005f);
                    for (float x = cx - 1.35f; x < cx + 1.4f; x += .34f, item++)
                    {
                        if (item % 7 == 3) { MiniTower(P(x, y + .03f, bz), q, .32f); continue; }
                        if (item % 7 == 5) { Mug(P(x, y + .03f, bz), candy[item % candy.Length]); continue; }
                        float h = .16f + (item % 3) * .05f;
                        B(x, y + .025f + h * .5f, bz, new Vector3(.22f, h, .2f), candy[item % candy.Length], .02f);
                    }
                }
                B(cx, 3.35f, bz - .05f, new Vector3(3.3f, .45f, .12f), s < 0 ? F.CandyCoral : F.Teal, .04f);
                Sign(s < 0 ? "SOUVENIRS" : "SWEETS", P(cx, 3.36f, bz - .12f), .022f, cream, new Vector3(0, yaw, 0));
            }

            // Checkout: a long counter in front of the window, with candy jars and a till.
            B(0, .5f, hd - 1.55f, new Vector3(4.2f, 1f, .8f), F.Wood, .06f, true);
            B(0, 1.03f, hd - 1.55f, new Vector3(4.4f, .07f, .95f), F.CandyCream, .02f);
            for (int i = 0; i < 4; i++) B(-1.5f + i, .52f, hd - 1.96f, new Vector3(.8f, .6f, .03f), i % 2 == 0 ? F.CandyCoral : F.CandyMint, .01f);
            B(-1.4f, 1.18f, hd - 1.45f, new Vector3(.45f, .22f, .35f), F.Metal, .04f);
            B(-1.4f, 1.42f, hd - 1.33f, new Vector3(.36f, .24f, .04f), F.Teal, .02f);
            for (int i = 0; i < 4; i++)
            {
                float x = .1f + i * .45f;
                Ball(x, 1.25f, hd - 1.6f, new Vector3(.12f, .12f, .12f), candy[3 + i % 3]);
                Ball(x, 1.25f, hd - 1.6f, new Vector3(.17f, .2f, .17f), F.Glass);
                Disc(x, 1.43f, hd - 1.6f, .12f, .05f, F.Brass);
            }

            // Two mannequins flanking the checkout, facing the doors.
            foreach (float s in new[] { -1f, 1f })
            {
                float mx = s * 2.75f, mz = hd - 1.6f;
                Disc(mx, .03f, mz, .38f, .14f, F.Ivory, true);
                mesh.ArcSlab(P(mx, .16f, mz), Vector2.one * .34f, Vector2.one * .39f, .02f, 0, 360, F.Brass, false, 32);
                Figure(mx, .17f, mz, 180, s < 0
                    ? Look(AvatarHair.Curls, AvatarEyewear.Heart, AvatarClothing.RoyalMantle, AvatarAccessory.Crown) : Look(AvatarHair.Afro, AvatarEyewear.Star, AvatarClothing.PufferVest, AvatarAccessory.Headphones),
                    s < 0 ? 5 : 7, MannequinPose.Display);
            }

            // ---------- Display tables and a glass case ----------
            foreach (float s in new[] { -1f, 1f })
            {
                float tx = s * 3.4f, tz = -1.6f;
                B(tx, .38f, tz, new Vector3(1.8f, .76f, 1.1f), F.Wood, .06f, true);
                B(tx, .79f, tz, new Vector3(1.9f, .06f, 1.2f), F.CandyCream, .02f);
                B(tx, .98f, tz + .2f, new Vector3(1.2f, .35f, .5f), F.Wood, .04f, true);
                B(tx, 1.17f, tz + .2f, new Vector3(1.3f, .05f, .6f), F.CandyCream, .02f);
                for (int i = 0; i < 4; i++)
                {
                    float x = tx - .6f + i * .4f;
                    for (int fold = 0; fold < 3; fold++)
                        B(x, .85f + fold * .06f, tz - .32f, new Vector3(.34f, .055f, .36f), candy[(i + fold + (s > 0 ? 2 : 0)) % candy.Length], .015f);
                }
                for (int i = 0; i < 3; i++) SnowGlobe(P(tx - .4f + i * .4f, 1.2f, tz + .2f), q, candy[(i + (s > 0 ? 3 : 0)) % candy.Length]);
            }
            B(3.4f, .45f, 1.6f, new Vector3(1.6f, .9f, .7f), F.Wood, .05f, true);
            B(3.4f, 1.15f, 1.6f, new Vector3(1.5f, .5f, .6f), F.Glass, .02f);
            B(3.4f, .92f, 1.6f, new Vector3(1.5f, .03f, .6f), F.Glow, .01f);
            for (int i = 0; i < 6; i++) B(2.9f + (i % 3) * .5f, .99f, 1.45f + (i / 3) * .3f, new Vector3(.18f, .1f, .14f), i % 2 == 0 ? F.Brass : F.CandyLilac, .02f);

            // Warm light inside.
            Accent("Gift shop", P(0, 3.4f, .3f), new Color(1, .82f, .6f), 2.2f, 8.5f);
            Accent("Gift shop back", P(0, 3.2f, hd - 2f), new Color(1, .86f, .66f), 1.4f, 6f);

            // A real player figure (see Core Mannequins). Skipped when no maker is registered.
            void Figure(float x, float y, float z, float turnY, AvatarAppearance look, int color, MannequinPose pose)
                => Mannequins.Create(visual, P(x, y, z), q * Quaternion.Euler(0, turnY, 0), look, color, pose);

            // A wall-length valance header with a glowing underside and a label.
            // facing: +1 reads from the +x side (left wall), -1 from the -x side (right wall).
            void Valance(float x, float z, float length, F color, string label, float facing, float y = 3.25f)
            {
                B(x, y, z, new Vector3(.12f, .5f, length), color, .04f);
                B(x + facing * .07f, y - .27f, z, new Vector3(.02f, .04f, length - .2f), F.Glow, .005f);
                Sign(label, P(x + facing * .07f, y, z), .02f, cream, new Vector3(0, yaw - 90 * facing, 0));
            }
        }

        // A fog machine: low, slow, soft mist that rolls out along the floor (here, out of the hero suit vault).
        // pos/turn = the emitter's middle and facing (mist drifts along +x of turn); area = the emitter box.
        void FogMachine(Vector3 pos, Quaternion turn, Vector3 area)
        {
            var source = Resources.Load<Material>("Tsunami/Spray");
            if (!source) return;
            var fog = new GameObject("Fog machine").transform;
            fog.SetParent(visual, false);
            fog.localPosition = pos;
            fog.localRotation = turn;
            var mist = fog.gameObject.AddComponent<ParticleSystem>();
            mist.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = mist.main;
            main.loop = true; main.playOnAwake = true; main.maxParticles = 50; main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 9f);
            main.startSpeed = 0;
            main.startSize = new ParticleSystem.MinMaxCurve(.45f, .9f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.startColor = new Color(.86f, .93f, 1f, .1f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.gravityModifier = 0;
            var emission = mist.emission; emission.rateOverTime = 6;
            var shape = mist.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = area;
            var drift = mist.velocityOverLifetime; drift.enabled = true; drift.space = ParticleSystemSimulationSpace.Local;
            drift.x = new ParticleSystem.MinMaxCurve(.08f, .22f); drift.y = new ParticleSystem.MinMaxCurve(-.01f, .005f); drift.z = new ParticleSystem.MinMaxCurve(-.08f, .08f);
            var grow = mist.sizeOverLifetime; grow.enabled = true; grow.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .8f, 1, 1.3f));
            var spin = mist.rotationOverLifetime; spin.enabled = true; spin.z = new ParticleSystem.MinMaxCurve(-.15f, .15f);
            var fade = mist.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .25f), new GradientAlphaKey(.8f, .7f), new GradientAlphaKey(0, 1) });
            fade.color = gradient;
            var material = new Material(source) { name = "Fog machine mist" };
            var cloud = Resources.Load<Texture2D>("CandyScenery/StormCloudPuff");
            if (cloud) { material.SetTexture("_CloudTexture", cloud); material.SetFloat("_UseCloudTexture", 1); }
            extraMaterials.Add(material);
            var renderer = mist.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingFudge = 10;
            mist.Play();
        }

        // A souvenir tower of tiny candy blocks, three per layer, layers crossed.
        void MiniTower(Vector3 bottom, Quaternion q, float height)
        {
            int layers = Mathf.Max(3, Mathf.RoundToInt(height / .06f));
            float h = height / layers, w = Mathf.Min(h * 1.1f, .09f), l = w * 3.2f;
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

        // A snow globe on a wooden base with a tiny block tower inside.
        void SnowGlobe(Vector3 bottom, Quaternion q, F baseColor)
        {
            mesh.Disk(bottom, .12f, .08f, baseColor, false, 16);
            MiniTower(bottom + Vector3.up * .08f, q, .1f);
            mesh.Ellipsoid(bottom + Vector3.up * .2f, Vector3.one * .14f, F.Glass, q, 8, 14);
        }

        void Mug(Vector3 bottom, F color)
        {
            mesh.Disk(bottom, .07f, .16f, color, false, 14);
            mesh.Disk(bottom + Vector3.up * .155f, .065f, .01f, F.DarkWood, false, 14);
            mesh.Ellipsoid(bottom + new Vector3(.08f, .08f, 0), new Vector3(.03f, .05f, .015f), color, rings: 4, sides: 8);
        }

        // A T-shirt on a hanger, facing along q's forward.
        void Shirt(Vector3 hook, Quaternion q, F color)
        {
            mesh.Beam(hook + Vector3.up * .4f, hook + Vector3.up * .32f, .008f, F.Brass);
            mesh.Box(hook + Vector3.up * .29f, new Vector3(.36f, .03f, .03f), F.DarkWood, .01f, false, q);
            mesh.Box(hook, new Vector3(.34f, .5f, .03f), color, .02f, false, q);
            foreach (float s in new[] { -1f, 1f })
                mesh.Box(hook + q * new Vector3(s * .22f, .18f, 0), new Vector3(.16f, .13f, .03f), color, .02f, false, q * Quaternion.Euler(0, 0, s * -25));
        }

        void HatStand(Vector3 foot)
        {
            mesh.Disk(foot, .3f, .05f, F.DarkWood, false, 18);
            mesh.Beam(foot, foot + Vector3.up * 1.75f, .035f, F.DarkWood);
            SunHat(foot + Vector3.up * 1.76f, F.CandyCream);
            F[] colors = { F.Ivory, F.CandyLilac, F.CandyMint };
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f * Mathf.Deg2Rad;
                var tip = foot + new Vector3(Mathf.Cos(a) * .38f, 1.3f, Mathf.Sin(a) * .38f);
                mesh.Beam(foot + Vector3.up * 1.15f, tip, .02f, F.DarkWood);
                SunHat(tip + Vector3.up * .02f, colors[i]);
            }
        }

        // A brass ring chandelier hanging from the ceiling, with glowing bulbs and a glass globe.
        void Chandelier(Vector3 ceiling, float drop, float radius)
        {
            var hub = ceiling + Vector3.down * drop;
            mesh.Beam(ceiling, hub, .025f, F.Brass);
            mesh.Disk(ceiling + Vector3.down * .04f, .14f, .04f, F.Brass, false, 14);
            var ring = new List<Vector3>();
            for (int i = 0; i <= 24; i++) { float a = i * Mathf.PI / 12; ring.Add(hub + new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius)); }
            mesh.Tube(ring, .03f, F.Brass, 8);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4;
                var arm = hub + new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius);
                mesh.Beam(hub, arm, .015f, F.Brass);
                mesh.Disk(arm, .05f, .05f, F.Brass, false, 10);
                mesh.Ellipsoid(arm + Vector3.up * .11f, new Vector3(.055f, .08f, .055f), F.Glow, rings: 6, sides: 10);
            }
            mesh.Ellipsoid(hub + Vector3.down * .08f, Vector3.one * .16f, F.Glow, rings: 8, sides: 12);
        }
    }
}
