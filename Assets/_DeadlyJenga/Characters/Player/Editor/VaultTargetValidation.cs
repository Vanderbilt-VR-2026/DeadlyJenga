using System;
using System.IO;
using System.Text;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.CastlePrototype.Editor
{
    [InitializeOnLoad]
    public static class VaultTargetValidation
    {
        const string Key = "CandyVaultValidation";
        static double started;
        static bool errors;
        static VaultTargetValidation()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
                started = EditorApplication.timeSinceStartup;
                Application.logMessageReceived += OnLog;
                EditorApplication.update += Wait;
            };
        }
        static void OnLog(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors = true;
        }
        public static void RunBatch()
        {
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }
        static void Wait()
        {
            if (EditorApplication.timeSinceStartup - started < 5) return;
            EditorApplication.update -= Wait;
            var report = new StringBuilder(); int failures = 0;
            void Check(string label, bool pass) { report.AppendLine((pass ? "PASS " : "FAIL ") + label); if (!pass) failures++; }
            var fixture = new GameObject("Temporary vault target checks");
            try
            {
                Check("Game enters Play Mode without runtime errors", !errors);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPath.Find("CastleSurvivor.prefab"));
                var instance = Object.Instantiate(prefab, fixture.transform);
                var motor = instance.GetComponent<CastleSurvivor>(); motor.externallyDriven = true;
                motor.RefreshMode(); motor.viewCamera.transform.rotation = Quaternion.identity;
                IVaultTargetProvider provider = motor;
                Vector3 origin = new Vector3(1000, 1000, 1000);
                GameObject Box(Vector3 offset, Vector3 scale)
                {
                    var box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.transform.SetParent(fixture.transform);
                    box.transform.position = origin + offset; box.transform.localScale = scale; return box;
                }
                motor.Teleport(origin + new Vector3(0, 1.05f, .45f), Quaternion.identity);
                Check("Empty air has no vault target", !provider.TryGetVaultTarget(out _, out _, out _));
                var ledge = Box(new Vector3(0, 1, 2), new Vector3(6, 2, 2)); Physics.SyncTransforms();
                Check("Valid ledge supplies a surface and landing", provider.TryGetVaultTarget(out var surface, out _, out var landing)
                    && surface == ledge.GetComponent<Collider>() && landing.y > origin.y + 2);
                motor.InputSuspended = true;
                Check("Suspended input suppresses target", !provider.TryGetVaultTarget(out _, out _, out _)); motor.InputSuspended = false;
                var ceiling = Box(new Vector3(0, 2 + motor.Capsule.height * .65f, 2), new Vector3(6, .25f, 2)); Physics.SyncTransforms();
                Check("Blocked landing suppresses target", !provider.TryGetVaultTarget(out _, out _, out _)); ceiling.SetActive(false);
                Check("The advertised target can actually start a vault", motor.TryStartVault());
                Check("Active vault suppresses another hint", !provider.TryGetVaultTarget(out _, out _, out _));
                motor.Teleport(origin + new Vector3(0, 1.05f, .45f), Quaternion.identity);
                ledge.transform.position = origin + new Vector3(.36f, 1, 2); ledge.transform.localScale = new Vector3(.25f, 2, 2); Physics.SyncTransforms();
                motor.vaultDetectionWidth = 0;
                Check("Centre-only query misses an off-centre ledge", !motor.TryFindLedge(out _, out _));
                motor.vaultDetectionWidth = 1;
                Check("Wider query finds the off-centre ledge", motor.TryFindLedge(out _, out _));
                motor.vaultDetectionWidth = 0;
                ledge.transform.position = origin + new Vector3(0, 1, 2); ledge.transform.localScale = new Vector3(6, 2, 2); Physics.SyncTransforms();
                motor.vaultLandingDepth = .08f; motor.TryFindLedge(out _, out var near);
                motor.vaultLandingDepth = .3f; motor.TryFindLedge(out _, out var deep);
                Check("Landing depth adjusts the accepted destination", deep.z > near.z + .2f);
                var shader = Shader.Find("DeadlyJenga/Candy Vault Glow");
                Check("Candy glow shader imports without errors", shader && !ShaderUtil.ShaderHasError(shader));
                Check("Prefab includes the assigned glow shader", File.ReadAllText(ProjectPath.Find("CastleSurvivor.prefab")).Contains("c87ee44115734e40a1e8ae05bab01403"));
            }
            catch (Exception e) { failures++; report.AppendLine(e.ToString()); }
            finally
            {
                Object.Destroy(fixture); Application.logMessageReceived -= OnLog;
                SessionState.SetBool(Key, false);
                report.AppendLine("Not covered: headset appearance, stereo comfort, GPU cost, arbitrary mesh ledges.");
                File.WriteAllText(ProjectPath.Report("VaultTargetValidation.txt"), report.ToString());
                Debug.Log(report.ToString()); EditorApplication.Exit(failures == 0 ? 0 : 1);
            }
        }
    }
}
