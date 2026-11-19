using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;

namespace DeadlyJenga.CandyPlayers
{
    public static class HybridXRInput
    {
        const float TrackingGraceSeconds = .65f;
        static ControllerPresence leftPresence, rightPresence;

        struct ControllerPresence
        {
            public XRController device;
            public float seenAt;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetPresence() => leftPresence = rightPresence = default;

        public static XRController Controller(bool left)
        {
            var controller = FindController(left,false);
            if (controller != null)
            {
                ref var presence = ref (left ? ref leftPresence : ref rightPresence);
                presence.device = controller;
                presence.seenAt = Time.unscaledTime;
            }
            return controller;
        }

        // Keep the control layout during a short occlusion, without reading stale inputs.
        internal static XRController ControllerOwner(bool left)
        {
            var tracked = Controller(left);
            if (tracked != null) return tracked;
            var presence = left ? leftPresence : rightPresence;
            return presence.device != null && presence.device.added && presence.device.enabled &&
                Time.unscaledTime - presence.seenAt < TrackingGraceSeconds ? presence.device : null;
        }
        public static XRController ControllerWithStick(bool left)=>FindController(left,true);
        static XRController FindController(bool left,bool needsStick)
        {
            foreach(var device in InputSystem.devices)
                if(device is XRController controller && !(controller is UnityEngine.XR.OpenXR.Features.Interactions.HandInteractionProfile.HandInteraction) && controller.enabled && controller.isTracked.isPressed && (!needsStick||controller.TryGetChildControl<Vector2Control>("primary2DAxis")!=null))
                    foreach(var usage in controller.usages)
                        if(usage==(left?CommonUsages.LeftHand:CommonUsages.RightHand))return controller;
            return null;
        }
        public static bool ControllerTracked(bool left)=>Controller(left)!=null;
        internal static bool DualControllers => ControllerOwner(true) != null && ControllerOwner(false) != null;
        public static bool SingleController => (ControllerOwner(true) != null) != (ControllerOwner(false) != null);
        public static bool SingleControllerLeft => ControllerOwner(true) != null && ControllerOwner(false) == null;
        public static XRController MovementController(bool dominantLeft) =>
            ControllerOwner(!dominantLeft) != null ? ControllerWithStick(!dominantLeft) : ControllerWithStick(dominantLeft);
        public static Vector2 Stick(XRController controller)=>controller?.TryGetChildControl<Vector2Control>("primary2DAxis")?.ReadValue()??Vector2.zero;
        public static Vector2 Stick(bool left)=>Stick(ControllerWithStick(left));
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
