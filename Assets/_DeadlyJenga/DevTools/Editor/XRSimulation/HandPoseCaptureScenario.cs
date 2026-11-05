using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AgentPlaytest;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using Object = UnityEngine.Object;

namespace DeadlyJenga.XRSimulation.Editor
{
    public sealed class HandPoseCaptureScenario : IPlaytestScenario
    {
        static readonly XRHandJointID[] JointIds =
        {
            XRHandJointID.ThumbMetacarpal, XRHandJointID.ThumbProximal, XRHandJointID.ThumbDistal, XRHandJointID.ThumbTip,
            XRHandJointID.IndexProximal, XRHandJointID.IndexIntermediate, XRHandJointID.IndexDistal, XRHandJointID.IndexTip,
            XRHandJointID.MiddleProximal, XRHandJointID.MiddleIntermediate, XRHandJointID.MiddleDistal, XRHandJointID.MiddleTip,
            XRHandJointID.RingProximal, XRHandJointID.RingIntermediate, XRHandJointID.RingDistal, XRHandJointID.RingTip,
            XRHandJointID.LittleProximal, XRHandJointID.LittleIntermediate, XRHandJointID.LittleDistal, XRHandJointID.LittleTip
        };
        [Serializable] public sealed class PoseValue { public Vector3 position; public Quaternion rotation; }
        [Serializable] public sealed class FrameHand
        {
            public Vector3 position;
            public Quaternion rotation;
            public bool tracked, skeletonTracked;
            public Vector3[] joints20;
        }
        [Serializable] public sealed class AvatarValue
        {
            public Vector3 feet, head;
            public Quaternion headRotation;
            public float scale;
            public bool seated, equipmentHolstered;
            public FrameHand left, right;
        }
        [Serializable] public sealed class HandValue
        {
            public bool left, sourceTracked, sourceSkeletonTracked, rawTracked, allRawJointsValid, pinching;
            public PoseValue rawWristTracking, productionWorld, productionTracking;
            public Vector3[] rawWristRelative20, productionBasis20;
            public string[] rawJointTrackingFlags;
            public float productionThumbIndexDistance;
        }
        [Serializable] public sealed class Snapshot
        {
            public string phase, inputUpdate, subsystemType, capturedUtc;
            public int frame, sourceId;
            public float unscaledTime;
            public bool customLeftPoseSource, customRightPoseSource;
            public Matrix4x4 trackingOriginMatrix;
            public Vector3 trackingOriginScale;
            public HandValue left, right;
            public AvatarValue avatarFrame;
            public PopupTelemetry.InputGates gates;
        }
        [Serializable] public sealed class GateMarker
        { public string phase; public int frame; public PopupTelemetry.InputGates gates; }
        [Serializable] public sealed class Result
        {
            public string kind = "Actual stock XRI optical-style hand subsystem data if executed; synthetic fixture, not physical Quest";
            public string coordinates = "rawWristRelative20 = inverse(raw wrist rotation)*(raw joint position-raw wrist position), unscaled tracking metres. productionBasis20 = exact CandyHandPose.skeleton values, already unscaled/1mm quantized in the production normalized palm basis. Do not divide these joints by trackingOriginScale or treat productionBasis20 as raw wrist axes.";
            public string scope = "Hands-source capture and readonly suspension-gate diagnostic only. Does not validate locomotion, full smoke, physical tracking, rendered skin or APK.";
            public string id, project, scene, worldType, selectedMap, selectedMode, unityVersion;
            public int pid;
            public string[] jointOrder = JointIds.Select(x => x.ToString()).ToArray();
            public bool arrivalWaitBoundReached;
            public List<Snapshot> snapshots = new List<Snapshot>();
            public List<GateMarker> gateMarkers = new List<GateMarker>();
        }
        readonly Result result = new Result();
        SimulatorRig rig;
        PopupTelemetry popup;
        Keyboard keyboard;
        CandyTrackingSource source;
        XRHandSubsystem subsystem;
        CandyPlayerAvatar avatar;

