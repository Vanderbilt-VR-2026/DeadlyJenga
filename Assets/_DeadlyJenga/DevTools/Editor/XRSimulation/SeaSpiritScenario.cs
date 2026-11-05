using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AgentPlaytest;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using DeadlyJenga.DevTools.Editor;
using Object = UnityEngine.Object;

namespace DeadlyJenga.XRSimulation.Editor
{
    // Observes production Sea Spirit camera/pose conversion. No replacement locomotion model.
    public sealed class SeaSpiritScenario : IPlaytestScenario
    {
        [Serializable] sealed class TransformSample
        {
            public bool exists;
            public string name, path;
            public Vector3 position, localPosition, scale, localScale;
            public Quaternion rotation, localRotation;
        }
        [Serializable] sealed class HandSample
        {
            public bool exists, tracked, skeletonTracked;
            public Vector3 position;
            public Quaternion rotation;
        }
        [Serializable] sealed class SourceSample
        {
            public string name, type;
            public bool exists, customLeftOverride, customRightOverride;
            public TransformSample trackingOrigin;
            public HandSample left, right;
        }
        [Serializable] sealed class DeviceSample
        {
            public string name, layout, usages;
            public int id, trackingState;
            public bool tracked;
            public Vector3 position, eyePosition;
            public Quaternion rotation, eyeRotation, pointerRotation;
        }
        [Serializable] sealed class SubsystemSample
        {
            public string type;
            public bool running, leftTracked, rightTracked, leftWristValid, rightWristValid;
            public Vector3 leftWristPosition, rightWristPosition;
            public Quaternion leftWristRotation, rightWristRotation;
        }
        [Serializable] sealed class CanvasSample
        {
            public bool active, enabled;
            public int id, cameraId;
            public TransformSample transform;
        }
        [Serializable] sealed class Snapshot
        {
            public string phase, deviceMode, selectedMapId, selectedModeId, actualWorldType, actualWorldObject, scenePath;
            public int survivorBotCount;
            public int frame, cameraId, controllerViewId;
            public float time, reach, orbit, viewerDistance, radius, entranceSeconds, orbitDegreesPerSecond, minimumReach, maximumReach;
            public bool spiritActive, entering, playingHand, menuOpen, vr;
            public Vector3 worldCenter, sourceLeftRelative, sourceRightRelative;
            public float sourceSymmetryXError, sourceSymmetryYZError;
            public TransformSample camera, controllerView, observer, bust, leftTarget, rightTarget, leftAnchor, rightAnchor;
            public SourceSample productionSource, survivorSource, flyingHandSource;
            public DeviceSample[] devices;
            public SubsystemSample[] hands;
            public CanvasSample[] worldCanvases;
        }
        [Serializable] sealed class NeutralObservation
        {
            public string phase;
            public int firstFrame, lastFrame;
            public float cameraDrift, leftSourceDrift, rightSourceDrift, targetLeftDrift, targetRightDrift, scaleDrift, orbitDrift;
        }
        [Serializable] sealed class TurnObservation
        {
            public string phase;
            public float rawHeadYawDelta, orbitDelta, cameraDisplacement;
            public Vector3 cameraDelta;
        }
        [Serializable] sealed class Observations
        {
            public string kind = "Actual Game with stock XRI devices/XRHands and explicitly supplemental mixed controller; not Quest or APK validation";
            public string limits = "Only actual tracking origins encountered are observed; no floor/device-origin configuration is changed. Neutral drift checks cover source and camera transforms, not a mobile performance/comfort guarantee. Symmetry metrics and head-turn orbit displacement describe current behavior, not a replacement locomotion acceptance rule. World canvases are inventory only; no map-popup contract is assumed.";
            public List<Snapshot> snapshots = new List<Snapshot>();
            public List<NeutralObservation> neutral = new List<NeutralObservation>();
            public List<TurnObservation> turns = new List<TurnObservation>();
            public string[] cleanupFailures;
        }

