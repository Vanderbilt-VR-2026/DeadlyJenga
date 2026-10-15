using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadlyJenga.CastlePrototype.Editor
{
    [InitializeOnLoad]
    public static class CastlePrototypeBuilder
    {
        static string ScenePath => ProjectPath.Find("Game.unity");
        static string MaterialRoot => ProjectPath.FolderOf("Corner Jade.mat");
        static string PrefabRoot => ProjectPath.FolderOf("CastleBlock.prefab");
        static CastlePrototypeBuilder()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("CastleValidate", false))
                { SessionState.SetBool("CastleValidate", false); EditorApplication.delayCall += ValidatePhysics; }
            };
        }
        static void FirstImport()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            if (!File.Exists(ScenePath)) Build();
        }

        [MenuItem("Tools/Castle Prototype/Build Test Assets")]
        public static void Build()
        {
            var oldScene = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var corner = Material("Corner Jade", new Color(.18f, .62f, .43f));
                var wall = Material("Wall Terracotta", new Color(.64f, .24f, .18f));
                var keep = Material("Keep Violet", new Color(.43f, .3f, .7f));
                var stone = Material("Foundation Slate", new Color(.19f, .23f, .29f));
                var water = Material("Ocean Blue", new Color(.025f, .2f, .3f));
                string physicsPath = ProjectPath.Find("Castle Friction.physicMaterial");
                var friction = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(physicsPath);
                if (!friction)
                {
                    friction = new PhysicsMaterial("Castle Friction") { staticFriction = .65f, dynamicFriction = .55f, bounciness = 0,
                        frictionCombine = PhysicsMaterialCombine.Average };
                    AssetDatabase.CreateAsset(friction, physicsPath);
                }
                var prefab = CreateBlockPrefab(wall, friction);
                var root = new GameObject("Castle Layout (local prototype)");
                var generator = root.AddComponent<CastleGenerator>();
                generator.generateOnStart = true; generator.blockPrefab = prefab;
                generator.cornerMaterial = corner; generator.wallMaterial = wall; generator.keepMaterial = keep;
                generator.foundation = Box("Fixed Foundation", new Vector3(0, -.5f, 0), new Vector3(35, 1, 35), stone).transform;
                var ocean = Box("Ocean (visual only)", new Vector3(0, -5, 0), new Vector3(600, .1f, 600), water);
                UnityEngine.Object.DestroyImmediate(ocean.GetComponent<Collider>());
                var camera = new GameObject("Prototype Camera").AddComponent<Camera>();
                camera.tag = "MainCamera"; camera.fieldOfView = 48; camera.farClipPlane = 1000;
                camera.transform.position = new Vector3(48, 40, -55);
                camera.transform.LookAt(new Vector3(0, 12, 0));
                camera.backgroundColor = new Color(.12f, .19f, .26f); camera.clearFlags = CameraClearFlags.SolidColor;
                var controls = camera.gameObject.AddComponent<CastleTestControls>(); controls.generator = generator; controls.viewCamera = camera;
                var light = new GameObject("Sun").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 2;
                light.shadows = LightShadows.Soft; light.transform.rotation = Quaternion.Euler(45, -30, 0);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.5f, .55f, .65f);
                generator.Rebuild(); // Saved preview is visible outside Play Mode; Start replaces it once.
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                Debug.Log("Castle prototype scene and assets created: " + ScenePath);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (oldScene.IsValid()) SceneManager.SetActiveScene(oldScene);
            }
            CastlePlayBuilder.Install();
        }

        static GameObject CreateBlockPrefab(Material wall, PhysicsMaterial friction)
        {
            var appearance = AssetDatabase.LoadAssetAtPath<CastleBlockAppearance>(MaterialRoot + "Block Appearance.asset");
            if (!appearance)
            {
                appearance = ScriptableObject.CreateInstance<CastleBlockAppearance>();
                AssetDatabase.CreateAsset(appearance, MaterialRoot + "Block Appearance.asset");
                var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube); mesh.name = "Crate Visual";
                UnityEngine.Object.DestroyImmediate(mesh.GetComponent<Collider>());
                mesh.GetComponent<Renderer>().sharedMaterial = wall;
                appearance.visualPrefab = PrefabUtility.SaveAsPrefabAsset(mesh, ProjectPath.Find("Crate Visual.prefab"));
                UnityEngine.Object.DestroyImmediate(mesh); EditorUtility.SetDirty(appearance);
            }
            var cube = new GameObject("CastleBlock"); cube.layer = CastleBlock.BlockLayer;
            cube.AddComponent<BoxCollider>().sharedMaterial = friction;
            var body = cube.AddComponent<Rigidbody>(); body.mass = 40;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearDamping = .05f; body.angularDamping = .1f;
            var visual = new GameObject("Visual (replace artwork here)"); visual.transform.SetParent(cube.transform, false);
            var block = cube.AddComponent<CastleBlock>(); block.visualRoot = visual.transform;
            block.appearance = appearance; block.sectionMaterial = wall; block.RefreshAppearance();
            var prefab = PrefabUtility.SaveAsPrefabAsset(cube, PrefabRoot + "CastleBlock.prefab");
            UnityEngine.Object.DestroyImmediate(cube); return prefab;
        }

        [MenuItem("Tools/Castle Prototype/Align Keep With Confirmed Layout")]
        public static void AlignKeep()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            var gen = UnityEngine.Object.FindFirstObjectByType<CastleGenerator>();
            var layout = CastleLayout.Build(gen.settings);
            var blocks = gen.GetComponentsInChildren<CastleBlock>(true);
            int restored = 0;
            for (int i = 0; i < layout.Count; i++)
            {
                var p = layout[i];
                if (p.Section != CastleSection.Keep) continue;
                string expected = $"{p.Section}_Course{p.Course:00}_Block{i:000}";
                foreach (var block in blocks)
                {
                    if (block.name != expected) continue;
                    Undo.RecordObject(block.transform, "Restore top crate position");
                    block.transform.SetPositionAndRotation(gen.transform.TransformPoint(p.Position), gen.transform.rotation * p.Rotation);
                    var body = block.GetComponent<Rigidbody>(); body.position = block.transform.position; body.rotation = block.transform.rotation;
                    // The user's experiment moved the artwork, not the physics roots.
                    if (p.Course == gen.settings.keepCourses - 1)
                    foreach (Transform artwork in block.visualRoot)
                    {
                        Undo.RecordObject(artwork, "Align top crate artwork with physics");
                        artwork.localPosition = Vector3.zero;
                    }
                    restored++;
                }
            }
            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(gen.gameObject.scene); EditorSceneManager.SaveScene(gen.gameObject.scene);
            Debug.Log($"Aligned {restored} keep crate poses and the top four visuals; materials, shaders, artwork and environment were preserved.");
        }

        [MenuItem("Tools/Castle Prototype/Apply Confirmed Structure %#F6")]
        public static void ApplyConfirmedStructure()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tags.FindProperty("layers");
            string[] names = { "Castle Survivor", "Castle Block", "Castle Survivor Surface" };
            for (int i = 0; i < names.Length; i++)
            {
                var item = layers.GetArrayElementAtIndex(8 + i);
                if (item.stringValue != "" && item.stringValue != names[i]) throw new InvalidOperationException("Castle layer slot already occupied.");
                item.stringValue = names[i];
            }
            tags.ApplyModifiedProperties();
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath);
            var prefab = CreateBlockPrefab(AssetDatabase.LoadAssetAtPath<Material>(MaterialRoot + "Wall Terracotta.mat"),
                AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(ProjectPath.Find("Castle Friction.physicMaterial")));
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.TryGetComponent<CastleGenerator>(out var gen))
                { gen.settings = new CastleSettings(); gen.blockPrefab = prefab; gen.Rebuild(); }
            }
            var player = PrefabUtility.LoadPrefabContents(ProjectPath.Find("CastleSurvivor.prefab"));
            try
            {
                player.layer = CastleBlock.SurvivorLayer;
                player.GetComponent<CastleSurvivor>().jumpHeight = 1.82f;
                player.GetComponent<CharacterController>().excludeLayers = 1 << CastleBlock.BlockLayer;
                PrefabUtility.SaveAsPrefabAsset(player, ProjectPath.Find("CastleSurvivor.prefab"));
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("Confirmed structure installed: identical 6x2x2 crates, octagonal A/B keep, paired outer towers, separated artwork and survivor collision.");
        }

        static Material Material(string name, Color color)
        {
            string path = MaterialRoot + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (!shader) throw new InvalidOperationException("URP/Lit shader unavailable.");
                material = new Material(shader); AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", .22f);
            return material;
        }
        static GameObject Box(string name, Vector3 position, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.position = position; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material; return go;
        }

        [MenuItem("Tools/Castle Prototype/Open Test Scene")]
        public static void Open()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Tools/Castle Prototype/Validate Layout and Physics %#F5")]
        public static void ValidatePhysics()
        {
            if (!Application.isPlaying)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(ScenePath);
                SessionState.SetBool("CastleValidate", true);
                EditorApplication.EnterPlaymode();
                return;
            }
            var scene = SceneManager.CreateScene("Castle isolated validation", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            var previous = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(scene);
            string report;
            try
            {
                var go = new GameObject("Validation Castle"); SceneManager.MoveGameObjectToScene(go, scene);
                var gen = go.AddComponent<CastleGenerator>(); gen.generateOnStart = false;
                gen.blockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + "CastleBlock.prefab");
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); SceneManager.MoveGameObjectToScene(floor, scene);
                floor.transform.position = new Vector3(0, -.5f, 0); floor.transform.localScale = new Vector3(CastleLayout.Footprint(gen.settings) + 4, 1, CastleLayout.Footprint(gen.settings) + 4);
                gen.Rebuild();
                var positions = new Vector3[gen.Bodies.Count];
                int overlaps = 0;
                for (int i = 0; i < positions.Length; i++)
                {
                    positions[i] = gen.Bodies[i].position;
                    var a = gen.Bodies[i].GetComponent<BoxCollider>();
                    for (int j = 0; j < i; j++)
                    {
                        var b = gen.Bodies[j].GetComponent<BoxCollider>();
                        if (a.bounds.Intersects(b.bounds) && Physics.ComputePenetration(a, a.transform.position, a.transform.rotation,
                            b, b.transform.position, b.transform.rotation, out _, out float depth) && depth > .001f) overlaps++;
                    }
                }
                var physics = scene.GetPhysicsScene();
                for (int tick = 0; tick < 500; tick++) physics.Simulate(.02f);
                float maxDrift = 0; int moved = 0;
                for (int i = 0; i < positions.Length; i++)
                {
                    float drift = Vector3.Distance(positions[i], gen.Bodies[i].position);
                    maxDrift = Mathf.Max(maxDrift, drift); if (drift > .25f) moved++;
                }
                // Remove both bottom supports of the first corner. Gravity must bring down its upper stack.
                Vector3 upperBefore = gen.Bodies[2].position;
                UnityEngine.Object.DestroyImmediate(gen.Bodies[0].gameObject);
                UnityEngine.Object.DestroyImmediate(gen.Bodies[1].gameObject);
                for (int tick = 0; tick < 150; tick++) physics.Simulate(.02f);
                float drop = upperBefore.y - gen.Bodies[2].position.y;
                bool pass = overlaps == 0 && moved == 0 && drop > .5f;
                report = $"{(pass ? "PASS" : "FAIL")} default castle validation\nBlocks: {positions.Length}\nInitial penetrations > 1mm: {overlaps}\n"
                    + $"10 seconds gravity simulation: maximum position drift {maxDrift:F4} m; blocks moving > 0.25m: {moved}\n"
                    + $"After removing first corner's two bottom supports: next block dropped {drop:F3}m in 3 seconds\n"
                    + "Validation uses an isolated Unity PhysicsScene at 50Hz. This is not headset or multiplayer performance validation.\n";
            }
            catch (Exception e) { report = "FAIL validation exception: " + e; }
            finally { SceneManager.SetActiveScene(previous); SceneManager.UnloadSceneAsync(scene); }
            File.WriteAllText(ProjectPath.Report("Validation.txt"), report); AssetDatabase.Refresh(); Debug.Log(report);
        }
    }
}
