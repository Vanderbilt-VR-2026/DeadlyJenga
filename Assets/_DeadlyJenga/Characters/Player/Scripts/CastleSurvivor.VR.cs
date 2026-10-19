using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

namespace DeadlyJenga.CastlePrototype
{
    public enum SurvivorVRTurn { Snap, Smooth, PhysicalOnly }
    public enum SurvivorVRMovement { HeadRelative, BodyRelative }
    public enum SurvivorVRPlayStyle { Seated, Standing }
    public enum SurvivorJumpGesture { IndexPinch, Fist }
    public sealed partial class CastleSurvivor
    {
        [Header("VR comfort • head looking is always tracked")]
        public SurvivorVRTurn vrTurn=SurvivorVRTurn.Smooth;
        public SurvivorVRMovement vrMovement=SurvivorVRMovement.HeadRelative;
        [Range(15,90)] public float vrSnapAngle=30;
        [Range(15,180)] public float vrSmoothDegreesPerSecond=60;
        public SurvivorVRPlayStyle vrPlayStyle=SurvivorVRPlayStyle.Seated;
        public SurvivorJumpGesture jumpGesture=SurvivorJumpGesture.IndexPinch;
        public InputAction seatedJump=new InputAction("Jump / hold to climb",InputActionType.Button,"<XRController>{RightHand}/primaryButton");
        [Min(.1f)] public float vrWalkSpeed=2.5f;
        [Range(.05f,.4f)] public float vrMoveDeadzone=.18f;
        [Min(.1f)] public float vrVaultDuration=.85f;
        [Tooltip("Pinch thresholds are real hand metres, independent of avatar scale.")]
        public float jumpPinchPress=.023f,jumpPinchRelease=.032f;
        [Range(0,1)] public float jumpFistPress=.7f,jumpFistRelease=.5f;
        bool jumpArmed,gestureHeld,previousJumpHeld;
        bool vrInputFocused=true;
        void OnApplicationFocus(bool focused){vrInputFocused=focused;if(!focused)ResetVRJump();}
        public void CycleJumpGesture(){jumpGesture=jumpGesture==SurvivorJumpGesture.IndexPinch?SurvivorJumpGesture.Fist:SurvivorJumpGesture.IndexPinch;ResetVRJump();}
        bool calibrateSeated;
        float seatedHeightOffset;
        public float ActiveVaultDuration=>(IsVR&&!externallyDriven?Mathf.Max(.1f,vrVaultDuration):Mathf.Max(.1f,vaultDuration))/vaultMultiplier;
        public string VRControlDescription=>vrPlayStyle==SurvivorVRPlayStyle.Standing
            ? "Standing: left stick moves; turn physically; right "+jumpGesture+" jumps / holds to climb"
            : "Seated: left stick moves; right stick turns smoothly; jump button holds to climb";
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
            if(!IsVR||externallyDriven||InputSuspended||!viewCamera||Climbing||Vaulting||vrPlayStyle==SurvivorVRPlayStyle.Standing)return;
            if(Mathf.Abs(axis)<.3f)turnReady=true;
            float angle=0;
            if(vrTurn==SurvivorVRTurn.Snap&&Mathf.Abs(axis)>.7f&&turnReady)
            {angle=Mathf.Sign(axis)*Mathf.Clamp(vrSnapAngle,15,90);turnReady=false;}
            if(vrTurn==SurvivorVRTurn.Smooth&&Mathf.Abs(axis)>.2f)
                angle=Mathf.Sign(axis)*Mathf.InverseLerp(.2f,1,Mathf.Abs(axis))*Mathf.Clamp(vrSmoothDegreesPerSecond,15,180)*Mathf.Max(0,dt);
            if(angle!=0)transform.RotateAround(viewCamera.transform.position,Vector3.up,angle);
        }
        public void UseStationaryVR()=>SetVRPlayStyle(SurvivorVRPlayStyle.Seated);
        public void UseStandingVR()=>SetVRPlayStyle(SurvivorVRPlayStyle.Standing);
        public void SetVRPlayStyle(SurvivorVRPlayStyle style,bool remember=true)
        {
            if(Climbing||Vaulting){ActionStatus="Finish traversal before switching controls";return;}
            if(style==SurvivorVRPlayStyle.Standing&&cameraOffset)
            {cameraOffset.localPosition-=Vector3.up*seatedHeightOffset;seatedHeightOffset=0;}
            vrPlayStyle=style;vrMovement=style==SurvivorVRPlayStyle.Standing?SurvivorVRMovement.HeadRelative:SurvivorVRMovement.BodyRelative;
            vrTurn=style==SurvivorVRPlayStyle.Standing?SurvivorVRTurn.PhysicalOnly:SurvivorVRTurn.Smooth;
            turnReady=false;ResetVRJump();calibrateSeated=style==SurvivorVRPlayStyle.Seated;
            if(remember)PlayerPrefs.SetInt("SurvivorVRPlayStyle",(int)style);
        }
        public void CalibrateSeatedHeight()
        {
            if(!IsVR||vrPlayStyle!=SurvivorVRPlayStyle.Seated||Climbing||Vaulting)return;
            float adjustment=eyeHeight*playerScale-transform.InverseTransformPoint(viewCamera.transform.position).y;
            cameraOffset.localPosition+=Vector3.up*adjustment;seatedHeightOffset+=adjustment;calibrateSeated=false;
        }
        void UpdateSeatedHeight()
        {
            if(calibrateSeated&&viewCamera.transform.localPosition.y>.3f)CalibrateSeatedHeight();
        }
        public Vector2 FilterVRMove(Vector2 stick)
        {
            float amount=Mathf.InverseLerp(vrMoveDeadzone,1,stick.magnitude);
            return stick.normalized*amount;
        }
        public void ResetVRJump(){jumpArmed=gestureHeld=previousJumpHeld=false;}
        public void ReadVRJump(DeadlyJenga.CandyPlayers.CandyHandPose hand,bool buttonHeld,out bool pressed,out bool held)
        {
            pressed=held=false;
            if(InputSuspended||!vrInputFocused){ResetVRJump();return;}
            if(vrPlayStyle==SurvivorVRPlayStyle.Standing)
            {
                if(!hand.tracked||!hand.skeletonTracked){ResetVRJump();return;}
                if(jumpGesture==SurvivorJumpGesture.IndexPinch)
                    gestureHeld=Vector3.Distance(hand.skeleton.Joint(3),hand.skeleton.Joint(7))<(gestureHeld?jumpPinchRelease:jumpPinchPress);
                else
                {
                    float curl=0;
                    for(int finger=1;finger<5;finger++)
                    {
                        int j=finger*4;float length=0;
                        for(int k=0;k<3;k++)length+=Vector3.Distance(hand.skeleton.Joint(j+k),hand.skeleton.Joint(j+k+1));
                        if(length<.015f){ResetVRJump();return;}
                        float straight=Vector3.Distance(hand.skeleton.Joint(j),hand.skeleton.Joint(j+3))/length;
                        curl+=Mathf.InverseLerp(.9f,.45f,straight)*.25f;
                    }
                    gestureHeld=curl>(gestureHeld?jumpFistRelease:jumpFistPress);
                }
                buttonHeld=gestureHeld;
            }
            if(!buttonHeld)jumpArmed=true;
            held=jumpArmed&&buttonHeld;pressed=held&&!previousJumpHeld;previousJumpHeld=held;
        }
        public void UseFluidVR(){SetVRPlayStyle(SurvivorVRPlayStyle.Seated);vrMovement=SurvivorVRMovement.HeadRelative;}
        public void CycleVRTurn(){if(vrPlayStyle==SurvivorVRPlayStyle.Standing)return;vrTurn=(SurvivorVRTurn)(((int)vrTurn+1)%3);turnReady=false;}
        public void CycleVRMovement(){vrMovement=vrMovement==SurvivorVRMovement.HeadRelative?SurvivorVRMovement.BodyRelative:SurvivorVRMovement.HeadRelative;}
    }
}