        readonly Observations observations = new Observations();
        SimulatorRig rig;
        PopupTelemetry popup;
        Keyboard keyboard;
        string output;
        const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static object Member(object owner, string name)
        {
            if (owner == null) return null;
            var type = owner.GetType();
            var property = type.GetProperty(name, Members);
            return property != null ? property.GetValue(owner) : type.GetField(name, Members)?.GetValue(owner);
        }
        static bool Flag(object owner, string name)
        {
            var value = Member(owner, name);
            if (!(value is bool result)) throw new InvalidOperationException("Required boolean unavailable: " + name);
            return result;
        }
        static float Number(object owner, string name)
        {
            if (owner == null) return 0;
            if (Member(owner, name) is float value) return value;
            throw new InvalidOperationException("Required numeric observation unavailable: " + name);
        }
        void SetSpirit(bool active)
        {
            var method = rig.Session.GetType().GetMethod("SetSeaSpirit", Members, null, new[] { typeof(bool) }, null);
            if (method == null) throw new InvalidOperationException("Current session lacks SetSeaSpirit(bool); this scenario requires the Sea Spirit source.");
            method.Invoke(rig.Session, new object[] { active });
        }
        static Component FindSpirit() => Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(x => x && x.enabled && x.GetType().FullName == "DeadlyJenga.CastlePrototype.SeaSpiritController" && Flag(x, "Active"));
        static string Hierarchy(Transform transform) => !transform ? "" : transform.parent ? Hierarchy(transform.parent) + "/" + transform.name : transform.name;
        static TransformSample TransformOf(Transform transform) => new TransformSample
        {
            exists = transform, name = transform ? transform.name : "", path = Hierarchy(transform),
            position = transform ? transform.position : Vector3.zero, rotation = transform ? transform.rotation : Quaternion.identity,
            localPosition = transform ? transform.localPosition : Vector3.zero, localRotation = transform ? transform.localRotation : Quaternion.identity,
            scale = transform ? transform.lossyScale : Vector3.zero, localScale = transform ? transform.localScale : Vector3.zero
        };
        static HandSample HandOf(CandyHandPose pose, bool exists) => new HandSample
        { exists = exists, tracked = pose.tracked, skeletonTracked = pose.skeletonTracked, position = pose.position, rotation = pose.rotation };
        static SourceSample SourceOf(CandyTrackingSource source) => new SourceSample
        {
            exists = source, name = source ? Hierarchy(source.transform) : "", type = source ? source.GetType().FullName : "",
            customLeftOverride = source && source.LeftPoseSource != null, customRightOverride = source && source.RightPoseSource != null,
            trackingOrigin = TransformOf(source ? source.trackingOrigin : null),
            left = HandOf(source ? source.WorldHand(true) : default, source), right = HandOf(source ? source.WorldHand(false) : default, source)
        };
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        static bool Finite(Quaternion value) => Finite(value.x) && Finite(value.y) && Finite(value.z) && Finite(value.w);
        static bool Valid(TransformSample sample) => !sample.exists || Finite(sample.position) && Finite(sample.rotation) && Finite(sample.scale) && sample.scale.x > 0 && sample.scale.y > 0 && sample.scale.z > 0;
        static DeviceSample DeviceOf(UnityEngine.InputSystem.InputDevice device)
        {
            T Read<T>(string control) where T : struct => device.TryGetChildControl<InputControl<T>>(control)?.ReadValue() ?? default;
            return new DeviceSample
            {
                name = device.name, layout = device.layout, usages = string.Join(",", device.usages), id = device.deviceId,
                tracked = Read<float>("isTracked") > .5f, trackingState = Read<int>("trackingState"),
                position = Read<Vector3>("devicePosition"), rotation = Read<Quaternion>("deviceRotation"),
                eyePosition = Read<Vector3>("centerEyePosition"), eyeRotation = Read<Quaternion>("centerEyeRotation"), pointerRotation = Read<Quaternion>("pointerRotation")
            };
        }
        static SubsystemSample HandsOf(XRHandSubsystem subsystem)
        {
            bool leftValid = subsystem.leftHand.GetJoint(XRHandJointID.Wrist).TryGetPose(out var left);
            bool rightValid = subsystem.rightHand.GetJoint(XRHandJointID.Wrist).TryGetPose(out var right);
            return new SubsystemSample { type = subsystem.GetType().FullName, running = subsystem.running,
                leftTracked = subsystem.leftHand.isTracked, rightTracked = subsystem.rightHand.isTracked,
                leftWristValid = leftValid, rightWristValid = rightValid,
                leftWristPosition = left.position, leftWristRotation = left.rotation,
                rightWristPosition = right.position, rightWristRotation = right.rotation };
        }
        Snapshot Record(PlaytestContext context, string phase)
        {
            if (popup != null) popup.Phase = phase;
            var spirit = FindSpirit();
            var camera = rig.Session.ActiveCamera;
            var source = Member(spirit, "source") as CandyTrackingSource;
            var observer = Member(spirit, "observer") as Transform;
            var controllerView = Member(spirit, "view") as Camera;
            var left = Member(spirit, "LeftHand") as Component;
            var right = Member(spirit, "RightHand") as Component;
            var subsystems = new List<XRHandSubsystem>(); SubsystemManager.GetSubsystems(subsystems);
            var sample = new Snapshot
            {
                phase = phase, frame = Time.frameCount, time = Time.unscaledTime,
                selectedMapId = MatchSetup.MapId, selectedModeId = MatchSetup.ModeId,
                actualWorldType = Services.World?.GetType().FullName, actualWorldObject = (Services.World as Component)?.gameObject.name,
                scenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
                survivorBotCount = rig.Session.SurvivorBots?.Count ?? 0,
                spiritActive = Flag(rig.Session, "SeaSpiritActive"), entering = spirit && Flag(spirit, "Entering"),
                playingHand = rig.Session.PlayingHand, menuOpen = rig.Session.MenuOpen, vr = rig.Session.IsVR,
                deviceMode = rig.Simulator ? rig.Simulator.deviceLifecycleManager.deviceMode.ToString() : "disposed",
                cameraId = camera ? camera.GetInstanceID() : 0, camera = TransformOf(camera ? camera.transform : null),
                controllerViewId = controllerView ? controllerView.GetInstanceID() : 0, controllerView = TransformOf(controllerView ? controllerView.transform : null),
                observer = TransformOf(observer), bust = TransformOf(Member(spirit, "BustAnchor") as Transform),
                leftTarget = TransformOf(left ? left.GetComponent<CastleHandActor>()?.motor?.transform : null),
                rightTarget = TransformOf(right ? right.GetComponent<CastleHandActor>()?.motor?.transform : null),
                leftAnchor = TransformOf(Member(spirit, "LeftHandAnchor") as Transform), rightAnchor = TransformOf(Member(spirit, "RightHandAnchor") as Transform),
                reach = Number(spirit, "reach"), orbit = Number(spirit, "orbitAngle"), entranceSeconds = Number(spirit, "entranceSeconds"),
                orbitDegreesPerSecond = Number(spirit, "orbitDegreesPerSecond"), minimumReach = Number(spirit, "minimumReach"), maximumReach = Number(spirit, "maximumReach"),
                worldCenter = Services.World.Center, radius = Services.World.Radius,
                productionSource = SourceOf(source), survivorSource = SourceOf(rig.Player.GetComponent<CandyTrackingSource>()),
                flyingHandSource = SourceOf(rig.Session.HandActor().GetComponent<CandyTrackingSource>()),
                devices = InputSystem.devices.Where(x => x is UnityEngine.InputSystem.XR.XRHMD || x is UnityEngine.InputSystem.XR.XRController).Select(DeviceOf).ToArray(),
                hands = subsystems.Select(HandsOf).ToArray(),
                worldCanvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(x => x.renderMode == RenderMode.WorldSpace).Select(x => new CanvasSample
                    { id = x.GetInstanceID(), enabled = x.enabled, active = x.gameObject.activeInHierarchy,
                        cameraId = x.worldCamera ? x.worldCamera.GetInstanceID() : 0, transform = TransformOf(x.transform) }).ToArray()
            };
            if (camera) sample.viewerDistance = Vector3.Distance(camera.transform.position, sample.worldCenter);
            if (source)
            {
                var l = source.WorldHand(true).position; var r = source.WorldHand(false).position;
                sample.sourceLeftRelative = source.trackingOrigin ? source.trackingOrigin.InverseTransformPoint(l) : l;
                sample.sourceRightRelative = source.trackingOrigin ? source.trackingOrigin.InverseTransformPoint(r) : r;
                sample.sourceSymmetryXError = Mathf.Abs(sample.sourceLeftRelative.x + sample.sourceRightRelative.x);
                sample.sourceSymmetryYZError = new Vector2(sample.sourceLeftRelative.y - sample.sourceRightRelative.y, sample.sourceLeftRelative.z - sample.sourceRightRelative.z).magnitude;
            }
            observations.snapshots.Add(sample);
            context.Check(!sample.spiritActive || spirit && source && sample.observer.exists && sample.controllerView.exists && sample.bust.exists && sample.leftTarget.exists && sample.rightTarget.exists,
                phase + ": active Sea Spirit exposes its actual source, observer, view and target transforms");
            context.Check(camera && Valid(sample.camera) && Valid(sample.observer) && Valid(sample.bust) && Valid(sample.leftTarget) && Valid(sample.rightTarget), phase + ": finite world poses and positive transform scales");
            context.Check(Finite(sample.worldCenter) && Finite(sample.radius) && sample.radius > 0 && Finite(sample.orbit) && Finite(sample.reach), phase + ": finite map/orbit/reach observations");
            if (source)
            {
                context.Check(!sample.productionSource.customLeftOverride && !sample.productionSource.customRightOverride, phase + ": production source has no custom optical overrides");
                context.Check(Finite(sample.productionSource.left.position) && Finite(sample.productionSource.right.position) && Finite(sample.productionSource.left.rotation) && Finite(sample.productionSource.right.rotation) && Valid(sample.productionSource.trackingOrigin), phase + ": finite source hand poses and tracking origin");
            }
            return sample;
        }
        IEnumerator Keys(PlaytestContext context, int frames, params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            yield return context.WaitFrames(frames);
        }
        IEnumerator Capture(PlaytestContext context, string phase, Action<Snapshot> captured = null)
        {
            popup?.RecordBoundary(phase + " editor-submit");
            yield return ManagedFrameObserver.Observe(() =>
            {
                popup?.RecordBoundary(phase + " player-observation");
                var sample = Record(context, phase);
                captured?.Invoke(sample);
            });
        }
        IEnumerator WaitEntrance(PlaytestContext context)
        {
            float deadline = Time.realtimeSinceStartup + 12;
            Component spirit;
            do
            {
                spirit = FindSpirit();
                if (spirit && !Flag(spirit, "Entering")) break;
                yield return context.WaitFrames(1);
            } while (Time.realtimeSinceStartup < deadline);
            context.Check(spirit && !Flag(spirit, "Entering"), "Actual Sea Spirit entrance finishes before pose checks (production duration is observed, not bypassed)");
            rig.SelectCamera();
            yield return context.WaitFrames(8);
        }
        IEnumerator Neutral(PlaytestContext context, string phase)
        {
            yield return Keys(context, 6);
            Snapshot start = null;
            yield return Capture(context, phase + " start", value => start = value);
            context.Check(start.spiritActive && !start.entering && !start.menuOpen && start.productionSource.exists, phase + ": active unsuspended Sea Spirit with its actual source");
            context.Check(start.productionSource.left.tracked && start.productionSource.right.tracked, phase + ": both neutral source hands tracked");
            context.Check(start.sourceRightRelative.x > start.sourceLeftRelative.x, phase + ": neutral left/right source order is not crossed");
            var drift = new NeutralObservation { phase = phase, firstFrame = start.frame };
            for (int i = 0; i < 4; i++)
            {
                yield return context.WaitFrames(8);
                Snapshot next = null;
                yield return Capture(context, phase + " neutral " + i, value => next = value);
                drift.lastFrame = next.frame;
                drift.cameraDrift = Mathf.Max(drift.cameraDrift, Vector3.Distance(start.camera.position, next.camera.position));
                drift.leftSourceDrift = Mathf.Max(drift.leftSourceDrift, Vector3.Distance(start.productionSource.left.position, next.productionSource.left.position));
                drift.rightSourceDrift = Mathf.Max(drift.rightSourceDrift, Vector3.Distance(start.productionSource.right.position, next.productionSource.right.position));
                drift.targetLeftDrift = Mathf.Max(drift.targetLeftDrift, Vector3.Distance(start.leftTarget.position, next.leftTarget.position));
                drift.targetRightDrift = Mathf.Max(drift.targetRightDrift, Vector3.Distance(start.rightTarget.position, next.rightTarget.position));
                drift.scaleDrift = Mathf.Max(drift.scaleDrift, Vector3.Distance(start.camera.scale, next.camera.scale));
                drift.orbitDrift = Mathf.Max(drift.orbitDrift, Mathf.Abs(Mathf.DeltaAngle(start.orbit, next.orbit)));
            }
            observations.neutral.Add(drift);
            context.Check(drift.cameraDrift < .03f && drift.leftSourceDrift < .03f && drift.rightSourceDrift < .03f && drift.scaleDrift < .001f && drift.orbitDrift < .05f,
                phase + ": repeated neutral input does not accumulate camera/source translation, scale or orbit drift within observed window");
        }

