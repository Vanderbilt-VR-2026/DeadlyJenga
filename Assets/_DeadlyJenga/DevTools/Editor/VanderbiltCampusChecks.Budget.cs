using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    // Quest 3 budget per viewpoint (STANDARDS §10): <= 250k visible triangles and <= 150 draw calls.
    // "Visible" = inside a 90 degree, 700 m frustum after picking each LODGroup's level by screen size
    // (the same formula as the builder: VanderbiltCampusBuilder.ScreenHeight).
    // Main result at lodBias 1 (the conservative Quest assumption); also shown at the editor's lodBias.
    // Draw calls: A = one per renderer submesh (today's gray materials), B = one per renderer
    // (every mat_ submesh sharing ONE palette material). Neither assumes static batching.
    public static partial class VanderbiltCampusChecks
    {
        const int TriangleBudget = 250000, DrawBudget = 150;
        const float Fov = 90, FarClip = 700, EyeHeight = 1.7f;
        static readonly string[] Roles = { "terrain LOD0", "terrain LOD1", "buildings LOD0", "buildings LOD1", "trees LOD0", "trees LOD1", "ring", "HLOD", "other" };

        sealed class ViewCount
        {
            public int triangles, drawsPerSubmesh, drawsPerRenderer;
            public readonly Dictionary<string, int[]> roles = Roles.ToDictionary(r => r, r => new int[2]);   // [triangles, draws A]
        }

        static List<(string name, Vector3 pos, Vector3 look)> Views(Transform root, VanderbiltCampusMap map, VanderbiltCampusData data)
        {
            var views = data.viewpoints.Select(v => (v.name, pos: root.TransformPoint(v.pos), look: root.TransformPoint(v.look))).ToList();
            if (views.Count > 0) return views;
            report.AppendLine("NOTE No viewpoints in unity_map.json: using the 5 tile centers with the most triangles.");
            return BusiestTiles(root, map);
        }

        static Camera ViewCamera()
        {
            var camera = new GameObject("Budget camera").AddComponent<Camera>();
            camera.enabled = false; camera.fieldOfView = Fov; camera.aspect = 1; camera.nearClipPlane = .05f; camera.farClipPlane = FarClip;
            return camera;
        }

        static void Aim(Camera camera, Vector3 pos, Vector3 look)
        {
            camera.transform.position = pos;
            var dir = look - pos; if (dir.sqrMagnitude < .01f) dir = Vector3.forward;
            camera.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }

        static void Budget(Transform root, VanderbiltCampusMap map, VanderbiltCampusData data)
        {
            var camera = ViewCamera();
            float editorBias = QualitySettings.lodBias;   // read only, never written
            try
            {
                foreach (var view in Views(root, map, data))
                {
                    Aim(camera, view.pos, view.look);
                    var count = Count(root, camera, 1);
                    // Main verdict = play-mode truth: Presentation swaps every mat_* for ONE material, so a renderer is one draw (B).
                    Check(count.triangles <= TriangleBudget && count.drawsPerRenderer <= DrawBudget,
                        $"Budget '{view.name}' at {view.pos:F0} (lodBias 1, play mode with one shared palette material): " +
                        $"tris={count.triangles} (limit {TriangleBudget}), draws B={count.drawsPerRenderer} (limit {DrawBudget}, one per renderer)");
                    report.AppendLine($"  info, gray-box mode (Presentation removed, one draw per submesh): draws A={count.drawsPerSubmesh} (same limit {DrawBudget}: " +
                        (count.drawsPerSubmesh <= DrawBudget ? "within" : "over") + ")");
                    report.AppendLine("  by role (tris / draws A): " + string.Join(", ",
                        count.roles.Where(r => r.Value[1] > 0).Select(r => $"{r.Key} {r.Value[0]} / {r.Value[1]}")));
                    if (!Mathf.Approximately(editorBias, 1))
                    {
                        var editor = Count(root, camera, editorBias);
                        report.AppendLine($"  at editor lodBias {editorBias}: tris={editor.triangles} draws A={editor.drawsPerSubmesh} B={editor.drawsPerRenderer}");
                    }
                }
            }
            finally { Object.DestroyImmediate(camera.gameObject); }
        }

        static string RoleOf(Renderer r)
        {
            string n = Regex.Replace(r.name, @"\.\d+$", "");
            if (n == "Terrain") return "terrain LOD0";
            if (n == "Terrain_LOD1") return "terrain LOD1";
            if (n == "Trees") return "trees LOD0";
            if (n == "Trees_LOD1") return "trees LOD1";
            if (n == "Ring") return "ring";
            if (n == "HLOD") return "HLOD";
            if (n.StartsWith("B_") && n.EndsWith("_LOD0")) return "buildings LOD0";
            if (n.StartsWith("B_") && n.EndsWith("_LOD1")) return "buildings LOD1";
            return "other";
        }

        // The LOD level a camera would show (-1 = culled), from our own math: no project setting is read or changed.
        static int ChooseLod(LODGroup group, LOD[] lods, Camera camera, float lodBias)
        {
            var center = group.transform.TransformPoint(group.localReferencePoint);
            float scale = Mathf.Max(Mathf.Abs(group.transform.lossyScale.x), Mathf.Abs(group.transform.lossyScale.y), Mathf.Abs(group.transform.lossyScale.z));
            float distance = Vector3.Distance(camera.transform.position, center);
            float screen = VanderbiltCampusBuilder.ScreenHeight(group.size * scale, distance, camera.fieldOfView) * lodBias;
            for (int i = 0; i < lods.Length; i++) if (screen >= lods[i].screenRelativeTransitionHeight) return i;
            return -1;
        }

        // Counts what a camera would draw: frustum test plus the LOD level each LODGroup would show.
        static ViewCount Count(Transform root, Camera camera, float lodBias)
        {
            var planes = GeometryUtility.CalculateFrustumPlanes(camera);
            var hidden = new HashSet<Renderer>();
            foreach (var group in root.GetComponentsInChildren<LODGroup>())
            {
                var lods = group.GetLODs();
                int chosen = ChooseLod(group, lods, camera, lodBias);
                for (int i = 0; i < lods.Length; i++)
                    if (i != chosen) foreach (var r in lods[i].renderers) if (r) hidden.Add(r);
                if (chosen >= 0) foreach (var r in lods[chosen].renderers) if (r) hidden.Remove(r);
            }
            var count = new ViewCount();
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
            {
                if (!r.enabled || hidden.Contains(r)) continue;
                var filter = r.GetComponent<MeshFilter>(); var mesh = filter ? filter.sharedMesh : null;
                if (!mesh || !GeometryUtility.TestPlanesAABB(planes, r.bounds)) continue;
                int tris = VanderbiltCampusBuilder.Triangles(mesh);
                count.triangles += tris; count.drawsPerSubmesh += mesh.subMeshCount; count.drawsPerRenderer++;
                var role = count.roles[RoleOf(r)]; role[0] += tris; role[1] += mesh.subMeshCount;
            }
            return count;
        }

        // Fallback viewpoints: the 5 tiles with the most triangles, 1.7 m above their ground, looking at the map center.
        static List<(string name, Vector3 pos, Vector3 look)> BusiestTiles(Transform root, VanderbiltCampusMap map)
        {
            var center = map.Center;
            var tiles = new List<(string name, int tris, Vector3 middle)>();
            foreach (Transform tile in root)
            {
                if (!tile.name.StartsWith("T_")) continue;
                var renderers = tile.GetComponentsInChildren<MeshRenderer>();
                if (renderers.Length == 0) continue;
                var b = renderers[0].bounds; foreach (var r in renderers) b.Encapsulate(r.bounds);
                int tris = renderers.Sum(r => r.GetComponent<MeshFilter>() && r.GetComponent<MeshFilter>().sharedMesh ? VanderbiltCampusBuilder.Triangles(r.GetComponent<MeshFilter>().sharedMesh) : 0);
                tiles.Add((tile.name, tris, b.center));
            }
            var views = new List<(string, Vector3, Vector3)>();
            foreach (var tile in tiles.OrderByDescending(t => t.tris).Take(5))
            {
                var top = new Vector3(tile.middle.x, map.TopHeight + 50, tile.middle.z);
                float ground = Physics.Raycast(top, Vector3.down, out var hit, map.TopHeight - map.lowestGround + 200, 1 << Layers.Surface, QueryTriggerInteraction.Ignore)
                    ? hit.point.y : map.lowestGround;
                var eye = new Vector3(tile.middle.x, ground + EyeHeight, tile.middle.z);
                var look = new Vector3(center.x, eye.y, center.z);
                if ((look - eye).sqrMagnitude < 1) look = eye + Vector3.forward;
                views.Add((tile.name + " center", eye, look));
            }
            return views;
        }

        static string Safe(string name) => new string(name.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_').ToArray());
    }
}
