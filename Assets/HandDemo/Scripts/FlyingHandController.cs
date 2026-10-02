using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using System.Collections.Generic;

namespace FlyingHand
{
    public enum ControlMode { Auto, Desktop, VR }

    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class FlyingHandController : MonoBehaviour
    {
        [Header("Control mode")]
        [InspectorName("ControlModeOverride")]
        public ControlMode ControlModeOverride = ControlMode.Auto;
        public ControlMode ActiveControlMode { get; private set; } = ControlMode.Desktop;
        public event System.Action<ControlMode> ControlModeChanged;
        [Header("Desktop")]
        [Tooltip("Degrees of requested hand yaw per mouse pixel; uses the same yaw torque and speed limit as VR.")]
        [Min(0)] public float mouseYawSensitivity = .15f;
        public bool captureDesktopCursor = true;
        public Vector2 DesktopLookDelta { get; private set; }
        readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();
        bool modeInitialized;
        CursorLockMode previousCursorLock;
        bool previousCursorVisible;
        bool cursorOwned;
        [Header("Input System — no pose input")]
        public InputAction move = new InputAction("Move", InputActionType.Value, "<XRController>{LeftHand}/primary2DAxis", expectedControlType: "Vector2");
        public InputAction turn = new InputAction("Yaw", InputActionType.Value, "<XRController>{RightHand}/primary2DAxis", expectedControlType: "Vector2");
        public InputAction ascend = new InputAction("Ascend", InputActionType.Value, "<XRController>{RightHand}/trigger", expectedControlType: "Axis");
        public InputAction descend = new InputAction("Descend", InputActionType.Value, "<XRController>{LeftHand}/trigger", expectedControlType: "Axis");
        [Header("Flight (metres / seconds)")]
        [Min(0)] public float thrust = 10f;
        [Min(0)] public float verticalThrust = 8f;
        [Min(.1f)] public float maximumSpeed = 12f;
        [Min(.1f)] public float maximumVerticalSpeed = 8f;
        [Min(0)] public float drag = .65f;
        [Min(0)] public float inputResponse = 8f;
        [Range(0, .5f)] public float stickDeadzone = .15f;
        [Header("Yaw and stability")]
        [Range(0, 180)] public float yawSpeed = 75f;
        [Min(0)] public float yawStrength = 8f;
        [Min(0)] public float stabilization = 12f;
        [Min(0)] public float maximumYawAcceleration = 8f;
        [Min(1)] public float mass = 650f;
        public Rigidbody Body { get; private set; }
        public Vector2 MoveInput { get; private set; }
        public float YawInput { get; private set; }
        public float VerticalInput { get; private set; }
        Vector3 smoothedInput;
        float smoothedYaw;
        bool focused = true;

        void Awake() { Body = GetComponent<Rigidbody>(); ConfigureBody(); }
        public void ConfigureBody()
        {
            if (!Body) Body = GetComponent<Rigidbody>();
            Body.mass = mass;
            Body.useGravity = false;
            Body.isKinematic = false;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            // Compound pointing-hand geometry has rotated principal inertia axes.
            // Align them with the vehicle so locked axes cannot couple yaw into roll.
            Body.ResetInertiaTensor();
            Body.inertiaTensorRotation = Quaternion.identity;
            Body.linearDamping = drag;
            Body.angularDamping = 0f;
            Body.maxAngularVelocity = yawSpeed * Mathf.Deg2Rad;
            Body.maxLinearVelocity = maximumSpeed;
            Body.solverIterations = 12;
            Body.solverVelocityIterations = 4;
        }
        void OnEnable()
        {
            EnsureAction(ref move, "Move", "<XRController>{LeftHand}/primary2DAxis", "Vector2");
            EnsureAction(ref turn, "Yaw", "<XRController>{RightHand}/primary2DAxis", "Vector2");
            EnsureAction(ref ascend, "Ascend", "<XRController>{RightHand}/trigger", "Axis");
            EnsureAction(ref descend, "Descend", "<XRController>{LeftHand}/trigger", "Axis");
            move.Enable(); turn.Enable(); ascend.Enable(); descend.Enable();
            RefreshControlMode();
        }
        static void EnsureAction(ref InputAction action, string name, string binding, string controlType)
        {
            if (action == null) action = new InputAction(name, InputActionType.Value, expectedControlType: controlType);
            if (action.bindings.Count == 0) action.AddBinding(binding);
        }
        public bool HasRunningXRDisplay()
        {
            SubsystemManager.GetSubsystems(displays);
            foreach (var display in displays) if (display.running) return true;
            return false;
        }
        public void RefreshControlMode()
        {
            var next = ControlModeOverride == ControlMode.Auto
                ? (HasRunningXRDisplay() ? ControlMode.VR : ControlMode.Desktop) : ControlModeOverride;
            if (modeInitialized && next == ActiveControlMode) return;
            ActiveControlMode = next; modeInitialized = true;
            ClearInput();
            if (next == ControlMode.Desktop && captureDesktopCursor && focused) CaptureCursor();
            else ReleaseCursor();
            Debug.Log($"FlyingHand: {next} Mode (override: {ControlModeOverride})", this);
            ControlModeChanged?.Invoke(next);
        }
        void CaptureCursor()
        {
            if (!cursorOwned) { previousCursorLock = Cursor.lockState; previousCursorVisible = Cursor.visible; cursorOwned = true; }
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        }
        void ReleaseCursor()
        {
            if (!cursorOwned) return;
            Cursor.lockState = previousCursorLock; Cursor.visible = previousCursorVisible; cursorOwned = false;
        }
        void ClearInput()
        {
            MoveInput = Vector2.zero; YawInput = VerticalInput = 0; DesktopLookDelta = Vector2.zero;
            smoothedInput = Vector3.zero; smoothedYaw = 0;
        }
        void OnDisable()
        {
            move?.Disable(); turn?.Disable(); ascend?.Disable(); descend?.Disable();
            ClearInput(); ReleaseCursor(); modeInitialized = false;
        }
        void OnDestroy() { move?.Dispose(); turn?.Dispose(); ascend?.Dispose(); descend?.Dispose(); }
        void OnApplicationFocus(bool value)
        {
            focused = value;
            if (!value) { ClearInput(); ReleaseCursor(); }
        }
        float Deadzone(float value) => Mathf.Abs(value) <= stickDeadzone ? 0 : Mathf.Sign(value) * Mathf.InverseLerp(stickDeadzone, 1, Mathf.Abs(value));
        void Update()
        {
            // A connected input device alone is not a running VR session. This also
            // handles delayed XR startup and a display stopping during Play Mode.
            RefreshControlMode();
            DesktopLookDelta = Vector2.zero;
            if (!focused) { ClearInput(); return; }
            if (ActiveControlMode == ControlMode.Desktop)
            {
                var keyboard = Keyboard.current; var mouse = Mouse.current;
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ReleaseCursor();
                else if (captureDesktopCursor && mouse != null && mouse.leftButton.wasPressedThisFrame) CaptureCursor();
                if (!captureDesktopCursor) ReleaseCursor();
                MoveInput = keyboard == null ? Vector2.zero : Vector2.ClampMagnitude(new Vector2(
                    (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                    (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0)), 1);
                VerticalInput = keyboard == null ? 0 : (keyboard.eKey.isPressed ? 1 : 0) - (keyboard.qKey.isPressed ? 1 : 0);
                if (mouse != null && (!captureDesktopCursor || Cursor.lockState == CursorLockMode.Locked)) DesktopLookDelta = mouse.delta.ReadValue();
                // Mouse delta is displacement, so convert it to a requested angular
                // rate before the common fixed-step yaw motor consumes it.
                YawInput = Time.deltaTime > 0 && yawSpeed > 0
                    ? Mathf.Clamp(DesktopLookDelta.x * mouseYawSensitivity / (Time.deltaTime * yawSpeed), -1, 1) : 0;
                return;
            }
            Vector2 raw = focused ? move.ReadValue<Vector2>() : Vector2.zero;
            MoveInput = raw.sqrMagnitude > stickDeadzone * stickDeadzone ? raw.normalized * Mathf.InverseLerp(stickDeadzone, 1, raw.magnitude) : Vector2.zero;
            YawInput = focused ? Deadzone(turn.ReadValue<Vector2>().x) : 0;
            VerticalInput = focused ? Mathf.Clamp(ascend.ReadValue<float>() - descend.ReadValue<float>(), -1, 1) : 0;
        }
        void FixedUpdate()
        {
            float blend = 1 - Mathf.Exp(-inputResponse * Time.fixedDeltaTime);
            smoothedInput = Vector3.Lerp(smoothedInput, new Vector3(MoveInput.x, VerticalInput, MoveInput.y), blend);
            smoothedYaw = Mathf.Lerp(smoothedYaw, YawInput, blend);
            Quaternion heading = Quaternion.Euler(0, Body.rotation.eulerAngles.y, 0);
            Vector3 acceleration = heading * new Vector3(smoothedInput.x * thrust, 0, smoothedInput.z * thrust) + Vector3.up * (smoothedInput.y * verticalThrust);
            Body.linearDamping = drag;
            Body.maxLinearVelocity = maximumSpeed;
            Body.maxAngularVelocity = yawSpeed * Mathf.Deg2Rad;
            Body.AddForce(acceleration, ForceMode.Acceleration);
            float targetYawRate = smoothedYaw * yawSpeed * Mathf.Deg2Rad;
            float gain = Mathf.Abs(YawInput) < .001f ? stabilization : yawStrength;
            float yawAcceleration = Mathf.Clamp((targetYawRate - Body.angularVelocity.y) * gain, -maximumYawAcceleration, maximumYawAcceleration);
            Body.AddTorque(Vector3.up * yawAcceleration, ForceMode.Acceleration);
            Vector3 velocity = Body.linearVelocity;
            velocity.y = Mathf.Clamp(velocity.y, -maximumVerticalSpeed, maximumVerticalSpeed);
            Body.linearVelocity = Vector3.ClampMagnitude(velocity, maximumSpeed);
            Body.angularVelocity = new Vector3(0, Mathf.Clamp(Body.angularVelocity.y, -Body.maxAngularVelocity, Body.maxAngularVelocity), 0);
        }
    }
}
