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
            (EpicCityLayout.MechanismCityDesign, "epic-candy-mechanism-city"),
            (EpicCityLayout.SkyRingCityDesign, "epic-candy-sky-ring-city"),
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
        public static void Run() => RunMaps(Maps);

        // -executeMethod DeadlyJenga.DevTools.Editor.CandyCityLayoutChecks.RunSkyRing (only the newest map)
        public static void RunSkyRing() => RunMaps(Maps.Where(m => m.design == EpicCityLayout.SkyRingCityDesign).ToArray());

        // -executeMethod DeadlyJenga.DevTools.Editor.CandyCityLayoutChecks.RunSkyRingTour
        // Player-eye pictures all over Candy Sky Ring City (streets, roofs, bridges, inside a tower) plus wide looks
        // across the whole map from the high points. No checks; pictures only.
        public static void RunSkyRingTour()
        {
            // name, stand near (x, y, z), look at (x, y, z); metres from the map center, y = rough floor height.
            var shots = new (string name, Vector3 at, Vector3 look)[]
            {
                ("street-01-se-crosswalk", new Vector3(40, .5f, -40), new Vector3(0, 8, 0)),
                ("street-02-plaza-vault", new Vector3(0, .5f, -21), new Vector3(0, 12, 8)),
                ("street-03-under-sky-bridge", new Vector3(-76, .5f, -12), new Vector3(-60, 28, -10)),
                ("street-04-sugar-market", new Vector3(-28, .5f, 30), new Vector3(-51, 5, 51)),
                ("street-05-harbor-drawbridge", new Vector3(35, .5f, -63), new Vector3(46, 5, -79)),
                ("street-06-downtown-base", new Vector3(35, .5f, 35), new Vector3(56, 30, 56)),
                ("street-07-avenue-to-keep", new Vector3(-20, .5f, -40), new Vector3(-59, 20, -59)),
                ("street-08-north-ring-road", new Vector3(13, .5f, 48), new Vector3(0, 8, -20)),
                ("street-09-east-shelter", new Vector3(60, .5f, 5), new Vector3(-20, 8, 5)),
                ("street-10-island-edge", new Vector3(-10, .5f, -92), new Vector3(0, 15, 0)),
                ("inside-01-canyon-tower", new Vector3(27, 5.6f, 17), new Vector3(-10, 6, 17)),
                ("roof-01-sugar-annex-sky-bridge", new Vector3(-66, 29.6f, 12), new Vector3(-56, 30, -28)),
                ("roof-02-harbor-block", new Vector3(48, 13.6f, -48), new Vector3(0, 8, 0)),
                ("roof-03-sugar-hall", new Vector3(-48, 9.6f, 48), new Vector3(0, 8, 0)),
                ("roof-04-vault-fountain", new Vector3(8, 13.6f, -8), new Vector3(0, 14, 2)),
                ("bridge-01-harbor-to-downtown", new Vector3(75, 21.6f, 16), new Vector3(67, 25, 46)),
                ("bridge-02-plaza-diagonal", new Vector3(7, 21.6f, 1.5f), new Vector3(34, 21, -10)),
                ("wide-01-from-downtown-top", new Vector3(56, 47, 56), new Vector3(-40, 0, -40)),
                ("wide-02-from-keep-top", new Vector3(-59, 37.5f, -59), new Vector3(56, 12, 56)),
                ("wide-03-from-canyon-se-roof", new Vector3(34, 29.6f, -17), new Vector3(-40, 5, 40)),
                ("wide-04-from-canyon-nw-roof", new Vector3(-20, 25.6f, 17), new Vector3(40, 5, -40)),
                ("wide-05-harbor-bridge-look-back", new Vector3(75, 21.6f, 16), new Vector3(-20, 6, -20)),
                ("wide-06-sugar-annex-over-city", new Vector3(-66, 29.6f, 12), new Vector3(40, 5, -10)),
            };
            string F(float v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var views = shots.Select(s => $"pov|{s.name}|{F(s.at.x)}|{F(s.at.y)}|{F(s.at.z)}|{F(s.look.x)}|{F(s.look.y)}|{F(s.look.z)}").ToArray();
            MapLiveCapture.BatchMaps(new List<string> { "epic-candy-sky-ring-city" }, Path.Combine(Directory.GetCurrentDirectory(), "Logs", "Captures", "SkyRingTour"), views);
        }

        // -executeMethod DeadlyJenga.DevTools.Editor.CandyCityLayoutChecks.RunSkyRingSkyline
        // Player-eye pictures looking OUT from Candy Sky Ring City at the distant cloud cities and the rainbow,
        // with the survivor camera's real 700 m view distance (anything farther is cut off in the game too).
        public static void RunSkyRingSkyline()
        {
            var shots = new (string name, Vector3 at, Vector3 look)[]
            {
                ("sky-01-north-edge", new Vector3(0, .5f, 85), new Vector3(0, 40, 500)),
                ("sky-02-south-edge-across-city", new Vector3(0, .5f, -90), new Vector3(0, 60, 500)),
                ("sky-03-east-edge", new Vector3(95, .5f, 0), new Vector3(500, 30, 0)),
                ("sky-04-west-edge", new Vector3(-95, .5f, 0), new Vector3(-500, 30, 0)),
                ("sky-05-downtown-top-nw", new Vector3(56, 47, 56), new Vector3(-300, 40, 400)),
                ("sky-06-keep-top-south", new Vector3(-59, 37.5f, -59), new Vector3(-200, 30, -500)),
                ("sky-07-plaza-up", new Vector3(0, .5f, -21), new Vector3(0, 70, 300)),
                ("sky-08-harbor-roof-se", new Vector3(48, 13.6f, -48), new Vector3(400, 40, -300)),
                ("sky-09-sw-edge", new Vector3(-70, .5f, -70), new Vector3(-400, 30, -400)),
                ("sky-10-ne-edge", new Vector3(70, .5f, 70), new Vector3(400, 30, 400)),
                ("sky-11-island-above-from-west-edge", new Vector3(-95, .5f, 0), new Vector3(-122, 85, 70)),
                ("sky-12-hamlet-waterfall", new Vector3(80, .5f, 20), new Vector3(315, -10, 127)),
                ("sky-13-island-above-from-downtown-top", new Vector3(56, 47, 56), new Vector3(-125, 60, 72)),
                ("sky-14-island-above-from-keep-top", new Vector3(-59, 37.5f, -59), new Vector3(-112, 45, 66)),
            };
            string F(float v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var views = shots.Select(s => $"pov|{s.name}|{F(s.at.x)}|{F(s.at.y)}|{F(s.at.z)}|{F(s.look.x)}|{F(s.look.y)}|{F(s.look.z)}").ToArray();
            MapLiveCapture.PlayerEyeFar = 700; // the batch runs over later editor frames and then quits Unity
            MapLiveCapture.BatchMaps(new List<string> { "epic-candy-sky-ring-city" }, Path.Combine(Directory.GetCurrentDirectory(), "Logs", "Captures", "SkyRingSkyline"), views);
        }

        static void RunMaps((int design, string id)[] maps)
        {
            var report = new StringBuilder("Candy city layout checks\n");
            string folder = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "Validation");
            Directory.CreateDirectory(folder);
            int failed = 0;
            try
            {
                foreach (var (design, id) in maps)
                {
                    var plan = EpicCityLayout.Build(design);
                    File.WriteAllText(Path.Combine(folder, "CandyCity-" + id + ".json"), JsonUtility.ToJson(ExportPlan(design, plan)));
                    int groundOnly = plan.routes.Count(r => r.name.StartsWith("Ground street", StringComparison.Ordinal));
                    var b = MayhemStructureLayout.Bounds(plan.modules);
                    report.AppendLine($"{id}: {plan.modules.Count} bodies, {plan.modules.Sum(m => m.shapes.Count)} shapes, {plan.sites.Count} sites, " +
                                      $"{plan.routes.Count - groundOnly} bridges built, {groundOnly} links fell back to ground, island {plan.halfSize.x * 2:0}x{plan.halfSize.y * 2:0} m, top {b.max.y:0.0} m");
                    if (groundOnly > 0) report.AppendLine("  ground fallbacks: " + string.Join("; ", plan.routes.Where(r => r.name.StartsWith("Ground street", StringComparison.Ordinal)).Select(r => r.name)));
                    failed += SettleAndPull(design, id, report);
                    if (design >= EpicCityLayout.MechanismCityDesign) failed += Mechanisms(id, report);
                }
            }
            catch (Exception e) { report.AppendLine("ERROR " + e); failed++; Debug.LogException(e); }
            report.AppendLine(failed == 0 ? "STATIC CHECKS PASSED (captures follow)" : failed + " PROBLEMS");
            File.WriteAllText(Path.Combine(folder, "CandyCityLayouts.txt"), report.ToString());
            Debug.Log(report.ToString());
            // Captures run in Play Mode and exit Unity when done.
            var views = maps.Length == 1 && maps[0].design == EpicCityLayout.SkyRingCityDesign
                ? new[] { "reference", "target", "close", "pov|market|36|0.5|-36|53|3|-53",
                    "pov|rainworks|44|0.5|-30|27|14|-17", "pov|school|-52|1|12|-71|12|28",
                    "pov|apartments|-36|0.5|-37|-60|12|-60", "pov|substation|85|0.5|24|68|21|45",
                    "pov|switchback|61|16.4|51|70|24|43",
                    "pov|living-room|-50.77|1.15|-72.27|-53.81|2.35|-64.13",
                    "pov|classroom|-54.64|1.15|41.04|-61.49|2.55|47.95",
                    "pov|produce|49.78|1.15|-48.37|55.79|2.6|-44.9",
                    "pov|cafe|20.6|1.15|-63.74|26.01|2.45|-66.95",
                    "pov|pump-room|33.5|1.15|-15|27|2.7|-10.9" }
                : MapPictureRig.AllViews;
            MapLiveCapture.BatchMaps(maps.Select(m => m.id).ToList(), Path.Combine(Directory.GetCurrentDirectory(), "Logs", "Captures", "CandyCityLayouts"), views);
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

        // Fires each transplanted mechanism the way the Hand would (pull a piece away, or shove it) and measures the result.
        static readonly (string trigger, string watch, bool remove, Vector3 shove)[] Triggers =
        {
            ("Loose arch keystone", "Vault terrace deck", true, default),
            ("Removable drop shoe", "Two-floor dropping cassette", true, default),
            ("Wing receiving foot", "Hinged inhabited book wing", true, default),
            ("Removable heel counterweight", "Counterweighted bascule leaf", true, default),
            ("Removable support", "Falling route", true, default),
            // Shoves are in the piece's own frame: plate pushed sideways at its end, roof along its slide, screen near its top.
            ("Pivot route plate", "Pivot route plate", false, new Vector3(0, 0, 25)),
            ("Moving roof", "Moving roof", false, new Vector3(25, 0, 0)),
            ("Screen west", "Screen west", false, new Vector3(25, 0, 0)),
        };

        static int Mechanisms(string id, StringBuilder report)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = Object.Instantiate(Resources.Load<GameObject>("ModeMaps/Mode_" + id));
            var mode = Physics.simulationMode;
            int problems = 0;
            try
            {
                var map = go.GetComponent<EpicCityMap>(); map.Rebuild();
                Physics.simulationMode = SimulationMode.Script;
                for (int i = 0; i < 300; i++) Physics.Simulate(.02f);
                var bodies = map.Blocks.ToList();
                string Purpose(Rigidbody r) => r.GetComponent<MayhemStructuralMember>().Purpose;
                report.AppendLine($"  mechanisms ({bodies.Count(b => b.GetComponent<HingeJoint>())} hinges):");
                foreach (var (trigger, watch, remove, shove) in Triggers)
                {
                    var t = bodies.FirstOrDefault(b => b && b.gameObject.activeSelf && Purpose(b) == trigger);
                    var w = t ? bodies.Where(b => b && b.gameObject.activeSelf && Purpose(b) == watch).OrderBy(b => Vector3.Distance(b.position, t.position)).FirstOrDefault() : null;
                    if (!t || !w) { report.AppendLine($"    MISSING {trigger} / {watch}"); problems++; continue; }
                    var p0 = w.position; var r0 = w.rotation;
                    if (remove) t.gameObject.SetActive(false);
                    // A hard Hand shove for 1 s in the piece's frame, then the other way if nothing moved.
                    else
                    {
                        var offset = trigger == "Pivot route plate" ? new Vector3(3.5f, 0, 0) : trigger.StartsWith("Screen", StringComparison.Ordinal) ? new Vector3(0, 1.5f, 0) : Vector3.zero;
                        for (int pass = 0; pass < 2; pass++)
                        {
                            float sign = pass == 0 ? 1 : -1;
                            for (int i = 0; i < 50; i++) { t.AddForceAtPosition(t.transform.TransformDirection(shove) * sign * t.mass, t.transform.TransformPoint(offset), ForceMode.Force); t.WakeUp(); Physics.Simulate(.02f); }
                            if (Vector3.Distance(w.position, p0) > .2f || Quaternion.Angle(r0, w.rotation) > 3) break;
                        }
                    }
                    for (int i = 0; i < 200; i++) Physics.Simulate(.02f);
                    report.AppendLine($"    {(remove ? "pulled" : "shoved")} '{trigger}': '{watch}' moved {Vector3.Distance(w.position, p0):0.00} m, turned {Quaternion.Angle(r0, w.rotation):0.0} deg");
                }
            }
            catch (Exception e) { report.AppendLine("    ERROR " + e.Message); problems++; }
            finally { Physics.simulationMode = mode; Object.DestroyImmediate(go); }
            return problems;
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
                foreach (var i in Enumerable.Range(0, bodies.Count).Where(i => drift[i] > .25f).Take(8)) report.AppendLine("    drifted: " + bodies[i].name + $" {drift[i]:0.00} m");

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
