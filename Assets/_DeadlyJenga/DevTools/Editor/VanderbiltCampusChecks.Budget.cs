using System.Collections.Generic;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    // Quest 3 budget per viewpoint (STANDARDS §10): <= 250k visible triangles and <= 150 draw calls.
    // "Visible" = inside a 90 degree, 700 m frustum after picking each LODGroup's level by screen size.
    // Two draw-call numbers: A = one per renderer submesh (today's gray materials), B = one per renderer
    // (every mat_ submesh sharing ONE palette material). Neither assumes static batching.
    public static partial class VanderbiltCampusChecks
    {
        const int TriangleBudget = 250000, DrawBudget = 150;
        const float Fov = 90, FarClip = 700, EyeHeight = 1.7f;

        struct ViewCount { public int triangles, drawsPerSubmesh, drawsPerRenderer; }

        static void Budget(Transform root, VanderbiltCampusMap map, VanderbiltCampusData data)
        {
            var views = data.viewpoints.Select(v => (v.name, pos: root.TransformPoint(v.pos), look: root.TransformPoint(v.look))).ToList();
            if (views.Count == 0)
            {
                report.AppendLine("NOTE No viewpoints in unity_map.json: using the 5 tile centers with the most triangles.");
                views = BusiestTiles(root, map);
            }
            var go = new GameObject("Budget camera"); var camera = go.AddComponent<Camera>();
            camera.enabled = false; camera.fieldOfView = Fov; camera.aspect = 1; camera.nearClipPlane = .05f; camera.farClipPlane = FarClip;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.55f, .75f, .92f);
            try
            {
                foreach (var view in views)
                {
                    camera.transform.position = view.pos;
                    var dir = view.look - view.pos; if (dir.sqrMagnitude < .01f) dir = Vector3.forward;
                    camera.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
                    var count = Count(root, camera);
                    bool pass = count.triangles <= TriangleBudget && count.drawsPerSubmesh <= DrawBudget;
                    Check(pass, $"Budget '{view.name}' at {view.pos:F0}: tris={count.triangles} draws(A per submesh)={count.drawsPerSubmesh} " +
                        $"(limits {TriangleBudget}/{DrawBudget}); draws(B one shared material)={count.drawsPerRenderer} " +
                        (count.drawsPerRenderer <= DrawBudget ? "within" : "over") + " budget");
                    Capture(camera, 1600, 1600, "VanderbiltCampus-view-" + Safe(view.name) + ".png");
                }
            }
            finally { Object.DestroyImmediate(go); }
            report.AppendLine("NOTE LOD choice uses QualitySettings.lodBias=" + QualitySettings.lodBias + " of the editor's quality level; Quest may differ.");
        }

        // Counts what a camera would draw: frustum test plus the LOD level each LODGroup would show.
        static ViewCount Count(Transform root, Camera camera)
        {
            var planes = GeometryUtility.CalculateFrustumPlanes(camera);
            var hidden = new HashSet<Renderer>();
            foreach (var group in root.GetComponentsInChildren<LODGroup>())
            {
                var lods = group.GetLODs();
                var center = group.transform.TransformPoint(group.localReferencePoint);
                float scale = Mathf.Max(Mathf.Abs(group.transform.lossyScale.x), Mathf.Abs(group.transform.lossyScale.y), Mathf.Abs(group.transform.lossyScale.z));
                float distance = Mathf.Max(.01f, Vector3.Distance(camera.transform.position, center));
                float screen = VanderbiltCampusBuilder.ScreenHeight(group.size * scale, distance, camera.fieldOfView) * QualitySettings.lodBias;
                int chosen = -1;
                for (int i = 0; i < lods.Length; i++) if (screen >= lods[i].screenRelativeTransitionHeight) { chosen = i; break; }
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
                count.triangles += VanderbiltCampusBuilder.Triangles(mesh);
                count.drawsPerSubmesh += mesh.subMeshCount;
                count.drawsPerRenderer++;
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

        // Straight-down orthographic render covering localArea exactly, north (+Z) up.
        static void Overhead(VanderbiltCampusMap map)
        {
            var area = map.localArea;
            var min = map.transform.TransformPoint(area.min); var max = map.transform.TransformPoint(area.max);
            float sizeX = Mathf.Max(1, max.x - min.x), sizeZ = Mathf.Max(1, max.z - min.z);
            int width = sizeX >= sizeZ ? 4096 : Mathf.Max(1, Mathf.RoundToInt(4096 * sizeX / sizeZ));
            int height = sizeZ >= sizeX ? 4096 : Mathf.Max(1, Mathf.RoundToInt(4096 * sizeZ / sizeX));
            var go = new GameObject("Overhead camera"); var camera = go.AddComponent<Camera>();
            try
            {
                camera.enabled = false; camera.orthographic = true;
                camera.orthographicSize = sizeZ * .5f; camera.aspect = sizeX / sizeZ;
                camera.transform.position = new Vector3((min.x + max.x) * .5f, max.y + 50, (min.z + max.z) * .5f);
                camera.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                camera.nearClipPlane = .1f; camera.farClipPlane = max.y - min.y + 150;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                Capture(camera, width, height, "VanderbiltCampus-overhead.png");
                report.AppendLine($"OVERHEAD Logs/Validation/VanderbiltCampus-overhead.png {width}x{height} px covers world X {min.x:F2}..{max.x:F2}, Z {min.z:F2}..{max.z:F2} " +
                    $"(map root at origin, so campus frame = world). {sizeX / width:F4} m per pixel; top of image = north (+Z), left = west (-X).");
            }
            finally { Object.DestroyImmediate(go); }
        }

        static string Safe(string name)
        {
            var chars = name.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_').ToArray();
            return new string(chars);
        }
    }
}
