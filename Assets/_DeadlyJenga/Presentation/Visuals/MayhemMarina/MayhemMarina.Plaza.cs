using System.Collections.Generic;
using UnityEngine;
using F=DeadlyJenga.CandyArt.Marina.MarinaFinish;

namespace DeadlyJenga.CandyArt.Marina
{
    // Showtime Plaza leads into the covered cinema and out to the waterfront.
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

        void CruiseShips()
        {
            var fleet = new GameObject("Marina cruise ships");
            fleet.transform.SetParent(visual, false);
            fleet.AddComponent<MarinaCruiseShips>().Build(visual.Find("Marina ocean"), OceanLevel);
            var sea = new GameObject("Marina sea life");
            sea.transform.SetParent(visual, false);
            sea.transform.localPosition = Vector3.up * OceanLevel;
            sea.AddComponent<CandySeaLife>().Build(110, 150, true, visual.Find("Marina ocean"));
        }

        void ShowtimePlaza()
        {
            var floor = new Vector3(PlazaCenter.x, PlazaTop, PlazaCenter.z);
            // Rock footing and a single flat stone deck, flush within the capsule step with its neighbours.
            mesh.Disk(floor + Vector3.down * 2.2f, PlazaRadius + .3f, 2.07f, F.Rock, true, 72);
            mesh.Disk(floor + Vector3.down * .13f, PlazaRadius, .13f, F.Stone, true, 72);

            // Flush inlays: a candy-tile mosaic medallion and a brass ring (art only, no collision).
            var mosaic = floor + Vector3.back * 3.85f;
            mesh.Disk(mosaic, 1.15f, .010f, F.CandyCream, false, 48);
            F[] tiles = { F.CandyMint, F.CandyCoral, F.CandyLilac, F.CandyCream };
            for (int ring = 0; ring < 3; ring++)
            {
                float inner = 1.38f + ring * .38f, outer = inner + .32f;
                int count = 16 + ring * 8;
                for (int i = 0; i < count; i++)
                {
                    float from = i * 360f / count + 1.2f, to = (i + 1) * 360f / count - 1.2f;
                    mesh.ArcSlab(mosaic, Vector2.one * inner, Vector2.one * outer, .010f, from, to, tiles[(i + ring) % tiles.Length], false, 3);
                }
            }
            // A five-pointed candy star at the very centre.
            for (int i = 0; i < 5; i++)
            {
                float a = i * 72f;
                var ray = Quaternion.Euler(0, a, 0) * Vector3.forward * .4f;
                mesh.Box(mosaic + ray + Vector3.up * .016f, new Vector3(.29f, .004f, .80f), F.CandyCoral, .002f, false, Quaternion.Euler(0, a, 0));
            }
            mesh.ArcSlab(mosaic, Vector2.one * 1.15f, Vector2.one * 1.26f, .010f, 0, 360, F.Brass, false, 48);
            mesh.ArcSlab(floor, Vector2.one * (PlazaRadius - .62f), Vector2.one * (PlazaRadius - .48f), .008f, 0, 360, F.Brass, false, 72);

            // Rounded masonry copings only where the plaza meets open water.
            for (int i = 0; i < 64; i++)
            {
                float a = -180 + i * 5.625f;
                if (PlazaEdgeOpen(a)) continue;
                var p = OnPlaza(a, PlazaRadius - .18f, PlazaTop - PlazaCenter.y + .08f);
                mesh.Box(p, new Vector3(.86f, .2f, .42f), F.Ivory, .07f, false, Quaternion.Euler(0, a + 90, 0));
            }
            // Low lanterns ring the plaza edge so it reads as a stage at every time of day.
            float[] posts = { -160f, 140f, 180f };
            foreach (float a in posts)
                DeckLight(OnPlaza(a, PlazaRadius - .55f, PlazaTop - PlazaCenter.y));

            // Festive pennant bunting strung between neighbouring lantern posts, high above head height.
            F[] pennants = { F.CandyCoral, F.CandyMint, F.CandyLilac, F.Brass };
            for (int p = 0; p < posts.Length; p++)
            {
                float a0 = posts[p], a1 = posts[(p + 1) % posts.Length];
                if (a1 <= a0) a1 += 360;
                if (a1 - a0 > 110 || (a0 < 0 && a1 > 0)) continue;
                var from = OnPlaza(a0, PlazaRadius - .55f, PlazaTop - PlazaCenter.y + 3.1f);
                var to = OnPlaza(a1, PlazaRadius - .55f, PlazaTop - PlazaCenter.y + 3.1f);
                var rope = new List<Vector3>();
                for (int i = 0; i <= 12; i++)
                {
                    float t = i / 12f;
                    rope.Add(Vector3.Lerp(from, to, t) + Vector3.down * Mathf.Sin(t * Mathf.PI) * .55f);
                }
                mesh.Tube(rope, .015f, F.CreamFabric, 6);
                var along = Quaternion.LookRotation(Vector3.ProjectOnPlane(to - from, Vector3.up));
                for (int i = 1; i < 12; i++)
                    mesh.Box(rope[i] + Vector3.down * .16f, new Vector3(.02f, .3f, .22f), pennants[(i + p) % pennants.Length], .01f, false, along * Quaternion.Euler(0, 90, 0));
            }
            // Flower planters between the lanterns soften the open-water edge.
            foreach (float a in new[] { -145f, -75f, 128f, 160f })
                Pot(OnPlaza(a, PlazaRadius - .7f, PlazaTop - PlazaCenter.y), .42f, .7f, (int)a + 400);
        }

