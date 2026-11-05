using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace DeadlyJenga.CandyPlayers
{
    public sealed partial class CandyEquipment
    {
        int cancelPickup,cancelDevice;
        bool cancelGripArmed,cancelGripUsed;

        void ResetPickupCancel()
        {
            cancelPickup=cancelDevice=0;
            cancelGripArmed=cancelGripUsed=false;
        }

        bool PickupDeclinePressed()
        {
            if(!motor.IsVR)
            {
                ResetPickupCancel();
                return Keyboard.current?.nKey.wasPressedThisFrame==true;
            }
            if(!HybridXRInput.SingleController)
            {
                ResetPickupCancel();
                return HybridXRInput.GripPressed(left);
            }
            var device=HybridXRInput.Controller(left);
            if(device==null){ResetPickupCancel();return false;}
            var button=device.TryGetChildControl<ButtonControl>("gripPressed");
            bool held=button?.isPressed==true;
            if(cancelPickup!=pendingPickup||cancelDevice!=device.deviceId)
            {
                ResetPickupCancel();
                cancelPickup=pendingPickup;cancelDevice=device.deviceId;
                cancelGripUsed=held;
                return false;
            }
            if(button?.wasPressedThisFrame==true)cancelGripArmed=!cancelGripUsed;
            var stickClick=device.TryGetChildControl<ButtonControl>("primary2DAxisClick");
            bool chord=device.TryGetChildControl<ButtonControl>("secondaryButton")?.isPressed==true||
                device.TryGetChildControl<ButtonControl>("primaryButton")?.isPressed==true||
                stickClick?.isPressed==true||stickClick?.wasPressedThisFrame==true||
                device.TryGetChildControl<AxisControl>("trigger")?.ReadValue()>.12f;
            if(chord){cancelGripArmed=false;cancelGripUsed=held;}
            if(held)return false;
            bool decline=cancelGripArmed;
            cancelGripArmed=cancelGripUsed=false;
            return decline;
        }
    }
}
