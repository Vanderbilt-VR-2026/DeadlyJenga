using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.CandyArt;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    // Takes pictures of lobby-catalog maps in an isolated preview scene, using the game's own
    // ocean/sand/sky environment. Nothing in Game.unity or Lobby.unity is opened or saved, and
    // everything stays on this computer.
    //
    // Views: hero (the lobby map-picker picture), top, front, close.
    // The hero view of each map is written to Presentation/Resources/LobbyPreviews/<map id>.png,
    // which the lobby map picker loads automatically. Other views go to Logs/MapPictures/.
    //
    // Editor: Tools > Deadly Jenga > Maps > Map Pictures > Render All / Render Missing / All Views.
    // CLI (needs graphics, so no -nographics; on this laptop add -force-d3d11):
    //   Unity -batchmode -projectPath <project> -executeMethod DeadlyJenga.DevTools.Editor.MapPictureRig.Batch
    //     [-mapId castle]             only this map (ids as in the lobby catalog)
    //     [-mapView hero|top|front|close|all]   default hero
    //     [-mapOut C:/temp/castle.png]  write here instead (a folder when several pictures are made)
    //     [-mapMissing]               skip maps that already have a lobby picture
    //     [-mapSize 1600x1100]        picture size in pixels
    //     [-mapClip orbit|arc|dolly|pan|crane]  also record a short MP4 with a moving camera (needs ffmpeg)
    //     [-clipSeconds 6] [-clipFps 30] [-clipSize 1280x720]
    //     [-open]                     open the gallery page in the browser afterwards
    // Every run also writes a gallery folder (Logs/Captures/...) with manifest.json, contact-sheet.png and index.html.
    public static class MapPictureRig
    {
        internal static readonly string[] AllViews = { "hero", "top", "front", "close" };

        [MenuItem("Tools/Deadly Jenga/Maps/Map Pictures/Quick: Render All (lobby pictures)")]
        static void RenderAll() => Run(null, new[] { "hero" }, null, false, 1600, 1100, null, CaptureOutput.OpenByDefault);

        [MenuItem("Tools/Deadly Jenga/Maps/Map Pictures/Quick: Render Missing (lobby pictures)")]
        static void RenderMissing() => Run(null, new[] { "hero" }, null, true, 1600, 1100, null, CaptureOutput.OpenByDefault);

        [MenuItem("Tools/Deadly Jenga/Maps/Map Pictures/Quick: Render All Views (to Logs)")]
        static void RenderAllViews() => Run(null, AllViews, null, false, 1600, 1100, null, CaptureOutput.OpenByDefault);

        [MenuItem("Tools/Deadly Jenga/Maps/Map Pictures/Quick: Record Orbit Clips (all maps)")]
        static void RecordOrbits() => Run(null, Array.Empty<string>(), null, false, 1600, 1100, new ClipRequest { mode = "orbit" }, CaptureOutput.OpenByDefault);

        public sealed class ClipRequest { public string mode = "orbit"; public float seconds = 6; public int fps = 30, width = 1280, height = 720; }

        // One entry point for both rigs: add -full for the live in-game capture (MapLiveCapture),
        // or -lobby for the live lobby survey. Without them this is the quick preview-scene capture.
        public static void Batch()
        {
            var args = Environment.GetCommandLineArgs();
            if (args.Contains("-full") || args.Contains("-lobby")) { MapLiveCapture.Batch(); return; }
            string Arg(string name) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
            string view = (Arg("-mapView") ?? "hero").ToLowerInvariant();
            int width = 1600, height = 1100;
            var size = Arg("-mapSize")?.Split('x');
            if (size != null && size.Length == 2 && int.TryParse(size[0], out int w) && int.TryParse(size[1], out int h)) { width = w; height = h; }
            int made = 0;
            ClipRequest clip = null;
            if (Arg("-mapClip") != null)
            {
                clip = new ClipRequest { mode = Arg("-mapClip").ToLowerInvariant() };
                if (float.TryParse(Arg("-clipSeconds"), out float sec)) clip.seconds = Mathf.Clamp(sec, 1, 60);
                if (int.TryParse(Arg("-clipFps"), out int fps)) clip.fps = Mathf.Clamp(fps, 10, 60);
                var cs = Arg("-clipSize")?.Split('x');
                if (cs != null && cs.Length == 2 && int.TryParse(cs[0], out int cw) && int.TryParse(cs[1], out int ch)) { clip.width = cw; clip.height = ch; }
            }
            var views = Arg("-mapView") == null && clip != null ? Array.Empty<string>() : view == "all" ? AllViews : new[] { view };
            try { made = Run(Arg("-mapId"), views, Arg("-mapOut"), args.Contains("-mapMissing"), width, height, clip, args.Contains("-open")); }
            catch (Exception e) { Debug.LogException(e); }
            EditorApplication.Exit(made > 0 ? 0 : 2);
        }

        public static int Run(string onlyMap, string[] views, string output, bool missingOnly, int width, int height, ClipRequest clip = null, bool open = false)
        {
            if (clip != null && !CameraPath.Modes.Contains(clip.mode)) throw new ArgumentException("Unknown clip " + clip.mode + " (" + string.Join(", ", CameraPath.Modes) + ").");
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            foreach (var v in views) if (!AllViews.Contains(v)) throw new ArgumentException("Unknown view " + v + " (hero, top, front, close).");
            var source = Resources.Load<IslandLobbyCatalog>("IslandLobby/Catalog");
            if (!source) throw new InvalidOperationException("IslandLobby/Catalog is missing.");
            var catalog = Object.Instantiate(source);
            catalog.EnsureModeMaps();
            var maps = catalog.maps.Where(m => string.IsNullOrEmpty(onlyMap) || m.id == onlyMap).ToArray();
            if (maps.Length == 0) throw new ArgumentException("No map with id " + onlyMap + ". Ids: " + string.Join(", ", catalog.maps.Select(m => m.id)));

            string lobbyFolder = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(ProjectPath.FolderOf("CandyEnvironment.cs").TrimEnd('/'))), "Resources", "LobbyPreviews");
            string logFolder = Path.Combine("Logs", "MapPictures");
            bool single = maps.Length == 1 && views.Length == 1 && clip == null;
            var gallery = new CaptureOutput("Map pictures" + (string.IsNullOrEmpty(onlyMap) ? "" : " " + onlyMap),
                !string.IsNullOrEmpty(output) && !Path.HasExtension(output) ? output : null);
            int made = 0;
            var oldWorld = Services.World;
            try
            {
                foreach (var map in maps)
                {
                    string lobbyPath = Path.Combine(lobbyFolder, map.id + ".png");
                    if (missingOnly && File.Exists(lobbyPath)) continue;
                    var paths = views.Select(view =>
                        !string.IsNullOrEmpty(output) ? (single && Path.HasExtension(output) ? output : Path.Combine(output, map.id + "-" + view + ".png"))
                        : view == "hero" ? lobbyPath : Path.Combine(logFolder, map.id + "-" + view + ".png")).ToArray();
                    try
                    {
                        Capture(map, catalog, views, paths, width, height, clip, gallery);
                        made += paths.Length + (clip != null ? 1 : 0);
                        for (int i = 0; i < paths.Length; i++) gallery.Add(paths[i], map.id + " · " + views[i], "image", width, height);
                    }
                    catch (Exception e) { Debug.LogError("Map picture failed for " + map.id + ": " + e); }
                    finally { Services.World = oldWorld; }
                }
            }
            finally { Services.World = oldWorld; Object.DestroyImmediate(catalog); }
            if (string.IsNullOrEmpty(output)) AssetDatabase.Refresh();
            gallery.Finish(open);
            Debug.Log("Map pictures: wrote " + made + ".");
            return made;
        }

        static void Capture(IslandLobbyCatalog.Map map, IslandLobbyCatalog catalog, string[] views, string[] paths, int width, int height, ClipRequest clip, CaptureOutput gallery)
        {
            var scene = map.mapPrefab ? EditorSceneManager.NewPreviewScene() : EditorSceneManager.OpenPreviewScene(map.scenePath);
            try
            {
                var root = map.mapPrefab ? PrefabUtility.InstantiatePrefab(map.mapPrefab, scene) as GameObject : null;
                var behaviours = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                foreach (var item in behaviours) if (item is IRebuildable builder) builder.Rebuild();
                behaviours = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                var world = behaviours.FirstOrDefault(c => c is IWorldQuery);
                if (world) Services.World = (IWorldQuery)world;

                // Same ocean, sand and sky the match uses.
                var env = behaviours.OfType<CandyEnvironment>().FirstOrDefault();
                if (!env && catalog.environmentPrefab)
                    env = (PrefabUtility.InstantiatePrefab(catalog.environmentPrefab.gameObject, scene) as GameObject)?.GetComponent<CandyEnvironment>();
                if (env)
                {
                    env.applyLighting = false;
                    if (world)
                    {
                        var baseline = env.GetComponent<CandyArtBaseline>() ?? env.gameObject.AddComponent<CandyArtBaseline>();
                        baseline.map = world;
                        var support = world.GetComponentsInChildren<Collider>().Where(c => !c.attachedRigidbody && !c.isTrigger)
                            .OrderByDescending(c => c.bounds.size.x * c.bounds.size.z).FirstOrDefault();
                        if (support) env.foundation = support.transform;
                    }
                    env.Refresh();
                }

                var renderers = (root ? root.GetComponentsInChildren<Renderer>(true)
                        : scene.GetRootGameObjects().Where(g => !g.GetComponent<CandyEnvironment>()).SelectMany(g => g.GetComponentsInChildren<Renderer>(true)))
                    .Where(r => r.enabled && r.gameObject.activeInHierarchy && r.bounds.size.sqrMagnitude > .001f && r.bounds.size.magnitude < 1800).ToArray();
                Bounds bounds;
                if (renderers.Length > 0) { bounds = renderers[0].bounds; foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds); }
                else if (world is IMapFootprint footprint) bounds = new Bounds(((IWorldQuery)world).Center, new Vector3(footprint.FootprintHalfSize * 2, 1, footprint.FootprintHalfSize * 2));
                else throw new InvalidOperationException("Map has neither visible geometry nor an island footprint.");

                var cameraObject = new GameObject("Map picture camera");
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.scene = scene;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.55f, .78f, .95f);
                camera.fieldOfView = 38;
                camera.nearClipPlane = .3f;
                camera.farClipPlane = 4000;
                var data = camera.GetUniversalAdditionalCameraData();
                data.allowXRRendering = false;

                var sunObject = new GameObject("Map picture sun");
                SceneManager.MoveGameObjectToScene(sunObject, scene);
                var sun = sunObject.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.6f;
                sun.color = new Color(1f, .95f, .86f);
                sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(48, -35, 0);

                var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 4);
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                var previous = RenderTexture.active;
                try
                {
                    for (int i = 0; i < views.Length; i++)
                    {
                        Place(camera, views[i], bounds, width / (float)height);
                        camera.targetTexture = target;
                        camera.Render();
                        RenderTexture.active = target;
                        image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                        image.Apply();
                        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(paths[i])));
                        File.WriteAllBytes(paths[i], image.EncodeToPNG());
                    }
                }
                finally
                {
                    RenderTexture.active = previous;
                    camera.targetTexture = null;
                    RenderTexture.ReleaseTemporary(target);
                    Object.DestroyImmediate(image);
                }
                if (clip != null) RecordClip(camera, map.id, bounds, clip, gallery);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        static void RecordClip(Camera camera, string id, Bounds bounds, ClipRequest clip, CaptureOutput gallery)
        {
            Place(camera, "hero", bounds, clip.width / (float)clip.height);
            var start = camera.transform.position - bounds.center;
            string frames = gallery.PathFor(id + "-" + clip.mode + "-frames");
            Directory.CreateDirectory(frames);
            int count = Mathf.Max(2, Mathf.RoundToInt(clip.seconds * clip.fps));
            var target = RenderTexture.GetTemporary(clip.width, clip.height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 4);
            var image = new Texture2D(clip.width, clip.height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                for (int f = 0; f < count; f++)
                {
                    CameraPath.Evaluate(clip.mode, f / (float)(count - 1), bounds.center, start.magnitude, start, out var position, out var rotation);
                    camera.transform.SetPositionAndRotation(position, rotation);
                    camera.targetTexture = target;
                    camera.Render();
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, clip.width, clip.height), 0, 0);
                    image.Apply();
                    File.WriteAllBytes(Path.Combine(frames, $"frame_{f:0000}.png"), image.EncodeToPNG());
                }
            }
            finally { RenderTexture.active = previous; camera.targetTexture = null; RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(image); }
            string mp4 = gallery.PathFor(id + "-" + clip.mode + ".mp4");
            if (CaptureOutput.EncodeClip(frames, clip.fps, mp4)) gallery.Add(mp4, id + " · " + clip.mode + " clip", "video", clip.width, clip.height);
        }

        // Frame the whole map so its bounding sphere fits the picture.
        internal static void Place(Camera camera, string view, Bounds bounds, float aspect)
        {
            float radius = Mathf.Max(bounds.extents.magnitude, 8f);
            float halfFov = camera.fieldOfView * .5f * Mathf.Deg2Rad;
            float horizontalHalf = Mathf.Atan(Mathf.Tan(halfFov) * aspect);
            float fit = radius / Mathf.Sin(Mathf.Min(halfFov, horizontalHalf));
            Vector3 direction; float distance = fit; var focus = bounds.center;
            switch (view)
            {
                case "top":
                    // Seen from above only the footprint matters, not the height.
                    float flat = Mathf.Max(bounds.extents.x, bounds.extents.z) * 1.1f;
                    direction = new Vector3(0, 1, -.02f);
                    distance = bounds.extents.y + flat / Mathf.Tan(Mathf.Min(halfFov, horizontalHalf));
                    break;
                case "front": direction = new Vector3(0, .25f, -1); break;
                case "close": direction = new Vector3(.7f, .45f, -1); distance = fit * .55f; focus.y = Mathf.Lerp(bounds.min.y, bounds.max.y, .6f); break;
                default: direction = new Vector3(1.05f, .8f, -1.3f); distance = fit * .95f; break; // hero, matches the existing lobby pictures
            }
            camera.transform.position = focus + direction.normalized * distance;
            camera.transform.LookAt(focus);
        }
    }
}
