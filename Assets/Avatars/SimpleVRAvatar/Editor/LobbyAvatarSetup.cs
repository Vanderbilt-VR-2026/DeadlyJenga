using System;
using Fusion;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadlyJenga.Avatars.Editor
{
    public static class LobbyAvatarSetup
    {
        const string Folder = "Assets/Avatars/SimpleVRAvatar/";
        const string NetworkPath = Folder + "LobbyNetworkAvatar.prefab";

        [MenuItem("Tools/Deadly Jenga/Install Lobby Avatar")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/Lobby.unity")
            {
                if (scene.isDirty) throw new InvalidOperationException("Save your current scene before installing the Lobby avatar.");
                scene = EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity");
            }
            var origin = UnityEngine.Object.FindFirstObjectByType<XROrigin>();
            if (origin == null || origin.gameObject.scene != scene || origin.Camera == null)
                throw new InvalidOperationException("Lobby needs its local XR rig and camera.");

            if (AssetDatabase.LoadAssetAtPath<GameObject>(NetworkPath) == null)
            {
                var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/VR Player Variant.prefab");
                try
                {
                    root.name = "LobbyNetworkAvatar";
                    var hands = root.GetComponent<NetworkHands>();
                    var serialized = new SerializedObject(hands);
                    var avatar = root.AddComponent<LobbyNetworkAvatar>();
                    avatar.headPose = root.transform.Find("Camera Offset/Head");
                    avatar.leftHandPose = ((NetworkTransform)serialized.FindProperty("leftHand").objectReferenceValue).transform;
                    avatar.rightHandPose = ((NetworkTransform)serialized.FindProperty("rightHand").objectReferenceValue).transform;
                    Require(avatar.headPose != null, "Network head transform");
                    serialized.FindProperty("head").objectReferenceValue = avatar.headPose.GetComponent<NetworkTransform>();
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    // This separate prefab replaces only the network controller graphics.
                    foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                        if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer) renderer.enabled = false;
                    PrefabUtility.SaveAsPrefabAsset(root, NetworkPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            var config = UnityEngine.Object.FindFirstObjectByType<LobbyAvatarScene>();
            if (config == null)
            {
                var go = new GameObject("Lobby Avatar Setup");
                Undo.RegisterCreatedObjectUndo(go, "Install Lobby avatar");
                config = Undo.AddComponent<LobbyAvatarScene>(go);
            }
            Undo.RecordObject(config, "Configure Lobby avatar");
            config.visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "SimpleVRAvatar.prefab").GetComponent<SimpleAvatarView>();
            config.networkPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkPath).GetComponent<NetworkObject>();
            config.headTarget = origin.Camera.transform;
            config.leftHandTarget = origin.transform.Find("Camera Offset/Left Controller/Left Controller Visual");
            config.rightHandTarget = origin.transform.Find("Camera Offset/Right Controller/Right Controller Visual");
            Require(config.leftHandTarget != null && config.rightHandTarget != null, "Local controller targets");
            EditorUtility.SetDirty(config);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = config.gameObject;
            Validate();
            Debug.Log("Lobby avatar installed and saved. Main and the original network player prefab are unchanged.");
        }

        [MenuItem("Tools/Deadly Jenga/Validate Lobby Avatar")]
        public static void Validate()
        {
            SimpleAvatarSetup.ValidatePrefab();
            var config = UnityEngine.Object.FindFirstObjectByType<LobbyAvatarScene>();
            Require(config != null && config.gameObject.scene.path == "Assets/Scenes/Lobby.unity", "Lobby-only configuration");
            Require(config.visualPrefab && config.networkPrefab && config.headTarget && config.leftHandTarget && config.rightHandTarget, "Scene references");
            var avatar = config.networkPrefab.GetComponent<LobbyNetworkAvatar>();
            Require(avatar && avatar.headPose && avatar.leftHandPose && avatar.rightHandPose, "Remote targets");
            Require(avatar.headPose.GetComponent<NetworkTransform>() != null, "Replicated head");
            var hands = new SerializedObject(config.networkPrefab.GetComponent<NetworkHands>());
            Require(hands.FindProperty("head").objectReferenceValue == avatar.headPose.GetComponent<NetworkTransform>(), "Head input binding");
            foreach (var renderer in config.networkPrefab.GetComponentsInChildren<Renderer>(true))
                if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer) Require(!renderer.enabled, "Duplicate controller hiding");
            Require(config.ColorForPlayer(1) != config.ColorForPlayer(2), "Distinct player colors");
            Require(config.ColorForPlayer(1) == config.ColorForPlayer(1 + config.playerColors.Length), "Stable palette wrapping");
            Debug.Log("Lobby avatar validation PASSED: scene bindings, replicated head/hands, controller hiding and player colors.");
        }

        static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("Lobby avatar validation failed: " + message);
        }
    }
}
