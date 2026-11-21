using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // "Disaster PvP 2": a gloved brawl on the castle summit while meteors rain in, seen close and through the hero's eyes.
    // Every survivor is driven through the game's own controls (StudioPerformer) in ONE live recording, so every hit is real.
    //   ...DisasterPvp2.RunRehearse   record the take, save a bundle in the job folder, take report, motion table, 480x270 preview of every shot
    //   ...DisasterPvp2.RunPreview    load the committed bundle, report, motion table, 640x360 render of every shot
    //   ...DisasterPvp2.RunFinalA/B/C load the committed bundle, render a third of the shots at 1920x1080
    // The cut, sound, music and titles are assembled outside Unity (Docs/Development/TrailerStudio/attempts/disaster-pvp-2/tools).
    // Output: Logs/ClaudeDisasterPvp2/<mode>/.
    [InitializeOnLoad]
    public static partial class DisasterPvp2
    {
        const int Fps = 30, LengthFrames = 450, Handle = 3;
        const float TapeAtZero = 1.0f;          // tape second shown at trailer frame 0 (before any per-shot shift)
        static float TapeF(float trailerFrame) => TapeAtZero + trailerFrame / Fps;
        static int TapeFrame(float trailerFrame) => Mathf.RoundToInt(TapeF(trailerFrame) * Fps);

        const string Key = "DeadlyJenga.DisasterPvp2";
        const string SetId = "disaster-pvp-2";
        static int Step { get => SessionState.GetInt(Key, 0); set => SessionState.SetInt(Key, value); }
        static string Mode { get => SessionState.GetString(Key + ".Mode", "rehearse"); set => SessionState.SetString(Key + ".Mode", value); }
        static double Deadline { get => SessionState.GetFloat(Key + ".Deadline", 0); set => SessionState.SetFloat(Key + ".Deadline", (float)value); }
        static string Failure { get => SessionState.GetString(Key + ".Failure", ""); set => SessionState.SetString(Key + ".Failure", value); }

        static IEnumerator<int> routine;
        static int lastTick = -1;
        static readonly StringBuilder notes = new StringBuilder();

        public static string Folder => Path.Combine(StudioLibrary.ProjectRoot, "Logs", "ClaudeDisasterPvp2", Mode);
        static string Bundle => Path.Combine(StudioLibrary.ProjectRoot, "Docs", "Development", "TrailerStudio", "attempts", "disaster-pvp-2", "bundle");

        static DisasterPvp2() { if (Step != 0) Hook(); }
        static void Hook() { EditorApplication.update -= Tick; EditorApplication.update += Tick; }

        public static void RunRehearse() => Begin("rehearse");
        public static void RunPreview() => Begin("preview");
        public static void RunFinalA() => Begin("finalA");
        public static void RunFinalB() => Begin("finalB");
        public static void RunFinalC() => Begin("finalC");

        static bool Recording => Mode == "rehearse";

        static void Begin(string mode)
        {
            Mode = mode;
            if (Directory.Exists(Folder)) Directory.Delete(Folder, true);
            Directory.CreateDirectory(Folder);
            notes.Clear();
            string tapes = StudioLibrary.TapeFolder(SetId);
            if (Directory.Exists(tapes)) Directory.Delete(tapes, true);
            Failure = ""; Step = 1;
            Deadline = EditorApplication.timeSinceStartup + 240;
            Hook();
        }

        static void Note(string line)
        {
            notes.AppendLine(line);
            Debug.Log("[DisasterPvp2] " + line);
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
                            if (StudioRun.State == StudioState.Held) routine = Shoot().GetEnumerator();
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
                Note("FAIL stopped at step " + Step + ": " + error);
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
            EditorApplication.update -= Tick;
            Note(failed ? $"FAIL disaster pvp 2 {Mode}: {Failure}" : "PASS disaster pvp 2 " + Mode + " finished, output in " + Folder);
            if (SessionState.GetString("DJWarm.id", "") != "")
            {
                Directory.CreateDirectory(Path.Combine("Logs", "Warm"));
                File.WriteAllText(Path.Combine("Logs", "Warm", "exit.txt"), failed ? "1" : "0");
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
                return;
            }
            bool startedForThis = Environment.GetCommandLineArgs().Any(a => a.Contains("DisasterPvp2.Run"));
            if (Application.isBatchMode || startedForThis) EditorApplication.Exit(failed ? 1 : 0);
        }

        static StudioSet NewSet()
        {
            var set = new StudioSet
            {
                id = SetId, title = "Disaster PvP 2", map = "castle", fps = Fps, length = TapeF(LengthFrames) + .8f, seed = 2031,
                handBot = false, notes = "Made by DisasterPvp2. Safe to delete."
            };
            StudioLibrary.Save(set);
            return set;
        }

        // The committed take: the Set, the tape, its sound and the cue log of the recording.
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
            cueLog = File.Exists(Path.Combine(Bundle, "cuelog.txt")) ? File.ReadAllLines(Path.Combine(Bundle, "cuelog.txt")).ToList() : new List<string>();
            StudioLibrary.Save(set);
            return set;
        }

        static List<string> cueLog = new List<string>();

        static void SaveBundle(StudioSet set)
        {
            string bundle = Path.Combine(Folder, "bundle");
            Directory.CreateDirectory(bundle);
            File.WriteAllText(Path.Combine(bundle, "set.json"), JsonUtility.ToJson(set, true));
            File.Copy(StudioLibrary.TapePath(set.id), Path.Combine(bundle, "main.djtape"), true);
            var world = StudioRun.Stage.tape.FindType(WorldTrack.Id)?.ActiveTake;
            File.WriteAllText(Path.Combine(bundle, "take.txt"), world?.id ?? "");
            File.WriteAllLines(Path.Combine(bundle, "cuelog.txt"), cueLog);
            string audio = StudioRender.AudioFile(set.id);
            if (File.Exists(audio)) File.Copy(audio, Path.Combine(bundle, "tape.wav"), true);
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

        static IEnumerable<int> Shoot()
        {
            var stage = StudioRun.Stage;
            yield return 0; yield return 0;
            Note($"# Disaster PvP 2, {Mode}, {DateTime.Now:yyyy-MM-dd HH:mm}\n");
            var plan = MakePlan(stage);
            if (Recording)
            {
                Direct(stage, plan);
                yield return 0;
                var clock = System.Diagnostics.Stopwatch.StartNew();
                StudioRun.RecordWholeSet();
                foreach (int wait in UntilHeld()) yield return wait;
                StudioRun.SaveTape();
                cueLog = StudioRun.CueLog.ToList();
                Note($"Recorded in {clock.Elapsed.TotalSeconds:0} s. {StudioRun.Status}");
                Note("Cue log:\n- " + string.Join("\n- ", cueLog));
                SaveBundle(stage.set);
            }
            else Note("Loaded the committed bundle; cue log has " + cueLog.Count + " lines.");

            var events = FindEvents(stage, plan);
            try { Report(stage, plan, events); } catch (Exception error) { Note("Take report failed: " + error); }
            foreach (int w in RenderShots(stage, plan, events, Mode)) yield return w;
        }

        static string V(Vector3 v) => v.ToString("F2");
        static Vector3 Flat(Vector3 v) { v.y = 0; return v.sqrMagnitude < 1e-6f ? Vector3.forward : v.normalized; }
        static Vector3 Flat0(Vector3 v) { v.y = 0; return v; }
        static string P(StudioStage stage, Vector3 world) => StudioTargets.Point(stage.ToStage(world));
        static int Mask => (1 << Layers.Block) | (1 << Layers.Surface) | 1;
        static bool Clear(Vector3 at, float r = .35f) => !Physics.CheckSphere(at, r, Mask, QueryTriggerInteraction.Ignore);
        static bool Sees(Vector3 from, Vector3 to) => !Physics.Linecast(from, to, (1 << Layers.Block) | (1 << Layers.Surface), QueryTriggerInteraction.Ignore);
        static float TopOf(Rigidbody body) => body.GetComponentsInChildren<Collider>().Where(c => c.enabled && !c.isTrigger).Select(c => c.bounds.max.y).DefaultIfEmpty(body.position.y).Max();
    }
}
