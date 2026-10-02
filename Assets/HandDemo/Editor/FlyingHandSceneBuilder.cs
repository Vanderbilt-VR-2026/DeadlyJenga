using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using Unity.XR.CoreUtils;

namespace FlyingHand.Editor
{
    public static class FlyingHandSceneBuilder
    {
        static Material white;
        static Transform visuals, colliders;
        static GameObject Child(string name, Transform parent, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            return go;
        }
        static Material Material(string name, Color color)
        {
            string path = "Assets/HandDemo/Materials/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
                var template = pipeline ? pipeline.defaultMaterial : null;
                mat = template ? new Material(template) : new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", .22f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", .22f);
            EditorUtility.SetDirty(mat); return mat;
        }
        static void Segment(string name, Vector3 a, Vector3 b, float radius)
        {
            Vector3 mid = (a + b) / 2;
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, b - a);
            float height = Vector3.Distance(a, b) + radius * 2;
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            mesh.name = name; mesh.transform.SetParent(visuals, false);
            mesh.transform.localPosition = mid; mesh.transform.localRotation = rotation;
            mesh.transform.localScale = new Vector3(radius * 2, height / 2, radius * 2);
            Object.DestroyImmediate(mesh.GetComponent<Collider>());
            mesh.GetComponent<Renderer>().sharedMaterial = white;
            var col = Child(name + " Collider", colliders, mid).AddComponent<CapsuleCollider>();
            col.transform.localRotation = rotation; col.radius = radius; col.height = height;
        }
        static void Palm()
        {
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mesh.name = "Palm"; mesh.transform.SetParent(visuals, false);
            mesh.transform.localScale = new Vector3(1.9f, .7f, 1.95f);
            Object.DestroyImmediate(mesh.GetComponent<Collider>());
            mesh.GetComponent<Renderer>().sharedMaterial = white;
            var col = Child("Palm Collider", colliders, Vector3.zero).AddComponent<BoxCollider>();
            col.size = new Vector3(1.7f, .6f, 1.65f);
        }
        static void Track(GameObject go, string device)
        {
            var driver = go.GetComponent<TrackedPoseDriver>() ?? go.AddComponent<TrackedPoseDriver>();
            driver.positionInput = new InputActionProperty(new InputAction("Position", InputActionType.Value, device + "/devicePosition", expectedControlType: "Vector3"));
            driver.rotationInput = new InputActionProperty(new InputAction("Rotation", InputActionType.Value, device + "/deviceRotation", expectedControlType: "Quaternion"));
            driver.trackingStateInput = new InputActionProperty(new InputAction("Tracking State", InputActionType.Value, device + "/trackingState", expectedControlType: "Integer"));
            driver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
        }
        [MenuItem("Tools/Flying Hand/Build in Current Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (Application.isPlaying || UnityEngine.SceneManagement.SceneManager.sceneCount != 1 || string.IsNullOrEmpty(scene.path) || !scene.path.StartsWith("Assets/HandDemo/Scenes/", System.StringComparison.Ordinal))
                throw new System.InvalidOperationException("Open a saved scene in Assets/HandDemo/Scenes before building. Other project scenes are not modified.");
            if (Object.FindObjectsByType<XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length > 1 ||
                Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length > 1)
                throw new System.InvalidOperationException("Resolve multiple cameras/XR Origins before building the demo.");
            if (Object.FindFirstObjectByType<FlyingHandController>(FindObjectsInactive.Include) || GameObject.Find("FlyingHandPlayer")) throw new System.InvalidOperationException("FlyingHandPlayer already exists; refusing duplicate build.");
            EnsureFolder("Assets/HandDemo");
            EnsureFolder("Assets/HandDemo/Materials");
            EnsureFolder("Assets/HandDemo/Prefabs");
            EnsureFolder("Assets/HandDemo/Scenes");
            if (!Object.FindFirstObjectByType<Light>())
            {
                var light = new GameObject("Directional Light").AddComponent<Light>();
                light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(50, -30, 0);
            }
            white = Material("Matte White Hand", Color.white);
            var player = new GameObject("FlyingHandPlayer");
            player.transform.position = new Vector3(0, 3, 0);
            var body = player.AddComponent<Rigidbody>();
            var controller = player.AddComponent<FlyingHandController>(); controller.ConfigureBody();
            visuals = Child("HandVisual", player.transform, Vector3.zero).transform;
            colliders = Child("HandColliders", player.transform, Vector3.zero).transform;
            Palm();
            Segment("Wrist and forearm stump", new Vector3(0, 0, -1.85f), new Vector3(0, 0, -.65f), .48f);
            Segment("Index proximal", new Vector3(-.65f, 0, .65f), new Vector3(-.65f, 0, 1.5f), .23f);
            Segment("Index middle", new Vector3(-.65f, 0, 1.5f), new Vector3(-.65f, 0, 2.02f), .21f);
            Segment("Index fingertip", new Vector3(-.65f, 0, 2.02f), new Vector3(-.65f, 0, 2.27f), .19f);
            for (int i = 0; i < 3; i++)
            {
                float x = -.13f + i * .47f, z = .75f - i * .12f, r = .225f - i * .015f;
                string finger = new[] { "Middle", "Ring", "Little" }[i];
                Segment(finger + " knuckle", new Vector3(x, .02f, z), new Vector3(x, -.18f, z + .32f), r);
                Segment(finger + " curl", new Vector3(x, -.18f, z + .32f), new Vector3(x, -.55f, z + .16f), r * .94f);
                Segment(finger + " folded tip", new Vector3(x, -.55f, z + .16f), new Vector3(x, -.48f, z - .23f), r * .85f);
            }
            Segment("Thumb base", new Vector3(-.7f, -.04f, -.42f), new Vector3(-1.04f, -.2f, .05f), .28f);
            Segment("Thumb folded tip", new Vector3(-1.04f, -.2f, .05f), new Vector3(-.62f, -.46f, .48f), .24f);
            var target = Child("CameraFollowTarget", player.transform, Vector3.zero).transform;
            controller.ConfigureBody();
            var rig = Object.FindFirstObjectByType<XROrigin>();
            if (!rig)
            {
                rig = new GameObject("XR Origin").AddComponent<XROrigin>();
                var offset = Child("Camera Offset", rig.transform, Vector3.zero);
                var cam = Camera.main;
                if (!cam) { cam = new GameObject("Main Camera").AddComponent<Camera>(); cam.tag = "MainCamera"; cam.gameObject.AddComponent<AudioListener>(); }
                cam.transform.SetParent(offset.transform, false);
                cam.transform.localPosition = Vector3.zero; cam.transform.localRotation = Quaternion.identity;
                cam.nearClipPlane = .05f; cam.farClipPlane = 500;
                Track(cam.gameObject, "<XRHMD>");
                Track(Child("Left Controller", offset.transform, Vector3.zero), "<XRController>{LeftHand}");
                Track(Child("Right Controller", offset.transform, Vector3.zero), "<XRController>{RightHand}");
                rig.Camera = cam; rig.Origin = rig.gameObject; rig.CameraFloorOffsetObject = offset;
                rig.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device; rig.CameraYOffset = 1f;
            }
            var follow = rig.gameObject.AddComponent<ThirdPersonVRFollow>(); follow.target = target;
            follow.controller = controller; follow.viewCamera = rig.Camera; follow.SnapToTarget();
            var area = new GameObject("FlyingHand Physics Test Area");
            var groundMat = Material("Ground Slate", new Color(.16f, .22f, .28f));
            var cubeMat = Material("Test Crates Orange", new Color(.95f, .38f, .09f));
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube); ground.name = "Ground 80m";
            ground.transform.SetParent(area.transform); ground.transform.position = new Vector3(0, -.5f, 12); ground.transform.localScale = new Vector3(80, 1, 80);
            ground.GetComponent<Renderer>().sharedMaterial = groundMat;
            for (int stack = 0; stack < 3; stack++)
                for (int level = 0; level < 4; level++)
                    Crate(area.transform, "Stack " + stack + " Level " + level, new Vector3((stack - 1) * 3, .55f + level * 1.1f, 10), Vector3.one * 1.05f, 30, cubeMat);
            Crate(area.transform, "Heavy Crate", new Vector3(4, 1, 5), Vector3.one * 2, 160, cubeMat);
            Crate(area.transform, "Small Crate", new Vector3(-3, .4f, 5), Vector3.one * .8f, 15, cubeMat);
            Crate(area.transform, "Tall Crate", new Vector3(-5, 1.5f, 9), new Vector3(1, 3, 1), 60, cubeMat);
            // Project XR configuration belongs to the host project. Importing or
            // rebuilding this demo must not silently change its OpenXR features.
            PrefabUtility.SaveAsPrefabAsset(player, "Assets/HandDemo/Prefabs/FlyingHandPlayer.prefab");
            EditorSceneManager.MarkSceneDirty(player.scene);
            string scenePath = string.IsNullOrEmpty(player.scene.path) ? "Assets/HandDemo/Scenes/FlyingHandQuest.unity" : player.scene.path;
            EditorSceneManager.SaveScene(player.scene, scenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = player;
            if (SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.Frame(new Bounds(new Vector3(0, 2, 4), new Vector3(18, 10, 22)), false);
            Debug.Log("FlyingHand build complete: " + scenePath);
        }
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int separator = path.LastIndexOf('/');
            string parent = path.Substring(0, separator);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(separator + 1));
        }
        static void Crate(Transform parent, string name, Vector3 position, Vector3 size, float mass, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent);
            go.transform.position = position; go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = mat;
            var rb = go.AddComponent<Rigidbody>(); rb.mass = mass; rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous; rb.solverIterations = 10;
        }
    }
}
