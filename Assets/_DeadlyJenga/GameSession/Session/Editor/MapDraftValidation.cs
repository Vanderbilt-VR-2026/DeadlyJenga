using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeadlyJenga.Core;
using Object = UnityEngine.Object;

namespace DeadlyJenga.CastlePrototype.Editor
{
    [InitializeOnLoad]
    public static class MapDraftValidation
    {
        const string Key = "MapDraftValidation";
        static string[] Drafts => AssetDatabase.FindAssets("t:Scene")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name.StartsWith("MapDrafter", StringComparison.Ordinal) || name == "Andy")
            .OrderBy(name => name, StringComparer.Ordinal).ToArray();
        static readonly string[] Scenes = Drafts.Concat(new[] { "Game", "Lobby" }).ToArray();
        static double started;
        static bool checking;
        static string Report => ProjectPath.Report("MapDraftValidation.txt");

        static MapDraftValidation()
        {
            Application.logMessageReceived += (message, stack, type) =>
            {
                if (SessionState.GetBool(Key, false) && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                {
                    SessionState.SetBool(Key + "Failed", true);
                    File.AppendAllText(Report, "ERROR " + message + "\n" + stack + "\n");
                }
            };
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Key, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                { started = EditorApplication.timeSinceStartup; checking = true; }
                if (state == PlayModeStateChange.EnteredEditMode)
                    EditorApplication.delayCall += Next;
            };
            EditorApplication.update += Tick;
        }

