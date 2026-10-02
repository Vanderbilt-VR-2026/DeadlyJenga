using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;

namespace FlyingHand.Editor
{
    public static class FlyingHandDesktopSmokeTest
    {
        static FlyingHandController c;
        static ThirdPersonVRFollow follow;
        static Keyboard keys;
        static Mouse mouse;
        static XRHMD head;
        static InputSettings originalInputSettings;
        static HideFlags originalInputSettingsFlags;
        static InputSettings probeInputSettings;
        static ControlMode savedMode;
        static bool savedCapture;
        static int stage;
        static double started;
        static bool settling;
        static StringBuilder report;
        public static string Result = "Not run";
        [MenuItem("Tools/Flying Hand/Run Desktop Smoke Test (Play Mode)")]
        public static void Begin()
        {
            if (Result == "Running" || FlyingHandSmokeTest.Result == "Running") throw new InvalidOperationException("A smoke test is already running.");
            c = UnityEngine.Object.FindFirstObjectByType<FlyingHandController>();
            if (!Application.isPlaying || !c || c.HasRunningXRDisplay()) throw new InvalidOperationException("Run this desktop probe in the HandDemo scene in Play Mode without an active XR display.");
            follow = UnityEngine.Object.FindFirstObjectByType<ThirdPersonVRFollow>();
            if (!follow || !follow.viewCamera) throw new InvalidOperationException("The HandDemo camera rig is required.");
            // MCP runs with the Game view unfocused. Use a temporary clone so
            // synthetic events reach the player without changing host settings.
            originalInputSettings = InputSystem.settings;
            originalInputSettingsFlags = originalInputSettings.hideFlags;
            // Input System 1.20 destroys a default HideAndDontSave settings object
            // when replacing it. Keep that original alive until it is restored.
            if (originalInputSettingsFlags == HideFlags.HideAndDontSave)
                originalInputSettings.hideFlags &= ~HideFlags.NotEditable;
            probeInputSettings = UnityEngine.Object.Instantiate(originalInputSettings);
            probeInputSettings.hideFlags = HideFlags.HideAndDontSave;
            probeInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            probeInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = probeInputSettings;
            savedMode = c.ControlModeOverride; savedCapture = c.captureDesktopCursor;
            c.captureDesktopCursor = false;
            keys = InputSystem.AddDevice<Keyboard>("FlyingHandTestKeyboard");
            mouse = InputSystem.AddDevice<Mouse>("FlyingHandTestMouse");
             head = InputSystem.AddDevice<XRHMD>("FlyingHandInputOnlyHMD");
            stage = 0; Result = "Running"; report = new StringBuilder(); Reset(); EditorApplication.update += Tick;
        }
        static void Reset()
        {
            c.enabled = false;
            settling = true;
            started = Time.timeAsDouble;
        }
        static void StartStage()
        {
            c.ControlModeOverride = stage == 6 ? ControlMode.VR : stage == 7 ? ControlMode.Desktop : ControlMode.Auto;
            c.Body.position = new Vector3(0, 3, 0); c.Body.rotation = Quaternion.identity;
            c.Body.linearVelocity = Vector3.zero; c.Body.angularVelocity = Vector3.zero;
            c.enabled = true;
            follow.desktopPitch = 18; follow.SnapToTarget(); started = Time.timeAsDouble;
            settling = false;
        }
        static void Check(string label, bool ok, string detail) => report.AppendLine((ok ? "PASS " : "FAIL ") + label + ": " + detail);
        static void Tick()
        {
            if (!Application.isPlaying || !c) { Finish(); return; }
            try
            {
                if (settling)
                {
                    InputSystem.QueueStateEvent(keys, new KeyboardState());
                    InputSystem.QueueStateEvent(mouse, new MouseState());
                    if (Time.timeAsDouble - started > .25) StartStage();
                    return;
                }
                c.SendMessage("OnApplicationFocus", true);
                Key[] pressed = stage == 1 ? new[] { Key.W, Key.E } : stage == 2 ? new[] { Key.S, Key.Q } :
                    stage == 3 ? new[] { Key.A } : stage == 4 ? new[] { Key.D } : stage == 6 ? new[] { Key.W, Key.E } : new Key[0];
                InputSystem.QueueStateEvent(keys, new KeyboardState(pressed));
                InputSystem.QueueStateEvent(mouse, new MouseState { delta = stage == 5 ? new Vector2(8, 3) : Vector2.zero });
                if (Time.timeAsDouble - started < 1.2) return;
                var p = c.Body.position;
                var driver = follow.viewCamera.GetComponent<TrackedPoseDriver>();
                if (stage == 0)
                {
                    Check("Auto without running display", c.ActiveControlMode == ControlMode.Desktop, c.ActiveControlMode.ToString());
                    Check("Input-only HMD does not select VR", !c.HasRunningXRDisplay() && c.ActiveControlMode == ControlMode.Desktop, "Simulated HMD present, no XR display");
                    Check("Desktop camera tracking disabled", !driver.enabled, driver.enabled.ToString());
                }
                if (stage == 1) Check("W/E through shared Rigidbody", p.z > 1 && p.y > 4, p.ToString());
                if (stage == 2) Check("S/Q through shared Rigidbody", p.z < -1 && p.y < 2, p.ToString());
                if (stage == 3) Check("A strafe left", p.x < -1, p.ToString());
                if (stage == 4) Check("D strafe right", p.x > 1, p.ToString());
                if (stage == 5)
                {
                    Check("Mouse X yaws body", Mathf.Abs(Mathf.DeltaAngle(0,c.Body.rotation.eulerAngles.y)) > 5, c.Body.rotation.eulerAngles.ToString());
                    Check("Mouse Y changes view pitch", follow.desktopPitch < 15, follow.desktopPitch.ToString());
                    Check("Desktop yaw stays upright", Vector3.Angle(c.transform.up,Vector3.up) < .5f, c.transform.eulerAngles.ToString());
                }
                if (stage == 6)
                {
                    Check("VR override restores tracked camera", c.ActiveControlMode == ControlMode.VR && driver.enabled, c.ActiveControlMode.ToString());
                    Check("VR ignores desktop input", Vector3.Distance(p,new Vector3(0,3,0)) < .05f, p.ToString());
                }
                if (stage == 7) Check("Desktop override restores mouse view", c.ActiveControlMode == ControlMode.Desktop && !driver.enabled, c.ActiveControlMode.ToString());
                stage++; if (stage == 8) { Finish(); return; } Reset();
            }
            catch (Exception e) { report.AppendLine("FAIL " + e); Finish(); }
        }
        static void Finish()
        {
            EditorApplication.update -= Tick;
            if (keys != null && keys.added) InputSystem.RemoveDevice(keys);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            if (head != null && head.added) InputSystem.RemoveDevice(head);
            if (originalInputSettings)
            {
                InputSystem.settings = originalInputSettings;
                originalInputSettings.hideFlags = originalInputSettingsFlags;
            }
            if (probeInputSettings) UnityEngine.Object.DestroyImmediate(probeInputSettings);
            originalInputSettings = null; probeInputSettings = null;
            if (c) { c.captureDesktopCursor = savedCapture; c.ControlModeOverride = savedMode; c.RefreshControlMode(); }
            Result = report == null ? "Aborted" : report.ToString(); Debug.Log("FlyingHand desktop validation\n" + Result);
        }
    }
}
