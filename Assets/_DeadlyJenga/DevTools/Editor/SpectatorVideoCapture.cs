using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [Serializable] public sealed class RecordingScene
    {
        public string path;
        public bool loaded, active;
    }

    [Serializable] public sealed class RecordingJob
    {
        public SpectatorRecordingPlan plan, previous;
        public RecordingScene[] scenes;
        public string phase, batchFolder, runFolder, status, failure = "";
        public int iteration, completed, failed, frame, width, height, errors;
        public long heartbeat;
        public bool cancel, commandLine, locked, previousFromLobby, previousLocal, runtimeStarted, runFinished, enteredPlay;
    }

    [InitializeOnLoad]
    public static class SpectatorVideoCapture
    {
        const string Key = "DeadlyJenga.SpectatorRecording.Job";
        static RecordingJob job;
        static Process encoder;
        static bool quitting;
        static double nextPoll;
        static string requestError = "";
        public static bool Busy => job != null && job.phase != "Finished";
        public static string Status => !string.IsNullOrEmpty(requestError) ? requestError : job?.status ?? "Ready to record.";
        public static string LastOutput => SessionState.GetString(Key + ".Output", SpectatorRecordingPlan.DefaultOutput);
        public static RecordingJob Job => job;

        static SpectatorVideoCapture()
        {
            string saved = SessionState.GetString(Key, "");
            if (!string.IsNullOrEmpty(saved)) job = JsonUtility.FromJson<RecordingJob>(saved);
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += PlayState;
            EditorApplication.quitting += () => { quitting = true; StopEncoder(); };
            AssemblyReloadEvents.beforeAssemblyReload += () =>
            {
                if (!Busy) return;
                if (job.phase == "Recording") { job.failure = "Assembly reload interrupted recording; source frames were kept."; job.runFinished = true; job.phase = "Returning"; }
                Save();
            };
        }

        public static void Start(SpectatorRecordingPlan source, bool commandLine = false)
        {
            if (source == null) throw new ArgumentException("Recording settings are missing or invalid.");
            if (Busy) throw new InvalidOperationException("A recording batch is already running.");
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) throw new InvalidOperationException("Wait for Unity to finish compiling and importing.");
            if (IslandRoomConnection.Instance && (IslandRoomConnection.Instance.Connected || IslandRoomConnection.Instance.Busy))
                throw new InvalidOperationException("Leave the multiplayer room before starting a local recording batch.");
            var plan = SpectatorRecordingPlan.Clone(source);
            plan.Validate(); CheckEncoder(plan.ffmpeg);
            requestError = "";
            if (!EditorApplication.isPlaying && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scenes = EditorSceneManager.GetSceneManagerSetup();
            if (scenes.Any(s => string.IsNullOrEmpty(s.path))) throw new InvalidOperationException("Save unnamed scenes before starting a recording batch.");
            var previous = SpectatorRecordingPlan.Current();
            string batch = SpectatorRecordingPlan.TimestampDirectory(plan.output);
            job = new RecordingJob
            {
                plan = plan, previous = previous, commandLine = commandLine,
                previousFromLobby = MatchSetup.FromLobby, previousLocal = MatchSetup.StartLocalSession,
                scenes = scenes.Select(s => new RecordingScene { path = s.path, loaded = s.isLoaded, active = s.isActive }).ToArray(),
                phase = "WaitingForEdit", batchFolder = batch, status = "Preparing recording batch."
            };
            SessionState.SetString(Key + ".Output", batch);
            File.WriteAllText(Path.Combine(batch, "settings.json"), JsonUtility.ToJson(plan, true));
            Save();
            if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode();
        }

        public static void Run()
        {
            try
            {
                string file = Argument("-capturePlan");
                var plan = string.IsNullOrEmpty(file) ? SpectatorRecordingPlan.Current() : JsonUtility.FromJson<SpectatorRecordingPlan>(File.ReadAllText(file));
                if (!string.IsNullOrEmpty(Argument("-captureOutput"))) plan.output = Argument("-captureOutput");
                if (!string.IsNullOrEmpty(Argument("-captureMode")))
                {
                    plan.modeId = Argument("-captureMode");
                    plan.match.rules = DeadlyJenga.Modes.ModeSimulation.Defaults(DeadlyJenga.Modes.MatchModeRuntime.ParseMode(plan.modeId));
                }
                if (!string.IsNullOrEmpty(Argument("-captureMap"))) plan.mapId = Argument("-captureMap");
                if (int.TryParse(Argument("-captureBots"), out int bots)) plan.match.population = bots + 1;
                if (int.TryParse(Argument("-captureIterations"), out int iterations)) plan.iterations = iterations;
                if (int.TryParse(Argument("-captureSeconds"), out int seconds)) plan.seconds = seconds;
                plan.notify = false;
                Start(plan, true);
            }
            catch (Exception e) { UnityEngine.Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static string Argument(string key)
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : "";
        }

        static void Tick()
        {
            if (quitting || EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + .1;
            if (!Busy) { ReadRequest(); return; }
            try
            {
                string cancelFile = ProjectPath.Report("SpectatorRecording.cancel");
                if (File.Exists(cancelFile)) { File.Delete(cancelFile); job.cancel = true; }
                if (job.cancel)
                {
                    StopEncoder();
                    if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.isPaused = false; EditorApplication.ExitPlaymode(); }
                    else Finish("Cancelled. Completed videos and partial frames have been kept.");
                    return;
                }
                if (job.phase == "WaitingForEdit" && !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating) BeginIteration();
                else if (job.phase == "Starting" && EditorApplication.isPlaying && !EditorApplication.isCompiling && !job.runtimeStarted)
                {
                    job.enteredPlay = job.runtimeStarted = true; EditorApplication.isPaused = false; Save();
                    var host = new GameObject("Temporary spectator recording").AddComponent<SpectatorRecordingHost>();
                    if (!host) throw new InvalidOperationException("The temporary capture host could not start.");
                    Object.DontDestroyOnLoad(host);
                    var driver = new SpectatorRecordingDriver(); host.cleanup = driver.Restore; driver.StartCapture(host);
                }
                else if (job.phase == "Returning" && !EditorApplication.isPlayingOrWillChangePlaymode) AfterPlay();
                else if (job.phase == "Encoding") PollEncoder();
                else if ((job.phase == "Starting" || job.phase == "Recording" || job.phase == "Returning") && DateTime.UtcNow.Ticks - job.heartbeat > TimeSpan.FromMinutes(2).Ticks)
                    Fail("No recording progress for two minutes. Check that Game View is visible and Play Mode is not paused.");
            }
            catch (Exception e) { Fail(e.ToString()); }
        }

        static void ReadRequest()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            string request = Path.Combine(SpectatorRecordingPlan.ProjectRoot, "Logs", "Validation", "SpectatorRecording.request.json");
            if (!File.Exists(request)) return;
            try
            {
                string json = File.ReadAllText(request);
                File.Delete(request);
                var plan = JsonUtility.FromJson<SpectatorRecordingPlan>(json);
                Start(plan);
                File.WriteAllText(ProjectPath.Report("SpectatorRecording.request-result.txt"), Busy ? "Started: " + LastOutput : "Cancelled before recording started.");
            }
            catch (Exception e)
            {
                requestError = "Recording request failed: " + e.Message;
                File.WriteAllText(ProjectPath.Report("SpectatorRecording.request-result.txt"), requestError);
                UnityEngine.Debug.LogError(requestError);
            }
        }

        static void BeginIteration()
        {
            job.iteration++; job.frame = job.width = job.height = job.errors = 0;
            job.failure = ""; job.runtimeStarted = job.runFinished = job.enteredPlay = false;
            job.runFolder = SpectatorRecordingPlan.TimestampDirectory(job.batchFolder);
            Directory.CreateDirectory(Path.Combine(job.runFolder, "frames"));
            File.WriteAllText(Path.Combine(job.runFolder, "settings.json"), JsonUtility.ToJson(job.plan, true));
            File.WriteAllText(Path.Combine(job.runFolder, "targets.csv"), "frame,simulationSeconds,slot,name\n");
            job.phase = "Starting"; SetStatus("Starting match " + job.iteration + " of " + job.plan.iterations + ".");
            EditorApplication.LockReloadAssemblies(); AssetDatabase.DisallowAutoRefresh(); job.locked = true; Save();
            var catalog = SpectatorRecordingPlan.Catalog();
            string scene;
            try
            {
                scene = catalog.maps.First(m => m.id == job.plan.mapId).scenePath;
                if (string.IsNullOrEmpty(scene) || !AssetDatabase.LoadAssetAtPath<SceneAsset>(scene)) scene = ProjectPath.Find("Game.unity");
            }
            finally { Object.DestroyImmediate(catalog); }
            EditorSceneManager.OpenScene(scene);
            var viewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            if (viewType != null) EditorWindow.GetWindow(viewType).Focus();
            EditorApplication.EnterPlaymode();
        }

        static void PlayState(PlayModeStateChange state)
        {
            if (!Busy || job.phase == "WaitingForEdit") return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                job.enteredPlay = true; job.heartbeat = DateTime.UtcNow.Ticks;
                Application.logMessageReceived += Log; Save();
            }
            if (state == PlayModeStateChange.ExitingPlayMode && job.enteredPlay)
            {
                Application.logMessageReceived -= Log;
                if (!job.runFinished && !job.cancel) job.failure = "Play Mode was stopped before recording finished.";
                job.phase = "Returning"; Save();
            }
            if (state == PlayModeStateChange.EnteredEditMode && job.enteredPlay) { job.phase = "Returning"; Save(); }
        }

        static void Log(string message, string trace, LogType kind)
        {
            if (!Busy || string.IsNullOrEmpty(job.runFolder) || (kind != LogType.Error && kind != LogType.Exception && kind != LogType.Assert)) return;
            job.errors++;
            File.AppendAllText(Path.Combine(job.runFolder, "errors.log"), DateTime.Now.ToString("O") + " " + message + "\n" + trace + "\n");
        }

        public static void Progress(int frame, int width, int height)
        {
            job.frame = frame; job.width = width; job.height = height; job.phase = "Recording"; job.heartbeat = DateTime.UtcNow.Ticks;
            job.status = "Recording " + job.iteration + " of " + job.plan.iterations + ": " + frame + "/" + (job.plan.seconds * job.plan.fps) + " frames.";
            if (frame % job.plan.fps == 0) Save();
        }

        public static void EndCapture(string failure = "")
        {
            if (!Busy) return;
            job.failure = failure; job.runFinished = true; job.phase = "Returning"; job.heartbeat = DateTime.UtcNow.Ticks; Save();
            EditorApplication.ExitPlaymode();
        }

        static void AfterPlay()
        {
            Unlock();
            if (!string.IsNullOrEmpty(job.failure)) { CompleteIteration(false); return; }
            if (!job.runFinished || job.frame != job.plan.seconds * job.plan.fps) { job.failure = "Unexpected frame count."; CompleteIteration(false); return; }
            string log = Path.Combine(job.runFolder, "encoding.log"); File.WriteAllText(log, "");
            encoder = new Process { StartInfo = new ProcessStartInfo
            {
                FileName = job.plan.ffmpeg,
                Arguments = "-y -hide_banner -loglevel warning -framerate " + job.plan.fps + " -i " + Quote(Path.Combine(job.runFolder, "frames", "%06d.jpg")) + " -c:v libx264 -preset fast -crf 23 -pix_fmt yuv420p -vf \"pad=ceil(iw/2)*2:ceil(ih/2)*2\" " + Quote(Path.Combine(job.runFolder, "video.partial.mp4")),
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true
            }};
            encoder.ErrorDataReceived += (_, e) => { if (e.Data != null) { try { File.AppendAllText(log, e.Data + "\n"); } catch (IOException) { } } };
            encoder.Start(); encoder.BeginErrorReadLine();
            job.phase = "Encoding"; SetStatus("Encoding video " + job.iteration + " of " + job.plan.iterations + ".");
        }

        static void PollEncoder()
        {
            if (encoder == null) { job.failure = "An assembly reload interrupted the encoder; source frames were kept."; CompleteIteration(false); return; }
            if (!encoder.HasExited)
            {
                if (DateTime.UtcNow.Ticks - job.heartbeat > TimeSpan.FromMinutes(10).Ticks) { StopEncoder(); job.failure = "FFmpeg exceeded the ten-minute encoding limit."; CompleteIteration(false); }
                return;
            }
            encoder.WaitForExit(); int code = encoder.ExitCode; encoder.Dispose(); encoder = null;
            string partial = Path.Combine(job.runFolder, "video.partial.mp4");
            if (code != 0 || !File.Exists(partial) || new FileInfo(partial).Length == 0)
            { job.failure = "FFmpeg failed (exit " + code + "). See encoding.log; source frames were kept."; CompleteIteration(false); return; }
            File.Move(partial, Path.Combine(job.runFolder, Path.GetFileName(job.runFolder) + ".mp4"));
            if (!job.plan.keepFrames)
            {
                try { Directory.Delete(Path.Combine(job.runFolder, "frames"), true); }
                catch (IOException e) { File.AppendAllText(Path.Combine(job.runFolder, "encoding.log"), "Video saved; frame cleanup skipped: " + e.Message); }
                catch (UnauthorizedAccessException e) { File.AppendAllText(Path.Combine(job.runFolder, "encoding.log"), "Video saved; frame cleanup skipped: " + e.Message); }
            }
            CompleteIteration(true);
        }

        static void CompleteIteration(bool success)
        {
            if (!success) StopEncoder();
            if (success) job.completed++; else job.failed++;
            File.WriteAllText(Path.Combine(job.runFolder, "result.json"), JsonUtility.ToJson(job, true));
            File.AppendAllText(ProjectPath.Report("SpectatorRecording.txt"), (success ? "PASS " : "FAIL ") + job.runFolder + " " + job.failure + "\n");
            if (job.iteration >= job.plan.iterations || (!success && !job.plan.continueAfterFailure)) Finish("Finished: " + job.completed + " video(s), " + job.failed + " failed run(s).");
            else { job.phase = "WaitingForEdit"; SetStatus("Preparing next fresh match."); }
        }

        static void Fail(string reason)
        {
            if (!Busy) return;
            job.failure = reason; job.heartbeat = DateTime.UtcNow.Ticks;
            if (job.phase == "Encoding") { StopEncoder(); CompleteIteration(false); }
            else if (EditorApplication.isPlayingOrWillChangePlaymode) { job.runFinished = true; job.phase = "Returning"; Save(); EditorApplication.isPaused = false; EditorApplication.ExitPlaymode(); }
            else if (!string.IsNullOrEmpty(job.runFolder)) { Unlock(); CompleteIteration(false); }
            else Finish("Failed: " + reason);
        }

        public static void Cancel() { if (Busy) { job.cancel = true; SetStatus("Cancelling; keeping completed videos and partial frames."); } }

        static void Finish(string message)
        {
            StopEncoder(); Unlock();
            try
            {
                if (job.scenes != null && job.scenes.Length > 0)
                    EditorSceneManager.RestoreSceneManagerSetup(job.scenes.Select(s => new SceneSetup { path = s.path, isLoaded = s.loaded, isActive = s.active }).ToArray());
                ApplyChoices(job.previous); MatchSetup.FromLobby = job.previousFromLobby; MatchSetup.StartLocalSession = job.previousLocal;
            }
            catch (Exception e) { message += " Scene/settings restore failed: " + e.Message; job.failed++; }
            job.phase = "Finished"; job.status = message; Save();
            File.WriteAllText(Path.Combine(job.batchFolder, "batch.json"), JsonUtility.ToJson(job, true));
            if (job.commandLine) { EditorApplication.Exit(job.failed > 0 || job.cancel ? 1 : 0); return; }
            if (job.plan.notify)
            {
                EditorApplication.Beep();
                foreach (var window in Resources.FindObjectsOfTypeAll<SpectatorRecordingWindow>()) window.ShowNotification(new GUIContent(message), 15);
                EditorUtility.DisplayDialog("Deadly Jenga recordings", message + "\n\n" + job.batchFolder, "OK");
            }
        }

        public static void ApplyChoices(SpectatorRecordingPlan plan)
        {
            MatchChoices.MapId = plan.mapId; MatchChoices.ModeId = plan.modeId; MatchChoices.BotTarget = plan.match.population; MatchChoices.HandCount = plan.match.hands;
            MatchChoices.Rules = SpectatorRecordingPlan.Clone(plan.match.movement); MatchChoices.SameHumanTeam = plan.match.sameHumanTeam;
            MatchChoices.SurvivorPreset = plan.match.survivorPreset; MatchChoices.HandPreset = plan.match.handPreset;
        }
        static void SetStatus(string message) { job.status = message; job.heartbeat = DateTime.UtcNow.Ticks; Save(); }
        static void Save()
        {
            if (job.heartbeat == 0) job.heartbeat = DateTime.UtcNow.Ticks;
            SessionState.SetString(Key, JsonUtility.ToJson(job));
            if (!string.IsNullOrEmpty(job.batchFolder)) File.WriteAllText(Path.Combine(job.batchFolder, "status.txt"), job.status);
        }
        static void Unlock()
        {
            if (!job.locked) return;
            job.locked = false; AssetDatabase.AllowAutoRefresh(); EditorApplication.UnlockReloadAssemblies();
        }
        static void StopEncoder()
        {
            if (encoder == null) return;
            try { if (!encoder.HasExited) { encoder.Kill(); encoder.WaitForExit(1000); } } catch (InvalidOperationException) { }
            encoder.Dispose(); encoder = null;
        }
        static void CheckEncoder(string file)
        {
            using (var process = Process.Start(new ProcessStartInfo { FileName = file, Arguments = "-version", UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }))
            {
                process.BeginOutputReadLine(); process.BeginErrorReadLine();
                if (!process.WaitForExit(5000)) { process.Kill(); throw new InvalidOperationException("FFmpeg did not respond."); }
                if (process.ExitCode != 0) throw new InvalidOperationException("FFmpeg could not start.");
            }
        }
        static string Quote(string value)
        {
            var result = new System.Text.StringBuilder("\""); int slashes = 0;
            foreach (char c in value)
            {
                if (c == '\\') { slashes++; continue; }
                result.Append('\\', c == '"' ? slashes * 2 + 1 : slashes); result.Append(c); slashes = 0;
            }
            result.Append('\\', slashes * 2); result.Append('"'); return result.ToString();
        }
    }

    // Use the same temporary coroutine-host pattern as the project's other Play Mode checks.
    public sealed class SpectatorRecordingHost : MonoBehaviour
    {
        public Action cleanup;
        void OnDestroy() => cleanup?.Invoke();
    }
}
