using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;

namespace DeadlyJenga.CastlePrototype
{
    [DefaultExecutionOrder(-150)]
    public sealed class CastleSessionMenuInput : MonoBehaviour
    {
        internal CastlePlaySession session;
        void Update(){if(session)session.PollMenuInput();}
    }

    public sealed partial class CastlePlaySession
    {
        bool menuInputArmed,menuResumePending;
        int menuTransitionFrame,lastMenuLeft=-1,lastMenuRight=-1,lastMenuTracking=-1;
        bool lastMenuVR;
        IPlayableCharacter lastMenuCharacter;
        IFingerTips menuFingers;

        void HoldMenuGameplayInput()
        {
            menuInputArmed=false;
            menuResumePending=true;
            menuTransitionFrame=Time.frameCount;
            if(!Ready)return;
            Player.InputSuspended=true;
            Hand.InputSuspended=true;
        }

        internal void PollMenuInput()
        {
            if(!Ready||VisitingLobby)return;
            var left=MenuController(true);
            var right=MenuController(false);
            if(!ReferenceEquals(lastMenuCharacter,Active))
            {
                lastMenuCharacter=Active;
                menuFingers=Active.gameObject.GetComponent<IFingerTips>();
                HoldMenuGameplayInput();
            }
            int tracking=0;
            if(menuFingers!=null)
            {
                if(menuFingers.TryIndexTip(true,out _))tracking|=1;
                if(menuFingers.TryIndexTip(false,out _))tracking|=2;
            }
            else if(Active.HasHandTracking)tracking=4;
            int leftId=left?.deviceId??0,rightId=right?.deviceId??0;
            if(leftId!=lastMenuLeft||rightId!=lastMenuRight||tracking!=lastMenuTracking||IsVR!=lastMenuVR)
            {
                lastMenuLeft=leftId;lastMenuRight=rightId;
                lastMenuTracking=tracking;lastMenuVR=IsVR;
                HoldMenuGameplayInput();
            }
            if(!focused)return;

            var keyboard=Keyboard.current;
            var nativeController=left??right;
            var native=Button(nativeController,"menuButton")??Button(nativeController,"menu");
            var leftSecondary=Button(left,"secondaryButton");
            var rightSecondary=Button(right,"secondaryButton");
            bool leftGrip=Button(left,"gripPressed")?.isPressed==true;
            bool rightGrip=Button(right,"gripPressed")?.isPressed==true;
            bool nativeHeld=IsVR&&native?.isPressed==true;
            bool leftMenuHeld=IsVR&&(MenuOpen||!leftGrip)&&leftSecondary?.isPressed==true;
            bool rightMenuHeld=IsVR&&left==null&&(MenuOpen||rightGrip)&&rightSecondary?.isPressed==true;
            bool escapeHeld=!IsVR&&keyboard?.escapeKey.isPressed==true;
            bool requested=IsVR?
                native?.wasPressedThisFrame==true||
                (MenuOpen||!leftGrip)&&leftSecondary?.wasPressedThisFrame==true||
                left==null&&(MenuOpen||rightGrip)&&rightSecondary?.wasPressedThisFrame==true||
                Active.MenuPressed:
                keyboard?.escapeKey.wasPressedThisFrame==true||Active.MenuPressed;
            bool opticalMenuHeld=(Active is IMenuInputState gesture?gesture.MenuHeld:Active.MenuPressed);
            bool menuHeld=nativeHeld||leftMenuHeld||rightMenuHeld||escapeHeld||opticalMenuHeld;
            if(menuInputArmed&&requested)SetMenu(!MenuOpen);
            else if(!menuHeld)menuInputArmed=true;

            if(menuResumePending&&Time.frameCount>menuTransitionFrame&&
                !menuHeld&&!GameplayActionHeld(left,right))
            {
                menuResumePending=false;
                SetMenu(MenuOpen);
            }
        }

        static XRController MenuController(bool left)
        {
            foreach(var device in InputSystem.devices)
            {
                if(!(device is XRController controller)||!controller.isTracked.isPressed||
                    controller.TryGetChildControl<Vector2Control>("primary2DAxis")==null)continue;
                foreach(var usage in controller.usages)
                    if(usage==(left?CommonUsages.LeftHand:CommonUsages.RightHand))return controller;
            }
            return null;
        }

        static ButtonControl Button(XRController controller,string name)=>
            controller?.TryGetChildControl<ButtonControl>(name);

        static bool ControllerActionHeld(XRController controller)
        {
            if(controller==null)return false;
            return Button(controller,"triggerPressed")?.isPressed==true||
                controller.TryGetChildControl<AxisControl>("trigger")?.ReadValue()>.12f||
                Button(controller,"gripPressed")?.isPressed==true||
                controller.TryGetChildControl<AxisControl>("grip")?.ReadValue()>.12f||
                Button(controller,"primaryButton")?.isPressed==true||
                Button(controller,"secondaryButton")?.isPressed==true||
                Button(controller,"primary2DAxisClick")?.isPressed==true||
                (Button(controller,"menuButton")??Button(controller,"menu"))?.isPressed==true;
        }

        bool GameplayActionHeld(XRController left,XRController right)
        {
            if(Active is IMenuInputState state&&state.MovementHeld)return true;
            var keys=Keyboard.current;
            if(keys!=null&&(keys.wKey.isPressed||keys.aKey.isPressed||keys.sKey.isPressed||keys.dKey.isPressed||
                keys.qKey.isPressed||keys.eKey.isPressed||keys.spaceKey.isPressed||
                keys.leftShiftKey.isPressed||keys.rightShiftKey.isPressed||
                keys.leftCtrlKey.isPressed||keys.rightCtrlKey.isPressed||
                keys.leftAltKey.isPressed||keys.rightAltKey.isPressed))return true;
            if(IsVR)return ControllerActionHeld(left)||ControllerActionHeld(right)||Active.SelectHeld;
            var keyboard=Keyboard.current;
            var mouse=Mouse.current;
            int mode=PlayerPrefs.GetInt("DeadlyJenga.PreferredControlMode",0);
            var pad=mode==0||mode==4?Gamepad.current:null;
            return Active.SelectHeld||pad!=null&&(pad.rightTrigger.isPressed||pad.leftTrigger.isPressed||pad.buttonNorth.isPressed||pad.leftShoulder.isPressed||pad.rightShoulder.isPressed)||mouse?.leftButton.isPressed==true||mouse?.rightButton.isPressed==true||
                keyboard?.vKey.isPressed==true||keyboard?.rKey.isPressed==true||
                keyboard?.eKey.isPressed==true||keyboard?.nKey.isPressed==true||
                keyboard?.deleteKey.isPressed==true;
        }
    }
}
