using System;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.Modes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static class SpectatorRecordingChecks
    {
        const string Key = "SpectatorRecordingChecks";
        static string Report => ProjectPath.Report("SpectatorRecordingChecks.txt");

        static SpectatorRecordingChecks()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Key, false) || SessionState.GetInt(Key + "Stage", 0) != 3 || state != PlayModeStateChange.ExitingPlayMode) return;
                var job = SpectatorVideoCapture.Job;
                if (job == null || job.iteration != 1 || !job.runFinished || job.frame == 0) return;
                // Deliberately corrupt this test run's generated images to exercise real FFmpeg failure.
                foreach (var frame in Directory.GetFiles(Path.Combine(job.runFolder, "frames"), "*.jpg")) File.WriteAllText(frame, "deliberately invalid test image");
                File.AppendAllText(Report, "Injected corrupt frames into first encoder-failure test run.\n");
            };
        }

        [MenuItem("Tools/Deadly Jenga/Validation/Check Recording Queue")]
        public static void Run() => Begin(false);
        public static void RunBatch() => Begin(true);

        static void Begin(bool batch)
        {
            if (SpectatorVideoCapture.Busy || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode and finish any recording batch first.");
            File.WriteAllText(Report, "Recording queue integration checks\n");
            SessionState.SetBool(Key, true); SessionState.SetBool(Key + "Batch", batch);
            SessionState.SetBool(Key + "Snapshot", false);
            SessionState.SetString(Key + "Scenes", string.Join("|", EditorSceneManager.GetSceneManagerSetup().Select(s => s.path)));
            try
            {
                var plan = Plan();
                var invalid = SpectatorRecordingPlan.Clone(plan); invalid.iterations = 0;
                Reject(invalid, "Invalid iteration count is rejected before Play Mode");
                invalid = SpectatorRecordingPlan.Clone(plan); invalid.output = Application.dataPath;
                Reject(invalid, "Output inside Assets is rejected");
                invalid = SpectatorRecordingPlan.Clone(plan); invalid.ffmpeg = Path.Combine(plan.output, "missing-encoder.exe");
                Reject(invalid, "Missing selected encoder is reported before Play Mode");
                string root = Path.Combine(Path.GetDirectoryName(Report), "RecordingNameChecks");
                string first = SpectatorRecordingPlan.TimestampDirectory(root), second = SpectatorRecordingPlan.TimestampDirectory(root);
                Check(first != second && Directory.Exists(first) && Directory.Exists(second), "Second-resolution names never overwrite an existing run");
                SessionState.SetInt(Key + "Stage", 1);
                SpectatorVideoCapture.Start(plan);
            }
            catch (Exception e) { Failed(e); }
        }

        static SpectatorRecordingPlan Plan()
        {
            var plan = SpectatorRecordingPlan.Current();
            plan.output = Path.Combine(SpectatorRecordingPlan.DefaultOutput, "Validation");
            plan.mapId = "mode-ctf"; plan.modeId = "ctf";
            plan.match.rules = ModeSimulation.Defaults(GameModeKind.CaptureFlag);
            plan.match.rules.flagTarget = 7; plan.match.rules.countdownSeconds = 0;
            plan.match.rules.automaticDisasters = false;
            plan.match.population = 5; plan.match.hands = 0;
            plan.match.survivorPreset = plan.match.handPreset = 0;
            plan.iterations = 2; plan.seconds = 2; plan.fps = 5;
            plan.keepFrames = false; plan.notify = false;
            return plan;
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            try
            {
                int stage = SessionState.GetInt(Key + "Stage", 0);
                var job = SpectatorVideoCapture.Job;
                if (SpectatorVideoCapture.Busy)
                {
                    if (stage == 1 && job.phase == "Recording" && !SessionState.GetBool(Key + "Snapshot", false))
                    {
                        var current = SpectatorRecordingPlan.Current();
                        Check(current.mapId == "mode-ctf" && current.modeId == "ctf" && current.match.population == 5 && current.match.rules.flagTarget == 7, "Live settings import retains actual map, mode, population and custom rules");
                        SessionState.SetBool(Key + "Snapshot", true);
                    }
                    if (stage == 2 && job.phase == "Recording" && job.frame >= 2) SpectatorVideoCapture.Cancel();
                    return;
                }
                if (job == null || job.phase != "Finished" || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                string scenes = string.Join("|", EditorSceneManager.GetSceneManagerSetup().Select(s => s.path));
                Check(scenes == SessionState.GetString(Key + "Scenes", ""), "Original scene setup restored after stage " + stage);
                if (stage == 1)
                {
                    Check(job.completed == 2 && job.failed == 0, "Two fresh CTF matches encode successfully");
                    Check(Directory.GetFiles(job.batchFolder, "*.mp4", SearchOption.AllDirectories).Length == 2, "Two completed videos have timestamped filenames");
                    foreach (var folder in Directory.GetDirectories(job.batchFolder))
                    {
                        var rules = JsonUtility.FromJson<ModeRules>(File.ReadAllText(Path.Combine(folder, "actual-rules.json")));
                        Check(rules.mode == GameModeKind.CaptureFlag && rules.flagTarget == 7, "Requested custom CTF rules survive Play Mode reset");
                        Check(!Directory.Exists(Path.Combine(folder, "frames")), "Successful encoding cleans temporary frames");
                    }
                    SessionState.SetInt(Key + "Stage", 2);
                    var plan = Plan(); plan.seconds = 30;
                    SpectatorVideoCapture.Start(plan);
                }
                else if (stage == 2)
                {
                    Check(job.cancel && job.completed == 0 && job.frame < job.plan.seconds * job.plan.fps, "Cancellation stops recording before completion");
                    Check(Directory.GetFiles(Path.Combine(job.runFolder, "frames"), "*.jpg").Length > 0, "Cancellation preserves captured frames");
                    SessionState.SetInt(Key + "Stage", 3); SpectatorVideoCapture.Start(Plan());
                }
                else if (stage == 3)
                {
                    Check(job.failed == 1 && job.completed == 1, "Real encoder failure is reported and the next iteration succeeds");
                    Check(Directory.GetDirectories(job.batchFolder).Any(d => Directory.Exists(Path.Combine(d, "frames"))), "Encoder failure preserves diagnostic frames");
                    SessionState.SetBool(Key, false);
                    File.AppendAllText(Report, "COMPLETE: validation, two iterations, live settings, cancellation, encoder failure, continuation and scene restoration passed.\n");
                    if (SessionState.GetBool(Key + "Batch", false)) EditorApplication.Exit(0);
                    else EditorUtility.DisplayDialog("Recording checks passed", Report, "OK");
                }
            }
            catch (Exception e) { Failed(e); }
        }

        static void Reject(SpectatorRecordingPlan plan, string message)
        {
            try { plan.Validate(); } catch (ArgumentException) { Check(true, message); return; } catch (FileNotFoundException) { Check(true, message); return; }
            throw new Exception(message + ": invalid plan was accepted.");
        }
        static void Check(bool condition, string message)
        {
            File.AppendAllText(Report, (condition ? "PASS " : "FAIL ") + message + "\n");
            if (!condition) throw new Exception(message);
        }
        static void Failed(Exception e)
        {
            SessionState.SetBool(Key, false); SpectatorVideoCapture.Cancel();
            File.AppendAllText(Report, "FAILED " + e + "\n");
            if (SessionState.GetBool(Key + "Batch", false)) EditorApplication.Exit(1);
        }
    }
}
