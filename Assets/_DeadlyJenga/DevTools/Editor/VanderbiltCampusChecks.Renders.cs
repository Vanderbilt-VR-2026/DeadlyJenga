using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.CastlePrototype;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    // Evidence renders of the temp campus instance only:
    //   VanderbiltCampus-view-<name>.png   one per budget viewpoint, palette colors
    //   VanderbiltCampus-overhead.png      straight down over localArea, palette colors
    //   VanderbiltCampus-overhead-gray.png the same with the gray World materials
    // Only the campus is drawn: its renderers move to a spare layer (31) that the cameras and the light
    // cull to, so other open scenes (ocean, lights) never show. Everything is put back afterwards.
    public static partial class VanderbiltCampusChecks
    {
        const int RenderLayer = 31;
        static readonly Color Sky = new Color(.62f, .78f, .93f);

        static void Renders(Transform root, VanderbiltCampusMap map, VanderbiltCampusData data, Scene scene)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var layers = renderers.ToDictionary(r => r, r => r.gameObject.layer);
            var materials = renderers.ToDictionary(r => r, r => r.sharedMaterials);
            var otherLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.enabled).ToList();
            var ambient = (RenderSettings.ambientMode, RenderSettings.ambientLight, RenderSettings.fog, RenderSettings.sun);
            float bias = QualitySettings.lodBias;
            var sun = new GameObject("Check sunlight"); SceneManager.MoveGameObjectToScene(sun, scene);
            var palette = PaletteMaterial();
            try
            {
                foreach (var r in renderers) r.gameObject.layer = RenderLayer;
                foreach (var l in otherLights) l.enabled = false;
                var light = sun.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1; light.shadows = LightShadows.Soft;
                light.cullingMask = 1 << RenderLayer;
                sun.transform.rotation = Quaternion.Euler(50, -30, 0);   // 50 degrees above the horizon
                RenderSettings.sun = light; RenderSettings.fog = false;
                RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.42f, .44f, .48f);

                if (palette)
                    foreach (var r in renderers)
                        if (!InVisual(r.transform, root)) r.sharedMaterials = Enumerable.Repeat(palette, r.sharedMaterials.Length).ToArray();
                ViewRenders(root, map, data);
                Overhead(root, map, "VanderbiltCampus-overhead.png", palette ? "palette colors" : "gray (CampusPalette.mat not found)");
                foreach (var r in renderers) r.sharedMaterials = materials[r];
                Overhead(root, map, "VanderbiltCampus-overhead-gray.png", "gray World materials");
            }
            finally
            {
                foreach (var r in renderers) if (r) { r.gameObject.layer = layers[r]; r.sharedMaterials = materials[r]; }
                foreach (var l in otherLights) if (l) l.enabled = true;
                (RenderSettings.ambientMode, RenderSettings.ambientLight, RenderSettings.fog, RenderSettings.sun) = ambient;
                QualitySettings.lodBias = bias;
                Object.DestroyImmediate(sun);
                if (palette) Object.DestroyImmediate(palette);
            }
        }

        // A temporary copy of Presentation's CampusPalette.mat (found by name), with a palette strip if it has none.
        static Material PaletteMaterial()
        {
            try
            {
                var asset = AssetDatabase.LoadAssetAtPath<Material>(ProjectPath.Find("CampusPalette.mat"));
                if (!asset) return null;
                var copy = new Material(asset) { name = "Campus palette (check copy)" };
                if (copy.HasProperty("_Palette") && !copy.GetTexture("_Palette"))
                {
                    try { copy.SetTexture("_Palette", AssetDatabase.LoadAssetAtPath<Texture2D>(ProjectPath.Find("CampusPaletteCandy.png"))); }
                    catch (FileNotFoundException) { }
                }
                return copy;
            }
            catch (FileNotFoundException) { report.AppendLine("NOTE CampusPalette.mat not found: renders use the gray materials."); return null; }
        }

        static Camera RenderCamera(Camera camera)
        {
            camera.cullingMask = 1 << RenderLayer;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Sky;
            return camera;
        }

        static void ViewRenders(Transform root, VanderbiltCampusMap map, VanderbiltCampusData data)
        {
            var camera = RenderCamera(ViewCamera());
            try
            {
                QualitySettings.lodBias = 1;   // render what the lodBias 1 budget counted
                foreach (var view in Views(root, map, data))
                {
                    Aim(camera, view.pos, view.look);
                    Capture(camera, 1600, 1600, "VanderbiltCampus-view-" + Safe(view.name) + ".png");
                }
            }
            finally { Object.DestroyImmediate(camera.gameObject); }
        }

        // Straight-down orthographic render covering localArea exactly. The camera looks along -Y with its
        // "up" set to +Z, so its right is +X: image top = north (+Z), image left = west (-X).
        static void Overhead(Transform root, VanderbiltCampusMap map, string fileName, string look)
        {
            var area = map.localArea;
            var min = map.transform.TransformPoint(area.min); var max = map.transform.TransformPoint(area.max);
            float sizeX = Mathf.Max(1, max.x - min.x), sizeZ = Mathf.Max(1, max.z - min.z);
            // Near and far planes must hold everything from the lowest ground to the highest roof.
            float top = max.y, bottom = Mathf.Min(min.y, map.transform.TransformPoint(new Vector3(0, map.lowestGround, 0)).y);
            foreach (var r in root.GetComponentsInChildren<Renderer>()) { top = Mathf.Max(top, r.bounds.max.y); bottom = Mathf.Min(bottom, r.bounds.min.y); }
            int width = sizeX >= sizeZ ? 4096 : Mathf.Max(1, Mathf.RoundToInt(4096 * sizeX / sizeZ));
            int height = sizeZ >= sizeX ? 4096 : Mathf.Max(1, Mathf.RoundToInt(4096 * sizeZ / sizeX));
            var camera = RenderCamera(new GameObject("Overhead camera").AddComponent<Camera>());
            try
            {
                QualitySettings.lodBias = 1000;   // always the most detailed level from above
                camera.enabled = false; camera.orthographic = true;
                camera.orthographicSize = sizeZ * .5f; camera.aspect = sizeX / sizeZ;
                camera.transform.position = new Vector3((min.x + max.x) * .5f, top + 20, (min.z + max.z) * .5f);
                camera.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                camera.nearClipPlane = 1; camera.farClipPlane = top + 20 - bottom + 20;
                Capture(camera, width, height, fileName);
                report.AppendLine($"OVERHEAD Logs/Validation/{fileName} ({look}) {width}x{height} px covers world X {min.x:F2}..{max.x:F2}, Z {min.z:F2}..{max.z:F2} " +
                    $"(map root at origin, so campus frame = world); {sizeX / width:F4} m per pixel; top = north (+Z), left = west (-X); heights {bottom:F1}..{top:F1} m.");
            }
            finally { Object.DestroyImmediate(camera.gameObject); }
        }
    }
}
