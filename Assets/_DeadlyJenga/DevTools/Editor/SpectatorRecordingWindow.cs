using System;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.Modes;
using UnityEditor;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    public sealed class SpectatorRecordingDraft : ScriptableObject
    {
        public LobbySetup match;
    }

    public sealed class SpectatorRecordingWindow : EditorWindow
    {
        [SerializeField] SpectatorRecordingPlan plan;
        SpectatorRecordingDraft draft;
        SerializedObject serializedDraft;
        DeadlyJenga.CastlePrototype.IslandLobbyCatalog catalog;
        Vector2 scroll;
        string error = "";
        string displayedBatch;

        [MenuItem("Tools/Deadly Jenga/Record Matches")]
        public static void Open()
        {
            var window = GetWindow<SpectatorRecordingWindow>(true, "Record Matches", true);
            var main = EditorGUIUtility.GetMainWindowPosition();
            window.minSize = new Vector2(480, 620);
            float width=Mathf.Max(480,Mathf.Min(560,main.width-40));
            float height=Mathf.Max(620,Mathf.Min(680,main.height-40));
            window.position = new Rect(main.center.x-width*.5f,main.center.y-height*.5f,width,height);
            window.Focus();
        }

        void OnEnable()
        {
            try
            {
                catalog = SpectatorRecordingPlan.Catalog();
                if (plan == null) plan = SpectatorRecordingPlan.Current();
                SetDraft();
            }
            catch (Exception e) { error = e.Message; }
            EditorApplication.update += Repaint;
        }

        void OnDisable()
        {
            EditorApplication.update -= Repaint;
            if (draft) DestroyImmediate(draft);
            if (catalog) DestroyImmediate(catalog);
        }

        void SetDraft()
        {
            if (draft) DestroyImmediate(draft);
            draft = CreateInstance<SpectatorRecordingDraft>();
            draft.hideFlags = HideFlags.DontSave;
            draft.match = plan.match;
            serializedDraft = new SerializedObject(draft);
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Record matches", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Runs fresh local matches in this Editor. Starting stops the current local Play session. Scenes are restored afterward. The observer occupies one player slot and its body stays in the match. Videos have no audio.", MessageType.Info);
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            if (plan == null || !catalog || !draft)
            {
                if (GUILayout.Button("Reload settings")) { error = ""; OnDisable(); OnEnable(); }
                return;
            }
            bool busy = SpectatorVideoCapture.Busy;
            if (busy && displayedBatch != SpectatorVideoCapture.Job.batchFolder)
            {
                displayedBatch = SpectatorVideoCapture.Job.batchFolder;
                plan = SpectatorRecordingPlan.Clone(SpectatorVideoCapture.Job.plan);
                SetDraft();
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            using (new EditorGUI.DisabledScope(busy))
            {
                if (GUILayout.Button("Use current lobby / match settings"))
                {
                    try
                    {
                        var current = SpectatorRecordingPlan.Current();
                        plan.mapId = current.mapId; plan.modeId = current.modeId; plan.match = current.match;
                        SetDraft(); error = "";
                    }
                    catch (Exception e) { error = e.Message; }
                }
                var maps = catalog.maps.Where(SpectatorRecordingPlan.MapAvailable).ToArray();
                var modes = catalog.modes.Where(m => m != null && m.available).ToArray();
                int mapIndex = Array.FindIndex(maps, m => m.id == plan.mapId);
                int modeIndex = Array.FindIndex(modes, m => m.id == plan.modeId);
                int nextMap = EditorGUILayout.Popup("Map", mapIndex, maps.Select(m => m.title).ToArray());
                int nextMode = EditorGUILayout.Popup("Game mode", modeIndex, modes.Select(m => m.title).ToArray());
                if (nextMap >= 0) plan.mapId = maps[nextMap].id;
                if (nextMode >= 0 && nextMode != modeIndex)
                {
                    plan.modeId = modes[nextMode].id;
                    plan.match.rules = ModeSimulation.Defaults(MatchModeRuntime.ParseMode(plan.modeId));
                    SetDraft();
                }
                plan.match.population = EditorGUILayout.IntField("Player slots", plan.match.population);
                EditorGUILayout.LabelField("AI survivors", Math.Max(0, plan.match.population - 1).ToString());
                plan.match.hands = EditorGUILayout.IntField("AI hands", plan.match.hands);
                plan.match.survivorPreset = EditorGUILayout.IntField("Survivor preset index", plan.match.survivorPreset);
                plan.match.handPreset = EditorGUILayout.IntField("Hand preset index", plan.match.handPreset);
                plan.match.sameHumanTeam = EditorGUILayout.Toggle("Same human team", plan.match.sameHumanTeam);
                serializedDraft.Update();
                var match = serializedDraft.FindProperty("match");
                EditorGUILayout.PropertyField(match.FindPropertyRelative("rules"), new GUIContent("Game mode settings"), true);
                EditorGUILayout.PropertyField(match.FindPropertyRelative("movement"), new GUIContent("Movement and stamina"), true);
                EditorGUILayout.PropertyField(match.FindPropertyRelative("disasters"), new GUIContent("Disaster tuning"), true);
                serializedDraft.ApplyModifiedProperties();
                plan.match = draft.match;
                plan.iterations = EditorGUILayout.IntField("Iterations", plan.iterations);
                plan.seconds = EditorGUILayout.IntField("Seconds per video", plan.seconds);
                plan.fps = EditorGUILayout.IntField("Video FPS", plan.fps);
                plan.viewSeconds = EditorGUILayout.IntField(new GUIContent("Rotate views (seconds)","Cycle all four views through the population. Zero keeps fixed targets."),plan.viewSeconds);
                plan.keepFrames = EditorGUILayout.Toggle("Keep source frames", plan.keepFrames);
                plan.continueAfterFailure = EditorGUILayout.Toggle("Continue after failures", plan.continueAfterFailure);
                plan.notify = EditorGUILayout.Toggle(new GUIContent("Completion alert", "Show a completion popup and play the system sound."), plan.notify);
                plan.output = EditorGUILayout.TextField("Output folder", plan.output);
                if (GUILayout.Button("Choose output folder"))
                {
                    string folder = EditorUtility.OpenFolderPanel("Recordings folder", plan.output, "");
                    if (!string.IsNullOrEmpty(folder)) plan.output = folder;
                }
                plan.ffmpeg = EditorGUILayout.TextField("FFmpeg executable", plan.ffmpeg);
                if (GUILayout.Button("Find FFmpeg"))
                {
                    string path = EditorUtility.OpenFilePanel("FFmpeg executable", "", "");
                    if (!string.IsNullOrEmpty(path)) { plan.ffmpeg = path; EditorPrefs.SetString(SpectatorRecordingPlan.EncoderPreference, path); }
                }
                if (GUILayout.Button("Start recording batch", GUILayout.Height(32)))
                {
                    try { SpectatorVideoCapture.Start(plan); error = ""; }
                    catch (Exception e) { error = e.Message; }
                }
            }
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(SpectatorVideoCapture.Status, MessageType.None);
            if (busy && GUILayout.Button("Cancel batch")) SpectatorVideoCapture.Cancel();
            string output = SpectatorVideoCapture.LastOutput;
            if (Directory.Exists(output) && GUILayout.Button("Open recordings folder")) EditorUtility.RevealInFinder(output);
            EditorGUILayout.EndScrollView();
        }
    }
}
