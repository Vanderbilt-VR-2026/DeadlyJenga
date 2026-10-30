using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadlyJenga.CastlePrototype.Editor
{
    public static class DraftMapCleanup
    {
        static readonly string[] EmptyCandidates = { "MapDrafterLuke1", "MapDrafterLuke2", "MapDrafterKaleb1", "MapDrafterKaleb2" };

        [MenuItem("Tools/Deadly Jenga/Maps/Remove Empty Drafts and Create Andy")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            if (setup.Any(s => s.isLoaded && SceneManager.GetSceneByPath(s.path).isDirty))
                throw new InvalidOperationException("Save edited scenes before cleaning draft maps.");
            var report = new System.Text.StringBuilder("Requested empty-draft cleanup and Andy setup\n");
            var paths = new List<string>();
            var rootNames = new HashSet<string> { "Fixed Foundation", "Sun", "Draft Demo Session", "Prototype Camera", "Castle Layout (local prototype)", "Candy Environment" };
            try
            {
                foreach (string name in EmptyCandidates)
                {
                    string path;
                    try { path = ProjectPath.Find(name + ".unity"); }
                    catch (FileNotFoundException) { continue; }
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var generator = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).SingleOrDefault(c => c is IWorldQuery);
                    var data = generator ? new SerializedObject(generator) : null;
                    bool empty = data != null && data.FindProperty("environmentOnly")?.boolValue == true && data.FindProperty("generateOnStart")?.boolValue == false && data.FindProperty("magneticMap")?.objectReferenceValue == null;
                    empty &= scene.GetRootGameObjects().All(r => rootNames.Contains(r.name));
                    empty &= scene.GetRootGameObjects().All(r => r.GetComponentsInChildren<Rigidbody>(true).Length == 0);
                    var foundation = data?.FindProperty("foundation")?.objectReferenceValue as Transform;
                    empty &= generator && generator.transform.Cast<Transform>().All(t => t == foundation);
                    if (!empty) throw new InvalidOperationException("Preserving changed/nonempty draft: " + path +
                        " roots=" + string.Join(",", scene.GetRootGameObjects().Select(r=>r.name+":"+r.hideFlags)) +
                        " bodies="+scene.GetRootGameObjects().Sum(r=>r.GetComponentsInChildren<Rigidbody>(true).Length)+
                        " world="+(generator?generator.GetType().Name:"missing")+
                        " environment="+data?.FindProperty("environmentOnly")?.boolValue+
                        " generation="+data?.FindProperty("generateOnStart")?.boolValue+
                        " children="+(generator?string.Join(",",generator.transform.Cast<Transform>().Select(t=>t.name)):"none"));
                    paths.Add(path);
                    report.AppendLine("VERIFIED EMPTY " + path);
                }
                string folder = ProjectPath.FolderOf("MapDrafterEvan1.unity").TrimEnd('/');
                string andyPath = folder + "/Andy.unity";
                if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(andyPath))
                {
                    if (paths.Count == 0) throw new InvalidOperationException("No verified blank template is available to create Andy.");
                    if (!AssetDatabase.CopyAsset(paths[0], andyPath)) throw new InvalidOperationException("Could not copy blank draft to Andy.");
                    AssetDatabase.ImportAsset(andyPath, ImportAssetOptions.ForceSynchronousImport);
                }
                var andy = EditorSceneManager.OpenScene(andyPath, OpenSceneMode.Single);
                var world = andy.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).Single(c => c is IWorldQuery);
                world.name = "Andy Island";
                var andyData = new SerializedObject(world);
                andyData.FindProperty("environmentOnly").boolValue = true;
                andyData.FindProperty("generateOnStart").boolValue = false;
                andyData.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(andy);
                var catalog = Resources.Load<IslandLobbyCatalog>("IslandLobby/Catalog");
                if (!catalog) throw new InvalidOperationException("Missing lobby catalog.");
                var maps = catalog.maps.Where(m => m != null && !paths.Contains(m.scenePath)).ToList();
                var entry = maps.Find(m => m.id == "andy");
                if (entry == null) { entry = new IslandLobbyCatalog.Map { id = "andy" }; maps.Add(entry); }
                entry.title = "Andy"; entry.scenePath = andyPath;
                entry.mapPrefab = null; entry.recommendedMode = "demo"; entry.recommendedPlayers = 1; entry.walkableIsland = true;
                catalog.maps = maps.ToArray(); EditorUtility.SetDirty(catalog);
                var builds = EditorBuildSettings.scenes.Where(s => !paths.Contains(s.path) && s.path != andyPath).ToList();
                builds.Add(new EditorBuildSettingsScene(andyPath, true));
                EditorBuildSettings.scenes = builds.ToArray();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                foreach (string path in paths)
                {
                    if (!AssetDatabase.DeleteAsset(path)) throw new InvalidOperationException("Could not delete verified empty draft: " + path);
                    report.AppendLine("REMOVED " + path);
                }
                AssetDatabase.SaveAssets();
                report.AppendLine("CREATED Andy: blank playable island, Demo default, walkable ground.");
                report.AppendLine("PRESERVED MapDrafterEvan1 and MapDrafterEvan2.");
                report.AppendLine("PASS catalog and build scenes reference Andy and no deleted drafts.");
                File.WriteAllText(ProjectPath.Report("DraftMapCleanup.txt"), report.ToString());
                Debug.Log(report.ToString());
            }
            finally
            {
                var remaining = setup.Where(s => string.IsNullOrEmpty(s.path) || File.Exists(s.path)).ToArray();
                if (remaining.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(remaining);
            }
        }
    }
}
