using System;
using System.IO;
using System.Linq;
using DeadlyJenga.CastlePrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    // OPTIONAL and OFF by default: a timing probe for baked lighting (ambient occlusion) on the campus.
    // The campus ships with one realtime sun and no baked lighting. This PC bakes on the CPU (no NVIDIA GPU),
    // so this only bakes the tiles within 120 m of the play-area center in a scratch scene, measures how long it
    // takes, and deletes the scratch scene and its lightmaps again. Nothing is applied to the map prefab
    // (Unity stores lightmaps per scene, and the map is a prefab loaded into Game.unity).
    // Not part of the Stage 5 "all" job. Report: Logs/Validation/VanderbiltCampus-bake-probe.txt
    public static class VanderbiltCampusLighting
    {
        const double LimitMinutes = 20;
        const string ScratchFolderName = "BakeProbe (scratch)";
        static string ReportPath => ProjectPath.Report("VanderbiltCampus-bake-probe.txt");
        static double started;
        static string scratchFolder;

        [MenuItem("Tools/Deadly Jenga/Maps/Experimental/Vanderbilt Campus Bake Probe (CPU, off by default)")]
        public static void BakeProbeMenu() => BakeProbeBatch();

        public static void BakeProbeBatch()
        {
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save edited scenes first (the probe opens its own scene).");
                File.WriteAllText(ReportPath, "Campus bake probe " + DateTime.Now.ToString("u") + " (CPU progressive, AO, 2 texels/m, tiles within 120 m of the play-area center)\n");
                var campus = Path.GetDirectoryName(ProjectPath.FolderOf("VanderbiltCampusMap.cs").TrimEnd('/')).Replace('\\', '/');
                scratchFolder = campus + "/" + ScratchFolderName;
                if (!AssetDatabase.IsValidFolder(scratchFolder)) AssetDatabase.CreateFolder(campus, ScratchFolderName);
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPath.Find("Mode_vanderbilt.prefab"));
                var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                var map = root.GetComponent<VanderbiltCampusMap>();
                foreach (Transform tile in root.transform)
                {
                    if (!tile.name.StartsWith("T_")) { if (tile.name == "Visual") tile.gameObject.SetActive(false); continue; }
                    var r = tile.GetComponentsInChildren<Renderer>().FirstOrDefault();
                    bool near = r && Vector2.Distance(new Vector2(r.bounds.center.x, r.bounds.center.z), new Vector2(map.Center.x, map.Center.z)) < 120;
                    tile.gameObject.SetActive(near);
                    if (near) foreach (var t in tile.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic);
                }
                var sun = new GameObject("Probe sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.lightmapBakeType = LightmapBakeType.Mixed;
                sun.transform.rotation = Quaternion.Euler(50, -30, 0);
                var settings = new LightingSettings { lightmapper = LightingSettings.Lightmapper.ProgressiveCPU, lightmapResolution = 2, ao = true, maxBounces = 1, bakedGI = true, realtimeGI = false };
                AssetDatabase.CreateAsset(settings, scratchFolder + "/CampusBakeProbe.lighting");
                Lightmapping.lightingSettings = settings;
                EditorSceneManager.SaveScene(scene, scratchFolder + "/CampusBakeProbe.unity");
                started = EditorApplication.timeSinceStartup;
                if (!Lightmapping.BakeAsync()) throw new InvalidOperationException("Lightmapping.BakeAsync refused to start.");
                EditorApplication.update += Poll;
            }
            catch (Exception e) { Finish("FAIL " + e.Message, 1); }
        }

        static void Poll()
        {
            double minutes = (EditorApplication.timeSinceStartup - started) / 60;
            if (Lightmapping.isRunning && minutes < LimitMinutes) return;
            EditorApplication.update -= Poll;
            if (Lightmapping.isRunning) { Lightmapping.Cancel(); Finish($"FAIL bake did not finish within {LimitMinutes} min (cancelled)", 1); return; }
            Finish($"PASS bake finished in {minutes:F1} min; {LightmapSettings.lightmaps.Length} lightmaps for the tiles near the play area. " +
                "Scale: the whole campus is about 296 tiles, so expect roughly that many times longer.", 0);
        }

        // Writes the result, removes the scratch scene and its lightmaps (only this probe's own folder), exits.
        static void Finish(string line, int code)
        {
            File.AppendAllText(ReportPath, line + "\n");
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (!string.IsNullOrEmpty(scratchFolder) && AssetDatabase.IsValidFolder(scratchFolder)) AssetDatabase.DeleteAsset(scratchFolder);
            }
            catch (Exception e) { File.AppendAllText(ReportPath, "NOTE clean-up: " + e.Message + "\n"); }
            VanderbiltCampusData.Exit(code);
        }
    }
}
