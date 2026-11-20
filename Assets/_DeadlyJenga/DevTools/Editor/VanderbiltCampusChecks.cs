using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    // Edit-mode checks for the built campus prefab: mesh data, materials, colliders, tile size,
    // Quest budgets per viewpoint, and evidence renders. Writes Logs/Validation/VanderbiltCampusChecks.txt.
    public static partial class VanderbiltCampusChecks
    {
        static StringBuilder report;
        static int failures;
        static string ReportPath => ProjectPath.Report("VanderbiltCampusChecks.txt");

        [MenuItem("Tools/Deadly Jenga/Validation/Check Vanderbilt Campus")]
        public static void RunMenu()
        {
            bool ok = Run();
            Debug.Log("Vanderbilt campus checks " + (ok ? "passed" : "failed") + ". See " + ReportPath);
        }

        // Entry point for the coordinator: exit 0 when every check passed.
        public static void RunBatch()
        {
            bool ok = false;
            try { ok = Run(); }
            catch (Exception e) { File.AppendAllText(ReportPath, "FAIL " + e + "\n"); }
            VanderbiltCampusData.Exit(ok ? 0 : 1);
        }

        public static bool Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            report = new StringBuilder(); failures = 0;
            report.AppendLine("Vanderbilt campus edit-mode checks " + DateTime.Now.ToString("u") + ". Desktop editor only; no headset.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPath.Find("Mode_vanderbilt.prefab"));
            var data = VanderbiltCampusData.Load();
            foreach (var p in data.problems) report.AppendLine("NOTE " + p);

            if (Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path)) EditorSceneManager.OpenScene(ProjectPath.Find("Andy.unity"));
            var previousWorld = Services.World; var active = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
            try
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                var map = root.GetComponent<VanderbiltCampusMap>();
                Check(map, "Mode_vanderbilt.prefab root has VanderbiltCampusMap");
                if (map)
                {
                    root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    Physics.SyncTransforms();
                    CheckMeshes(root.transform);
                    CheckColliders(root.transform);
                    CheckBuildingColliders(root.transform);
                    CheckTiles(root.transform);
                    Check(map.SaveLayout() == "vanderbilt-campus:" + map.layoutVersion, "Layout token is " + map.SaveLayout());
                    Check(map.spawns.Length > 0 && map.spawns.All(s => s), "Map has " + map.spawns.Length + " spawn points");
                    Budget(root.transform, map, data);
                    Renders(root.transform, map, data, scene);
                }
            }
            catch (Exception e) { failures++; report.AppendLine("FAIL " + e); }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
                Services.World = previousWorld;
                report.AppendLine(failures == 0 ? "RESULT PASS" : "RESULT FAIL (" + failures + ")");
                File.WriteAllText(ReportPath, report.ToString());
            }
            return failures == 0;
        }

        static void Check(bool pass, string text)
        {
            report.AppendLine((pass ? "PASS " : "FAIL ") + text);
            if (!pass) failures++;
        }

        // Presentation's "Visual" child may use its own meshes and materials; World content may not.
        static bool InVisual(Transform t, Transform root)
        {
            for (var p = t; p && p != root; p = p.parent) if (p.name == "Visual" && p.parent == root) return true;
            return false;
        }

        static void CheckMeshes(Transform root)
        {
            int renderers = 0; var noUv = new List<string>(); var noColor = new List<string>(); var badMaterial = new List<string>();
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (InVisual(r.transform, root)) continue;
                renderers++;
                var mesh = r.GetComponent<MeshFilter>() ? r.GetComponent<MeshFilter>().sharedMesh : null;
                if (!mesh) { noUv.Add(r.name + " (no mesh)"); continue; }
                if (!mesh.HasVertexAttribute(VertexAttribute.TexCoord0)) noUv.Add(r.name);
                if (!mesh.HasVertexAttribute(VertexAttribute.Color)) noColor.Add(r.name);
                if (r.sharedMaterials.Any(m => !m || !m.name.StartsWith("mat_"))) badMaterial.Add(r.name);
            }
            Check(renderers > 0, renderers + " world renderers found");
            Check(noUv.Count == 0, "Every world mesh has UV0" + List(noUv));
            Check(noColor.Count == 0, "Every world mesh has vertex colors (material index in R)" + List(noColor));
            Check(badMaterial.Count == 0, "Every world material is named mat_<id>" + List(badMaterial));
        }

        // Every building (B_<id> visual) needs a collider group COL_B_<id> with at least one collider, and a
        // whole-building non-convex collider (COLM_B_<id>) needs a real mesh (at least 4 triangles).
        static void CheckBuildingColliders(Transform root)
        {
            var missing = new List<string>(); var thin = new List<string>(); int convexParts = 0, wholeMeshes = 0;
            foreach (Transform tile in root)
            {
                if (!tile.name.StartsWith("T_")) continue;
                var ids = tile.GetComponentsInChildren<Renderer>(true)
                    .Select(r => Regex.Match(Regex.Replace(r.name, @"\.\d+$", ""), @"^B_(.+)_LOD[01]$"))
                    .Where(m => m.Success).Select(m => m.Groups[1].Value).Distinct();
                foreach (var id in ids)
                {
                    var holder = tile.Find("Colliders/COL_B_" + id);
                    if (!holder || holder.GetComponentsInChildren<Collider>(true).Length == 0) missing.Add(tile.name + "/B_" + id);
                }
                foreach (var shape in tile.GetComponentsInChildren<MeshCollider>(true))
                {
                    string n = Regex.Replace(shape.name, @"\.\d+$", "");
                    if (n.StartsWith("COL_B_")) convexParts++;
                    if (!n.StartsWith("COLM_B_")) continue;
                    wholeMeshes++;
                    if (shape.convex || !shape.sharedMesh || VanderbiltCampusBuilder.Triangles(shape.sharedMesh) < 4) thin.Add(tile.name + "/" + n);
                }
            }
            report.AppendLine($"NOTE Building colliders: {convexParts} convex parts (COL_B_*_n), {wholeMeshes} whole-building non-convex meshes (COLM_B_*)");
            Check(missing.Count == 0, "Every building has at least one collider" + List(missing));
            Check(thin.Count == 0, "Every COLM_B_* collider is non-convex with at least 4 triangles" + List(thin));
        }

        static void CheckColliders(Transform root)
        {
            var all = root.GetComponentsInChildren<Collider>(true);
            var wrongLayer = all.Where(c => c.gameObject.layer != Layers.Surface).Select(c => c.name).ToList();
            var onVisual = all.OfType<MeshCollider>().Where(c => c.GetComponent<Renderer>()).Select(c => c.name).ToList();
            var noMesh = all.OfType<MeshCollider>().Where(c => !c.sharedMesh).Select(c => c.name).ToList();
            var climb = root.GetComponentsInChildren<CampusClimbSurface>(true);
            Check(all.Length > 0, all.Length + " colliders");
            Check(wrongLayer.Count == 0, "Every collider is on Layers.Surface" + List(wrongLayer));
            Check(onVisual.Count == 0, "No MeshCollider sits on a visual renderer" + List(onVisual));
            Check(noMesh.Count == 0, "Every MeshCollider has a mesh" + List(noMesh));
            Check(climb.Length > 0, climb.Length + " climbable building collider groups");
            Check(!root.GetComponent<CampusClimbSurface>() && climb.All(c => !c.name.Contains("Terrain")), "Climb surfaces are on buildings only (not terrain or map root)");
        }

        // Each tile's Terrain should be about a 100 m square (catches Blender scale or axis mistakes).
        static void CheckTiles(Transform root)
        {
            int tiles = 0; var odd = new List<string>();
            foreach (Transform tile in root)
            {
                if (!tile.name.StartsWith("T_")) continue;
                tiles++;
                // The LOD0 ground is named exactly "Terrain" (Terrain_LOD1 is the cheap far copy).
                var terrain = tile.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => Regex.Replace(r.name, @"\.\d+$", "") == "Terrain");
                if (!terrain) { odd.Add(tile.name + " (no Terrain)"); continue; }
                var size = terrain.bounds.size;
                if (Mathf.Abs(size.x - 100) > 2 || Mathf.Abs(size.z - 100) > 2) odd.Add($"{tile.name} {size.x:F1}x{size.z:F1} m");
            }
            Check(tiles > 0, tiles + " tiles in the prefab");
            Check(odd.Count == 0, "Every Terrain is about 100 x 100 m (within 2 m)" + List(odd));
        }

        static string List(List<string> items) =>
            items.Count == 0 ? "" : ": " + string.Join(", ", items.Take(12)) + (items.Count > 12 ? " ... (" + items.Count + " total)" : "");

        // Renders one camera into a PNG under Logs/Validation.
        internal static void Capture(Camera camera, int width, int height, string fileName)
        {
            var before = camera.targetTexture; var old = RenderTexture.active;
            var rt = new RenderTexture(width, height, 24); var png = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                png.ReadPixels(new Rect(0, 0, width, height), 0, 0); png.Apply();
                File.WriteAllBytes(ProjectPath.Report(fileName), png.EncodeToPNG());
            }
            finally { camera.targetTexture = before; RenderTexture.active = old; Object.DestroyImmediate(rt); Object.DestroyImmediate(png); }
        }
    }
}
