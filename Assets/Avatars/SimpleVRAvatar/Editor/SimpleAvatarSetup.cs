using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadlyJenga.Avatars.Editor
{
    public static class SimpleAvatarSetup
    {
        const string Folder = "Assets/Avatars/SimpleVRAvatar";
        const string PrefabPath = Folder + "/SimpleVRAvatar.prefab";

        [MenuItem("Tools/Deadly Jenga/Validate Simple Avatar")]
        public static void ValidatePrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) throw new InvalidOperationException("Create the avatar prefab first.");
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = UnityEngine.Object.Instantiate(prefab);
                SceneManager.MoveGameObjectToScene(instance, preview);
                var view = instance.GetComponent<SimpleAvatarView>();
                Require(view.head && view.torso && view.leftHand && view.rightHand, "Mesh references");
                Require(view.leftHand.position.x < 0 && view.rightHand.position.x > 0, "Left/right handedness");
                foreach (var filter in instance.GetComponentsInChildren<MeshFilter>())
                    Require(filter.sharedMesh != null, "Mesh import");
                var targets = new GameObject("Validation targets");
                SceneManager.MoveGameObjectToScene(targets, preview);
                Transform Target(string name, Vector3 position, Quaternion rotation)
                {
                    var t = new GameObject(name).transform;
                    t.SetParent(targets.transform);
                    t.SetPositionAndRotation(position, rotation);
                    return t;
                }
                view.headTarget = Target("Head target", new Vector3(2, 1.7f, 3), Quaternion.Euler(25, 60, 0));
                view.leftHandTarget = Target("Left target", new Vector3(1.5f, 1.2f, 3.3f), Quaternion.Euler(10, 20, 30));
                view.rightHandTarget = Target("Right target", new Vector3(2.5f, 1.3f, 3.4f), Quaternion.identity);
                view.leftHandPositionOffset = new Vector3(0, 0, 0.04f);
                view.leftHandRotationOffset = new Vector3(15, 0, 0);
                view.ApplyTrackingPose();
                Require(Vector3.Distance(view.head.position, view.headTarget.position) < .0001f, "Head tracking");
                Require(Quaternion.Angle(view.head.rotation, view.headTarget.rotation) < .01f, "Head rotation");
                Require(Vector3.Distance(view.leftHand.position, view.leftHandTarget.position + view.leftHandTarget.rotation * view.leftHandPositionOffset) < .0001f, "Wrist offset");
                Require(Quaternion.Angle(view.leftHand.rotation, view.leftHandTarget.rotation * Quaternion.Euler(view.leftHandRotationOffset)) < .01f, "Wrist rotation");
                Require(Vector3.Distance(view.rightHand.position, view.rightHandTarget.position) < .0001f, "Right tracking");
                Require(Vector3.Dot(view.torso.up, Vector3.up) > .9999f, "Upright torso");
                var yaw = Quaternion.Euler(0, 60, 0);
                Require(Vector3.Distance(view.torso.position, view.headTarget.position + yaw * view.torsoOffset) < .0001f, "Torso offset");
                var renderer = view.head.GetComponent<Renderer>();
                var sharedColor = renderer.sharedMaterial.GetColor("_BaseColor");
                view.SetPlayerColor(Color.magenta);
                var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
                Require(block.GetColor("_BaseColor") == Color.magenta, "Per-avatar color");
                Require(renderer.sharedMaterial.GetColor("_BaseColor") == sharedColor, "Shared material unchanged");
                view.SetLocalView(true);
                Require(renderer.forceRenderingOff && view.torso.GetComponent<Renderer>().forceRenderingOff, "Local body hiding");
                Require(!view.leftHand.GetComponent<Renderer>().forceRenderingOff, "Local hands visible");
                view.SetLocalView(false);
                Require(!renderer.forceRenderingOff, "Remote visibility restored");
                view.headTarget = view.leftHandTarget = view.rightHandTarget = null;
                view.ApplyTrackingPose();
                Debug.Log("Simple avatar validation PASSED: meshes, handedness, tracking, offsets, color isolation, visibility, and missing targets.");
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        static void Require(bool condition, string check)
        {
            if (!condition) throw new InvalidOperationException("Avatar validation failed: " + check);
        }

        [MenuItem("Tools/Deadly Jenga/Create Simple Avatar Prefab")]
        public static void CreatePrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                Debug.Log("Simple avatar prefab already exists; preserving your edits.");
                return;
            }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/SimpleVRAvatar.fbx");
            if (model == null) throw new InvalidOperationException("Import SimpleVRAvatar.fbx first.");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP/Lit shader is required.");

            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/PlayerColor.mat");
            if (material == null)
            {
                material = new Material(shader) { name = "PlayerColor" };
                material.SetColor("_BaseColor", new Color(0.12f, 0.65f, 0.95f));
                material.SetFloat("_Smoothness", 0.45f);
                AssetDatabase.CreateAsset(material, Folder + "/PlayerColor.mat");
            }

            var root = new GameObject("SimpleVRAvatar");
            var imported = UnityEngine.Object.Instantiate(model, root.transform);
            imported.name = "Visuals";
            try
            {
                var view = root.AddComponent<SimpleAvatarView>();
                foreach (var t in imported.GetComponentsInChildren<Transform>(true))
                {
                    switch (t.name)
                    {
                        case "Head": view.head = t; break;
                        case "Torso": view.torso = t; break;
                        case "LeftHand": view.leftHand = t; break;
                        case "RightHand": view.rightHand = t; break;
                    }
                }
                if (!view.head || !view.torso || !view.leftHand || !view.rightHand)
                    throw new InvalidOperationException("Missing required avatar mesh pivots.");
                foreach (var r in imported.GetComponentsInChildren<Renderer>())
                    r.sharedMaterial = material;

                // The model is authored in meters. Catch export/import scale mistakes.
                var headBounds = view.head.GetComponent<Renderer>().bounds;
                if (headBounds.size.y < 0.3f || headBounds.size.y > 0.5f)
                    throw new InvalidOperationException("Avatar scale is incorrect; expected a 0.38 m head.");
                view.torsoOffset = view.torso.position - view.head.position;
                view.SetPlayerColor(view.playerColor);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                Debug.Log("Simple avatar ready: " + PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
