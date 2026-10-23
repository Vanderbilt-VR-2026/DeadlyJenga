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
        bool suitTriggerReleased;
        bool SuitShootingLeft
        {
            get
            {
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
            if(Keyboard.current?.vKey.wasPressedThisFrame==true){SetEquipped(!state.drawn);SetSuitThrust(false);return;}
            bool controllerLeft=!SuitShootingLeft;
            if(vr&&state.leftHanded!=controllerLeft)Runtime?.RequestEquipment(state.drawn,controllerLeft,false);
            var flightHand=avatar.tracking?avatar.tracking.WorldHand(controllerLeft):default;
            var shootHand=avatar.tracking?avatar.tracking.WorldHand(SuitShootingLeft):default;
            var flightTrigger=controllerLeft==left?trigger:offTrigger;
            if(flightTrigger==null)return;
            if(!flightTrigger.IsPressed())suitTriggerReleased=true;
            if(vr&&flightTrigger.WasPressedThisFrame()&&suitTriggerReleased)
            {
                var eye=avatar.viewCamera.transform;
                var hip=CandyWeaponVisual.Holster(EquipmentKind.Blaster,transform.position,eye.position,Quaternion.Euler(0,eye.eulerAngles.y,0),Scale,controllerLeft);
                if(!state.drawn||flightHand.tracked&&Vector3.Distance(flightHand.position,hip.position)<.30f*Scale)
                {SetEquipped(!state.drawn);suitTriggerReleased=false;return;}
            }
            if(!state.drawn){SetSuitThrust(false);return;}
            bool jets=vr?flightHand.tracked&&flightTrigger.IsPressed()&&suitTriggerReleased:Keyboard.current?.spaceKey.isPressed==true;
            SetSuitThrust(jets);
            if(jets)
            {
                // Feet supply lift, the controller palm tilts the thrust. Motion still uses the capsule motor.
                var steer=vr?flightHand.rotation*Vector3.up:avatar.viewCamera.transform.forward;
                var acceleration=Vector3.up*(motor.gravity+9)+Vector3.ProjectOnPlane(steer,Vector3.up)*13;
                motor.ApplyPoweredFlight(acceleration,Time.deltaTime);
            }
            bool shooting=vr?shootHand.tracked&&shootHand.skeletonTracked&&(SuitShootingLeft?avatar.tracking.LeftPinching:avatar.tracking.RightPinching):Mouse.current?.leftButton.isPressed==true&&Cursor.lockState==CursorLockMode.Locked;
            // Two-controller rigs retain a support-trigger fallback when optical hand tracking is unavailable.
            if(vr&&shootHand.tracked&&!shootHand.skeletonTracked)shooting=(SuitShootingLeft==left?trigger:offTrigger).IsPressed();
            if(shooting)FireOnce();
        }
    }
}
