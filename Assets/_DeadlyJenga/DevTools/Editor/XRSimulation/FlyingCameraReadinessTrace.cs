using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyPhysicsHands;
using DeadlyJenga.Core;
using FlyingHand;

namespace DeadlyJenga.XRSimulation.Editor
{
    public sealed class FlyingCameraReadinessTrace : IDisposable
    {
        [Serializable] public sealed class Row
        {
            public string phase, sample, inputUpdate, target, positionTarget, mode, scene, worldType;
            public int frame, state, activeCamera, camera, palm, renderedSequence;
            public bool hmdTracked, baselineReady, menu, left, driverEnabled, explicitCapture;
            public Vector3 raw, centerEye, actionPosition, cameraLocal, cameraWorld, rootPosition, rootScale;
            public Vector3 palmPosition, palmTarget, baseline, desired, residual, rootLocalPosition;
            public Quaternion rawRotation, cameraRotation, cameraLocalRotation, rootRotation, motorRotation;
            public Matrix4x4 cameraParentMatrix;
            public Vector3 worldCenter;
            public float time, water, nearClip, nearestPalmCollider, worldRadius, worldTopHeight;
            public string trackingType, updateType;
        }
        [Serializable] public sealed class Document
        {
            public string kind = "Actual runtime samples if executed; synthetic stock XRI state, not Quest";
            public string fixtureInput = "Reflection sets stock simulator private HMD state; simulator remains sole HMD writer. Real TPD and game callbacks run normally.";
            public string finiteProbe = "Separate inactive-object private-method seam probe; never normal-loop or rendered evidence";
            public List<Row> rows = new List<Row>();
            public List<string> notes = new List<string>();
        }
        public CastleHandActor Actor;
        public CandyFlyingHandBridge Bridge;
        public CastlePlaySession Session;
        public XRSimulatedHMD Hmd;
        public string Phase;
        public bool ExplicitCapture;
        public readonly Document Data = new Document();
        int renders;
        static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        public static bool Ready(ThirdPersonVRFollow follow) => (bool)typeof(ThirdPersonVRFollow).GetField("baselineReady", Private).GetValue(follow);
        public static Vector3 Baseline(ThirdPersonVRFollow follow) => (Vector3)typeof(ThirdPersonVRFollow).GetField("headBaseline", Private).GetValue(follow);
        readonly IDisposable sampling;
        bool disposed;
        public FlyingCameraReadinessTrace()
        {
            sampling = ManagedFrameObserver.Register(SampleAfterLateUpdate);
            RenderPipelineManager.beginCameraRendering += Begin;
            Camera.onPreCull += PreCull;
            InputSystem.onAfterUpdate += AfterInput;
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            RenderPipelineManager.beginCameraRendering -= Begin;
            Camera.onPreCull -= PreCull;
            InputSystem.onAfterUpdate -= AfterInput;
            sampling.Dispose();
        }
        void Begin(ScriptableRenderContext context, Camera camera) { if (GraphicsSettings.currentRenderPipeline) Render(camera); }
        void PreCull(Camera camera) { if (!GraphicsSettings.currentRenderPipeline) Render(camera); }
        void Render(Camera camera)
        {
            if (Actor && camera == Actor.ViewCamera) { renders++; Sample("render-begin"); }
        }
        void AfterInput() { Sample(InputState.currentUpdateType == InputUpdateType.BeforeRender ? "input-after-before-render-unordered" : "input-after-update-unordered"); }
        void SampleAfterLateUpdate() { Sample("player-loop-after-script-late-update"); }
        public Row Sample(string sample)
        {
            if (!Actor || !Actor.follow || !Actor.ViewCamera || Hmd == null || Data.rows.Count >= 20000) return null;
            var follow = Actor.follow;
            var camera = Actor.ViewCamera;
            var driver = camera.GetComponent<TrackedPoseDriver>();
            var physical = Bridge ? Bridge.PhysicalHand : null;
            var palm = physical && physical.bodies.Length > 0 ? physical.bodies[0] : null;
            var q = Quaternion.Euler(0, Actor.motor.transform.eulerAngles.y, 0);
            var desired = (palm ? palm.position : follow.FollowPosition) + q * new Vector3(0, .22f, -follow.handViewDistance);
            float water = Services.World == null ? float.NegativeInfinity : Services.World.WaterHeight(camera.transform.position) + .08f;
            var row = new Row
            {
                phase = Phase, sample = sample, inputUpdate = InputState.currentUpdateType.ToString(),
                frame = Time.frameCount, time = Time.unscaledTime, renderedSequence = renders, explicitCapture = ExplicitCapture,
                state = Hmd.trackingState.ReadValue(), hmdTracked = Hmd.isTracked.isPressed,
                raw = Hmd.devicePosition.ReadValue(), centerEye = Hmd.centerEyePosition.ReadValue(), rawRotation = Hmd.centerEyeRotation.ReadValue(),
                camera = camera.GetInstanceID(), activeCamera = Session && Session.ActiveCamera ? Session.ActiveCamera.GetInstanceID() : 0,
                cameraLocal = camera.transform.localPosition, cameraWorld = camera.transform.position,
                cameraRotation = camera.transform.rotation, cameraLocalRotation = camera.transform.localRotation,
                rootPosition = follow.transform.position, rootLocalPosition = follow.transform.localPosition,
                rootRotation = follow.transform.rotation, rootScale = follow.transform.lossyScale,
                cameraParentMatrix = camera.transform.parent.localToWorldMatrix,
                motorRotation = Actor.motor.transform.rotation, baselineReady = Ready(follow), baseline = Baseline(follow),
                target = follow.target ? follow.target.GetInstanceID() + ":" + follow.target.name : "none",
                positionTarget = follow.positionTarget ? follow.positionTarget.GetInstanceID() + ":" + follow.positionTarget.name : "none",
                palm = palm ? palm.GetInstanceID() : 0, palmPosition = palm ? palm.position : Vector3.zero,
                palmTarget = physical ? physical.Target.position : Vector3.zero,
                desired = desired, residual = camera.transform.position - desired, water = float.IsFinite(water) ? water : -999999,
                nearClip = camera.nearClipPlane, nearestPalmCollider = -1,
                menu = Session && Session.MenuOpen, left = Bridge && Bridge.useLeftHand,
                mode = Actor.motor.ActiveControlMode + "/" + (Bridge ? Bridge.Mode.ToString() : "none"),
                scene = camera.gameObject.scene.path, worldType = Services.World?.GetType().FullName ?? "none",
                worldCenter = Services.World?.Center ?? Vector3.zero, worldRadius = Services.World?.Radius ?? 0,
                worldTopHeight = Services.World?.TopHeight ?? 0,
                driverEnabled = driver && driver.enabled, trackingType = driver ? driver.trackingType.ToString() : "none",
                updateType = driver ? driver.updateType.ToString() : "none",
                actionPosition = driver && driver.positionInput.action != null && driver.positionInput.action.enabled ? driver.positionInput.action.ReadValue<Vector3>() : Vector3.zero
            };
            if (palm)
            {
                var colliders = palm.GetComponents<Collider>().Where(x => x.enabled && !x.isTrigger).ToArray();
                if (colliders.Length > 0) row.nearestPalmCollider = colliders.Min(x => Vector3.Distance(camera.transform.position, x.ClosestPoint(camera.transform.position)));
            }
            Data.rows.Add(row);
            return row;
        }
        public void Save(string path) { File.WriteAllText(path, JsonUtility.ToJson(Data, true)); }
    }
}
