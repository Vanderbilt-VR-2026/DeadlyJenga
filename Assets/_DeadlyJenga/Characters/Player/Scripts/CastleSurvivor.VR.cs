using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using System.Collections.Generic;

namespace DeadlyJenga.CastlePrototype
{
    public enum SurvivorVRTurn { Snap, Smooth, PhysicalOnly }
    public enum SurvivorVRMovement { HeadRelative, BodyRelative }
    public enum SurvivorVRPlayStyle { Seated, Standing }
    public enum SurvivorJumpGesture { IndexPinch, Fist }
    public sealed partial class CastleSurvivor
    {
        [Header("VR comfort Ã¢â‚¬Â¢ head looking is always tracked")]
        public SurvivorVRTurn vrTurn=SurvivorVRTurn.Smooth;
        public SurvivorVRMovement vrMovement=SurvivorVRMovement.HeadRelative;
        [Range(15,90)] public float vrSnapAngle=30;
        [Range(15,360)] public float vrSmoothDegreesPerSecond=120;
        public bool autoDetectHybridControls=true;
        bool hybridProfileActive;
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
        Transform headPitchPivot;
        readonly List<XRInputSubsystem> trackingSystems=new();
        readonly List<XRInputSubsystem> observedTrackingSystems=new();
        float nextTrackingScan;
        bool trackingRebase;
        float rebaseUntil;

        void OnApplicationFocus(bool focused)
        {
            vrInputFocused=focused;
            ClearSprint();
            ResetVRJump();
            if(focused&&IsVR)RequestTrackingRebase();
        }
        void RequestTrackingRebase()
        {
            trackingRebase=true;
            seatedCalibrated=false;
            rebaseUntil=Time.unscaledTime+.18f;
            ResetVRLook();
        }
        void TrackingOriginChanged(XRInputSubsystem subsystem)=>RequestTrackingRebase();
        void ReleaseTrackingSystems()
        {
            foreach(var system in observedTrackingSystems)system.trackingOriginUpdated-=TrackingOriginChanged;
            observedTrackingSystems.Clear();
            nextTrackingScan=0;
        }
        void ObserveTrackingOrigin()
        {
            if(Time.unscaledTime<nextTrackingScan)return;
            nextTrackingScan=Time.unscaledTime+1;
            SubsystemManager.GetSubsystems(trackingSystems);
            foreach(var system in trackingSystems)
            {
                if(!system.running||observedTrackingSystems.Contains(system))continue;
                system.trackingOriginUpdated+=TrackingOriginChanged;
                observedTrackingSystems.Add(system);
            }
        }
        void EnsureHeadPitchPivot()
        {
            if(headPitchPivot||!viewCamera)return;
            var eye=viewCamera.transform;
            headPitchPivot=new GameObject("Head-only joystick pitch").transform;
            headPitchPivot.SetParent(eye.parent,false);
            eye.SetParent(headPitchPivot,false);
        }

