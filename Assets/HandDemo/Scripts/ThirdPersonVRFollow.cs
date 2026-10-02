using UnityEngine;
using Unity.XR.CoreUtils;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;

namespace FlyingHand
{
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class ThirdPersonVRFollow : MonoBehaviour
    {
        public Transform target;
        [Min(0)] public float distance = 6.5f;
        [Min(0)] public float height = 2f;
        [Min(.01f)] public float positionSmoothTime = .18f;
        [Min(0)] public float yawResponse = 4f;
        public bool followYaw = true;
        [Header("Desktop view")]
        public FlyingHandController controller;
        public Camera viewCamera;
        [Min(0)] public float mousePitchSensitivity = .12f;
        public float desktopPitch = 18f;
        public Vector2 pitchLimits = new Vector2(-35, 75);
        TrackedPoseDriver poseDriver;
        bool originalDriverEnabled;
        Vector3 trackedLocalPosition;
        Quaternion trackedLocalRotation;
        StereoTargetEyeMask originalTargetEye;
        bool ownsStereoTarget;
        bool desktopView;
        bool viewInitialized;
        Vector3 velocity;
        float yaw;
        void OnEnable()
        {
            if (!controller && target) controller = target.GetComponentInParent<FlyingHandController>();
            if (!viewCamera) { var origin = GetComponent<XROrigin>(); if (origin) viewCamera = origin.Camera; }
            if (!viewCamera) viewCamera = GetComponentInChildren<Camera>(true);
            if (!target || !viewCamera)
            {
                Debug.LogError("FlyingHand follow requires a target and a camera in its rig.", this);
                enabled = false;
                return;
            }
            poseDriver = viewCamera.GetComponent<TrackedPoseDriver>();
            originalDriverEnabled = poseDriver && poseDriver.enabled;
            trackedLocalPosition = viewCamera.transform.localPosition;
            trackedLocalRotation = viewCamera.transform.localRotation;
            // stereoTargetEye is a Built-in renderer API. SRPs own their XR
            // rendering; with no running display they already render mono.
            ownsStereoTarget = GraphicsSettings.currentRenderPipeline == null;
            if (ownsStereoTarget) originalTargetEye = viewCamera.stereoTargetEye;
            if (controller) controller.ControlModeChanged += ApplyViewMode;
            ApplyViewMode(controller ? controller.ActiveControlMode : ControlMode.VR);
        }
        void Start() { SnapToTarget(); }
        void ApplyViewMode(ControlMode mode)
        {
            if (!viewCamera) return;
            bool desktop = mode == ControlMode.Desktop;
            if (viewInitialized && desktop == desktopView) return;
            desktopView = desktop; viewInitialized = true;
            if (poseDriver) poseDriver.enabled = !desktop && originalDriverEnabled;
            if (ownsStereoTarget) viewCamera.stereoTargetEye = desktop ? StereoTargetEyeMask.None : originalTargetEye;
            viewCamera.transform.localPosition = desktop ? Vector3.zero : trackedLocalPosition;
            viewCamera.transform.localRotation = desktop ? Quaternion.Euler(desktopPitch, 0, 0) : trackedLocalRotation;
        }
        void OnDisable()
        {
            if (controller) controller.ControlModeChanged -= ApplyViewMode;
            if (viewInitialized && viewCamera)
            {
                if (poseDriver) poseDriver.enabled = originalDriverEnabled;
                if (ownsStereoTarget) viewCamera.stereoTargetEye = originalTargetEye;
                if (desktopView) { viewCamera.transform.localPosition = trackedLocalPosition; viewCamera.transform.localRotation = trackedLocalRotation; }
            }
            viewInitialized = false;
        }
        public void SnapToTarget()
        {
            if (!target) return;
            yaw = target.eulerAngles.y;
            Quaternion rotation = Quaternion.Euler(0, yaw, 0);
            transform.SetPositionAndRotation(target.position + rotation * new Vector3(0, height, -distance), rotation);
            velocity = Vector3.zero;
        }
        void LateUpdate()
        {
            if (!target) return;
            if (followYaw) yaw = Mathf.LerpAngle(yaw, target.eulerAngles.y, 1 - Mathf.Exp(-yawResponse * Time.deltaTime));
            Quaternion rotation = Quaternion.Euler(0, yaw, 0);
            Vector3 desired = target.position + rotation * new Vector3(0, height, -distance);
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, positionSmoothTime, Mathf.Infinity, Time.deltaTime);
            transform.rotation = rotation;
            if (controller) ApplyViewMode(controller.ActiveControlMode);
            if (desktopView && viewCamera && controller)
            {
                desktopPitch = Mathf.Clamp(desktopPitch - controller.DesktopLookDelta.y * mousePitchSensitivity, pitchLimits.x, pitchLimits.y);
                viewCamera.transform.localPosition = Vector3.zero;
                viewCamera.transform.localRotation = Quaternion.Euler(desktopPitch, 0, 0);
            }
            // In VR only this parent moves; tracked camera pose remains untouched.
        }
    }
}
