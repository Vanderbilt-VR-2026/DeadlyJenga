using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DeadlyJenga.CastlePrototype;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    // Per-tile Quest budget (STANDARDS §10, Stage 5 "Done when" 3): for every tile, the busiest spot among a
    // 3 x 3 grid of standing points (1.7 m above COL_Terrain) looking north, east, south and west, at lodBias 1.
    // Verdict per tile = play mode (one shared palette material): <= 250k triangles and <= 150 draw calls B
    // (one per renderer). Draws A (one per submesh, gray box) is listed as information.
    // Tiles with no walkable ground (backdrop ring without COL_Terrain) are listed as "no standing spot".
    public static partial class VanderbiltCampusChecks
    {
        const double TileBudgetMinutes = 20;

        [MenuItem("Tools/Deadly Jenga/Validation/Vanderbilt Campus Tile Budgets")]
        public static void TileBudgetMenu() => Debug.Log("Campus tile budgets " + (RunTileBudget() ? "passed" : "failed") + ": " + ProjectPath.Report("VanderbiltCampusTileBudget.txt"));

        public static void TileBudgetBatch()
        {
            bool ok = false;
            try { ok = RunTileBudget(); }
            catch (Exception e) { File.AppendAllText(ProjectPath.Report("VanderbiltCampusTileBudget.txt"), "FAIL " + e + "\n"); }
            VanderbiltCampusData.Exit(ok ? 0 : 1);
        }

        public static bool RunTileBudget() => RunSuite("VanderbiltCampusTileBudget.txt", "Vanderbilt campus per-tile budget (lodBias 1)", (root, map, data, scene) =>
        {
            var camera = ViewCamera();
            var clock = Stopwatch.StartNew();
            int passed = 0, failed = 0, skipped = 0;
            report.AppendLine($"Limits: {TriangleBudget} triangles, {DrawBudget} draw calls (B, one per renderer, play mode). A = one per submesh (gray box), information only.");
            report.AppendLine("tile | busiest spot (x, y, z) | facing | triangles | draws B | draws A | result");
            try
            {
                foreach (Transform tile in root)
                {
                    if (!tile.name.StartsWith("T_")) continue;
                    if (clock.Elapsed.TotalMinutes > TileBudgetMinutes) { Check(false, $"Time limit {TileBudgetMinutes} min reached; remaining tiles not measured"); break; }
                    var renderers = tile.GetComponentsInChildren<MeshRenderer>(true);
                    if (renderers.Length == 0) continue;
                    var b = renderers[0].bounds; foreach (var r in renderers) b.Encapsulate(r.bounds);
                    ViewCount busiest = null; Vector3 spot = default; string facing = "";
                    for (int i = 0; i < 3; i++)
                        for (int j = 0; j < 3; j++)
                        {
                            var world = new Vector3(Mathf.Lerp(b.min.x, b.max.x, .2f + .3f * i), 0, Mathf.Lerp(b.min.z, b.max.z, .2f + .3f * j));
                            var local = map.transform.InverseTransformPoint(world);
                            if (!VanderbiltCampusBuilder.GroundAt(map.transform, new Vector2(local.x, local.z), out float ground)) continue;
                            var eye = map.transform.TransformPoint(new Vector3(local.x, ground + EyeHeight, local.z));
                            foreach (var (name, dir) in new[] { ("north", Vector3.forward), ("east", Vector3.right), ("south", Vector3.back), ("west", Vector3.left) })
                            {
                                Aim(camera, eye, eye + dir);
                                var count = Count(root, camera, 1);
                                if (busiest == null || count.triangles > busiest.triangles) { busiest = count; spot = eye; facing = name; }
                            }
                        }
                    if (busiest == null) { skipped++; report.AppendLine($"{tile.name} | no standing spot (no COL_Terrain) | - | - | - | - | SKIP"); continue; }
                    bool pass = busiest.triangles <= TriangleBudget && busiest.drawsPerRenderer <= DrawBudget;
                    if (pass) passed++; else { failed++; failures++; }
                    report.AppendLine($"{tile.name} | {spot:F0} | {facing} | {busiest.triangles} | {busiest.drawsPerRenderer} | {busiest.drawsPerSubmesh} | {(pass ? "PASS" : "FAIL")}");
                }
            }
            finally { Object.DestroyImmediate(camera.gameObject); }
            report.AppendLine($"SUMMARY {passed} tiles pass, {failed} fail, {skipped} without a standing spot; {clock.Elapsed.TotalSeconds:F0} s.");
        });
    }
}