        internal Vector3 SafeAvatarHead(Vector3 position)
        {
            if(!IsVR||externallyDriven)return position;
            var local=transform.InverseTransformPoint(position);
            float height=eyeHeight*playerScale;
            if(!vrInputFocused||trackingRebase||!float.IsFinite(local.y)||local.y<-.15f*playerScale||local.y>2.55f*playerScale)
                local.y=height;
            return transform.TransformPoint(local);
        }
        public void CycleJumpGesture(){jumpGesture=jumpGesture==SurvivorJumpGesture.IndexPinch?SurvivorJumpGesture.Fist:SurvivorJumpGesture.IndexPinch;ResetVRJump();}
        bool calibrateSeated;
        float vrLookPitch;
        Vector3 seatedAnchor, seatedHeading=Vector3.forward;
        bool seatedCalibrated;
        Unity.XR.CoreUtils.XROrigin trackingOrigin;
        public float ActiveVaultDuration=>(IsVR&&!externallyDriven?Mathf.Max(.1f,vrVaultDuration):Mathf.Max(.1f,vaultDuration))/vaultMultiplier;
        public string VRControlDescription=>vrPlayStyle==SurvivorVRPlayStyle.Standing
            ? "Standing: move stick; turn physically; "+jumpGesture+" jumps / holds to climb"
            : "Seated: non-dominant stick moves; dominant stick looks; dominant primary jumps / holds to climb";
        InputAction[] headActions=System.Array.Empty<InputAction>();
        public Vector3 MovementForward
        {
            get
            {
                bool vr=IsVR&&!externallyDriven;
                Vector3 direction = !vr ? viewCamera.transform.forward :
                    !vrInputFocused||trackingRebase ? transform.forward :
                    vrMovement==SurvivorVRMovement.BodyRelative
                        ? transform.TransformDirection(vrPlayStyle==SurvivorVRPlayStyle.Seated?seatedHeading:Vector3.forward)
                        : RawHeadForward();
                Vector3 forward=Vector3.ProjectOnPlane(direction,Vector3.up);
                return forward.sqrMagnitude>.025f?forward.normalized:Vector3.ProjectOnPlane(transform.forward,Vector3.up).normalized;
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
            if(!IsVR||externallyDriven||InputSuspended||!viewCamera||Climbing||Vaulting)return;
            if(Mathf.Abs(axis)<.3f)turnReady=true;
            float angle=0;
            if(vrTurn==SurvivorVRTurn.Snap&&Mathf.Abs(axis)>.7f&&turnReady)
            {angle=Mathf.Sign(axis)*Mathf.Clamp(vrSnapAngle,15,90);turnReady=false;}
            if(vrTurn==SurvivorVRTurn.Smooth&&Mathf.Abs(axis)>.2f)
                angle=Mathf.Sign(axis)*Mathf.InverseLerp(.2f,1,Mathf.Abs(axis))*Mathf.Clamp(vrSmoothDegreesPerSecond,15,360)*Mathf.Max(0,dt);
            if(angle!=0)transform.RotateAround(viewCamera.transform.position,Vector3.up,angle);
        }
        public void ApplyVRLook(Vector2 stick,float dt,bool dualController=false)
        {
            if (PlayerPrefs.GetInt("SurvivorVR.InvertLookX") != 0) stick.x = -stick.x;
            if (PlayerPrefs.GetInt("SurvivorVR.InvertLookY") != 0) stick.y = -stick.y;
            ApplyVRTurn(stick.x,dt);
            if(!IsVR||externallyDriven||InputSuspended||trackingRebase||!viewCamera||Climbing||Vaulting||vrTurn==SurvivorVRTurn.PhysicalOnly)return;
            if (dualController && vrPlayStyle==SurvivorVRPlayStyle.Seated && PlayerPrefs.GetInt("SurvivorVR.SeatedDualPitch",0)==0)
            {
                if (vrLookPitch!=0) ResetVRLook();
                return;
            }
            float amount=Mathf.InverseLerp(.25f,1,Mathf.Abs(stick.y));
            float next=Mathf.Clamp(vrLookPitch-Mathf.Sign(stick.y)*amount*Mathf.Clamp(vrSmoothDegreesPerSecond*.65f,65,220)*dt,-65,80);
            if(next==0&&vrLookPitch==0)return;
            vrLookPitch=next;
            if(!headPitchPivot)return;
            headPitchPivot.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);
            Vector3 forward=Vector3.ProjectOnPlane(RawHeadForward(),Vector3.up);
            if(forward.sqrMagnitude<.025f)forward=MovementForward;
            headPitchPivot.RotateAround(viewCamera.transform.position,Vector3.Cross(Vector3.up,forward.normalized),next);
        }
        Vector3 RawHeadForward() => cameraOffset.TransformDirection(viewCamera.transform.localRotation*Vector3.forward);
        public void UseStationaryVR()=>SetVRPlayStyle(SurvivorVRPlayStyle.Seated);
        public void UseStandingVR()=>SetVRPlayStyle(SurvivorVRPlayStyle.Standing);
        void ResetVRLook()
        {
            if(headPitchPivot)headPitchPivot.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);
            vrLookPitch=0;
        }
        public void SetVRPlayStyle(SurvivorVRPlayStyle style,bool remember=true)
        {
            if(Climbing||Vaulting){ActionStatus="Finish traversal before switching controls";return;}
            seatedCalibrated=false;
            if(IsVR)RequestTrackingRebase();
            ClearSprint();ResetVRLook();
            vrPlayStyle=style;vrMovement=style==SurvivorVRPlayStyle.Standing?SurvivorVRMovement.HeadRelative:SurvivorVRMovement.BodyRelative;
            vrTurn=style==SurvivorVRPlayStyle.Standing?SurvivorVRTurn.PhysicalOnly:SurvivorVRTurn.Smooth;
            turnReady=false;ResetVRJump();calibrateSeated=style==SurvivorVRPlayStyle.Seated;
            if(remember)RememberComfort(playStyle:true,movement:true,turn:true);
        }
        public void CalibrateSeatedHeight()
        {
            if(!IsVR||vrPlayStyle!=SurvivorVRPlayStyle.Seated||Climbing||Vaulting)return;
            if(!TryReadHeadPosition(out var raw))return;
            seatedAnchor=raw;
            var forward=Vector3.ProjectOnPlane(transform.InverseTransformDirection(RawHeadForward()),Vector3.up);
            seatedHeading=forward.sqrMagnitude>.025f?forward.normalized:Vector3.forward;
            seatedCalibrated=true;
            calibrateSeated=false;
            ApplySeatedOrigin(raw,true);
        }