        [MenuItem("Tools/Deadly Jenga/Maps/Register Playable Drafts and Validate")]
        public static void SetupAndRun()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            var source = new SerializedObject(Object.FindFirstObjectByType<CastlePlaySession>());
            var survivor = source.FindProperty("survivorPrefab").objectReferenceValue;
            var hand = source.FindProperty("handPrefab").objectReferenceValue;
            string survivorPath = AssetDatabase.GetAssetPath(survivor), handPath = AssetDatabase.GetAssetPath(hand);
            var survivorType = survivor.GetType();
            var handType = hand.GetType();
            var catalog = Resources.Load<IslandLobbyCatalog>("IslandLobby/Catalog");
            if (!catalog) throw new InvalidOperationException("Missing island lobby catalog.");
            var maps = catalog.maps.ToList();
            var buildScenes = EditorBuildSettings.scenes.ToList();
            foreach (string name in Drafts)
            {
                string path = ProjectPath.Find(name + ".unity");
                var scene = EditorSceneManager.OpenScene(path);
                var world = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(x => x is IWorldQuery);
                var settings = new SerializedObject(world);
                bool hasContent=settings.FindProperty("settings.cathedral")?.boolValue==true || settings.FindProperty("magneticMap")?.objectReferenceValue!=null;
                settings.FindProperty("environmentOnly").boolValue = !hasContent;
                settings.FindProperty("generateOnStart").boolValue = hasContent;
                settings.ApplyModifiedPropertiesWithoutUndo();
                var session = Object.FindFirstObjectByType<CastlePlaySession>();
                if (!session) session = new GameObject("Draft Demo Session").AddComponent<CastlePlaySession>();
                var data = new SerializedObject(session);
                data.FindProperty("generator").objectReferenceValue = world;
                data.FindProperty("survivorPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(survivorPath).GetComponent(survivorType);
                data.FindProperty("handPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(handPath).GetComponent(handType);
                data.FindProperty("overviewCamera").objectReferenceValue = Camera.main;
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(scene);
                var entry = maps.Find(x => x.id == name || x.scenePath == path);
                if (entry == null) { entry = new IslandLobbyCatalog.Map { id = name }; maps.Add(entry); }
                entry.title = name;
                entry.scenePath = path;
                var build = buildScenes.Find(x => x.path == path);
                if (build == null) buildScenes.Add(new EditorBuildSettingsScene(path, true)); else build.enabled = true;
            }
            catalog = Resources.Load<IslandLobbyCatalog>("IslandLobby/Catalog");
            catalog.maps = maps.ToArray();
            EditorUtility.SetDirty(catalog);
            EditorBuildSettings.scenes = buildScenes.ToArray();
            AssetDatabase.SaveAssets();
            Run();
        }

        [MenuItem("Tools/Deadly Jenga/Maps/Validate Drafts and Lobby")]
        public static void Run()
        {
            File.WriteAllText(Report, "Draft maps, Game and lobby Play Mode validation\n");
            SessionState.SetBool(Key + "Failed", false);
            SessionState.SetInt(Key + "Index", -1);
            SessionState.SetBool(Key, true);
            if (!EditorApplication.ExecuteMenuItem("Tools/Deadly Jenga/Validate Disaster Setup"))
                throw new InvalidOperationException("Disaster validation menu is unavailable.");
            Next();
        }

        static void Next()
        {
            int index = SessionState.GetInt(Key + "Index", -1) + 1;
            if (SessionState.GetBool(Key + "Failed", false) || index == Scenes.Length)
            {
                bool failed = SessionState.GetBool(Key + "Failed", false);
                File.AppendAllText(Report, failed ? "FAILED\n" : "PASSED: all " + Scenes.Length + " scenes; no runtime errors.\n");
                SessionState.SetBool(Key, false);
                if (Application.isBatchMode) EditorApplication.Exit(failed ? 1 : 0);
                return;
            }
            SessionState.SetInt(Key + "Index", index);
            EditorSceneManager.OpenScene(ProjectPath.Find(Scenes[index] + ".unity"));
            EditorApplication.EnterPlaymode();
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            File.AppendAllText(Report, "PASS " + SceneManager.GetActiveScene().name + ": " + message + "\n");
        }

        static void Tick()
        {
            if (!checking || !EditorApplication.isPlaying || EditorApplication.timeSinceStartup - started < 6) return;
            checking = false;
            try
            {
                int index = SessionState.GetInt(Key + "Index", 0);
                var world = Services.World;
                Require(world != null && world.PlayableBounds.size.sqrMagnitude > 0, "world has valid playable bounds");
                Require(RenderSettings.skybox, "skybox is assigned");
                Require(GameObject.Find("Survivor (local)") || GameObject.Find("Lobby survivor (current character prefab)"), "local player spawns");
                if(Scenes[index]=="MapDrafterEvan1")
                {
                    Require(world.TopHeight>=120,"Evan 1 cathedral reports its tower height");
                    Require(((ISyncedWorld)world).SaveLayout().Contains("\"cathedral\":true"),"Evan 1 preserves its cathedral layout in network settings");
                    Require(world.Blocks.All(b=>b&&!b.isKinematic&&b.useGravity),"Evan 1 uses dynamic prefab blocks");
                }
                else if (Scenes[index]=="MapDrafterEvan2")
                    Require(world.Blocks.Count>0,"Evan 2 preserves its authored magnetic structure");
                else if (index < Drafts.Length)
                {
                    Require(world.Blocks.Count == 0, "map starts without blocks");
                    Require(Mathf.Abs(world.TopHeight-world.SafeSpawnPoint().y)<.01f,"empty map reports its actual ground height, not the absent tower");
                    Require(Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Any(x => x.name == "Submerged Sandbar" && x.sharedMesh && x.sharedMesh.vertexCount > 100), "sand terrain generates");
                    Require(Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Any(x => x.name == "Ocean" && x.sharedMesh && x.sharedMesh.vertexCount > 100), "ocean mesh generates");
                    var synced = (ISyncedWorld)world;
                    Require(synced.LoadLayout(synced.SaveLayout(), 0, new Pose(synced.Root.position, synced.Root.rotation), synced.Root.localScale), "empty network layout round-trips");
                    Object.FindFirstObjectByType<CastlePlaySession>().ResetCastle();
                    Require(world.Blocks.Count == 0, "rebuild preserves empty map");
                    Require(!synced.LoadLayout(synced.SaveLayout(), 1, new Pose(synced.Root.position, synced.Root.rotation), synced.Root.localScale), "nonempty network layout is rejected");
                }
                else if (Scenes[index] == "Game") Require(world.Blocks.Count > 0, "castle still generates blocks");
                else
                {
                    var lobby = Object.FindFirstObjectByType<IslandLobby>();
                    foreach (string name in Drafts)
                    {
                        var entry = lobby.catalog.maps.Single(x => Path.GetFileNameWithoutExtension(x.scenePath) == name);
                        Require(SceneUtility.GetBuildIndexByScenePath(entry.scenePath) >= 0, name + " is registered for launch");
                        Require(lobby.GetComponentsInChildren<RectTransform>(true).Any(x=>x.name=="map-current"),name+" is available through the compact map selector");
                    }
                    Require(lobby.catalog.maps.Any(x => x.id == "castle"), "original castle remains selectable");
                }
            }
            catch (Exception error)
            {
                SessionState.SetBool(Key + "Failed", true);
                File.AppendAllText(Report, "FAIL " + error + "\n");
            }
            EditorApplication.ExitPlaymode();
        }
    }
}
