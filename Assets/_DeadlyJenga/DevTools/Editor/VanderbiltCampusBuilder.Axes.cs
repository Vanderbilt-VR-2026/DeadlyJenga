using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    // Checks that the imported tiles line up with unity_map.json. The exporter writes the same points twice:
    // as Route_<nn> / Spawn_<name> empties inside the FBX and as numbers in the json. If the imported empties
    // sit at the json points turned 180 degrees about the vertical axis (x and z negated), the FBX axis
    // conversion went the other way; the builder turns every tile 180 degrees back and says so in the report.
    // A mirror (only one axis negated) cannot be fixed by turning and is reported as a problem.
    public static partial class VanderbiltCampusBuilder
    {
        static void CheckAxes(Transform root, VanderbiltCampusData data, BuildStats stats)
        {
            var pairs = new List<(Vector3 marker, Vector3 json)>();
            var routeNumber = new Regex(@"^Route_(\d+)$");
            foreach (var t in stats.routeMarkers)
            {
                var m = routeNumber.Match(CleanName(t.name));
                if (m.Success && int.TryParse(m.Groups[1].Value, out int i) && i < data.route.Count)
                    pairs.Add((root.InverseTransformPoint(t.position), data.route[i]));
            }
            foreach (var t in stats.spawnMarkers)
            {
                var spawn = data.spawns.Find(s => "Spawn_" + SafeName(s.name) == CleanName(t.name));
                if (spawn != null) pairs.Add((root.InverseTransformPoint(t.position), spawn.pos));
            }
            if (pairs.Count == 0) { stats.notes.Add("Axis check skipped: no Route_/Spawn_ empties match unity_map.json."); return; }

            var candidates = new (string name, Vector3 scale)[]
            {
                ("as exported", new Vector3(1, 1, 1)),
                ("turned 180 degrees about Y", new Vector3(-1, 1, -1)),
                ("mirrored north-south", new Vector3(1, 1, -1)),
                ("mirrored east-west", new Vector3(-1, 1, 1)),
            };
            var errors = candidates.Select(c => pairs.Average(p => Vector3.Distance(p.marker, Vector3.Scale(p.json, c.scale)))).ToArray();
            int best = Array.IndexOf(errors, errors.Min());
            string summary = $"{pairs.Count} empties vs unity_map.json: mean error as exported {errors[0]:F2} m, best fit '{candidates[best].name}' {errors[best]:F2} m";
            if (best == 0 || errors[0] < 1) { stats.notes.Add("Axis check OK: " + summary); return; }
            if (best == 1 && errors[1] < 1)
            {
                foreach (Transform tile in root) if (tile.name.StartsWith("T_")) tile.localRotation = Quaternion.Euler(0, 180, 0);
                stats.axisFix = "Tiles imported turned 180 degrees about Y (" + summary + "). The builder turned every tile back. " +
                    "Fix at the source (exporter axes or import setting) and this correction stops being applied.";
                Debug.LogWarning("[VanderbiltCampus] " + stats.axisFix);
                return;
            }
            stats.Problem("Tiles do not line up with unity_map.json: " + summary + ".");
        }

        // Same rule as the pipeline's safe_name(): letters, digits and underscores only, spaces become underscores.
        static string SafeName(string text)
        {
            var s = Regex.Replace(text.Replace(" ", "_"), "[^A-Za-z0-9_]+", "");
            return s.Length > 0 ? s : "x";
        }
    }
}
