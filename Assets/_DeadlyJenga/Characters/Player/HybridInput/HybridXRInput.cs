using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;

namespace DeadlyJenga.CandyPlayers
{
    public static class HybridXRInput
    {
        public static XRController Controller(bool left)
        {
            foreach(var device in InputSystem.devices)
                if(device is XRController controller && !(controller is UnityEngine.XR.OpenXR.Features.Interactions.HandInteractionProfile.HandInteraction) && controller.isTracked.isPressed)
                    foreach(var usage in controller.usages)
                        if(usage==(left?CommonUsages.LeftHand:CommonUsages.RightHand))return controller;
            return null;
        }
        public static bool ControllerTracked(bool left)=>Controller(left)!=null;
        public static bool SingleController=>ControllerTracked(true)!=ControllerTracked(false);
        public static bool SingleControllerLeft=>ControllerTracked(true)&&!ControllerTracked(false);
        public static Vector2 MoveStick()
        {
            var controller=Controller(true)??Controller(false);
            return controller?.TryGetChildControl<Vector2Control>("primary2DAxis")?.ReadValue()??Vector2.zero;
        }
        static ButtonControl SingleButton(string name)=>SingleController?(Controller(true)??Controller(false))?.TryGetChildControl<ButtonControl>(name):null;
        public static bool SinglePrimaryHeld=>SingleButton("primaryButton")?.isPressed==true;
        public static bool SingleModePressed=>SingleButton("primary2DAxisClick")?.wasPressedThisFrame==true;
        public static bool SingleCancelPressed=>SingleButton("gripPressed")?.wasPressedThisFrame==true;
    }
}