        bool FloorTracking()
        {
            if(!trackingOrigin)trackingOrigin=GetComponent<Unity.XR.CoreUtils.XROrigin>();
            return trackingOrigin&&trackingOrigin.CurrentTrackingOriginMode==TrackingOriginModeFlags.Floor;
        }

        bool TryReadHeadPosition(out Vector3 raw)
        {
            raw=viewCamera.transform.localPosition;
            return float.IsFinite(raw.x)&&float.IsFinite(raw.y)&&float.IsFinite(raw.z)&&
                raw.sqrMagnitude<100&&(!FloorTracking()||raw.y>(vrPlayStyle==SurvivorVRPlayStyle.Seated?.30f:.04f)&&raw.y<2.55f);
        }

        void ApplySeatedOrigin(Vector3 raw,bool snap)
        {
            // Preserve small physical head movement. Only ease out drift beyond the seated range.
            Vector3 delta=raw-seatedAnchor;
            var permitted=new Vector3(Mathf.Clamp(delta.x,-.18f,.18f),
                Mathf.Clamp(delta.y,-.24f,.20f),Mathf.Clamp(delta.z,-.18f,.18f));
            var target=Vector3.up*(eyeHeight*playerScale)-(raw-permitted)*playerScale;
            cameraOffset.localPosition=snap?target:Vector3.Lerp(cameraOffset.localPosition,target,1-Mathf.Exp(-12*Time.unscaledDeltaTime));
        }

        void UpdateSeatedHeight()
        {
            ObserveTrackingOrigin();
            if(!vrInputFocused)return;
            bool valid=TryReadHeadPosition(out var raw);
            if(trackingRebase)
            {
                if(Time.unscaledTime<rebaseUntil)return;
                if(vrPlayStyle==SurvivorVRPlayStyle.Seated)
                {
                    if(!valid)return;
                    CalibrateSeatedHeight();
                    if(!seatedCalibrated)return;
                }
                trackingRebase=false;
            }
            if(vrPlayStyle==SurvivorVRPlayStyle.Seated)
            {
                if(calibrateSeated&&valid)CalibrateSeatedHeight();
                if(seatedCalibrated&&valid)ApplySeatedOrigin(raw,false);
            }
            else if(valid)
            {
                cameraOffset.localPosition=Vector3.up*(FloorTracking()?0:eyeHeight*playerScale);
            }
            else if(float.IsFinite(raw.y))
            {
                // Tracking can briefly return zero or an invalid floor sample after recenter.
                cameraOffset.localPosition=Vector3.up*((eyeHeight-Mathf.Clamp(raw.y,-3,3))*playerScale);
            }
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
                if(!hand.tracked||!hand.skeletonTracked)gestureHeld=false;
                else
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
                buttonHeld|=gestureHeld;
            }
            if(!buttonHeld)jumpArmed=true;
            held=jumpArmed&&buttonHeld;pressed=held&&!previousJumpHeld;previousJumpHeld=held;
        }
        void DetectHybridControls()
        {
            if(!autoDetectHybridControls)return;
            if(!candyTracking)candyTracking=GetComponent<DeadlyJenga.CandyPlayers.CandyTrackingSource>();
            if(!candyTracking)return;
            if(Climbing||Vaulting)return;
            if(candyTracking.IsHybrid&&!hybridProfileActive)
            {
                hybridComfort=CaptureComfort();
                SetVRPlayStyle(SurvivorVRPlayStyle.Standing,false);
                hybridProfileActive=true;
            }
            else if(hybridProfileActive&&DeadlyJenga.CandyPlayers.HybridXRInput.DualControllers)
            {
                hybridProfileActive=false;
                RestoreComfort(hybridComfort);
            }
        }
        public void UseFluidVR(){SetVRPlayStyle(SurvivorVRPlayStyle.Seated);vrMovement=SurvivorVRMovement.HeadRelative;RememberComfort(movement:true);}
        public void CycleVRTurn(){vrTurn=(SurvivorVRTurn)(((int)vrTurn+1)%3);turnReady=false;ResetVRLook();RememberComfort(turn:true);}
        public void CycleVRMovement(){vrMovement=vrMovement==SurvivorVRMovement.HeadRelative?SurvivorVRMovement.BodyRelative:SurvivorVRMovement.HeadRelative;RememberComfort(movement:true);}
    }
}
