using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    // Gray fallback materials and the FBX import settings for every tile.
    public static partial class VanderbiltCampusBuilder
    {
        static string MaterialIdsPath =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Tools", "VanderbiltCampus", "config", "material_ids.json");

        // One gray URP/Lit material per material ID (mat_<id>.mat). Presentation swaps the look; World keeps these.
        static List<Material> EnsureMaterials(string folder, BuildStats stats)
        {
            var result = new List<Material>();
            var ids = new List<string>(); var gray = new Color32(0x9A, 0x9A, 0x9A, 0xFF);
            try
            {
                var root = MiniJson.Parse(File.ReadAllText(MaterialIdsPath)) as Dictionary<string, object>;
                if (root != null && root.TryGetValue("gray_fallback", out var g) && g is string hex && ColorUtility.TryParseHtmlString(hex, out var parsed)) gray = parsed;
                if (root != null && root.TryGetValue("ids", out var list) && list is List<object> items)
                    foreach (var item in items)
                        if (item is Dictionary<string, object> d && d.TryGetValue("id", out var id) && id is string s && s.Length > 0) ids.Add(s);
            }
            catch (Exception e) { stats.problems.Add("material_ids.json could not be read (" + MaterialIdsPath + "): " + e.Message); return result; }
            if (ids.Count == 0) { stats.problems.Add("material_ids.json has no ids."); return result; }

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) { stats.problems.Add("URP Lit shader not found."); return result; }
            foreach (var id in ids)
            {
                string name = "mat_" + id, path = folder + "/" + name + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!m) { m = new Material(shader) { name = name }; AssetDatabase.CreateAsset(m, path); }
                if (m.shader != shader) m.shader = shader;
                m.color = gray; if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", gray);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", .2f);
                m.enableInstancing = true;
                EditorUtility.SetDirty(m); result.Add(m);
            }
            stats.materials = result.Count;
            if (result.Count != 27) stats.notes.Add("material_ids.json lists " + result.Count + " ids (STANDARDS v1 has 27).");
            return result;
        }

        // Import settings the builder relies on. Reimports only when something actually changed.
        static void ConfigureImporter(string path, List<Material> materials, BuildStats stats)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (!importer) { stats.problems.Add("Not a model importer: " + path); return; }
            bool changed = false;
            void Set<T>(T current, T wanted, Action<T> apply)
            {
                if (EqualityComparer<T>.Default.Equals(current, wanted)) return;
                apply(wanted); changed = true;
            }
            // Scale and axes: 1 unit = 1 m, Blender's Z-up baked into the vertices so objects keep identity rotation.
            Set(importer.globalScale, 1f, v => importer.globalScale = v);
            Set(importer.useFileScale, true, v => importer.useFileScale = v);
            Set(importer.bakeAxisConversion, true, v => importer.bakeAxisConversion = v);
            Set(importer.preserveHierarchy, true, v => importer.preserveHierarchy = v);
            // Static geometry only.
            Set(importer.animationType, ModelImporterAnimationType.None, v => importer.animationType = v);
            Set(importer.importAnimation, false, v => importer.importAnimation = v);
            Set(importer.importConstraints, false, v => importer.importConstraints = v);
            Set(importer.importBlendShapes, false, v => importer.importBlendShapes = v);
            Set(importer.importCameras, false, v => importer.importCameras = v);
            Set(importer.importLights, false, v => importer.importLights = v);
            Set(importer.importVisibility, false, v => importer.importVisibility = v);
            // Meshes: not readable (saves memory), no automatic colliders (the builder adds them),
            // no compression so vertex color R (the material index) stays exact.
            Set(importer.isReadable, false, v => importer.isReadable = v);
            Set(importer.addCollider, false, v => importer.addCollider = v);
            Set(importer.meshCompression, ModelImporterMeshCompression.Off, v => importer.meshCompression = v);
            // Materials: FBX slots named mat_<id> are remapped to the gray materials.
            Set(importer.materialImportMode, ModelImporterMaterialImportMode.ImportViaMaterialDescription, v => importer.materialImportMode = v);
            Set(importer.materialLocation, ModelImporterMaterialLocation.InPrefab, v => importer.materialLocation = v);
            var current = importer.GetExternalObjectMap();
            foreach (var material in materials)
            {
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), material.name);
                if (current.TryGetValue(id, out var existing) && existing == material) continue;
                importer.AddRemap(id, material); changed = true;
            }
            if (changed) importer.SaveAndReimport();
        }
    }
}
