using System;
using UnityEditor;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    public static class MilestoneSetup
    {
        public static void AuthorAndValidate()
        {
            ConfigurePassthrough();
            foreach (string menu in new[] { "Tools/Deadly Jenga/Maps/Build Mode Map Prefabs", "Tools/Deadly Jenga/Maps/Validate Mode Layouts", "Tools/Deadly Jenga/Validate Disaster Setup", "Tools/Characters/Validate desktop hand poses" })
                if (!EditorApplication.ExecuteMenuItem(menu)) throw new InvalidOperationException("Missing validation menu: " + menu);
            Debug.Log("Milestone authoring and deterministic checks completed.");
        }
        [MenuItem("Tools/Deadly Jenga/Enable approved OpenXR passthrough")]
        public static void ConfigurePassthrough()
        {
            var group = BuildTargetGroup.Android;
            FeatureHelpers.RefreshFeatures(group);
            foreach (string id in new[] { "com.unity.openxr.feature.arfoundation-meta-session", "com.unity.openxr.feature.arfoundation-meta-camera" })
            {
                var feature = FeatureHelpers.GetFeatureWithIdForBuildTarget(group, id);
                if (!feature) throw new InvalidOperationException("Missing OpenXR feature: " + id);
                feature.enabled = true;
                EditorUtility.SetDirty(feature);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Enabled Quest OpenXR session and passthrough camera. No raw camera-image permission requested.");
        }
    }
}
