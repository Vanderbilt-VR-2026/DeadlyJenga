using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // "Disaster PvP 1": survivors fight each other on the castle spire while meteors tear it apart.
    //   ...DisasterPvpMoment.RunScout    record a take, save it as a bundle, write a take report, render scout
    //                                    filmstrips (2 fps) from four cameras, and a motion blur colour check
    //   ...DisasterPvpMoment.RunRecord   record the planned take, save the bundle, render the preview
    //   ...DisasterPvpMoment.RunPreview  load the committed bundle, render every shot at 640x360
    //   ...DisasterPvpMoment.RunFinalA/B/C  load the bundle, render a third of the shots at 1920x1080
    // The cut, sound and music are assembled outside Unity (Docs/Development/TrailerStudio/attempts/disaster-pvp-1/tools).
    // Everything goes to Logs/ClaudeDisasterPvp/.
    [InitializeOnLoad]
    public static partial class DisasterPvpMoment
    {
        const int Version = 1;
        const int Fps = 30, LengthFrames = 450, Handle = 3;
        const float TapeAtZero = 1.0f;          // tape second shown at trailer frame 0
        static float Tape(float trailerSeconds) => TapeAtZero + trailerSeconds;
        static float TapeF(int trailerFrame) => TapeAtZero + trailerFrame / (float)Fps;

        // ---- Running ----
        const string Key = "DeadlyJenga.DisasterPvpMoment";
        const string SetId = "disaster-pvp-1";
        static int Step { get => SessionState.GetInt(Key, 0); set => SessionState.SetInt(Key, value); }
        static string Mode { get => SessionState.GetString(Key + ".Mode", "scout"); set => SessionState.SetString(Key + ".Mode", value); }
        static double Deadline { get => SessionState.GetFloat(Key + ".Deadline", 0); set => SessionState.SetFloat(Key + ".Deadline", (float)value); }
        static string Failure { get => SessionState.GetString(Key + ".Failure", ""); set => SessionState.SetString(Key + ".Failure", value); }

        static IEnumerator<int> routine;
        static int lastTick = -1;
        static readonly StringBuilder notes = new StringBuilder();

        public static string Folder => Path.Combine(StudioLibrary.ProjectRoot, "Logs", "ClaudeDisasterPvp");
        static string Bundle => Path.Combine(StudioLibrary.ProjectRoot, "Docs", "Development", "TrailerStudio", "attempts", "disaster-pvp-1", "bundle");

        static DisasterPvpMoment() { if (Step != 0) Hook(); }
        static void Hook() { EditorApplication.update -= Tick; EditorApplication.update += Tick; }

        public static void RunScout() => Begin("scout");
        public static void RunRecord() => Begin("record");
        public static void RunPreview() => Begin("preview");
        public static void RunFinalA() => Begin("finalA");
        public static void RunFinalB() => Begin("finalB");
        public static void RunFinalC() => Begin("finalC");

        static bool Recording => Mode == "scout" || Mode == "record";

        static void Begin(string mode)
        {
            if (Directory.Exists(Folder)) Directory.Delete(Folder, true);
            Directory.CreateDirectory(Folder);
            notes.Clear();
            string tapes = StudioLibrary.TapeFolder(SetId);
            if (Directory.Exists(tapes)) Directory.Delete(tapes, true);
            Mode = mode; Failure = ""; Step = 1;
            Deadline = EditorApplication.timeSinceStartup + 240;
            Hook();
        }

        static void Note(string line)
        {
            notes.AppendLine(line);
            Debug.Log("[DisasterPvpMoment] " + line);
            try { File.WriteAllText(Path.Combine(Folder, "notes.md"), notes.ToString()); } catch { }
        }

        static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                switch (Step)
                {
                    case 1:
                        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                        StudioLibrary.SetsFolderOverride = Path.Combine(Folder, "sets");
                        StudioRun.OpenStage(Recording ? NewSet() : LoadBundle());
                        Step = 2; Deadline = EditorApplication.timeSinceStartup + 570;
                        break;
                    case 2:
                        if (routine == null)
                        {
                            if (StudioRun.State == StudioState.Held) routine = Shoot(Mode);
                            else if (EditorApplication.timeSinceStartup > Deadline) throw new TimeoutException("The stage never opened: " + StudioRun.Status);
                            return;
                        }
                        if (EditorApplication.timeSinceStartup > Deadline) { StudioRender.Cancel(); throw new TimeoutException("Out of time inside the stage."); }
                        if (Time.frameCount == lastTick) return;
                        lastTick = Time.frameCount;
                        if (routine.MoveNext()) return;
                        routine = null;
                        Step = 3;
                        StudioRun.CloseStage();
                        break;
                    case 3:
                        if (EditorApplication.isPlayingOrWillChangePlaymode || StudioRun.State != StudioState.Closed) return;
                        Finish();
                        break;
                }
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Failure = error.Message;
                Note("FAIL stopped at step " + Step + ": " + error.Message);
                routine = null;
                if (StudioRun.State != StudioState.Closed && Step < 3) { Step = 3; StudioRun.CloseStage(); }
                else Finish();
            }
        }

        static void Finish()
        {
            bool failed = Failure.Length > 0;
            Step = 0;
            StudioLibrary.SetsFolderOverride = "";
            StudioRender.BlurMode = MotionBlurMode.CameraAndObjects;
            EditorApplication.update -= Tick;
            Note(failed ? $"FAIL disaster pvp {Mode}: {Failure}" : "PASS disaster pvp " + Mode + " finished, output in " + Folder);
            if (SessionState.GetString("DJWarm.id", "") != "")
            {
                Directory.CreateDirectory(Path.Combine("Logs", "Warm"));
                File.WriteAllText(Path.Combine("Logs", "Warm", "exit.txt"), failed ? "1" : "0");
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
                return;
            }
            bool startedForThis = Environment.GetCommandLineArgs().Any(a => a.Contains("DisasterPvpMoment.Run"));
            if (Application.isBatchMode || startedForThis) EditorApplication.Exit(failed ? 1 : 0);
        }

        static StudioSet NewSet()
        {
            var set = new StudioSet
            {
                id = SetId, title = "Disaster PvP 1", map = "castle", fps = Fps, length = TapeF(LengthFrames) + .6f, seed = 2027,
                handBot = false, notes = "Made by DisasterPvpMoment. Safe to delete."
            };
            StudioLibrary.Save(set);
            return set;
        }

        // The take from an earlier record run, committed to the repository: the Set, the tape and its sound.
        static StudioSet LoadBundle()
        {
            if (!Directory.Exists(Bundle)) throw new DirectoryNotFoundException("No recorded bundle at " + Bundle);
            var set = JsonUtility.FromJson<StudioSet>(File.ReadAllText(Path.Combine(Bundle, "set.json")));
            string tape = StudioLibrary.TapePath(SetId);
            Directory.CreateDirectory(Path.GetDirectoryName(tape));
            File.Copy(Path.Combine(Bundle, "main.djtape"), tape, true);
            string take = File.ReadAllText(Path.Combine(Bundle, "take.txt")).Trim();
            string audio = StudioRender.TakeAudioFile(SetId, take);
            Directory.CreateDirectory(Path.GetDirectoryName(audio));
            if (File.Exists(Path.Combine(Bundle, "tape.wav"))) File.Copy(Path.Combine(Bundle, "tape.wav"), audio, true);
            StudioLibrary.Save(set);
            return set;
        }

        static void SaveBundle(StudioSet set)
        {
            string bundle = Path.Combine(Folder, "bundle");
            Directory.CreateDirectory(bundle);
            File.WriteAllText(Path.Combine(bundle, "set.json"), JsonUtility.ToJson(set, true));
            File.Copy(StudioLibrary.TapePath(set.id), Path.Combine(bundle, "main.djtape"), true);
            var world = StudioRun.Stage.tape.FindType(WorldTrack.Id)?.ActiveTake;
            File.WriteAllText(Path.Combine(bundle, "take.txt"), world?.id ?? "");
            string audio = StudioRender.AudioFile(set.id);
            if (File.Exists(audio))
            {
                File.Copy(audio, Path.Combine(bundle, "tape.wav"), true);
                string stems = Path.Combine(Path.GetDirectoryName(audio), "stems");
                if (Directory.Exists(stems))
                {
                    Directory.CreateDirectory(Path.Combine(Folder, "stems"));
                    foreach (var s in Directory.GetFiles(stems)) File.Copy(s, Path.Combine(Folder, "stems", Path.GetFileName(s)), true);
                }
            }
            Note($"Bundle saved: tape {new FileInfo(StudioLibrary.TapePath(set.id)).Length / 1024} KB, sound {(File.Exists(audio) ? new FileInfo(audio).Length / 1024 : 0)} KB.");
        }

        static IEnumerable<int> UntilHeld()
        {
            float started = Time.realtimeSinceStartup;
            while (StudioRun.State != StudioState.Held)
            {
                if (StudioRun.State == StudioState.Closed) throw new InvalidOperationException("The stage closed: " + StudioRun.Status);
                if (Time.realtimeSinceStartup - started > 300) throw new TimeoutException("A run took more than five minutes: " + StudioRun.Status);
                yield return 0;
            }
        }

        static IEnumerable<int> UntilRendered() { while (StudioRender.Running) yield return 0; }

        static string V(Vector3 v) => v.ToString("F2");
        static Vector3 Flat(Vector3 v) { v.y = 0; return v.sqrMagnitude < 1e-6f ? Vector3.forward : v.normalized; }
        static float TopOf(Rigidbody body) => body.GetComponentsInChildren<Collider>().Where(c => c.enabled && !c.isTrigger).Select(c => c.bounds.max.y).DefaultIfEmpty(body.position.y).Max();
        static string P(StudioStage stage, Vector3 world) => StudioTargets.Point(stage.ToStage(world));
        static bool Clear(Vector3 at) => !Physics.CheckSphere(at, .3f, ~0, QueryTriggerInteraction.Ignore);
    }
}
