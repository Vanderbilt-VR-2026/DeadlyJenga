using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using DeadlyJenga.ArchitectureMap.Editor;

// Copy only into an isolated Unity fixture project, never the game's Assets.
public static class ArchitectureMapUnityChecks
{
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').EndsWith("/.architecture/local/unity-validation/Assets", StringComparison.Ordinal))
            throw new InvalidOperationException("Unity integration tests require the isolated fixture project.");
        var material = new Material(Shader.Find("Hidden/InternalErrorShader"));
        AssetDatabase.CreateAsset(material, "Assets/FixtureMaterial.mat");
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.GetComponent<Renderer>().sharedMaterial = material;
        var prefab = PrefabUtility.SaveAsPrefabAsset(cube, "Assets/Fixture.prefab");
        UnityEngine.Object.DestroyImmediate(cube);
        var parent = new GameObject("Nested");
        var child = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        child.transform.SetParent(parent.transform);
        PrefabUtility.SaveAsPrefabAsset(parent, "Assets/Nested.prefab");
        UnityEngine.Object.DestroyImmediate(parent);
        AssetDatabase.SaveAssets();
        ArchitectureMapEditor.ValidateBatch();
        var root = Path.GetDirectoryName(Application.dataPath);
        var snapshot = JsonUtility.FromJson<ArchitectureMapEditor.Snapshot>(File.ReadAllText(Path.Combine(root, ".architecture/local/unity.json")));
        var materialId = AssetDatabase.AssetPathToGUID("Assets/FixtureMaterial.mat");
        if (!snapshot.assets.Single(a => a.path == "Assets/Fixture.prefab").dependencies.Contains(materialId)) throw new Exception("Missing material dependency.");
        var prefabId = AssetDatabase.AssetPathToGUID("Assets/Fixture.prefab");
        if (!snapshot.assets.Single(a => a.path == "Assets/Nested.prefab").dependencies.Contains(prefabId)) throw new Exception("Missing nested prefab dependency.");
        if (!snapshot.assets.All(a => a.hash.Length == 64)) throw new Exception("Missing content hashes.");
        Debug.Log("ARCHITECTURE_UNITY_CHECKS_PASSED: material reference, nested prefab, content hashes, editor compilation");
    }
}
