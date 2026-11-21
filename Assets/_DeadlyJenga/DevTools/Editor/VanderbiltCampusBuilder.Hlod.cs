using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.DevTools.Editor
{
    // Far-distance tile HLOD: the tile's LoD1 meshes (Terrain_LOD1, building LOD1s, Trees_LOD1, Ring)
    // merged into ONE mesh with ONE submesh and the gray "mat_mixed" material, so a far tile is one draw call.
    // The vertex color R still carries each triangle's material index, so the palette shader colors it.
    //
    // Preferred source: an "HLOD" object exported inside the tile FBX (used as is when present).
    // Fallback: the builder merges the meshes itself and saves Hlod/<tile> HLOD.asset - but only when
    // MakeHlodAssets is true. It is off because the estimated size (reported in the build report as
    // "HLOD estimate") is far above what we want as plain .asset files in Git (not LFS).
    public static partial class VanderbiltCampusBuilder
    {
        public static readonly bool MakeHlodAssets = false;
        const string MixedMaterialName = "mat_mixed";
        static Material mixedMaterial;

        // Bytes per vertex in the merged mesh: position 12 + normal 12 + color 4 + uv0 8.
        const int HlodVertexBytes = 36;

        static MeshRenderer MakeHlod(Transform tile, List<Renderer> parts, BuildStats stats)
        {
            long vertices = 0, indices = 0;
            foreach (var r in parts)
            {
                var mesh = MeshOf(r); if (!mesh) continue;
                vertices += mesh.vertexCount;
                for (int s = 0; s < mesh.subMeshCount; s++) indices += mesh.GetIndexCount(s);
            }
            stats.hlodVertices += vertices; stats.hlodIndices += indices;
            if (!MakeHlodAssets || !mixedMaterial || parts.Count == 0) return null;

            var merged = Merge(tile, parts, tile.name + " HLOD");
            string folder = Folder(CampusFolder(), "Hlod");
            string path = folder + "/" + tile.name + " HLOD.asset";
            var asset = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (asset) { EditorUtility.CopySerialized(merged, asset); Object.DestroyImmediate(merged); }
            else { AssetDatabase.CreateAsset(merged, path); asset = merged; }
            // Not readable at runtime (saves memory); the data stays in the asset.
            var so = new SerializedObject(asset); so.FindProperty("m_IsReadable").boolValue = false; so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            stats.hlodAssetBytes += new FileInfo(path).Exists ? new FileInfo(path).Length : 0;

            var go = new GameObject("HLOD", typeof(MeshFilter), typeof(MeshRenderer)); go.isStatic = true;
            go.transform.SetParent(tile, false);
            go.GetComponent<MeshFilter>().sharedMesh = asset;
            var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = mixedMaterial;
            stats.builtHlods++;
            return renderer;
        }

        static Mesh MeshOf(Renderer r)
        {
            var filter = r ? r.GetComponent<MeshFilter>() : null;
            return filter ? filter.sharedMesh : null;
        }

        // Reads the meshes in edit mode (allowed for non-readable meshes outside Play Mode) and
        // appends every submesh into one triangle list, in the tile's local space.
        static Mesh Merge(Transform tile, List<Renderer> parts, string name)
        {
            var vertices = new List<Vector3>(); var normals = new List<Vector3>();
            var colors = new List<Color32>(); var uvs = new List<Vector2>(); var triangles = new List<int>();
            var toTile = tile.worldToLocalMatrix;
            foreach (var r in parts)
            {
                var mesh = MeshOf(r); if (!mesh) continue;
                var m = toTile * r.transform.localToWorldMatrix;
                var v = mesh.vertices; var n = mesh.normals; var c = mesh.colors32; var uv = mesh.uv;
                int start = vertices.Count;
                for (int i = 0; i < v.Length; i++)
                {
                    vertices.Add(m.MultiplyPoint3x4(v[i]));
                    normals.Add(n.Length == v.Length ? m.MultiplyVector(n[i]).normalized : Vector3.up);
                    colors.Add(c.Length == v.Length ? c[i] : new Color32(255, 255, 255, 255));
                    uvs.Add(uv.Length == v.Length ? uv[i] : Vector2.zero);
                }
                for (int s = 0; s < mesh.subMeshCount; s++)
                    foreach (int index in mesh.GetTriangles(s)) triangles.Add(start + index);
            }
            var merged = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            merged.SetVertices(vertices); merged.SetNormals(normals); merged.SetColors(colors); merged.SetUVs(0, uvs);
            merged.SetTriangles(triangles, 0); merged.RecalculateBounds();
            return merged;
        }
    }
}
