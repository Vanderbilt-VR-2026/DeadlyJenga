using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeadlyJenga.Core;

namespace DeadlyJenga.World.Hazards.Editor
{
    public sealed class HazardValidation : IPreprocessBuildWithReport, IProcessSceneWithReport
    {
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report) => ValidateCatalog();
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            foreach (var root in scene.GetRootGameObjects())
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (component is IWorldQuery world && !MapHazard.ValidBounds(world.PlayableBounds))
                    throw new BuildFailedException($"{scene.name}/{component.name}: invalid PlayableBounds.");
        }

        [MenuItem("Tools/Deadly Jenga/Validate Disaster Setup")]
        public static void ValidateCatalog()
        {
            var catalog = Resources.Load<HazardCatalog>("DisasterCatalog");
            if (!catalog || catalog.prefabs == null || catalog.prefabs.Length == 0)
                throw new BuildFailedException("DisasterCatalog must exist in Resources and contain disaster prefabs.");
            var names = new HashSet<string>();
            var types = new HashSet<HazardType>();
            var registered = new HashSet<GameObject>();
            foreach (var prefab in catalog.prefabs)
            {
                if (!prefab || !PrefabUtility.IsPartOfPrefabAsset(prefab)) Fail("Catalog contains a missing or non-prefab entry.");
                if (!registered.Add(prefab) || !names.Add(prefab.name)) Fail("Duplicate disaster entry or ID: " + prefab.name);
                var hazards = prefab.GetComponentsInChildren<MonoBehaviour>(true).OfType<IHazard>().ToArray();
                if (hazards.Length != 1 || !(hazards[0] is MapHazard hazard) || hazard.gameObject != prefab)
                    Fail(prefab.name + ": needs exactly one MapHazard on the root.");
                var mapHazard = prefab.GetComponent<MapHazard>();
                if (!prefab.activeSelf || !mapHazard.enabled) Fail(prefab.name + ": root and hazard must be enabled.");
                if (!mapHazard.TryValidatePrefab(out var error)) Fail(prefab.name + ": " + error);
                if (!types.Add(mapHazard.Type)) Fail(prefab.name + ": each disaster needs its own HazardType.");
                foreach (var component in prefab.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(component.gameObject) > 0)
                        Fail(prefab.name + ": missing script.");
                var methods = mapHazard.GetType().GetInterfaceMap(typeof(IHazard));
                for (int i = 0; i < methods.InterfaceMethods.Length; i++)
                    if ((methods.InterfaceMethods[i].Name == "Begin" || methods.InterfaceMethods[i].Name == "End") &&
                        methods.TargetMethods[i].DeclaringType != typeof(MapHazard))
                        Fail(prefab.name + ": do not replace the validated Begin/End entry points.");
            }
            var folder = ProjectPath.FolderOf("MapHazard.cs").TrimEnd('/');
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (!registered.Contains(prefab)) Fail(prefab.name + ": hazard prefab is not registered in DisasterCatalog. Register it, or remove it through Unity when retiring the disaster.");
            }
            Debug.Log($"Disaster setup passed: {registered.Count} registered map-aware prefabs.");
        }

        static void Fail(string message) => throw new BuildFailedException(message);
    }

    [CustomEditor(typeof(MapHazard), true)]
    public sealed class MapHazardInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var hazard = (MapHazard)target;
            if (!hazard.TryValidatePrefab(out var error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            EditorGUILayout.HelpBox("Coverage comes from the active map's PlayableBounds. Intensity and duration do not scale with map size. Register this root prefab in DisasterCatalog.", MessageType.Info);
        }
    }
}
