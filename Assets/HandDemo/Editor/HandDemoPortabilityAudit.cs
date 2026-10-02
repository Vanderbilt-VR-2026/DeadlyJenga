using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using Unity.XR.CoreUtils;

namespace FlyingHand.Editor
{
    public static class HandDemoAudit
    {
        public const string ScenePath = "Assets/HandDemo/Scenes/FlyingHandQuest.unity";
        public static string Result { get; private set; }

        [MenuItem("Tools/Flying Hand/Audit Scene and Prefab")]
        public static void Validate()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath) throw new InvalidOperationException("Open " + ScenePath + " before auditing.");
            var report = new StringBuilder();
            var objects = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).ToArray();
            foreach (var go in objects) Inspect(go, report);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/HandDemo/Prefabs/FlyingHandPlayer.prefab");
            if (!prefab) report.AppendLine("FAIL Player prefab missing");
            else foreach (var t in prefab.GetComponentsInChildren<Transform>(true)) Inspect(t.gameObject, report);
            Check(report, "one Camera", objects.Sum(g => g.GetComponents<Camera>().Length) == 1);
            Check(report, "one MainCamera", objects.Count(g => g.CompareTag("MainCamera")) == 1);
            Check(report, "one XR Origin", objects.Sum(g => g.GetComponents<XROrigin>().Length) == 1);
            Check(report, "one AudioListener", objects.Sum(g => g.GetComponents<AudioListener>().Length) == 1);
            Check(report, "no duplicate EventSystems", objects.SelectMany(g => g.GetComponents<Component>()).Count(c => c && c.GetType().FullName == "UnityEngine.EventSystems.EventSystem") <= 1);
            var follow = objects.Select(g => g.GetComponent<ThirdPersonVRFollow>()).FirstOrDefault(c => c);
            Check(report, "follow target/controller/camera assigned", follow && follow.target && follow.controller && follow.viewCamera);
            foreach (var driver in objects.SelectMany(g => g.GetComponents<TrackedPoseDriver>()))
                Check(report, driver.name + " pose actions", driver.positionInput.action != null && driver.positionInput.action.bindings.Count > 0 && driver.rotationInput.action != null && driver.rotationInput.action.bindings.Count > 0 && driver.trackingStateInput.action != null && driver.trackingStateInput.action.bindings.Count > 0);
            var paths = AssetDatabase.FindAssets("", new[] { "Assets/HandDemo" }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            foreach (var dependency in AssetDatabase.GetDependencies(paths, true))
                if (dependency.StartsWith("Assets/", StringComparison.Ordinal) && !dependency.StartsWith("Assets/HandDemo/", StringComparison.Ordinal) && dependency != "Assets/HandDemo")
                    report.AppendLine("FAIL External asset: " + dependency);
            Result = report.ToString();
            Debug.Log("HandDemo structural audit\n" + Result);
        }

        static void Check(StringBuilder report, string name, bool ok) => report.AppendLine((ok ? "PASS " : "FAIL ") + name);

        static void Inspect(GameObject go, StringBuilder report)
        {
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go) != 0) report.AppendLine("FAIL Missing Script: " + go.name);
            foreach (var component in go.GetComponents<Component>())
            {
                if (!component) continue;
                var serialized = new SerializedObject(component);
                var property = serialized.GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                        report.AppendLine("FAIL Broken reference: " + go.name + "/" + component.GetType().Name + "/" + property.propertyPath);
            }
            foreach (var renderer in go.GetComponents<Renderer>())
                foreach (var material in renderer.sharedMaterials)
                    if (!material || !material.shader || !material.shader.isSupported || material.shader.name == "Hidden/InternalErrorShader")
                        report.AppendLine("FAIL Missing/unsupported material: " + go.name);
        }
    }
}
// HandDemo-only validation entry point.
