using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AgentPlaytest;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.DevTools.Editor;
using DeadlyJenga.Core;
using Object = UnityEngine.Object;

namespace DeadlyJenga.XRSimulation.Editor
{
    public sealed class SimulatorScenario : IPlaytestScenario
    {
        Keyboard keyboard;
        PopupTelemetry popup;
        static bool Grip(bool left) => HybridXRInput.Controller(left)?.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("gripPressed")?.isPressed == true;
        static Vector2 Stick(bool left) => HybridXRInput.Controller(left)?.TryGetChildControl<UnityEngine.InputSystem.Controls.Vector2Control>("primary2DAxis")?.ReadValue() ?? Vector2.zero;
        static bool MenuHeld(CandyTrackingSource source)
        {
            var property = source.GetType().GetProperty("MenuSelectHeld");
            if (property == null) throw new System.InvalidOperationException("Current production MenuSelectHeld property is missing.");
            return (bool)property.GetValue(source);
        }
        IEnumerator Keys(PlaytestContext context, int frames, params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            yield return context.WaitFrames(frames);
        }
        IEnumerator Observe(Action observation, string label)
        {
            popup?.RecordBoundary(label + " editor-submit");
            yield return ManagedFrameObserver.Observe(() =>
            {
                popup?.RecordBoundary(label + " player-observation");
                observation();
            });
        }

