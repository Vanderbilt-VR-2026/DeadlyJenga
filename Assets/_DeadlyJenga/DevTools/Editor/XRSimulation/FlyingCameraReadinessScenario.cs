using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using AgentPlaytest;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyPhysicsHands;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.Core;
using DeadlyJenga.DevTools.Editor;
using FlyingHand;
using Object = UnityEngine.Object;

namespace DeadlyJenga.XRSimulation.Editor
{
    public sealed class FlyingCameraReadinessScenario : IPlaytestScenario
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly Vector3 neutral = new Vector3(.18f, 1.35f, -.12f);
        readonly Quaternion facing = Quaternion.Euler(0, 23, 0);
        SimulatorRig rig;
        FlyingCameraReadinessTrace trace;
        PlaytestContext context;
        CastleHandActor actor;
        CandyFlyingHandBridge bridge;
        TrackedPoseDriver driver;
        FieldInfo simulatedState, simulatedEuler;
        int failures;

        void Check(bool value, string message)
        {
            context.Report.assertions.Add(new Assertion { passed = value, message = message, frame = Time.frameCount });
            if (!value) failures++;
        }
        IEnumerator Frames(int n) { yield return context.WaitFrames(n); }
        IEnumerator Until(Func<bool> predicate, string message)
        {
            int first = Time.frameCount; float time = Time.realtimeSinceStartup;
            while (!predicate() && Time.frameCount - first < 120 && Time.realtimeSinceStartup - time < 5) yield return null;
            context.Check(predicate(), "Readiness: " + message);
        }
        void Head(Vector3 position, Quaternion rotation, InputTrackingState mask)
        {
            // The stock simulator still publishes its device state in its normal Update.
            // Missing private fields are a version/applicability failure, never a custom production pose override.
            var state = new XRSimulatedHMDState
            {
                devicePosition = position, centerEyePosition = position,
                deviceRotation = rotation, centerEyeRotation = rotation,
                isTracked = mask != InputTrackingState.None, trackingState = (int)mask
            };
            simulatedState.SetValue(rig.Simulator, state);
            simulatedEuler.SetValue(rig.Simulator, rotation.eulerAngles);
            rig.Simulator.hmdIsTracked = state.isTracked;
            rig.Simulator.hmdTrackingState = mask;
        }
        bool Consumed(Vector3 position) => driver && Vector3.Distance(actor.ViewCamera.transform.localPosition, position) < .0001f;
        FlyingCameraReadinessTrace.Row Now()
        {
            var row = trace.Data.rows.LastOrDefault(x => x.sample == "player-loop-after-script-late-update");
            if (row == null || Time.frameCount - row.frame > 1)
                throw new InvalidOperationException("No current real player-frame camera snapshot; do not read the Editor input buffer as fallback.");
            return row;
        }
        void Neutral(string label)
        {
            var row = Now();
            context.Check(row != null && row.palm != 0, "Readiness: actual physical palm exists for " + label);
            context.Check(trace.Data.rows.Any(x => x.sample == "player-loop-after-script-late-update" && x.phase == trace.Phase),
                "Readiness: actual post-LateUpdate loop samples exist for " + label);
            Check(row.activeCamera == row.camera && row.driverEnabled, label + ": real hand camera and TPD active");
            Check(actor.follow.target == actor.motor.transform && actor.follow.positionTarget == bridge.PhysicalHand.bodies[0].transform,
                label + ": motor heading and actual physical palm are independent follow targets");
            Check(row.cameraWorld.y > row.water + .05f, label + ": neutral oracle is clear of water clamp");
            Check(row.residual.magnitude <= .02f, label + ": neutral compact view center within 0.02 world units of palm+motor-yaw offset; error=" + row.residual.magnitude);
        }
        IEnumerator Rendered(string phase)
        {
            trace.Phase = phase;
            yield return Until(() => trace.Data.rows.Any(x => x.phase == phase && x.sample == "render-begin" && !x.explicitCapture && (x.state & 1) != 0 && Vector3.Distance(x.centerEye, neutral) < .0001f),
                "real active-camera render after position-valid neutral input in " + phase);
            var row = trace.Data.rows.First(x => x.phase == phase && x.sample == "render-begin" && !x.explicitCapture && (x.state & 1) != 0 && Vector3.Distance(x.centerEye, neutral) < .0001f);
            Check(row.residual.magnitude <= .02f, phase + ": first observed position-valid rendered neutral residual <=0.02; error=" + row.residual.magnitude);
        }
        IEnumerator Enter(bool delayed, bool left)
        {
            if (rig.Session.PlayingHand)
            {
                // Seed a genuinely DIFFERENT stale pose through the real TPD before deactivation.
                Head(Vector3.zero, Quaternion.identity, InputTrackingState.Position | InputTrackingState.Rotation);
                yield return Until(() => Consumed(Vector3.zero), "hand camera consumed zero pose before delayed-entry reset");
                yield return Frames(3);
            }
            rig.Session.SetRole(false); rig.SelectCamera();
            PlayerPrefs.SetInt("CandyBlaster.LeftHanded", left ? 1 : 0);
            // The same stock device state is used in both runs, with only validity arrival changed.
            Head(Vector3.zero, Quaternion.identity, InputTrackingState.Position | InputTrackingState.Rotation);
            yield return Frames(4);
            Head(neutral, facing, delayed ? InputTrackingState.None : InputTrackingState.Position | InputTrackingState.Rotation);
            yield return Frames(4);
            trace.Phase = (left ? "left-" : "right-") + (delayed ? "delayed-entry-invalid" : "valid-first-entry");
            rig.Session.SetRole(true); rig.SelectCamera(); rig.Session.SetMenu(true);
            context.Check(rig.Session.PlayingHand, "Readiness: real role command accepted hand role");
            yield return Until(() => bridge.PhysicalHand && bridge.PhysicalHand.bodies.Length > 0, "real physical palm created");
            yield return Frames(4);
            if (delayed)
            {
                Check(!FlyingCameraReadinessTrace.Ready(actor.follow), trace.Phase + ": positional baseline remains pending while mask None");
                trace.Phase = (left ? "left-" : "right-") + "first-valid-delayed";
                Head(neutral, facing, InputTrackingState.Position | InputTrackingState.Rotation);
                yield return Rendered(trace.Phase);
            }
            yield return Until(() => Consumed(neutral), "real TPD consumed the simulator's nonzero center-eye position");
            yield return Frames(4);
            Neutral(trace.Phase);
            Check(FlyingCameraReadinessTrace.Ready(actor.follow), trace.Phase + ": positional baseline captured after valid pose");
        }
        IEnumerator Lean(string label)
        {
            trace.Phase = label;
            var neutralRow = Now();
            var baseline = FlyingCameraReadinessTrace.Baseline(actor.follow);
            var lean = new Vector3(.12f, .03f, .08f);
            Head(neutral + lean, facing, InputTrackingState.Position | InputTrackingState.Rotation);
            yield return Until(() => Consumed(neutral + lean), "real TPD consumed subsequent physical lean");
            yield return Frames(6);
            var row = Now();
            var expected = neutralRow.cameraParentMatrix.MultiplyVector(lean);
            Check(Vector3.Distance(row.residual - neutralRow.residual, expected) <= .02f,
                label + ": raw center-eye lean appears once through recorded parent scale/rotation");
            Check(Vector3.Distance(FlyingCameraReadinessTrace.Baseline(actor.follow), baseline) < .0001f,
                label + ": neutral baseline is not recaptured each frame");
            yield return Frames(12);
            Check(Vector3.Distance(Now().residual, row.residual) <= .02f, label + ": held head lean is preserved without drift");
            var beforeRotation = actor.ViewCamera.transform.rotation;
            Head(neutral + lean, Quaternion.Euler(0, 38, 0), InputTrackingState.Position | InputTrackingState.Rotation);
            yield return Frames(5);
            Check(Mathf.Abs(Quaternion.Angle(beforeRotation, actor.ViewCamera.transform.rotation) - 15) <= 1,
                label + ": head yaw is applied once");
            Check(Vector3.Distance(Now().residual, row.residual) <= .02f, label + ": head yaw alone does not orbit view center");
            trace.Phase = label + "-explicit-local-snap";
            actor.follow.SnapToTarget();
            yield return Frames(3);
            Neutral(trace.Phase);
            var afterSnap = Now();
            Head(neutral + lean * 2, Quaternion.Euler(0, 38, 0), InputTrackingState.Position | InputTrackingState.Rotation);
            yield return Frames(6);
            Check(Vector3.Distance(Now().residual - afterSnap.residual, afterSnap.cameraParentMatrix.MultiplyVector(lean)) <= .02f,
                label + ": new lean remains after explicit local Snap; no system-recenter claim");
        }
        IEnumerator FocusedLean(string label)
        {
            trace.Phase = label;
            var before = Now();
            var baseline = FlyingCameraReadinessTrace.Baseline(actor.follow);
            var lean = new Vector3(.12f, .03f, .08f);
            Head(neutral + lean, facing, InputTrackingState.Position | InputTrackingState.Rotation);
            yield return Until(() => Consumed(neutral + lean), "real TPD consumed the next synthetic physical lean");
            yield return Frames(5);
            var after = Now();
            Check(Vector3.Distance(after.residual - before.residual, before.cameraParentMatrix.MultiplyVector(lean)) <= .02f,
                label + ": next lean appears once through actual camera parent matrix");
            Check(Vector3.Distance(FlyingCameraReadinessTrace.Baseline(actor.follow), baseline) < .0001f,
                label + ": initial neutral baseline retained");
            yield return Frames(4);
            Check(Vector3.Distance(Now().residual, after.residual) <= .02f, label + ": held lean has no drift");
        }
        IEnumerator Masks()
        {
            Head(neutral, facing, InputTrackingState.Rotation);
            yield return Frames(4);
            trace.Phase = "rotation-valid-position-invalid";
            actor.follow.SnapToTarget();
            yield return Frames(4);
            Check(!FlyingCameraReadinessTrace.Ready(actor.follow), "Rotation-only tracking mask cannot capture positional baseline");
            Head(neutral, facing, InputTrackingState.Position);
            trace.Phase = "position-valid-rotation-invalid";
            yield return Frames(5);
            Check(Consumed(neutral) && FlyingCameraReadinessTrace.Ready(actor.follow), "Position-only mask can establish consumed positional baseline");
            Neutral(trace.Phase);
            var state = driver.trackingStateInput.action;
            context.Check(state != null && state.enabled && state.controls.Count > 0, "Readiness: authored enabled resolved tracking action for disabled-action case");
            state.Disable();
            try
            {
                trace.Phase = "authored-tracking-action-disabled";
                yield return Frames(2);
                actor.follow.SnapToTarget();
                yield return Frames(4);
                Check(!FlyingCameraReadinessTrace.Ready(actor.follow), "Authored disabled tracking action is None, not null-action fallback");
            }
            finally { state.Enable(); }
            Head(neutral, facing, InputTrackingState.Position | InputTrackingState.Rotation);
            yield return Frames(5);
            Neutral("tracking-action-reenabled");
            actor.motor.ControlModeOverride = ControlMode.Desktop; actor.motor.RefreshControlMode();
            trace.Phase = "desktop-mode-boundary"; yield return Frames(3);
            Check(actor.follow.DesktopView, "Actual motor mode command selects desktop follow mode");
            actor.motor.ControlModeOverride = ControlMode.VR; actor.motor.RefreshControlMode();
            trace.Phase = "vr-mode-boundary"; yield return Frames(5);
            Neutral(trace.Phase);
        }
        void FiniteSeamProbe()
        {
            // Explicit private-method unit seam on INACTIVE temporary objects. Never drive the live HMD with NaN.
            var name = "FlyingCameraFiniteProbe" + Guid.NewGuid().ToString("N");
            var baseName = name + "Base";
            GameObject root = null; UnityEngine.InputSystem.InputDevice device = null; InputAction position = null, tracking = null;
            try
            {
                InputSystem.RegisterLayout<UnityEngine.InputSystem.InputDevice>(baseName);
                InputSystem.RegisterLayout("{\"name\":\"" + name + "\",\"extend\":\"" + baseName + "\",\"controls\":[{\"name\":\"position\",\"layout\":\"Vector3\",\"offset\":0},{\"name\":\"tracking\",\"layout\":\"Integer\",\"offset\":12}]}");
                device = InputSystem.AddDevice(name);
                // PassThrough makes the deliberately non-actuatable NaN readable; Value actions can return default while waiting.
                position = new InputAction("isolated finite pose", InputActionType.PassThrough, "<" + name + ">/position");
                tracking = new InputAction("isolated tracking mask", InputActionType.Value, "<" + name + ">/tracking");
                position.Enable(); tracking.Enable();
                root = new GameObject("INACTIVE finite baseline seam probe"); root.SetActive(false);
                var child = new GameObject("Inactive camera"); child.transform.SetParent(root.transform);
                var camera = child.AddComponent<Camera>();
                var tpd = child.AddComponent<TrackedPoseDriver>();
                tpd.positionInput = new InputActionProperty(position); tpd.trackingStateInput = new InputActionProperty(tracking);
                tpd.ignoreTrackingState = false;
                var follow = root.AddComponent<ThirdPersonVRFollow>(); follow.viewCamera = camera;
                typeof(ThirdPersonVRFollow).GetField("poseDriver", Private).SetValue(follow, tpd);
                var capture = typeof(ThirdPersonVRFollow).GetMethod("CaptureHeadBaseline", Private);
                var p = device.GetChildControl<Vector3Control>("position");
                var mask = device.GetChildControl<IntegerControl>("tracking");
                InputSystem.QueueDeltaStateEvent(mask, 1);
                InputSystem.QueueDeltaStateEvent(p, new Vector3(float.NaN, 0, 0));
                InputSystem.Update();
                context.Check(float.IsNaN(position.ReadValue<Vector3>().x), "Finite probe input genuinely contains NaN on non-XR device");
                capture.Invoke(follow, null);
                Check(!FlyingCameraReadinessTrace.Ready(follow), "Isolated private seam: nonfinite processed position cannot capture stale finite camera baseline");
                InputSystem.QueueDeltaStateEvent(p, Vector3.right * .2f); InputSystem.Update();
                capture.Invoke(follow, null);
                Check(!FlyingCameraReadinessTrace.Ready(follow), "Isolated private seam: finite action with stale camera position remains pending");
                child.transform.localPosition = Vector3.right * .2f;
                capture.Invoke(follow, null);
                Check(FlyingCameraReadinessTrace.Ready(follow), "Isolated private seam: matching finite action and camera can capture baseline; not TPD timing evidence");
            }
            finally
            {
                try
                {
                    if (root) Object.DestroyImmediate(root);
                    position?.Dispose(); tracking?.Dispose();
                    if (device != null && device.added) InputSystem.RemoveDevice(device);
                }
                finally
                {
                    try { InputSystem.RemoveLayout(name); }
                    finally { InputSystem.RemoveLayout(baseName); }
                }
            }
        }
        internal IEnumerator RunFiniteOnly(PlaytestContext c)
        {
            context = c;
            FiniteSeamProbe();
            c.Check(failures == 0, "Finite-only inactive non-XR seam outcomes passed; no camera runtime matrix was repeated");
            yield break;
        }
        public IEnumerator Run(PlaytestContext c) => RunMeasured(c, false);
        internal IEnumerator RunFirstRender(PlaytestContext c) => RunMeasured(c, true);
        IEnumerator RunMeasured(PlaytestContext c, bool focused)
        {
            context = c;
            yield return Until(() => Object.FindFirstObjectByType<CastlePlaySession>()?.Player != null && Services.World != null, "real Game session/world ready");
            try
            {
                rig = new SimulatorRig(false);
                actor = rig.Session.HandActor();
                bridge = actor.GetComponent<CandyFlyingHandBridge>();
                driver = actor.ViewCamera.GetComponent<TrackedPoseDriver>();
                context.Check(bridge && bridge.PoseSource == null && driver, "Readiness: real bridge without custom pose override and real TPD");
                context.Check(!driver.ignoreTrackingState && driver.trackingType == TrackedPoseDriver.TrackingType.RotationAndPosition,
                    "Readiness: actual prefab honors positional tracking state");
                simulatedState = typeof(XRInteractionSimulator).GetField("m_HMDState", Private);
                simulatedEuler = typeof(XRInteractionSimulator).GetField("m_CenterEyeEuler", Private);
                context.Check(simulatedState != null && simulatedState.FieldType == typeof(XRSimulatedHMDState) && simulatedEuler != null,
                    "Readiness: installed XRI3.3.2 simulator state seam available");
                context.Check(typeof(ThirdPersonVRFollow).GetField("baselineReady", Private) != null &&
                    typeof(ThirdPersonVRFollow).GetField("headBaseline", Private) != null,
                    "Readiness: current follow baseline telemetry fields exist");
                rig.Simulator.targetedDeviceInput = (TargetedDevices)0;
                trace = new FlyingCameraReadinessTrace();
                trace.Actor = actor; trace.Bridge = bridge; trace.Session = rig.Session;
                yield return Frames(4);
                trace.Hmd = InputSystem.devices.OfType<XRSimulatedHMD>().Single();
                trace.Data.notes.Add(ManagedFrameObserver.SamplingPoint + "; default TPD updateType=" + driver.updateType + "; no forced BeforeRender update. Render-begin requires actual active camera render, explicit screenshots excluded.");
                if (focused) { rig.Session.SetRole(true); rig.SelectCamera(); }
                foreach (bool left in new[] { false, true })
                {
                    if (focused)
                    {
                        yield return Enter(true, left);
                        yield return FocusedLean(left ? "left-next-lean" : "right-next-lean");
                        continue;
                    }
                    yield return Enter(false, left);
                    var controlResidual = Now().residual;
                    if (controlResidual.magnitude > .02f)
                        trace.Data.notes.Add("CONTROL neutral failed for left=" + left + "; paired samples retained but no isolated delayed-timing conclusion is supported.");
                    yield return Enter(true, left);
                    Check(Vector3.Distance(Now().residual, controlResidual) <= .02f,
                        (left ? "Left" : "Right") + " paired valid-first/delayed neutral residuals agree within0.02");
                    yield return Lean(left ? "left-lean" : "right-lean");
                }
                if (!focused) { yield return Masks(); FiniteSeamProbe(); }
                trace.Phase = "final-evidence"; trace.ExplicitCapture = true;
                try { c.Screenshot(actor.ViewCamera, "flying-camera-readiness-final"); }
                finally { trace.ExplicitCapture = false; }
                trace.Data.notes.Add("Unsupported: system OpenXR recenter, hand-specific calibration command, Quest/Android/haptics/stereo/comfort/performance. Optical mixed input belongs to separate existing smoke; this adapter covers dual-controller normal role timing and both preferences.");
                trace.Data.notes.Add("Observed real BeforeRender input samples=" + trace.Data.rows.Count(x => x.sample == "input-after-before-render-unordered") +
                    "; zero means BeforeRender input timing was NOT exercised even if actual camera rendering occurred. Input-after callbacks have no claimed order relative to TPD; render-begin rows are the final camera outcome.");
            }
            finally
            {
                try { trace?.Save(System.IO.Path.Combine(c.Plan.output, "flying-camera-readiness-trace.json")); }
                finally
                {
                    try { trace?.Dispose(); }
                    finally
                    {
                        try { rig?.Dispose(); }
                        finally
                        {
                            c.Check(ManagedFrameObserver.SaveCleanup(System.IO.Path.Combine(c.Plan.output, "camera-observer-cleanup.json")),
                                "Managed camera observers and owned hook removed before Play Mode exit");
                        }
                    }
                }
            }
            c.Check(failures == 0, "All measured flying-camera readiness outcomes passed; inspect trace and separate seam-probe labels");
        }
    }
    public sealed class FlyingCameraFirstRenderScenario : IPlaytestScenario
    {
        public IEnumerator Run(PlaytestContext context) => new FlyingCameraReadinessScenario().RunFirstRender(context);
    }
    public sealed class FlyingCameraFiniteScenario : IPlaytestScenario
    {
        public IEnumerator Run(PlaytestContext context) => new FlyingCameraReadinessScenario().RunFiniteOnly(context);
    }
}
