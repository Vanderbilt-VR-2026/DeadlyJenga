using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    // Sinking Liner tools.
    // Play Mode menu: Tools > Deadly Jenga > Maps > Sinking Liner > Flood Bow / Break at line 1-3 / Auto Sinking.
    // Batch check: -executeMethod DeadlyJenga.DevTools.Editor.LinerTools.RunChecks
    //   1. scripted physics in an empty scene: floats level at its draft; breaking at each line makes separate
    //      floating pieces; a flooded bow tips the ship until it breaks and the bow sinks. Logs/Validation/Liner.txt
    //   2. pictures in Game.unity (Play Mode, no bots) through the automatic sinking. Logs/Captures/Liner/
    [InitializeOnLoad]
    public static class LinerTools
    {
        const string Menu = "Tools/Deadly Jenga/Maps/Sinking Liner/";
        const string MapId = "liner";
        static LinerMap Live => EditorApplication.isPlaying ? Services.World as LinerMap : null;

        [MenuItem(Menu + "Flood Bow (30 s)")] static void Flood() => Live.BreachBow(30);
        [MenuItem(Menu + "Break at Line 1 (stern quarter)")] static void Break1() => Live.Break(0);
        [MenuItem(Menu + "Break at Line 2 (middle)")] static void Break2() => Live.Break(1);
        [MenuItem(Menu + "Break at Line 3 (bow quarter)")] static void Break3() => Live.Break(2);
        [MenuItem(Menu + "Rebuild Ship")] static void Rebuild() => Live.Rebuild();
        [MenuItem(Menu + "Auto Sinking On or Off")] static void Auto() { Live.autoSinking = !Live.autoSinking; Debug.Log("Liner auto sinking: " + Live.autoSinking); }
        [MenuItem(Menu + "Flood Bow (30 s)", true)][MenuItem(Menu + "Break at Line 1 (stern quarter)", true)][MenuItem(Menu + "Break at Line 2 (middle)", true)]
        [MenuItem(Menu + "Break at Line 3 (bow quarter)", true)][MenuItem(Menu + "Rebuild Ship", true)][MenuItem(Menu + "Auto Sinking On or Off", true)]
        static bool HasLiner() => Live;

        static string ValidationFolder => Path.Combine(Directory.GetCurrentDirectory(), "Logs", "Validation");
        static string ReportPath => Path.Combine(ValidationFolder, "Liner.txt");
        static string CaptureFolder => Path.Combine(Directory.GetCurrentDirectory(), "Logs", "Captures", "Liner");

        [MenuItem(Menu + "Run Checks and Pictures")]
        public static void RunChecks()
        {
            Directory.CreateDirectory(ValidationFolder);
            var report = new StringBuilder("Sinking Liner checks\n");
            int problems = 0;
            try { problems += PhysicsChecks(report); }
            catch (Exception e) { report.AppendLine("ERROR " + e); problems++; Debug.LogException(e); }
            report.AppendLine(problems == 0 ? "PHYSICS CHECKS PASSED (pictures follow)" : problems + " PROBLEMS");
            File.WriteAllText(ReportPath, report.ToString());
            Debug.Log(report.ToString());
            BeginCapture(problems);
        }

        // ---------- 1. Scripted physics ----------

        static int PhysicsChecks(StringBuilder report)
        {
            int problems = 0;
            var prefab = Resources.Load<GameObject>("ModeMaps/Mode_" + MapId);
            if (!prefab) { report.AppendLine("FAIL missing ModeMaps/Mode_liner prefab"); return 1; }
            var catalog = Resources.Load<IslandLobbyCatalog>("IslandLobby/Catalog");
            if (!catalog || !catalog.maps.Any(m => m.id == MapId && m.mapPrefab == prefab)) { report.AppendLine("FAIL lobby catalog has no liner entry for the prefab"); problems++; }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var mode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            var go = Object.Instantiate(prefab);
            const float dt = .02f;
            try
            {
                var map = go.GetComponent<LinerMap>();
                map.autoSinking = false;
                map.Rebuild();
                void Run(float seconds) { for (float t = 0; t < seconds; t += dt) { map.Step(dt); Physics.Simulate(dt); } }
                string Pose(Rigidbody b) => $"keel {Keel(map, b):0.0} m, pitch {Signed(b.rotation.eulerAngles.x):0.0} deg, roll {Signed(b.rotation.eulerAngles.z):0.0} deg";

                report.AppendLine($"Hull {map.length} x {map.beam} m, {map.hullHeight} m tall, draft {map.draft} m; {map.Segments.Count} sections, split lines at {string.Join(", ", map.splitLines.Select(f => (f * 100).ToString("0") + "%"))}");
                // a) Floats level at its draft.
                Run(15);
                var whole = map.Blocks[0];
                report.AppendLine($"  floating 15 s: {map.PieceCount} piece, {Pose(whole)}, speed {whole.linearVelocity.magnitude:0.00} m/s");
                float keel = Keel(map, whole);
                if (Mathf.Abs(keel + map.draft) > 1.5f) { report.AppendLine($"  FAIL keel should sit near -{map.draft} m"); problems++; }
                if (Mathf.Abs(Signed(whole.rotation.eulerAngles.x)) > 2 || Mathf.Abs(Signed(whole.rotation.eulerAngles.z)) > 2) { report.AppendLine("  FAIL not level"); problems++; }
                // A sideways nudge: it must roll back upright.
                whole.AddTorque(whole.transform.forward * whole.mass * 4, ForceMode.Impulse);
                Run(2);
                float pushed = Mathf.Abs(Signed(whole.rotation.eulerAngles.z));
                Run(25);
                float settled = Mathf.Abs(Signed(whole.rotation.eulerAngles.z));
                report.AppendLine($"  roll nudge: {pushed:0.0} deg after 2 s, {settled:0.0} deg after 27 s");
                if (settled > 3) { report.AppendLine("  FAIL does not right itself"); problems++; }

                // b) Break at every line: separate floating pieces.
                for (int line = 0; line < map.Segments.Count - 1; line++)
                {
                    bool ok = map.Break(line);
                    if (!ok) { report.AppendLine($"  FAIL break at line {line + 1} did nothing"); problems++; }
                    if (map.Break(line)) { report.AppendLine($"  FAIL line {line + 1} broke twice"); problems++; }
                }
                // Stop the flooding that breaking starts, to see whether the dry pieces float.
                foreach (var s in map.Segments) { s.flood = 0; s.floodTarget = 0; s.floodPerSecond = 0; }
                Run(15);
                report.AppendLine($"  broken at all {map.Segments.Count - 1} lines: {map.PieceCount} pieces");
                if (map.PieceCount != map.Segments.Count) { report.AppendLine($"  FAIL expected {map.Segments.Count} pieces"); problems++; }
                foreach (var b in map.Blocks.Where(b => b && b.gameObject.activeSelf))
                {
                    report.AppendLine($"    {b.name}: {Pose(b)}");
                    if (Keel(map, b) > -map.draft * .4f || Keel(map, b) < -map.hullHeight) { report.AppendLine("    FAIL piece is not floating at a sensible depth"); problems++; }
                }

                // c) The sinking story: flood the bow, the ship tips, breaks in the middle, the bow goes down.
                map.Rebuild();
                Run(5);
                map.BreachBow(30);
                float t0 = 0, maxPitch = 0; bool brokeAt = false;
                for (float t = 0; t < 120; t += dt)
                {
                    map.Step(dt); Physics.Simulate(dt);
                    var b = map.Segments[map.breakLine].GetComponentInParent<Rigidbody>();
                    float pitch = Mathf.Abs(Signed(b.rotation.eulerAngles.x));
                    maxPitch = Mathf.Max(maxPitch, pitch);
                    if (!brokeAt && pitch >= map.breakPitch) { brokeAt = map.Break(map.breakLine); t0 = t; }
                    if (brokeAt && t - t0 > 40) break;
                }
                report.AppendLine($"  sinking: bow flooded, max pitch {maxPitch:0.0} deg, {(brokeAt ? $"broke at line {map.breakLine + 1} after {t0:0} s" : "never broke")}; {map.PieceCount} pieces");
                if (!brokeAt) { report.AppendLine("  FAIL the flooded ship never tipped enough to break"); problems++; }
                foreach (var b in map.Blocks.Where(b => b))
                    report.AppendLine($"    {b.name}: {(b.gameObject.activeSelf ? Pose(b) + $", deck end {DeckLow(map, b):0.0} m" : "sunk out of sight")}");
                var bowPiece = map.Segments[map.Segments.Count - 1].GetComponentInParent<Rigidbody>(true);
                if (bowPiece && bowPiece.gameObject.activeSelf && DeckLow(map, bowPiece) > 0) { report.AppendLine("  FAIL bow piece is still above water"); problems++; }
            }
            catch (Exception e) { report.AppendLine("  ERROR " + e); problems++; Debug.LogException(e); }
            finally { Physics.simulationMode = mode; Object.DestroyImmediate(go); }
            return problems;
        }

        static float Signed(float angle) => Mathf.DeltaAngle(0, angle);

        // Lowest float point of a piece, relative to the water.
        static float Keel(LinerMap map, Rigidbody body) =>
            body.GetComponentsInChildren<LinerSegment>().SelectMany(s => s.floatPoints.Select(p => s.transform.TransformPoint(p).y)).DefaultIfEmpty(0).Average() - map.waterHeight;

        // Lowest deck corner of a piece, relative to the water (negative = that end is under).
        static float DeckLow(LinerMap map, Rigidbody body)
        {
            float deck = map.hullHeight - map.draft, low = float.MaxValue;
            foreach (var s in body.GetComponentsInChildren<LinerSegment>())
                foreach (float z in new[] { s.zFrom, s.zTo })
                    low = Mathf.Min(low, s.transform.TransformPoint(new Vector3(0, deck, z)).y);
            return low - map.waterHeight;
        }

        // ---------- 2. Pictures ----------

        const string Key = "DeadlyJenga.LinerCapture";
        [Serializable] sealed class Job { public int step, problems; public double stepAt; public bool prepared; public List<string> files = new List<string>(); }
        static Job job;

        static LinerTools() { EditorApplication.playModeStateChanged += OnState; }

        static void BeginCapture(int problems)
        {
            Directory.CreateDirectory(CaptureFolder);
            SessionState.SetString(Key, JsonUtility.ToJson(new Job { problems = problems }));
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            EditorApplication.EnterPlaymode();
        }

        static void OnState(PlayModeStateChange change)
        {
            string json = SessionState.GetString(Key, "");
            if (string.IsNullOrEmpty(json)) return;
            job = JsonUtility.FromJson<Job>(json);
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                Application.runInBackground = true; AudioListener.volume = 0;
                MatchSetup.FromLobby = false; MatchSetup.StartLocalSession = false;
                MatchSetup.MapId = MapId; MatchSetup.ModeId = "demo"; MatchSetup.BotTarget = 0; MatchSetup.HandCount = 0;
                job.stepAt = EditorApplication.timeSinceStartup; job.step = 0; job.prepared = false;
                EditorApplication.update += Tick;
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= Tick;
                SessionState.EraseString(Key);
                File.AppendAllText(ReportPath, $"pictures: {job.files.Count} written to {CaptureFolder}\n" + string.Join("\n", job.files.Select(f => "  " + Path.GetFileName(f))) + "\n");
                Finish(job.problems == 0 && job.files.Count > 0 ? 0 : 2);
            }
        }

        static void Finish(int code)
        {
            if (!Application.isBatchMode && SessionState.GetString("DJWarm.id", "") == "") return;
            if (SessionState.GetString("DJWarm.id", "") != "")
            {
                Directory.CreateDirectory("Logs/Warm");
                File.WriteAllText("Logs/Warm/exit.txt", code.ToString());
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
                return;
            }
            EditorApplication.Exit(code);
        }

        // Seconds after the previous step, then what to do. The bow floods fast so the whole story fits in a minute.
        static readonly (float wait, string action)[] Script =
        {
            (6, "shoot calm"),
            (0, "flood"), (14, "shoot tipping"),
            (0, "wait-break"), (2.5f, "shoot breaking"), (12, "shoot broken"), (18, "shoot sinking"),
            (0, "done"),
        };

        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            double now = EditorApplication.timeSinceStartup;
            var session = Services.Session;
            if (!job.prepared)
            {
                if (session == null || Services.World == null) { if (now - job.stepAt > 60) { Debug.LogError("Liner capture: game did not load"); EditorApplication.ExitPlaymode(); } return; }
                if (!(Services.World is LinerMap)) { MatchSetup.MapId = MapId; MatchSetup.ApplyMap(); }
                if (!(Services.World is LinerMap liner)) { if (now - job.stepAt > 60) { Debug.LogError("Liner capture: map did not load"); EditorApplication.ExitPlaymode(); } return; }
                liner.autoSinking = false;
                Prepare(session);
                job.prepared = true; job.stepAt = now; Save(); return;
            }
            Prepare(session);
            var map = Services.World as LinerMap;
            if (!map) { Debug.LogError("Liner capture: map vanished"); EditorApplication.ExitPlaymode(); return; }
            if (job.step >= Script.Length) return;
            var (wait, action) = Script[job.step];
            if (action == "wait-break")
            {
                // Hold here until the tipping ship breaks (or give up after 60 s and break it).
                var b = map.Segments[map.breakLine].GetComponentInParent<Rigidbody>();
                float pitch = Mathf.Abs(Mathf.DeltaAngle(0, b.rotation.eulerAngles.x));
                if (pitch < map.breakPitch && now - job.stepAt < 60) return;
                map.Break(map.breakLine);
                job.stepAt = now; job.step++; Save(); return;
            }
            if (now - job.stepAt < wait) return;
            job.stepAt = now; job.step++;
            try
            {
                if (action == "done") { Save(); EditorApplication.ExitPlaymode(); return; }
                if (action == "flood") map.BreachBow(12);
                else if (action.StartsWith("shoot ", StringComparison.Ordinal)) Shoot(map, session, action.Substring(6));
            }
            catch (Exception e) { Debug.LogException(e); }
            Save();
        }

        static void Save() => SessionState.SetString(Key, JsonUtility.ToJson(job));

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

        static void Shoot(LinerMap map, ISessionInfo session, string label)
        {
            var game = session.ActiveCamera;
            var camera = new GameObject("Liner picture camera", typeof(Camera)).GetComponent<Camera>();
            bool fog = RenderSettings.fog;
            try
            {
                camera.enabled = false;
                if (game) camera.CopyFrom(game);
                camera.cullingMask &= ~(1 << 5);
                camera.farClipPlane = 5000; camera.nearClipPlane = .1f; camera.targetTexture = null;
                var data = camera.GetUniversalAdditionalCameraData();
                data.allowXRRendering = false;
                if (game) { var g = game.GetUniversalAdditionalCameraData(); data.renderPostProcessing = g.renderPostProcessing; data.volumeLayerMask = g.volumeLayerMask; }
                RenderSettings.fog = false;
                var root = map.transform;
                var views = new List<(string name, Vector3 at, Vector3 look, float fov)>
                {
                    ("hero", root.TransformPoint(150, 70, -200), root.TransformPoint(0, 0, 10), 42),
                    ("side", root.TransformPoint(260, 12, 0), root.TransformPoint(0, 2, 0), 55),
                };
                // Eye height on the deck of the stern piece, looking toward the bow.
                var stern = map.Segments[0];
                if (stern && stern.gameObject.activeInHierarchy)
                {
                    var t = stern.transform;
                    float deck = map.hullHeight - map.draft;
                    views.Add(("deck", t.TransformPoint(new Vector3(9, deck + .8f, stern.zTo - 25)), t.TransformPoint(new Vector3(0, deck + 4, stern.zTo + 60)), 70));
                }
                foreach (var (name, at, look, fov) in views)
                {
                    camera.transform.SetPositionAndRotation(at, Quaternion.LookRotation(look - at, Vector3.up));
                    camera.fieldOfView = fov;
                    string path = Path.Combine(CaptureFolder, label + "-" + name + ".png");
                    RenderNow(camera, 1600, 900, path);
                    job.files.Add(path);
                }
                Debug.Log($"Liner picture {label}: {map.PieceCount} pieces, " + string.Join("; ", map.Blocks.Where(b => b && b.gameObject.activeSelf).Select(b => $"{b.name} pitch {Mathf.DeltaAngle(0, b.rotation.eulerAngles.x):0.0}")));
            }
            finally { RenderSettings.fog = fog; Object.Destroy(camera.gameObject); }
        }

        static void RenderNow(Camera camera, int width, int height, string path)
        {
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 4);
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target; camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                Debug.Log("CAPTURE FILE " + path);
            }
            finally { RenderTexture.active = previous; camera.targetTexture = null; RenderTexture.ReleaseTemporary(target); Object.Destroy(image); }
        }
    }
}
