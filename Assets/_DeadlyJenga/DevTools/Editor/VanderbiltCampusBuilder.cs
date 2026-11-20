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
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    // Builds Mode_vanderbilt.prefab from the Blender pipeline's tile FBX files.
    // Runs only from the menu or BuildBatch(); never on load. Never saves Lobby.unity or Game.unity.
    // Steps: gray materials -> FBX import settings -> one map root with every tile -> colliders,
    // LOD groups, markers -> optional "Visual" (Presentation) -> prefab -> lobby catalog -> match layout -> report.
    public static partial class VanderbiltCampusBuilder
    {
        public const string MapId = "mode-vanderbilt";
        public const string Title = "Campus (LoD2)";
        const string PrefabName = "Mode_vanderbilt.prefab";
        const string VisualPrefabName = "Campus Visual.prefab";
        static readonly Regex TileFile = new Regex(@"^T_-?\d+_-?\d+\.fbx$", RegexOptions.IgnoreCase);

        // Counts and problems collected while building; written to the report at the end.
        sealed class BuildStats
        {
            public int tiles, buildings, lodGroups, colliders, convexColliders, climbSurfaces, materials, trianglesAll, trianglesLod0, spawns;
            public int renderers, submeshes, ringTilesWithoutCollider, tinyConvex, buildingsMissingLevel, exporterHlods, builtHlods;
            public long hlodVertices, hlodIndices, hlodAssetBytes;
            public string axisFix = "";
            public readonly List<string> problems = new List<string>();
            // A real problem: fails the build (exit 1) and goes to the Unity log so the coordinator's summary shows it.
            public void Problem(string text)
            {
                problems.Add(text);
                Debug.LogError("[VanderbiltCampus] PROBLEM: " + text);
            }
            public readonly List<string> notes = new List<string>();
            public readonly List<Transform> spawnMarkers = new List<Transform>();
            public readonly List<Transform> routeMarkers = new List<Transform>();
            public readonly List<Renderer> terrain = new List<Renderer>();
            public readonly List<Renderer> visuals = new List<Renderer>();
        }

        static string ReportPath => ProjectPath.Report("VanderbiltCampusBuild.txt");

        [MenuItem("Tools/Deadly Jenga/Maps/Build Vanderbilt Campus")]
        public static void BuildMenu()
        {
            bool ok = Build();
            Debug.Log("Vanderbilt campus build " + (ok ? "finished" : "finished with problems") + ". See " + ReportPath);
        }

        // Entry point for the coordinator: build, write the report, exit 0 when there were no problems.
        public static void BuildBatch()
        {
            bool ok = false;
            try { ok = Build(); }
            catch (Exception e) { File.AppendAllText(ReportPath, "FAIL " + e + "\n"); }
            VanderbiltCampusData.Exit(ok ? 0 : 1);
        }

        // One coordinator job: build, then the edit-mode checks (overhead render + budget), then exit once.
        // The exit code reflects only the build; check failures are in VanderbiltCampusChecks.txt.
        public static void BuildAndCheckBatch()
        {
            bool ok = false;
            try { ok = Build(); }
            catch (Exception e) { File.AppendAllText(ReportPath, "FAIL " + e + "\n"); Debug.LogError("[VanderbiltCampus] PROBLEM: build threw " + e.Message); }
            if (prefabSaved)
            {
                try
                {
                    bool checks = VanderbiltCampusChecks.Run();
                    Debug.Log("[VanderbiltCampus] checks " + (checks ? "passed" : "reported failures") + ": " + ProjectPath.Report("VanderbiltCampusChecks.txt"));
                }
                catch (Exception e) { Debug.LogWarning("[VanderbiltCampus] checks threw: " + e.Message); }
            }
            VanderbiltCampusData.Exit(ok ? 0 : 1);
        }

        static bool prefabSaved;

        // Returns true when the prefab was saved and there were no real problems (notes are fine).
        public static bool Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            prefabSaved = false;
            var stats = new BuildStats();
            File.WriteAllText(ReportPath, "Vanderbilt campus build " + DateTime.Now.ToString("u") + "\n");

            string campusFolder = CampusFolder();
            string modelsFolder = Folder(campusFolder, "Models");
            string materialsFolder = Folder(campusFolder, "Materials");
            var data = VanderbiltCampusData.Load();
            foreach (var p in data.problems) stats.Problem(p);

            var materials = EnsureMaterials(materialsFolder, stats);
            var tilePaths = AssetDatabase.FindAssets("t:Model", new[] { modelsFolder })
                .Select(AssetDatabase.GUIDToAssetPath).Where(p => TileFile.IsMatch(Path.GetFileName(p))).Distinct().OrderBy(p => p).ToList();
            if (materials.Count == 0 || tilePaths.Count == 0)
            {
                if (tilePaths.Count == 0) stats.Problem("No T_<col>_<row>.fbx tiles in " + modelsFolder + ". The existing prefab was left unchanged.");
                WriteReport(stats, data, null);
                return false;
            }
            foreach (var path in tilePaths) ConfigureImporter(path, materials, stats);

            if (Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path)) EditorSceneManager.OpenScene(ProjectPath.Find("Andy.unity"));
            var previousWorld = Services.World; var active = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
            try
            {
                var root = new GameObject("Campus (LoD2) - Vanderbilt tiles");
                var map = root.AddComponent<VanderbiltCampusMap>();
                foreach (var path in tilePaths) AddTile(root.transform, path, stats);
                CheckAxes(root.transform, data, stats);
                FillMap(map, data, stats);
                NestVisual(root.transform, stats);

                string prefabPath = PrefabPath();
                var saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool success);
                if (!success || !saved) throw new InvalidOperationException("Could not save " + prefabPath);
                prefabSaved = true;
                UpdateCatalog(prefabPath);
                AuthorMatchLayout(map, data, stats);
                AssetDatabase.SaveAssets();
                WriteReport(stats, data, prefabPath);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
                Services.World = previousWorld;
            }
            return stats.problems.Count == 0;
        }

        // The campus folder is the parent of the folder holding VanderbiltCampusMap.cs.
        static string CampusFolder() =>
            Path.GetDirectoryName(ProjectPath.FolderOf("VanderbiltCampusMap.cs").TrimEnd('/')).Replace('\\', '/');

        static string PrefabPath()
        {
            try { return ProjectPath.Find(PrefabName); }
            catch (FileNotFoundException)
            {
                var maps = Path.GetDirectoryName(CampusFolder()).Replace('\\', '/');
                return Folder(Folder(maps, "Resources"), "ModeMaps") + "/" + PrefabName;
            }
        }

        static string Folder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
            return path;
        }

        // Fills the map component: spawns, lowest ground, playable area and layout version.
        static void FillMap(VanderbiltCampusMap map, VanderbiltCampusData data, BuildStats stats)
        {
            var root = map.transform;
            // The json spawns and the Spawn_* empties are the same points: use the json, empties only as a fallback.
            var spawns = new List<Transform>();
            if (data.spawns.Count == 0)
            {
                spawns.AddRange(stats.spawnMarkers);
                if (spawns.Count > 0) stats.notes.Add("No spawns in unity_map.json; used " + spawns.Count + " Spawn_* empties.");
            }
            else
            {
                var group = new GameObject("Spawns (unity_map.json)").transform; group.SetParent(root, false);
                foreach (var s in data.spawns)
                {
                    var t = new GameObject(s.name).transform; t.SetParent(group, false); t.localPosition = s.pos;
                    spawns.Add(t);
                }
            }
            if (spawns.Count == 0) stats.Problem("No spawns: add Spawn_* empties or a spawns list in unity_map.json.");
            map.spawns = spawns.ToArray(); stats.spawns = spawns.Count;

            float lowest = float.PositiveInfinity;
            foreach (var r in stats.terrain) lowest = Mathf.Min(lowest, root.InverseTransformPoint(r.bounds.min).y);
            if (data.hasLowestGround) lowest = data.lowestGround;
            if (float.IsInfinity(lowest)) { lowest = 0; stats.Problem("No Terrain meshes found; lowestGround set to 0."); }
            if (lowest < -.01f) stats.Problem("Lowest ground is " + lowest.ToString("F2") + " m; the contract needs y >= 0.");
            map.lowestGround = lowest;

            if (data.hasArea) map.localArea = data.localArea;
            else
            {
                bool any = false; var area = new Bounds();
                foreach (var r in stats.visuals)
                {
                    var b = new Bounds(root.InverseTransformPoint(r.bounds.center), r.bounds.size);
                    if (!any) { area = b; any = true; } else area.Encapsulate(b);
                }
                if (!any) area = new Bounds(Vector3.zero, new Vector3(100, 40, 100));
                area.Expand(new Vector3(20, 10, 20));
                map.localArea = area;
                stats.notes.Add("local_area missing from unity_map.json; used all visual bounds plus a 10 m margin.");
            }
            map.layoutVersion = data.layoutVersion;
        }

        // The Presentation look is optional: without it the map stays gray and still plays.
        static void NestVisual(Transform root, BuildStats stats)
        {
            // Presentation's own builder makes (or refreshes) the palette material and visual prefab first.
            try { DeadlyJenga.CandyArt.Editor.CampusVisualBuilder.Build(); }
            catch (Exception e) { stats.notes.Add("Campus visual builder failed: " + e.Message); }
            string path;
            try { path = ProjectPath.Find(VisualPrefabName); }
            catch (FileNotFoundException) { stats.notes.Add(VisualPrefabName + " not found: built as gray box (no Visual child)."); return; }
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var visual = asset ? PrefabUtility.InstantiatePrefab(asset, root) as GameObject : null;
            if (!visual) { stats.Problem("Could not nest " + path); return; }
            visual.name = "Visual";
            stats.notes.Add("Nested " + path + " as Visual.");
        }

        static void UpdateCatalog(string prefabPath)
        {
            var catalog = Resources.Load<IslandLobbyCatalog>("IslandLobby/Catalog");
            if (!catalog) throw new InvalidOperationException("IslandLobby/Catalog not found.");
            var list = (catalog.maps ?? Array.Empty<IslandLobbyCatalog.Map>()).ToList();
            var entry = list.Find(m => m != null && m.id == MapId);
            if (entry == null) { entry = new IslandLobbyCatalog.Map { id = MapId }; list.Add(entry); }
            entry.title = Title;
            entry.mapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (string.IsNullOrEmpty(entry.scenePath)) entry.scenePath = ProjectPath.Find("Game.unity");
            entry.recommendedMode = "demo"; entry.recommendedPlayers = 1; entry.walkableIsland = true;
            entry.supportedModes = new[] { "demo" };
            catalog.maps = list.ToArray();
            EditorUtility.SetDirty(catalog);
        }

        static void WriteReport(BuildStats stats, VanderbiltCampusData data, string prefabPath)
        {
            var r = new StringBuilder();
            r.AppendLine("unity_map.json: " + (data.found ? data.path : "missing"));
            r.AppendLine("Prefab: " + (prefabPath ?? "not saved"));
            r.AppendLine($"Tiles={stats.tiles} Buildings={stats.buildings} Colliders={stats.colliders} (convex {stats.convexColliders}) ClimbSurfaces={stats.climbSurfaces}");
            r.AppendLine($"Triangles all visuals={stats.trianglesAll} LOD0+terrain+trees+ring={stats.trianglesLod0} Materials={stats.materials} Spawns={stats.spawns}");
            r.AppendLine($"Visual renderers={stats.renderers} submeshes={stats.submeshes} (all static, batching-static; not merged so material IDs stay submeshes)");
            r.AppendLine($"Tile LODGroups={stats.lodGroups} (one per tile, not nested); switch heights detail {TileDetail} / mid {TileMid} / far {TileFar}; buildings missing a LOD level={stats.buildingsMissingLevel}");
            if (stats.axisFix.Length > 0) r.AppendLine("AXES " + stats.axisFix);
            int perBuilding = stats.buildings > 0 ? Mathf.RoundToInt((float)stats.convexColliders / stats.buildings) : 0;
            r.AppendLine($"Convex colliders per building (average)={perBuilding}; tiny convex pieces (< 0.5 m or < 0.05 m3)={stats.tinyConvex}");
            long hlodBytes = stats.hlodVertices * HlodVertexBytes + stats.hlodIndices * 4;
            r.AppendLine($"HLOD exporter objects={stats.exporterHlods} built={stats.builtHlods} (MakeHlodAssets={MakeHlodAssets}); HLOD estimate if built here: " +
                $"{stats.hlodVertices} vertices, {stats.hlodIndices / 3} triangles, {hlodBytes / 1048576f:F1} MB binary, about {hlodBytes * 2 / 1048576f:F0} MB as text .asset (ForceText)" +
                (stats.builtHlods > 0 ? $"; actual files {stats.hlodAssetBytes / 1048576f:F1} MB" : ""));
            if (stats.ringTilesWithoutCollider > 0)
                r.AppendLine("NOTE " + stats.ringTilesWithoutCollider + " backdrop ring tiles have no COL_Terrain (expected: farther than 20 m from campus).");
            foreach (var note in stats.notes) r.AppendLine("NOTE " + note);
            foreach (var problem in stats.problems) r.AppendLine("PROBLEM " + problem);
            r.AppendLine(stats.problems.Count == 0 && prefabPath != null ? "RESULT PASS" : "RESULT FAIL (" + stats.problems.Count + " problems)");
            File.AppendAllText(ReportPath, r.ToString());
        }
    }
}
