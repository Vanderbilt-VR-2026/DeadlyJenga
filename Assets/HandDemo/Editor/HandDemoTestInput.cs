using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.XR;

namespace FlyingHand.Editor
{
    // Minimal editor-only devices exercise the actual generic OpenXR bindings
    // without taking a dependency on XR Interaction Toolkit's simulator.
    [InputControlLayout(commonUsages = new[] { "LeftHand", "RightHand" })]
    public class HandDemoTestController : XRController
    {
        [InputControl] public Vector2Control primary2DAxis { get; private set; }
        [InputControl] public AxisControl trigger { get; private set; }
        protected override void FinishSetup()
        {
            base.FinishSetup();
            primary2DAxis = GetChildControl<Vector2Control>("primary2DAxis");
            trigger = GetChildControl<AxisControl>("trigger");
        }
    }

    public struct HandDemoControllerState
    {
        public Vector2 primary2DAxis;
        public float trigger;
        public Quaternion deviceRotation;
    }

    public struct HandDemoHMDState
    {
        public Vector3 devicePosition;
        public Quaternion deviceRotation;
        public bool isTracked;
        public int trackingState;
    }

    public static class HandDemoTestInput
    {
        public static void Queue(HandDemoTestController device, HandDemoControllerState state)
        {
            InputSystem.QueueDeltaStateEvent(device.primary2DAxis, state.primary2DAxis);
            InputSystem.QueueDeltaStateEvent(device.trigger, state.trigger);
            InputSystem.QueueDeltaStateEvent(device.deviceRotation, state.deviceRotation);
        }

        public static void Queue(XRHMD device, HandDemoHMDState state)
        {
            InputSystem.QueueDeltaStateEvent(device.devicePosition, state.devicePosition);
            InputSystem.QueueDeltaStateEvent(device.deviceRotation, state.deviceRotation);
            InputSystem.QueueDeltaStateEvent(device.trackingState, state.trackingState);
        }
    }
}
