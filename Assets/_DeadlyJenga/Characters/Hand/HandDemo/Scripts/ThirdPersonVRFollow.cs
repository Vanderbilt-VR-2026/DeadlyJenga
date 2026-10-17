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
        [Tooltip("Optional visible-hand position target. Heading still comes from target, never wrist roll/pitch.")]
        public Transform positionTarget;
        [Header("Bounded observer view")]
        public bool compactHandView;
        public float handViewDistance=3.5f,handViewHeight=1.25f,maximumFollowLag=1.2f;
        public bool vrSnapYaw=true;
        public float vrYawStep=30;
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
        Vector3 headBaseline;
        bool baselineReady;
        public bool DesktopView=>desktopView;
        public Vector3 FollowPosition=>positionTarget?positionTarget.position:target?target.position:transform.position;
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
        public void ApplyViewMode(ControlMode mode)
        {
            if (!viewCamera) return;
            bool desktop = mode == ControlMode.Desktop;
            if (viewInitialized && desktop == desktopView) return;
            desktopView = desktop; viewInitialized = true;
            baselineReady=false;
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
            if(viewInitialized&&!desktopView)CaptureHeadBaseline();
            Vector3 desired=FollowPosition+rotation*new Vector3(0,compactHandView?handViewHeight:height,-(compactHandView?handViewDistance:distance));
            if(viewInitialized)desired-=ViewOffset(rotation);
            transform.SetPositionAndRotation(desired, rotation);
            velocity = Vector3.zero;
        }
        void CaptureHeadBaseline()
        {
            if(!viewCamera)return;
            headBaseline=transform.InverseTransformPoint(viewCamera.transform.position);baselineReady=true;
        }
        Vector3 ViewOffset(Quaternion rotation)
        {
            // Desktop also has an XR camera-floor parent; account for it instead of
            // accidentally adding another 1.6 metres to the observer's eye height.
            Vector3 local=desktopView?transform.InverseTransformPoint(viewCamera.transform.position):headBaseline;
            return rotation*Vector3.Scale(local,transform.lossyScale);
        }
        public void StepFollow(float dt)
        {
            if(!target||!viewCamera)return;
            if(controller)ApplyViewMode(controller.ActiveControlMode);
            bool vr=!desktopView;
            if(vr&&!baselineReady)CaptureHeadBaseline();
            if(followYaw)
            {
                float delta=Mathf.DeltaAngle(yaw,target.eulerAngles.y);
                if(vr&&vrSnapYaw){float step=Mathf.Max(15,vrYawStep);if(Mathf.Abs(delta)>=step)yaw+=Mathf.Round(delta/step)*step;}
                else yaw=Mathf.LerpAngle(yaw,target.eulerAngles.y,1-Mathf.Exp(-yawResponse*dt));
            }
            Quaternion rotation=Quaternion.Euler(0,yaw,0);
            float d=compactHandView?handViewDistance:distance,h=compactHandView?handViewHeight:height;
            Vector3 desired=FollowPosition+rotation*new Vector3(0,h,-d);
            desired-=ViewOffset(rotation);
            transform.position=Vector3.SmoothDamp(transform.position,desired,ref velocity,positionSmoothTime,Mathf.Infinity,dt);
            // Bounded lag keeps a blocked/fast-moving visible hand within the observer's reach.
            transform.position=desired+Vector3.ClampMagnitude(transform.position-desired,Mathf.Max(.1f,maximumFollowLag));
            transform.rotation=rotation;
        }
        void LateUpdate()
        {
            if (!target) return;
            StepFollow(Time.deltaTime);
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