        public IEnumerator Run(PlaytestContext context)
        {
            output = Path.Combine(context.Plan.output, "sea-spirit-observations.json");
            if (File.Exists(output)) throw new IOException("Sea Spirit evidence already exists; use a new run folder.");
            var previousKeyboard = Keyboard.current;
            try
            {
                yield return context.WaitUntil(() => Object.FindFirstObjectByType<CastlePlaySession>()?.Player != null && Services.World != null);
                rig = new SimulatorRig(false);
                popup = new PopupTelemetry();
                popup.OutputPath = Path.Combine(context.Plan.output, "sea-popup-telemetry.json");
                keyboard = InputSystem.AddDevice<Keyboard>("Sea Spirit scenario keyboard");
                yield return Keys(context, 8);
                context.Check(popup.SampleCount > 0, "Managed Sea Spirit telemetry sampled the real post-LateUpdate player loop");
                context.Check(!Flag(rig.Session, "SeaSpiritActive"), "Scenario begins outside Sea Spirit mode");
                context.Check(rig.Session.CanEditWorld, "Current Game session allows the real Sea Spirit command");
                yield return Capture(context, "survivor before sea entry");
                SetSpirit(true); rig.SelectCamera();
                context.Check(Flag(rig.Session, "SeaSpiritActive"), "Production SetSeaSpirit(true) accepted; no private update or pose injection");
                yield return Capture(context, "controller entrance requested");
                yield return WaitEntrance(context);
                yield return Neutral(context, "stock controllers");
                context.Screenshot(rig.Session.ActiveCamera, "sea-spirit-stock-controller-camera");

                rig.Simulator.targetedDeviceInput = TargetedDevices.HMD;
                Snapshot before = null;
                yield return Capture(context, "head turn before", value => before = value);
                var hmd = InputSystem.devices.OfType<XRSimulatedHMD>().Single();
                var rawBefore = before.devices.Single(x => x.id == hmd.deviceId).rotation;
                yield return Keys(context, 12, Key.RightArrow);
                yield return Keys(context, 8);
                Snapshot after = null;
                yield return Capture(context, "head turn after", value => after = value);
                var rawAfter = after.devices.Single(x => x.id == hmd.deviceId).rotation;
                var delta = new TurnObservation { phase = "stock HMD right-arrow head turn", rawHeadYawDelta = Mathf.DeltaAngle(rawBefore.eulerAngles.y, rawAfter.eulerAngles.y),
                    orbitDelta = Mathf.DeltaAngle(before.orbit, after.orbit), cameraDelta = after.camera.position - before.camera.position,
                    cameraDisplacement = Vector3.Distance(before.camera.position, after.camera.position) };
                observations.turns.Add(delta);
                context.Check(Mathf.Abs(delta.rawHeadYawDelta) > .1f, "Stock keyboard action actually changed simulated head yaw; resulting orbit/camera displacement is observation only");
                yield return Neutral(context, "controllers after head turn");

                rig.Simulator.targetedDeviceInput = TargetedDevices.LeftDevice;
                yield return Keys(context, 3, Key.LeftBracket);
                yield return Keys(context, 8);
                context.Check(rig.Simulator.deviceLifecycleManager.deviceMode == SimulatedDeviceLifecycleManager.DeviceMode.Hand, "Sample keyboard action selected stock XRHands mode");
                yield return Neutral(context, "stock XRHands");
                var handSample = observations.snapshots.Last();
                context.Check(handSample.productionSource.left.skeletonTracked && handSample.productionSource.right.skeletonTracked && handSample.hands.Any(x => x.running && x.type.Contains("Simulator")), "Actual simulator XRHands skeletons reach the Sea Spirit production reader");
                context.Screenshot(rig.Session.ActiveCamera, "sea-spirit-stock-hands-camera");
                foreach (bool left in new[] { true, false })
                {
                    rig.AddSupplemental(left);
                    yield return Keys(context, 8);
                    Snapshot mixed = null;
                    yield return Capture(context, left ? "left supplemental controller" : "right supplemental controller", value => mixed = value);
                    yield return ManagedFrameObserver.Observe(() => context.Check(HybridXRInput.SingleController && HybridXRInput.SingleControllerLeft == left, "Mirrored mixed fixture has exactly its declared controller side"));
                    context.Check((left ? mixed.productionSource.left : mixed.productionSource.right).tracked && !(left ? mixed.productionSource.left : mixed.productionSource.right).skeletonTracked &&
                        (left ? mixed.productionSource.right : mixed.productionSource.left).skeletonTracked, "Mixed fixture preserves controller palm and opposite stock XRHands skeleton");
                    yield return Neutral(context, left ? "mixed left" : "mixed right");
                    rig.RemoveSupplemental();
                    yield return Keys(context, 5);
                }
                rig.Simulator.leftHandIsTracked = rig.Simulator.rightHandIsTracked = false;
                rig.Simulator.hmdIsTracked = false; rig.Simulator.hmdTrackingState = InputTrackingState.None;
                yield return Keys(context, 6);
                Snapshot lost = null;
                yield return Capture(context, "all stock tracking lost", value => lost = value);
                context.Check(!lost.productionSource.left.tracked && !lost.productionSource.right.tracked, "Sea Spirit source clears both hands on stock tracking loss");
                rig.Simulator.leftHandIsTracked = rig.Simulator.rightHandIsTracked = true;
                rig.Simulator.hmdIsTracked = true; rig.Simulator.hmdTrackingState = InputTrackingState.Position | InputTrackingState.Rotation;
                yield return Keys(context, 8);
                yield return Neutral(context, "stock tracking recovered");
                SetSpirit(false);
                yield return Keys(context, 6);
                context.Check(!Flag(rig.Session, "SeaSpiritActive"), "Production Sea Spirit exit accepted");
                yield return Capture(context, "sea exit to hand role");
                rig.Session.SetRole(false); rig.SelectCamera();
                yield return Keys(context, 6);
                yield return Capture(context, "return to survivor");
                context.Check(rig.Session.ActiveCamera == rig.Player.viewCamera && !rig.Session.PlayingHand, "Sea exit restores survivor active camera through production role command");
                SetSpirit(true); rig.SelectCamera();
                context.Check(Flag(rig.Session, "SeaSpiritActive"), "Production Sea Spirit reentry accepted after survivor return");
                yield return WaitEntrance(context);
                yield return Neutral(context, "XRHands second sea entry");
                SetSpirit(false);
                yield return Keys(context, 5);
                rig.Session.SetRole(false); rig.SelectCamera();
                yield return Keys(context, 5);
                yield return Capture(context, "second sea exit to survivor");
                context.Check(!Flag(rig.Session, "SeaSpiritActive") && !rig.Session.PlayingHand && rig.Session.ActiveCamera == rig.Player.viewCamera,
                    "Repeated Sea Spirit entry/exit leaves the production survivor camera selected");
            }
            finally
            {
                var failures = new List<Exception>();
                void Cleanup(string name, Action action)
                { try { action(); } catch (Exception error) { failures.Add(new InvalidOperationException(name, error)); } }
                Cleanup("Exit Sea Spirit", () => { if (rig != null && rig.Session) { SetSpirit(false); if (Flag(rig.Session, "SeaSpiritActive")) throw new InvalidOperationException("Sea Spirit exit refused during cleanup."); } });
                Cleanup("Remove scenario keyboard", () => { if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard); });
                Cleanup("Restore previous keyboard", () => { if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent(); });
                Cleanup("Save popup telemetry", () => popup?.Save());
                Cleanup("Remove popup observer", () => popup?.Dispose());
                Cleanup("Dispose simulator rig", () => rig?.Dispose());
                Cleanup("Verify observer cleanup before Play exit", () => context.Check(
                    ManagedFrameObserver.SaveCleanup(Path.Combine(context.Plan.output, "sea-observer-cleanup.json")),
                    "Managed Sea Spirit observers and owned hook removed before Play Mode exit"));
                observations.cleanupFailures = failures.Select(x => x.ToString()).ToArray();
                Cleanup("Save Sea Spirit observations", () => File.WriteAllText(output, JsonUtility.ToJson(observations, true)));
                if (failures.Count > 0) throw new AggregateException("Sea Spirit scenario cleanup failed.", failures);
            }
        }
    }
}
