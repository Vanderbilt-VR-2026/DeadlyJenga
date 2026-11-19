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
        static void ResetPresence() { leftPresence = rightPresence = default; leftBare = rightBare = default; }

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
                        if(usage==(left?CommonUsages.LeftHand:CommonUsages.RightHand))return PickedUp(left,controller)?controller:null;
            return null;
        }

        // A side that has been playing bare-handed keeps doing so when its controller shows up again, until that
        // controller is really picked up (moved, or anything on it pressed). A controller lying on a desk comes back
        // into tracking whenever the headset loses the bare hand for a moment, for example when the player looks up;
        // taking that as "two controllers" flipped the whole control scheme and rebased the view.
        struct BareHand { public bool playing; public XRController waiting; public Vector3 seenAt; public Quaternion seenTurn; }
        static BareHand leftBare, rightBare;
        const float PickUpMove = .04f, PickUpTurn = 10f;
        public static void NoteHandTracked(bool left) { if (left) leftBare.playing = true; else rightBare.playing = true; }
        public static bool PlayingBareHanded(bool left) => left ? leftBare.playing : rightBare.playing;
        static bool PickedUp(bool left, XRController controller)
        {
            ref var side = ref (left ? ref leftBare : ref rightBare);
            if (!side.playing) return true;
            Vector3 at = controller.devicePosition.ReadValue();
            Quaternion turn = controller.deviceRotation.ReadValue();
            if (side.waiting != controller) { side.waiting = controller; side.seenAt = at; side.seenTurn = turn; }
            float Axis(string name) => controller.TryGetChildControl<AxisControl>(name)?.ReadValue() ?? 0;
            bool Button(string name) => controller.TryGetChildControl<ButtonControl>(name)?.isPressed == true;
            // A held controller moves, turns, or has a finger resting on it. One lying on a desk does none of these.
            bool used = (at - side.seenAt).sqrMagnitude > PickUpMove * PickUpMove || Quaternion.Angle(turn, side.seenTurn) > PickUpTurn ||
                Axis("trigger") > .15f || Axis("grip") > .15f || Stick(controller).sqrMagnitude > .06f ||
                Button("primaryButton") || Button("secondaryButton") || MoveClickHeld(controller) ||
                Button("primaryTouched") || Button("secondaryTouched") || Button("thumbstickTouched") || Button("triggerTouched") || Button("thumbrestTouched") ||
                Button("primaryTouch") || Button("secondaryTouch") || Button("primary2DAxisTouch");
            if (!used) return false;
            side.playing = false; side.waiting = null;
            return true;
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
