using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

namespace FlyingHand.Editor
{
    // Editor-only integration probe. Synthetic XR input never ships in the player.
    public static class FlyingHandSmokeTest
    {
        static FlyingHandController controller;
        static HandDemoTestController left, right;
        static XRHMD head;
        static double started;
        static int stage;
        static bool settling;
        static StringBuilder report;
        static float maximumObservedSpeed;
        static ControlMode originalOverride;
        static Vector3 startCamera;
        static InputSettings originalInputSettings, probeInputSettings;
        static HideFlags originalInputSettingsFlags;
        public static string Result = "Not run";
        [MenuItem("Tools/Flying Hand/Run VR Smoke Test (Play Mode)")]
        public static void Begin()
        {
            if (Result == "Running" || FlyingHandDesktopSmokeTest.Result == "Running") throw new InvalidOperationException("A smoke test is already running.");
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            if (InputSystem.GetDevice<UnityEngine.InputSystem.XR.XRHMD>() != null) throw new InvalidOperationException("Disconnect the real HMD before synthetic testing.");
            controller = UnityEngine.Object.FindFirstObjectByType<FlyingHandController>();
            if (!controller || !UnityEngine.Object.FindFirstObjectByType<ThirdPersonVRFollow>()) throw new InvalidOperationException("Open the HandDemo scene first.");
            originalInputSettings = InputSystem.settings;
            originalInputSettingsFlags = originalInputSettings.hideFlags;
            // Preserve the default settings: Input System 1.20 otherwise destroys
            // HideAndDontSave settings when a temporary probe replaces them.
            if (originalInputSettingsFlags == HideFlags.HideAndDontSave)
                originalInputSettings.hideFlags &= ~HideFlags.NotEditable;
            probeInputSettings = UnityEngine.Object.Instantiate(originalInputSettings);
            probeInputSettings.hideFlags = HideFlags.HideAndDontSave;
            probeInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            probeInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = probeInputSettings;
            originalOverride = controller.ControlModeOverride;
            controller.ControlModeOverride = ControlMode.VR; controller.RefreshControlMode();
            InputSystem.RegisterLayout<HandDemoTestController>(); 
            left = InputSystem.AddDevice<HandDemoTestController>("FlyingHandTestLeft");
            right = InputSystem.AddDevice<HandDemoTestController>("FlyingHandTestRight");
            head = InputSystem.AddDevice<XRHMD>("FlyingHandTestHead");
            InputSystem.SetDeviceUsage(left, CommonUsages.LeftHand); InputSystem.SetDeviceUsage(right, CommonUsages.RightHand);
            stage = 0; report = new StringBuilder(); Result = "Running";
            ResetStage(); EditorApplication.update += Tick;
        }
        static void ResetStage()
        {
            // Drain queued input from the previous stage before resetting the
            // Rigidbody. Editor update and player input update are independent.
            controller.enabled = false;
            settling = true;
            started = Time.timeAsDouble;
        }
        static void StartStage()
        {
            controller.Body.position = new Vector3(0, 3, 0); controller.Body.rotation = Quaternion.identity;
            controller.Body.linearVelocity = Vector3.zero; controller.Body.angularVelocity = Vector3.zero;
            controller.enabled = true;
            var follow = UnityEngine.Object.FindFirstObjectByType<ThirdPersonVRFollow>(); follow.SnapToTarget();
            startCamera = Camera.main.transform.localPosition; maximumObservedSpeed = 0;
            started = Time.timeAsDouble;
            settling = false;
        }
        static void Check(string label, bool passed, string details)
        { report.AppendLine((passed ? "PASS " : "FAIL ") + label + ": " + details); }
        static void Tick()
        {
            if (!Application.isPlaying || !controller) { Finish(); return; }
            try
            {
                if (settling)
                {
                    var neutral = new HandDemoControllerState { deviceRotation = Quaternion.identity };
                    HandDemoTestInput.Queue(left, neutral); HandDemoTestInput.Queue(right, neutral);
                    if (Time.timeAsDouble - started > .25) StartStage();
                    return;
                }
                controller.SendMessage("OnApplicationFocus", true);
                var l = new HandDemoControllerState { deviceRotation = Quaternion.identity };
                var r = l;
                var h = new HandDemoHMDState { deviceRotation = Quaternion.identity, isTracked = true, trackingState = 3 };
                if (stage == 0 || stage == 7) l.primary2DAxis = Vector2.up;
                if (stage == 1) l.primary2DAxis = Vector2.down;
                if (stage == 2) l.primary2DAxis = Vector2.right;
                if (stage == 3) r.primary2DAxis = Vector2.right;
                if (stage == 4) r.trigger = 1;
                if (stage == 5) l.trigger = 1;
                if (stage == 6)
                {
                    h.deviceRotation = Quaternion.Euler(15, 55, 0); h.devicePosition = new Vector3(.2f, .3f, .1f);
                    l.deviceRotation = Quaternion.Euler(60, 90, 30); r.deviceRotation = Quaternion.Euler(0, -90, 90);
                }
                HandDemoTestInput.Queue(left, l); HandDemoTestInput.Queue(right, r); HandDemoTestInput.Queue(head, h);
                maximumObservedSpeed = Mathf.Max(maximumObservedSpeed, controller.Body.linearVelocity.magnitude);
                double duration = stage == 0 ? 2.6 : stage == 7 ? 4.0 : 1.2;
                if (Time.timeAsDouble - started < duration) return;
                Vector3 p = controller.Body.position;
                if (stage == 0)
                {
                    Check("XR left stick forward", p.z > 2, p.ToString());
                    var crate = GameObject.Find("Stack 1 Level 2");
                    Check("Dynamic collision pushes stack", Mathf.Abs(crate.transform.position.z - 10) > .2f, crate.transform.position.ToString());
                    Check("Collision upright", Vector3.Angle(controller.transform.up, Vector3.up) < 2, controller.transform.eulerAngles.ToString());
                    var follow = UnityEngine.Object.FindFirstObjectByType<ThirdPersonVRFollow>();
                    Vector3 expected = follow.target.position + follow.transform.rotation * new Vector3(0, follow.height, -follow.distance);
                    Check("Camera root follows", Vector3.Distance(follow.transform.position, expected) < 3, "follow lag=" + Vector3.Distance(follow.transform.position, expected));
                }
                if (stage == 1) Check("XR left stick reverse", p.z < -1, p.ToString());
                if (stage == 2) Check("XR left stick strafe", p.x > 1, p.ToString());
                if (stage == 3)
                {
                    Check("XR right stick yaw", Mathf.Abs(Mathf.DeltaAngle(0,controller.Body.rotation.eulerAngles.y)) > 10, controller.Body.rotation.eulerAngles.ToString());
                    Check("Yaw stays upright", Vector3.Angle(controller.Body.rotation * Vector3.up, Vector3.up) < .5f, controller.Body.rotation.eulerAngles.ToString());
                }
                if (stage == 4) Check("XR right trigger ascend", p.y > 4, p.ToString());
                if (stage == 5) Check("XR left trigger descend", p.y < 2, p.ToString());
                if (stage == 6)
                {
                    Check("Headset rotation preserved", Quaternion.Angle(Camera.main.transform.localRotation, h.deviceRotation) < 1, Camera.main.transform.localEulerAngles.ToString());
                    Check("Headset translation preserved", Vector3.Distance(Camera.main.transform.localPosition,h.devicePosition) < .05f, Camera.main.transform.localPosition.ToString());
                    Check("Head/controller poses do not steer", Vector3.Distance(p,new Vector3(0,3,0)) < .05f && Quaternion.Angle(controller.Body.rotation,Quaternion.identity) < 1, p.ToString());
                }
                if (stage == 7) Check("Speed bounded", maximumObservedSpeed <= controller.maximumSpeed + .1f && maximumObservedSpeed > 5, maximumObservedSpeed.ToString("F2"));
                stage++; if (stage > 7) { Finish(); return; } ResetStage();
            }
            catch (Exception e) { report.AppendLine("FAIL Exception: " + e); Finish(); }
        }
        static void Finish()
        {
            EditorApplication.update -= Tick;
            if (left != null && left.added) InputSystem.RemoveDevice(left);
            if (right != null && right.added) InputSystem.RemoveDevice(right);
            if (head != null && head.added) InputSystem.RemoveDevice(head);
            if (originalInputSettings)
            {
                InputSystem.settings = originalInputSettings;
                originalInputSettings.hideFlags = originalInputSettingsFlags;
            }
            if (probeInputSettings) UnityEngine.Object.DestroyImmediate(probeInputSettings);
            originalInputSettings = null; probeInputSettings = null;
            if (controller) { controller.ControlModeOverride = originalOverride; controller.RefreshControlMode(); }
            Result = report == null ? "Aborted" : report.ToString();
            Debug.Log("FlyingHand integration validation\n" + Result);
        }
    }
}
