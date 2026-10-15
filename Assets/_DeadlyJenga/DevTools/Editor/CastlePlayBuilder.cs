using FlyingHand;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;

namespace DeadlyJenga.CastlePrototype.Editor
{
    [InitializeOnLoad]
    public static class CastlePlayBuilder
    {
        static string ScenePath => ProjectPath.Find("Game.unity");
        static string PrefabRoot => ProjectPath.FolderOf("CastleSurvivor.prefab");
        static CastlePlayBuilder() {}
        static void FirstImport()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
            FixHandMaterial();
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + "CastleSurvivor.prefab")) Install();
        }
        static void FixHandMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(ProjectPath.Find("Matte White Hand.mat"));
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (material && shader && material.shader != shader)
            {
                material.shader = shader; material.SetColor("_BaseColor", Color.white); material.SetFloat("_Smoothness", .22f);
                EditorUtility.SetDirty(material); AssetDatabase.SaveAssets();
            }
        }
        [MenuItem("Tools/Castle Prototype/Configure Headset Support %#F7")]
        public static void ConfigureXR()
        {
            foreach (var group in new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android })
            {
                var settings = UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(group);
                if (!settings || !settings.Manager) throw new System.InvalidOperationException("XR settings missing for " + group);
                if (!UnityEditor.XR.Management.Metadata.XRPackageMetadataStore.AssignLoader(settings.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", group))
                    throw new System.InvalidOperationException("Could not assign OpenXR for " + group);
                settings.InitManagerOnStart = true;
                var xr = UnityEngine.XR.OpenXR.OpenXRSettings.GetSettingsForBuildTargetGroup(group);
                var touch = xr.GetFeature<UnityEngine.XR.OpenXR.Features.Interactions.OculusTouchControllerProfile>();
                if (touch) { touch.enabled = true; EditorUtility.SetDirty(touch); }
                EditorUtility.SetDirty(settings); EditorUtility.SetDirty(settings.Manager); EditorUtility.SetDirty(xr);
            }
            FixHandMaterial(); AssetDatabase.SaveAssets();
            Debug.Log("OpenXR and Touch controls enabled for desktop/Android. No running display falls back to desktop.");
        }
        static Transform Child(string name, Transform parent)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform;
        }
        static Camera Rig(GameObject root, out Transform offset)
        {
            var origin = root.AddComponent<XROrigin>(); origin.enabled = false;
            offset = Child("Camera Offset", root.transform);
            var cam = Child("View Camera", offset).gameObject.AddComponent<Camera>();
            cam.tag = "MainCamera"; cam.nearClipPlane = .03f; cam.farClipPlane = 700; cam.fieldOfView = 75;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.12f, .19f, .26f);
            cam.gameObject.AddComponent<AudioListener>();
            var tracker = cam.gameObject.AddComponent<TrackedPoseDriver>();
            tracker.positionInput = new InputActionProperty(new InputAction("Head Position", InputActionType.Value, "<XRHMD>/centerEyePosition", expectedControlType: "Vector3"));
            tracker.rotationInput = new InputActionProperty(new InputAction("Head Rotation", InputActionType.Value, "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion"));
            tracker.trackingStateInput = new InputActionProperty(new InputAction("Head Tracking", InputActionType.Value, "<XRHMD>/trackingState", expectedControlType: "Integer"));
            tracker.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            CastleSurvivor.ConfigureHeadTracking(tracker);
            origin.Camera = cam; origin.Origin = root; origin.CameraFloorOffsetObject = offset.gameObject;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor; origin.CameraYOffset = 1.6f;
            return cam;
        }
        [MenuItem("Tools/Castle Prototype/Install Player and Hand Prefabs")]
        public static void Install()
        {
            if (Application.isPlaying) throw new System.InvalidOperationException("Exit Play Mode before editing demo prefabs.");
            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var playerPrefab = AssetDatabase.LoadAssetAtPath<CastleSurvivor>(PrefabRoot + "CastleSurvivor.prefab");
                if (!playerPrefab)
                {
                    var go = new GameObject("CastleSurvivor"); go.SetActive(false); go.layer = 2;
                    var capsule = go.AddComponent<CharacterController>(); capsule.height = .85f; capsule.radius = .12f;
                    capsule.center = Vector3.up * .425f; capsule.stepOffset = .025f; capsule.skinWidth = .0075f;
                    capsule.minMoveDistance = 0; capsule.slopeLimit = 48;
                    var cam = Rig(go, out var offset);
                    var motor = go.AddComponent<CastleSurvivor>(); motor.viewCamera = cam; motor.cameraOffset = offset;
                    go.SetActive(true);
                    playerPrefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabRoot + "CastleSurvivor.prefab").GetComponent<CastleSurvivor>();
                    Object.DestroyImmediate(go);
                }
                var handPrefab = AssetDatabase.LoadAssetAtPath<CastleHandActor>(ProjectPath.Find("CastleFlyingHand.prefab"));
                if (!handPrefab)
                {
                    var go = new GameObject("CastleFlyingHand"); go.SetActive(false);
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPath.Find("FlyingHandPlayer.prefab"));
                    var hand = (GameObject)PrefabUtility.InstantiatePrefab(source, go.transform);
                    hand.transform.localPosition = Vector3.zero;
                    var motor = hand.GetComponent<FlyingHandController>(); motor.captureDesktopCursor = false;
                    var rig = Child("Follow Camera Rig", go.transform).gameObject;
                    var cam = Rig(rig, out _); rig.GetComponent<XROrigin>().enabled = true;
                    var follow = rig.AddComponent<ThirdPersonVRFollow>(); follow.controller = motor; follow.viewCamera = cam;
                    follow.target = hand.transform.Find("CameraFollowTarget"); follow.distance = 7; follow.height = 3;
                    var actor = go.AddComponent<CastleHandActor>(); actor.motor = motor; actor.follow = follow;
                    go.SetActive(true);
                    handPrefab = PrefabUtility.SaveAsPrefabAsset(go, ProjectPath.Find("CastleFlyingHand.prefab")).GetComponent<CastleHandActor>();
                    Object.DestroyImmediate(go);
                }
                CastlePlaySession session = null; CastleGenerator generator = null; Camera overview = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.TryGetComponent<CastlePlaySession>(out var s)) session = s;
                    if (root.TryGetComponent<CastleGenerator>(out var g)) generator = g;
                    if (root.TryGetComponent<CastleTestControls>(out var controls)) overview = controls.viewCamera;
                }
                if (!session) session = new GameObject("Castle Play Session").AddComponent<CastlePlaySession>();
                session.generator = generator; session.survivorPrefab = playerPrefab; session.handPrefab = handPrefab; session.overviewCamera = overview;
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
                Debug.Log("Castle player/hand prefabs installed. Play the castle scene to spawn on top.");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); if (previous.IsValid()) SceneManager.SetActiveScene(previous); }
        }
    }
}
