using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class TycoonDeckVisual
    {
        // Children use metres even when their breakaway block is a scaled unit cube.
        TycoonDeckVisual Fitting(string label, Vector3 size)
        {
            var fitting = Detail(transform, label);
            fitting.transform.localPosition = Vector3.up * .5f;
            fitting.transform.localScale = new Vector3(1 / size.x, 1 / size.y, 1 / size.z);
            return fitting;
        }

        // Revolved band between rings, including tapered cups and the axle hardware.
        void Band(Vector3 a, Vector3 b, Vector3 axis, float radiusA, float radiusB, int material, int sides = 24)
        {
            var u = Vector3.Cross(axis, Mathf.Abs(axis.y) > .9f ? Vector3.right : Vector3.up).normalized;
            var v = Vector3.Cross(axis, u);
            Vector3 Radial(int i)
            {
                float angle = i * 2 * Mathf.PI / sides;
                return u * Mathf.Cos(angle) + v * Mathf.Sin(angle);
            }
            for (int i = 0; i < sides; i++)
            {
                var r = Radial(i);
                var next = Radial(i + 1);
                Quad(a + r * radiusA, a + next * radiusA, b + next * radiusB, b + r * radiusB, material);
            }
        }

        void Rod(Vector3 a, Vector3 b, float r, int material, int sides = 12)
        {
            var axis = (b - a).normalized;
            Band(a, b, axis, r, r, material, sides);
            Band(a, a, axis, 0, r, material, sides);
            Band(b, b, axis, r, 0, material, sides);
        }

        void BuildSharkSign(Vector3 size)
        {
            var bracket = Fitting("Shark sign brackets", size);
            foreach (float x in new[] { -4.9f, 6f })
            {
                var foot = new Vector3(x, 0, 1.45f);
                var shoulder = new Vector3(x, 6.6f, 5.45f);
                bracket.Rod(foot, shoulder, .24f, 2);
                bracket.Rod(foot + Vector3.back * 1.6f, shoulder, .17f, 2);
                bracket.Rod(shoulder, shoulder + Vector3.up * 3, .22f, 2);
            }
            bracket.FinishMesh(new[] { Cream, Mint, Cream, Coral });
            var face = Detail(bracket.transform, "Shark marquee");
            // The mouth centre in the artwork is one third of the way across.
            // Its opening aligns with the axle when viewed from the four slide lanes.
            var lowerLeft = new Vector3(-8.9f, 4.3f, 5.45f);
            var upperLeft = lowerLeft + Vector3.up * 13.5f;
            var span = Vector3.right * 27;
            face.Quad(lowerLeft, upperLeft, upperLeft + span, lowerLeft + span, 0);
            var print = Resources.Load<Material>("Tycoon Shark Sign");
            face.FinishMesh(new[] { print, print, print, print });
        }

        void BuildWheel(Vector3 size)
        {
            var palette = new[] { Resources.Load<Material>("Tycoon Gold"), Resources.Load<Material>("Tycoon Aqua"), Mint, Coral };
            var stand = Fitting("Pail wheel frame and inlet", size);
            var hub = new Vector3(0, -.55f, -1.75f);
            float ground = -size.y;
            foreach (int side in new[] { -1, 1 })
            {
                var foot = new Vector3(side * 3.6f, ground + .18f, .48f);
                stand.Rod(foot, new Vector3(0, hub.y, .48f), .30f, 1);
                stand.Slab(foot.x,ground,foot.z,1.38f,.19f,1.06f,1);
                foreach (float bolt in new[] { -.48f, .48f })
                    stand.Slab(foot.x+bolt,ground+.19f,foot.z,.12f,.09f,.12f,2);
            }
            stand.Rod(hub + Vector3.forward * 2.55f, hub + Vector3.back * .95f, .46f, 0);

            var pipe = new[] {
                new Vector3(6.3f, ground, .8f), new Vector3(6.3f, 4.8f, .8f),
                new Vector3(6.1f, 5.48f, .68f), new Vector3(5.55f, 5.85f, .45f),
                new Vector3(.9f, 5.85f, hub.z), new Vector3(.56f, 5.62f, hub.z),
                new Vector3(.48f, 5.3f, hub.z)
            };
            for (int i = 1; i < pipe.Length; i++)
                stand.Rod(pipe[i-1], pipe[i], .34f, 2);
            stand.Rod(pipe[0], pipe[0] + Vector3.up * .26f, .58f, 2);
            stand.Rod(pipe[0] + Vector3.up * 6, pipe[0] + Vector3.up * 6.23f, .45f, 2);
            stand.Rod(pipe[pipe.Length-1], new Vector3(.48f, 4.03f, hub.z), .17f, 1);
            for (int i = 0; i < 13; i++)
            {
                float x = -4.95f + i * .29f;
                stand.Rod(new Vector3(x, ground + .07f, hub.z - .82f),
                    new Vector3(x, ground + .07f, hub.z + .82f), .055f, 1);
            }
            stand.FinishMesh(palette);

            var rotor = Detail(stand.transform, "Six-pail wheel");
            rotor.transform.localPosition = hub;
            foreach (float z in new[] { -.57f, .57f })
            {
                var front = new Vector3(0, 0, z - .11f);
                var back = new Vector3(0, 0, z + .11f);
                var axis = Vector3.forward;
                rotor.Band(front, back, axis, 3.76f, 3.76f, 0, 48);
                rotor.Band(back, front, axis, 3.47f, 3.47f, 0, 48);
                rotor.Band(front, front, axis, 3.47f, 3.76f, 0, 48);
                rotor.Band(back, back, axis, 3.76f, 3.47f, 0, 48);
            }
            for (int i = 0; i < 6; i++)
            {
                var direction = Quaternion.AngleAxis(i * 60, Vector3.forward) * Vector3.up;
                rotor.Rod(Vector3.back * .56f, direction * 3.62f + Vector3.back * .56f, .18f, 0);
                var bottom = direction * 3.64f;
                var lip = direction * 4.68f;
                rotor.Band(bottom, lip, direction, .62f, 1.07f, 3);
                rotor.Band(lip, lip, direction, 1.07f, .92f, 0);
                rotor.Band(lip, bottom + direction * .16f, direction, .92f, .50f, 3);
                rotor.Band(bottom + direction * .16f, bottom + direction * .16f, direction, .50f, 0, 3);
                rotor.Band(bottom, bottom, direction, 0, .62f, 3);
            }
            rotor.Rod(Vector3.back * .97f, Vector3.forward * .72f, .65f, 0);
            rotor.FinishMesh(palette);
            var pour = Detail(stand.transform, "Water from tipping pail");
            pour.Band(Vector3.zero, Vector3.down, Vector3.down, .32f, .22f, 1);
            pour.FinishMesh(palette);
            rotor.gameObject.AddComponent<TycoonWheelMotion>().Initialize(pour.transform, ground);
        }
    }
}