        public IEnumerator Run(PlaytestContext c) => RunScenario(c, false);
        internal IEnumerator RunTail(PlaytestContext c) => RunScenario(c, true);
        IEnumerator RunScenario(PlaytestContext c, bool tailOnly)
        {
            yield return c.WaitUntil(() => Object.FindFirstObjectByType<CastlePlaySession>()?.Player != null);
            c.Check(Services.World != null, "Real Game scene initialized its world and session");
            var originalKeyboard = Keyboard.current;
            SimulatorRig rig = null;
            StockExpressionActions expressionActions = null;
            try
            {
                rig = new SimulatorRig(false);
                keyboard = InputSystem.AddDevice<Keyboard>("XR simulation test keyboard");
                var sim = rig.Simulator;
                var player = rig.Player;
                var source = player.GetComponent<CandyTrackingSource>();
                popup = new PopupTelemetry();
                popup.OutputPath = System.IO.Path.Combine(c.Plan.output, "hand-popup-telemetry.json");
                popup.Phase = "controller bindings; deliberate menu close";
                c.Check(source && source.LeftPoseSource == null && source.RightPoseSource == null,
                    "Production hand reader has no custom optical pose overrides");
                if (!tailOnly)
                {
                yield return c.WaitFrames(6);
                c.Check(popup.SampleCount > 0, "Managed telemetry sampled the real post-LateUpdate player loop");
                c.Check(player.IsVR, "Explicit fixture VR override selects production VR input path without an XR display");
                c.Check(InputSystem.devices.Count(x => x is XRSimulatedHMD) == 1,
                    "Stock XRI lifecycle emitted exactly one simulated HMD");
                yield return Observe(() => c.Check(HybridXRInput.ControllerTracked(true) && HybridXRInput.ControllerTracked(false),
                    "Stock XRI lifecycle emitted both tracked, handed controllers"), "tracked controllers");
                rig.Session.SetMenu(false);

                yield return Keys(c, 5, Key.G, Key.T);
                yield return Observe(() => c.Check(Grip(false) && HybridXRInput.Controller(false).TryGetChildControl<UnityEngine.InputSystem.Controls.AxisControl>("trigger").ReadValue() > .5f,
                    "Sample keyboard bindings reach right XR controller grip and trigger"), "right grip and trigger");
                yield return Keys(c, 4);
                yield return Observe(() => c.Check(!Grip(false), "Sample grip release clears the emitted button state"), "right grip release");
                yield return Keys(c, 5, Key.LeftShift, Key.G, Key.I);
                yield return Observe(() => c.Check(Grip(true) && !Grip(false) && Stick(true).y > .5f,
                    "Shift routes grip and joystick input to the left controller"), "left grip and stick");
                // Stock XRI updates only the selected side. Release its inputs before releasing Shift.
                yield return Keys(c, 4, Key.LeftShift);
                yield return Observe(() => c.Check(!Grip(true) && Stick(true).sqrMagnitude < .01f,
                    "Left keyboard inputs release before changing simulator side"), "left release before side change");
                yield return Keys(c, 5, Key.I);
                yield return Observe(() => c.Check(Stick(false).y > .5f && Stick(true).sqrMagnitude < .01f,
                    "Unmodified joystick binding reaches only the right controller"), "right stick");
                yield return Keys(c, 4);
                player.UseStationaryVR();
                popup.RecordBoundary("dominance readiness editor");
                c.Check(!player.InputSuspended, "Production survivor input is active for dominance routing; gates=" + popup.GateSummary());
                PlayerPrefs.SetInt("CandyBlaster.LeftHanded", 0);
                var startPosition = player.transform.position;
                yield return Keys(c, 8, Key.LeftShift, Key.I);
                yield return Keys(c, 3, Key.LeftShift);
                c.Check(Vector3.ProjectOnPlane(player.transform.position - startPosition, Vector3.up).magnitude > .005f,
                    "With right dominance the non-dominant left joystick moves the real survivor through normal Update frames");
                PlayerPrefs.SetInt("CandyBlaster.LeftHanded", 1);
                var startRotation = player.transform.rotation;
                yield return Keys(c, 8, Key.LeftShift, Key.L);
                yield return Keys(c, 3, Key.LeftShift);
                c.Check(Quaternion.Angle(startRotation, player.transform.rotation) > .5f,
                    "With left dominance the dominant left joystick turns the real survivor");
                startPosition = player.transform.position;
                yield return Keys(c, 8, Key.I);
                yield return Keys(c, 3);
                c.Check(Vector3.ProjectOnPlane(player.transform.position - startPosition, Vector3.up).magnitude > .005f,
                    "With left dominance the non-dominant right joystick moves the real survivor through normal Update frames");
                PlayerPrefs.SetInt("CandyBlaster.LeftHanded", 0);
                startRotation = player.transform.rotation;
                yield return Keys(c, 8, Key.L);
                yield return Keys(c, 3);
                c.Check(Quaternion.Angle(startRotation, player.transform.rotation) > .5f,
                    "With right dominance the dominant right joystick turns the real survivor");

                rig.Session.SetMenu(true);
                popup.Phase = "head motion; deliberate menu open";
                sim.targetedDeviceInput = TargetedDevices.HMD;
                var hmd = InputSystem.devices.OfType<XRSimulatedHMD>().Single();
                var before = player.viewCamera.transform.localPosition;
                yield return Keys(c, 8, Key.E, Key.RightArrow);
                yield return Keys(c, 3);
                c.Check(Vector3.Distance(before, player.viewCamera.transform.localPosition) > .005f,
                    "Keyboard-driven stock HMD moves the real tracked camera while menu is open");
                yield return Observe(() => c.Check(Vector3.Distance(player.viewCamera.transform.localPosition, hmd.devicePosition.ReadValue()) < .015f,
                    "TrackedPoseDriver consumes stock XRI HMD position"), "tracked head position");
                yield return Observe(() => c.Check(Quaternion.Angle(player.viewCamera.transform.localRotation, hmd.deviceRotation.ReadValue()) < 1f,
                    "TrackedPoseDriver consumes stock XRI HMD rotation once"), "tracked head rotation");
                c.Screenshot(player.viewCamera, "xri-controller-head-tracking");

                sim.hmdIsTracked = false; sim.hmdTrackingState = InputTrackingState.None;
                popup.Phase = "HMD tracking loss and recovery";
                yield return c.WaitFrames(3);
                var frozen = player.viewCamera.transform.localPosition;
                yield return Keys(c, 6, Key.E);
                yield return Keys(c, 3);
                c.Check(Vector3.Distance(frozen, player.viewCamera.transform.localPosition) < .005f,
                    "Tracked camera rejects new poses while HMD tracking state is unavailable");
                sim.hmdIsTracked = true; sim.hmdTrackingState = InputTrackingState.Position | InputTrackingState.Rotation;
                yield return c.WaitFrames(4);
                c.Check(Vector3.Distance(frozen, player.viewCamera.transform.localPosition) > .005f,
                    "Tracked camera resumes from the current pose after tracking recovery");

                player.UseStationaryVR(); player.CalibrateSeatedHeight();
                popup.Phase = "seated calibration and turn";
                yield return c.WaitFrames(2);
                var calibrated = player.transform.InverseTransformPoint(player.viewCamera.transform.position).y;
                c.Check(player.IsVR && !float.IsNaN(calibrated) && !float.IsInfinity(calibrated) && Mathf.Abs(calibrated - player.eyeHeight * player.playerScale) < .015f,
                    "Seated calibration sets the real camera to the configured local eye-height target");
                rig.Session.SetMenu(false);
                yield return c.WaitFrames(2);
                c.Check(!player.InputSuspended, "Production turning is enabled for the pivot test");
                var pivot = player.viewCamera.transform.position;
                var rotationBeforeTurn = player.transform.rotation;
                player.ApplyVRTurn(1, .1f);
                var expectedTurn = Mathf.Clamp(player.vrSmoothDegreesPerSecond, 15, 360) * .1f;
                c.Check(Mathf.Abs(Quaternion.Angle(rotationBeforeTurn, player.transform.rotation) - expectedTurn) < .1f && Vector3.Distance(pivot, player.viewCamera.transform.position) < .005f,
                    "Configured nonzero VR turn changes yaw by the expected amount while preserving the tracked eye pivot");

                sim.targetedDeviceInput = TargetedDevices.LeftDevice;
                popup.Phase = "stock hands and pinch";
                yield return Keys(c, 3, Key.LeftBracket);
                yield return Keys(c, 6);
                c.Check(sim.deviceLifecycleManager.deviceMode == SimulatedDeviceLifecycleManager.DeviceMode.Hand,
                    "Stock sample action switches lifecycle to hand mode");
                var subsystems = new List<XRHandSubsystem>();
                SubsystemManager.GetSubsystems(subsystems);
                c.Check(subsystems.Any(x => x.running && x.GetType().Name.Contains("Simulator")),
                    "Actual XRI simulator XRHandSubsystem is running");
                yield return c.WaitFrames(5);
                yield return Observe(() => c.Check(source.WorldHand(true).skeletonTracked && source.WorldHand(false).skeletonTracked,
                    "Both optical-style skeletons flow through XRHands into production CandyTrackingSource"), "optical skeletons");
                yield return Keys(c, 3, Key.M);
                yield return Keys(c, 5);
                yield return Observe(() => c.Check(source.RightPinching, "Stock pinch expression reaches production finger-distance gesture detection"), "pinch held");
                yield return Keys(c, 3, Key.M);
                yield return Keys(c, 5);
                yield return Observe(() => c.Check(!source.RightPinching, "Stock pinch toggle releases the production gesture"), "pinch release");
                }
                else
                {
                    popup.Phase = "fresh stock hand setup for unexecuted tail";
                    yield return c.WaitFrames(6);
                    sim.targetedDeviceInput = TargetedDevices.LeftDevice;
                    yield return Keys(c, 3, Key.LeftBracket);
                    yield return Keys(c, 6);
                    rig.Session.SetMenu(false);
                    bool eligible = false;
                    PopupTelemetry.InputGates gates = null;
                    using (ManagedFrameObserver.Register(() =>
                    {
                        gates = popup.CaptureGates();
                        eligible = gates.arrivalActive == "False" && !player.InputSuspended;
                    }))
                    {
                        try { yield return c.WaitUntil(() => eligible); }
                        finally { c.Check(eligible, "Tail readiness: normal arrival completed and real survivor input active; gates=" + JsonUtility.ToJson(gates)); }
                    }
                    yield return Observe(() => c.Check(source.WorldHand(true).skeletonTracked && source.WorldHand(false).skeletonTracked,
                        "Tail readiness: both actual stock skeletons reach the production source"), "tail optical readiness");
                }
                expressionActions = new StockExpressionActions(sim, System.IO.Path.Combine(c.Plan.output, "expression-action-lifecycle.json"));
                yield return Keys(c, 3, Key.O);
                yield return Keys(c, 4);
                var openIndex = Vector3.zero;
                yield return Observe(() => openIndex = source.WorldHand(false).skeleton.Joint(7), "open finger reference");
                c.Screenshot(player.viewCamera, "xri-open-finger-pose");
                yield return Keys(c, 3, Key.P);
                yield return Keys(c, 4);
                yield return Observe(() => c.Check(Vector3.Distance(openIndex, source.WorldHand(false).skeleton.Joint(7)) > .005f,
                    "Stock open and fist expressions change actual XRHands finger joints in the production reader"), "fist joints");
                c.Screenshot(player.viewCamera, "xri-fist-finger-pose");
                yield return Keys(c, 3, Key.P);
                yield return Keys(c, 4);
                sim.targetedDeviceInput = TargetedDevices.RightDevice;
                yield return Keys(c, 6, Key.E, Key.UpArrow);
                yield return Keys(c, 3);
                yield return Observe(() => c.Check(source.WorldHand(false).skeletonTracked,
                    "Offset and rotated wrist pose retains a complete real XRI hand skeleton"), "offset wrist skeleton");

                foreach (bool left in new[] { true, false })
                {
                    popup.Phase = left ? "left controller mixed input" : "right controller mixed input";
                    rig.AddSupplemental(left);
                    yield return c.WaitFrames(4);
                    yield return Observe(() => c.Check(HybridXRInput.SingleController && HybridXRInput.SingleControllerLeft == left,
                        "Supplemental fixture exposes only the " + (left ? "left" : "right") + " controller"), "mixed controller side");
                    yield return Observe(() => c.Check(source.IsHybrid && !source.WorldHand(left).skeletonTracked && source.WorldHand(!left).skeletonTracked,
                        "Mixed fixture combines controller palm with opposite real XRI hand subsystem skeleton"), "mixed source poses");
                    c.Check(source.LeftPoseSource == null && source.RightPoseSource == null,
                        "Mixed fixture never substitutes a custom optical hand pose source");
                    rig.WriteSupplemental(left, Vector2.up, true);
                    yield return c.WaitFrames(3);
                    yield return Observe(() => c.Check(HybridXRInput.MoveStick().y > .9f && Grip(left),
                        "Single-controller stick and look modifier reach production hybrid input"), "mixed stick and grip");
                    rig.RemoveSupplemental();
                    yield return c.WaitFrames(4);
                    yield return Observe(() => c.Check(!HybridXRInput.SingleController && HybridXRInput.MoveStick().sqrMagnitude < .001f,
                        "Controller removal clears stale hybrid walking input"), "controller removal");
                    rig.AddSupplemental(left);
                    yield return c.WaitFrames(4);
                    yield return Observe(() => c.Check(source.IsHybrid && !Grip(left) && HybridXRInput.MoveStick().sqrMagnitude < .001f,
                        "Same-handed controller reacquisition restores hybrid tracking without stale stick or grip"), "controller reacquisition");
                    rig.RemoveSupplemental();
                    yield return c.WaitFrames(3);
                }
                sim.leftHandIsTracked = false; sim.rightHandIsTracked = false;
                popup.Phase = "optical-style tracking loss and recovery";
                yield return c.WaitFrames(5);
                yield return Observe(() => c.Check(!source.HasTrackedHands && !MenuHeld(source),
                    "Hand tracking loss clears production hand and menu-select state"), "optical loss");
                sim.leftHandIsTracked = true; sim.rightHandIsTracked = true;
                yield return c.WaitFrames(5);
                yield return Observe(() => c.Check(source.HasHandTracking && !MenuHeld(source),
                    "Neutral hand reacquisition restores tracking without a stuck pinch"), "optical reacquisition");
                c.Screenshot(player.viewCamera, "xri-hands-through-production-reader");

                rig.Session.SetMenu(true);
                rig.Session.SetRole(true);
                popup.Phase = "deliberate flying-hand role switch";
                rig.SelectCamera();
                yield return c.WaitFrames(5);
                c.Check(rig.Session.PlayingHand && rig.Session.HandActor().IsVR && rig.Session.ActiveCamera == rig.Session.HandActor().ViewCamera,
                    "Role command selects the real flying-hand VR camera");
                c.Screenshot(rig.Session.ActiveCamera, "xri-flying-hand-camera");
                rig.Session.SetRole(false); rig.SelectCamera();
                popup.Phase = "deliberate survivor role switch";
                yield return c.WaitFrames(4);
                c.Check(!rig.Session.PlayingHand && rig.Session.ActiveCamera == player.viewCamera,
                    "Returning to survivor restores its active tracked camera");
            }
            finally
            {
                try { popup?.Save(); }
                finally
                {
                    try { popup?.Dispose(); }
                    finally
                    {
                        popup = null;
                        try
                        {
                            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                            if (originalKeyboard != null && originalKeyboard.added) originalKeyboard.MakeCurrent();
                        }
                        finally
                        {
                            try { rig?.Dispose(); }
                            finally
                            {
                                try { expressionActions?.Dispose(); }
                                finally
                                {
                                c.Check(ManagedFrameObserver.SaveCleanup(System.IO.Path.Combine(c.Plan.output, "input-observer-cleanup.json")),
                                    "Managed input observers and owned hook removed before Play Mode exit");
                                c.Check(expressionActions == null || expressionActions.Restored, "Stock Open/Fist action enabled states restored after fixture cleanup");
                                }
                            }
                        }
                    }
                }
            }
            yield return c.WaitFrames(3);
            c.Check(!InputSystem.devices.Any(x => x is XRSimulatedHMD || x is XRSimulatedController),
                "Fixture cleanup removed all owned simulated HMD/controller devices");
            var remaining = new List<XRHandSubsystem>();
            SubsystemManager.GetSubsystems(remaining);
            c.Check(!remaining.Any(x => x.running && x.GetType().Name.Contains("Simulator")),
                "Fixture cleanup stopped the simulated hand provider");
        }
    }
    public sealed class SimulatorTailScenario : IPlaytestScenario
    {
        public IEnumerator Run(PlaytestContext context) => new SimulatorScenario().RunTail(context);
    }
}
