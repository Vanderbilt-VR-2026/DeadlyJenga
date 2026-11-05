using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadlyJenga.CandyPlayers
{
    public sealed partial class CandyEquipment
    {
        float nextThrustRefresh;bool thrustRequested;
        void SetSuitThrust(bool active)
        {
            if(active!=thrustRequested||active&&Time.unscaledTime>=nextThrustRefresh)
            {
                (Runtime as IPoweredEquipmentRuntime)?.RequestThrust(active);thrustRequested=active;nextThrustRefresh=Time.unscaledTime+.5f;
            }
        }
        bool suitWasFlying;float suitTakeoffUntil;
        bool SuitShootingLeft
        {
            get
            {
                if(avatar.tracking&&avatar.tracking.IsHybrid)return !HybridXRInput.SingleControllerLeft;
                if(avatar.tracking)
                {
                    var a=avatar.tracking.WorldHand(true);var b=avatar.tracking.WorldHand(false);
                    if(a.skeletonTracked!=b.skeletonTracked)return a.skeletonTracked;
                }
                return !left;
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
                return new Pose(eye.position+eye.rotation*new Vector3(left?.25f:-.25f,-.18f,.40f)*Scale,eye.rotation);
            }
        }
        void UpdateSuit(EquipmentState state)
        {
            bool vr=motor.IsVR;
            bool controllerLeft=!SuitShootingLeft;
            var keyboard=Keyboard.current;
            if(vr?HybridXRInput.SecondaryPressed(controllerLeft):keyboard?.vKey.wasPressedThisFrame==true)
            {
                SetEquipped(!state.drawn);
                SetSuitThrust(false);
                suitWasFlying=false;
                return;
            }
            if(vr&&state.leftHanded!=controllerLeft)Runtime?.RequestEquipment(state.drawn,controllerLeft,false);
            if(!state.drawn){SetSuitThrust(false);suitWasFlying=false;return;}
            SetSuitThrust(true);
            if(!suitWasFlying){suitWasFlying=true;suitTakeoffUntil=Time.time+.45f;}

            var controller=HybridXRInput.Controller(controllerLeft);
            float Axis(string name)=>controller?.TryGetChildControl<UnityEngine.InputSystem.Controls.AxisControl>(name)?.ReadValue()??0;
            Vector2 move=vr?HybridXRInput.Stick(controllerLeft):new Vector2(
                (keyboard?.dKey.isPressed==true?1:0)-(keyboard?.aKey.isPressed==true?1:0),
                (keyboard?.wKey.isPressed==true?1:0)-(keyboard?.sKey.isPressed==true?1:0));
            float magnitude=Mathf.InverseLerp(vr?.18f:0f,1f,move.magnitude);
            var forward=motor.MovementForward;
            var right=Vector3.Cross(Vector3.up,forward);
            var horizontal=(right*move.normalized.x+forward*move.normalized.y)*(5f*magnitude);
            float lift=vr?Axis("trigger")-Axis("grip"):
                (keyboard?.eKey.isPressed==true?1:0)-(keyboard?.qKey.isPressed==true?1:0);
            float climb=Mathf.Sign(lift)*Mathf.InverseLerp(.12f,1f,Mathf.Abs(lift))*(lift>=0?3.5f:3f);
            motor.ApplyHoverFlight(horizontal+Vector3.up*climb,Time.deltaTime,Time.time<suitTakeoffUntil);

            var shootHand=avatar.tracking?avatar.tracking.WorldHand(SuitShootingLeft):default;
            bool shooting=vr?shootHand.tracked&&shootHand.skeletonTracked&&(SuitShootingLeft?avatar.tracking.LeftPinching:avatar.tracking.RightPinching):Mouse.current?.leftButton.isPressed==true&&Cursor.lockState==CursorLockMode.Locked;
            // Two-controller rigs retain a support-trigger fallback when optical hand tracking is unavailable.
            if(vr&&shootHand.tracked&&!shootHand.skeletonTracked)shooting=(SuitShootingLeft==left?trigger:offTrigger).IsPressed();
            if(shooting)FireOnce();
        }
    }
}
