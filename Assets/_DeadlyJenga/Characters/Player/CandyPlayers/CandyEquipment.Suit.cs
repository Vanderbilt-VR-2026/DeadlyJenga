using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadlyJenga.CandyPlayers
{
    public sealed partial class CandyEquipment
    {
        float nextThrustRefresh;bool thrustRequested;int suitFireSource;
        void SetSuitThrust(bool active)
        {
            if(active!=thrustRequested||active&&Time.unscaledTime>=nextThrustRefresh)
            {
                (Runtime as IPoweredEquipmentRuntime)?.RequestThrust(active);thrustRequested=active;nextThrustRefresh=Time.unscaledTime+.5f;
            }
        }
        bool suitWasFlying;float suitTakeoffUntil;
        bool SuitFlightLeft=>HybridXRInput.SingleController?HybridXRInput.SingleControllerLeft:!left;
        bool SuitShootingLeft
        {
            get
            {
                if(HybridXRInput.SingleController)return !HybridXRInput.SingleControllerLeft;
                if(avatar.tracking)
                {
                    var a=avatar.tracking.WorldHand(true);var b=avatar.tracking.WorldHand(false);
                    if(a.skeletonTracked!=b.skeletonTracked)return a.skeletonTracked;
                }
                return left;
            }
        }
        public Pose SuitMuzzle
        {
            get
            {
                if(motor.IsVR&&avatar.tracking)
                {
                    var hand=avatar.tracking.WorldHand(SuitShootingLeft);
                    if(!hand.skeletonTracked&&avatar.tracking.TryControllerAim(SuitShootingLeft,out var aim))return aim;
                    var outward=hand.rotation*Vector3.down;
                    return new Pose(hand.position+outward*.09f*Scale,Quaternion.LookRotation(outward,hand.rotation*Vector3.forward));
                }
                var eye=avatar.viewCamera.transform;
                var offset=new Vector3(left?.25f:-.25f,-.18f,.40f);
                if(!motor.IsVR&&Local&&Equipment.drawn&&(Mouse.current?.rightButton.isPressed==true||DesktopGamepad.AimHeld)&&
                    TryAimGripOffset(Equipment,left,false,out var aimedOffset))offset=aimedOffset;
                return new Pose(eye.position+eye.rotation*offset*Scale,eye.rotation);
            }
        }
        bool MaintainPausedSuit(EquipmentState state)
        {
            bool hovering=state.kind==EquipmentKind.PoweredArmor&&state.drawn&&suitWasFlying&&
                motor.MaintainPausedHoverFlight(Time.unscaledDeltaTime);
            if(hovering)suitTakeoffUntil=0;
            else{motor.ClearHoverFlight();suitWasFlying=false;}
            return hovering;
        }
        bool SuitTogglePressed()
        {
            if(!HybridXRInput.SecondaryPressed(left))return false;
            // The tablet uses left secondary alone, or right grip + secondary with only a right controller.
            if(left)return HybridXRInput.GripHeld(true);
            return HybridXRInput.ControllerWithStick(true)!=null||!HybridXRInput.GripHeld(false);
        }

        void UpdateSuit(EquipmentState state)
        {
            bool vr=motor.IsVR;
            bool controllerLeft=SuitFlightLeft;
            var keyboard=Keyboard.current;
            if(vr?SuitTogglePressed():(keyboard?.vKey.wasPressedThisFrame==true||DesktopGamepad.HolsterPressed))
            {
                SetEquipped(!state.drawn);
                SetSuitThrust(false);
                suitWasFlying=false;
                return;
            }
            if(!state.drawn){SetSuitThrust(false);suitWasFlying=false;return;}
            SetSuitThrust(true);
            if(!suitWasFlying){suitWasFlying=true;suitTakeoffUntil=Time.time+.45f;}

            var controller=HybridXRInput.Controller(controllerLeft);
            float Axis(string name)=>controller?.TryGetChildControl<UnityEngine.InputSystem.Controls.AxisControl>(name)?.ReadValue()??0;
            Vector2 move=vr?HybridXRInput.Stick(controllerLeft):new Vector2(
                (keyboard?.dKey.isPressed==true?1:0)-(keyboard?.aKey.isPressed==true?1:0),
                (keyboard?.wKey.isPressed==true?1:0)-(keyboard?.sKey.isPressed==true?1:0));
            if(!vr)move=Vector2.ClampMagnitude(move+DesktopGamepad.Move,1);
            float magnitude=Mathf.InverseLerp(vr?SuitMoveDeadzone:0f,1f,move.magnitude);
            bool sprinting=motor.SprintRequested||keyboard?.leftShiftKey.isPressed==true||!vr&&DesktopGamepad.SprintHeld;
            float speedMult=sprinting?2.1f:1f;
            var forward=motor.MovementForward;
            var right=Vector3.Cross(Vector3.up,forward);
            var horizontal=(right*move.normalized.x+forward*move.normalized.y)*(5f*speedMult*magnitude);
            float lift=vr?Axis("trigger")-Axis("grip"):
                (keyboard?.eKey.isPressed==true?1:0)-(keyboard?.qKey.isPressed==true?1:0);
            if(!vr)lift=Mathf.Clamp(lift+DesktopGamepad.Lift,-1,1);
            float climbSpeed=lift>=0?(sprinting?6f:3.5f):(sprinting?4.5f:3f);
            float climb=Mathf.Sign(lift)*Mathf.InverseLerp(SuitLiftDeadzone,1f,Mathf.Abs(lift))*climbSpeed;
            motor.ApplyHoverFlight(horizontal+Vector3.up*climb,Time.deltaTime,Time.time<suitTakeoffUntil);

            var shootHand=avatar.tracking?avatar.tracking.WorldHand(SuitShootingLeft):default;
            bool shooting=vr?shootHand.tracked&&shootHand.skeletonTracked&&(SuitShootingLeft?avatar.tracking.LeftPinching:avatar.tracking.RightPinching):(Mouse.current?.leftButton.isPressed==true||DesktopGamepad.FireHeld)&&(Cursor.lockState==CursorLockMode.Locked||DesktopGamepad.Current!=null);
            // Two-controller rigs retain a support-trigger fallback when optical hand tracking is unavailable.
            if(vr&&shootHand.tracked&&!shootHand.skeletonTracked)shooting=(SuitShootingLeft==left?trigger:offTrigger).IsPressed();
            var fireController=vr&&!shootHand.skeletonTracked?HybridXRInput.Controller(SuitShootingLeft):null;
            bool available=vr?shootHand.tracked&&(shootHand.skeletonTracked||fireController!=null):
                DesktopGamepad.Current!=null||Mouse.current!=null&&Mouse.current.enabled&&Cursor.lockState==CursorLockMode.Locked;
            int source=vr?(shootHand.skeletonTracked?(SuitShootingLeft?-1:-2):fireController?.deviceId??0):DesktopGamepad.Current?.deviceId??Mouse.current?.deviceId??0;
            if(!available||suitFireSource!=source)armed=false;
            suitFireSource=available?source:0;
            if(!available)return;
            if(!shooting)armed=true;
            if(shooting&&armed)FireOnce();
        }
    }
}
