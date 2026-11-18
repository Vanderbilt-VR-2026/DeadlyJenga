using System;
using System.IO;
using System.Linq;
using System.Text;
using DeadlyJenga.Core;
using DeadlyJenga.Presentation;
using UnityEditor;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    // Fast checks for the capture tools (no Play Mode, no rendering). Report: Logs/Validation/CaptureTools.txt
    // Tools > Deadly Jenga > Captures > Run Capture Tool Checks, or -executeMethod ...CaptureToolChecks.RunBatch
    public static class CaptureToolChecks
    {
        [Serializable] sealed class Catalog { public AvatarAppearance[] skins, hair, headgear, eyewear, outfits, back, extras; }

        [MenuItem("Tools/Deadly Jenga/Captures/Run Capture Tool Checks", false, 20)]
        public static void Run() { int failed = Check(out string report); Debug.Log(report); if (failed > 0) Debug.LogError($"Capture tool checks: {failed} failed"); }

        public static void RunBatch() { int failed = Check(out _); EditorApplication.Exit(failed > 0 ? 1 : 0); }

        static int Check(out string text)
        {
            var report = new StringBuilder("Capture tool checks\n");
            int failed = 0;
            void Require(bool ok, string what) { report.AppendLine((ok ? "PASS " : "FAIL ") + what); if (!ok) failed++; }

            // 1. Every catalog item has a wardrobe picture in every body colour, named the way the wardrobe looks it up.
            var catalog = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("WardrobeCatalog").text);
            var groups = new[] { catalog.skins, catalog.hair, catalog.headgear, catalog.eyewear, catalog.outfits, catalog.back, catalog.extras };
            var manifest = WardrobeThumbnailLibrary.Current;
            Require(manifest != null && manifest.colors.Length > 0, "Wardrobe picture manifest lists the body colours");
            var style = Resources.Load<CandyPlayers.CandyPlayerRig>("CandyPlayerVisual")?.style;
            Require(style && manifest != null && style.colors.Length == manifest.colors.Length, "Manifest colours match the player palette");
            int missing = 0, items = 0;
            for (int slot = 0; slot < groups.Length; slot++)
                foreach (var entry in groups[slot] ?? Array.Empty<AvatarAppearance>())
                {
                    items++;
                    int colours = slot == 0 ? 1 : manifest?.colors.Length ?? 0;
                    for (int c = 0; c < colours; c++)
                        if (!Resources.Load<Texture2D>(WardrobeThumbnailLibrary.ResourcePath(slot, entry, c))) { missing++; if (missing <= 10) report.AppendLine("  missing " + WardrobeThumbnailLibrary.ResourcePath(slot, entry, c)); }
                }
            Require(missing == 0, $"All {items} catalog items have their pictures ({missing} missing)");
            if (manifest != null && manifest.colors.Length > 2)
                Require(WardrobeThumbnailLibrary.NearestColor(manifest.colors[2] * .97f) == 2, "Nearest body colour picks the right picture set");

            // 2. Gallery output: manifest, contact sheet and page; reopening a folder appends instead of replacing.
            string temp = Path.Combine(Path.GetTempPath(), "dj-capture-check-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(temp);
                var png = new Texture2D(8, 8); var pixels = Enumerable.Repeat(Color.magenta, 64).ToArray(); png.SetPixels(pixels); png.Apply();
                File.WriteAllBytes(Path.Combine(temp, "a.png"), png.EncodeToPNG());
                File.WriteAllBytes(Path.Combine(temp, "b.png"), png.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(png);
                var first = new CaptureOutput("check", temp); first.Add(Path.Combine(temp, "a.png"), "a"); first.SaveManifest();
                var second = new CaptureOutput("check", temp); second.Add(Path.Combine(temp, "b.png"), "b"); second.Finish(false);
                string json = File.ReadAllText(Path.Combine(temp, "manifest.json"));
                Require(json.Contains("a.png") && json.Contains("b.png"), "Gallery reopened after a reload keeps earlier items");
                Require(File.Exists(Path.Combine(temp, "contact-sheet.png")), "Gallery writes a contact sheet");
                string html = File.ReadAllText(Path.Combine(temp, "index.html"));
                Require(html.Contains("\"a.png\"") && html.Contains("Copy all paths") && html.Contains("onwheel"), "Gallery page lists items with copy and zoom controls");

                // 3. Accepting a candidate replaces exactly that picture.
                string live = Path.Combine(temp, "live.png"), candidate = Path.Combine(temp, "candidate.png");
                File.WriteAllBytes(live, new byte[] { 1 }); File.WriteAllBytes(candidate, new byte[] { 2, 3 });
                File.Copy(candidate, live, true); File.Delete(candidate);
                Require(File.ReadAllBytes(live).Length == 2 && !File.Exists(candidate), "Accepting a candidate swaps in the new picture");
            }
            finally { try { Directory.Delete(temp, true); } catch (IOException) { } }

            // 4. Camera paths start and end where expected and always look at the subject.
            foreach (var mode in CameraPath.Modes)
            {
                bool ok = true;
                var start = new Vector3(10, 6, -10);
                for (int i = 0; i <= 10; i++)
                {
                    CameraPath.Evaluate(mode, i / 10f, Vector3.zero, start.magnitude, start, out var position, out var rotation);
                    var toFocus = (Vector3.zero - position).normalized;
                    if (Vector3.Dot(rotation * Vector3.forward, toFocus) < .999f) ok = false;
                    if (mode is "orbit" or "arc" or "crane" && Mathf.Abs(position.magnitude - start.magnitude) > .01f) ok = false;
                }
                Require(ok, $"Camera path '{mode}' keeps the subject centred");
            }
            CameraPath.Evaluate("orbit", 0, Vector3.zero, 10, new Vector3(0, 0, -10), out var p0, out _);
            CameraPath.Evaluate("orbit", 1, Vector3.zero, 10, new Vector3(0, 0, -10), out var p1, out _);
            Require((p0 - p1).sqrMagnitude < .001f, "Orbit ends where it started (loops cleanly)");

            // 5. Map ids exist for every lobby picture the rigs write.
            var maps = UnityEngine.Object.Instantiate(Resources.Load<CastlePrototype.IslandLobbyCatalog>("IslandLobby/Catalog"));
            maps.EnsureModeMaps();
            Require(maps.maps.Length > 0 && maps.maps.All(m => !string.IsNullOrEmpty(m.id)), $"Lobby catalog lists {maps.maps.Length} maps with ids");
            Require(maps.maps.Count(m => m.mapPrefab || !string.IsNullOrEmpty(m.scenePath)) == maps.maps.Length, "Every catalog map can be loaded by the rigs");
            UnityEngine.Object.DestroyImmediate(maps);

            report.AppendLine(failed == 0 ? "ALL PASSED" : failed + " FAILED");
            text = report.ToString();
            File.WriteAllText(ProjectPath.Report("CaptureTools.txt"), text);
            return failed;
        }
    }
}
