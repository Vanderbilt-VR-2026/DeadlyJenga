using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Modes;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DeadlyJenga.DevTools.Editor
{
    // Route connectivity and clearance inside the play area (Stage 5 validation, Christmas Candy Town style):
    // 1. A standing grid (1.5 m cells) over the play area on the real colliders: a cell is standable when its top
    //    surface is flat enough and a survivor capsule fits; neighbours connect when the height step is at most a
    //    jump and a capsule sweep between them is clear. Every map spawn, match spawn, flag and the hill must reach
    //    the play-area center.
    // 2. Doors and steps: the walk route (unity_map.json "route") inside the play area is swept leg by leg with a
    //    survivor capsule and sampled every 0.5 m for step heights.
    // Towers are not in the edit-mode prefab (they are built in Play Mode); their plinths are treated as blocked.
    public static partial class VanderbiltCampusChecks
    {
        const float Cell = 1.5f, CapsuleRadius = .3f, Knee = .4f, Head = 1.7f, StepJump = 1.2f, StepWalk = .5f;
        const int RouteMask = (1 << Layers.Surface) | 1;

        [MenuItem("Tools/Deadly Jenga/Validation/Vanderbilt Campus Routes")]
        public static void RoutesMenu() => Debug.Log("Campus routes " + (RunRoutes() ? "passed" : "failed") + ": " + ProjectPath.Report("VanderbiltCampusRoutes.txt"));

        public static void RoutesBatch()
        {
            bool ok = false;
            try { ok = RunRoutes(); }
            catch (Exception e) { File.AppendAllText(ProjectPath.Report("VanderbiltCampusRoutes.txt"), "FAIL " + e + "\n"); }
            VanderbiltCampusData.Exit(ok ? 0 : 1);
        }

        public static bool RunRoutes() => RunSuite("VanderbiltCampusRoutes.txt", "Vanderbilt campus route connectivity and clearance", (root, map, data, scene) =>
        {
            var grid = new StandGrid(map);
            report.AppendLine($"NOTE standing grid: {grid.Standable} standable cells of {grid.Total}, cell {Cell} m, jump step {StepJump} m.");
            var start = grid.Nearest(map.Center);
            Check(start >= 0, "Play-area center has a standable cell nearby");
            if (start < 0) return;
            var reached = grid.Flood(start);

            var targets = new List<(string name, Vector3 world)>();
            foreach (var s in map.spawns) if (s) targets.Add(("map spawn " + s.name, s.position));
            var layout = Resources.LoadAll<MapMatchLayout>("MapMatchLayouts").FirstOrDefault(l => l.layoutSignature == map.SaveLayout());
            Check(layout, "Campus match layout matches the map's layout token " + map.SaveLayout());
            if (layout)
            {
                foreach (var p in layout.freeSpawns) targets.Add(("match spawn", map.transform.TransformPoint(p)));
                foreach (var t in layout.teams)
                {
                    targets.Add(("flag " + t.name, map.transform.TransformPoint(t.flagHome)));
                    foreach (var p in t.spawns) targets.Add(("team " + t.name + " spawn", map.transform.TransformPoint(p)));
                }
                targets.Add(("hill", map.transform.TransformPoint(layout.hill)));
            }
            foreach (var (name, world) in targets)
            {
                int cell = grid.Nearest(world);
                bool ok = cell >= 0 && reached.Contains(cell);
                Check(ok, $"Route from {name} at {map.transform.InverseTransformPoint(world):F1} to the play-area center" + (cell < 0 ? " (no standable cell within 3 m)" : ""));
            }
            RouteLegs(map, data);
        });

        // Capsule sweeps and step heights along the walk route, inside the play area only.
        static void RouteLegs(VanderbiltCampusMap map, VanderbiltCampusData data)
        {
            var route = data.route.Select(p => map.transform.TransformPoint(p)).Where(p => map.InPlayArea(p)).ToList();
            if (route.Count < 2) { report.AppendLine("NOTE fewer than 2 route waypoints inside the play area; door/step sweep skipped."); return; }
            int blocked = 0, tall = 0, jumps = 0;
            for (int i = 1; i < route.Count; i++)
            {
                var a = route[i - 1]; var b = route[i];
                float length = Vector3.Distance(new Vector3(a.x, 0, a.z), new Vector3(b.x, 0, b.z));
                float previous = float.NaN;
                for (float d = 0; d <= length; d += .5f)
                {
                    var p = Vector3.Lerp(a, b, length > 0 ? d / length : 0);
                    var local = map.transform.InverseTransformPoint(p);
                    if (!VanderbiltCampusBuilder.GroundAt(map.transform, new Vector2(local.x, local.z), out float ground)) continue;
                    if (!float.IsNaN(previous))
                    {
                        float step = Mathf.Abs(ground - previous);
                        if (step > 1.8f) { tall++; report.AppendLine($"  step {step:F2} m (taller than a jump) on leg {i} at {local:F1}"); }
                        else if (step > StepWalk) jumps++;
                    }
                    previous = ground;
                    var feet = map.transform.TransformPoint(new Vector3(local.x, ground, local.z));
                    if (Physics.CheckCapsule(feet + Vector3.up * (Knee + CapsuleRadius), feet + Vector3.up * (Head - CapsuleRadius), CapsuleRadius, RouteMask, QueryTriggerInteraction.Ignore))
                    {
                        var hit = Physics.OverlapCapsule(feet + Vector3.up * (Knee + CapsuleRadius), feet + Vector3.up * (Head - CapsuleRadius), CapsuleRadius, RouteMask, QueryTriggerInteraction.Ignore).FirstOrDefault();
                        blocked++;
                        if (blocked <= 20) report.AppendLine($"  capsule blocked on leg {i} at {local:F1} by '{(hit ? hit.name : "?")}'");
                        break;   // one line per leg
                    }
                }
            }
            Check(blocked == 0, $"Walk route inside the play area ({route.Count} waypoints): survivor capsule clear through doors and gaps ({blocked} blocked legs)");
            Check(tall == 0, $"Walk route steps are all jumpable ({tall} steps over 1.8 m; {jumps} samples need a hop over {StepWalk} m)");
        }

        // A 2D grid of standing points over the play area, built from physics queries.
        sealed class StandGrid
        {
            readonly VanderbiltCampusMap map;
            readonly int size;
            readonly float[] height;   // world y of the standing surface, NaN when not standable
            readonly Vector2 origin;   // local x/z of cell (0, 0)
            public int Total => height.Length;
            public int Standable => height.Count(h => !float.IsNaN(h));

            public StandGrid(VanderbiltCampusMap map)
            {
                this.map = map;
                size = Mathf.CeilToInt(map.playAreaRadius * 2 / Cell) + 1;
                origin = new Vector2(map.playAreaCenter.x - map.playAreaRadius, map.playAreaCenter.z - map.playAreaRadius);
                height = new float[size * size];
                for (int z = 0; z < size; z++)
                    for (int x = 0; x < size; x++) height[z * size + x] = Stand(x, z);
            }

            Vector3 World(int x, int z, float y) => map.transform.TransformPoint(new Vector3(origin.x + x * Cell, y, origin.y + z * Cell));

            float Stand(int x, int z)
            {
                var local = new Vector2(origin.x + x * Cell, origin.y + z * Cell);
                if ((local - new Vector2(map.playAreaCenter.x, map.playAreaCenter.z)).magnitude > map.playAreaRadius) return float.NaN;
                foreach (var t in map.towers)
                    if ((local - new Vector2(t.position.x, t.position.z)).magnitude < map.HalfWidth(t)) return float.NaN;
                var top = World(x, z, map.playAreaTop + 20);
                if (!Physics.Raycast(top, Vector3.down, out var hit, map.playAreaTop - map.lowestGround + 60, RouteMask, QueryTriggerInteraction.Ignore)) return float.NaN;
                if (hit.normal.y < .7f) return float.NaN;
                var feet = hit.point;
                if (Physics.CheckCapsule(feet + Vector3.up * (Knee + CapsuleRadius), feet + Vector3.up * (Head - CapsuleRadius), CapsuleRadius, RouteMask, QueryTriggerInteraction.Ignore)) return float.NaN;
                return feet.y;
            }

            public int Nearest(Vector3 world)
            {
                var local = map.transform.InverseTransformPoint(world);
                int cx = Mathf.RoundToInt((local.x - origin.x) / Cell), cz = Mathf.RoundToInt((local.z - origin.y) / Cell);
                int best = -1; float bestDistance = float.MaxValue;
                for (int dz = -2; dz <= 2; dz++)
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        int x = cx + dx, z = cz + dz;
                        if (x < 0 || z < 0 || x >= size || z >= size || float.IsNaN(height[z * size + x])) continue;
                        float d = (World(x, z, height[z * size + x]) - world).sqrMagnitude;
                        if (d < bestDistance) { bestDistance = d; best = z * size + x; }
                    }
                return best;
            }

            // Breadth-first search over connected cells.
            public HashSet<int> Flood(int start)
            {
                var seen = new HashSet<int> { start }; var queue = new Queue<int>(); queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    int i = queue.Dequeue(); int x = i % size, z = i / size;
                    foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        int nx = x + dx, nz = z + dz, n = nz * size + nx;
                        if (nx < 0 || nz < 0 || nx >= size || nz >= size || seen.Contains(n) || float.IsNaN(height[n])) continue;
                        if (Mathf.Abs(height[n] - height[i]) > StepJump) continue;
                        var a = World(x, z, height[i]); var b = World(nx, nz, height[n]);
                        var lift = Vector3.up * (Mathf.Max(height[i], height[n]) - a.y + Knee + CapsuleRadius);
                        if (Physics.CapsuleCast(a + lift, a + Vector3.up * (Head - CapsuleRadius + Mathf.Max(0, height[n] - height[i])), CapsuleRadius,
                            (b - a).normalized, Cell, RouteMask, QueryTriggerInteraction.Ignore)) continue;
                        seen.Add(n); queue.Enqueue(n);
                    }
                }
                return seen;
            }
        }
    }
}
