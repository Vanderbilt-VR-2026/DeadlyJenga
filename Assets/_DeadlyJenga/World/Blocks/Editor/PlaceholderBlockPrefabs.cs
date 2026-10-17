using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;

namespace DeadlyJenga.World.Blocks.Editor
{
    public static class PlaceholderBlockPrefabs
    {
        const string CheckKey = "PlaceholderBlocks.Check";
        static double started;
        static int errors;

        [MenuItem("Tools/Deadly Jenga/Blocks/Create Missing Placeholder Prefabs")]
        public static void CreateMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            string materialPath = ProjectPath.FolderOf("Castle Friction.physicMaterial") + "PlaceholderGray.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetColor("_BaseColor", new Color(.5f, .5f, .5f));
                material.SetFloat("_Smoothness", .15f);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            Create("MagneticCubePlaceholder", new Vector3(2, 2, 2), material);
            Create("CatwalkPlaceholder", new Vector3(6, .3f, 2), material);
            AssetDatabase.SaveAssets();
        }

        static void Create(string name, Vector3 size, Material material)
        {
            string path = ProjectPath.FolderOf("CastleBlock.prefab") + name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path)) return;
            var root = new GameObject(name);
            try
            {
                root.layer = Layers.Block;
                var box = root.AddComponent<BoxCollider>();
                box.size = size;
                box.sharedMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(ProjectPath.Find("Castle Friction.physicMaterial"));
                var body = root.AddComponent<Rigidbody>();
                body.mass = 40;
                body.linearDamping = .05f;
                body.angularDamping = .1f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.name = "Visual";
                visual.transform.SetParent(root.transform, false);
                visual.transform.localScale = size;
                UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
                visual.GetComponent<MeshRenderer>().sharedMaterial = material;
                root.AddComponent<CastleBlock>().visualRoot = visual.transform;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        public static void CreateAndCheckGame()
        {
            CreateMissing();
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            SessionState.SetBool(CheckKey, true);
            EditorApplication.EnterPlaymode();
        }

        // Only resume a check explicitly requested by the batch entry point.
        [InitializeOnLoadMethod]
        static void RegisterCheck()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(CheckKey, false) || state != PlayModeStateChange.EnteredPlayMode) return;
                errors = 0;
                Application.logMessageReceived += CountErrors;
                started = EditorApplication.timeSinceStartup;
                EditorApplication.update += FinishCheck;
            };
        }
        static void CountErrors(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
        }
        static void FinishCheck()
        {
            if (EditorApplication.timeSinceStartup - started < 3) return;
            EditorApplication.update -= FinishCheck;
            try
            {
                Check("MagneticCubePlaceholder", new Vector3(2, 2, 2));
                Check("CatwalkPlaceholder", new Vector3(6, .3f, 2));
                if (errors > 0) throw new InvalidOperationException("Game logged runtime errors: " + errors);
                File.WriteAllText(ProjectPath.Report("PlaceholderBlocks.txt"), "PASS: Game entered Play Mode; both prefabs have matching visual/collider dimensions, dynamic physics and registered climbable surfaces.");
            }
            catch (Exception e)
            {
                errors++;
                File.WriteAllText(ProjectPath.Report("PlaceholderBlocks.txt"), "FAIL: " + e);
            }
            Application.logMessageReceived -= CountErrors;
            SessionState.SetBool(CheckKey, false);
            EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.Exit(errors == 0 ? 0 : 1);
        }
        static void Check(string name, Vector3 size)
        {
            string path = ProjectPath.Find(name + ".prefab");
            foreach (string dependency in AssetDatabase.GetDependencies(path))
                if (dependency.Contains("/Presentation/")) throw new InvalidOperationException(name + " depends on art.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var instance = UnityEngine.Object.Instantiate(prefab, new Vector3(1000, 100, 0), Quaternion.identity);
            try
            {
                var block = instance.GetComponent<CastleBlock>();
                var box = instance.GetComponent<BoxCollider>();
                if (box.size != size || block.Visual.localScale != size || block.Body.isKinematic || block.appearance)
                    throw new InvalidOperationException(name + " has incorrect setup.");
                block.SyncSurface();
                Physics.SyncTransforms();
                if (!Physics.Raycast(box.bounds.center + Vector3.up * (size.y / 2 + 1), Vector3.down, out var hit, 2, 1 << Layers.Surface)
                    || !Climbables.TryGet(hit.collider, out var climbable) || !climbable.CanClimb)
                    throw new InvalidOperationException(name + " has no climbable standing surface.");
            }
            finally { UnityEngine.Object.Destroy(instance); }
        }
    }
}
