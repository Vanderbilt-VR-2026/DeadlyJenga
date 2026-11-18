using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DeadlyJenga.CastlePrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    // Checks for the compact candy cities (Candy Plaza Wheel, Candy Canyon, Candy Plaza Canyon):
    // 1. plan export for offline route analysis, 2. 10 s settle, 3. pull one tower floor and count what falls,
    // 4. full in-game captures. Report: Logs/Validation/CandyCityLayouts.txt
    // -executeMethod DeadlyJenga.DevTools.Editor.CandyCityLayoutChecks.Run
    public static class CandyCityLayoutChecks
    {
        static readonly (int design, string id)[] Maps =
        {
            (EpicCityLayout.PlazaWheelDesign, "epic-candy-plaza-wheel"),
            (EpicCityLayout.CandyCanyonDesign, "epic-candy-canyon"),
            (EpicCityLayout.PlazaCanyonDesign, "epic-candy-plaza-canyon"),
        };

        [Serializable] sealed class Shape { public int module; public Vector3 center, size; public Quaternion rotation; public int finish; public Vector3[] vertices; }
        [Serializable] sealed class Module { public int index, district; public string purpose; public Vector3 position; public float mass; }
        [Serializable] sealed class Site { public string name, kind; public Vector3 position; public float heading, width, depth; public int tiers; public Vector3 boundsMin, boundsMax; }
        [Serializable] sealed class Route { public string name; public int from, to; public Vector3 a, b; }
        [Serializable] sealed class Export
        {
            public int design; public Vector2 halfSize; public float gridStep;
            public List<Module> modules = new(); public List<Shape> shapes = new(); public List<Site> sites = new();
            public List<Route> routes = new(); public List<Vector3> spawns = new(); public List<Vector3> ground = new();
        }

        [MenuItem("Tools/Deadly Jenga/Maps/Candy City Layout Checks")]
        public static void Run()
        {
            var report = new StringBuilder("Candy city layout checks\n");
            string folder = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "Validation");
            Directory.CreateDirectory(folder);
            int failed = 0;
            try
            {
                foreach (var (design, id) in Maps)
                {
                    var plan = EpicCityLayout.Build(design);
                    File.WriteAllText(Path.Combine(folder, "CandyCity-" + id + ".json"), JsonUtility.ToJson(ExportPlan(design, plan)));
                    int groundOnly = plan.routes.Count(r => r.name.StartsWith("Ground street", StringComparison.Ordinal));
                    var b = MayhemStructureLayout.Bounds(plan.modules);
                    report.AppendLine($"{id}: {plan.modules.Count} bodies, {plan.modules.Sum(m => m.shapes.Count)} shapes, {plan.sites.Count} sites, " +
                                      $"{plan.routes.Count - groundOnly} bridges built, {groundOnly} links fell back to ground, island {plan.halfSize.x * 2:0}x{plan.halfSize.y * 2:0} m, top {b.max.y:0.0} m");
                    if (groundOnly > 0) report.AppendLine("  ground fallbacks: " + string.Join("; ", plan.routes.Where(r => r.name.StartsWith("Ground street", StringComparison.Ordinal)).Select(r => r.name)));
                    failed += SettleAndPull(design, id, report);
                }
            }
            catch (Exception e) { report.AppendLine("ERROR " + e); failed++; Debug.LogException(e); }
            report.AppendLine(failed == 0 ? "STATIC CHECKS PASSED (captures follow)" : failed + " PROBLEMS");
            File.WriteAllText(Path.Combine(folder, "CandyCityLayouts.txt"), report.ToString());
            Debug.Log(report.ToString());
            // Captures run in Play Mode and exit Unity when done.
            MapLiveCapture.BatchMaps(Maps.Select(m => m.id).ToList(), Path.Combine(Directory.GetCurrentDirectory(), "Logs", "Captures", "CandyCityLayouts"));
        }

        static Export ExportPlan(int design, EpicCityPlan plan)
        {
            var e = new Export { design = design, halfSize = plan.halfSize, gridStep = 4 };
            for (int i = 0; i < plan.modules.Count; i++)
            {
                var m = plan.modules[i];
                e.modules.Add(new Module { index = i, district = m.district, purpose = m.purpose, position = m.position, mass = m.mass });
                foreach (var s in m.shapes)
                {
                    var shape = new Shape { module = i, center = m.position + m.rotation * s.center, size = s.size, rotation = m.rotation * s.rotation, finish = s.finish };
                    if (s.vertices != null) shape.vertices = s.vertices.Select(v => m.position + m.rotation * (s.center + s.rotation * v)).ToArray();
                    e.shapes.Add(shape);
                }
            }
            foreach (var s in plan.sites) e.sites.Add(new Site { name = s.name, kind = s.kind, position = s.position, heading = s.heading, width = s.width, depth = s.depth, tiers = s.tiers, boundsMin = s.bounds.min, boundsMax = s.bounds.max });
            foreach (var r in plan.routes) e.routes.Add(new Route { name = r.name, from = r.from, to = r.to, a = r.a, b = r.b });
            e.spawns.AddRange(plan.spawnPoints);
            for (float z = -plan.halfSize.y; z <= plan.halfSize.y; z += e.gridStep)
                for (float x = -plan.halfSize.x; x <= plan.halfSize.x; x += e.gridStep)
                    e.ground.Add(new Vector3(x, EpicCityLayout.GroundHeight(plan, new Vector3(x, 0, z)), z));
            return e;
        }

        // Builds the real map in an empty scene, lets it settle, then shoves one middle floor of the tallest tower.
        static int SettleAndPull(int design, string id, StringBuilder report)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefab = Resources.Load<GameObject>("ModeMaps/Mode_" + id);
            if (!prefab) { report.AppendLine("  FAIL missing prefab"); return 1; }
            var go = Object.Instantiate(prefab);
            var mode = Physics.simulationMode;
            try
            {
                var map = go.GetComponent<EpicCityMap>();
                map.Rebuild();
                Physics.simulationMode = SimulationMode.Script;
                var bodies = map.Blocks.ToList();
                var start = bodies.Select(b => b.position).ToArray();
                for (int i = 0; i < 500; i++) Physics.Simulate(.02f);
                var drift = bodies.Select((b, i) => Vector3.Distance(b.position, start[i])).ToArray();
                int moved = drift.Count(d => d > .25f), asleep = bodies.Count(b => b.IsSleeping());
                report.AppendLine($"  settle 10 s: max drift {drift.Max():0.00} m, {moved} bodies moved > .25 m, {asleep}/{bodies.Count} asleep");
                int problems = moved > bodies.Count / 20 ? 1 : 0;
                if (problems > 0) foreach (var i in Enumerable.Range(0, bodies.Count).Where(i => drift[i] > .25f).Take(8)) report.AppendLine("    drifted: " + bodies[i].name + $" {drift[i]:0.00} m");

                // Pull test: drag the tallest Jenga tower's middle floor sideways at 4 m/s for 5 s (a sustained Hand pull).
                string Purpose(Rigidbody r) => r.GetComponent<MayhemStructuralMember>().Purpose;
                var floors = bodies.Where(b => Purpose(b).Contains(" floor ")).ToList();
                var tower = floors.GroupBy(b => Purpose(b).Substring(0, Purpose(b).LastIndexOf(" floor ", StringComparison.Ordinal))).OrderByDescending(g => g.Count()).First().OrderBy(b => b.position.y).ToList();
                var settled = bodies.Select(b => b.position).ToArray();
                var target = tower[tower.Count / 2];
                var above = tower.Where(b => b.position.y > target.position.y + .5f).ToList();
                var before = above.Select(b => b.position).ToArray();
                var from = target.position;
                for (int i = 0; i < 250; i++) { var v = target.linearVelocity; target.linearVelocity = new Vector3(4, v.y, 0); Physics.Simulate(.02f); }
                for (int i = 0; i < 300; i++) Physics.Simulate(.02f);
                float dragged = Vector3.ProjectOnPlane(target.position - from, Vector3.up).magnitude;
                int fell = above.Where((b, i) => before[i].y - b.position.y > 2).Count();
                int collateral = bodies.Where((b, i) => !tower.Contains(b) && Vector3.Distance(b.position, settled[i]) > 1).Count();
                report.AppendLine($"  pull: dragged '{target.GetComponent<MayhemStructuralMember>().Purpose}' ({target.mass:0} kg) {dragged:0.0} m; {fell}/{above.Count} floors above it dropped > 2 m; {collateral} other bodies disturbed > 1 m");
                return problems;
            }
            finally
            {
                Physics.simulationMode = mode;
                Object.DestroyImmediate(go);
            }
        }
    }
}
