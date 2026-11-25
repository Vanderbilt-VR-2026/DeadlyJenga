using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Blender structure pass 05: a raised, wadeable lazy river loops around the base on stilts.
    // A family raft slide drops from the bleacher top into it, and two ramps lead down from the boardwalk.
    public static partial class TycoonLayout
    {
        public const float RiverFloor = 3.6f, RiverWidth = 6.2f;
        public static float RiverWallTop => RiverFloor + TycoonDeckVisual.RiverWallHeight;

        // Centre line of the loop in map metres (x, z), traced from the Blender layout.
        static readonly Vector2[] RiverGuide =
        {
            new(34,-49), new(0,-50), new(-34,-49), new(-41,-47), new(-47,-42), new(-50.5f,-35), new(-53,-27),
            new(-54.5f,-18), new(-55,-5), new(-54,6), new(-51,16), new(-46,25), new(-40,31.5f), new(-30,34.5f),
            new(-15,36), new(0,36.5f), new(15,36), new(30,35), new(40,32), new(46,27), new(49.5f,18),
            new(51,8), new(52,-4), new(52.5f,-16), new(52,-28), new(50.5f,-37), new(47,-43.5f), new(41,-48),
        };

        static void WaterPark(List<CastlePlacement> plan)
        {
            var river = RiverPath();
            RiverLoop(plan, river);
            // The raft slide ends just above the water, clear of the trough wall.
            float splash = RiverWallTop + .6f;
            MoldedRoute(plan, CastleSection.Corner, 4.2f, TycoonDeckFinish.Flume, false,
                P(-32.6f,26.1f,-2), P(-35.5f,25.5f,-2.4f), P(-39.5f,22.1f,-4), P(-43.5f,16.6f,-7),
                P(-47,11.6f,-10.5f), P(-50,7.3f,-14.5f), P(-52.6f,splash+.45f,-18), P(-54.2f,splash,-20.5f));
            foreach (var p in new[] { P(-43.5f,16.6f,-7), P(-47,11.6f,-10.5f), P(-50,7.3f,-14.5f) })
                Pier(plan, CastleSection.Corner, p.x, p.y - 1f, p.z, 1.8f);
            RiverRamp(plan, river, P(-32,7,-42.8f), new Vector2(-41,-47));
            RiverRamp(plan, river, P(32,7,-42.8f), new Vector2(43,-47));
        }

        // Closed, rounded loop resampled into panels about 7 m long.
        public static List<Vector3> RiverPath()
        {
            var ring = new List<Vector3>();
            foreach (var g in RiverGuide) ring.Add(P(g.x, RiverFloor, g.y));
            for (int pass = 0; pass < 3; pass++)
            {
                var smooth = new List<Vector3>();
                for (int i = 0; i < ring.Count; i++)
                {
                    var a = ring[i]; var b = ring[(i + 1) % ring.Count];
                    smooth.Add(Vector3.Lerp(a, b, .25f));
                    smooth.Add(Vector3.Lerp(a, b, .75f));
                }
                ring = smooth;
            }
            float total = 0;
            for (int i = 0; i < ring.Count; i++) total += Vector3.Distance(ring[i], ring[(i + 1) % ring.Count]);
            int count = Mathf.RoundToInt(total / 7f);
            float step = total / count, segmentStart = 0;
            int segment = 0;
            var even = new List<Vector3>(count);
            for (int k = 0; k < count; k++)
            {
                float target = k * step;
                float length = Vector3.Distance(ring[segment], ring[(segment + 1) % ring.Count]);
                while (segmentStart + length < target)
                {
                    segmentStart += length; segment++;
                    length = Vector3.Distance(ring[segment], ring[(segment + 1) % ring.Count]);
                }
                even.Add(Vector3.Lerp(ring[segment], ring[(segment + 1) % ring.Count], (target - segmentStart) / length));
            }
            return even;
        }

        static void RiverLoop(List<CastlePlacement> plan, List<Vector3> ring)
        {
            int n = ring.Count;
            var edges = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                var a = Vector3.Cross(Vector3.up, ring[i] - ring[(i - 1 + n) % n]).normalized;
                var b = Vector3.Cross(Vector3.up, ring[(i + 1) % n] - ring[i]).normalized;
                var miter = (a + b).normalized;
                edges[i] = miter * (RiverWidth * .5f / Mathf.Max(.75f, Vector3.Dot(miter, b)));
            }
            // The pump house stands on the rear panel, on the seaward side.
            int pump = 0;
            for (int i = 1; i < n; i++)
                if (Flat((ring[i] + ring[(i + 1) % n]) * .5f - P(0, 0, 40)).sqrMagnitude <
                    Flat((ring[pump] + ring[(pump + 1) % n]) * .5f - P(0, 0, 40)).sqrMagnitude) pump = i;
            for (int i = 0; i < n; i++)
            {
                var a = ring[i]; var b = ring[(i + 1) % n];
                var ea = edges[i]; var eb = edges[(i + 1) % n];
                var finish = i == pump ? TycoonDeckFinish.RiverPump : TycoonDeckFinish.River;
                var openings = TycoonRailOpening.None;
                if (i == pump)
                {
                    var mid = (a + b) * .5f;
                    openings = Flat(mid - (ea + eb) * .5f).magnitude > Flat(mid).magnitude ? TycoonRailOpening.Front : TycoonRailOpening.Rear;
                }
                AddPanel(plan, CastleSection.Aqua, RiverWidth, finish, a - ea, a + ea, b - eb, b + eb, openings);
                Pier(plan, CastleSection.Wall, a.x, RiverFloor - .6f, a.z, 2.2f);
            }
        }

        // A boardwalk ramp lands on the inner trough wall nearest the target.
        static void RiverRamp(List<CastlePlacement> plan, List<Vector3> ring, Vector3 start, Vector2 target)
        {
            int n = ring.Count, best = 0;
            for (int i = 1; i < n; i++)
                if (Flat(ring[i] - P(target.x, 0, target.y)).sqrMagnitude < Flat(ring[best] - P(target.x, 0, target.y)).sqrMagnitude) best = i;
            var p = ring[best];
            var across = Vector3.Cross(Vector3.up, ring[(best + 1) % n] - ring[(best - 1 + n) % n]).normalized;
            if (Vector3.Dot(across, Flat(start - p)) < 0) across = -across;
            var end = p + across * (RiverWidth * .5f - TycoonDeckVisual.RiverWallThickness * .5f);
            end.y = RiverWallTop;
            Walk(plan, 3, start, end);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);
    }
}
