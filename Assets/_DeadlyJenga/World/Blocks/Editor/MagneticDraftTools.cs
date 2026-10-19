using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using Object = UnityEngine.Object;

namespace DeadlyJenga.World.Blocks.Editor
{
    public static class MagneticDraftTools
    {
        const string Key = "MagneticDraft.Check";
        static double started;
        static int stage, errors, idleRecomputations;
        static Vector3 releasedAt;
        static MagneticBlock released;
        static string Report => ProjectPath.Report("MagneticEvan2.txt");
        static void Require(bool condition, string message)
        {
            File.AppendAllText(Report, (condition ? "PASS " : "FAIL ") + message + "\n");
            if (!condition) throw new InvalidOperationException(message);
        }
        [MenuItem("Tools/Deadly Jenga/Maps/Build Evan 2 Magnetic Towers")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save intended scene edits first.");
            var scene = EditorSceneManager.OpenScene(ProjectPath.Find("MapDrafterEvan2.unity"));
            var generator = Object.FindFirstObjectByType<CastleGenerator>();
            var map = generator.GetComponent<MagneticDraftMap>();
            if (!map) map = generator.gameObject.AddComponent<MagneticDraftMap>();
            map.cubePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPath.Find("MagneticCubePlaceholder.prefab"));
            map.catwalkPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPath.Find("CatwalkPlaceholder.prefab"));
            map.weakMaterial = Material("MagneticWeakRed", new Color(.95f, .12f, .06f));
            map.mediumMaterial = Material("MagneticMediumGreen", new Color(.08f, .8f, .22f));
            map.strongMaterial = Material("MagneticStrongPurple", new Color(.56f, .16f, .95f));
            generator.magneticMap = map;
            generator.generateOnStart = true; generator.environmentOnly = false;
            generator.settings.cathedral = false;
            generator.foundation.localScale = new Vector3(36, 1, 36);
            var area = generator.GetComponent<BoxCollider>();
            if (!area) area = generator.gameObject.AddComponent<BoxCollider>();
            area.isTrigger = true; area.center = new Vector3(0, 14, 0); area.size = new Vector3(36, 30, 36);
            generator.playableArea = area;
            EditorUtility.SetDirty(generator); EditorUtility.SetDirty(map);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
        static Material Material(string name, Color color)
        {
            string path = ProjectPath.FolderOf("PlaceholderGray.mat") + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", .25f);
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }
        public static void InstallAndTest()
        {
            Install();
            if (!EditorApplication.ExecuteMenuItem("Tools/Deadly Jenga/Validate Disaster Setup")) throw new InvalidOperationException("Disaster validation menu is unavailable.");
            File.WriteAllText(Report, "Evan 2 magnetic structures\n");
            SessionState.SetBool(Key, true); EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]
        static void Resume()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Key, false) || state != PlayModeStateChange.EnteredPlayMode) return;
                started = EditorApplication.timeSinceStartup; stage = errors = 0;
                Application.logMessageReceived += Log;
                EditorApplication.update += Tick;
            };
        }
        static void Log(string message, string trace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (trace.Contains("UnityEditor.Search.SearchDatabase"))
            {
                File.AppendAllText(Report, "EDITOR INDEXING ISSUE (outside gameplay): " + message + "\n" + trace + "\n");
                return;
            }
            errors++; File.AppendAllText(Report, "ERROR " + message + "\n" + trace + "\n");
        }
        static void Tick()
        {
            if (EditorApplication.timeSinceStartup - started < 4) return;
            try
            {
                var generator = Object.FindFirstObjectByType<CastleGenerator>();
                var structure = generator.magneticMap.Structure;
                if (stage == 3)
                {
                    Require(released.Broken && !released.Body.isKinematic, "A real rigidbody impact breaks the magnetic bridge.");
                    Require(structure.Blocks.Where(b => b.strength == 3).All(b => b.Supported), "Impact breaks the bridge without collapsing neighboring towers.");
                    Require(errors == 0, "No captured runtime errors.");
                    File.AppendAllText(Report, "Blocks: " + generator.Bodies.Count + "; build milliseconds: " + generator.LastBuildMilliseconds + "\n");
                    Finish(0); return;
                }
                if (stage == 0)
                {
                    Require(structure.Blocks.All(b => b.Supported && b.Body.isKinematic), "Every authored piece has a foundation path and holds under gravity.");
                    Require(structure.GetComponentsInChildren<Joint>().Length == 0, "Zero physics joints.");
                    idleRecomputations = structure.Recomputations;
                    released = structure.Blocks.First(b => b.strength == 1 && b.GetComponent<BoxCollider>().size.y < 1);
                    Physics.SyncTransforms();
                    Require(Physics.Raycast(released.Body.position + Vector3.up, Vector3.down, out var hit, 2, 1 << Layers.Surface)
                        && Climbables.TryGet(hit.collider, out var climbable) && climbable.Body == released.Body && climbable.CanClimb,
                        "Attached bridge has its own registered climbable standing surface.");
                    released.GetComponent<CastleBlock>().Push(Vector3.right, null);
                    Require(released.Supported, "Small push leaves weak bridge attached.");
                    Require(structure.Blocks.All(b => b.Body.position == b.transform.position), "Physics poses match the generated map.");
                    stage++; started = EditorApplication.timeSinceStartup; return;
                }
                if (stage == 1)
                {
                    Require(structure.Recomputations == idleRecomputations, "No graph work while idle or below the break threshold.");
                    releasedAt = released.Body.position;
                    released.GetComponent<CastleBlock>().Push(Vector3.right * 3, null);
                    Require(released.Broken && !released.Body.isKinematic, "Weak bridge releases after accumulated damage.");
                    Require(structure.Blocks.Where(b => b.strength == 3).All(b => b.Supported), "Bridge break leaves all strong tower connections intact.");
                    stage++; started = EditorApplication.timeSinceStartup; return;
                }
                Require(released.Body.position.y < releasedAt.y - .5f, "Released bridge falls under real gravity.");
                // Destroy one tower's foundations. Weak cross-links must not keep that tower floating.
                foreach (var b in structure.Blocks.Where(b => b.foundationAnchor && b.transform.localPosition.x < 0 && b.transform.localPosition.z < 0).ToArray()) b.Eliminate(null);
                Require(structure.Blocks.Where(b => b.strength == 3 && b.transform.localPosition.x < -5 && b.transform.localPosition.z < -5).All(b => b.Broken), "Removing tower foundations releases its upper structure despite bridge links.");
                Require(structure.Blocks.Where(b => b.strength == 3 && b.transform.localPosition.x > 5 && b.transform.localPosition.z > 5).All(b => b.Supported), "Other tower stays supported.");
                string saved = generator.SaveLayout(); int count = generator.Bodies.Count;
                int broken = structure.SaveBroken().Length;
                Require(!generator.LoadLayout(saved, count + 1, new Pose(generator.transform.position, generator.transform.rotation), Vector3.one), "Invalid layout count is rejected without rebuilding.");
                Require(generator.LoadLayout(saved, count, new Pose(generator.transform.position, generator.transform.rotation), Vector3.one), "Magnetic layout loads through existing synchronization interface.");
                structure = generator.magneticMap.Structure;
                Require(structure.SaveBroken().Length == broken, "Broken bonds survive layout serialization.");
                generator.SetReplica(true);
                Require(generator.Bodies.All(b => b.isKinematic), "Replica bodies do not simulate independently.");
                generator.SetReplica(false);
                Require(structure.Blocks.All(b => b.Body.isKinematic == b.Supported), "Returning authority preserves supported versus loose pieces.");
                generator.Rebuild();
                Require(generator.magneticMap.Structure.Blocks.All(b => b.Supported && b.Health == b.MaxHealth), "Rebuild restores all magnets and health.");
                released = generator.magneticMap.Structure.Blocks.First(b => b.strength == 1 && b.GetComponent<BoxCollider>().size.y < 1);
                var projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                projectile.transform.position = released.Body.position + Vector3.up * 3;
                var body = projectile.AddComponent<Rigidbody>(); body.mass = 200;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.linearVelocity = Vector3.down * 12;
                stage = 3; started = EditorApplication.timeSinceStartup;
            }
            catch (Exception e) { File.AppendAllText(Report, e + "\n"); Finish(1); }
        }
        static void Finish(int code)
        {
            EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
            SessionState.SetBool(Key, false); EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }
}
