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
        public bool useMapAnchor;
        public Vector3 mapAnchor;
        public string ffmpeg = "ffmpeg", replaySource = "";
        public bool observedMatch;
        public bool fightSet, rooftopBattleSet;
        public TheatreActor[] cast;
        public TheatreShot[] shots;
        public TheatreCue[] cues;
        public TheatreEdit[] edit;

        [Serializable] public sealed class TheatreEdit
        {
            public int shot;
            public float from, duration = 1;
        }

        [Serializable] public sealed class TheatreActor
        {
            public string id;
            public int color;
            public bool live, customLook;
            public AvatarAppearance appearance;
            public Vector3 start;
            public TheatreMove[] moves;
            public TheatrePose[] poses;
            public FightKey[] performance;
            public TheatreStuntPose[] stunt;
        }

        [Serializable] public sealed class FightKey
        {
            public float at, yaw, pitch, roll, height = 1.58f, leftCurl = 1, rightCurl = 1;
            public Vector3 feet, left, right;
        }

        [Serializable] public sealed class TheatrePose
        {
            public float at, duration = 1, crouch, lean;
            public Vector3 leftReach, rightReach;
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
            public bool avoidWalls, autoFocus, firstPerson;
            public Vector3 from, to, look;
        }

        [Serializable] public sealed class TheatreCue
        {
            public float at;
            public string action, actor, target, hazard, weapon;
            public float strength, heading, height;
            public bool optional, lethal;
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(map) || string.IsNullOrWhiteSpace(ffmpeg))
                throw new InvalidDataException("Title, map ID and FFmpeg executable are required.");
            if (fps < 12 || fps > 60 || width < 320 || width > 3840 || height < 180 || height > 2160 ||
                width % 2 != 0 || height % 2 != 0 || !Finite(warmup) || !Finite(stageOffset) || !Finite(mapAnchor) || warmup < 0 || warmup > 10)
                throw new InvalidDataException("Use even dimensions, 12–60 fps and a warmup of 0–10 seconds.");
            int maxCast = observedMatch && (Application.isPlaying || !string.IsNullOrWhiteSpace(replaySource)) ? 64 : 12;
            if (cast == null || cast.Length < 1 || cast.Length > maxCast || cast.Any(a => a == null || string.IsNullOrWhiteSpace(a.id) || !Finite(a.start)) ||
                cast.Select(a => a.id).Distinct(StringComparer.Ordinal).Count() != cast.Length)
                throw new InvalidDataException($"Provide 1–{maxCast} uniquely named actors with finite start positions.");
            var ids = new HashSet<string>(cast.Select(a => a.id), StringComparer.Ordinal);
            bool Known(string id) => !string.IsNullOrEmpty(id) && ids.Contains(id);
            foreach (var actor in cast)
            {
                if (actor.stunt != null && actor.stunt.Length > 0 &&
                    (actor.live || !TheatreStuntPose.ValidTrack(actor.stunt)))
                    throw new InvalidDataException("Invalid full-body stunt track: " + actor.id);
                if (actor.performance != null && (actor.live || actor.performance.Length > 160 ||
                    actor.performance.Any(key => key == null || !Finite(key.at) || !Finite(key.feet) ||
                    !Finite(key.left) || !Finite(key.right) || !Finite(key.height) || key.height < .65f || key.height > 2 ||
                    !Finite(key.yaw) || !Finite(key.pitch) || !Finite(key.roll)) ||
                    actor.performance.Zip(actor.performance.Skip(1), (a, b) => a.at >= b.at).Any(invalid => invalid)))
                    throw new InvalidDataException("Invalid fight performance track: " + actor.id);
                if (actor.live && actor.moves != null && actor.moves.Length > 0)
                    throw new InvalidDataException("Live bots use their gameplay brain and cannot also have an authored motion track.");
                if (actor.poses != null && (actor.poses.Length > 60 || actor.poses.Any(pose => pose == null ||
                    !Finite(pose.at) || !Finite(pose.duration) || pose.at < 0 || pose.duration <= 0 ||
                    !Finite(pose.crouch) || pose.crouch < 0 || pose.crouch > .6f || !Finite(pose.lean) ||
                    Mathf.Abs(pose.lean) > 50 || !Finite(pose.leftReach) || !Finite(pose.rightReach) ||
                    pose.leftReach.sqrMagnitude > 2.25f || pose.rightReach.sqrMagnitude > 2.25f)))
                    throw new InvalidDataException("Invalid pose track on " + actor.id);
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
                    shot.firstPerson && !Known(shot.subject) ||
                    !Finite(shot.from) || !Finite(shot.to) || !Finite(shot.look) ||
                    !string.IsNullOrEmpty(shot.subject) && !Known(shot.subject) ||
                    !string.IsNullOrEmpty(shot.second) && (!Known(shot.second) || !Known(shot.subject)))
                    throw new InvalidDataException("Invalid shot timing, lens or actor binding.");
            }
            if (shots.Sum(s => Mathf.CeilToInt(s.duration * fps)) > 3600)
                throw new InvalidDataException("Limit each take to 3,600 rendered frames across all angles.");
            if (edit != null && (edit.Length > 32 || edit.Any(cut => cut == null || cut.shot < 0 || cut.shot >= shots.Length ||
                !Finite(cut.from) || !Finite(cut.duration) || cut.from < 0 || cut.duration <= 0 ||
                cut.from + cut.duration > shots[cut.shot].duration + .0001f)))
                throw new InvalidDataException("Edit entries must stay within their source shots.");
            foreach (var cue in cues ?? Array.Empty<TheatreCue>())
            {
                if (cue == null || !Finite(cue.at) || !Finite(cue.strength) || cue.at < 0 || cue.at >= shots.Max(s => s.start + s.duration) ||
                    !(cue.action == "hazard" && !string.IsNullOrWhiteSpace(cue.hazard) ||
                      cue.action == "tsunami" && Finite(cue.heading) && Finite(cue.height) && cue.height >= 0 && cue.height <= 100 ||
                      cue.action == "dropDeck" || cue.action == "stopHazards" || cue.action == "collapse" && cue.strength > 0 && cue.strength <= 30 ||
                      cue.action == "gear" && Enum.TryParse(cue.weapon, true, out EquipmentKind weapon) &&
                      weapon >= EquipmentKind.Blaster && weapon <= EquipmentKind.Dagger &&
                      (string.IsNullOrEmpty(cue.actor) || Known(cue.actor)) ||
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
        [SerializeField] string json = "";
        [SerializeField] bool showRecipe;
        int selectedShot, selectedCue, previewFrame = -1;
        string previewPath;
        Texture2D preview;
        Camera liveCamera;
        RenderTexture liveImage;
        bool livePreview;
        float liveTime;
        double nextPreview;
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
            if (string.IsNullOrWhiteSpace(json)) LoadPreset(0);
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
                    if (GUILayout.Button("Preview saved take in 3D"))
                        Try(() => GameplayTheatre.Start(JsonUtility.FromJson<TheatrePlan>(json), "", false, true));
                }
                if (GameplayTheatre.Busy && GUILayout.Button("Cancel")) GameplayTheatre.Cancel();
                using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || GameplayTheatre.Busy))
                    if (GUILayout.Button("Record current match"))
                        Try(() => GameplayTheatre.RecordCurrentMatch(JsonUtility.FromJson<TheatrePlan>(json)));
                if (GUILayout.Button("Open last gallery")) Try(GameplayTheatre.OpenGallery);
                if (GUILayout.Button("Load take for reframe"))
                    Try(() =>
                    {
                        string folder = EditorUtility.OpenFolderPanel("Completed Theatre take", "", "");
                        if (folder.Length == 0) return;
                        var edit = JsonUtility.FromJson<TheatrePlan>(File.ReadAllText(Path.Combine(folder, "recipe.json")));
                        edit.replaySource = File.Exists(Path.Combine(folder, "motion.djt")) ? folder : edit.replaySource;
                        if (string.IsNullOrEmpty(edit.replaySource)) throw new InvalidDataException("This take has no motion tape.");
                        json = JsonUtility.ToJson(edit, true);
                    });
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
            if (GameplayTheatre.TimelineImage)
            {
                EditorGUILayout.LabelField("Saved take • live 3D", EditorStyles.boldLabel);
                GameplayTheatre.TimelineFrame = EditorGUILayout.IntSlider("Timeline frame", GameplayTheatre.TimelineFrame, 0,
                    Mathf.Max(0, GameplayTheatre.TimelineLength - 1));
                GUILayout.Label(GameplayTheatre.TimelineImage, GUILayout.ExpandWidth(true), GUILayout.MaxHeight(360));
                if (GUILayout.Button("Close 3D preview")) GameplayTheatre.ClosePreview();
            }
            showRecipe = EditorGUILayout.Foldout(showRecipe, "Advanced recipe JSON", true);
            if (showRecipe) json = EditorGUILayout.TextArea(json, GUILayout.MinHeight(210));
            else Director();
            EditorGUILayout.EndScrollView();
            if (GameplayTheatre.Busy || livePreview && EditorApplication.isPlaying) Repaint();
        }
        void OnDisable()
        {
            if (preview) DestroyImmediate(preview);
            ReleaseLivePreview();
        }

        void ReleaseLivePreview()
        {
            if (liveCamera) DestroyImmediate(liveCamera.gameObject);
            if (liveImage) DestroyImmediate(liveImage);
            liveCamera = null;
            liveImage = null;
        }

        void LiveViewport(TheatrePlan.TheatreShot shot, Vector3 stageOffset)
        {
            if (!EditorApplication.isPlaying)
            {
                ReleaseLivePreview();
                return;
            }
            livePreview = EditorGUILayout.Toggle("Live shot viewport", livePreview);
            if (!livePreview || GameplayTheatre.Busy)
            {
                ReleaseLivePreview();
                return;
            }
            liveTime = EditorGUILayout.Slider("Camera time", liveTime, shot.start, shot.start + shot.duration);
            var previewRect = GUILayoutUtility.GetAspectRect(16f / 9);
            if (Event.current.type != EventType.Repaint) return;
            if (EditorApplication.timeSinceStartup >= nextPreview && Services.World != null)
            {
                nextPreview = EditorApplication.timeSinceStartup + .12;
                if (!liveCamera)
                {
                    var rig = new GameObject("Theatre live preview") { hideFlags = HideFlags.HideAndDontSave };
                    liveCamera = rig.AddComponent<Camera>();
                    liveCamera.enabled = false;
                    liveCamera.farClipPlane = 1600;
                    liveCamera.GetUniversalAdditionalCameraData().allowXRRendering = false;
                    liveImage = new RenderTexture(640, 360, 24) { hideFlags = HideFlags.HideAndDontSave };
                }
                Try(() => GameplayTheatre.PreviewCurrentShot(liveCamera, liveImage, shot, liveTime, stageOffset));
            }
            if (liveImage) GUI.DrawTexture(previewRect, liveImage, ScaleMode.ScaleToFit);
        }
        void Director()
        {
            TheatrePlan edit;
            try { edit = JsonUtility.FromJson<TheatrePlan>(json); }
            catch (ArgumentException) { return; }
            if (edit?.shots == null || edit.shots.Length == 0 || edit.shots.Any(s => s == null)) return;
            selectedShot = Mathf.Clamp(selectedShot, 0, edit.shots.Length - 1);
            selectedShot = EditorGUILayout.Popup("Camera shot", selectedShot, edit.shots.Select(s => s.name ?? "(unnamed)").ToArray());
            GameplayTheatre.TimelineShot = selectedShot;
            var shot = edit.shots[selectedShot];
            EditorGUI.BeginChangeCheck();
            if (EditorApplication.isPlaying && !GameplayTheatre.Busy)
            {
                string[] cast = GameplayTheatre.CurrentCastIds();
                shot.subject = PickTarget("Camera target", shot.subject, cast);
                if (!string.IsNullOrEmpty(shot.subject)) shot.second = PickTarget("Second target", shot.second, cast);
                else shot.second = "";
            }
            shot.start = EditorGUILayout.Slider("Start (seconds)", shot.start, 0, Mathf.Max(0, 60 - shot.duration));
            shot.duration = EditorGUILayout.Slider("Length (seconds)", shot.duration, .25f, Mathf.Max(.25f, 60 - shot.start));
            shot.fov = EditorGUILayout.Slider("Vertical lens", shot.fov, 20, 110);
            shot.roll = EditorGUILayout.Slider("Dutch angle", shot.roll, -45, 45);
            shot.from = EditorGUILayout.Vector3Field("Rail start", shot.from);
            shot.to = EditorGUILayout.Vector3Field("Rail end", shot.to);
            shot.look = EditorGUILayout.Vector3Field("Look offset", shot.look);
            shot.avoidWalls = EditorGUILayout.Toggle("Avoid block occlusion", shot.avoidWalls);
            shot.autoFocus = EditorGUILayout.Toggle("Depth of field on target", shot.autoFocus);
            if (edit.cues != null && edit.cues.Length > 0 && edit.cues.All(c => c != null))
            {
                selectedCue = Mathf.Clamp(selectedCue, 0, edit.cues.Length - 1);
                selectedCue = EditorGUILayout.Popup("Action cue", selectedCue, edit.cues.Select(c => c.action ?? "(unnamed)").ToArray());
                edit.cues[selectedCue].at = EditorGUILayout.Slider("Cue at (seconds)", edit.cues[selectedCue].at, 0,
                    Mathf.Max(0, edit.shots.Max(s => s.start + s.duration) - .1f));
            }
            if (EditorGUI.EndChangeCheck()) json = JsonUtility.ToJson(edit, true);
            LiveViewport(shot, edit.stageOffset);
            if (GUILayout.Button("Build cut from current shots"))
            {
                edit.edit = edit.shots.Select((s, i) => new TheatrePlan.TheatreEdit { shot = i, duration = s.duration }).ToArray();
                json = JsonUtility.ToJson(edit, true);
            }
            if (edit.edit != null && edit.edit.Length > 0)
            {
                EditorGUILayout.LabelField("Export cut", EditorStyles.boldLabel);
                for (int i = 0; i < edit.edit.Length; i++)
                {
                    var cut = edit.edit[i];
                    if (cut == null || cut.shot < 0 || cut.shot >= edit.shots.Length) continue;
                    EditorGUI.BeginChangeCheck();
                    cut.shot = EditorGUILayout.Popup("Source", cut.shot, edit.shots.Select(s => s.name).ToArray());
                    float limit = edit.shots[cut.shot].duration;
                    cut.from = EditorGUILayout.Slider("In", cut.from, 0, Mathf.Max(0, limit - .05f));
                    cut.duration = EditorGUILayout.Slider("Length", cut.duration, .05f, Mathf.Max(.05f, limit - cut.from));
                    if (EditorGUI.EndChangeCheck()) json = JsonUtility.ToJson(edit, true);
                }
            }
            ReviewShot();
        }
        static string PickTarget(string label, string current, string[] cast)
        {
            int index = Array.IndexOf(cast, current ?? "") + 1;
            string[] choices = new[] { "Stage" }.Concat(cast).ToArray();
            int selection = EditorGUILayout.Popup(label, index, choices);
            return selection == index ? current : selection == 0 ? "" : cast[selection - 1];
        }

        void ReviewShot()
        {
            string root = GameplayTheatre.LastGalleryFolder;
            if (string.IsNullOrEmpty(root)) return;
            string folder = Path.Combine(root, $"shot-{selectedShot + 1:D2}");
            if (!Directory.Exists(folder) || !File.Exists(Path.Combine(root, "index.html"))) return;
            int last = Directory.GetFiles(folder, "*.png").Length - 1;
            if (last < 0) return;
            previewFrame = EditorGUILayout.IntSlider("Review rendered frame", Mathf.Max(0, previewFrame), 0, last);
            string file = Path.Combine(folder, $"{previewFrame:D05}.png");
            if (file != previewPath && File.Exists(file))
            {
                if (!preview) preview = new Texture2D(2, 2);
                preview.LoadImage(File.ReadAllBytes(file));
                previewPath = file;
            }
            if (preview) GUI.DrawTexture(GUILayoutUtility.GetRect(180, 180, GUILayout.ExpandWidth(true)), preview, ScaleMode.ScaleToFit);
        }

        void Render(bool draft)
        {
            plan = JsonUtility.FromJson<TheatrePlan>(json);
            if (plan == null) throw new InvalidDataException("Recipe is empty.");
            if (draft) { plan.width = 640; plan.height = 360; if (string.IsNullOrEmpty(plan.replaySource)) plan.fps = 15; }
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
            public bool batch, played, completed, existingMatch, previewOnly;
            public RecordingScene[] scenes;
        }
        sealed class Actor
        {
            public TheatrePlan.TheatreActor track;
            public CastleSurvivor motor;
            public CandyPlayerAvatar avatar;
            public Vector3 position;
            public Quaternion facing = Quaternion.identity;
            public float downAt = float.PositiveInfinity;
            public float punchAt = -100;
        }
        static Job job;
        static IEnumerator<int> take;
        static int lastFrame = -1, errors;
        static float oldScale;
        static int oldCaptureRate, oldRate, oldSync;
        static bool oldBackground, savedTime, restoreRandom, captureLocked;
        static UnityEngine.Random.State oldRandom;
        static Process encoder;
        static readonly List<string> EncoderErrors = new List<string>();
        public static bool Busy => job != null;
        public static string LastGalleryFolder => SessionState.GetString(Key + ".Gallery", "");
        public static string Status { get; private set; } = "Ready to stage a local take.";
        internal static RenderTexture TimelineImage { get; private set; }
        internal static int TimelineFrame, TimelineLength, TimelineShot;

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
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "-theatreInstallMixer") >= 0)
                    TheatreAudioCapture.InstallMixer();
                string recipe = Arg("-theatrePlan");
                var plan = recipe.Length == 0 ? TheatrePlan.Showcase() : JsonUtility.FromJson<TheatrePlan>(File.ReadAllText(recipe));
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "-theatreStuntFixture") >= 0)
                    TheatreStuntImport.AttachFixture(plan);
                if (Arg("-theatreFfmpeg") is string ffmpeg && ffmpeg.Length > 0) plan.ffmpeg = ffmpeg;
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "-theatreDraft") >= 0)
                { plan.width = 640; plan.height = 360; plan.fps = 15; }
                VerifyRecipeValidation();
                double deadline = EditorApplication.timeSinceStartup + 120;
                void BeginWhenReady()
                {
                    if ((EditorApplication.isCompiling || EditorApplication.isUpdating) && EditorApplication.timeSinceStartup < deadline) return;
                    EditorApplication.update -= BeginWhenReady;
                    try { Start(plan, Arg("-theatreOutput"), true,
                        Array.IndexOf(Environment.GetCommandLineArgs(), "-theatrePreviewCheck") >= 0); }
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

        public static void Start(TheatrePlan plan, string output, bool batch, bool previewOnly = false)
        {
            if (Busy || SpectatorVideoCapture.Busy || EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Stop other recordings and Play Mode before staging a take.");
            if (plan == null) throw new InvalidDataException("Recipe is empty.");
            plan.Validate();
            if (previewOnly && string.IsNullOrWhiteSpace(plan.replaySource))
                throw new InvalidOperationException("Load a saved take for reframe before opening its 3D timeline.");
            if (!string.IsNullOrWhiteSpace(plan.replaySource))
            {
                string source = Path.GetFullPath(plan.replaySource);
                string priorRecipe = Path.Combine(source, "recipe.json");
                if (!File.Exists(Path.Combine(source, "motion.djt")) || !File.Exists(Path.Combine(source, "take.wav")) ||
                    !File.Exists(priorRecipe) || !File.Exists(Path.Combine(source, "result.txt")) ||
                    !File.ReadAllText(Path.Combine(source, "result.txt")).StartsWith("PASS ", StringComparison.Ordinal))
                    throw new InvalidDataException("Select a completed Theatre take containing a motion tape and game audio.");
                var prior = JsonUtility.FromJson<TheatrePlan>(File.ReadAllText(priorRecipe));
                if (prior == null || prior.map != plan.map || prior.fps != plan.fps)
                    throw new InvalidDataException("Reframes use the recorded map and frame rate.");
                plan.replaySource = source;
            }
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
                previewOnly = previewOnly,
                existingMatch = batch && Array.IndexOf(Environment.GetCommandLineArgs(), "-theatreCurrentMatch") >= 0,
                scenes = setup.Select(s => new RecordingScene { path = s.path, loaded = s.isLoaded, active = s.isActive }).ToArray() };
            File.WriteAllText(Path.Combine(folder, "recipe.json"), JsonUtility.ToJson(plan, true));
            if (!previewOnly) SessionState.SetString(Key + ".Gallery", folder);
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

        static Dictionary<string, Actor> CurrentCast()
        {
            var actors = new Dictionary<string, Actor>(StringComparer.Ordinal);
            foreach (var avatar in Object.FindObjectsByType<CandyPlayerAvatar>(FindObjectsSortMode.None))
            {
                var identity = avatar.GetComponent<IModeActor>();
                if (identity == null || string.IsNullOrEmpty(identity.ActorId)) continue;
                actors.TryAdd(identity.ActorId, new Actor { avatar = avatar, position = avatar.transform.position,
                    track = new TheatrePlan.TheatreActor { id = identity.ActorId } });
            }
            return actors;
        }
        internal static string[] CurrentCastIds() => CurrentCast().Keys.OrderBy(id => id, StringComparer.Ordinal).ToArray();

        internal static void PreviewCurrentShot(Camera camera, RenderTexture image, TheatrePlan.TheatreShot shot,
            float time, Vector3 stageOffset, Vector3? mapAnchor = null)
        {
            var world = Services.World;
            if (world == null) return;
            var actors = CurrentCast();
            PlaceCamera(camera, shot, actors, (mapAnchor.HasValue ? world.Center + mapAnchor.Value : Stage(world)) + stageOffset,
                Mathf.Clamp01((time - shot.start) / Mathf.Max(.01f, shot.duration)));
            camera.targetTexture = image;
            try { camera.Render(); }
            finally { camera.targetTexture = null; }
        }

        internal static void RecordCurrentMatch(TheatrePlan plan)
        {
            if (Busy || SpectatorVideoCapture.Busy || !EditorApplication.isPlaying || EditorApplication.isPaused)
                throw new InvalidOperationException("Resume Play Mode and stop other captures first.");
            var session = Object.FindFirstObjectByType<CastlePlaySession>();
            if (!session || Services.World == null || !session.HasWorldAuthority || session.IsVR ||
                session.Network && session.Network.HumanCount > 1)
                throw new InvalidOperationException("Current-match capture needs an active local desktop match.");
            if (plan == null) throw new InvalidDataException("Recipe is empty.");
            BindCurrentMatch(plan);
            var folder = SpectatorRecordingPlan.TimestampDirectory(Path.Combine(SpectatorRecordingPlan.ProjectRoot,
                "Logs", "GameplayTheatre"));
            File.WriteAllText(Path.Combine(folder, "recipe.json"), JsonUtility.ToJson(plan, true));
            job = new Job { plan = plan, folder = folder, played = true, existingMatch = true, scenes = Array.Empty<RecordingScene>() };
            SessionState.SetString(Key + ".Gallery", folder);
            BeginCapture();
        }

        static void BindCurrentMatch(TheatrePlan plan)
        {
            var cast = CurrentCast();
            if (cast.Count < 1 || cast.Count > 64)
                throw new InvalidOperationException("Current-match motion capture supports 1–64 survivor rigs.");
            plan.observedMatch = true;
            plan.cast = cast.Values.Select(actor => actor.track).OrderBy(track => track.id, StringComparer.Ordinal).ToArray();
            plan.map = MatchSetup.MapId;
            plan.cues = Array.Empty<TheatrePlan.TheatreCue>();
            plan.replaySource = "";
            foreach (var shot in plan.shots ?? Array.Empty<TheatrePlan.TheatreShot>())
            {
                if (shot == null) continue;
                if (!cast.ContainsKey(shot.subject ?? "")) shot.subject = "";
                if (string.IsNullOrEmpty(shot.subject) || !cast.ContainsKey(shot.second ?? "")) shot.second = "";
            }
            plan.Validate();
        }
        public static void Cancel() { if (job != null) End("Cancelled. Rendered frames are preserved."); }
        internal static void ClosePreview() { if (job?.previewOnly == true) End(""); }
        static void Save() => SessionState.SetString(Key, job == null ? "" : JsonUtility.ToJson(job));

        static void State(PlayModeStateChange state)
        {
            if (job == null) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                job.played = true;
                BeginCapture();
            }
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                CleanCapture();
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
                Status = passed ? (finished.previewOnly ? "3D preview closed." : "Gallery ready: " + finished.folder) : finished.failure;
                File.WriteAllText(Path.Combine(finished.folder, "result.txt"), (passed ? "PASS " : "FAIL ") + Status);
                if (finished.batch) EditorApplication.Exit(passed ? 0 : 1);
            }
        }
        static void BeginCapture()
        {
            errors = 0; lastFrame = -1; Save();
            EditorApplication.LockReloadAssemblies();
            AssetDatabase.DisallowAutoRefresh();
            captureLocked = true;
            Application.logMessageReceived += Log;
            take = Capture();
        }
        static void CleanCapture()
        {
            Application.logMessageReceived -= Log;
            try { take?.Dispose(); }
            finally
            {
                take = null;
                RestoreTime();
                StopEncoder();
                if (captureLocked)
                {
                    captureLocked = false;
                    AssetDatabase.AllowAutoRefresh();
                    EditorApplication.UnlockReloadAssemblies();
                }
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
            if (job.existingMatch && !job.batch)
            {
                var finished = job;
                job = null;
                Save();
                CleanCapture();
                Status = failure.Length == 0 ? "Gallery ready: " + finished.folder : failure;
                File.WriteAllText(Path.Combine(finished.folder, "result.txt"),
                    (failure.Length == 0 ? "PASS " : "FAIL ") + Status);
                return;
            }
            job.failure = failure; job.completed = failure.Length == 0; Save();
            EditorApplication.ExitPlaymode();
        }

        static IEnumerator<int> Capture()
        {
            var plan = job.plan;
            bool existingMatch = job.existingMatch;
            var watch = Stopwatch.StartNew();
            CastlePlaySession session;
            while (!(session = Object.FindFirstObjectByType<CastlePlaySession>()) || session.SurvivorBots == null)
            {
                if (watch.Elapsed.TotalSeconds > 30) throw new TimeoutException("Game session never initialized.");
                yield return 0;
            }
            if (session.IsVR || !session.HasWorldAuthority || session.Network && session.Network.HumanCount > 1)
                throw new InvalidOperationException("Theatre requires a local desktop match.");
            if (existingMatch && job.batch)
            {
                while (Services.World == null || CurrentCast().Count == 0)
                {
                    if (watch.Elapsed.TotalSeconds > 30) throw new TimeoutException("Current-match fixture never initialized.");
                    yield return 0;
                }
                BindCurrentMatch(plan);
                File.WriteAllText(Path.Combine(job.folder, "recipe.json"), JsonUtility.ToJson(plan, true));
            }
            SaveTime(plan, !existingMatch);
            if (!existingMatch)
            {
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
                if (selected.mapPrefab && ReferenceEquals(Services.World, previousWorld) ||
                    !selected.mapPrefab && UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != selected.scenePath)
                    throw new InvalidOperationException("The requested map did not provide the active block world.");
            }
            var world = Services.World;
            if (world == null || world.Blocks.Count == 0)
                throw new InvalidOperationException("The requested map did not provide the active block world.");
            Vector3 stage = (plan.useMapAnchor ? world.Center + plan.mapAnchor : Stage(world)) + plan.stageOffset;
            Rigidbody fightDeck = plan.fightSet && !existingMatch ? BuildFightDeck(world, stage) : null;
            bool replay = !string.IsNullOrEmpty(plan.replaySource);
            var towerSet = plan.rooftopBattleSet && !existingMatch ? BuildBattleSet(world, stage) : null;
            var actors = existingMatch ? CurrentCast() : new Dictionary<string, Actor>(StringComparer.Ordinal);
            foreach (var track in plan.cast)
            {
                if (existingMatch) continue;
                var before = new HashSet<GameObject>(session.SurvivorBots.Members);
                if (!session.SurvivorBots.SpawnOne()) throw new InvalidOperationException("Could not cast " + track.id);
                var member = session.SurvivorBots.Members.Single(o => !before.Contains(o));
                var motor = member.GetComponent<CastleSurvivor>();
                var avatar = member.GetComponent<CandyPlayerAvatar>();
                if (!motor || !avatar) throw new InvalidOperationException("Cast member lacks its normal survivor rig.");
                if (track.live && !replay) motor.Teleport(stage + track.start, Quaternion.identity);
                else motor.enabled = false;
                if (track.performance?.Length > 0 || track.stunt?.Length > 0)
                {
                    var brain = member.GetComponent<DeadlyJenga.CastleBots.CastleBotBrain>();
                    if (brain) brain.enabled = false;
                }
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
            if (plan.shots.Any(shot => shot.autoFocus)) cameraData.requiresDepthTexture = true;
            var volumeObject = new GameObject("Theatre focus volume");
            var volume = volumeObject.AddComponent<UnityEngine.Rendering.Volume>();
            volume.isGlobal = true; volume.priority = 1000;
            var focusProfile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
            volume.sharedProfile = focusProfile;
            var focus = focusProfile.Add<DepthOfField>(true);
            focus.mode.Override(DepthOfFieldMode.Bokeh);
            focus.aperture.Override(4);
            focus.focalLength.Override(35);
            focus.active = false;
            int renderWidth = job.previewOnly ? 640 : plan.width;
            int renderHeight = job.previewOnly ? 360 : plan.height;
            var target = RenderTexture.GetTemporary(renderWidth, renderHeight, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(renderWidth, renderHeight, TextureFormat.RGB24, false);
            if (existingMatch && job.batch)
                PreviewCurrentShot(camera, target, plan.shots[0], plan.shots[0].start, plan.stageOffset,
                    plan.useMapAnchor ? plan.mapAnchor : null);
            var cues = (plan.cues ?? Array.Empty<TheatrePlan.TheatreCue>()).OrderBy(c => c.at).ToArray();
            int cueIndex = 0;
            var listenerFocus = session.GetComponent<DeadlyJenga.CandyAudio.CandyListenerFocus>();
            bool restoreFocus = listenerFocus && listenerFocus.enabled;
            if (listenerFocus) listenerFocus.enabled = false;
            var originalListeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None)
                .ToDictionary(listener => listener, listener => listener.enabled);
            foreach (var listener in originalListeners.Keys) listener.enabled = false;
            var audioRig = new GameObject("Theatre audio perspective");
            audioRig.AddComponent<AudioListener>();
            audioRig.transform.position = stage + new Vector3(0, 1.2f, -2);
            TheatreAudioCapture audio = null;
            TheatreMotionTape tape = null;
            int[] counts = new int[plan.shots.Length];
            var frames = new string[plan.shots.Length];
            for (int i = 0; i < frames.Length; i++)
                Directory.CreateDirectory(frames[i] = Path.Combine(job.folder, $"shot-{i + 1:D2}"));
            File.WriteAllText(Path.Combine(job.folder, "cues.txt"), "Simulation seconds and executed cues\n");
            File.WriteAllText(Path.Combine(job.folder, "take.txt"), $"Map={plan.map}\nStage={stage}\nBlocks={world.Blocks.Count}\nCast={actors.Count}\nFPS={plan.fps}\n");
            try
            {
                int end = plan.shots.Max(s => Mathf.RoundToInt(s.start * plan.fps) + Mathf.CeilToInt(s.duration * plan.fps));
                string recording = replay ? plan.replaySource : job.folder;
                var ids = plan.cast.Select(a => a.id).ToArray();
                tape = new TheatreMotionTape(Path.Combine(recording, "motion.djt"), replay, plan.map, plan.fps, end, stage,
                    world.Blocks.ToArray(), ids, ids.Select(id => actors[id].avatar).ToArray());
                stage = tape.Stage;
                if (job.previewOnly)
                {
                    TimelineImage = target;
                    TimelineFrame = 0;
                    TimelineLength = end;
                    Status = "Scrub the saved take or select a different camera.";
                    int previousFrame = -1, previousShot = -1, check = 0;
                    int[] testFrames = { 0, end - 1, end / 2 };
                    while (true)
                    {
                        if (job.batch)
                        {
                            if (check == testFrames.Length) yield break;
                            TimelineFrame = testFrames[check];
                        }
                        int frame = Mathf.Clamp(TimelineFrame, 0, end - 1);
                        int angle = Mathf.Clamp(TimelineShot, 0, plan.shots.Length - 1);
                        if (previousFrame != frame || previousShot != angle)
                        {
                            tape.Seek(frame);
                            foreach (var actor in actors.Values)
                                actor.position = actor.track.stunt?.Length > 0
                                    ? TheatreStuntPose.Present(actor.track.stunt, actor.avatar, actor.motor.transform, stage, frame / (float)plan.fps)
                                    : actor.avatar.transform.position;
                            var shot = plan.shots[angle];
                            float distance = PlaceCamera(camera, shot, actors, stage,
                                Mathf.Clamp01((frame / (float)plan.fps - shot.start) / shot.duration));
                            focus.active = shot.autoFocus;
                            if (focus.active) focus.focusDistance.Override(Mathf.Max(.2f, distance));
                            if (job.batch)
                                Render(camera, target, image, Path.Combine(job.folder, $"preview-{check++}.png"));
                            else
                            {
                                camera.targetTexture = target;
                                try { camera.Render(); }
                                finally { camera.targetTexture = null; }
                            }
                            previousFrame = frame;
                            previousShot = angle;
                        }
                        yield return 0;
                    }
                }
                if (replay)
                {
                    File.Copy(Path.Combine(recording, "take.wav"), Path.Combine(job.folder, "take.wav"));
                    string sourceStems = Path.Combine(recording, "stems");
                    if (Directory.Exists(sourceStems))
                    {
                        string destination = Path.Combine(job.folder, "stems");
                        Directory.CreateDirectory(destination);
                        foreach (string stem in Directory.EnumerateFiles(sourceStems, "*.wav"))
                            File.Copy(stem, Path.Combine(destination, Path.GetFileName(stem)));
                    }
                    File.AppendAllText(Path.Combine(job.folder, "take.txt"), "Motion source: " + recording + "\n");
                }
                else audio = new TheatreAudioCapture(Path.Combine(job.folder, "take.wav"));
                for (int frame = 0; frame < end; frame++)
                {
                    if (existingMatch && !ReferenceEquals(Services.World, world))
                        throw new InvalidOperationException("The map changed during the take.");
                    float time = frame / (float)plan.fps;
                    if (replay)
                    {
                        tape.ReadFrame();
                        foreach (var actor in actors.Values)
                            actor.position = actor.track.stunt?.Length > 0
                                ? TheatreStuntPose.Present(actor.track.stunt, actor.avatar, actor.motor.transform, stage, time)
                                : actor.avatar.transform.position;
                    }
                    else
                    {
                        if (existingMatch)
                        {
                            foreach (var actor in actors.Values)
                            {
                                if (!actor.avatar) throw new InvalidOperationException("A recorded avatar left the match.");
                                actor.position = actor.avatar.transform.position;
                            }
                        }
                        else
                        {
                            foreach (var actor in actors.Values) Perform(actor, stage, time);
                            Physics.SyncTransforms();
                            while (cueIndex < cues.Length && cues[cueIndex].at <= time + .00001f)
                            {
                                if (fightDeck && cues[cueIndex].action == "collapse")
                                {
                                    fightDeck.isKinematic = false;
                                    fightDeck.AddTorque(new Vector3(14, 0, 6), ForceMode.VelocityChange);
                                }
                                if (towerSet != null && cues[cueIndex].action == "collapse")
                                    foreach (var body in towerSet)
                                    {
                                        if (!body.isKinematic || body.name == "Theatre landing deck") continue;
                                        body.isKinematic = false;
                                        float side = Mathf.Sign(body.position.x - stage.x);
                                        float height = body.position.y - stage.y;
                                        float fracture = Mathf.Sin(body.position.x * 3.1f + body.position.z * 2.2f + height * 5);
                                        body.AddForce(new Vector3(-side * height * .66f + fracture * 1.8f, .12f,
                                            -height * .50f + fracture), ForceMode.VelocityChange);
                                        body.AddTorque(new Vector3(.2f + height * .12f, fracture * .25f, side * height * .11f),
                                            ForceMode.VelocityChange);
                                    }
                                if (towerSet != null && cues[cueIndex].action == "dropDeck")
                                    foreach (var body in towerSet.Where(b => b.name == "Theatre landing deck"))
                                    {
                                        body.isKinematic = false;
                                        body.AddTorque(new Vector3(2, 0, -3), ForceMode.VelocityChange);
                                    }
                                Cue(cues[cueIndex++], session, actors, world, stage, time);
                            }
                        }
                        tape.WriteFrame();
                    }
                    for (int i = 0; i < plan.shots.Length; i++)
                    {
                        var shot = plan.shots[i];
                        int first = Mathf.RoundToInt(shot.start * plan.fps), length = Mathf.CeilToInt(shot.duration * plan.fps);
                        if (frame < first || frame >= first + length) continue;
                        float distance = PlaceCamera(camera, shot, actors, stage, (frame - first) / (float)Mathf.Max(1, length - 1));
                        if (shot.firstPerson)
                            audioRig.transform.SetPositionAndRotation(camera.transform.position, camera.transform.rotation);
                        focus.active = shot.autoFocus;
                        if (focus.active) focus.focusDistance.Override(Mathf.Max(.2f, distance));
                        Render(camera, target, image, Path.Combine(frames[i], $"{counts[i]++:D05}.png"));
                    }
                    audio?.CaptureFrame();
                    Status = $"Staging frame {frame + 1}/{end}, {counts.Sum()} camera frames.";
                    yield return 0;
                }
                if (!replay && cueIndex != cues.Length) throw new InvalidDataException("A cue falls after the last sampled frame.");
                audio?.Complete(end, plan.fps);
                File.AppendAllText(Path.Combine(job.folder, "take.txt"),
                    $"Peak recorded particles={tape.PeakParticles}\nParticle budget reached={tape.ParticleBudgetReached}\nPeak equipped actors={tape.PeakEquipped}\n");
            }
            finally
            {
                tape?.Dispose();
                audio?.Dispose();
                audioRig.SetActive(false);
                Object.Destroy(audioRig);
                foreach (var listener in originalListeners)
                    if (listener.Key) listener.Key.enabled = listener.Value;
                if (listenerFocus) listenerFocus.enabled = restoreFocus;
                TimelineImage = null;
                camera.targetTexture = null; RenderTexture.ReleaseTemporary(target);
                Object.Destroy(image); Object.Destroy(cameraObject);
                Object.Destroy(volumeObject); Object.Destroy(focusProfile);
                if (!existingMatch)
                {
                    foreach (var actor in actors.Values) if (actor.avatar) actor.avatar.ClearPoseOverride();
                    if (session) DemoDisasters.StopAll(session);
                }
            }
            if (existingMatch) RestoreTime();
            for (int i = 0; i < counts.Length; i++)
            {
                int expected = Mathf.CeilToInt(plan.shots[i].duration * plan.fps);
                if (counts[i] != expected) throw new InvalidDataException($"Shot {i + 1} captured {counts[i]} frames, expected {expected}.");
                Status = $"Encoding {i + 1}/{counts.Length}.";
                StartEncoder(plan, frames[i], Mathf.RoundToInt(plan.shots[i].start * plan.fps), counts[i]);
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
            if (plan.edit != null && plan.edit.Length > 0)
            {
                Status = "Assembling cut.";
                StartEditEncoder(plan, job.folder);
                var timer = Stopwatch.StartNew();
                while (!encoder.HasExited)
                {
                    if (timer.Elapsed.TotalMinutes > 3) throw new TimeoutException("FFmpeg cut did not finish.");
                    yield return 0;
                }
                encoder.WaitForExit();
                if (encoder.ExitCode != 0) throw new IOException("FFmpeg cut failed: " + string.Join("\n", EncoderErrors));
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
        static Rigidbody BuildFightDeck(IWorldQuery world, Vector3 stage)
        {
            var deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
            deck.name = "Theatre fight deck";
            deck.transform.position = stage - Vector3.up * .34f;
            deck.transform.localScale = new Vector3(5.8f, .64f, 5.4f);
            var surface = world.Blocks.Where(block => block)
                .Select(block => block.GetComponentInChildren<Renderer>()).FirstOrDefault(renderer => renderer);
            if (surface) deck.GetComponent<Renderer>().sharedMaterial = surface.sharedMaterial;
            var escape = GameObject.CreatePrimitive(PrimitiveType.Cube);
            escape.name = "Theatre escape ledge";
            escape.transform.position = stage + new Vector3(-1.58f, -.49f, 3.7f);
            escape.transform.localScale = new Vector3(1.6f, .6f, 2.1f);
            if (surface) escape.GetComponent<Renderer>().sharedMaterial = surface.sharedMaterial;
            var body = deck.AddComponent<Rigidbody>();
            body.isKinematic = true;
            return body;
        }
        static List<Rigidbody> BuildBattleSet(IWorldQuery world, Vector3 stage)
        {
            var palette = world.Blocks.Where(b => b).Select(b => b.GetComponentInChildren<Renderer>())
                .Where(r => r && r.sharedMaterial).Select(r => r.sharedMaterial).Distinct().ToArray();
            var towers = new List<Rigidbody>();
            Block(new Vector3(-1.6f, -3.6f, -.45f), new Vector3(.40f, 5.2f, 2.3f), 2);
            Block(new Vector3(1.6f, -3.6f, -.45f), new Vector3(.40f, 5.2f, 2.3f), 2);
            Block(new Vector3(-3.8f, -3.6f, 5), new Vector3(3.4f, 6, 3.2f), 0);
            Block(new Vector3(3.8f, -3.6f, 5), new Vector3(3.4f, 6, 3.2f), 0);
            var landing = Block(new Vector3(0, -.44f, 0), new Vector3(3.2f, .88f, 3.3f), 1);
            landing.name = "Theatre landing deck";
            var landingBody = landing.AddComponent<Rigidbody>();
            landingBody.isKinematic = true;
            towers.Add(landingBody);
            // Leave the front open so the landing deck can drop away from the main span.
            landingBody.collisionDetectionMode = CollisionDetectionMode.Discrete;
            Block(new Vector3(0, -.55f, 5.3f), new Vector3(13, 1.1f, 7.4f), 2);
            Block(new Vector3(0, -.68f, 12.4f), new Vector3(14, 1.36f, 6.8f), 1);
            foreach (int side in new[] { -1, 1 })
            {
                float x = side * (side < 0 ? 5.5f : 7f);
                float z = side < 0 ? 10.8f : 14.7f;
                Block(new Vector3(x, -1.5f, z), new Vector3(4.1f, 3, 4.1f), 0);
                for (int level = 0; level < (side < 0 ? 7 : 9); level++)
                for (int lane = -1; lane <= 1; lane++)
                {
                    bool cross = level % 2 != 0;
                    var offset = (cross ? Vector3.forward : Vector3.right) * (lane * 1.25f);
                    float taper = 1 - level * .035f;
                    float drift = Mathf.Sin(level * .82f) * .7f;
                    var block = Block(new Vector3(x + drift, .46f + .92f * level, z) + offset * taper,
                        new Vector3(1.23f * taper, .92f, 3.75f * taper), level + lane + 2);
                    if (cross) block.transform.Rotate(0, 90, 0);
                    var body = block.AddComponent<Rigidbody>();
                    body.mass = 4;
                    body.isKinematic = true;
                    towers.Add(body);
                }
            }
            return towers;

            GameObject Block(Vector3 position, Vector3 size, int material)
            {
                var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                piece.name = "Theatre rooftop block";
                piece.layer = Layers.Block;
                piece.transform.position = stage + position;
                piece.transform.localScale = size;
                if (palette.Length > 0)
                    piece.GetComponent<Renderer>().sharedMaterial = palette[material % palette.Length];
                return piece;
            }
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
            if (time >= actor.downAt)
            {
                var rig = actor.avatar.Rig;
                if (!rig) return;
                float fall = Mathf.SmoothStep(0, 1, Mathf.Clamp01((time - actor.downAt) / .30f));
                var frame = actor.avatar.ReadFrame();
                actor.avatar.ApplyCosmeticFrame(frame);
                var lean = Quaternion.AngleAxis(88 * fall, actor.facing * Vector3.right);
                for (int i = 0; i < rig.BoneCount; i++)
                {
                    var bone = rig.AppearanceBone(i);
                    if (!bone) continue;
                    bone.SetPositionAndRotation(actor.position + lean * (bone.position - actor.position)
                        + (actor.facing * Vector3.forward) * (.33f * fall) + Vector3.up * (.065f * fall), lean * bone.rotation);
                }
                return;
            }
            if (actor.track.live)
            {
                actor.position = actor.motor.transform.position;
                return;
            }
            if (actor.track.stunt?.Length > 0)
            {
                actor.position = TheatreStuntPose.Present(actor.track.stunt, actor.avatar, actor.motor.transform, stage, time);
                actor.facing = actor.motor.transform.rotation;
                return;
            }
            var keys = actor.track.performance;
            if (keys != null && keys.Length > 0)
            {
                int next = Array.FindIndex(keys, key => key.at > time);
                var a = keys[next < 0 ? keys.Length - 1 : Mathf.Max(0, next - 1)];
                var b = next < 0 ? a : keys[next];
                float u = a == b ? 0 : Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a.at, b.at, time));
                actor.position = stage + Vector3.Lerp(a.feet, b.feet, u);
                actor.facing = Quaternion.Euler(0, Mathf.LerpAngle(a.yaw, b.yaw, u), 0);
                actor.motor.transform.SetPositionAndRotation(actor.position, actor.facing);
                var frame = actor.avatar.ReadFrame();
                float size = Mathf.Max(.1f, frame.scale);
                frame.feet = actor.position;
                frame.head = actor.position + Vector3.up * (Mathf.Lerp(a.height, b.height, u) * size);
                frame.headRotation = actor.facing * Quaternion.Euler(Mathf.Lerp(a.pitch, b.pitch, u), 0, Mathf.Lerp(a.roll, b.roll, u));
                frame.left = FightHand(true, Vector3.Lerp(a.left, b.left, u), Mathf.Lerp(a.leftCurl, b.leftCurl, u));
                frame.right = FightHand(false, Vector3.Lerp(a.right, b.right, u), Mathf.Lerp(a.rightCurl, b.rightCurl, u));
                actor.avatar.OverridePose(frame);
                actor.avatar.RefreshExternal(Time.deltaTime);
                actor.avatar.ApplyCosmeticFrame(frame);
                return;

                CandyHandPose FightHand(bool left, Vector3 offset, float curl) => new CandyHandPose
                {
                    tracked = true, skeletonTracked = true,
                    position = actor.position + actor.facing * offset * size,
                    rotation = actor.facing * Quaternion.Euler(-8, 0, left ? -19 : 19),
                    skeleton = CandyHandSkeleton.Rest(left, curl)
                };
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
            foreach (var key in actor.track.poses ?? Array.Empty<TheatrePlan.TheatrePose>())
            {
                float u = (time - key.at) / key.duration;
                if (u < 0 || u > 1) continue;
                float weight = Mathf.SmoothStep(0, 1, Mathf.Clamp01(Mathf.Min(u, 1 - u) * 5));
                pose.head -= Vector3.up * (key.crouch * scale * weight);
                pose.headRotation *= Quaternion.Euler(key.lean * weight, 0, 0);
                pose.left.position += actor.facing * key.leftReach * (scale * weight);
                pose.right.position += actor.facing * key.rightReach * (scale * weight);
            }
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
                    wave.approachSpeed = cue.strength > 0 ? Mathf.Clamp(cue.strength, 1, 30) : 30;
                    wave.height = cue.height > 0 ? cue.height : Mathf.Clamp(stage.y - world.WaterHeight(stage) + 8, 18, 100);
                    wave.affectPlayers = false;
                    wave.Begin();
                    if (!wave.Active) throw new InvalidOperationException("Staged tsunami failed to start.");
                    detail += $" heading={wave.approachHeading} height={wave.height}";
                    break;
                case "dropDeck": break;
                case "stopHazards": DemoDisasters.StopAll(session); break;
                case "gear":
                    var kind = (EquipmentKind)Enum.Parse(typeof(EquipmentKind), cue.weapon, true);
                    bool equipped;
                    if (string.IsNullOrEmpty(cue.actor))
                        equipped = session.Modes.GiveEveryoneEquipment(kind, false);
                    else
                    {
                        var recipient = actors[cue.actor].motor.GetComponent<IModeActor>();
                        var grant = typeof(MatchModeRuntime).GetMethod("GiveEquipmentTo",
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                        equipped = recipient != null && grant != null &&
                            (bool)grant.Invoke(session.Modes, new object[] { recipient.ActorId, kind, false, true });
                    }
                    if (!equipped) throw new InvalidOperationException("Could not grant " + cue.weapon + " to " + cue.actor);
                    detail += " " + kind;
                    break;
                case "fire":
                    var shooter = actors[cue.actor];
                    var subject = actors[cue.target];
                    var identity = shooter.motor.GetComponent<IModeActor>();
                    if (identity == null || string.IsNullOrEmpty(identity.ActorId))
                        throw new InvalidOperationException("Shooter is not registered as a mode actor: " + cue.actor);
                    Vector3 aim = subject.position + Vector3.up * (cue.lethal ? .8f : .85f) - identity.ViewPose.position;
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
                    if (!fired && !cue.optional) throw new InvalidOperationException("Gameplay rejected fire cue for " + cue.actor +
                        ". Check draw state, cooldown, and cover at the muzzle.");
                    if (fired && cue.lethal)
                    {
                        subject.downAt = time;
                        subject.motor.GetComponent<IDamageable>()?.Eliminate(shooter.motor.gameObject);
                    }
                    detail += $" {cue.actor}->{cue.target} weapon={session.Modes.EquipmentFor(identity.ActorId).kind} accepted={fired}";
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
        static float PlaceCamera(Camera camera, TheatrePlan.TheatreShot shot, Dictionary<string, Actor> actors, Vector3 stage, float u)
        {
            if (shot.firstPerson)
            {
                var pose = actors[shot.subject].avatar.ReadFrame();
                camera.transform.SetPositionAndRotation(pose.head + pose.headRotation * shot.from * pose.scale, pose.headRotation);
                camera.fieldOfView = shot.fov;
                return 4;
            }
            var anchor = stage;
            if (!string.IsNullOrEmpty(shot.subject) && actors.TryGetValue(shot.subject, out var subject))
            {
                anchor = subject.position;
                if (!string.IsNullOrEmpty(shot.second) && actors.TryGetValue(shot.second, out var second))
                    anchor = (anchor + second.position) * .5f;
            }
            var position = anchor + Vector3.Lerp(shot.from, shot.to, Mathf.SmoothStep(0, 1, u));
            var look = anchor + shot.look;
            if (shot.avoidWalls && Physics.SphereCast(look, .12f, (position - look).normalized, out var obstruction,
                Vector3.Distance(look, position), (1 << Layers.Block) | (1 << Layers.Surface), QueryTriggerInteraction.Ignore))
                position = look + (position - look).normalized * Mathf.Max(.25f, obstruction.distance - .12f);
            var direction = look - position;
            if (direction.sqrMagnitude < .001f) throw new InvalidDataException("Camera and look target overlap: " + shot.name);
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction) * Quaternion.Euler(0, 0, shot.roll));
            camera.fieldOfView = shot.fov;
            return direction.magnitude;
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

        static void StartEncoder(TheatrePlan plan, string folder, int startFrame, int frameCount)
        {
            EncoderErrors.Clear();
            var start = new ProcessStartInfo(plan.ffmpeg) { WorkingDirectory = folder, UseShellExecute = false,
                CreateNoWindow = true, RedirectStandardError = true };
            string from = (startFrame / (double)plan.fps).ToString("F9", CultureInfo.InvariantCulture);
            string duration = (frameCount / (double)plan.fps).ToString("F9", CultureInfo.InvariantCulture);
            start.Arguments = "-hide_banner -loglevel error -nostdin -n -framerate " +
                plan.fps.ToString(CultureInfo.InvariantCulture) +
                $" -i %05d.png -i ../take.wav -map 0:v:0 -map 1:a:0 -af atrim=start={from},asetpts=PTS-STARTPTS,apad" +
                $" -t {duration} -c:v libx264 -preset fast -crf 17 -pix_fmt yuv420p -c:a aac -b:a 192k -movflags +faststart clip.mp4";
            RunEncoder(start);
        }

        static void StartEditEncoder(TheatrePlan plan, string folder)
        {
            string Number(double value) => value.ToString("F9", CultureInfo.InvariantCulture);
            var args = new StringBuilder("-hide_banner -loglevel error -nostdin -n");
            var filters = new StringBuilder();
            var join = new StringBuilder();
            for (int i = 0; i < plan.edit.Length; i++)
            {
                var cut = plan.edit[i];
                args.Append($" -i shot-{cut.shot + 1:D2}/clip.mp4");
                string range = $"start={Number(cut.from)}:duration={Number(cut.duration)}";
                filters.Append($"[{i}:v]trim={range},setpts=PTS-STARTPTS[v{i}];");
                filters.Append($"[{i}:a]atrim={range},asetpts=PTS-STARTPTS[a{i}];");
                join.Append($"[v{i}][a{i}]");
            }
            filters.Append(join).Append($"concat=n={plan.edit.Length}:v=1:a=1[v][a]");
            args.Append($" -filter_complex \"{filters}\" -map [v] -map [a] -c:v libx264 -crf 17 -pix_fmt yuv420p -c:a aac -movflags +faststart edit.mp4");
            RunEncoder(new ProcessStartInfo(plan.ffmpeg) { WorkingDirectory = folder, Arguments = args.ToString(),
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true });
        }

        static void RunEncoder(ProcessStartInfo start)
        {
            EncoderErrors.Clear();
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
            html.Append($"<h1>{Html(plan.title)}</h1><p>{Html(plan.map)} · {plan.width} × {plan.height} · {plan.fps} fps · Unity camera footage with game audio</p><p><a href='take.wav'>Game mix WAV</a> · <a href='recipe.json'>Recipe</a> · <a href='cues.txt'>Cue log</a> · <a href='take.txt'>Take info</a></p>");
            string stemFolder = Path.Combine(job.folder, "stems");
            if (Directory.Exists(stemFolder))
            {
                html.Append("<p>Audio stems ");
                foreach (string stem in Directory.EnumerateFiles(stemFolder, "*.wav").OrderBy(Path.GetFileName))
                    html.Append($"<a href='stems/{Uri.EscapeDataString(Path.GetFileName(stem))}'>{Html(Path.GetFileNameWithoutExtension(stem))}</a> ");
                html.Append("</p>");
            }
            html.Append("<main>");
            for (int i = 0; i < counts.Length; i++)
            {
                string folder = $"shot-{i + 1:D2}", poster = $"{counts[i] / 2:D05}.png";
                html.Append($"<article><video controls preload='metadata' poster='{folder}/{poster}' src='{folder}/clip.mp4'></video><h2>{Html(plan.shots[i].name)}</h2><p>{counts[i] / (float)plan.fps:F2} s · <a href='{folder}/clip.mp4'>MP4</a> · <a href='{folder}/{poster}'>Still</a></p></article>");
            }
            html.Append("</main>");
            if (File.Exists(Path.Combine(job.folder, "edit.mp4")))
                html.Append("<h2>Edited cut</h2><video controls preload='metadata' src='edit.mp4'></video>");
            if (!string.IsNullOrEmpty(plan.replaySource))
            {
                var source = JsonUtility.FromJson<TheatrePlan>(File.ReadAllText(Path.Combine(plan.replaySource, "recipe.json")));
                float first = Mathf.RoundToInt(plan.shots[0].start * plan.fps) / (float)plan.fps;
                int index = Array.FindIndex(source.shots, shot => shot.start <= first && first < shot.start + shot.duration);
                if (index >= 0)
                {
                    string original = Html(new Uri(Path.Combine(plan.replaySource, $"shot-{index + 1:D2}", "clip.mp4")).AbsoluteUri);
                    float offset = first - Mathf.RoundToInt(source.shots[index].start * plan.fps) / (float)plan.fps;
                    string delay = offset.ToString("F6", CultureInfo.InvariantCulture);
                    html.Append($"<h2>Compare the first reframe with its source</h2><main><article><video id='original' controls muted src='{original}'></video></article><article><video id='reframe' controls src='shot-01/clip.mp4'></video></article></main>");
                    html.Append("<button id='compare'>Play both at the same take time</button><script>");
                    html.Append($"document.getElementById('compare').onclick=()=>{{const a=document.getElementById('original'),b=document.getElementById('reframe');a.currentTime=b.currentTime+{delay};a.play();b.play();}};");
                    html.Append("</script>");
                }
            }
            html.Append("</html>");
            File.WriteAllText(Path.Combine(job.folder, "index.html"), html.ToString());
        }
        static void SaveTime(TheatrePlan plan, bool seed)
        {
            oldCaptureRate = Time.captureFramerate; oldRate = Application.targetFrameRate; oldSync = QualitySettings.vSyncCount;
            oldScale = Time.timeScale; oldBackground = Application.runInBackground; oldRandom = UnityEngine.Random.state;
            savedTime = true; Time.timeScale = 1; Time.captureFramerate = plan.fps;
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1; Application.runInBackground = true;
            restoreRandom = seed;
            if (seed) UnityEngine.Random.InitState(plan.seed);
        }
        static void RestoreTime()
        {
            if (!savedTime) return;
            Time.captureFramerate = oldCaptureRate; Application.targetFrameRate = oldRate; QualitySettings.vSyncCount = oldSync;
            Time.timeScale = oldScale; Application.runInBackground = oldBackground;
            if (restoreRandom) UnityEngine.Random.state = oldRandom;
            savedTime = false;
        }
    }
}
