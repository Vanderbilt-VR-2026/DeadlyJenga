using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    // Map pictures taken in the real game: each map is loaded in Game.unity in Play Mode exactly as the lobby
    // starts it (its recommended mode, no bots), so the map's own art is there (Ninja moonlit lake, Christmas snow,
    // Castle coast with boats and sea life). The player, HUD and bots are hidden, then the same views and
    // clips as MapPictureRig are taken. Nothing is saved to any scene; everything stays on this computer.
    //
    // Editor: Tools > Deadly Jenga > Maps > Map Pictures > Full: ... (Quick: ... is the fast preview-scene rig).
    // CLI tip: MapPictureRig.Batch with -full (or -lobby) runs this same capture.
    // CLI (needs graphics; on this laptop add -force-d3d11):
    //   Unity -batchmode -projectPath <project> -executeMethod DeadlyJenga.DevTools.Editor.MapLiveCapture.Batch
    //     same options as MapPictureRig: -mapId, -mapView hero|top|front|close|all, -mapOut, -mapMissing,
    //     -mapClip orbit|arc|dolly|pan|crane, -clipSeconds, -clipFps, -clipSize, -open
    //     plus [-settle 6]  seconds to let the map finish building before the pictures
    // Hero pictures go to Presentation/Resources/LobbyPreviews/<map id>.png unless -mapOut is given.
    //
    // Lobby survey: -lobby [-spots 40]  loads Lobby.unity and photographs the Marina from many cameras:
    // an aerial ring at two heights, a straight-down view, and eye-level shots from walkable spots all over
    // the lobby (four directions each, plus one toward the middle). Optional -mapClip adds an orbit/arc clip.
    [InitializeOnLoad]
    public static class MapLiveCapture
    {
        const string Key = "DeadlyJenga.MapLiveCapture";

        [Serializable] sealed class Job
        {
            public List<string> maps = new(), modes = new();
            public int index;
            public string[] views = { "hero" };
            public string output = "", gallery = "", clip = "";
            public float seconds = 6, settle = 6;
            public int fps = 30, clipWidth = 1280, clipHeight = 720, width = 1600, height = 1100;
            public bool open, batch, written, lobby;
            public int spots = 40;
            public List<string> failed = new();
        }

        static Job job;
        static double enteredAt, readyAt;
        static bool prepared;

        static MapLiveCapture() { EditorApplication.playModeStateChanged += OnState; }

        [MenuItem("Tools/Deadly Jenga/Maps/Map Pictures/Full: Render All (lobby pictures)")]
        static void RenderAll() => Begin(new Job { open = CaptureOutput.OpenByDefault }, null, false);

        [MenuItem("Tools/Deadly Jenga/Maps/Map Pictures/Full: Render Missing (lobby pictures)")]
        static void RenderMissing() => Begin(new Job { open = CaptureOutput.OpenByDefault }, null, true);

        [MenuItem("Tools/Deadly Jenga/Maps/Map Pictures/Full: Lobby Survey (many cameras)")]
        static void LobbySurvey() => BeginLobby(new Job { lobby = true, open = CaptureOutput.OpenByDefault });

        [MenuItem("Tools/Deadly Jenga/Maps/Map Pictures/Full: All Views + Orbit (to Logs)")]
        static void RenderEverything() => Begin(new Job { views = MapPictureRig.AllViews, clip = "orbit", output = Path.GetFullPath(Path.Combine("Logs", "Captures", DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-live-maps")), open = CaptureOutput.OpenByDefault }, null, false);

        public static void Batch()
        {
            var args = Environment.GetCommandLineArgs();
            string Arg(string name) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
            var j = new Job { batch = true, open = args.Contains("-open"), output = Arg("-mapOut") ?? "", clip = (Arg("-mapClip") ?? "").ToLowerInvariant() };
            string view = (Arg("-mapView") ?? (j.clip.Length > 0 ? "none" : "hero")).ToLowerInvariant();
            j.views = view == "all" ? MapPictureRig.AllViews : view == "none" ? Array.Empty<string>() : new[] { view };
            if (float.TryParse(Arg("-settle"), out float settle)) j.settle = Mathf.Clamp(settle, 1, 60);
            if (float.TryParse(Arg("-clipSeconds"), out float sec)) j.seconds = Mathf.Clamp(sec, 1, 60);
            if (int.TryParse(Arg("-clipFps"), out int fps)) j.fps = Mathf.Clamp(fps, 10, 60);
            Size(Arg("-clipSize"), ref j.clipWidth, ref j.clipHeight);
            Size(Arg("-mapSize"), ref j.width, ref j.height);
            if (args.Contains("-lobby"))
            {
                j.lobby = true;
                if (int.TryParse(Arg("-spots"), out int spots)) j.spots = Mathf.Clamp(spots, 4, 200);
                if (Arg("-mapSize") == null) { j.width = 1280; j.height = 720; }
                BeginLobby(j);
                return;
            }
            Begin(j, Arg("-mapId"), args.Contains("-mapMissing"));
        }

        static void Size(string text, ref int w, ref int h)
        {
            var p = text?.Split('x');
            if (p != null && p.Length == 2 && int.TryParse(p[0], out int a) && int.TryParse(p[1], out int b)) { w = a; h = b; }
        }

        static string LobbyFolder() =>
            Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(ProjectPath.FolderOf("CandyEnvironment.cs").TrimEnd('/'))), "Resources", "LobbyPreviews");

        static void Begin(Job j, string onlyMap, bool missingOnly)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("Stop Play Mode first."); return; }
            if (j.clip.Length > 0 && !CameraPath.Modes.Contains(j.clip)) { Debug.LogError("Unknown clip " + j.clip); if (j.batch) EditorApplication.Exit(2); return; }
            var catalog = Object.Instantiate(Resources.Load<IslandLobbyCatalog>("IslandLobby/Catalog"));
            catalog.EnsureModeMaps();
            foreach (var map in catalog.maps)
            {
                if (!string.IsNullOrEmpty(onlyMap) && map.id != onlyMap) continue;
                if (missingOnly && File.Exists(Path.Combine(LobbyFolder(), map.id + ".png"))) continue;
                j.maps.Add(map.id);
                j.modes.Add(string.IsNullOrEmpty(map.recommendedMode) ? "demo" : map.recommendedMode);
            }
            Object.DestroyImmediate(catalog);
            if (j.maps.Count == 0) { Debug.LogError("No maps to capture."); if (j.batch) EditorApplication.Exit(2); return; }
            j.gallery = !string.IsNullOrEmpty(j.output) && !Path.HasExtension(j.output) ? Path.GetFullPath(j.output)
                : Path.GetFullPath(Path.Combine("Logs", "Captures", DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-live-maps"));
            if (!j.batch && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Save(j);
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            EditorApplication.EnterPlaymode();
        }

        static void BeginLobby(Job j)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("Stop Play Mode first."); return; }
            j.maps.Add("lobby"); j.modes.Add("");
            j.gallery = !string.IsNullOrEmpty(j.output) ? Path.GetFullPath(j.output)
                : Path.GetFullPath(Path.Combine("Logs", "Captures", DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-lobby-survey"));
            j.output = j.gallery;
            if (!j.batch && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Save(j);
            EditorSceneManager.OpenScene(ProjectPath.Find("Lobby.unity"));
            EditorApplication.EnterPlaymode();
        }

        static void Save(Job j) => SessionState.SetString(Key, JsonUtility.ToJson(j));

        static void OnState(PlayModeStateChange change)
        {
            string json = SessionState.GetString(Key, "");
            if (string.IsNullOrEmpty(json)) return;
            job = JsonUtility.FromJson<Job>(json);
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                // Same choices the lobby makes for a solo sandbox start of this map.
                Application.runInBackground = true;
                AudioListener.volume = 0;
                enteredAt = EditorApplication.timeSinceStartup; readyAt = 0; prepared = false;
                if (job.lobby) { EditorApplication.update += Tick; return; }
                MatchSetup.FromLobby = false;
                MatchSetup.StartLocalSession = false;
                MatchSetup.MapId = job.maps[job.index];
                // Mode maps (Ninja Temple, CTF, Tycoon...) are built by their own game mode, so start that mode.
                MatchSetup.ModeId = job.modes.Count > job.index ? job.modes[job.index] : "demo";
                MatchSetup.BotTarget = 0;
                MatchSetup.HandCount = 0;
                enteredAt = EditorApplication.timeSinceStartup;
                readyAt = 0;
                prepared = false;
                EditorApplication.update += Tick;
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= Tick;
                job.index++;
                if (job.index < job.maps.Count)
                {
                    Save(job);
                    EditorApplication.EnterPlaymode();
                    return;
                }
                SessionState.EraseString(Key);
                var gallery = new CaptureOutput("Live map pictures", job.gallery);
                gallery.Finish(job.open);
                if (string.IsNullOrEmpty(job.output)) AssetDatabase.Refresh();
                if (job.failed.Count > 0) Debug.LogWarning("Live map capture could not load: " + string.Join(", ", job.failed));
                if (job.batch) EditorApplication.Exit(job.written ? 0 : 2);
            }
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            string id = job.maps[job.index];
            double now = EditorApplication.timeSinceStartup;
            var session = Services.Session;
            var world = Services.World;
            bool loaded = job.lobby ? session != null && now - enteredAt > 5
                : session != null && world != null && world.Blocks != null && world.Blocks.Count > 0 && MatchSetup.MapId == id;
            if (!loaded && !job.lobby && world != null && now - enteredAt > 8) loaded = MatchSetup.MapId == id; // maps without blocks
            if (!loaded)
            {
                if (now - enteredAt > 45) { Fail(id, "map did not finish loading"); }
                return;
            }
            if (!prepared)
            {
                if (!job.lobby)
                {
                    // The session may already have built its default map; swap to ours the same way the game does.
                    MatchSetup.MapId = id;
                    MatchSetup.ApplyMap();
                }
                Prepare(session); prepared = true; readyAt = now + job.settle; return;
            }
            if (now < readyAt) { Prepare(session); return; }
            EditorApplication.update -= Tick;
            try { if (job.lobby) Survey(world, session); else Shoot(id, world, session); job.written = true; }
            catch (Exception e) { Debug.LogException(e); job.failed.Add(id); }
            Save(job);
            EditorApplication.ExitPlaymode();
        }

        static void Fail(string id, string reason)
        {
            EditorApplication.update -= Tick;
            Debug.LogWarning("Live map capture skipped " + id + ": " + reason);
            job.failed.Add(id);
            Save(job);
            EditorApplication.ExitPlaymode();
        }

        // Hide everything that is not the map: our own character, flying hand, bots and all UI.
        static void Prepare(ISessionInfo session)
        {
            if (session.SurvivorBots != null) { session.SurvivorBots.AutoFill = false; session.SurvivorBots.Running = false; session.SurvivorBots.Clear(); }
            if (session.HandBots != null) { session.HandBots.Running = false; session.HandBots.Clear(); }
            foreach (var actor in new[] { session.Player, session.Hand })
            {
                if (!NetworkActors.Alive(actor)) continue;
                actor.InputSuspended = true;
                foreach (var r in actor.gameObject.GetComponentsInChildren<Renderer>(true)) r.forceRenderingOff = true;
            }
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.enabled = false;
        }

        static void Shoot(string id, IWorldQuery world, ISessionInfo session)
        {
            var gallery = new CaptureOutput("Live map pictures", job.gallery);
            var root = (world as Component)?.transform;
            // Frame the playable area: skip big backdrops (ground planes, lakes, skies) and anything far outside the map.
            float reach = Mathf.Max(15, world.Radius * 1.6f);
            var renderers = root ? root.GetComponentsInChildren<Renderer>(false).Where(r => r.enabled && !r.forceRenderingOff &&
                r.bounds.size.sqrMagnitude > .001f && r.bounds.size.magnitude < reach * 2.2f &&
                Vector2.Distance(new Vector2(r.bounds.center.x, r.bounds.center.z), new Vector2(world.Center.x, world.Center.z)) < reach).ToArray() : Array.Empty<Renderer>();
            Bounds bounds;
            if (renderers.Length > 0) { bounds = renderers[0].bounds; foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds); }
            else bounds = new Bounds(world.Center + Vector3.up * world.TopHeight * .5f, new Vector3(world.Radius * 2, Mathf.Max(4, world.TopHeight), world.Radius * 2));

            var game = session.ActiveCamera;
            var camera = new GameObject("Live map picture camera", typeof(Camera)).GetComponent<Camera>();
            try
            {
                camera.enabled = false;
                if (game) camera.CopyFrom(game);
                camera.cullingMask &= ~(1 << 5); // no UI
                camera.fieldOfView = 38;
                camera.farClipPlane = Mathf.Max(camera.farClipPlane, 4000);
                camera.targetTexture = null;
                var data = camera.GetUniversalAdditionalCameraData();
                data.allowXRRendering = false;
                if (game) { var g = game.GetUniversalAdditionalCameraData(); data.renderPostProcessing = g.renderPostProcessing; data.volumeLayerMask = g.volumeLayerMask; }

                string lobby = Path.Combine(LobbyFolder(), id + ".png");
                bool single = job.maps.Count == 1 && job.views.Length == 1 && job.clip.Length == 0 && Path.HasExtension(job.output);
                foreach (var view in job.views)
                {
                    string path = single ? job.output : string.IsNullOrEmpty(job.output) && view == "hero" ? lobby : Path.Combine(job.gallery, id + "-" + view + ".png");
                    MapPictureRig.Place(camera, view, bounds, job.width / (float)job.height);
                    Render(camera, job.width, job.height, path);
                    gallery.Add(path, id + " · " + view, "image", job.width, job.height);
                }
                if (job.clip.Length > 0)
                {
                    MapPictureRig.Place(camera, "hero", bounds, job.clipWidth / (float)job.clipHeight);
                    var start = camera.transform.position - bounds.center;
                    string frames = gallery.PathFor(id + "-" + job.clip + "-frames");
                    Directory.CreateDirectory(frames);
                    int count = Mathf.Max(2, Mathf.RoundToInt(job.seconds * job.fps));
                    for (int f = 0; f < count; f++)
                    {
                        CameraPath.Evaluate(job.clip, f / (float)(count - 1), bounds.center, start.magnitude, start, out var position, out var rotation);
                        camera.transform.SetPositionAndRotation(position, rotation);
                        Render(camera, job.clipWidth, job.clipHeight, Path.Combine(frames, $"frame_{f:0000}.png"));
                    }
                    string mp4 = gallery.PathFor(id + "-" + job.clip + ".mp4");
                    if (CaptureOutput.EncodeClip(frames, job.fps, mp4)) gallery.Add(mp4, id + " · " + job.clip + " clip", "video", job.clipWidth, job.clipHeight);
                }
                gallery.SaveManifest();
            }
            finally { Object.Destroy(camera.gameObject); }
        }

        static Camera MakeCamera(ISessionInfo session)
        {
            var game = session.ActiveCamera;
            var camera = new GameObject("Live picture camera", typeof(Camera)).GetComponent<Camera>();
            camera.enabled = false;
            if (game) camera.CopyFrom(game);
            camera.cullingMask &= ~(1 << 5);
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, 4000);
            camera.nearClipPlane = .1f;
            camera.targetTexture = null;
            var data = camera.GetUniversalAdditionalCameraData();
            data.allowXRRendering = false;
            if (game) { var g = game.GetUniversalAdditionalCameraData(); data.renderPostProcessing = g.renderPostProcessing; data.volumeLayerMask = g.volumeLayerMask; }
            return camera;
        }

        // Many cameras around and inside the lobby.
        static void Survey(IWorldQuery world, ISessionInfo session)
        {
            var gallery = new CaptureOutput("Lobby survey", job.gallery);
            var middle = world?.Center ?? ((session as Component) ? ((Component)session).transform.position : Vector3.zero);
            float worldRadius = world?.Radius ?? 40;
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r => r.enabled && !r.forceRenderingOff &&
                r.gameObject.layer != 5 && r.bounds.size.magnitude < 600 && r.bounds.size.sqrMagnitude > .01f &&
                (r.bounds.center - middle).sqrMagnitude < 250f * 250f).ToArray();
            var bounds = new Bounds(middle, Vector3.one * Mathf.Max(20, worldRadius * 2));
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            var camera = MakeCamera(session);
            float aspect = job.width / (float)job.height;
            int shot = 0;
            void Take(Vector3 position, Vector3 lookAt, string label, float fov)
            {
                camera.fieldOfView = fov;
                camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position));
                string file = Path.Combine(job.gallery, $"lobby-{++shot:000}-{label}.png");
                Render(camera, job.width, job.height, file);
                gallery.Add(file, label.Replace('-', ' '), "image", job.width, job.height);
            }
            try
            {
                var center = bounds.center;
                // Eye-level spots on walkable ground, spread on a jittered grid.
                var spots = new List<Vector3>();
                float water = world?.WaterHeight(center) ?? bounds.min.y;
                int grid = Mathf.CeilToInt(Mathf.Sqrt(job.spots * 12f));
                var random = new System.Random(5);
                for (int gx = 0; gx < grid && spots.Count < job.spots; gx++)
                    for (int gz = 0; gz < grid && spots.Count < job.spots; gz++)
                    {
                        float x = Mathf.Lerp(bounds.min.x, bounds.max.x, (gx + .2f + .6f * (float)random.NextDouble()) / grid);
                        float z = Mathf.Lerp(bounds.min.z, bounds.max.z, (gz + .2f + .6f * (float)random.NextDouble()) / grid);
                        var top = new Vector3(x, bounds.max.y + 5, z);
                        // Every surface under this point, top to bottom; keep the lowest walkable floor with standing room
                        // (so plazas under canopies count, and roofs or umbrella tops do not).
                        Vector3? floor = null;
                        foreach (var h in Physics.RaycastAll(top, Vector3.down, bounds.size.y + 20, ~0, QueryTriggerInteraction.Ignore))
                        {
                            int layer = h.collider.gameObject.layer;
                            if (layer != Layers.Surface && layer != Layers.Block) continue;
                            if (h.normal.y < .7f || h.point.y < water + .05f) continue;
                            if (Physics.CheckSphere(h.point + Vector3.up * 1.65f, .35f, ~0, QueryTriggerInteraction.Ignore)) continue;
                            if (floor == null || h.point.y < floor.Value.y) floor = h.point;
                        }
                        if (floor == null) continue;
                        var hit = new RaycastHit { point = floor.Value };
                        if (spots.Any(s => (s - hit.point).sqrMagnitude < 9)) continue;
                        spots.Add(hit.point);
                    }
                // Frame the aerial views on where people can actually walk, not on far ships and sea life.
                if (spots.Count >= 3)
                {
                    var walk = new Bounds(spots[0], Vector3.one);
                    foreach (var p in spots) walk.Encapsulate(p);
                    walk.Expand(new Vector3(12, 8, 12));
                    center = walk.center;
                    bounds = walk;
                }
                float radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
                // Aerial ring: 12 directions at a high and a low angle.
                foreach (float elevation in new[] { 38f, 16f })
                    for (int i = 0; i < 12; i++)
                    {
                        float yaw = i * 30f;
                        var dir = Quaternion.Euler(0, yaw, 0) * (Quaternion.Euler(elevation, 0, 0) * Vector3.back) ;
                        Take(center + dir * radius * (elevation > 20 ? 1.9f : 1.6f), center, $"aerial-{(elevation > 20 ? "high" : "low")}-{yaw:000}", 40);
                    }
                Take(center + Vector3.up * radius * 2.3f + Vector3.back * .01f, center, "overhead", 45);

                for (int i = 0; i < spots.Count; i++)
                {
                    var eye = spots[i] + Vector3.up * 1.65f;
                    string place = $"spot{i + 1:00}";
                    for (int d = 0; d < 4; d++)
                    {
                        var dir = Quaternion.Euler(0, d * 90f, 0) * Vector3.forward;
                        if (Physics.Raycast(eye, dir, 1.5f, ~0, QueryTriggerInteraction.Ignore)) continue; // staring at a wall
                        Take(eye, eye + dir * 10 + Vector3.down * .6f, $"{place}-{new[] { "north", "east", "south", "west" }[d]}", 70);
                    }
                    var toMiddle = new Vector3(center.x, eye.y, center.z);
                    if ((toMiddle - eye).sqrMagnitude > 4) Take(eye, toMiddle, $"{place}-toward-middle", 60);
                }

                if (job.clip.Length > 0)
                {
                    camera.fieldOfView = 40;
                    var start = Quaternion.Euler(30, 0, 0) * Vector3.back * radius * 1.8f;
                    string frames = gallery.PathFor("lobby-" + job.clip + "-frames");
                    Directory.CreateDirectory(frames);
                    int count = Mathf.Max(2, Mathf.RoundToInt(job.seconds * job.fps));
                    for (int f = 0; f < count; f++)
                    {
                        CameraPath.Evaluate(job.clip, f / (float)(count - 1), center, start.magnitude, start, out var position, out var rotation);
                        camera.transform.SetPositionAndRotation(position, rotation);
                        Render(camera, job.clipWidth, job.clipHeight, Path.Combine(frames, $"frame_{f:0000}.png"));
                    }
                    string mp4 = gallery.PathFor("lobby-" + job.clip + ".mp4");
                    if (CaptureOutput.EncodeClip(frames, job.fps, mp4)) gallery.Add(mp4, "lobby " + job.clip + " clip", "video", job.clipWidth, job.clipHeight);
                }
                gallery.SaveManifest();
                Debug.Log($"Lobby survey: {shot} pictures, {spots.Count} eye-level spots.");
            }
            finally { Object.Destroy(camera.gameObject); }
        }

        static void Render(Camera camera, int width, int height, string path)
        {
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 4);
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                File.WriteAllBytes(path, image.EncodeToPNG());
                Debug.Log("CAPTURE FILE " + Path.GetFullPath(path));
            }
            finally { RenderTexture.active = previous; camera.targetTexture = null; RenderTexture.ReleaseTemporary(target); Object.Destroy(image); }
        }
    }
}
