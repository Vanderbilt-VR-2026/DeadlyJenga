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
            public bool open, batch, written, lobby, diagnose, rainbowMotion;
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
            j.diagnose = args.Contains("-diagnose");
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

        // Scripted batch entry: capture just these maps (all views) into one gallery folder, then exit.
        public static void BatchMaps(ICollection<string> ids, string output)
        {
            Begin(new Job { batch = true, output = output, views = MapPictureRig.AllViews }, null, false, ids);
        }

        // Same, with chosen views. A view "pov|name|x|y|z|lookX|lookY|lookZ" is a player-eye shot: the camera stands
        // on the first surface below (x, y + 1.6, z) relative to the map center, 1 m up, looking at the look point.
        public static void BatchMaps(ICollection<string> ids, string output, string[] views)
        {
            Begin(new Job { batch = true, output = output, views = views }, null, false, ids);
        }

        public static void CaptureCloudCityBank()
        {
            BatchMaps(new[]{"epic-candy-sky-ring-city"},Path.GetFullPath(Path.Combine("Logs","Captures","CloudCityBank")),
                new[]{"reference","cloud-landing","cloud-underside"});
        }

        public static void CaptureCloudCityRiver()
        {
            BatchMaps(new[]{"epic-candy-sky-ring-city"},Path.GetFullPath(Path.Combine("Logs","Captures","CloudCityRiver")),
                new[]{"reference","river-route","river-basin","cloud-landing","pov|river-bend|-39|1|22|-44|1|17"});
        }

        public static void CaptureCloudCityRainbows()
        {
            Begin(new Job { batch=true, rainbowMotion=true, views=Array.Empty<string>(),
                output=Path.GetFullPath(Path.Combine("Logs","Captures","CloudCityRainbows")) },
                "epic-candy-sky-ring-city",false);
        }

        public static void CaptureCloudCityResidents()
        {
            BatchMaps(new[]{"epic-candy-sky-ring-city"}, Path.GetFullPath(Path.Combine("Logs","Captures","CloudCityResidents")),
                new[]{"reference","resident-party"});
        }

        public static void CaptureCloudCityFalls()
        {
            BatchMaps(new[]{"epic-candy-sky-ring-city"}, Path.GetFullPath(Path.Combine("Logs","Captures","CloudCityFalls")),
                new[]{"reference","cloud-fall-inside","cloud-fall-outside","cloud-resident-inside"});
        }

        static void PlaceCloudFallCamera(Camera camera,string view,IWorldQuery world)
        {
            var bank=GameObject.Find("Visual · sculpted cloud skirt").GetComponentInChildren<MeshRenderer>().bounds;
            Vector3 interior=bank.center;
            Vector3 exterior=interior+Vector3.right*(bank.extents.x+8);
            float death=world.WaterHeight(interior);
            if(death>=bank.min.y || bank.min.y-death>12 || Mathf.Abs(world.WaterHeight(exterior)-death)>.01f)
                throw new InvalidOperationException("Fall line must be just below the bank, inside and outside.");
            if(view=="cloud-fall-inside")
            {
                var probe=new GameObject("Cloud fall controller probe");
                probe.layer=Layers.Survivor;
                try
                {
                    var controller=probe.AddComponent<CharacterController>();
                    controller.radius=.25f;
                    controller.excludeLayers=TraversalCollision.CapsuleExclusions;
                    var foundation=((EpicCityMap)world).foundation.GetComponent<MeshCollider>();
                    // Find bare foundation with no bridge or building underneath the capsule.
                    bool tested=false;
                    for(int i=0;i<96 && !tested;i++)
                    {
                        float angle=i*Mathf.PI/48;
                        var start=world.Center+new Vector3(Mathf.Cos(angle)*76,12,Mathf.Sin(angle)*76);
                        if(!foundation.Raycast(new Ray(start,Vector3.down),out var ground,25) ||
                            Physics.SphereCast(start,.40f,Vector3.down,out _,25,TraversalCollision.Mask,QueryTriggerInteraction.Ignore))continue;
                        probe.transform.position=ground.point+Vector3.up*3;
                        Physics.SyncTransforms();
                        for(int step=0;step<26;step++)controller.Move(Vector3.down*.25f);
                        tested=probe.transform.position.y<ground.point.y-2;
                    }
                    if(!tested)throw new InvalidOperationException("Player capsule could not fall through open cloud.");
                    probe.transform.position=exterior+Vector3.up*6;
                    Physics.SyncTransforms();
                    for(int step=0;step<120;step++)controller.Move(Vector3.down*.5f);
                    if(probe.transform.position.y>=death)throw new InvalidOperationException("Outside fall blocked.");
                }
                finally{Object.DestroyImmediate(probe);}
            }
            var at=view=="cloud-resident-inside"
                ? GameObject.Find("Cloud residents").GetComponentInChildren<MeshRenderer>().bounds.center
                : view=="cloud-fall-inside" ? interior : exterior;
            camera.transform.SetPositionAndRotation(at,Quaternion.LookRotation(Vector3.left));
        }

        static Vector3[] residentStarts;
        static float residentSampleTime;

        static bool CheckResidents()
        {
            var root = GameObject.Find("Cloud residents").transform;
            var actors = root.GetComponentsInChildren<MeshRenderer>();
            var sizes = root.Cast<Transform>().Select(group => group.childCount).OrderBy(count => count);
            if (!sizes.SequenceEqual(new[]{1,1,1,1,1,1,1,1,1,1,2,2,2,4,5}) || actors.Length != 25 ||
                actors.Select(a => a.GetComponent<MeshFilter>().sharedMesh).Distinct().Count() != 6 ||
                actors.Select(a => a.sharedMaterial).Distinct().Count() != 4 ||
                actors.Any(a => !a.sharedMaterial.shader.isSupported || a.bounds.size.magnitude < .6f || a.bounds.size.magnitude > 4f) ||
                root.GetComponentsInChildren<Collider>().Length != 0 || root.GetComponentsInChildren<Rigidbody>().Length != 0)
                throw new InvalidOperationException("Cloud resident count, asset, scale or physics check failed.");
            if (residentStarts == null)
            {
                residentStarts = actors.Select(a => a.transform.position).ToArray();
                residentSampleTime = Time.time;
                return false;
            }
            if (Time.time - residentSampleTime < .65f) return false;
            for (int i = 0; i < actors.Length; i++)
                if (Vector3.Distance(actors[i].transform.position, residentStarts[i]) < .2f)
                    throw new InvalidOperationException("Resident stopped: " + actors[i].name);
            return true;
        }

        static int rainbowFrame;
        static float lastFlowTime;

        static bool CaptureRainbowFrame(ISessionInfo session)
        {
            var falls=GameObject.Find("Visual · rainbow waterfall above");
            if(!falls)throw new InvalidOperationException("Spring waterfall missing");
            float time=falls.GetComponent<Renderer>().sharedMaterial.GetFloat("_FlowTime");
            if(rainbowFrame>0 && time-lastFlowTime<.085f)return false;
            lastFlowTime=time;
            var camera=MakeCamera(session);
            try
            {
                camera.fieldOfView=44;
                void Shot(string subject,string file,bool bridge=false)
                {
                    var go=GameObject.Find("Visual · "+subject);
                    if(!go)throw new InvalidOperationException(subject+" missing");
                    var r=go.GetComponent<Renderer>();
                    if(!r.sharedMaterial.shader.isSupported)throw new InvalidOperationException("Unsupported rainbow shader");
                    var b=r.bounds;
                    var look=b.center;
                    Vector3 offset;
                    if(bridge)offset=Vector3.Cross((go.transform.TransformPoint(go.GetComponent<MeshFilter>().sharedMesh.vertices[0])-look).normalized,Vector3.up).normalized*b.size.magnitude*1.15f;
                    else
                    {
                        var splash=GameObject.Find("Visual · rainbow splash "+subject.Substring("rainbow waterfall ".Length));
                        if(!splash)throw new InvalidOperationException("Missing waterfall impact: "+subject);
                        float width=splash.GetComponent<Renderer>().bounds.size.y*.5f;
                        look=splash.transform.position+Vector3.up*width*.35f;
                        offset=go.transform.TransformDirection(new Vector3(.65f,1.1f,.65f))*width*1.55f;
                    }
                    camera.transform.position=look+offset;camera.transform.LookAt(look);
                    fogFocus=look;fogRadius=b.extents.magnitude;
                    Render(camera,1600,1000,Path.Combine(job.gallery,file+".png"));
                }
                Shot("rainbow waterfall above","spring-"+rainbowFrame.ToString("D3"));
                if(rainbowFrame==0 || rainbowFrame==17)
                {
                    foreach(string end in new[]{"hamlet","rim"})Shot("rainbow waterfall "+end,end+"-"+rainbowFrame);
                    Shot("bridge current","bridge-"+rainbowFrame,true);
                }
                File.AppendAllText(Path.Combine(job.gallery,"sample-times.txt"),time.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+Environment.NewLine);
            }
            finally { Object.Destroy(camera.gameObject); }
            return ++rainbowFrame==18;
        }

        // Far plane for player-eye shots; 0 keeps the usual long picture range.
        public static float PlayerEyeFar;

        static bool PlayerEye(Camera camera, string view, IWorldQuery world, out string name)
        {
            name = null; var p = view.Split('|');
            if (p.Length != 8 || p[0] != "pov") return false;
            var f = new float[6];
            for (int i = 0; i < 6; i++) f[i] = float.Parse(p[i + 2], System.Globalization.CultureInfo.InvariantCulture);
            name = p[1];
            var guess = world.Center + new Vector3(f[0], f[1], f[2]);
            var feet = guess;
            if (Physics.Raycast(guess + Vector3.up * 1.6f, Vector3.down, out var hit, 40, ~0, QueryTriggerInteraction.Ignore)) feet = hit.point;
            camera.transform.position = feet + Vector3.up * 1f;
            camera.transform.LookAt(world.Center + new Vector3(f[3], f[4], f[5]));
            camera.fieldOfView = 70;
            // Optional: see only as far as a player does (no pushed-out far plane or fog).
            if (PlayerEyeFar > 0) { camera.farClipPlane = PlayerEyeFar; fogRadius = 0; }
            return true;
        }

        static void Size(string text, ref int w, ref int h)
        {
            var p = text?.Split('x');
            if (p != null && p.Length == 2 && int.TryParse(p[0], out int a) && int.TryParse(p[1], out int b)) { w = a; h = b; }
        }

        static string LobbyFolder() =>
            Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(ProjectPath.FolderOf("CandyEnvironment.cs").TrimEnd('/'))), "Resources", "LobbyPreviews");

        // In a Windows editor set to the Android target, some map shaders (Ninja Temple blocks) draw as flat,
        // unlit colours. Pictures match the game best with the Windows target.
        static void WarnIfAndroid()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
                Debug.LogWarning("Map capture: the editor is on the Android build target; some map shaders look flat here. Switch to Windows (or run with -buildTarget Win64) for true-to-game pictures.");
        }

        static void FinishBatch(int code)
        {
            if (string.IsNullOrEmpty(SessionState.GetString("DJWarm.id", "")))
            {
                EditorApplication.Exit(code);
                return;
            }
            Directory.CreateDirectory(Path.Combine("Logs", "Warm"));
            File.WriteAllText(Path.Combine("Logs", "Warm", "exit.txt"), code.ToString());
        }

        static void Begin(Job j, string onlyMap, bool missingOnly, ICollection<string> onlyMaps = null)
        {
            WarnIfAndroid();
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("Stop Play Mode first."); return; }
            if (j.clip.Length > 0 && !CameraPath.Modes.Contains(j.clip)) { Debug.LogError("Unknown clip " + j.clip); if (j.batch) FinishBatch(2); return; }
            var catalog = Object.Instantiate(Resources.Load<IslandLobbyCatalog>("IslandLobby/Catalog"));
            catalog.EnsureModeMaps();
            foreach (var map in catalog.maps)
            {
                if (!string.IsNullOrEmpty(onlyMap) && map.id != onlyMap) continue;
                if (onlyMaps != null && !onlyMaps.Contains(map.id)) continue;
                if (missingOnly && File.Exists(Path.Combine(LobbyFolder(), map.id + ".png"))) continue;
                j.maps.Add(map.id);
                j.modes.Add(string.IsNullOrEmpty(map.recommendedMode) ? "demo" : map.recommendedMode);
            }
            Object.DestroyImmediate(catalog);
            if (j.maps.Count == 0) { Debug.LogError("No maps to capture."); if (j.batch) FinishBatch(2); return; }
            j.gallery = !string.IsNullOrEmpty(j.output) && !Path.HasExtension(j.output) ? Path.GetFullPath(j.output)
                : Path.GetFullPath(Path.Combine("Logs", "Captures", DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-live-maps"));
            if (!j.batch && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Save(j);
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            EditorApplication.EnterPlaymode();
        }

        static void BeginLobby(Job j)
        {
            WarnIfAndroid();
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
                prepared = false; rainbowFrame=0; residentStarts=null;
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
                if (job.batch) FinishBatch(job.written && job.failed.Count == 0 ? 0 : 2);
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
            if(job.views.Contains("resident-party"))
            {
                try { if(!CheckResidents())return; }
                catch(Exception e){ Debug.LogException(e); Fail(id,e.Message); return; }
            }
            if(job.rainbowMotion)
            {
                try { if(!CaptureRainbowFrame(session))return; }
                catch(Exception e){Debug.LogException(e);Fail(id,e.Message);return;}
            }
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
            var tablet = GameObject.Find("Headset player tablet");
            if (tablet) tablet.SetActive(false);
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
            // Best subject: the map's own blocks (the tower/town players play on).
            var blockBounds = world.Blocks.Where(b => b).SelectMany(b => b.GetComponentsInChildren<Renderer>(false)).Where(r => r.enabled).Select(r => r.bounds).ToList();
            if (blockBounds.Count >= 4)
            {
                bounds = blockBounds[0];
                foreach (var b in blockBounds.Skip(1)) bounds.Encapsulate(b);
                bounds.Expand(new Vector3(4, 2, 4));
            }
            else if (renderers.Length > 0) { bounds = renderers[0].bounds; foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds); }
            else bounds = new Bounds(world.Center + Vector3.up * world.TopHeight * .5f, new Vector3(world.Radius * 2, Mathf.Max(4, world.TopHeight), world.Radius * 2));

            bounds = ClampToPlayArea(bounds, world);
            var game = session.ActiveCamera;
            if (job.diagnose) Diagnose(id, world, root, renderers, bounds, game);
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

                fogFocus = bounds.center; fogRadius = bounds.extents.magnitude;
                string lobby = Path.Combine(LobbyFolder(), id + ".png");
                bool single = job.maps.Count == 1 && job.views.Length == 1 && job.clip.Length == 0 && Path.HasExtension(job.output);
                foreach (var view in job.views)
                {
                    camera.fieldOfView = 38;
                    if (PlayerEye(camera, view, world, out string eye))
                    {
                        string eyePath = Path.Combine(job.gallery, id + "-" + eye + ".png");
                        Render(camera, job.width, job.height, eyePath);
                        gallery.Add(eyePath, id + " · " + eye, "image", job.width, job.height);
                        continue;
                    }
                    string path = single ? job.output : string.IsNullOrEmpty(job.output) && view == "hero" ? lobby : Path.Combine(job.gallery, id + "-" + view + ".png");
                    if (view == "reference" && id == "epic-candy-sky-ring-city")
                    {
                        // Fitted to the nine roof centres in the original 1600 x 1100 map screenshot.
                        camera.fieldOfView = 36.94f;
                        camera.transform.SetPositionAndRotation(world.Center + new Vector3(240.93f,200.47f,-298.43f),
                            Quaternion.Euler(24.87f,-38.81f,0));
                    }
                    else if(view.StartsWith("cloud-fall-",StringComparison.Ordinal) || view=="cloud-resident-inside")
                        PlaceCloudFallCamera(camera,view,world);
                    else if(view=="resident-party")
                    {
                        var party = GameObject.Find("Cloud residents").transform.GetChild(0);
                        var lead = party.GetChild(0).GetComponent<Renderer>();
                        var heading = Vector3.ProjectOnPlane(lead.transform.position - residentStarts[0],Vector3.up).normalized;
                        var boundsParty = lead.bounds;
                        foreach(var actor in party.GetComponentsInChildren<MeshRenderer>()) boundsParty.Encapsulate(actor.bounds);
                        camera.fieldOfView = 48;
                        camera.transform.position = boundsParty.center + heading * 6 + Vector3.Cross(Vector3.up,heading)*2 + Vector3.up*1.9f;
                        camera.transform.LookAt(boundsParty.center);
                    }
                    else if(view=="river-route" || view=="river-basin")
                    {
                        var river=GameObject.Find("Visual · city rainbow river").GetComponent<MeshRenderer>();
                        var pool=GameObject.Find("Visual · lower cloud pool").transform;
                        var splash=GameObject.Find("Visual · rainbow splash rim").transform;
                        if(Vector3.Distance(pool.position,splash.position)>.02f)
                            throw new InvalidOperationException("Rim splash missed the receiving pool");
                        if(view=="river-route")
                        {
                            var at=river.bounds.center;
                            camera.transform.position=at+new Vector3(-48,160,-58);
                            camera.transform.LookAt(at);
                            camera.fieldOfView=55;
                        }
                        else
                        {
                            var at=Vector3.Lerp(pool.position,GameObject.Find("Visual · rim spillway").transform.position,.32f);
                            camera.transform.position=at+new Vector3(-82,48,110);
                            camera.transform.LookAt(at);
                        }
                    }
                    else if(view=="cloud-landing" || view=="cloud-underside")
                    {
                        var bank=GameObject.Find("Visual · sculpted cloud skirt").GetComponentInChildren<MeshRenderer>();
                        if(bank.sharedMaterial.shader.name!="DeadlyJenga/Island Cumulus" || !bank.sharedMaterial.shader.isSupported)
                            throw new InvalidOperationException("Cloud bank material unavailable");
                        var b=bank.bounds;
                        if(view=="cloud-landing")
                        {
                            var impact=GameObject.Find("Visual · rainbow splash above").transform.position;
                            var outward=Vector3.ProjectOnPlane(impact-b.center,Vector3.up).normalized;
                            camera.transform.position=impact+outward*84+Vector3.Cross(outward,Vector3.up)*58+Vector3.up*25;
                            camera.transform.LookAt(impact+Vector3.down*5);
                        }
                        else
                        {
                            camera.transform.position=b.center+new Vector3(.62f,-.38f,-1.16f)*b.size.x;
                            camera.transform.LookAt(b.center);
                        }
                    }
                    else MapPictureRig.Place(camera, view, bounds, job.width / (float)job.height);
                    // "balloon": a close look at the first hot-air balloon, when the map has one (else the hero view).
                    var balloon = view == "balloon" ? GameObject.Find("Visual · hot-air balloon 1") : null;
                    if (balloon)
                    {
                        var at = balloon.transform.position + Vector3.down * 2;
                        camera.transform.position = at + new Vector3(16, 3, -22);
                        camera.transform.LookAt(at);
                    }
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

        // Big ground plates, snowfall and backdrops can be far larger than where people play.
        // Keep the frame inside the map's playable radius (flat maps such as Christmas Town especially).
        static Bounds ClampToPlayArea(Bounds bounds, IWorldQuery world)
        {
            if (world == null || world.Radius <= 0) return bounds;
            float half = world.Radius * 1.1f;
            var c = world.Center;
            float minY = Mathf.Max(bounds.min.y, world.WaterHeight(c) - 1), maxY = Mathf.Min(bounds.max.y, c.y + Mathf.Max(world.TopHeight, 4) + world.Radius * .6f);
            if (maxY <= minY) maxY = minY + 4;
            float hx = Mathf.Min(bounds.extents.x, half), hz = Mathf.Min(bounds.extents.z, half);
            var clamped = new Bounds();
            clamped.SetMinMax(new Vector3(c.x - hx, minY, c.z - hz), new Vector3(c.x + hx, maxY, c.z + hz));
            return clamped;
        }

        // -diagnose: write what was actually loaded and a frame through the game's own camera, for comparison.
        static void Diagnose(string id, IWorldQuery world, Transform root, Renderer[] kept, Bounds bounds, Camera game)
        {
            var all = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"map {id}: world {world.GetType().Name} on '{root?.name}' scene '{root?.gameObject.scene.name}' center {world.Center} radius {world.Radius} top {world.TopHeight} water {world.WaterHeight(world.Center)}");
            sb.AppendLine($"blocks {world.Blocks.Count}; renderers under map {(root ? root.GetComponentsInChildren<Renderer>(false).Length : 0)}; kept for framing {kept.Length}; scene renderers {all.Length}");
            sb.AppendLine($"framed bounds center {bounds.center} size {bounds.size}");
            foreach (var big in all.Where(r => r.enabled).OrderByDescending(r => r.bounds.size.magnitude).Take(8))
                sb.AppendLine($"  big renderer '{big.name}' parent '{big.transform.parent?.name}' size {big.bounds.size} at {big.bounds.center} layer {big.gameObject.layer}");
            var roots = root ? root.gameObject.scene.GetRootGameObjects() : Array.Empty<GameObject>();
            sb.AppendLine("scene roots: " + string.Join(", ", roots.Select(g => g.name + (g.activeInHierarchy ? "" : "(off)"))));
            if (game)
            {
                var d = game.GetUniversalAdditionalCameraData();
                sb.AppendLine($"game camera '{game.name}' pos {game.transform.position} fwd {game.transform.forward} mask {game.cullingMask} clear {game.clearFlags} far {game.farClipPlane} post {d.renderPostProcessing} type {d.renderType}");
                string path = Path.Combine(job.gallery, id + "-gamecamera.png");
                var rt = RenderTexture.GetTemporary(1280, 720, 24);
                var old = game.targetTexture; game.targetTexture = rt; game.Render(); game.targetTexture = old;
                var prev = RenderTexture.active; RenderTexture.active = rt;
                var img = new Texture2D(1280, 720, TextureFormat.RGB24, false); img.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); img.Apply();
                RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
                Directory.CreateDirectory(job.gallery); File.WriteAllBytes(path, img.EncodeToPNG()); Object.Destroy(img);
                sb.AppendLine("game camera frame: " + path);
            }
            sb.AppendLine($"lights on: {Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Count(l => l.enabled)}; ambient {RenderSettings.ambientMode} {RenderSettings.ambientSkyColor}; skybox {RenderSettings.skybox?.name}");
            Directory.CreateDirectory(job.gallery);
            File.WriteAllText(Path.Combine(job.gallery, id + "-diagnose.txt"), sb.ToString());
            Debug.Log("MAP DIAGNOSE " + sb);
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
            fogFocus = bounds.center; fogRadius = bounds.extents.magnitude;
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

        // Subject the shot is about (set before each Render); fog is pushed just past it for the picture.
        static Vector3 fogFocus; static float fogRadius;
        static readonly int NinjaFog = Shader.PropertyToID("_NinjaFogParams");

        static void Render(Camera camera, int width, int height, string path)
        {
            bool fog = RenderSettings.fog; var mode = RenderSettings.fogMode;
            float start = RenderSettings.fogStartDistance, end = RenderSettings.fogEndDistance, density = RenderSettings.fogDensity;
            if (fog && fogRadius > 0)
            {
                float d = Vector3.Distance(camera.transform.position, fogFocus);
                if (mode == FogMode.Linear) { if (end < d + fogRadius * 1.5f) { RenderSettings.fogStartDistance = Mathf.Max(start, d - fogRadius * .2f); RenderSettings.fogEndDistance = d + fogRadius * 3f; } }
                else RenderSettings.fogDensity = Mathf.Min(density, 1.2f / (d + fogRadius));
            }
            // Maps with their own shader fog (Ninja Temple) keep start/end in a global; push it out the same way.
            var customFog = Shader.GetGlobalVector(NinjaFog);
            if (fogRadius > 0 && customFog.y > customFog.x && customFog.x > 0)
            {
                float d = Vector3.Distance(camera.transform.position, fogFocus);
                Shader.SetGlobalVector(NinjaFog, new Vector4(Mathf.Max(customFog.x, d - fogRadius * .2f), Mathf.Max(customFog.y, d + fogRadius * 3f), customFog.z, customFog.w));
            }
            try { RenderNow(camera, width, height, path); }
            finally
            {
                RenderSettings.fogStartDistance = start; RenderSettings.fogEndDistance = end; RenderSettings.fogDensity = density;
                Shader.SetGlobalVector(NinjaFog, customFog);
            }
        }

        static void RenderNow(Camera camera, int width, int height, string path)
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
