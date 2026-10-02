using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace FlyingHand.Editor
{
    // Runs both integration probes across normal Play Mode domain reloads.
    [InitializeOnLoad]
    public static class HandDemoRootValidation
    {
        const string Key = "HandDemo.RootValidation.Stage";
        const string Report = "Temp/HandDemoRootValidation.txt";
        static double deadline;
        static HandDemoRootValidation()
        {
            EditorApplication.playModeStateChanged += OnPlayState;
            EditorApplication.update += Tick;
            deadline = EditorApplication.timeSinceStartup + 180;
        }

        [MenuItem("Tools/Flying Hand/Run Complete Validation")]
        public static void Begin()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the current scene first.");
            Directory.CreateDirectory("Temp");
            File.WriteAllText(Report, "Project: " + Path.GetFullPath(".") + "\nUnity: " + Application.unityVersion + "\n");
            EditorSceneManager.OpenScene(HandDemoAudit.ScenePath);
            HandDemoAudit.Validate();
            Append(HandDemoAudit.Result);
            SetStage(1);
        }

        // Explicit migration command; validation itself never modifies materials.
        public static void PrepareAndBegin()
        {
            if (GraphicsSettings.defaultRenderPipeline != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (!shader) throw new InvalidOperationException("The host URP Lit shader is unavailable.");
                foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/HandDemo/Materials" }))
                {
                    var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                    var color = material.GetColor("_Color");
                    material.shader = shader;
                    material.SetColor("_BaseColor", color);
                    material.SetFloat("_Smoothness", .22f);
                    material.SetFloat("_Metallic", 0);
                    EditorUtility.SetDirty(material);
                }
                AssetDatabase.SaveAssets();
            }
            Begin();
        }

        static void Append(string text) { File.AppendAllText(Report, text + "\n"); }
        static void SetStage(int stage)
        {
            SessionState.SetInt(Key, stage);
            deadline = EditorApplication.timeSinceStartup + 180;
        }
        static void OnPlayState(PlayModeStateChange state)
        {
            try
            {
                int stage = SessionState.GetInt(Key, 0);
                if (state == PlayModeStateChange.EnteredPlayMode && (stage == 2 || stage == 5))
                {
                    Application.runInBackground = true;
                    if (stage == 2) { FlyingHandDesktopSmokeTest.Begin(); SetStage(3); }
                    else { FlyingHandSmokeTest.Begin(); SetStage(6); }
                }
                if (state == PlayModeStateChange.EnteredEditMode && stage == 4)
                {
                    EditorSceneManager.OpenScene(HandDemoAudit.ScenePath);
                    SetStage(8);
                }
                if (state == PlayModeStateChange.EnteredEditMode && stage == 7)
                {
                    HandDemoAudit.Validate();
                    Append(HandDemoAudit.Result);
                    Append("Compile failed: " + EditorUtility.scriptCompilationFailed);
                    Append("Active target: " + EditorUserBuildSettings.activeBuildTarget);
                    Append("Scene dirty: " + EditorSceneManager.GetActiveScene().isDirty);
                    ValidateOpenXR();
                    Append("COMPLETE");
                    SetStage(0);
                }
            }
            catch (Exception e) { Fail(e); }
        }
        static void Tick()
        {
            int stage = SessionState.GetInt(Key, 0);
            if (stage == 0 || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Validation stage " + stage);
                if (stage == 1 || stage == 8)
                {
                    SetStage(stage == 1 ? 2 : 5);
                    EditorApplication.isPlaying = true;
                }
                if (Application.isPlaying) EditorApplication.QueuePlayerLoopUpdate();
                if (stage == 3 && FlyingHandDesktopSmokeTest.Result != "Running")
                {
                    Append("DESKTOP\n" + FlyingHandDesktopSmokeTest.Result);
                    SetStage(4); EditorApplication.isPlaying = false;
                }
                if (stage == 6 && FlyingHandSmokeTest.Result != "Running")
                {
                    Append("SIMULATED XR\n" + FlyingHandSmokeTest.Result);
                    var crate = GameObject.Find("Stack 1 Level 2");
                    Append("Crate tilt after collision: " + Vector3.Angle(crate.transform.up, Vector3.up));
                    Append("Running XR display: " + UnityEngine.Object.FindFirstObjectByType<FlyingHandController>().HasRunningXRDisplay());
                    SetStage(7); EditorApplication.isPlaying = false;
                }
            }
            catch (Exception e) { Fail(e); }
        }
        static void Fail(Exception e)
        {
            Append("FAIL " + e);
            SetStage(0);
            if (Application.isPlaying) EditorApplication.isPlaying = false;
            Debug.LogException(e);
        }

        static void ValidateOpenXR()
        {
            // Keep the portable editor assembly independent of the optional OpenXR package.
            var validation = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("UnityEditor.XR.OpenXR.OpenXRProjectValidation"))
                .FirstOrDefault(t => t != null);
            if (validation == null) { Append("OpenXR validation unavailable"); return; }
            var method = validation.GetMethod("GetCurrentValidationIssues");
            var issues = (System.Collections.IList)Activator.CreateInstance(method.GetParameters()[0].ParameterType);
            var selectedGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            try
            {
                // Some package rules consult the selected UI group rather than their argument.
                EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Android;
                method.Invoke(null, new object[] { issues, BuildTargetGroup.Android });
            }
            finally { EditorUserBuildSettings.selectedBuildTargetGroup = selectedGroup; }
            Append("Android OpenXR issues: " + issues.Count);
            foreach (var issue in issues)
            {
                var type = issue.GetType();
                Append(((bool)type.GetField("error").GetValue(issue) ? "FAIL " : "OPTIONAL ") + type.GetField("message").GetValue(issue));
            }
        }
    }
}
