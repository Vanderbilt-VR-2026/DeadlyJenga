using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AgentPlaytest;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CandyPhysicsHands;
using DeadlyJenga.CastlePrototype;
using Object = UnityEngine.Object;

namespace DeadlyJenga.XRSimulation.Editor
{
    public sealed class HandExpressionDiagnosticScenario : IPlaytestScenario
    {
        static readonly XRHandJointID[] Joints = {
            XRHandJointID.ThumbMetacarpal, XRHandJointID.ThumbProximal, XRHandJointID.ThumbDistal, XRHandJointID.ThumbTip,
            XRHandJointID.IndexProximal, XRHandJointID.IndexIntermediate, XRHandJointID.IndexDistal, XRHandJointID.IndexTip,
            XRHandJointID.MiddleProximal, XRHandJointID.MiddleIntermediate, XRHandJointID.MiddleDistal, XRHandJointID.MiddleTip,
            XRHandJointID.RingProximal, XRHandJointID.RingIntermediate, XRHandJointID.RingDistal, XRHandJointID.RingTip,
            XRHandJointID.LittleProximal, XRHandJointID.LittleIntermediate, XRHandJointID.LittleDistal, XRHandJointID.LittleTip };
        [Serializable] sealed class HandRow
        {
            public bool rawTracked, wristValid, all20Valid, rawProductionValidity, sourceTracked, sourceSkeletonTracked;
            public Vector3 wristPosition, sourcePosition;
            public Quaternion wristRotation, sourceRotation;
            public Vector3[] rawTracking20, rawWristRelative20, normalized20;
            public string[] flags20;
        }
        [Serializable] sealed class ActionRow
        {
            public string name, inputSource, phase, controls;
            public bool enabled, held, performed, completed;
        }
        [Serializable] sealed class Row
        {
            public string phase, inputUpdate, leftExpression, rightExpression, quickAction;
            public int frame;
            public bool o, p, m, shift, applicationFocused, suspended, menu;
            public HandRow left, right;
            public ActionRow[] actions;
        }
        [Serializable] sealed class Document
        {
            public string kind = "Read-only real player-frame stock O/P diagnostic; synthetic non-headset input";
            public string limits = "Fresh real Game+stock hand mode with matching M toggle/release prelude, not the entire earlier smoke state. Raw tracking-space joints and wrist-relative joints are separate from exact production normalized/unscaled joints. Invalid/rejected production poses are recorded, never replaced or made to pass.";
            public string id, project, subsystemType;
            public int pid, sourceId;
            public string[] jointOrder = Joints.Select(x => x.ToString()).ToArray();
            public List<Row> rows = new List<Row>();
        }
        readonly Document data = new Document();
        SimulatorRig rig;
        CandyTrackingSource source;
        XRHandSubsystem subsystem;
        Keyboard keyboard;
        string phase;
        FieldInfo leftState, rightState;
        string Expression(FieldInfo field)
        {
            var state = field.GetValue(rig.Simulator);
            return state.GetType().GetProperty("expressionName").GetValue(state).ToString();
        }
        HandRow Hand(bool left)
        {
            var raw = left ? subsystem.leftHand : subsystem.rightHand;
            bool wristValid = raw.GetJoint(XRHandJointID.Wrist).TryGetPose(out var wrist);
            var normalized = source.WorldHand(left);
            var result = new HandRow { rawTracked = raw.isTracked, wristValid = wristValid, all20Valid = true,
                wristPosition = wrist.position, wristRotation = wrist.rotation,
                sourceTracked = normalized.tracked, sourceSkeletonTracked = normalized.skeletonTracked,
                sourcePosition = normalized.position, sourceRotation = normalized.rotation,
                rawTracking20 = new Vector3[20], rawWristRelative20 = new Vector3[20], normalized20 = new Vector3[20], flags20 = new string[20] };
            var rawPose = new CandyHandPose { tracked = raw.isTracked, skeletonTracked = true, position = wrist.position, rotation = wrist.rotation };
            for (int i = 0; i < 20; i++)
            {
                var joint = raw.GetJoint(Joints[i]);
                bool valid = joint.TryGetPose(out var pose);
                result.all20Valid &= valid;
                result.flags20[i] = joint.trackingState.ToString();
                result.rawTracking20[i] = pose.position;
                result.rawWristRelative20[i] = Quaternion.Inverse(wrist.rotation) * (pose.position - wrist.position);
                result.normalized20[i] = normalized.skeleton.Joint(i);
                rawPose.skeleton.SetJoint(i, result.rawWristRelative20[i]);
            }
            result.rawProductionValidity = wristValid && result.all20Valid && CandyHandTransition.IsValidPose(rawPose);
            return result;
        }
        void Sample()
        {
            if (phase == null || InputState.currentUpdateType == InputUpdateType.Editor) return;
            data.rows.Add(new Row { phase = phase, frame = Time.frameCount, inputUpdate = InputState.currentUpdateType.ToString(),
                leftExpression = Expression(leftState), rightExpression = Expression(rightState), quickAction = rig.Simulator.currentHandExpression.name,
                o = keyboard.oKey.isPressed, p = keyboard.pKey.isPressed, m = keyboard.mKey.isPressed, shift = keyboard.leftShiftKey.isPressed,
                applicationFocused = Application.isFocused, suspended = rig.Player.InputSuspended, menu = rig.Session.MenuOpen,
                left = Hand(true), right = Hand(false), actions = rig.Simulator.handExpressionManager.simulatedHandExpressions.Select(e => {
                    var reader = e.toggleInput;
                    var action = reader.inputActionReferencePerformed ? reader.inputActionReferencePerformed.action : reader.inputActionPerformed;
                    return new ActionRow { name = e.name, inputSource = reader.inputSourceMode.ToString(),
                        phase = action?.phase.ToString(), enabled = action?.enabled ?? false,
                        controls = action == null ? "<absent>" : string.Join(",", action.controls.Select(c => c.path)),
                        held = reader.ReadIsPerformed(), performed = reader.ReadWasPerformedThisFrame(), completed = reader.ReadWasCompletedThisFrame() };
                }).ToArray() });
        }
        IEnumerator Keys(PlaytestContext c, string label, int frames, params Key[] keys)
        {
            phase = label;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            yield return c.WaitFrames(frames);
        }
        public IEnumerator Run(PlaytestContext c)
        {
            var oldKeyboard = Keyboard.current;
            IDisposable observer = null;
            StockExpressionActions expressionActions = null;
            try
            {
                yield return c.WaitUntil(() => Object.FindFirstObjectByType<CastlePlaySession>()?.Player != null);
                rig = new SimulatorRig(false);
                source = rig.Player.GetComponent<CandyTrackingSource>();
                c.Check(source && source.LeftPoseSource == null && source.RightPoseSource == null, "Diagnostic uses actual production source without pose overrides");
                keyboard = InputSystem.AddDevice<Keyboard>("Stock expression diagnostic keyboard");
                data.id = c.Plan.id; data.project = c.Plan.project; data.pid = System.Diagnostics.Process.GetCurrentProcess().Id;
                leftState = typeof(XRInteractionSimulator).GetField("m_LeftHandState", BindingFlags.Instance | BindingFlags.NonPublic);
                rightState = typeof(XRInteractionSimulator).GetField("m_RightHandState", BindingFlags.Instance | BindingFlags.NonPublic);
                c.Check(leftState != null && rightState != null, "Installed stock simulator exposes read-only per-side expression telemetry");
                rig.Simulator.targetedDeviceInput = TargetedDevices.LeftDevice;
                yield return Keys(c, null, 3, Key.LeftBracket);
                yield return Keys(c, null, 6);
                var systems = new List<XRHandSubsystem>(); SubsystemManager.GetSubsystems(systems);
                subsystem = systems.SingleOrDefault(s => s.running && s.GetType().Name.Contains("Simulator"));
                c.Check(subsystem != null, "Actual stock XRHands provider ready for expression diagnostic");
                data.sourceId = source.GetInstanceID(); data.subsystemType = subsystem.GetType().FullName;
                expressionActions = new StockExpressionActions(rig.Simulator, Path.Combine(c.Plan.output, "expression-action-lifecycle.json"));
                observer = ManagedFrameObserver.Register(Sample);
                yield return Keys(c, "pinch prelude held", 3, Key.M);
                yield return Keys(c, "pinch prelude released", 5);
                yield return Keys(c, "pinch toggle-off held", 3, Key.M);
                yield return Keys(c, "pinch toggle-off released", 5);
                yield return Keys(c, "Open O held", 3, Key.O);
                yield return Keys(c, "Open O released", 4);
                yield return Keys(c, "Fist P held", 3, Key.P);
                yield return Keys(c, "Fist P released", 4);
                c.Check(new[] { "Open O held", "Open O released", "Fist P held", "Fist P released" }.All(p => data.rows.Any(r => r.phase == p)),
                    "Held and released O/P phases each have actual native snapshots; this is collection readiness, not an expression PASS");
                var open = data.rows.Last(r => r.phase == "Open O released");
                var fist = data.rows.Last(r => r.phase == "Fist P released");
                c.Check(open.rightExpression == "Open" && fist.rightExpression == "Fist" &&
                    data.rows.Any(r => r.phase == "Open O held" && r.actions.Any(a => a.name == "Open" && a.enabled && a.performed)) &&
                    data.rows.Any(r => r.phase == "Fist P held" && r.actions.Any(a => a.name == "Fist" && a.enabled && a.performed)),
                    "Enabled stock O/P actions perform and persist as actual Open/Fist after release");
                c.Check(open.right.rawProductionValidity && fist.right.rawProductionValidity && open.right.sourceSkeletonTracked && fist.right.sourceSkeletonTracked &&
                    Vector3.Distance(open.right.normalized20[7], fist.right.normalized20[7]) > .005f,
                    "Stock open and fist expressions change actual XRHands finger joints in the production reader");
            }
            finally
            {
                try { File.WriteAllText(Path.Combine(c.Plan.output, "stock-expression-diagnostic.json"), JsonUtility.ToJson(data, true)); }
                finally
                {
                    try { observer?.Dispose(); }
                    finally
                    {
                        try { if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard); if (oldKeyboard != null && oldKeyboard.added) oldKeyboard.MakeCurrent(); }
                        finally
                        {
                            try { rig?.Dispose(); }
                            finally
                            {
                                try { expressionActions?.Dispose(); }
                                finally
                                {
                                    c.Check(ManagedFrameObserver.SaveCleanup(Path.Combine(c.Plan.output, "expression-observer-cleanup.json")), "Expression diagnostic removes owned observers/hooks before Play exit");
                                    c.Check(expressionActions == null || expressionActions.Restored, "Stock Open/Fist action enabled states restored after diagnostic cleanup");
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
