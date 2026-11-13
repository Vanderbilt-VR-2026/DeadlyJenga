using DeadlyJenga.Core;
using DeadlyJenga.CandyPlayers;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CastleSurvivor : IMenuInputState
    {
        public bool MenuHeld=>!IsVR?DesktopGamepad.MenuHeld:HandTracking&&HandTracking.LeftMenuHeld;
        public bool MovementHeld
        {
            get
            {
                if(externallyDriven)return false;
                var keys=Keyboard.current;
                if(!IsVR)return DesktopGamepad.Moving||keys!=null&&(keys.wKey.isPressed||keys.aKey.isPressed||
                    keys.sKey.isPressed||keys.dKey.isPressed||keys.spaceKey.isPressed||keys.leftShiftKey.isPressed);
                var equipment=GetComponent<CandyEquipment>();
                if(equipment&&equipment.SuitMovementHeld)return true;
                bool dominantLeft=PlayerPrefs.GetInt("CandyBlaster.LeftHanded",0)!=0;
                bool both=HybridXRInput.DualControllers;
                var movementController=HybridXRInput.MovementController(dominantLeft);
                if(HybridXRInput.MoveClickHeld(movementController))return true;
                var walking=FilterVRMove(HybridXRInput.Stick(movementController));
                var looking=both?HybridXRInput.Stick(dominantLeft):Vector2.zero;
                if(HybridXRInput.SingleController&&vrTurn!=SurvivorVRTurn.PhysicalOnly&&HybridXRInput.SingleLookHeld)
                {
                    walking=Vector2.zero;
                    looking=HybridXRInput.MoveStick();
                }
                if(walking.sqrMagnitude>0)return true;
                if(vrTurn!=SurvivorVRTurn.PhysicalOnly)
                {
                    if(Mathf.Abs(looking.y)>.25f)return true;
                    if(vrTurn==SurvivorVRTurn.Smooth&&Mathf.Abs(looking.x)>.2f)return true;
                    if(vrTurn==SurvivorVRTurn.Snap&&Mathf.Abs(looking.x)>=.3f)return true;
                }
                return MenuJumpHeld();
            }
        }
        bool MenuJumpHeld()
        {
            if(vrPlayStyle!=SurvivorVRPlayStyle.Standing||!HandTracking)return false;
            var hand=HandTracking.GestureHand;
            if(!hand.tracked||!hand.skeletonTracked)return false;
            if(jumpGesture==SurvivorJumpGesture.IndexPinch)
                return Vector3.Distance(hand.skeleton.Joint(3),hand.skeleton.Joint(7))<jumpPinchRelease;
            float curl=0;
            for(int finger=1;finger<5;finger++)
            {
                int joint=finger*4;float length=0;
                for(int part=0;part<3;part++)
                    length+=Vector3.Distance(hand.skeleton.Joint(joint+part),hand.skeleton.Joint(joint+part+1));
                if(length<.015f)return false;
                float straight=Vector3.Distance(hand.skeleton.Joint(joint),hand.skeleton.Joint(joint+3))/length;
                curl+=Mathf.InverseLerp(.9f,.45f,straight)*.25f;
            }
            return curl>jumpFistRelease;
        }
    }
}
