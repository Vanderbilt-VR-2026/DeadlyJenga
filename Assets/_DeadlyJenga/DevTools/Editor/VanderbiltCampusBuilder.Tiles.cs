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
    //   COL_Terrain, COL_Trees     -> static non-convex MeshCollider on Layers.Surface (renderer removed)
    //   COL_B_<id>_<n>             -> static convex MeshCollider, grouped under COL_B_<id> with CampusClimbSurface
    //   Terrain + Terrain_LOD1     -> LODGroup "Terrain LODs" (far terrain never culled)
    //   Trees + Trees_LOD1         -> LODGroup "Trees LODs"
    //   B_<id>_LOD0 + B_<id>_LOD1  -> LODGroup "B_<id>"
    //   Spawn_*, Route_*, Climb_*  -> empty markers (moved under "Markers")
    //   anything else with a mesh  -> static visual, never a collider (this includes Ring: no collider, no LOD)
    public static partial class VanderbiltCampusBuilder
    {
        // LOD switch heights (fraction of screen height). Unity: height = size / (2 * distance * tan(fov / 2)),
        // so at 90 degree FOV distance = size / (2 * height). Distances below are for lodBias 1.
        // Terrain group (size ~100 m tile): LOD0 until 20% (~250 m from the tile center), then LOD1 forever.
        public const float TerrainLod0 = .20f, TerrainLod1 = 0f;
        // Trees group (one merged mesh per tile, size ~100 m): LOD0 until 12% (~420 m), LOD1 until 1% (~5 km,
        // past the 700 m far clip, so in practice never culled), then culled.
        public const float TreesLod0 = .12f, TreesLod1 = .01f;
        // Buildings (size s = largest side): LOD0 until 4% (12.5 x s: a 20 m building ~250 m),
        // LOD1 until 0.5% (100 x s: a 20 m building ~2 km, beyond the far clip), then culled.
        public const float BuildingLod0 = .04f, BuildingLod1 = .005f;

        // Fraction of screen height an object of this size fills (Unity's LODGroup rule). The budget check uses it too.
        internal static float ScreenHeight(float size, float distance, float fovDegrees) =>
            size / (2 * Mathf.Max(.01f, distance) * Mathf.Tan(fovDegrees * .5f * Mathf.Deg2Rad));

        static readonly Regex BuildingCollider = new Regex(@"^COL_B_(.+)_(\d+)$");
        static readonly Regex LodName = new Regex(@"^(B_.+)_LOD([01])$|^(Terrain|Trees)(_LOD1)?$");

        // Blender may add ".001" to duplicate names; ignore it when reading roles.
        static string CleanName(string name) => Regex.Replace(name, @"\.\d+$", "");

        static void AddTile(Transform root, string path, BuildStats stats)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!model) { stats.Problem("Could not load " + path); return; }
            // Unpacked so the builder can regroup objects; meshes still come from the FBX,
            // so a re-export updates them, and a rebuild picks up new or renamed objects.
            var tile = (GameObject)PrefabUtility.InstantiatePrefab(model, root.gameObject.scene);
            PrefabUtility.UnpackPrefabInstance(tile, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            tile.name = model.name;
            tile.transform.SetParent(root, false);
            tile.transform.localPosition = Vector3.zero; tile.transform.localRotation = Quaternion.identity; tile.transform.localScale = Vector3.one;
            tile.isStatic = true;
            stats.tiles++;

            var colliders = Child(tile.transform, "Colliders");
            var markers = Child(tile.transform, "Markers");
            var buildingColliders = new Dictionary<string, Transform>();
            var lods = new Dictionary<string, Transform[]>();   // group name -> [LOD0, LOD1]
            bool terrainCollider = false, hasBuildings = false;

            foreach (var t in tile.GetComponentsInChildren<Transform>(true).ToArray())
            {
                if (t == tile.transform || t == colliders || t == markers) continue;
                string name = CleanName(t.name);
                if (name.StartsWith("B_") || name.StartsWith("COL_B_")) hasBuildings = true;
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
                    var lod = LodName.Match(name);
                    bool lod1 = false;
                    if (lod.Success)
                    {
                        string group = lod.Groups[1].Success ? lod.Groups[1].Value : lod.Groups[3].Value + " LODs";
                        int level = lod.Groups[1].Success ? (lod.Groups[2].Value == "0" ? 0 : 1) : (lod.Groups[4].Success ? 1 : 0);
                        lod1 = level == 1;
                        if (!lods.TryGetValue(group, out var pair)) lods[group] = pair = new Transform[2];
                        if (pair[level]) stats.Problem(tile.name + ": two objects for " + group + " LOD" + level);
                        pair[level] = t;
                    }
                    MakeVisual(t, lod1, stats);
                    if (name == "Terrain") { var r = t.GetComponent<Renderer>(); if (r) stats.terrain.Add(r); }
                }
            }
            foreach (var pair in lods) GroupLods(tile.transform, pair.Key, pair.Value, stats);
            if (!terrainCollider)
            {
                // Backdrop ring tiles far from campus have no ground collider on purpose.
                if (hasBuildings) stats.Problem(tile.name + " has campus buildings but no COL_Terrain collider.");
                else stats.ringTilesWithoutCollider++;
            }
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
            if (!mesh) { stats.Problem("Collider object without a mesh: " + t.name); return false; }
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

        // Visual meshes are fully static (isStatic sets every flag, including Batching Static) and never collide.
        static void MakeVisual(Transform t, bool lod1, BuildStats stats)
        {
            foreach (var c in t.GetComponents<Collider>()) Object.DestroyImmediate(c);
            t.gameObject.isStatic = true;
            var filter = t.GetComponent<MeshFilter>(); var renderer = t.GetComponent<MeshRenderer>();
            if (!filter || !filter.sharedMesh || !renderer) return;
            stats.visuals.Add(renderer);
            stats.renderers++; stats.submeshes += filter.sharedMesh.subMeshCount;
            int tris = Triangles(filter.sharedMesh);
            stats.trianglesAll += tris; if (!lod1) stats.trianglesLod0 += tris;
            foreach (var m in renderer.sharedMaterials)
                if (!m || !m.name.StartsWith("mat_")) { stats.Problem(t.name + " uses material '" + (m ? m.name : "none") + "' (needs mat_<id>)."); break; }
        }

        internal static int Triangles(Mesh mesh)
        {
            long count = 0;
            for (int s = 0; s < mesh.subMeshCount; s++) count += mesh.GetIndexCount(s) / 3;
            return (int)count;
        }

        // LOD0 and LOD1 of one thing go under one object that switches between them by screen size.
        static void GroupLods(Transform tile, string group, Transform[] levels, BuildStats stats)
        {
            bool building = group.StartsWith("B_");
            if (!building && !levels[1]) return;   // terrain or trees with only LOD0: keep a plain renderer
            float near = building ? BuildingLod0 : group.StartsWith("Terrain") ? TerrainLod0 : TreesLod0;
            float far = building ? BuildingLod1 : group.StartsWith("Terrain") ? TerrainLod1 : TreesLod1;

            var holder = Child(tile, group);
            holder.gameObject.isStatic = true;
            var oldParents = levels.Where(l => l).Select(l => l.parent).Where(p => p != tile).Distinct().ToList();
            var entries = new List<LOD>();
            if (levels[0]) { levels[0].SetParent(holder, true); entries.Add(new LOD(near, levels[0].GetComponentsInChildren<Renderer>())); }
            if (levels[1]) { levels[1].SetParent(holder, true); entries.Add(new LOD(far, levels[1].GetComponentsInChildren<Renderer>())); }
            if (!levels[0]) stats.Problem(tile.name + ": " + group + " is missing LOD0.");
            else if (!levels[1]) stats.Problem(tile.name + ": " + group + " is missing LOD1.");
            // A single level still uses the far threshold (buildings cull at 0.5% screen height).
            if (entries.Count == 1) entries[0] = new LOD(far, entries[0].renderers);
            var lodGroup = holder.gameObject.AddComponent<LODGroup>();
            lodGroup.SetLODs(entries.ToArray());
            lodGroup.RecalculateBounds();
            // If Blender already had an empty B_<id> parent, remove it now that it is empty.
            foreach (var old in oldParents)
                if (old && old.childCount == 0 && old.GetComponents<Component>().Length == 1) Object.DestroyImmediate(old.gameObject);
            if (building) stats.buildings++; else stats.lodGroups++;
        }
    }
}
