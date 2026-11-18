using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using DeadlyJenga.Modes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    public enum StudioState { Closed, Opening, Held, Running }

    // What one run records. Everything not listed follows its track's own mode.
    public sealed class StudioPass
    {
        public string label = "Recording";
        public List<string> record = new List<string>();   // track ids to record
        public bool fixedClock = true;    // true = exactly one tape frame per rendered frame (repeatable, may not be real time)
        public int from, to = -1;         // tape frames; to = -1 means the end of the Set
        public bool liveWorld;            // the world is simulated (and can be recorded) instead of played back
        public float speed = 1;           // world speed while performing (slow motion: .5 or .25)
        public string performer = "";     // mark id the local player performs (empty = nobody)
        public int preRoll;               // tape frames played before recording starts, so the performer can get moving
        public float countdown;           // seconds of 3-2-1 before the tape rolls
        public bool punchIn;              // keep the performer's old take up to 'from' and record from there
        public bool loop;                 // record again and again, a new take each time, until stopped
        public bool paths = true;         // draw the other actors' paths on the floor for the performer
        public int branchFrom = -1;       // keep the world tape before this frame and simulate from it (-1 = start from a fresh map)
        public string note = "";          // saved with the world take (branch and variation labels)
        public int seed = -1;             // random seed for this run (-1 = the Set's seed)
    }

    // The Studio's engine. It opens a Set as a running scene ("the stage"), then records, plays and scrubs it.
    // The window and the batch tests both drive it through the methods here.
    [InitializeOnLoad]
    public static partial class StudioRun
    {
        const string Key = "DeadlyJenga.TrailerStudio.Job";

        [Serializable] sealed class Job
        {
            public string setId = "", failure = "";
            public bool entered;
            public RecordingScene[] scenes = Array.Empty<RecordingScene>();
        }

        static Job job;
        static IEnumerator<int> routine;
        static int lastTick = -1;

        public static StudioState State { get; private set; } = StudioState.Closed;
        public static StudioStage Stage { get; private set; }
        public static int Frame { get; private set; }
        public static bool Recording { get; private set; }
        public static string Status { get; private set; } = "Stage is closed.";
        public static StudioDrift LastDrift { get; private set; }
        public static readonly List<string> CueLog = new List<string>();

        public static event Action Changed;               // state, frame or tape changed: redraw
        public static event Action<int> FrameShown;       // a running frame is complete (after animation, before render)

        public static bool StageOpen => State == StudioState.Held || State == StudioState.Running;
        public static bool Busy => State == StudioState.Opening || State == StudioState.Running;

        static StudioRun()
        {
            string saved = SessionState.GetString(Key, "");
            if (saved.Length > 0) job = JsonUtility.FromJson<Job>(saved);
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            // A script reload during Play Mode loses the stage; say so instead of pretending.
            if (job != null && job.entered && EditorApplication.isPlaying && Stage == null)
            {
                Status = "Scripts reloaded while the stage was open. Close the stage and open it again.";
                State = StudioState.Closed;
            }
        }

        static void SaveJob() => SessionState.SetString(Key, job == null ? "" : JsonUtility.ToJson(job));
        static void Notify() { try { Changed?.Invoke(); } catch (Exception error) { Debug.LogException(error); } }

        // ---- Opening and closing the stage ----

        public static void OpenStage(StudioSet set)
        {
            if (State != StudioState.Closed || EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling || EditorApplication.isUpdating || GameplayTheatre.Busy)
                throw new InvalidOperationException("Stop Play Mode and other recordings before opening the stage.");
            if (set == null) throw new InvalidOperationException("Pick a Set first.");
            var problems = set.Problems();
            if (problems.Count > 0) throw new InvalidOperationException(problems[0]);
            if (Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount)
                .Any(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save or close modified scenes first.");
            StudioLibrary.Save(set);

            string scene;
            var catalog = SpectatorRecordingPlan.Catalog();
            try
            {
                var map = catalog.maps.FirstOrDefault(m => m.id == set.map);
                if (map == null) throw new InvalidDataException("This map is not in the lobby catalog: " + set.map);
                scene = string.IsNullOrEmpty(map.scenePath) || map.mapPrefab ? ProjectPath.Find("Game.unity") : map.scenePath;
            }
            finally { Object.DestroyImmediate(catalog); }

            var setup = EditorSceneManager.GetSceneManagerSetup().Where(s => !string.IsNullOrEmpty(s.path)).ToArray();
            job = new Job { setId = set.id,
                scenes = setup.Select(s => new RecordingScene { path = s.path, loaded = s.isLoaded, active = s.isActive }).ToArray() };
            SaveJob();
            State = StudioState.Opening;
            Status = "Opening the stage…";
            Debug.Log("[TrailerStudio] opening stage for " + set.id + " in " + scene);
            try { EditorSceneManager.OpenScene(scene); EditorApplication.EnterPlaymode(); }
            catch { job = null; SaveJob(); State = StudioState.Closed; throw; }
            Notify();
        }

        public static void CloseStage()
        {
            if (job == null) { State = StudioState.Closed; return; }
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            else { job = null; SaveJob(); State = StudioState.Closed; Notify(); }
        }

        static void PlayModeChanged(PlayModeStateChange change)
        {
            Debug.Log($"[TrailerStudio] play mode {change}; job {(job == null ? "none" : job.setId)}; state {State}");
            if (job == null) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                job.entered = true; SaveJob();
                State = StudioState.Opening;
                routine = OpenRoutine();
            }
            // Unity Version Control can cancel a Play Mode entry and start it again a moment later.
            // That shows up as an "exiting" event before Play Mode was ever entered; it is not the stage closing.
            if (change == PlayModeStateChange.ExitingPlayMode && job.entered) Teardown();
            if (change == PlayModeStateChange.EnteredEditMode && job.entered)
            {
                var finished = job; job = null; SaveJob();
                try
                {
                    if (finished.scenes.Length > 0)
                        EditorSceneManager.RestoreSceneManagerSetup(finished.scenes.Select(s =>
                            new SceneSetup { path = s.path, isLoaded = s.loaded, isActive = s.active }).ToArray());
                }
                catch (Exception error) { Debug.LogException(error); }
                State = StudioState.Closed;
                Status = string.IsNullOrEmpty(finished.failure) ? "Stage is closed." : finished.failure;
                Notify();
            }
        }

        static void Tick()
        {
            if (routine == null || !EditorApplication.isPlaying || Time.frameCount == lastTick) return;
            lastTick = Time.frameCount;
            try { if (!routine.MoveNext()) routine = null; }
            catch (Exception error)
            {
                routine = null;
                Debug.LogException(error);
                Fail(error.Message);
            }
        }

        // Something went wrong: stop the run, keep the stage open if it exists, and say what happened.
        static void Fail(string message)
        {
            Debug.LogWarning("[TrailerStudio] " + message);
            Status = message;
            running = false; Recording = false;
            DisposeRecorders();
            AbandonRun(currentPass != null && currentPass.liveWorld);
            if (Stage == null)
            {
                if (job != null) { job.failure = message; SaveJob(); }
                State = StudioState.Closed;
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            }
            else
            {
                try { Hold(Mathf.Clamp(Frame, 0, Stage.Frames - 1)); }
                catch (Exception error) { Debug.LogException(error); State = StudioState.Held; }
                Status = message;
            }
            Notify();
        }

        static IEnumerator<int> OpenRoutine()
        {
            var set = StudioLibrary.Load(job.setId);
            if (set == null) throw new InvalidDataException("The Set file is missing: " + job.setId);
            var watch = Stopwatch.StartNew();
            CastlePlaySession session;
            while (!(session = Object.FindFirstObjectByType<CastlePlaySession>()) || session.SurvivorBots == null)
            {
                if (watch.Elapsed.TotalSeconds > 60) throw new TimeoutException("The game session never started.");
                yield return 0;
            }
            if (!session.HasWorldAuthority) throw new InvalidOperationException("The Studio needs a local match (not a joined one).");

            SaveClock();
            SetClock(set.fps, true, 1);
            UnityEngine.Random.InitState(set.seed);
            MatchSetup.MapId = set.map;
            var selected = MatchSetup.SelectedMap;
            if (selected == null) throw new InvalidOperationException("Map selection failed: " + set.map);
            var previousWorld = Services.World;
            MatchSetup.ApplyMap();
            session.SurvivorBots.AutoFill = false; session.SurvivorBots.Clear(); session.HandBots.Clear();
            session.SetMenu(false);
            var rules = ModeSimulation.Defaults(GameModeKind.Demo);
            rules.punchEnabled = rules.healthEnabled = true;
            rules.deathEnabled = false;
            session.Modes.Configure(rules);
            session.Modes.StartRound();
            // No random weapon and power-up bubbles on a film set. Marks can still start with equipment.
            session.Modes.ConfigurePowerupSpawns(new PowerupSpawnSettings { enabled = false });
            if (session.Social != null) session.Social.VoiceEnabled = false;
            for (int n = 0; n < Mathf.CeilToInt(set.warmup * set.fps) + 3; n++) yield return 0;
            if (selected.mapPrefab && ReferenceEquals(Services.World, previousWorld) ||
                !selected.mapPrefab && UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != selected.scenePath)
                throw new InvalidOperationException("The map did not load: " + set.map);
            var world = Services.World;
            if (world == null || world.Blocks.Count == 0) throw new InvalidOperationException("The map has no blocks: " + set.map);

            Stage = new StudioStage { session = session, world = world, set = set };
            Stage.RefreshBlocks();
            Stage.anchor = StudioStage.FindAnchor(world, set);
            ApplyStartState(Stage);
            StudioEnvironment.Apply(set);
            Stage.tape = LoadOrCreateTape(set, Stage);
            StudioCast.Park(Stage);
            StudioCamera.Create(Stage);
            StudioDrivers.Ensure();
            Hold(0);
            Status = "Stage is open.";
            Debug.Log($"[TrailerStudio] stage open: {set.map}, {Stage.blocks.Length} blocks, anchor {Stage.anchor}");
        }

        public static StudioTape LoadOrCreateTape(StudioSet set, StudioStage stage)
        {
            string file = StudioLibrary.TapePath(set.id);
            StudioTape tape = null;
            if (StudioTape.IsTape(file))
            {
                try { tape = StudioTape.Load(file); }
                catch (Exception error) { Debug.LogWarning("Trailer Studio could not read " + file + ": " + error.Message); }
            }
            if (tape == null) tape = new StudioTape { path = file };
            tape.header.setId = set.id; tape.header.map = set.map; tape.header.fps = set.fps;
            tape.header.frames = set.Frames;
            if (stage != null) { tape.header.stage = stage.anchor; tape.header.blocks = stage.blocks.Length; }
            if (string.IsNullOrEmpty(tape.header.created)) tape.header.created = DateTime.UtcNow.ToString("o");
            foreach (var type in StudioTrackTypes.All) type.EnsureTracks(set, tape);
            return tape;
        }

        static void Teardown()
        {
            running = false; Recording = false; routine = null;
            EndLiveSound(false);
            DisposeRecorders();
            DisposePlayers();
            StudioCamera.Destroy();
            StudioDrivers.Destroy();
            StudioEnvironment.Restore();
            StudioDirector.End();
            RestoreClock();
            Stage = null;
            State = StudioState.Closed;
        }

        // ---- The clock ----

        static bool clockSaved;
        static int oldCapture, oldRate, oldSync;
        static float oldScale;
        static bool oldBackground;

        static void SaveClock()
        {
            oldCapture = Time.captureFramerate; oldRate = Application.targetFrameRate; oldSync = QualitySettings.vSyncCount;
            oldScale = Time.timeScale; oldBackground = Application.runInBackground;
            clockSaved = true;
            Application.runInBackground = true;
        }

        // fixedClock: every rendered frame is exactly one tape frame. Otherwise the game runs in real time.
        static void SetClock(int fps, bool fixedClock, float scale)
        {
            Time.captureFramerate = fixedClock ? fps : 0;
            if (fixedClock) { QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1; }
            else { QualitySettings.vSyncCount = oldSync; Application.targetFrameRate = oldRate; }
            Time.timeScale = scale;
        }

        static void RestoreClock()
        {
            if (!clockSaved) return;
            Time.captureFramerate = oldCapture; Application.targetFrameRate = oldRate; QualitySettings.vSyncCount = oldSync;
            Time.timeScale = oldScale; Application.runInBackground = oldBackground;
            clockSaved = false;
        }
    }
}
