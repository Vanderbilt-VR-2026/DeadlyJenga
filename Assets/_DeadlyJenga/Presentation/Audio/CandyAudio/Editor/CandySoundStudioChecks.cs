using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeadlyJenga.CandyAudio.Editor
{
    // Explicit, short fixture. Never runs just because a project opens.
    public static class CandySoundStudioChecks
    {
        const string Pending = "CandySoundStudio.Checks.Pending";
        static string Report => Path.Combine("Logs", "SoundStudio", "unity-checks.txt");
        static double next;
        static int stage;
        static CandySoundStudioWindow window;
        static CandyAudioBank original, trial;
        static AudioClip clip;

        [MenuItem("Tools/Candy Audio/Run Sound Studio Checks")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Path.GetDirectoryName(Report));
            File.WriteAllText(Report, "Sound Studio focused checks\n" + DateTime.UtcNow.ToString("O") + "\n");
            try
            {
                EditChecks();
                SessionState.SetString(Pending, "runtime");
                SessionState.SetString(Pending + ".scene", UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Resume();
                EditorApplication.EnterPlaymode();
            }
            catch (Exception error) { Fail(error); }
        }

        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            File.AppendAllText(Report, "PASS " + message + "\n");
        }

        static void EditChecks()
        {
            var bank = Resources.Load<CandyAudioBank>("CandyAudio/Default");
            Check(bank, "Default bank loads");
            var entries = CandySoundCatalog.Build(bank);
            Check(entries.Select(e => e.id).Distinct().Count() == entries.Count, "Catalog stable IDs have no collisions");
            Check(Enum.GetValues(typeof(CandySound)).Cast<CandySound>().All(id => entries.Any(e => e.id == id)), "Every named cue is cataloged");
            Check(entries.All(e => !string.IsNullOrWhiteSpace(e.trigger) && !string.IsNullOrWhiteSpace(e.category)), "All entries have trigger explanations and categories");
            Check(entries.Any(e => e.category == "Disasters / Kraken") && entries.Any(e => e.category == "Combat / Dagger"), "Kraken and weapon-specific routes exist");
            var issues = CandyStudioAssets.Validate(bank);
            Check(issues.Count == 0, "Existing project bank validates: " + string.Join("; ", issues));
            File.AppendAllText(Report, "Catalog: " + entries.Count + " routes, " + entries.Sum(e => e.uses.Count) + " indexed source references\n");
            foreach (var id in new[] { CandySound.FootstepCandy, CandySound.KrakenRise, CandySound.FireCrackle,
                CandyDirectAudio.Home, CandyDirectAudio.Lobby, CandySignatureSounds.Id("Dagger/Shot/precision"),
                (CandySound)900, DeadlyJenga.NinjaArt.NinjaSounds.SoundId("Ninja gong") })
            {
                var entry = entries.Single(e => e.id == id);
                var cue = CandySoundCatalog.Authored(bank, id) ?? entry.fallback?.Invoke();
                Check(cue?.variants != null && cue.variants.Length > 0 && cue.variants.All(c => c && c.length > 0), "Preview data: " + entry.category + " / " + entry.title);
            }
            var binding = new CandyAudioBinding { trigger = CandyAudioTrigger.Emote, filter = " Phew ", minimumStrength = .5f };
            Check(CandyAudioTriggers.Matches(binding, CandyAudioTrigger.Emote, "phew", 1, true), "Event filter is trimmed and case-insensitive");
            Check(!CandyAudioTriggers.Matches(binding, CandyAudioTrigger.Emote, "Phew", 1, false), "Local-only excludes remote actors");
            Check(!CandyAudioTriggers.Matches(binding, CandyAudioTrigger.Emote, "Phew", .1f, true), "Strength threshold excludes weak events");
            Check(!CandyAudioTriggers.Matches(binding, CandyAudioTrigger.Emote, "Other", 1, true), "Name filter excludes other events");

            string parent = ProjectPath.FolderOf("CandySoundStudioChecks.cs").TrimEnd('/');
            string folder = parent + "/StudioCheck-" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
            AudioClip generated = null;
            try
            {
                var scratch = ScriptableObject.CreateInstance<CandyAudioBank>();
                generated = AudioClip.Create("Studio export check", 2205, 2, 22050, false);
                var samples = new float[4410];
                for (int i = 0; i < samples.Length; i++) samples[i] = Mathf.Sin(i * .04f) * .1f;
                generated.SetData(samples, 0);
                var cue = new CandyAudioCue { sound = (CandySound)2000000, variants = new[] { generated }, volume = .61f };
                scratch.cues = new[] { cue };
                scratch.bindings = new[] { new CandyAudioBinding { sound = cue.sound, trigger = CandyAudioTrigger.RoundStarted } };
                string path = folder + "/TestBank.asset"; AssetDatabase.CreateAsset(scratch, path);
                CandyStudioAssets.PersistGeneratedClips(scratch, cue);
                Check(AssetDatabase.Contains(cue.variants[0]) && cue.variants[0].channels == 2 && cue.variants[0].samples == 2205, "Generated stereo sound bakes and reimports without losing samples");
                Check(CandyStudioAssets.Validate(scratch).Count == 0, "Authored recording and trigger validate");
                Undo.RecordObject(scratch, "Studio check volume"); cue.volume = .27f; Undo.FlushUndoRecordObjects();
                Undo.PerformUndo(); Check(Mathf.Approximately(scratch.cues[0].volume, .61f), "Project edit supports Undo");
                Undo.PerformRedo(); Check(Mathf.Approximately(scratch.cues[0].volume, .27f), "Project edit supports Redo");
                EditorUtility.SetDirty(scratch); AssetDatabase.SaveAssetIfDirty(scratch);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var loaded = AssetDatabase.LoadAssetAtPath<CandyAudioBank>(path);
                Check(loaded.cues[0].variants[0] && loaded.bindings[0].trigger == CandyAudioTrigger.RoundStarted, "Saved bank retains its clip and event binding");
                Undo.ClearUndo(scratch);
            }
            finally { if (generated) UnityEngine.Object.DestroyImmediate(generated); AssetDatabase.DeleteAsset(folder); }
            CandySoundStudioWindow.Open();
            window = EditorWindow.GetWindow<CandySoundStudioWindow>();
            Check(window.rootVisualElement.Q<ListView>() != null && window.rootVisualElement.Q<ScrollView>() != null, "UI Toolkit browser constructs its list and detail panels");
        }

        [InitializeOnLoadMethod]
        static void Resume()
        {
            if (string.IsNullOrEmpty(SessionState.GetString(Pending, ""))) return;
            EditorApplication.playModeStateChanged -= ModeChanged;
            EditorApplication.playModeStateChanged += ModeChanged;
            if (EditorApplication.isPlaying)
            {
                stage = 0; next = EditorApplication.timeSinceStartup + 1;
                EditorApplication.update -= Tick; EditorApplication.update += Tick;
            }
        }
        static void ModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                stage = 0; next = EditorApplication.timeSinceStartup + 1;
                EditorApplication.update -= Tick; EditorApplication.update += Tick;
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.playModeStateChanged -= ModeChanged;
                SessionState.EraseString(Pending);
                var scene = SessionState.GetString(Pending + ".scene", "");
                if (!string.IsNullOrEmpty(scene) && File.Exists(scene)) EditorSceneManager.OpenScene(scene);
                CandySoundStudioWindow.Open();
                bool failed = File.ReadAllText(Report).Contains("\nFAIL ");
                File.AppendAllText(Report, (failed ? "FAILED" : "COMPLETE") + " · no project bank or gameplay scene changed\n");
            }
        }
        static void Tick()
        {
            if (EditorApplication.timeSinceStartup < next) return;
            try
            {
                if (stage == 0)
                {
                    var engine = CandyAudioEngine.Instance;
                    Check(engine, "Actual Play Mode boots the audio engine");
                    if (!UnityEngine.Object.FindFirstObjectByType<AudioListener>()) new GameObject("Studio fixture listener").AddComponent<AudioListener>();
                    original = engine.bank; trial = UnityEngine.Object.Instantiate(original);
                    clip = AudioClip.Create("Runtime studio trigger test", 4410, 1, 22050, false);
                    var cue = new CandyAudioCue { sound = (CandySound)2000001, variants = new[] { clip }, cooldown = 0 };
                    trial.cues = trial.cues.Concat(new[] { cue }).ToArray();
                    trial.bindings = new[] { new CandyAudioBinding { sound = cue.sound, trigger = CandyAudioTrigger.RoundStarted } };
                    engine.Configure(trial);
                    int played = engine.Played;
                    GameEvents.RaiseRoundStarted();
                    Check(engine.Played == played + 1, "Real Core round event plays the authored custom cue");
                    trial.bindings[0].enabled = false; played = engine.Played; GameEvents.RaiseRoundStarted();
                    Check(engine.Played == played, "Disabled event binding is silent");
                    trial.bindings[0].enabled = true;
                    var dispatcher = engine.GetComponent<CandyAudioTriggers>(); dispatcher.enabled = false;
                    GameEvents.RaiseRoundStarted(); Check(engine.Played == played, "Disabled dispatcher unsubscribes");
                    dispatcher.enabled = true; GameEvents.RaiseRoundStarted();
                    Check(engine.Played == played + 1, "Re-enabled dispatcher subscribes exactly once");
                    var signature = CandySignatureSounds.DescribeWeapon(new EquipmentActionFrame { kind = EquipmentKind.Dagger, action = EquipmentAction.Shot });
                    var mute = CandyAudioDefaults.Copy(signature); mute.enabled = false;
                    trial.cues = trial.cues.Concat(new[] { mute }).ToArray(); engine.Configure(trial);
                    Check(!engine.PlayCue(signature, Vector3.zero, local:true), "Signature playback respects authored mute instead of bypassing the bank");
                    engine.Configure(original); UnityEngine.Object.Destroy(trial); UnityEngine.Object.Destroy(clip);
                    stage = 1; next = EditorApplication.timeSinceStartup + .25;
                }
                else
                {
                    EditorApplication.update -= Tick;
                    EditorApplication.ExitPlaymode();
                }
            }
            catch (Exception error) { Fail(error); EditorApplication.update -= Tick; EditorApplication.ExitPlaymode(); }
        }
        static void Fail(Exception error)
        {
            if (EditorApplication.isPlaying)
            {
                var engine = CandyAudioEngine.Instance;
                if (engine && original) engine.Configure(original);
                if (trial) UnityEngine.Object.Destroy(trial);
                if (clip) UnityEngine.Object.Destroy(clip);
            }
            File.AppendAllText(Report, "FAIL " + error + "\n");
            Debug.LogException(error);
        }
    }
}
