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
    // Deck Ship tools.
    // Play Mode menu: Tools > Deadly Jenga > Maps > Deck Ship > Roll / Pitch / Iceberg / Next Cargo / Auto Events.
    // Batch check (no headset): -executeMethod DeadlyJenga.DevTools.Editor.DeckShipTools.RunChecks
    //   1. every cargo layout is built in an empty scene and run through calm sway, a roll, a pitch and the iceberg
    //      with scripted physics; how much moved, fell and went overboard is written to Logs/Validation/DeckShip.txt
    //   2. the real map is loaded in Game.unity (Play Mode, no bots) and photographed calm, mid-roll, at the
    //      iceberg and while new cargo drops in. Pictures: Logs/Captures/DeckShip/
    [InitializeOnLoad]
    public static class DeckShipTools
    {
        const string Menu = "Tools/Deadly Jenga/Maps/Deck Ship/";
        static DeckShipMap Live => EditorApplication.isPlaying ? Services.World as DeckShipMap : null;

        [MenuItem(Menu + "Roll")] static void Roll() => Live.StartEvent(DeckShipEvent.Roll);
        [MenuItem(Menu + "Pitch")] static void Pitch() => Live.StartEvent(DeckShipEvent.Pitch);
        [MenuItem(Menu + "Iceberg")] static void Iceberg() => Live.StartEvent(DeckShipEvent.Iceberg);
        [MenuItem(Menu + "Right the Ship")] static void Right() => Live.StartEvent(DeckShipEvent.Righting);
        [MenuItem(Menu + "Next Cargo (drop in)")] static void Next() => Live.NextCargo(true);
        [MenuItem(Menu + "Auto Events On or Off")] static void Auto() { Live.autoEvents = !Live.autoEvents; Debug.Log("Deck Ship auto events: " + Live.autoEvents); }
        [MenuItem(Menu + "Roll", true)][MenuItem(Menu + "Pitch", true)][MenuItem(Menu + "Iceberg", true)][MenuItem(Menu + "Right the Ship", true)]
        [MenuItem(Menu + "Next Cargo (drop in)", true)][MenuItem(Menu + "Auto Events On or Off", true)]
        static bool HasShip() => Live;

        static string ValidationFolder => Path.Combine(Directory.GetCurrentDirectory(), "Logs", "Validation");
        static string CaptureFolder => Path.Combine(Directory.GetCurrentDirectory(), "Logs", "Captures", "DeckShip");
        static string ReportPath => Path.Combine(ValidationFolder, "DeckShip.txt");

        [MenuItem(Menu + "Run Checks and Pictures")]
        public static void RunChecks()
        {
            Directory.CreateDirectory(ValidationFolder);
            var report = new StringBuilder("Deck Ship checks\n");
            int problems = 0;
            try { problems += StaticChecks(report); }
            catch (Exception e) { report.AppendLine("ERROR " + e); problems++; Debug.LogException(e); }
            report.AppendLine(problems == 0 ? "PHYSICS CHECKS PASSED (pictures follow)" : problems + " PROBLEMS");
            File.WriteAllText(ReportPath, report.ToString());
            Debug.Log(report.ToString());
            BeginCapture(problems);
        }

        // ---------- 1. Scripted physics in an empty scene ----------

        static int StaticChecks(StringBuilder report)
        {
            int problems = 0;
            var prefab = Resources.Load<GameObject>("ModeMaps/Mode_deck-ship");
            if (!prefab) { report.AppendLine("FAIL missing ModeMaps/Mode_deck-ship prefab"); return 1; }
            var catalog = Resources.Load<IslandLobbyCatalog>("IslandLobby/Catalog");
            if (!catalog || !catalog.maps.Any(m => m.id == "deck-ship" && m.mapPrefab == prefab)) { report.AppendLine("FAIL lobby catalog has no deck-ship entry for the prefab"); problems++; }
            for (int k = 0; k < DeckShipCargo.Count; k++) problems += RunCargo(prefab, (DeckShipCargoKind)k, report, k == 0);
            return problems;
        }

        static int RunCargo(GameObject prefab, DeckShipCargoKind kind, StringBuilder report, bool checkHull)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = Object.Instantiate(prefab);
            var mode = Physics.simulationMode;
            int problems = 0;
            const float dt = .02f;
            try
            {
                var map = go.GetComponent<DeckShipMap>();
                map.autoEvents = false; map.cargo = kind;
                Physics.simulationMode = SimulationMode.Script;
                map.Rebuild();
                if (checkHull) problems += HullChecks(map, report);
                var cargo = map.Blocks.Skip(1).ToList();
                report.AppendLine($"{DeckShipCargo.Titles[(int)kind]}: {cargo.Count} loose blocks");
                float time = 0;
                void Run(float seconds) { for (float t = 0; t < seconds; t += dt) { map.Step(dt); Physics.Simulate(dt); time += dt; map.CleanUpCargo(time); } }
                Vector3[] Local() => cargo.Select(b => map.Hull.InverseTransformPoint(b.position)).ToArray();

                var start = Local();
                Run(8);
                var calm = Local();
                var drift = calm.Select((p, i) => Vector3.Distance(p, start[i])).ToArray();
                int calmMoved = drift.Count(d => d > .5f);
                report.AppendLine($"  calm sway 8 s: max drift {drift.Max():0.00} m, {calmMoved} moved > .5 m, {cargo.Count(b => b.IsSleeping())} asleep, {map.Overboard} overboard");
                if (calmMoved > cargo.Count / 10) { report.AppendLine("  FAIL more than a tenth of the cargo moves in calm sway"); problems++; }
                foreach (var i in Enumerable.Range(0, cargo.Count).Where(i => drift[i] > .5f).Take(5)) report.AppendLine("    drifted: " + cargo[i].name + $" {drift[i]:0.00} m");

                foreach (var (ev, seconds) in new[] { (DeckShipEvent.Roll, DeckShipMap.RollSeconds), (DeckShipEvent.Pitch, DeckShipMap.PitchSeconds), (DeckShipEvent.Iceberg, DeckShipMap.IcebergSeconds) })
                {
                    var before = Local(); int overboard = map.Overboard;
                    float maxRoll = 0, maxPitch = 0;
                    map.StartEvent(ev);
                    for (float t = 0; t < seconds; t += dt)
                    {
                        map.Step(dt); Physics.Simulate(dt); time += dt; map.CleanUpCargo(time);
                        maxRoll = Mathf.Max(maxRoll, Mathf.Abs(map.RollAngle)); maxPitch = Mathf.Max(maxPitch, Mathf.Abs(map.PitchAngle));
                    }
                    Run(3);
                    var after = Local();
                    int slid = 0, fell = 0;
                    for (int i = 0; i < cargo.Count; i++)
                    {
                        if (!cargo[i].gameObject.activeSelf) continue;
                        var move = after[i] - before[i];
                        if (move.y < -1.2f) fell++;
                        else if (new Vector2(move.x, move.z).magnitude > 1) slid++;
                    }
                    report.AppendLine($"  {ev}: max roll {maxRoll:0.0} deg, max pitch {maxPitch:0.0} deg; {slid} slid > 1 m, {fell} fell > 1.2 m, {map.Overboard - overboard} went overboard; {cargo.Count(b => b.gameObject.activeSelf)}/{cargo.Count} still aboard");
                }
                if (map.Overboard == cargo.Count) { report.AppendLine("  NOTE every block went overboard"); }
            }
            catch (Exception e) { report.AppendLine("  ERROR " + e.Message); problems++; Debug.LogException(e); }
            finally { Physics.simulationMode = mode; Object.DestroyImmediate(go); }
            return problems;
        }

        // The deck is under every spawn spot, ramps are walkable, and the climb boards are registered.
        static int HullChecks(DeckShipMap map, StringBuilder report)
        {
            int problems = 0;
            Physics.SyncTransforms();
            for (int i = 0; i < 6; i++)
            {
                var p = map.SafeSpawnPoint();
                bool ground = Physics.Raycast(p + Vector3.up * .5f, Vector3.down, out var hit, 1.5f, 1, QueryTriggerInteraction.Ignore) && hit.normal.y > .9f;
                if (!ground) { report.AppendLine($"  FAIL spawn {p} has no flat deck under it"); problems++; }
            }
            var ramps = map.Hull.GetComponentsInChildren<BoxCollider>().Where(c => c.name.Contains("ramp")).ToList();
            foreach (var ramp in ramps)
            {
                float angle = Vector3.Angle(ramp.transform.up, map.Hull.up);
                if (angle > 30) { report.AppendLine($"  FAIL {ramp.name} is {angle:0} deg steep"); problems++; }
            }
            var climbFaces = map.Hull.GetComponentsInChildren<BoxCollider>().Where(c => c.gameObject.layer == Layers.Surface).ToList();
            int registered = climbFaces.Count(c => Climbables.TryGet(c, out var climb) && climb.Body == map.Blocks[0]);
            report.AppendLine($"  hull: {map.Hull.GetComponentsInChildren<Collider>().Length} colliders, {ramps.Count} ramps (max {ramps.Select(r => Vector3.Angle(r.transform.up, map.Hull.up)).DefaultIfEmpty().Max():0} deg), {registered}/{climbFaces.Count} climb boards registered, spawns on deck");
            if (registered != climbFaces.Count || climbFaces.Count == 0) problems++;
            return problems;
        }

        // ---------- 2. Pictures in the real game ----------

        const string Key = "DeadlyJenga.DeckShipCapture";
        [Serializable] sealed class Job { public int step, problems; public double stepAt; public bool prepared; public List<string> files = new List<string>(); }
        static Job job;

        static DeckShipTools() { EditorApplication.playModeStateChanged += OnState; }

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
                MatchSetup.MapId = "deck-ship"; MatchSetup.ModeId = "demo"; MatchSetup.BotTarget = 0; MatchSetup.HandCount = 0;
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

        // Each entry: seconds after the previous step, then what to do.
        static readonly (float wait, string action)[] Script =
        {
            (6, "shoot calm"),
            (0, "roll"), (6, "shoot roll-peak"), (13.5f, "shoot roll-after"),
            (0, "iceberg"), (3.5f, "shoot iceberg-approach"), (2.1f, "shoot iceberg-impact"), (7, "shoot iceberg-after"),
            (6, "right"), (8.5f, "cargo 1"), (1.1f, "shoot drop-container-yard"), (6, "shoot container-yard"),
            (0, "cargo 2"), (6.5f, "shoot domino-run"), (0, "pitch"), (5.3f, "shoot domino-pitch"),
            (10, "cargo 3"), (6.5f, "shoot step-pyramid"),
            (0, "done"),
        };

        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            double now = EditorApplication.timeSinceStartup;
            var session = Services.Session;
            if (!job.prepared)
            {
                if (session == null || Services.World == null) { if (now - job.stepAt > 60) { Debug.LogError("Deck Ship capture: game did not load"); EditorApplication.ExitPlaymode(); } return; }
                if (!(Services.World is DeckShipMap)) { MatchSetup.MapId = "deck-ship"; MatchSetup.ApplyMap(); }
                if (!(Services.World is DeckShipMap ship)) { if (now - job.stepAt > 60) { Debug.LogError("Deck Ship capture: map did not load"); EditorApplication.ExitPlaymode(); } return; }
                ship.autoEvents = false;
                Prepare(session);
                job.prepared = true; job.stepAt = now; Save(); return;
            }
            Prepare(session);
            var map = Services.World as DeckShipMap;
            if (!map) { Debug.LogError("Deck Ship capture: map vanished"); EditorApplication.ExitPlaymode(); return; }
            if (job.step >= Script.Length) return;
            var (wait, action) = Script[job.step];
            if (now - job.stepAt < wait) return;
            job.stepAt = now; job.step++;
            try
            {
                if (action == "done") { Save(); EditorApplication.ExitPlaymode(); return; }
                if (action == "roll") map.StartEvent(DeckShipEvent.Roll);
                else if (action == "pitch") map.StartEvent(DeckShipEvent.Pitch);
                else if (action == "iceberg") map.StartEvent(DeckShipEvent.Iceberg);
                else if (action == "right") map.StartEvent(DeckShipEvent.Righting);
                else if (action.StartsWith("cargo ", StringComparison.Ordinal)) map.SwapCargo((DeckShipCargoKind)int.Parse(action.Substring(6)), true);
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

        // Views: hero (aft starboard, high), stern (behind, shows rolling), side (starboard, shows pitch),
        // deck (eye height on the stern deck, moving with the ship), top.
        static void Shoot(DeckShipMap map, ISessionInfo session, string label)
        {
            string[] views = label.StartsWith("calm", StringComparison.Ordinal) ? new[] { "hero", "stern", "side", "deck", "top" }
                : label.StartsWith("roll", StringComparison.Ordinal) ? new[] { "stern", "hero", "deck" }
                : label.StartsWith("iceberg", StringComparison.Ordinal) ? new[] { "hero", "deck" }
                : label.Contains("pitch") ? new[] { "side", "deck" }
                : new[] { "hero", "deck" };
            var game = session.ActiveCamera;
            var camera = new GameObject("Deck Ship picture camera", typeof(Camera)).GetComponent<Camera>();
            bool fog = RenderSettings.fog;
            try
            {
                camera.enabled = false;
                if (game) camera.CopyFrom(game);
                camera.cullingMask &= ~(1 << 5);
                camera.farClipPlane = 4000; camera.nearClipPlane = .05f;
                camera.targetTexture = null;
                var data = camera.GetUniversalAdditionalCameraData();
                data.allowXRRendering = false;
                if (game) { var g = game.GetUniversalAdditionalCameraData(); data.renderPostProcessing = g.renderPostProcessing; data.volumeLayerMask = g.volumeLayerMask; }
                RenderSettings.fog = false;
                var root = map.transform; var ship = map.Hull;
                foreach (var view in views)
                {
                    switch (view)
                    {
                        case "hero": Place(camera, root.TransformPoint(34, 30, -52), root.TransformPoint(0, 5, 2), 42); break;
                        case "stern": Place(camera, root.TransformPoint(0, 13, -78), root.TransformPoint(0, 7, 0), 36); break;
                        case "side": Place(camera, root.TransformPoint(72, 11, 0), root.TransformPoint(0, 6, 0), 46); break;
                        case "top": Place(camera, root.TransformPoint(0, 110, 0), root.TransformPoint(0, 0, .01f), 42); break;
                        default: Place(camera, ship.TransformPoint(0, 7.8f, -25.5f), ship.TransformPoint(0, 6.2f, 6), 70, ship.up); break;
                    }
                    string path = Path.Combine(CaptureFolder, label + "-" + view + ".png");
                    RenderNow(camera, 1600, 900, path);
                    job.files.Add(path);
                }
                Debug.Log($"Deck Ship picture {label}: event {map.CurrentEvent}, roll {map.RollAngle:0.0}, pitch {map.PitchAngle:0.0}, cargo {map.CargoBlocks}, overboard {map.Overboard}");
            }
            finally { RenderSettings.fog = fog; Object.Destroy(camera.gameObject); }
        }

        static void Place(Camera camera, Vector3 position, Vector3 target, float fov, Vector3 up = default)
        {
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position, up == default ? Vector3.up : up));
            camera.fieldOfView = fov;
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
                File.WriteAllBytes(path, image.EncodeToPNG());
                Debug.Log("CAPTURE FILE " + path);
            }
            finally { RenderTexture.active = previous; camera.targetTexture = null; RenderTexture.ReleaseTemporary(target); Object.Destroy(image); }
        }
    }
}
