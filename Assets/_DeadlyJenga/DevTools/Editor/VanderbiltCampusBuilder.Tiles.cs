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
    //   COL_Terrain, COL_Trees      -> static non-convex MeshCollider on Layers.Surface (renderer removed)
    //   COL_B_<id>_<n>              -> static convex MeshCollider, grouped under COL_B_<id> with CampusClimbSurface
    //   Terrain, B_<id>_LOD0, Trees -> tile LOD0 (detail)
    //   Terrain_LOD1, B_<id>_LOD1, Trees_LOD1 -> tile LOD1 (mid distance)
    //   Ring                        -> tile LOD0 and LOD1 (and LOD2 when there is no HLOD)
    //   HLOD                        -> tile LOD2 (far): one merged mesh (from the exporter, or built here)
    //   Spawn_*, Route_*, Climb_*   -> empty markers (moved under "Markers")
    //   anything else with a mesh   -> static visual, never a collider, always drawn
    //
    // One LODGroup per tile on the tile root, never nested: every renderer belongs to exactly one LODGroup,
    // which is the only arrangement Unity handles reliably. (The FBX importer may add its own LODGroup for
    // "_LOD0/_LOD1" names; it is removed first.)
    public static partial class VanderbiltCampusBuilder
    {
        // Tile LOD switch heights (fraction of screen height). Unity: height = size / (2 * distance * tan(fov / 2)),
        // so at 90 degree FOV and lodBias 1, distance = size / (2 * height), measured to the tile's bounds center.
        // A tile's size is about 100 m (more with tall buildings).
        // LOD0 (detail) until 35%  -> about 143 m from the tile center (the nearest tile edge is ~50 m closer).
        // LOD1 (LoD1 meshes) until 20% -> about 250 m.
        // LOD2 (HLOD, one draw call) beyond that, never culled, so the ground always draws.
        // At lodBias 2 (the editor's default quality level) every distance doubles.
        public const float TileDetail = .35f, TileMid = .20f, TileFar = 0f;

        // Fraction of screen height an object of this size fills (Unity's LODGroup rule). The budget check uses it too.
        internal static float ScreenHeight(float size, float distance, float fovDegrees) =>
            size / (2 * Mathf.Max(.01f, distance) * Mathf.Tan(fovDegrees * .5f * Mathf.Deg2Rad));

        static readonly Regex BuildingCollider = new Regex(@"^COL_B_(.+)_(\d+)$");
        static readonly Regex BuildingLod = new Regex(@"^B_(.+)_LOD([01])$");

        // Blender may add ".001" to duplicate names; ignore it when reading roles.
        static string CleanName(string name) => Regex.Replace(name, @"\.\d+$", "");

        // Renderers of one tile sorted by LOD role.
        sealed class TileParts
        {
            public Renderer terrain, terrainLod1, trees, treesLod1, hlod;
            public readonly List<Renderer> ring = new List<Renderer>();
            public readonly Dictionary<string, Renderer[]> buildings = new Dictionary<string, Renderer[]>();
        }

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
            foreach (var auto in tile.GetComponentsInChildren<LODGroup>(true)) Object.DestroyImmediate(auto);
            stats.tiles++;

            var colliders = Child(tile.transform, "Colliders");
            var markers = Child(tile.transform, "Markers");
            var buildingColliders = new Dictionary<string, Transform>();
            var parts = new TileParts();
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
                    if (name.StartsWith("Route_")) stats.routeMarkers.Add(t);
                }
                else
                {
                    var lod = BuildingLod.Match(name);
                    bool mid = name.EndsWith("_LOD1");
                    var renderer = MakeVisual(t, mid, stats);
                    if (!renderer) continue;
                    if (lod.Success)
                    {
                        string id = lod.Groups[1].Value; int level = lod.Groups[2].Value == "0" ? 0 : 1;
                        if (!parts.buildings.TryGetValue(id, out var pair)) parts.buildings[id] = pair = new Renderer[2];
                        if (pair[level]) stats.Problem(tile.name + ": two objects named B_" + id + "_LOD" + level);
                        pair[level] = renderer;
                    }
                    else if (name == "Terrain") { parts.terrain = renderer; stats.terrain.Add(renderer); }
                    else if (name == "Terrain_LOD1") parts.terrainLod1 = renderer;
                    else if (name == "Trees") parts.trees = renderer;
                    else if (name == "Trees_LOD1") parts.treesLod1 = renderer;
                    else if (name == "Ring") parts.ring.Add(renderer);
                    else if (name == "HLOD") { parts.hlod = renderer; stats.exporterHlods++; }
                }
            }
            TileLods(tile.transform, parts, stats);
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
            // Safety net: PhysX cannot build a convex hull from a flat or tiny mesh and logs errors in the game.
            if (convex && Degenerate(mesh))
            {
                stats.skippedConvex++; if (stats.skippedConvexNames.Count < 15) stats.skippedConvexNames.Add(t.name);
                Object.DestroyImmediate(t.gameObject);
                return false;
            }
            foreach (var r in t.GetComponents<Renderer>()) Object.DestroyImmediate(r);
            Object.DestroyImmediate(filter);
            foreach (var old in t.GetComponents<Collider>()) Object.DestroyImmediate(old);
            t.gameObject.layer = Layers.Surface;
            t.gameObject.isStatic = true;
            var shape = t.gameObject.AddComponent<MeshCollider>();
            shape.sharedMesh = mesh; shape.convex = convex;
            stats.colliders++;
            if (convex)
            {
                stats.convexColliders++;
                var size = mesh.bounds.size;   // bounds are available even when the mesh is not readable
                if (Mathf.Max(size.x, size.y, size.z) < .5f || size.x * size.y * size.z < .05f) stats.tinyConvex++;
            }
            return true;
        }

        // True when a convex collider mesh is too thin (smallest side under 5 cm) or its points do not span
        // a volume (fewer than 4 points off one plane). Meshes are readable here because this runs in edit mode.
        static bool Degenerate(Mesh mesh)
        {
            var size = mesh.bounds.size;
            if (Mathf.Min(size.x, size.y, size.z) < .05f) return true;
            Vector3[] v;
            try { v = mesh.vertices; } catch (System.Exception) { return false; }
            if (v.Length == 0 && mesh.vertexCount > 0) return false;   // data not accessible: only the size test applies
            if (v.Length < 4) return true;
            var a = v[0];
            int bi = -1; for (int i = 1; i < v.Length; i++) if ((v[i] - a).sqrMagnitude > 1e-4f) { bi = i; break; }
            if (bi < 0) return true;
            var ab = v[bi] - a; Vector3 normal = Vector3.zero;
            for (int i = 0; i < v.Length; i++)
            {
                var n = Vector3.Cross(ab, v[i] - a);
                if (n.sqrMagnitude > 1e-6f) { normal = n.normalized; break; }
            }
            if (normal == Vector3.zero) return true;   // all points on one line
            for (int i = 0; i < v.Length; i++) if (Mathf.Abs(Vector3.Dot(v[i] - a, normal)) > .01f) return false;
            return true;   // all points on one plane
        }

        // Visual meshes are fully static (isStatic sets every flag, including Batching Static) and never collide.
        static MeshRenderer MakeVisual(Transform t, bool lod1, BuildStats stats)
        {
            foreach (var c in t.GetComponents<Collider>()) Object.DestroyImmediate(c);
            t.gameObject.isStatic = true;
            var filter = t.GetComponent<MeshFilter>(); var renderer = t.GetComponent<MeshRenderer>();
            if (!filter || !filter.sharedMesh || !renderer) return null;
            stats.visuals.Add(renderer);
            stats.renderers++; stats.submeshes += filter.sharedMesh.subMeshCount;
            int tris = Triangles(filter.sharedMesh);
            stats.trianglesAll += tris; if (!lod1) stats.trianglesLod0 += tris;
            foreach (var m in renderer.sharedMaterials)
                if (!m || !m.name.StartsWith("mat_")) { stats.Problem(t.name + " uses material '" + (m ? m.name : "none") + "' (needs mat_<id>)."); break; }
            return renderer;
        }

        internal static int Triangles(Mesh mesh)
        {
            long count = 0;
            for (int s = 0; s < mesh.subMeshCount; s++) count += mesh.GetIndexCount(s) / 3;
            return (int)count;
        }

        // One LODGroup on the tile root: LOD0 detail, LOD1 the LoD1 meshes, LOD2 the HLOD (or the LoD1 meshes again).
        // Anything missing a level uses the level it has, so nothing pops out.
        static void TileLods(Transform tile, TileParts parts, BuildStats stats)
        {
            var detail = new List<Renderer>(); var mid = new List<Renderer>();
            void Pair(Renderer near, Renderer far)
            {
                if (near || far) { detail.Add(near ? near : far); mid.Add(far ? far : near); }
            }
            Pair(parts.terrain, parts.terrainLod1);
            Pair(parts.trees, parts.treesLod1);
            foreach (var pair in parts.buildings)
            {
                if (!pair.Value[0] || !pair.Value[1]) stats.buildingsMissingLevel++;
                Pair(pair.Value[0], pair.Value[1]);
                stats.buildings++;
            }
            detail.AddRange(parts.ring); mid.AddRange(parts.ring);
            if (detail.Count == 0) return;

            var hlod = parts.hlod ? parts.hlod : MakeHlod(tile, mid, stats);
            var far = hlod ? new List<Renderer> { hlod } : mid;
            var lodGroup = tile.gameObject.AddComponent<LODGroup>();
            lodGroup.SetLODs(new[]
            {
                new LOD(TileDetail, detail.ToArray()),
                new LOD(TileMid, mid.ToArray()),
                new LOD(TileFar, far.ToArray())
            });
            lodGroup.RecalculateBounds();
            stats.lodGroups++;
        }
    }
}
