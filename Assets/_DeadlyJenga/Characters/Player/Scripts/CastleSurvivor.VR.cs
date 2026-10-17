using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

namespace DeadlyJenga.CastlePrototype
{
    public enum SurvivorVRTurn { Snap, Smooth, PhysicalOnly }
    public enum SurvivorVRMovement { HeadRelative, BodyRelative }
    public sealed partial class CastleSurvivor
    {
        [Header("VR comfort • head looking is always tracked")]
        public SurvivorVRTurn vrTurn=SurvivorVRTurn.Snap;
        public SurvivorVRMovement vrMovement=SurvivorVRMovement.HeadRelative;
        [Range(15,90)] public float vrSnapAngle=30;
        [Range(15,180)] public float vrSmoothDegreesPerSecond=60;
        InputAction[] headActions=System.Array.Empty<InputAction>();
        public Vector3 MovementForward
        {
            get
            {
                var basis=IsVR&&!externallyDriven&&vrMovement==SurvivorVRMovement.BodyRelative?transform:viewCamera.transform;
                Vector3 forward=Vector3.ProjectOnPlane(basis.forward,Vector3.up);
                return forward.sqrMagnitude>.001f?forward.normalized:Vector3.ProjectOnPlane(transform.forward,Vector3.up).normalized;
            }
        }
        // Input-only and center-eye backends both work. PassThrough avoids value-action
        // arbitration between unit quaternions. TPD remains the sole head pose writer.
        public static InputAction[] ConfigureHeadTracking(TrackedPoseDriver driver)
        {
            if(!driver)return System.Array.Empty<InputAction>();bool enabled=driver.enabled;driver.enabled=false;
            var position=new InputAction("Survivor head position",InputActionType.PassThrough,expectedControlType:"Vector3");
            position.AddBinding("<XRHMD>/devicePosition");position.AddBinding("<XRHMD>/centerEyePosition");
            var rotation=new InputAction("Survivor head rotation",InputActionType.PassThrough,expectedControlType:"Quaternion");
            rotation.AddBinding("<XRHMD>/deviceRotation");rotation.AddBinding("<XRHMD>/centerEyeRotation");
            var state=new InputAction("Survivor head tracking",InputActionType.PassThrough,"<XRHMD>/trackingState",expectedControlType:"Integer");
            driver.positionInput=new InputActionProperty(position);driver.rotationInput=new InputActionProperty(rotation);driver.trackingStateInput=new InputActionProperty(state);
            driver.trackingType=TrackedPoseDriver.TrackingType.RotationAndPosition;driver.updateType=TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;driver.ignoreTrackingState=false;driver.enabled=enabled;
            return new[]{position,rotation,state};
        }
        // Joystick yaw moves the origin about the head; it never writes camera-local pose,
        // applies roll, follows head yaw a second time, or orbits a seated player's eyes.
        public void ApplyVRTurn(float axis,float dt)
        {
            if(!IsVR||externallyDriven||InputSuspended||!viewCamera)return;
            if(Mathf.Abs(axis)<.3f)turnReady=true;
            float angle=0;
            if(vrTurn==SurvivorVRTurn.Snap&&Mathf.Abs(axis)>.7f&&turnReady)
            {angle=Mathf.Sign(axis)*Mathf.Clamp(vrSnapAngle,15,90);turnReady=false;}
            if(vrTurn==SurvivorVRTurn.Smooth&&Mathf.Abs(axis)>.2f)
                angle=Mathf.Sign(axis)*Mathf.InverseLerp(.2f,1,Mathf.Abs(axis))*Mathf.Clamp(vrSmoothDegreesPerSecond,15,180)*Mathf.Max(0,dt);
            if(angle!=0)transform.RotateAround(viewCamera.transform.position,Vector3.up,angle);
        }
        public void UseStationaryVR(){vrMovement=SurvivorVRMovement.BodyRelative;vrTurn=SurvivorVRTurn.Snap;turnReady=false;}
        public void UseFluidVR(){vrMovement=SurvivorVRMovement.HeadRelative;vrTurn=SurvivorVRTurn.Smooth;turnReady=false;}
        public void CycleVRTurn(){vrTurn=(SurvivorVRTurn)(((int)vrTurn+1)%3);turnReady=false;}
        public void CycleVRMovement(){vrMovement=vrMovement==SurvivorVRMovement.HeadRelative?SurvivorVRMovement.BodyRelative:SurvivorVRMovement.HeadRelative;}
    }
}
