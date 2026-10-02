using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace FlyingHand.Editor
{
    public static class HandDemoPackageExporter
    {
        public const string Root = "Assets/HandDemo";

        [MenuItem("Tools/Flying Hand/Export Portable Package")]
        public static void ExportWithDialog()
        {
            string path = EditorUtility.SaveFilePanel("Export Flying Hand Demo", "", "FlyingHandDemo", "unitypackage");
            if (!string.IsNullOrEmpty(path)) Export(path);
        }

        public static void Export(string outputPath)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before exporting.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            var feature = AssetDatabase.FindAssets("", new[] { Root })
                .Select(AssetDatabase.GUIDToAssetPath).Append(Root).Distinct().ToArray();
            // Include the complete project-asset dependency closure explicitly.
            // Unity 6's IncludeDependencies flag also embeds Packages/* source
            // files, which would undermine Package Manager version ownership.
            var projectDependencies = AssetDatabase.GetDependencies(feature, true)
                .Where(p => p.StartsWith("Assets/", StringComparison.Ordinal));
            var paths = feature.Concat(projectDependencies).Distinct().OrderBy(p => p).ToArray();
            var outside = paths.Where(p => p != Root && !p.StartsWith(Root + "/", StringComparison.Ordinal)).ToArray();
            if (outside.Length != 0)
                throw new InvalidOperationException("Feature depends on assets outside its root: " + string.Join(", ", outside));
            string directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            Directory.CreateDirectory(directory);
            AssetDatabase.ExportPackage(paths, outputPath, ExportPackageOptions.Recurse);
            UnityEngine.Debug.Log("Exported HandDemo with project dependencies, excluding external UPM files: " + outputPath);
        }
    }
}
