using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeadlyJenga.CandyArt.Editor
{
    // Builds the campus look assets next to CampusPalette.cs:
    // CampusPalette.mat, CampusPaletteCandy.png, CampusPalettePlain.png and "Campus Visual.prefab".
    // Safe to run again: it updates the same files. Runs only from the menu or when another tool calls Build().
    public static class CampusVisualBuilder
    {
        [MenuItem("Tools/Deadly Jenga/Maps/Build Campus Visual")]
        static void BuildFromMenu()
        {
            var prefab = Build();
            if (prefab) Debug.Log("Campus Visual built: " + AssetDatabase.GetAssetPath(prefab), prefab);
        }

        // Returns the Campus Visual prefab asset.
        public static GameObject Build()
        {
            string folder = ProjectPath.FolderOf("CampusPalette.cs");
            var candy = WritePalette(folder + "CampusPaletteCandy.png", false);
            var plain = WritePalette(folder + "CampusPalettePlain.png", true);
            var material = WriteMaterial(folder + "CampusPalette.mat", folder + "CampusPalette.shader", candy);
            var prefab = WritePrefab(folder + "Campus Visual.prefab", material, candy, plain);
            AssetDatabase.SaveAssets();
            return prefab;
        }

        static Texture2D WritePalette(string path, bool plain)
        {
            var texture = CampusPaletteColors.CreateTexture(plain, true);
            byte[] png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);
            // Only rewrite the file when the colors changed, so a rebuild leaves git clean.
            if (!File.Exists(path) || !SameBytes(File.ReadAllBytes(path), png))
            {
                File.WriteAllBytes(path, png);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            bool changed = importer.textureType != TextureImporterType.Default || !importer.sRGBTexture ||
                importer.filterMode != FilterMode.Point || importer.wrapMode != TextureWrapMode.Clamp ||
                importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed ||
                importer.alphaIsTransparency || importer.npotScale != TextureImporterNPOTScale.None || importer.isReadable;
            if (changed)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.isReadable = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material WriteMaterial(string path, string shaderPath, Texture2D candy)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            if (!shader) shader = Shader.Find("DeadlyJenga/Campus Palette");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetTexture("_Palette", candy);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        static GameObject WritePrefab(string path, Material material, Texture2D candy, Texture2D plain)
        {
            bool exists = File.Exists(path);
            var root = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject("Campus Visual");
            try
            {
                var palette = root.GetComponent<CampusPalette>();
                if (!palette) palette = root.AddComponent<CampusPalette>();
                palette.paletteMaterial = material;
                palette.candyPalette = candy;
                palette.plainPalette = plain;
                var atmosphere = root.GetComponent<CampusAtmosphere>();
                if (!atmosphere) atmosphere = root.AddComponent<CampusAtmosphere>();
                if (!atmosphere.daySky) atmosphere.daySky = FindDaySky();
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }
        }

        static bool SameBytes(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        // The island's daytime candy sky. If it is missing the campus keeps whatever sky is active.
        static Material FindDaySky()
        {
            try { return AssetDatabase.LoadAssetAtPath<Material>(ProjectPath.Find("Tropical Sky.mat")); }
            catch (FileNotFoundException) { return null; }
        }
    }
}
