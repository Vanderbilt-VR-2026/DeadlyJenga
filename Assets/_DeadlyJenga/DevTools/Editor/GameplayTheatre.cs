using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using DeadlyJenga.Modes;
using DeadlyJenga.Presentation;
using UnityEngine.Rendering.Universal;
using DeadlyJenga.World.Hazards;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [Serializable] public sealed class TheatrePlan
    {
        public string title = "Deadly Jenga showreel", map = "castle";
        public int fps = 24, width = 1280, height = 720, seed = 148;
        public float warmup = .75f;
        public Vector3 stageOffset;
        public string ffmpeg = "ffmpeg";
        public TheatreActor[] cast;
        public TheatreShot[] shots;
        public TheatreCue[] cues;

        [Serializable] public sealed class TheatreActor
        {
            public string id;
            public int color;
            public bool live, customLook;
            public AvatarAppearance appearance;
            public Vector3 start;
            public TheatreMove[] moves;
        }

        [Serializable] public sealed class TheatreMove
        {
            public float at, duration = 1, lift;
            public Vector3 to;
        }

        [Serializable] public sealed class TheatreShot
        {
            public string name, subject, second;
            public float start, duration = 2, fov = 52, roll;
            public Vector3 from, to, look;
        }

        [Serializable] public sealed class TheatreCue
        {
            public float at;
            public string action, actor, target, hazard, weapon;
            public float strength, heading, height;
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(map) || string.IsNullOrWhiteSpace(ffmpeg))
                throw new InvalidDataException("Title, map ID and FFmpeg executable are required.");
            if (fps < 12 || fps > 60 || width < 320 || width > 3840 || height < 180 || height > 2160 ||
                width % 2 != 0 || height % 2 != 0 || !Finite(warmup) || !Finite(stageOffset) || warmup < 0 || warmup > 10)
                throw new InvalidDataException("Use even dimensions, 12–60 fps and a warmup of 0–10 seconds.");
            if (cast == null || cast.Length < 1 || cast.Length > 12 || cast.Any(a => a == null || string.IsNullOrWhiteSpace(a.id) || !Finite(a.start)) ||
                cast.Select(a => a.id).Distinct(StringComparer.Ordinal).Count() != cast.Length)
                throw new InvalidDataException("Provide 1–12 uniquely named actors with finite start positions.");
            var ids = new HashSet<string>(cast.Select(a => a.id), StringComparer.Ordinal);
            bool Known(string id) => !string.IsNullOrEmpty(id) && ids.Contains(id);
            foreach (var actor in cast)
            {
                if (actor.live && actor.moves != null && actor.moves.Length > 0)
                    throw new InvalidDataException("Live bots use their gameplay brain and cannot also have an authored motion track.");
                float end = 0;
                foreach (var move in actor.moves ?? Array.Empty<TheatreMove>())
                {
                    if (move == null || !Finite(move.at) || !Finite(move.duration) || !Finite(move.lift) || !Finite(move.to) ||
                        move.at < end || move.duration <= 0 || move.lift < 0 || move.lift > 30)
                        throw new InvalidDataException("Actor moves must be finite, ordered and nonoverlapping: " + actor.id);
                    end = move.at + move.duration;
                }
            }
            if (shots == null || shots.Length == 0 || shots.Length > 20)
                throw new InvalidDataException("Provide 1–20 shots.");
            foreach (var shot in shots)
            {
                if (shot == null || string.IsNullOrWhiteSpace(shot.name) || !Finite(shot.start) || !Finite(shot.duration) ||
                    shot.start < 0 || shot.duration < .25f || shot.start + shot.duration > 60 ||
                    !Finite(shot.fov) || shot.fov < 20 || shot.fov > 110 || !Finite(shot.roll) ||
                    !Finite(shot.from) || !Finite(shot.to) || !Finite(shot.look) ||
                    !string.IsNullOrEmpty(shot.subject) && !Known(shot.subject) ||
                    !string.IsNullOrEmpty(shot.second) && (!Known(shot.second) || !Known(shot.subject)))
                    throw new InvalidDataException("Invalid shot timing, lens or actor binding.");
            }
            if (shots.Sum(s => Mathf.CeilToInt(s.duration * fps)) > 3600)
                throw new InvalidDataException("Limit each take to 3,600 rendered frames across all angles.");
            foreach (var cue in cues ?? Array.Empty<TheatreCue>())
            {
                if (cue == null || !Finite(cue.at) || !Finite(cue.strength) || cue.at < 0 || cue.at >= shots.Max(s => s.start + s.duration) ||
                    !(cue.action == "hazard" && !string.IsNullOrWhiteSpace(cue.hazard) ||
                      cue.action == "tsunami" && Finite(cue.heading) && Finite(cue.height) && cue.height >= 0 && cue.height <= 100 ||
                      cue.action == "stopHazards" || cue.action == "collapse" && cue.strength > 0 && cue.strength <= 30 ||
                      cue.action == "gear" && Enum.TryParse(cue.weapon, true, out EquipmentKind weapon) &&
                      weapon >= EquipmentKind.Blaster && weapon <= EquipmentKind.Dagger ||
                      (cue.action == "punch" || cue.action == "fire") && Known(cue.actor) && Known(cue.target) && cue.actor != cue.target))
                    throw new InvalidDataException("Unknown or incomplete theatre cue.");
            }
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);

        public static TheatrePlan Showcase()
        {
            TheatreMove Move(float at, float duration, float lift, float x, float z, float y = 0) =>
                new TheatreMove { at = at, duration = duration, lift = lift, to = new Vector3(x, y, z) };
            return new TheatrePlan
            {
                cast = new[]
                {
                    new TheatreActor { id = "runner", color = 0, start = new Vector3(-2,0,0), moves = new[] { Move(0,1.65f,1.35f,1.7f,0), Move(2.9f,2.3f,1.7f,5.8f,1.4f,-14) } },
                    new TheatreActor { id = "rival", color = 2, start = new Vector3(2.9f,0,0), moves = new[] { Move(.5f,1.2f,.1f,2.12f,0), Move(1.8f,1.1f,.65f,3.4f,-.9f), Move(3.2f,2.1f,1.3f,7,-1.6f,-17) } },
                    new TheatreActor { id = "escapee", color = 4, start = new Vector3(-2,0,2), moves = new[] { Move(.2f,2.2f,.55f,2.5f,2.2f), Move(3,2.4f,1.1f,7,2.2f,-13) } }
                },
                shots = new[]
                {
                    new TheatreShot { name = "01 Rooftop pursuit", subject = "runner", start = 0, duration = 2.4f, from = new Vector3(-4,2.4f,-5), to = new Vector3(-1,1.1f,-4), look = new Vector3(.25f,.6f,0), fov = 46, roll = -3 },
                    new TheatreShot { name = "02 Impact reverse", subject = "rival", second = "runner", start = .8f, duration = 2.1f, from = new Vector3(3,1.2f,3), to = new Vector3(4,2,1.5f), look = new Vector3(0,.55f,0), fov = 44 },
                    new TheatreShot { name = "03 Tsunami breakaway", subject = "runner", second = "escapee", start = 2.1f, duration = 3.5f, from = new Vector3(4,2,-6), to = new Vector3(8,3,-9), look = new Vector3(-.65f,.4f,0), fov = 58 },
                    new TheatreShot { name = "04 Collapse wide", start = 2.5f, duration = 3.5f, from = new Vector3(19,9,-22), to = new Vector3(23,4,-25), look = new Vector3(0,-7,0), fov = 58 }
                },
                cues = new[]
                {
                    new TheatreCue { at = 1.68f, action = "punch", actor = "runner", target = "rival" },
                    new TheatreCue { at = 1.12f, action = "tsunami", heading = 0 },
                    new TheatreCue { at = 3.05f, action = "collapse", strength = 9 }
                }
            };
        }
    }

    public sealed class GameplayTheatreWindow : EditorWindow
    {
        TheatrePlan plan;
        string[] mapIds = Array.Empty<string>(), mapNames = Array.Empty<string>();
        int mapIndex;
        Vector2 scroll;
        string json = "";
        string issue = "";

        [MenuItem("Tools/Deadly Jenga/Gameplay Theatre")]
        public static void Open() => GetWindow<GameplayTheatreWindow>("Gameplay Theatre");

        void OnEnable()
        {
            var catalog = SpectatorRecordingPlan.Catalog();
            try
            {
                var maps = catalog.maps.Where(SpectatorRecordingPlan.MapAvailable).ToArray();
                mapIds = maps.Select(m => m.id).ToArray();
                mapNames = maps.Select(m => m.title).ToArray();
                mapIndex = Mathf.Max(0, Array.IndexOf(mapIds, "castle"));
            }
            finally { Object.DestroyImmediate(catalog); }
            LoadPreset(0);
        }
        void LoadPreset(int preset)
        {
            plan = TheatrePlan.Showcase();
            if (preset == 1)
            {
                plan.title = "Rooftop duel";
                plan.shots = plan.shots.Take(2).ToArray();
                plan.cues = plan.cues.Where(c => c.action == "punch").ToArray();
            }
            if (preset == 2)
            {
                plan.title = "Breakaway disaster";
                plan.shots = plan.shots.Skip(2).ToArray();
                plan.cues = plan.cues.Where(c => c.action != "punch").ToArray();
            }
            if (mapIds.Length > 0) plan.map = mapIds[Mathf.Clamp(mapIndex, 0, mapIds.Length - 1)];
            json = JsonUtility.ToJson(plan, true); issue = "";
        }
        void OnGUI()
        {
            EditorGUILayout.LabelField("Gameplay Theatre", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(GameplayTheatre.Status, MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Showcase")) LoadPreset(0);
                if (GUILayout.Button("Duel")) LoadPreset(1);
                if (GUILayout.Button("Disaster")) LoadPreset(2);
                if (GUILayout.Button("Open recipe"))
                    Try(() => { string path = EditorUtility.OpenFilePanel("Open theatre recipe", "", "json"); if (path.Length > 0) json = File.ReadAllText(path); });
                if (GUILayout.Button("Save recipe"))
                    Try(() => { string path = EditorUtility.SaveFilePanel("Save theatre recipe", "", "theatre", "json"); if (path.Length > 0) File.WriteAllText(path, json); });
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(GameplayTheatre.Busy))
                {
                    if (GUILayout.Button("Draft render")) Try(() => Render(true));
                    if (GUILayout.Button("Full render")) Try(() => Render(false));
                }
                if (GameplayTheatre.Busy && GUILayout.Button("Cancel")) GameplayTheatre.Cancel();
                if (GUILayout.Button("Open last gallery")) Try(GameplayTheatre.OpenGallery);
            }
            if (mapNames.Length > 0)
                using (new EditorGUILayout.HorizontalScope())
                {
                    mapIndex = EditorGUILayout.Popup("Map", mapIndex, mapNames);
                    if (GUILayout.Button("Apply to recipe", GUILayout.Width(110)))
                        Try(() => { var edit = JsonUtility.FromJson<TheatrePlan>(json); edit.map = mapIds[mapIndex]; json = JsonUtility.ToJson(edit, true); });
                }
            if (issue.Length > 0) EditorGUILayout.HelpBox(issue, MessageType.Error);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            json = EditorGUILayout.TextArea(json, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
            if (GameplayTheatre.Busy) Repaint();
        }
        void Render(bool draft)
        {
            plan = JsonUtility.FromJson<TheatrePlan>(json);
            if (plan == null) throw new InvalidDataException("Recipe is empty.");
            if (draft) { plan.width = 640; plan.height = 360; plan.fps = 15; }
            GameplayTheatre.Start(plan, null, false);
        }
        void Try(Action action) { try { issue = ""; action(); } catch (Exception error) { issue = error.Message; } }
    }

    [InitializeOnLoad]
    public static class GameplayTheatre
    {
        const string Key = "DeadlyJenga.GameplayTheatre.Job";
        [Serializable] sealed class Job
        {
            public TheatrePlan plan;
            public string folder, failure;
            public bool batch, played, completed;
            public RecordingScene[] scenes;
        }
        sealed class Actor
        {
            public TheatrePlan.TheatreActor track;
            public CastleSurvivor motor;
            public CandyPlayerAvatar avatar;
            public Vector3 position;
            public Quaternion facing = Quaternion.identity;
            public float punchAt = -100;
        }
        static Job job;
        static IEnumerator<int> take;
        static int lastFrame = -1, errors;
        static float oldScale;
        static int oldCaptureRate, oldRate, oldSync;
        static bool oldBackground, savedTime;
        static UnityEngine.Random.State oldRandom;
        static Process encoder;
        static readonly List<string> EncoderErrors = new List<string>();
        public static bool Busy => job != null;
        public static string Status { get; private set; } = "Ready to stage a local take.";

        static GameplayTheatre()
        {
            string saved = SessionState.GetString(Key, "");
            if (saved.Length > 0) job = JsonUtility.FromJson<Job>(saved);
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += State;
            EditorApplication.quitting += StopEncoder;
        }

        public static void RunBatch()
        {
            try
            {
                string recipe = Arg("-theatrePlan");
                var plan = recipe.Length == 0 ? TheatrePlan.Showcase() : JsonUtility.FromJson<TheatrePlan>(File.ReadAllText(recipe));
                if (Arg("-theatreFfmpeg") is string ffmpeg && ffmpeg.Length > 0) plan.ffmpeg = ffmpeg;
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "-theatreDraft") >= 0)
                { plan.width = 640; plan.height = 360; plan.fps = 15; }
                VerifyRecipeValidation();
                double deadline = EditorApplication.timeSinceStartup + 120;
                void BeginWhenReady()
                {
                    if ((EditorApplication.isCompiling || EditorApplication.isUpdating) && EditorApplication.timeSinceStartup < deadline) return;
                    EditorApplication.update -= BeginWhenReady;
                    try { Start(plan, Arg("-theatreOutput"), true); }
                    catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
                }
                EditorApplication.update += BeginWhenReady;
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        static void VerifyRecipeValidation()
        {
            var plan = TheatrePlan.Showcase();
            plan.Validate();
            void Reject(Action<TheatrePlan> mutate, string label)
            {
                var candidate = TheatrePlan.Showcase();
                mutate(candidate);
                try { candidate.Validate(); }
                catch (InvalidDataException) { return; }
                throw new InvalidOperationException("Validation accepted " + label);
            }
            Reject(p => p.width = 641, "odd dimensions");
            Reject(p => p.cast[1].id = p.cast[0].id, "duplicate actor ID");
            Reject(p => p.shots[0].subject = "missing", "missing camera target");
            Reject(p => p.cues[0].at = float.NaN, "non-finite cue");
            Reject(p => p.cast[0].moves[1].at = .1f, "overlapping actor moves");
            Debug.Log("Gameplay Theatre recipe validation: showcase and five invalid-input checks passed.");
        }

        static string Arg(string key)
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : "";
        }

        public static void Start(TheatrePlan plan, string output, bool batch)
        {
            if (Busy || SpectatorVideoCapture.Busy || EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Stop other recordings and Play Mode before staging a take.");
            if (plan == null) throw new InvalidDataException("Recipe is empty.");
            plan.Validate();
            if (Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount)
                .Any(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save or close modified scenes first.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            if (batch) setup = setup.Where(scene => !string.IsNullOrEmpty(scene.path)).ToArray();
            if (setup.Any(scene => string.IsNullOrEmpty(scene.path)))
                throw new InvalidOperationException("Save or close unnamed scenes first.");
            string scene;
            var catalog = SpectatorRecordingPlan.Catalog();
            try
            {
                var map = catalog.maps.FirstOrDefault(m => m.id == plan.map);
                if (map == null) throw new InvalidDataException("Map ID is missing from the lobby catalog: " + plan.map);
                scene = string.IsNullOrEmpty(map.scenePath) || map.mapPrefab ? ProjectPath.Find("Game.unity") : map.scenePath;
            }
            finally { Object.DestroyImmediate(catalog); }
            string root = string.IsNullOrWhiteSpace(output) ? Path.Combine(SpectatorRecordingPlan.ProjectRoot, "Logs", "GameplayTheatre") : Path.GetFullPath(output);
            string folder = SpectatorRecordingPlan.TimestampDirectory(root);
            job = new Job { plan = plan, folder = folder, batch = batch,
                scenes = setup.Select(s => new RecordingScene { path = s.path, loaded = s.isLoaded, active = s.isActive }).ToArray() };
            File.WriteAllText(Path.Combine(folder, "recipe.json"), JsonUtility.ToJson(plan, true));
            SessionState.SetString(Key + ".Gallery", folder);
            Save();
            try { EditorSceneManager.OpenScene(scene); Status = "Opening Game in Play Mode."; EditorApplication.EnterPlaymode(); }
            catch { job = null; Save(); throw; }
        }

        public static void OpenGallery()
        {
            string path = Path.Combine(SessionState.GetString(Key + ".Gallery", ""), "index.html");
            if (!File.Exists(path)) throw new FileNotFoundException("No completed gallery in this editor session.");
            Application.OpenURL(new Uri(Path.GetFullPath(path)).AbsoluteUri);
        }
        public static void Cancel() { if (job != null) End("Cancelled. Rendered frames are preserved."); }
        static void Save() => SessionState.SetString(Key, job == null ? "" : JsonUtility.ToJson(job));

        static void State(PlayModeStateChange state)
        {
            if (job == null) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                job.played = true; errors = 0; lastFrame = -1; Save();
                Application.logMessageReceived += Log;
                take = Capture();
            }
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                Application.logMessageReceived -= Log;
                take?.Dispose(); take = null;
                RestoreTime(); StopEncoder();
                if (!job.completed && string.IsNullOrEmpty(job.failure)) job.failure = "Play Mode stopped early.";
                Save();
            }
            if (state == PlayModeStateChange.EnteredEditMode && job.played)
            {
                var finished = job; job = null; Save();
                bool passed = finished.completed && string.IsNullOrEmpty(finished.failure);
                try
                {
                    if (finished.scenes.Length > 0)
                        EditorSceneManager.RestoreSceneManagerSetup(finished.scenes.Select(s =>
                            new SceneSetup { path = s.path, isLoaded = s.loaded, isActive = s.active }).ToArray());
                }
                catch (Exception error) { passed = false; finished.failure = error.ToString(); }
                Status = passed ? "Gallery ready: " + finished.folder : finished.failure;
                File.WriteAllText(Path.Combine(finished.folder, "result.txt"), (passed ? "PASS " : "FAIL ") + Status);
                if (finished.batch) EditorApplication.Exit(passed ? 0 : 1);
            }
        }
        static void Log(string message, string stack, LogType type)
        {
            if (job == null || type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            errors++;
            File.AppendAllText(Path.Combine(job.folder, "runtime-errors.txt"), message + "\n" + stack + "\n");
        }

        static void Tick()
        {
            if (take == null || !EditorApplication.isPlaying || Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            try { if (!take.MoveNext()) End(errors == 0 ? "" : "Unity logged runtime errors. See runtime-errors.txt."); }
            catch (Exception error) { End(error.ToString()); }
        }
        static void End(string failure)
        {
            if (job == null) return;
            job.failure = failure; job.completed = failure.Length == 0; Save();
            EditorApplication.ExitPlaymode();
        }

        static IEnumerator<int> Capture()
        {
            var plan = job.plan;
            var watch = Stopwatch.StartNew();
            CastlePlaySession session;
            while (!(session = Object.FindFirstObjectByType<CastlePlaySession>()) || session.SurvivorBots == null)
            {
                if (watch.Elapsed.TotalSeconds > 30) throw new TimeoutException("Game session never initialized.");
                yield return 0;
            }
            if (session.IsVR || !session.HasWorldAuthority || session.Network && session.Network.HumanCount > 1)
                throw new InvalidOperationException("Theatre requires a local desktop match.");
            SaveTime(plan);
            MatchSetup.MapId = plan.map;
            var selected = MatchSetup.SelectedMap;
            if (selected == null) throw new InvalidOperationException("Map selection failed at runtime.");
            var previousWorld = Services.World;
            MatchSetup.ApplyMap();
            session.SurvivorBots.AutoFill = false; session.SurvivorBots.Clear(); session.HandBots.Clear();
            session.SetMenu(false);
            foreach (var hud in session.GetComponentsInChildren<CandyMatchHud>(true)) Object.Destroy(hud);
            var rules = ModeSimulation.Defaults(GameModeKind.Demo);
            rules.punchEnabled = rules.healthEnabled = true;
            rules.deathEnabled = false;
            session.Modes.Configure(rules);
            session.Modes.StartRound();
            if (session.Social != null) session.Social.VoiceEnabled = false;
            for (int n = 0; n < Mathf.CeilToInt(plan.warmup * plan.fps) + 3; n++) yield return 0;
            var world = Services.World;
            if (world == null || world.Blocks.Count == 0 || selected.mapPrefab && ReferenceEquals(world, previousWorld) ||
                !selected.mapPrefab && UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != selected.scenePath)
                throw new InvalidOperationException("The requested map did not provide the active block world.");
            Vector3 stage = Stage(world) + plan.stageOffset;
            var actors = new Dictionary<string, Actor>(StringComparer.Ordinal);
            foreach (var track in plan.cast)
            {
                var before = new HashSet<GameObject>(session.SurvivorBots.Members);
                if (!session.SurvivorBots.SpawnOne()) throw new InvalidOperationException("Could not cast " + track.id);
                var member = session.SurvivorBots.Members.Single(o => !before.Contains(o));
                var motor = member.GetComponent<CastleSurvivor>();
                var avatar = member.GetComponent<CandyPlayerAvatar>();
                if (!motor || !avatar) throw new InvalidOperationException("Cast member lacks its normal survivor rig.");
                if (track.live) motor.Teleport(stage + track.start, Quaternion.identity);
                else motor.enabled = false;
                avatar.SetColor(track.color);
                if (track.customLook) avatar.ApplyAppearance(track.appearance);
                actors.Add(track.id, new Actor { track = track, motor = motor, avatar = avatar });
            }
            var cameraObject = new GameObject("Gameplay Theatre camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false; camera.nearClipPlane = .08f; camera.farClipPlane = 1600;
            camera.clearFlags = CameraClearFlags.Skybox; camera.allowHDR = true;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.allowXRRendering = false;
            cameraData.renderPostProcessing = true;
            var target = RenderTexture.GetTemporary(plan.width, plan.height, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(plan.width, plan.height, TextureFormat.RGB24, false);
            var cues = (plan.cues ?? Array.Empty<TheatrePlan.TheatreCue>()).OrderBy(c => c.at).ToArray();
            int cueIndex = 0;
            int[] counts = new int[plan.shots.Length];
            var frames = new string[plan.shots.Length];
            for (int i = 0; i < frames.Length; i++)
                Directory.CreateDirectory(frames[i] = Path.Combine(job.folder, $"shot-{i + 1:D2}"));
            File.WriteAllText(Path.Combine(job.folder, "cues.txt"), "Simulation seconds and executed cues\n");
            File.WriteAllText(Path.Combine(job.folder, "take.txt"), $"Map={plan.map}\nStage={stage}\nBlocks={world.Blocks.Count}\nCast={actors.Count}\nFPS={plan.fps}\n");
            try
            {
                int end = plan.shots.Max(s => Mathf.RoundToInt(s.start * plan.fps) + Mathf.CeilToInt(s.duration * plan.fps));
                for (int frame = 0; frame < end; frame++)
                {
                    float time = frame / (float)plan.fps;
                    foreach (var actor in actors.Values) Perform(actor, stage, time);
                    Physics.SyncTransforms();
                    while (cueIndex < cues.Length && cues[cueIndex].at <= time + .00001f)
                        Cue(cues[cueIndex++], session, actors, world, stage, time);
                    for (int i = 0; i < plan.shots.Length; i++)
                    {
                        var shot = plan.shots[i];
                        int first = Mathf.RoundToInt(shot.start * plan.fps), length = Mathf.CeilToInt(shot.duration * plan.fps);
                        if (frame < first || frame >= first + length) continue;
                        PlaceCamera(camera, shot, actors, stage, (frame - first) / (float)Mathf.Max(1, length - 1));
                        Render(camera, target, image, Path.Combine(frames[i], $"{counts[i]++:D05}.png"));
                    }
                    Status = $"Staging frame {frame + 1}/{end}, {counts.Sum()} camera frames.";
                    yield return 0;
                }
                if (cueIndex != cues.Length) throw new InvalidDataException("A cue falls after the last sampled frame.");
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.ReleaseTemporary(target);
                Object.Destroy(image); Object.Destroy(cameraObject);
                foreach (var actor in actors.Values) if (actor.avatar) actor.avatar.ClearPoseOverride();
                if (session) DemoDisasters.StopAll(session);
            }
            for (int i = 0; i < counts.Length; i++)
            {
                int expected = Mathf.CeilToInt(plan.shots[i].duration * plan.fps);
                if (counts[i] != expected) throw new InvalidDataException($"Shot {i + 1} captured {counts[i]} frames, expected {expected}.");
                Status = $"Encoding {i + 1}/{counts.Length}.";
                StartEncoder(plan, frames[i]);
                var timer = Stopwatch.StartNew();
                while (!encoder.HasExited)
                {
                    if (timer.Elapsed.TotalMinutes > 3) throw new TimeoutException("FFmpeg did not finish.");
                    yield return 0;
                }
                encoder.WaitForExit();
                if (encoder.ExitCode != 0 || new FileInfo(Path.Combine(frames[i], "clip.mp4")).Length <= 0)
                    throw new IOException("FFmpeg failed: " + string.Join("\n", EncoderErrors));
                StopEncoder();
            }
            Gallery(plan, counts);
        }

        static Vector3 Stage(IWorldQuery world)
        {
            var block = world.Blocks.Where(b => b && b.gameObject.activeInHierarchy)
                .OrderByDescending(b => b.position.y).Take(32)
                .OrderBy(b => Vector3.ProjectOnPlane(b.position - world.Center, Vector3.up).sqrMagnitude).FirstOrDefault();
            if (!block) return world.SafeSpawnPoint();
            float top = block.GetComponentsInChildren<Collider>().Where(c => c.enabled && !c.isTrigger)
                .Select(c => c.bounds.max.y).DefaultIfEmpty(block.position.y + .5f).Max();
            return new Vector3(block.position.x, top + .025f, block.position.z);
        }
        static Vector3 Position(TheatrePlan.TheatreActor actor, float time)
        {
            Vector3 position = actor.start;
            foreach (var move in actor.moves ?? Array.Empty<TheatrePlan.TheatreMove>())
            {
                if (time < move.at) break;
                if (time < move.at + move.duration)
                {
                    float u = (time - move.at) / move.duration;
                    var point = Vector3.LerpUnclamped(position, move.to, u);
                    if (move.to.y < position.y) point.y = Mathf.LerpUnclamped(position.y, move.to.y, u * u);
                    return point + Vector3.up * (4 * u * (1 - u) * move.lift);
                }
                position = move.to;
            }
            return position;
        }
        static void Perform(Actor actor, Vector3 stage, float time)
        {
            if (actor.track.live)
            {
                actor.position = actor.motor.transform.position;
                return;
            }
            actor.position = stage + Position(actor.track, time);
            var direction = Vector3.ProjectOnPlane(Position(actor.track, time + .05f) - Position(actor.track, time - .05f), Vector3.up);
            if (direction.sqrMagnitude > .00001f) actor.facing = Quaternion.LookRotation(direction);
            actor.motor.transform.SetPositionAndRotation(actor.position, actor.facing);
            var pose = actor.avatar.ReadFrame();
            float scale = Mathf.Max(.1f, pose.scale);
            pose.feet = actor.position; pose.head = actor.position + Vector3.up * (1.58f * scale); pose.headRotation = actor.facing;
            float stride = direction.sqrMagnitude > .00001f ? Mathf.Sin(time * 13) * .28f : 0;
            float punch = Mathf.Clamp01(1 - Mathf.Abs((time - actor.punchAt) - .08f) / .17f);
            pose.left.position = pose.head + actor.facing * new Vector3(-.27f, -.37f, .2f + stride) * scale;
            pose.right.position = pose.head + actor.facing * new Vector3(.27f, -.37f + .24f * punch, .2f - stride + .85f * punch) * scale;
            pose.left.rotation = pose.right.rotation = actor.facing;
            actor.avatar.OverridePose(pose);
            actor.avatar.ApplyCosmeticFrame(pose);
        }
        static void Cue(TheatrePlan.TheatreCue cue, CastlePlaySession session, Dictionary<string, Actor> actors,
            IWorldQuery world, Vector3 stage, float time)
        {
            string detail = cue.action;
            switch (cue.action)
            {
                case "hazard":
                    if (!DemoDisasters.Execute(session, cue.hazard)) throw new InvalidOperationException("Hazard failed to start: " + cue.hazard);
                    detail += " " + cue.hazard; break;
                case "tsunami":
                    var prefab = DemoDisasters.Prefabs.FirstOrDefault(p => p && p.GetComponent<TsunamiHazard>());
                    if (!prefab) throw new InvalidOperationException("The production tsunami prefab is missing.");
                    var waveObject = Object.Instantiate(prefab, session.transform);
                    var wave = waveObject.GetComponent<TsunamiHazard>();
                    wave.randomDirection = false; wave.approachHeading = cue.heading;
                    wave.offshoreDistance = 0; wave.warningTime = 0; wave.riseTime = .8f;
                    wave.approachSpeed = 30;
                    wave.height = cue.height > 0 ? cue.height : Mathf.Clamp(stage.y - world.WaterHeight(stage) + 8, 18, 100);
                    wave.affectPlayers = false;
                    wave.Begin();
                    if (!wave.Active) throw new InvalidOperationException("Staged tsunami failed to start.");
                    detail += $" heading={wave.approachHeading} height={wave.height}";
                    break;
                case "stopHazards": DemoDisasters.StopAll(session); break;
                case "gear":
                    var kind = (EquipmentKind)Enum.Parse(typeof(EquipmentKind), cue.weapon, true);
                    if (!session.Modes.GiveEveryoneEquipment(kind, false))
                        throw new InvalidOperationException("Could not equip the cast with " + cue.weapon);
                    detail += " " + kind;
                    break;
                case "fire":
                    var shooter = actors[cue.actor];
                    var subject = actors[cue.target];
                    var identity = shooter.motor.GetComponent<IModeActor>();
                    if (identity == null || string.IsNullOrEmpty(identity.ActorId))
                        throw new InvalidOperationException("Shooter is not registered as a mode actor: " + cue.actor);
                    Vector3 aim = subject.position + Vector3.up * .55f - identity.ViewPose.position;
                    if (aim.sqrMagnitude < .01f) throw new InvalidOperationException("Shooter and target overlap.");
                    aim.Normalize();
                    shooter.facing = Quaternion.LookRotation(Vector3.ProjectOnPlane(aim, Vector3.up));
                    int previous = session.Modes.Frame.equipmentSequence;
                    var shot = new MatchModeRuntime.EquipmentCommand
                    {
                        action = "attack", position = identity.ViewPose.position + aim * .18f, forward = aim
                    };
                    session.Modes.ReceiveCommand(identity.ActorId, "equipment:" + JsonUtility.ToJson(shot));
                    bool fired = session.Modes.Frame.equipmentActions.Any(e =>
                        e.id > previous && e.actor == identity.ActorId && e.action == EquipmentAction.Shot);
                    if (!fired) throw new InvalidOperationException("Gameplay rejected fire cue for " + cue.actor +
                        ". Check draw state, cooldown, and cover at the muzzle.");
                    detail += $" {cue.actor}->{cue.target} weapon={session.Modes.EquipmentFor(identity.ActorId).kind}";
                    break;
                case "punch":
                    var actor = actors[cue.actor]; var target = actors[cue.target];
                    actor.punchAt = time;
                    var direction = target.position - actor.position;
                    if (direction.sqrMagnitude > .0001f) actor.facing = Quaternion.LookRotation(Vector3.ProjectOnPlane(direction, Vector3.up).normalized);
                    var damageable = target.motor.GetComponent<IDamageable>();
                    float healthBefore = damageable?.Health ?? 0;
                    bool accepted = session.Modes.RequestPunch(actor.motor.gameObject, actor.position + Vector3.up * .55f, direction, 8);
                    detail += $" {cue.actor}->{cue.target} gameplayAccepted={accepted} healthDelta={healthBefore - (damageable?.Health ?? 0):F1}"; break;
                case "collapse":
                    int count = 0;
                    foreach (var body in world.Blocks.Where(b => b && b.gameObject.activeInHierarchy && !b.isKinematic)
                        .OrderBy(b => (b.position - stage).sqrMagnitude).Take(10))
                    {
                        var outward = Vector3.ProjectOnPlane(body.position - stage, Vector3.up);
                        body.AddForce((outward.normalized + Vector3.right * .75f + Vector3.up * .12f) * cue.strength, ForceMode.VelocityChange);
                        count++;
                    }
                    if (count == 0) throw new InvalidOperationException("Collapse cue found no active dynamic blocks.");
                    detail += $" blocks={count}"; break;
            }
            File.AppendAllText(Path.Combine(job.folder, "cues.txt"), $"{time:F3} {detail}\n");
        }
        static void PlaceCamera(Camera camera, TheatrePlan.TheatreShot shot, Dictionary<string, Actor> actors, Vector3 stage, float u)
        {
            var anchor = string.IsNullOrEmpty(shot.subject) ? stage : actors[shot.subject].position;
            if (!string.IsNullOrEmpty(shot.second)) anchor = (anchor + actors[shot.second].position) * .5f;
            var position = anchor + Vector3.Lerp(shot.from, shot.to, Mathf.SmoothStep(0, 1, u));
            var look = anchor + shot.look;
            var direction = look - position;
            if (direction.sqrMagnitude < .001f) throw new InvalidDataException("Camera and look target overlap: " + shot.name);
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction) * Quaternion.Euler(0, 0, shot.roll));
            camera.fieldOfView = shot.fov;
        }
        static void Render(Camera camera, RenderTexture target, Texture2D image, string path)
        {
            var old = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply(false);
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally { camera.targetTexture = null; RenderTexture.active = old; }
        }

        static void StartEncoder(TheatrePlan plan, string folder)
        {
            EncoderErrors.Clear();
            var start = new ProcessStartInfo(plan.ffmpeg) { WorkingDirectory = folder, UseShellExecute = false,
                CreateNoWindow = true, RedirectStandardError = true };
            start.Arguments = "-hide_banner -loglevel error -nostdin -n -framerate " +
                plan.fps.ToString(CultureInfo.InvariantCulture) +
                " -i %05d.png -c:v libx264 -preset fast -crf 17 -pix_fmt yuv420p -movflags +faststart clip.mp4";
            encoder = new Process { StartInfo = start };
            encoder.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (EncoderErrors) EncoderErrors.Add(e.Data); };
            if (!encoder.Start()) throw new IOException("FFmpeg did not start.");
            encoder.BeginErrorReadLine();
        }
        static void StopEncoder()
        {
            if (encoder == null) return;
            try { if (!encoder.HasExited) encoder.Kill(); } catch (InvalidOperationException) { }
            encoder.Dispose(); encoder = null;
        }
        static void Gallery(TheatrePlan plan, int[] counts)
        {
            string Html(string value) => System.Net.WebUtility.HtmlEncode(value);
            var html = new StringBuilder("<!doctype html><html lang='en'><meta charset='utf-8'><meta name='viewport' content='width=device-width'><title>Gameplay Theatre</title><style>body{background:#13111d;color:#f4efff;font:16px system-ui;margin:5vw}a{color:#adf6df}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(min(320px,100%),1fr));gap:22px}article{background:#211c32;padding:16px;border-radius:14px}video{width:100%;border-radius:9px}h1,h2{margin:.4em 0}</style>");
            html.Append($"<h1>{Html(plan.title)}</h1><p>{Html(plan.map)} · {plan.width} × {plan.height} · {plan.fps} fps · Silent Unity camera footage</p><p><a href='recipe.json'>Recipe</a> · <a href='cues.txt'>Cue log</a> · <a href='take.txt'>Take info</a></p><main>");
            for (int i = 0; i < counts.Length; i++)
            {
                string folder = $"shot-{i + 1:D2}", poster = $"{counts[i] / 2:D05}.png";
                html.Append($"<article><video controls preload='metadata' poster='{folder}/{poster}' src='{folder}/clip.mp4'></video><h2>{Html(plan.shots[i].name)}</h2><p>{counts[i] / (float)plan.fps:F2} s · <a href='{folder}/clip.mp4'>MP4</a> · <a href='{folder}/{poster}'>Still</a></p></article>");
            }
            html.Append("</main></html>");
            File.WriteAllText(Path.Combine(job.folder, "index.html"), html.ToString());
        }
        static void SaveTime(TheatrePlan plan)
        {
            oldCaptureRate = Time.captureFramerate; oldRate = Application.targetFrameRate; oldSync = QualitySettings.vSyncCount;
            oldScale = Time.timeScale; oldBackground = Application.runInBackground; oldRandom = UnityEngine.Random.state;
            savedTime = true; Time.timeScale = 1; Time.captureFramerate = plan.fps;
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1; Application.runInBackground = true;
            UnityEngine.Random.InitState(plan.seed);
        }
        static void RestoreTime()
        {
            if (!savedTime) return;
            Time.captureFramerate = oldCaptureRate; Application.targetFrameRate = oldRate; QualitySettings.vSyncCount = oldSync;
            Time.timeScale = oldScale; Application.runInBackground = oldBackground; UnityEngine.Random.state = oldRandom; savedTime = false;
        }
    }
}
