using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // The raised lazy river: a tiled trough with low walls and shallow, wadeable water.
    public sealed partial class TycoonDeckVisual
    {
        public const float RiverWallHeight = .75f, RiverWallThickness = .6f, RiverWaterDepth = .55f;
        public const string RiverWaterName = "River water";

        static Vector3 Across(TycoonDeckProfile p, float across, float along) =>
            Vector3.Lerp(Vector3.Lerp(p.StartLeft, p.StartRight, across), Vector3.Lerp(p.EndLeft, p.EndRight, across), along);

        void RiverTrough(TycoonDeckProfile p, Vector3 size)
        {
            float wall = RiverWallThickness / size.x;
            var top = vertical * RiverWallHeight;
            var capTop = vertical * (RiverWallHeight + .1f);
            foreach (int side in new[] { 0, 1 })
            {
                float lo = side == 0 ? 0 : 1 - wall, hi = side == 0 ? wall : 1;
                Prism(Across(p, lo, 0) + top, Across(p, hi, 0) + top, Across(p, lo, 1) + top, Across(p, hi, 1) + top,
                    RiverWallHeight / size.y, 2);
                Prism(Across(p, lo, 0) + capTop, Across(p, hi, 0) + capTop, Across(p, lo, 1) + capTop, Across(p, hi, 1) + capTop,
                    .1f / size.y, 3);
            }
            // Blue tile bands across the floor show the panel seams under the water.
            int bands = Mathf.Max(1, Mathf.RoundToInt(size.z / 1.75f));
            var lift = vertical * .012f;
            for (int j = 0; j < bands; j++)
            {
                float t = (j + .5f) / bands, half = .07f / size.z;
                Prism(Across(p, wall, t - half) + lift, Across(p, 1 - wall, t - half) + lift,
                    Across(p, wall, t + half) + lift, Across(p, 1 - wall, t + half) + lift, .01f / size.y, 1);
            }
            if (p.Finish != TycoonDeckFinish.RiverPump) return;
            // Pump house outside the seaward wall. Openings.Front puts it on the left side.
            float s = (p.Openings & TycoonRailOpening.Front) != 0 ? -1 : 1;
            float half_ = size.x * .5f;
            Slab(s * (half_ + 1.0f), -.6f, 0, 1.8f, 2.6f, 2.6f, 2);
            Slab(s * (half_ + 1.0f), 2.0f, 0, 2.2f, .22f, 3.0f, 3);
            Slab(s * (half_ - .3f), 1.05f, 0, 1.9f, .32f, .5f, 2);
        }

        void BuildRiverWater(TycoonDeckProfile p, Material[] palette)
        {
            var size = dimensions;
            float wall = RiverWallThickness / size.x;
            var water = Detail(transform, RiverWaterName);
            var lift = vertical * RiverWaterDepth;
            water.Quad(Across(p, wall, 0) + lift, Across(p, wall, 1) + lift, Across(p, 1 - wall, 1) + lift, Across(p, 1 - wall, 0) + lift, 1);
            // A few foam flecks drift on the surface.
            for (int i = 0; i < 5; i++)
            {
                float u = .25f + .5f * Mathf.Repeat(i * .618f, 1), v = (i + .5f) / 5;
                var c = Across(p, u, v) + lift + vertical * .01f;
                var dx = Vector3.right * (.28f / size.x); var dz = Vector3.forward * (.12f / size.z);
                water.Quad(c - dx - dz, c - dx + dz, c + dx + dz, c + dx - dz, 2);
            }
            if (p.Finish == TycoonDeckFinish.RiverPump)
            {
                float s = (p.Openings & TycoonRailOpening.Front) != 0 ? -1 : 1;
                float x = size.x * .5f - .95f;
                var a = LocalPoint(s * (x + .1f), 1.1f, -.24f); var b = LocalPoint(s * (x + .1f), 1.1f, .24f);
                var c = LocalPoint(s * (x - .25f), RiverWaterDepth, .3f); var d = LocalPoint(s * (x - .25f), RiverWaterDepth, -.3f);
                water.Quad(a, b, c, d, 1); water.Quad(d, c, b, a, 1);
            }
            water.FinishMesh(palette);
        }

        // Low trough walls block survivors like the floor does. Called once the surface proxy exists.
        internal static void AddRiverWalls(CastleBlock block, TycoonDeckProfile p, Vector3 size)
        {
            var frame = block.CreateTraversalFrame();
            if (!frame) return;
            foreach (int side in new[] { 0, 1 })
            {
                float across = side == 0 ? RiverWallThickness * .5f / size.x : 1 - RiverWallThickness * .5f / size.x;
                var a = Vector3.Scale(Vector3.Lerp(p.StartLeft, p.StartRight, across), size);
                var b = Vector3.Scale(Vector3.Lerp(p.EndLeft, p.EndRight, across), size);
                var wall = new GameObject("River wall").AddComponent<BoxCollider>();
                wall.transform.SetParent(frame, false);
                wall.transform.localPosition = (a + b) * .5f + Vector3.up * RiverWallHeight * .5f;
                wall.transform.localRotation = Quaternion.LookRotation(b - a, Vector3.up);
                wall.size = new Vector3(RiverWallThickness, RiverWallHeight, (b - a).magnitude);
            }
            block.RegisterTraversalShapes(frame);
        }
    }
}
