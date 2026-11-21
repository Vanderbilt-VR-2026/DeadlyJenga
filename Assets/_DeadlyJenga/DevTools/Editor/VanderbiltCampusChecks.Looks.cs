using System;
using System.Collections.Generic;
using System.IO;
using DeadlyJenga.CastlePrototype;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    // Looks comparison (Stage 5 "Done when" 4): the candy and the plain palette rendered from every hero spot
    // in Tools/VanderbiltCampus/config/hero_spots.json, into Logs/Validation/VanderbiltCampus/looks/<spot>_<palette>.png.
    // World is never changed: the palette material is a temporary copy on the temp instance.
    public static partial class VanderbiltCampusChecks
    {
        static readonly (string name, string strip)[] Palettes = { ("candy", "CampusPaletteCandy.png"), ("plain", "CampusPalettePlain.png") };

        [MenuItem("Tools/Deadly Jenga/Validation/Vanderbilt Campus Looks (candy vs plain)")]
        public static void LooksMenu() => Debug.Log("Campus looks " + (RunLooks() ? "rendered" : "had failures") + ": " + ProjectPath.Report("VanderbiltCampusLooks.txt"));

        public static void LooksBatch()
        {
            bool ok = false;
            try { ok = RunLooks(); }
            catch (Exception e) { File.AppendAllText(ProjectPath.Report("VanderbiltCampusLooks.txt"), "FAIL " + e + "\n"); }
            VanderbiltCampusData.Exit(ok ? 0 : 1);
        }

        public static bool RunLooks() => RunSuite("VanderbiltCampusLooks.txt", "Vanderbilt campus looks (candy and plain palettes)", (root, map, data, scene) =>
        {
            var spots = HeroSpots();
            Check(spots.Count > 0, spots.Count + " hero spots with a position in hero_spots.json");
            Directory.CreateDirectory(Path.Combine("Logs", "Validation", "VanderbiltCampus", "looks"));
            WithRenderSetup(root, scene, (groups, original) =>
            {
                var camera = RenderCamera(ViewCamera());
                try
                {
                    foreach (var look in Palettes)
                    {
                        var palette = PaletteMaterial(look.strip);
                        Check(palette, "Palette material for " + look.name);
                        if (!palette) continue;
                        Paint(root, palette);
                        foreach (var spot in spots)
                        {
                            AimAtSpot(camera, map, spot);
                            ForceLods(groups, camera);
                            string file = $"VanderbiltCampus/looks/{Safe(spot.name)}_{look.name}.png";
                            Capture(camera, 1600, 1000, file);
                            report.AppendLine($"PASS rendered Logs/Validation/{file}");
                        }
                        original();
                        Object.DestroyImmediate(palette);
                    }
                }
                finally { Object.DestroyImmediate(camera.gameObject); }
            });
        });

        sealed class HeroSpot { public string name, kind; public Vector2 xy; }

        static List<HeroSpot> HeroSpots()
        {
            var list = new List<HeroSpot>();
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Tools", "VanderbiltCampus", "config", "hero_spots.json");
            if (!File.Exists(path)) { report.AppendLine("NOTE hero_spots.json not found at " + path); return list; }
            if (!(MiniJson.Parse(File.ReadAllText(path)) is Dictionary<string, object> root) || !root.TryGetValue("spots", out var value) || !(value is List<object> spots)) return list;
            foreach (var item in spots)
            {
                if (!(item is Dictionary<string, object> spot) || !spot.TryGetValue("match", out var m) || !(m is Dictionary<string, object> match)) continue;
                if (!match.TryGetValue("centroid", out var c) || !(c is List<object> xy) || xy.Count < 2 || !(xy[0] is double x) || !(xy[1] is double y)) continue;
                list.Add(new HeroSpot
                {
                    name = spot.TryGetValue("spot", out var n) && n is string s ? s : "spot" + list.Count,
                    kind = match.TryGetValue("kind", out var k) && k is string kind ? kind : "",
                    xy = new Vector2((float)x, (float)y)
                });
            }
            return list;
        }

        // Buildings: from 60 m south-west and 25 m up. Lawns, points and groups: from 35 m south-west, 12 m up.
        static void AimAtSpot(Camera camera, VanderbiltCampusMap map, HeroSpot spot)
        {
            bool building = spot.kind == "building";
            float back = building ? 60 : 35, up = building ? 25 : 12;
            if (!VanderbiltCampusBuilder.GroundAt(map.transform, spot.xy, out float ground)) ground = map.lowestGround;
            var target = map.transform.TransformPoint(new Vector3(spot.xy.x, ground + (building ? 10 : 2), spot.xy.y));
            var eye = map.transform.TransformPoint(new Vector3(spot.xy.x - back * .7071f, ground + up, spot.xy.y - back * .7071f));
            Aim(camera, eye, target);
        }
    }
}