        static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        static bool RotationValid(Quaternion value) => float.IsFinite(value.x) && float.IsFinite(value.y) &&
            float.IsFinite(value.z) && float.IsFinite(value.w) && value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w > .0001f;
        static bool Matches(FrameHand frame, HandValue hand) => frame.tracked && frame.skeletonTracked &&
            Vector3.Distance(frame.position, hand.productionWorld.position) <= .001f &&
            Quaternion.Angle(frame.rotation, hand.productionWorld.rotation) <= .1f &&
            Enumerable.Range(0, 20).All(i => Vector3.Distance(frame.joints20[i], hand.productionBasis20[i]) <= .001f);
        static PoseValue PoseOf(Vector3 position, Quaternion rotation) => new PoseValue { position = position, rotation = rotation };
        static FrameHand FramePose(CandyHandPose pose) => new FrameHand
        {
            position = pose.position, rotation = pose.rotation, tracked = pose.tracked,
            skeletonTracked = pose.skeletonTracked,
            joints20 = Enumerable.Range(0, 20).Select(pose.skeleton.Joint).ToArray()
        };
        AvatarValue AvatarFrame()
        {
            var frame = avatar.LastFrame;
            var equipment = rig.Player.GetComponent<CandyEquipment>();
            return new AvatarValue { feet = frame.feet, head = frame.head, headRotation = frame.headRotation,
                scale = frame.scale, seated = frame.seated, equipmentHolstered = !equipment || !equipment.Equipment.drawn,
                left = FramePose(frame.left), right = FramePose(frame.right) };
        }
        IEnumerator Keys(PlaytestContext context, int frames, params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            yield return context.WaitFrames(frames);
        }
        IEnumerator Gate(PlaytestContext context, string phase)
        {
            popup.Phase = phase;
            yield return ManagedFrameObserver.Observe(() =>
            {
                popup.RecordBoundary(phase + " native");
                result.gateMarkers.Add(new GateMarker { phase = phase, frame = Time.frameCount, gates = popup.CaptureGates() });
            });
        }
        HandValue Hand(PlaytestContext context, bool left)
        {
            var raw = left ? subsystem.leftHand : subsystem.rightHand;
            context.Check(raw.isTracked && raw.GetJoint(XRHandJointID.Wrist).TryGetPose(out _),
                (left ? "Left" : "Right") + " actual XRHands wrist is tracked with valid pose");
            raw.GetJoint(XRHandJointID.Wrist).TryGetPose(out var wrist);
            var production = source.WorldHand(left);
            var origin = source.trackingOrigin;
            var hand = new HandValue
            {
                left = left, rawTracked = raw.isTracked, sourceTracked = production.tracked,
                sourceSkeletonTracked = production.skeletonTracked, pinching = left ? source.LeftPinching : source.RightPinching,
                rawWristTracking = PoseOf(wrist.position, wrist.rotation),
                productionWorld = PoseOf(production.position, production.rotation),
                productionTracking = PoseOf(origin ? origin.InverseTransformPoint(production.position) : production.position,
                    origin ? Quaternion.Inverse(origin.rotation) * production.rotation : production.rotation),
                rawWristRelative20 = new Vector3[20], productionBasis20 = new Vector3[20], rawJointTrackingFlags = new string[20],
                allRawJointsValid = true
            };
            for (int i = 0; i < JointIds.Length; i++)
            {
                var joint = raw.GetJoint(JointIds[i]);
                bool valid = joint.TryGetPose(out var pose);
                hand.rawJointTrackingFlags[i] = joint.trackingState.ToString();
                hand.allRawJointsValid &= valid;
                if (valid) hand.rawWristRelative20[i] = Quaternion.Inverse(wrist.rotation) * (pose.position - wrist.position);
                hand.productionBasis20[i] = production.skeleton.Joint(i);
            }
            context.Check(hand.allRawJointsValid && hand.sourceTracked && hand.sourceSkeletonTracked,
                (left ? "Left" : "Right") + " full 20-joint raw and production skeletons are genuinely tracked");
            context.Check(hand.rawWristRelative20.All(Finite) && hand.productionBasis20.All(Finite) && Finite(wrist.position) &&
                RotationValid(wrist.rotation) && Finite(production.position) && RotationValid(production.rotation),
                (left ? "Left" : "Right") + " all 20 recorded joint positions are finite unscaled metres");
            hand.productionThumbIndexDistance = Vector3.Distance(hand.productionBasis20[3], hand.productionBasis20[7]);
            return hand;
        }
        IEnumerator Capture(PlaytestContext context, string phase, bool leftPinch, bool rightPinch)
        {
            popup.Phase = phase;
            yield return ManagedFrameObserver.Observe(() =>
            {
                context.Check(source.LeftPoseSource == null && source.RightPoseSource == null,
                    phase + ": no custom optical pose source or invented joint overrides");
                context.Check(source.LeftPinching == leftPinch && source.RightPinching == rightPinch,
                    phase + ": actual production pinch predicates match requested hand selection");
                var origin = source.trackingOrigin;
                var sample = new Snapshot
                {
                    phase = phase, frame = Time.frameCount, unscaledTime = Time.unscaledTime,
                    inputUpdate = InputState.currentUpdateType.ToString(), subsystemType = subsystem.GetType().FullName,
                    capturedUtc = DateTime.UtcNow.ToString("O"), sourceId = source.GetInstanceID(),
                    customLeftPoseSource = source.LeftPoseSource != null, customRightPoseSource = source.RightPoseSource != null,
                    trackingOriginMatrix = origin ? origin.localToWorldMatrix : Matrix4x4.identity,
                    trackingOriginScale = origin ? origin.lossyScale : Vector3.one,
                    left = Hand(context, true), right = Hand(context, false), avatarFrame = AvatarFrame(), gates = popup.CaptureGates()
                };
                result.snapshots.Add(sample);
                context.Check(sample.avatarFrame.equipmentHolstered && float.IsFinite(sample.avatarFrame.scale) && sample.avatarFrame.scale > 0 &&
                    Finite(sample.avatarFrame.head) && Finite(sample.avatarFrame.feet) && RotationValid(sample.avatarFrame.headRotation) &&
                    Finite(sample.avatarFrame.left.position) && Finite(sample.avatarFrame.right.position) &&
                    RotationValid(sample.avatarFrame.left.rotation) && RotationValid(sample.avatarFrame.right.rotation) &&
                    sample.avatarFrame.left.skeletonTracked && sample.avatarFrame.right.skeletonTracked &&
                    sample.avatarFrame.left.joints20.All(Finite) && sample.avatarFrame.right.joints20.All(Finite),
                    phase + ": complete actual local avatar LastFrame has holstered equipment and both finite tracked skeletons");
                if (leftPinch && rightPinch)
                {
                    context.Check(sample.left.productionThumbIndexDistance < .023f && sample.right.productionThumbIndexDistance < .023f,
                        "Both pinches contain the actual complete 20-joint production poses, not a two-tip edit stub");
                    context.Check(Matches(sample.avatarFrame.left, sample.left) && Matches(sample.avatarFrame.right, sample.right) &&
                        Vector3.Distance(sample.avatarFrame.left.joints20[3], sample.avatarFrame.left.joints20[7]) < .023f &&
                        Vector3.Distance(sample.avatarFrame.right.joints20[3], sample.avatarFrame.right.joints20[7]) < .023f,
                        "Bilateral actual avatar frame matches source optical wrists/joints within 1mm/.1deg and both pinch gaps below 23mm");
                }
            });
        }
        public IEnumerator Run(PlaytestContext context)
        {
            string output = Path.Combine(context.Plan.output, "stock-hand-poses-20.json");
            var previousKeyboard = Keyboard.current;
            try
            {
                yield return context.WaitUntil(() => Object.FindFirstObjectByType<CastlePlaySession>()?.Player != null && Services.World != null);
                rig = new SimulatorRig(false);
                source = rig.Player.GetComponent<CandyTrackingSource>();
                context.Check(source, "Real active survivor exposes production CandyTrackingSource");
                avatar = rig.Player.GetComponent<CandyPlayerAvatar>();
                context.Check(avatar, "Real active survivor exposes the production local avatar frame");
                rig.Player.GetComponent<CandyEquipment>()?.SetEquipped(false);
                popup = new PopupTelemetry { OutputPath = Path.Combine(context.Plan.output, "hand-pose-gates.json") };
                keyboard = InputSystem.AddDevice<Keyboard>("Focused stock hand pose fixture keyboard");
                result.id = context.Plan.id; result.project = context.Plan.project;
                result.pid = System.Diagnostics.Process.GetCurrentProcess().Id; result.unityVersion = Application.unityVersion;
                result.scene = rig.Player.gameObject.scene.path; result.worldType = Services.World.GetType().FullName;
                result.selectedMap = MatchSetup.MapId; result.selectedMode = MatchSetup.ModeId;
                yield return Keys(context, 4);
                yield return Gate(context, "before normal seated-style and menu-close commands");
                rig.Player.UseStationaryVR();
                rig.Session.SetMenu(false);
                yield return Keys(context, 8);
                yield return Gate(context, "after normal commands and neutral frames");
                // Observe authored arrival completion only; never force focus, input, session updates or time.
                int first = Time.frameCount; float deadline = Time.realtimeSinceStartup + 120;
                while (popup.LatestGates?.arrivalActive == "True" && Time.frameCount - first < 120 && Time.realtimeSinceStartup < deadline)
                    yield return context.WaitFrames(1);
                result.arrivalWaitBoundReached = popup.LatestGates?.arrivalActive == "True";
                yield return Gate(context, "neutral arrival observation window ended; no locomotion verdict");

                rig.Simulator.targetedDeviceInput = TargetedDevices.LeftDevice;
                yield return Keys(context, 3, Key.LeftBracket);
                yield return Keys(context, 6);
                var subsystems = new List<XRHandSubsystem>(); SubsystemManager.GetSubsystems(subsystems);
                subsystem = subsystems.SingleOrDefault(x => x.running && x.GetType().Name.Contains("Simulator"));
                context.Check(rig.Simulator.deviceLifecycleManager.deviceMode == SimulatedDeviceLifecycleManager.DeviceMode.Hand && subsystem != null,
                    "Stock sample input selects actual simulator XRHandSubsystem hand mode");
                yield return ManagedFrameObserver.Observe(() => context.Check(
                    !HybridXRInput.ControllerTracked(true) && !HybridXRInput.ControllerTracked(false),
                    "Stock hand mode has no tracked controller fallback on either source hand"));
                yield return Capture(context, "stock optical neutral full skeletons", false, false);
                yield return Keys(context, 3, Key.M);
                yield return Keys(context, 4);
                yield return Capture(context, "stock RIGHT pinch complete skeleton", false, true);
                yield return Keys(context, 3, Key.LeftShift, Key.M);
                yield return Keys(context, 3, Key.LeftShift);
                yield return Keys(context, 3);
                yield return Capture(context, "stock BILATERAL pinch complete skeletons", true, true);

                yield return Keys(context, 3, Key.M);
                yield return Keys(context, 3);
                yield return Keys(context, 3, Key.LeftShift, Key.M);
                yield return Keys(context, 3, Key.LeftShift);
                yield return Keys(context, 3);
                yield return Capture(context, "stock pinch releases through real provider", false, false);
                yield return Gate(context, "after optical gestures released; still no locomotion verdict");
            }
            finally
            {
                try
                {
                    File.WriteAllText(output, JsonUtility.ToJson(result, true));
                    popup?.Save();
                }
                finally
                {
                    try { popup?.Dispose(); }
                    finally
                    {
                        try
                        {
                            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                            if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
                        }
                        finally
                        {
                            try { rig?.Dispose(); }
                            finally
                            {
                                context.Check(ManagedFrameObserver.SaveCleanup(Path.Combine(context.Plan.output, "hand-pose-observer-cleanup.json")),
                                    "Focused hand capture removed all owned observers/hooks before Play Mode exit");
                            }
                        }
                    }
                }
            }
        }
    }
}
