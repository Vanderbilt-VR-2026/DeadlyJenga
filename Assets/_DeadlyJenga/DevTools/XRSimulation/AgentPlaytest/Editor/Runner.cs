using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AgentPlaytest
{
    [InitializeOnLoad] public static class Runner
    {
        const string Key = "AgentPlaytest.Active";
        [Serializable] sealed class Saved
        {
            public PlaytestPlan plan;
            public SceneSetup[] scenes;
            public bool batch, background;
        }
        static Saved saved;
        static PlaytestReport report;
        static Stack<IEnumerator> stack;
        static double began, nextTick, startupReady;
        static string pendingBatch;
        static bool finishing, reloadLocked, batchInvocation;
        static int oldTargetRate, oldVsync;
        static float oldTimeScale, oldVolume;
        public static string Project => Path.GetFullPath(Path.Combine(Application.dataPath, "..")).TrimEnd(Path.DirectorySeparatorChar);
        static string Inbox => Path.Combine(Project, "Library", "AgentPlaytest", "inbox.json");
        static Runner()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += State;
            EditorApplication.quitting += Quitting;
            var json = SessionState.GetString(Key, "");
            if (!string.IsNullOrEmpty(json)) saved = JsonUtility.FromJson<Saved>(json);
        }
        public static void RunBatch()
        {
            try
            {
                batchInvocation = true;
                var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-apt-plan");
                if (i < 0 || i + 1 == args.Length) throw new ArgumentException("Pass -apt-plan <absolute JSON file>.");
                pendingBatch = args[i + 1]; startupReady = EditorApplication.timeSinceStartup + 2;
            }
            catch (Exception e) { Debug.LogError("AgentPlaytest startup: " + e.Message); EditorApplication.Exit(2); }
        }
        public static void StartFromFile(string path)
        {
            var plan = JsonUtility.FromJson<PlaytestPlan>(File.ReadAllText(path));
            Validate(plan);
            if (saved != null || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Editor is busy. Existing play sessions are left untouched.");
            var scenes = EditorSceneManager.GetSceneManagerSetup();
            for (int n = 0; n < SceneManager.sceneCount; n++)
            {
                var scene = SceneManager.GetSceneAt(n);
                if (scene.isDirty || (!Application.isBatchMode && string.IsNullOrEmpty(scene.path) && scene.rootCount > 0))
                    throw new InvalidOperationException("Save or close unsaved scenes before testing.");
            }
            if (Directory.Exists(plan.output) && Directory.EnumerateFileSystemEntries(plan.output).Any())
                throw new IOException("Output folder must be new or empty; existing evidence is preserved.");
            Directory.CreateDirectory(plan.output);
            saved = new Saved { plan = plan, scenes = scenes, batch = batchInvocation, background = Application.runInBackground };
            SessionState.SetString(Key, JsonUtility.ToJson(saved));
            try
            {
                if (string.IsNullOrEmpty(plan.scene)) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                else EditorSceneManager.OpenScene(plan.scene, OpenSceneMode.Single);
                File.WriteAllText(Path.Combine(plan.output, "plan.json"), JsonUtility.ToJson(plan, true));
                File.WriteAllText(Path.Combine(plan.output, "status.txt"), "starting");
                EditorApplication.EnterPlaymode();
            }
            catch { saved = null; SessionState.EraseString(Key); throw; }
        }
        static bool SamePath(string a, string b)
        {
            var comparison = Application.platform == RuntimePlatform.WindowsEditor ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return string.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar), comparison);
        }
        static void Validate(PlaytestPlan p)
        {
            if (p == null || p.schemaVersion != 1 || string.IsNullOrWhiteSpace(p.id)) throw new ArgumentException("Invalid plan/schema.");
            if (string.IsNullOrEmpty(p.project) || !SamePath(p.project, Project)) throw new InvalidOperationException("Exact project path mismatch.");
            int pid = System.Diagnostics.Process.GetCurrentProcess().Id;
            if (p.targetPid != pid && !(p.targetPid == 0 && Application.isBatchMode)) throw new InvalidOperationException("Editor PID mismatch.");
            if (p.width < 64 || p.width > 4096 || p.height < 64 || p.height > 4096 || p.timeoutSeconds < 1 || p.timeoutSeconds > 3600)
                throw new ArgumentException("Invalid resolution/timeout.");
            if (string.IsNullOrEmpty(p.output) || !Path.IsPathRooted(p.output)) throw new ArgumentException("Output must be absolute.");
            p.output = Path.GetFullPath(p.output);
            foreach (string folder in new[] {"Assets", "Packages", "ProjectSettings", "Library"})
            {
                string root = Path.Combine(Project, folder);
                if (SamePath(p.output, root) || p.output.StartsWith(root + Path.DirectorySeparatorChar, Application.platform == RuntimePlatform.WindowsEditor ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                    throw new ArgumentException("Evidence must be outside Unity source and cache folders.");
            }
            if (!string.IsNullOrEmpty(p.scene))
            {
                string absolute = Path.GetFullPath(Path.Combine(Project, p.scene));
                if (!absolute.StartsWith(Path.Combine(Project, "Assets") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(absolute) || !absolute.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Scene must be an existing project Assets scene.");
            }
        }
        static void State(PlayModeStateChange state)
        {
            if (saved == null) return;
            if (state == PlayModeStateChange.EnteredPlayMode) Begin();
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                if (report == null) { report = NewReport(); report.status = "failed"; report.failure = "Play Mode ended before the scenario initialized."; }
                else if (!finishing) { report.status = "failed"; report.failure = "Play Mode ended externally."; }
                report.editModeEnteredUtc = DateTime.UtcNow.ToString("O");
                report.finishToEditSeconds = began > 0 && !string.IsNullOrEmpty(report.scenarioFinishedUtc) ? EditorApplication.timeSinceStartup - began - report.scenarioElapsedSeconds : -1;
                Complete();
            }
        }
        static PlaytestReport NewReport() => new PlaytestReport { id = saved.plan.id, project = Project, scenario = saved.plan.scenario,
            unityVersion = Application.unityVersion, pid = System.Diagnostics.Process.GetCurrentProcess().Id, startedUtc = DateTime.UtcNow.ToString("O"), firstFrame = Time.frameCount };
        static void Begin()
        {
            if (report != null) return;
            report = NewReport(); began = EditorApplication.timeSinceStartup; finishing = false;
            oldTimeScale = Time.timeScale; oldVolume = AudioListener.volume; oldTargetRate = Application.targetFrameRate; oldVsync = QualitySettings.vSyncCount;
            Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
            Time.timeScale = 1; if (saved.plan.mute) AudioListener.volume = 0; Application.runInBackground = true;
            Application.logMessageReceived += Log;
            EditorApplication.LockReloadAssemblies(); AssetDatabase.DisallowAutoRefresh(); reloadLocked = true;
            try
            {
                Type type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(saved.plan.scenario, false)).FirstOrDefault(t => t != null);
                if (type == null || !typeof(IPlaytestScenario).IsAssignableFrom(type)) throw new ArgumentException("Scenario must implement AgentPlaytest.IPlaytestScenario: " + saved.plan.scenario);
                var scenario = (IPlaytestScenario)Activator.CreateInstance(type);
                stack = new Stack<IEnumerator>(); stack.Push(scenario.Run(new PlaytestContext(saved.plan, report)));
                File.WriteAllText(Path.Combine(saved.plan.output, "status.txt"), "running");
            }
            catch (Exception e) { Finish("failed", e.ToString()); }
        }
        static void Tick()
        {
            if (EditorApplication.timeSinceStartup < nextTick) return;
            nextTick = EditorApplication.timeSinceStartup + .008;
            if (saved == null)
            {
                if (pendingBatch != null)
                {
                    if (EditorApplication.isCompiling || EditorApplication.isUpdating) { startupReady = EditorApplication.timeSinceStartup + 2; return; }
                    if (EditorApplication.timeSinceStartup < startupReady) return;
                    string path = pendingBatch; pendingBatch = null;
                    try { StartFromFile(path); } catch (Exception e) { Debug.LogError("AgentPlaytest startup: " + e); EditorApplication.Exit(2); }
                    return;
                }
                if (!File.Exists(Inbox) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                PlaytestPlan requested = null;
                try
                {
                    requested = JsonUtility.FromJson<PlaytestPlan>(File.ReadAllText(Inbox));
                    if (requested.targetPid != System.Diagnostics.Process.GetCurrentProcess().Id) return;
                    string consumed = Inbox + ".consumed"; if (File.Exists(consumed)) return;
                    File.Move(Inbox, consumed);
                    try { StartFromFile(consumed); }
                    finally { File.Delete(consumed); }
                }
                catch (Exception e)
                {
                    File.WriteAllText(Path.Combine(Path.GetDirectoryName(Inbox), "rejected.json"), JsonUtility.ToJson(new Rejection { id = requested?.id, message = e.Message }, true));
                }
                return;
            }
            if (finishing || !EditorApplication.isPlaying) return;
            if (report == null) Begin();
            if (finishing) return;
            EditorApplication.QueuePlayerLoopUpdate();
            if (File.Exists(Path.Combine(saved.plan.output, "cancel.request"))) { Finish("cancelled", "Cancellation requested."); return; }
            if (EditorApplication.timeSinceStartup - began > saved.plan.timeoutSeconds) { Finish("timed_out", "Scenario exceeded its wall-clock limit."); return; }
            try
            {
                while (stack.Count > 0)
                {
                    var top = stack.Peek();
                    if (!top.MoveNext()) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                    if (top.Current is IEnumerator nested) { stack.Push(nested); continue; }
                    if (top.Current is AsyncOperation operation) { stack.Push(WaitOperation(operation)); continue; }
                    if (top.Current != null) throw new NotSupportedException("Yield null, an IEnumerator, or AsyncOperation; use context.WaitSeconds/WaitFrames.");
                    return;
                }
                Finish(report.errors.Count == 0 ? "passed" : "failed", report.errors.Count == 0 ? "" : "Runtime errors were logged.");
            }
            catch (Exception e) { Finish("failed", e.ToString()); }
        }
        [Serializable] sealed class Rejection { public string id, message; }
        static IEnumerator WaitOperation(AsyncOperation op) { while (!op.isDone) yield return null; }
        static void Log(string message, string trace, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && report != null && report.errors.Count < 50)
                report.errors.Add(message + "\n" + trace);
        }
        static void DisposeStack()
        {
            if (stack == null) return;
            while (stack.Count > 0) try { (stack.Pop() as IDisposable)?.Dispose(); } catch (Exception e) { report.errors.Add("Cleanup: " + e); report.status = "failed"; }
            stack = null;
        }
        static void Finish(string status, string failure)
        {
            if (finishing) return;
            finishing = true; report.status = status; report.failure = failure; report.lastFrame = Time.frameCount;
            report.scenarioFinishedUtc = DateTime.UtcNow.ToString("O");
            report.scenarioElapsedSeconds = began > 0 ? EditorApplication.timeSinceStartup - began : 0;
            DisposeStack(); Time.timeScale = oldTimeScale; AudioListener.volume = oldVolume; Application.targetFrameRate = oldTargetRate; QualitySettings.vSyncCount = oldVsync;
            Application.logMessageReceived -= Log;
            EditorApplication.ExitPlaymode();
        }
        static void Complete()
        {
            bool batch = saved.batch; string output = saved.plan.output;
            double restoreBegan = EditorApplication.timeSinceStartup;
            try
            {
                DisposeStack(); Application.logMessageReceived -= Log;
                Application.runInBackground = saved.background;
                Time.timeScale = oldTimeScale; AudioListener.volume = oldVolume; Application.targetFrameRate = oldTargetRate; QualitySettings.vSyncCount = oldVsync;
                if (saved.scenes != null && saved.scenes.Any(s => s.isLoaded && !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(saved.scenes);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            catch (Exception e) { report.status = "failed"; report.errors.Add("Restore: " + e); }
            finally
            {
                report.restoreFinishedUtc = DateTime.UtcNow.ToString("O");
                report.sceneRestoreSeconds = EditorApplication.timeSinceStartup - restoreBegan;
                if (reloadLocked) { reloadLocked = false; AssetDatabase.AllowAutoRefresh(); EditorApplication.UnlockReloadAssemblies(); }
                report.finishedUtc = DateTime.UtcNow.ToString("O"); report.elapsedSeconds = began > 0 ? EditorApplication.timeSinceStartup - began : 0;
                File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
                File.WriteAllText(Path.Combine(output, "status.txt"), report.status);
                int code = report.status == "passed" ? 0 : 1;
                saved = null; report = null; stack = null; finishing = false; began = 0; SessionState.EraseString(Key);
                if (batch) EditorApplication.Exit(code);
            }
        }
        static void Quitting()
        {
            if (saved == null) return;
            if (report == null) report = NewReport();
            report.status = "interrupted"; report.failure = "Editor quit before completion."; report.finishedUtc = DateTime.UtcNow.ToString("O");
            File.WriteAllText(Path.Combine(saved.plan.output, "report.json"), JsonUtility.ToJson(report, true));
            SessionState.EraseString(Key);
        }
    }
}
