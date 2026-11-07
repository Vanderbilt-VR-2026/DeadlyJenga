using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;

namespace DeadlyJenga.CandyPlayers
{
    public static class HybridXRInput
    {
        public static XRController Controller(bool left)=>FindController(left,false);
        public static XRController ControllerWithStick(bool left)=>FindController(left,true);
        static XRController FindController(bool left,bool needsStick)
        {
            foreach(var device in InputSystem.devices)
                if(device is XRController controller && !(controller is UnityEngine.XR.OpenXR.Features.Interactions.HandInteractionProfile.HandInteraction) && controller.isTracked.isPressed && (!needsStick||controller.TryGetChildControl<Vector2Control>("primary2DAxis")!=null))
                    foreach(var usage in controller.usages)
                        if(usage==(left?CommonUsages.LeftHand:CommonUsages.RightHand))return controller;
            return null;
        }
        public static bool ControllerTracked(bool left)=>Controller(left)!=null;
        public static bool SingleController=>ControllerTracked(true)!=ControllerTracked(false);
        public static bool SingleControllerLeft=>ControllerTracked(true)&&!ControllerTracked(false);
        public static XRController MovementController(bool dominantLeft)=>ControllerWithStick(!dominantLeft)??ControllerWithStick(dominantLeft);
        public static Vector2 Stick(XRController controller)=>controller?.TryGetChildControl<Vector2Control>("primary2DAxis")?.ReadValue()??Vector2.zero;
        public static Vector2 Stick(bool left)=>Stick(Controller(left));
        static ButtonControl MoveClick(XRController controller)=>
            controller?.TryGetChildControl<ButtonControl>("thumbstickClicked")??
            controller?.TryGetChildControl<ButtonControl>("primary2DAxisClick")??
            controller?.TryGetChildControl<ButtonControl>("joystickClicked");
        public static bool MoveClickPressed(XRController controller)=>MoveClick(controller)?.wasPressedThisFrame==true;
        public static bool MoveClickHeld(XRController controller)=>MoveClick(controller)?.isPressed==true;
        public static bool SecondaryPressed(bool left) =>
            Controller(left)?.TryGetChildControl<ButtonControl>("secondaryButton")?.wasPressedThisFrame == true;
        public static bool GripHeld(bool left) =>
            Controller(left)?.TryGetChildControl<ButtonControl>("gripPressed")?.isPressed == true;
        public static bool PrimaryHeld(bool left) =>
            Controller(left)?.TryGetChildControl<ButtonControl>("primaryButton")?.isPressed == true;
        public static Vector2 MoveStick()
        {
            var controller=ControllerWithStick(true)??ControllerWithStick(false);
            return controller?.TryGetChildControl<Vector2Control>("primary2DAxis")?.ReadValue()??Vector2.zero;
        }
        static ButtonControl SingleButton(string name)=>SingleController?(Controller(true)??Controller(false))?.TryGetChildControl<ButtonControl>(name):null;
        public static bool SingleLookHeld=>SingleButton("gripPressed")?.isPressed==true;
        public static bool SinglePrimaryHeld=>SingleButton("primaryButton")?.isPressed==true;
        public static bool SingleModePressed=>SingleLookHeld&&SingleButton("primaryButton")?.wasPressedThisFrame==true;
        public static bool GripPressed(bool left)=>Controller(left)?.TryGetChildControl<ButtonControl>("gripPressed")?.wasPressedThisFrame==true;
    }
}
