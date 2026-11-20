using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    // Turns one imported tile model into map content. Object names from Blender decide roles:
    //   COL_Terrain, COL_Trees   -> static non-convex MeshCollider on Layers.Surface (renderer removed)
    //   COL_B_<id>_<n>           -> static convex MeshCollider, grouped under COL_B_<id> with CampusClimbSurface
    //   B_<id>_LOD0 / _LOD1      -> grouped under B_<id> with a LODGroup (4% / 0.5% screen height, then culled)
    //   Spawn_*, Route_*, Climb_* -> empty markers (moved under "Markers")
    //   anything else with a mesh -> static visual, never a collider
    public static partial class VanderbiltCampusBuilder
    {
        static readonly Regex BuildingCollider = new Regex(@"^COL_B_(.+)_(\d+)$");
        static readonly Regex BuildingLod = new Regex(@"^B_(.+)_LOD([01])$");

        // Blender may add ".001" to duplicate names; ignore it when reading roles.
        static string CleanName(string name) => Regex.Replace(name, @"\.\d+$", "");

        static void AddTile(Transform root, string path, BuildStats stats)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!model) { stats.problems.Add("Could not load " + path); return; }
            // Unpacked so the builder can regroup objects; meshes still come from the FBX,
            // so a re-export updates them, and a rebuild picks up new or renamed objects.
            var tile = (GameObject)PrefabUtility.InstantiatePrefab(model, root.gameObject.scene);
            PrefabUtility.UnpackPrefabInstance(tile, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            tile.name = model.name;
            tile.transform.SetParent(root, false);
            tile.transform.localPosition = Vector3.zero; tile.transform.localRotation = Quaternion.identity; tile.transform.localScale = Vector3.one;
            stats.tiles++;

            var colliders = Child(tile.transform, "Colliders");
            var markers = Child(tile.transform, "Markers");
            var buildingColliders = new Dictionary<string, Transform>();
            var lods = new Dictionary<string, Transform[]>();
            bool terrainCollider = false;

            foreach (var t in tile.GetComponentsInChildren<Transform>(true).ToArray())
            {
                if (t == tile.transform || t == colliders || t == markers) continue;
                string name = CleanName(t.name);
                if (name.StartsWith("COL_"))
                {
                    var match = BuildingCollider.Match(name);
                    bool convex = match.Success;
                    if (!MakeCollider(t, convex, stats)) continue;
                    if (name == "COL_Terrain") terrainCollider = true;
                    if (convex)
                    {
                        string id = match.Groups[1].Value;
                        if (!buildingColliders.TryGetValue(id, out var holder))
                        {
                            holder = Child(colliders, "COL_B_" + id);
                            holder.gameObject.AddComponent<CampusClimbSurface>();
                            holder.gameObject.layer = Layers.Surface; holder.gameObject.isStatic = true;
                            buildingColliders[id] = holder; stats.climbSurfaces++;
                        }
                        t.SetParent(holder, true);
                    }
                    else t.SetParent(colliders, true);
                }
                else if (name.StartsWith("Spawn_") || name.StartsWith("Route_") || name.StartsWith("Climb_"))
                {
                    foreach (var r in t.GetComponents<Renderer>()) Object.DestroyImmediate(r);
                    foreach (var f in t.GetComponents<MeshFilter>()) Object.DestroyImmediate(f);
                    t.SetParent(markers, true);
                    if (name.StartsWith("Spawn_")) stats.spawnMarkers.Add(t);
                }
                else
                {
                    var lod = BuildingLod.Match(name);
                    if (lod.Success)
                    {
                        string id = lod.Groups[1].Value; int level = lod.Groups[2].Value == "0" ? 0 : 1;
                        if (!lods.TryGetValue(id, out var pair)) lods[id] = pair = new Transform[2];
                        if (pair[level]) stats.problems.Add(tile.name + ": two objects named B_" + id + "_LOD" + level);
                        pair[level] = t;
                    }
                    MakeVisual(t, name, lod.Success && lod.Groups[2].Value == "1", stats);
                    if (name == "Terrain") { var r = t.GetComponent<Renderer>(); if (r) stats.terrain.Add(r); }
                }
            }
            foreach (var pair in lods) GroupBuilding(tile.transform, pair.Key, pair.Value, stats);
            if (!terrainCollider) stats.problems.Add(tile.name + " has no COL_Terrain collider.");
        }

        static Transform Child(Transform parent, string name)
        {
            var t = new GameObject(name).transform; t.SetParent(parent, false); return t;
        }

        // A collider object keeps only its mesh as a MeshCollider; its renderer is removed.
        static bool MakeCollider(Transform t, bool convex, BuildStats stats)
        {
            var filter = t.GetComponent<MeshFilter>();
            var mesh = filter ? filter.sharedMesh : null;
            if (!mesh) { stats.problems.Add("Collider object without a mesh: " + t.name); return false; }
            foreach (var r in t.GetComponents<Renderer>()) Object.DestroyImmediate(r);
            Object.DestroyImmediate(filter);
            foreach (var old in t.GetComponents<Collider>()) Object.DestroyImmediate(old);
            t.gameObject.layer = Layers.Surface;
            t.gameObject.isStatic = true;
            var shape = t.gameObject.AddComponent<MeshCollider>();
            shape.sharedMesh = mesh; shape.convex = convex;
            stats.colliders++; if (convex) stats.convexColliders++;
            return true;
        }

        // Visual meshes are static (batching, occlusion) and never collide.
        static void MakeVisual(Transform t, string name, bool lod1, BuildStats stats)
        {
            foreach (var c in t.GetComponents<Collider>()) Object.DestroyImmediate(c);
            t.gameObject.isStatic = true;
            var filter = t.GetComponent<MeshFilter>(); var renderer = t.GetComponent<MeshRenderer>();
            if (!filter || !filter.sharedMesh || !renderer) return;
            stats.visuals.Add(renderer);
            int tris = Triangles(filter.sharedMesh);
            stats.trianglesAll += tris; if (!lod1) stats.trianglesLod0 += tris;
            foreach (var m in renderer.sharedMaterials)
                if (!m || !m.name.StartsWith("mat_")) { stats.problems.Add(t.name + " uses material '" + (m ? m.name : "none") + "' (needs mat_<id>)."); break; }
        }

        internal static int Triangles(Mesh mesh)
        {
            long count = 0;
            for (int s = 0; s < mesh.subMeshCount; s++) count += mesh.GetIndexCount(s) / 3;
            return (int)count;
        }

        // B_<id>_LOD0 and B_<id>_LOD1 go under one B_<id> object that switches between them by screen size.
        static void GroupBuilding(Transform tile, string id, Transform[] levels, BuildStats stats)
        {
            var group = Child(tile, "B_" + id);
            group.gameObject.isStatic = true;
            var oldParents = levels.Where(l => l).Select(l => l.parent).Where(p => p != tile).Distinct().ToList();
            var entries = new List<LOD>();
            if (levels[0]) { levels[0].SetParent(group, true); entries.Add(new LOD(.04f, levels[0].GetComponentsInChildren<Renderer>())); }
            if (levels[1]) { levels[1].SetParent(group, true); entries.Add(new LOD(.005f, levels[1].GetComponentsInChildren<Renderer>())); }
            if (!levels[0] || !levels[1]) stats.problems.Add("B_" + id + " is missing LOD" + (levels[0] ? "1" : "0") + ".");
            // A single level still culls at 0.5% screen height.
            if (entries.Count == 1) entries[0] = new LOD(.005f, entries[0].renderers);
            var lodGroup = group.gameObject.AddComponent<LODGroup>();
            lodGroup.SetLODs(entries.ToArray());
            lodGroup.RecalculateBounds();
            // If Blender already had an empty B_<id> parent, remove it now that it is empty.
            foreach (var old in oldParents)
                if (old && old.childCount == 0 && old.GetComponents<Component>().Length == 1) Object.DestroyImmediate(old.gameObject);
            stats.buildings++;
        }
    }
}