        void StyleStage()
        {
            var turn = StyleStageTurn;
            Vector3 At(float x, float y, float z) => new Vector3(Wardrobe.x, CinemaFloor, Wardrobe.z) + turn * new Vector3(x, y, z);

            // Raised runway disk with a glowing rim; the wardrobe places the player on it.
            mesh.Disk(At(0, 0, 0), 1.35f, .2f, F.Stone, true, 48);
            mesh.ArcSlab(At(0, 0, 0), Vector2.one * 1.35f, Vector2.one * 1.47f, .05f, 0, 360, F.Glow, false, 48);
            mesh.ArcSlab(At(0, .2f, 0), Vector2.one * 1.12f, Vector2.one * 1.2f, .008f, 0, 360, F.Brass, false, 48);

            // Free-standing vanity: carved panel, illuminated oval mirror and a crown arch.
            mesh.Box(At(0, 1.95f, 2.3f), new Vector3(3.7f, 3.9f, .3f), F.Teal, .16f, true, turn);
            mesh.Box(At(0, .12f, 2.3f), new Vector3(4.1f, .24f, .62f), F.DarkWood, .08f, true, turn);
            var crown = new List<Vector3>();
            for (int i = 0; i <= 32; i++)
            {
                float a = Mathf.PI - i * Mathf.PI / 32;
                crown.Add(At(Mathf.Cos(a) * 1.85f, 3.9f + Mathf.Sin(a) * .62f, 2.18f));
            }
            mesh.Tube(crown, .2f, F.Ivory, 12, true);
            mesh.Box(At(0, 4.13f, 2.12f), new Vector3(1.80f, .48f, .34f), F.Teal, .11f, false, turn);
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

            // Keep the cabinet and clothing rack on opposite sides of the mirror.
            {
                var rack = turn * Quaternion.Euler(0, -38, 0);
                Vector3 R(float x, float y, float z) => At(2.35f, 0, 1.15f) + rack * new Vector3(x, y, z);
                mesh.Beam(R(-.75f, 0, 0), R(-.75f, 2.29f, 0), .045f, F.Brass, true);
                mesh.Beam(R(.75f, 0, 0), R(.75f, 2.29f, 0), .045f, F.Brass, true);
                mesh.Beam(R(-.78f, 1.98f, 0), R(.78f, 1.98f, 0), .03f, F.Brass);
                mesh.Box(R(0, .08f, 0), new Vector3(1.7f, .16f, .55f), F.DarkWood, .05f, false, rack);
                for (int j = 0; j < 4; j++)
                {
                    var color = (j + 1) % 2 == 0 ? F.MintFabric : F.CoralFabric;
                    var p = R((j - 1.5f) * .36f, 1.38f, 0);
                    mesh.Box(p, new Vector3(.3f, .85f, .12f), color, .06f, false, rack);
                    mesh.Box(p + rack * new Vector3(-.18f, .24f, 0), new Vector3(.16f, .5f, .12f), color, .05f, false, rack * Quaternion.Euler(0, 0, -14));
                    mesh.Box(p + rack * new Vector3(.18f, .24f, 0), new Vector3(.16f, .5f, .12f), color, .05f, false, rack * Quaternion.Euler(0, 0, 14));
                }
                mesh.Box(R(0, 2.25f, .05f), new Vector3(1.67f, .10f, .58f), F.Wood, .025f, false, rack);
                for (int j = 0; j < 3; j++)
                {
                    var hat = R((j - 1) * .5f, 2.3f, .05f);
                    mesh.Disk(hat, .22f, .05f, F.Metal, false, 20);
                    mesh.Disk(hat + Vector3.up * .05f, .14f, .26f, (F)((int)F.Red + (j + 1) % 3), false, 20);
                }
                Pot(At(2.05f, 0, 2.55f), .42f, .78f, 31);
            }

            // Frame and feet behind the wardrobe screen, as in the original shop.
            var frame = StyleScreen + StyleScreenFacing * Vector3.forward * .12f;
            mesh.Box(frame, new Vector3(2.48f, 2.91f, .16f), F.Teal, .09f, true, StyleScreenFacing);
            for (int side = -1; side <= 1; side += 2)
            {
                var foot = frame + StyleScreenFacing * Vector3.right * (side * .95f);
                foot.y = CinemaFloor + .3f;
                mesh.Box(foot, new Vector3(.16f, .6f, .2f), F.Brass, .035f, true, StyleScreenFacing);
            }
            Accent("Style stage", At(0, 3.2f, .4f), new Color(1, .79f, .61f), 3, 6);
        }

        void HallOfFame()
        {
            var turn = FameTurn;

            // Winners' steps in front of the board. Solid, so players can hop up for a victory pose.
            // On each one a round piston head pivots back and forth, carrying the winner's statue and
            // three pop-up spotlights that light it (see MarinaFamePiston).
            F[] finishes = { F.Brass, F.Metal, F.Coral };
            for (int i = 0; i < FameSteps.Length; i++)
            {
                var step = FameSteps[i];
                var top = FamePodium + turn * step;
                mesh.Box(top - Vector3.up * (step.y * .5f), new Vector3(1.1f, step.y, 1.0f), F.CandyCream, .09f, true, turn);
                mesh.ArcSlab(top, Vector2.one * .49f, Vector2.one * .53f, .015f, 0, 360, F.Glow, false, 40);
                MarinaFamePiston.Build(visual, i, top, turn, finishes[i]);
            }
            Accent("Recent match", FamePodium + turn * new Vector3(0, 3.1f, -1.8f), new Color(1, .86f, .62f), 2.6f, 6.5f);
        }
    }
}
