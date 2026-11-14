#if UNITY_EDITOR || UNITY_STANDALONE
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace DeadlyJenga.Presentation
{
    // PC-simulated VR: creates a virtual headset and two Quest-style controllers and drives
    // them from keyboard + mouse. The game reads them through its normal VR input bindings.
    //   WASD  left stick (move)        Mouse  head look       Space  right A (jump)
    //   Shift left stick click (sprint) LMB/RMB dominant trigger/grip
    //   Arrows right stick (turn)      G  off-hand trigger    Z/X  left X/Y   F  right B
    //   Hold Q / E + mouse  move left / right controller (scroll = depth, + Ctrl = rotate)
    //   C  crouch   R  recenter head + hands   M  menu button   Esc  free cursor (click to relock)
    public class PcSimulatedVR : MonoBehaviour
    {
        public static PcSimulatedVR Instance { get; private set; }

        public float lookSensitivity = .12f;
        public float handSensitivity = .0015f;
        public float standingHeadHeight = 0f;
        public float crouchDrop = .5f;

        XRSimulatedHMD hmd;
        XRSimulatedController left, right;
        float yaw, pitch;
        bool crouched, cursorFree, skipMouse;
        Vector3 leftOffset, rightOffset;
        Quaternion leftTilt = Quaternion.identity, rightTilt = Quaternion.identity;
        static readonly Vector3 LeftHome = new(-.2f, -.35f, .4f), RightHome = new(.2f, -.35f, .4f);

        // Test hooks: scripts can hold keys without a real keyboard.
        public Vector2 TestMove, TestLook;
        public bool TestJump, TestSprint, TestTrigger, TestGrip;

        public static PcSimulatedVR Create()
        {
            if (Instance) return Instance;
            var go = new GameObject("PC Simulated VR");
            DontDestroyOnLoad(go);
            return go.AddComponent<PcSimulatedVR>();
        }

        void OnEnable()
        {
            Instance = this;
            hmd = InputSystem.AddDevice<XRSimulatedHMD>("PC Simulated HMD");
            left = InputSystem.AddDevice<XRSimulatedController>("PC Simulated Left Controller");
            right = InputSystem.AddDevice<XRSimulatedController>("PC Simulated Right Controller");
            InputSystem.SetDeviceUsage(left, UnityEngine.InputSystem.CommonUsages.LeftHand);
            InputSystem.SetDeviceUsage(right, UnityEngine.InputSystem.CommonUsages.RightHand);
            Recenter();
        }

        void OnDisable()
        {
            if (hmd != null && hmd.added) InputSystem.RemoveDevice(hmd);
            if (left != null && left.added) InputSystem.RemoveDevice(left);
            if (right != null && right.added) InputSystem.RemoveDevice(right);
            hmd = null; left = right = null;
            if (Instance == this) Instance = null;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }

        void Recenter()
        {
            yaw = pitch = 0f;
            leftOffset = rightOffset = Vector3.zero;
            leftTilt = rightTilt = Quaternion.identity;
        }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            bool menuOpen = Services.Session != null && Services.Session.MenuOpen;
            UpdateCursor(kb, mouse, menuOpen);

            bool mouseLive = mouse != null && Cursor.lockState == CursorLockMode.Locked && !skipMouse;
            Vector2 delta = mouseLive ? mouse.delta.ReadValue() : Vector2.zero;
            delta += TestLook;
            bool holdLeft = Held(kb?.qKey), holdRight = Held(kb?.eKey), rotateHand = Held(kb?.leftCtrlKey);
            float scroll = mouse != null ? mouse.scroll.ReadValue().y / 120f : 0f;

            if (holdLeft || holdRight)
            {
                var move = new Vector3(delta.x, delta.y, scroll * 40f) * handSensitivity;
                var spin = Quaternion.Euler(-delta.y * lookSensitivity, delta.x * lookSensitivity, 0f);
                if (holdLeft) { if (rotateHand) leftTilt = spin * leftTilt; else leftOffset += move; }
                if (holdRight) { if (rotateHand) rightTilt = spin * rightTilt; else rightOffset += move; }
            }
            else
            {
                yaw += delta.x * lookSensitivity;
                pitch = Mathf.Clamp(pitch - delta.y * lookSensitivity, -85f, 85f);
            }
            if (Pressed(kb?.rKey)) Recenter();
            if (Pressed(kb?.cKey)) crouched = !crouched;

            // Headset (in tracking space; XR Origin adds the eye height).
            var head = Quaternion.Euler(pitch, yaw, 0f);
            var body = Quaternion.Euler(0f, yaw, 0f);
            var headPos = new Vector3(0f, standingHeadHeight - (crouched ? crouchDrop : 0f), 0f);
            var h = new XRSimulatedHMDState
            {
                centerEyePosition = headPos, centerEyeRotation = head,
                leftEyePosition = headPos, leftEyeRotation = head,
                rightEyePosition = headPos, rightEyeRotation = head,
                devicePosition = headPos, deviceRotation = head,
                isTracked = true, trackingState = 3,
            };
            InputState.Change(hmd, h);

            // Controllers sit in front of the body and point where the head looks (mouse aims).
            bool leftHanded = PlayerPrefs.GetInt("CandyBlaster.LeftHanded", 0) != 0;
            Vector2 stick = TestMove;
            if (Held(kb?.wKey)) stick.y += 1; if (Held(kb?.sKey)) stick.y -= 1;
            if (Held(kb?.dKey)) stick.x += 1; if (Held(kb?.aKey)) stick.x -= 1;
            stick = Vector2.ClampMagnitude(stick, 1f);
            Vector2 turn = Vector2.zero;
            if (Held(kb?.rightArrowKey)) turn.x += 1; if (Held(kb?.leftArrowKey)) turn.x -= 1;
            if (Held(kb?.upArrowKey)) turn.y += 1; if (Held(kb?.downArrowKey)) turn.y -= 1;

            bool mouseOk = mouse != null && Cursor.lockState == CursorLockMode.Locked && !holdLeft && !holdRight;
            bool fire = TestTrigger || mouseOk && mouse.leftButton.isPressed;
            bool grab = TestGrip || mouseOk && mouse.rightButton.isPressed;
            bool offFire = Held(kb?.gKey);

            var l = new XRSimulatedControllerState
            {
                primary2DAxis = stick,
                devicePosition = headPos + body * (LeftHome + leftOffset),
                deviceRotation = head * leftTilt,
                isTracked = true, trackingState = 3, batteryLevel = 1f,
            };
            var r = new XRSimulatedControllerState
            {
                primary2DAxis = turn,
                devicePosition = headPos + body * (RightHome + rightOffset),
                deviceRotation = head * rightTilt,
                isTracked = true, trackingState = 3, batteryLevel = 1f,
            };
            l = l.WithButton(ControllerButton.Primary2DAxisClick, TestSprint || Held(kb?.leftShiftKey))
                 .WithButton(ControllerButton.Primary2DAxisTouch, stick != Vector2.zero)
                 .WithButton(ControllerButton.PrimaryButton, Held(kb?.zKey))
                 .WithButton(ControllerButton.SecondaryButton, Held(kb?.xKey))
                 .WithButton(ControllerButton.MenuButton, Held(kb?.mKey))
                 .WithButton(ControllerButton.UserPresence, true);
            r = r.WithButton(ControllerButton.PrimaryButton, TestJump || Held(kb?.spaceKey))
                 .WithButton(ControllerButton.SecondaryButton, Held(kb?.fKey))
                 .WithButton(ControllerButton.UserPresence, true);

            ref var dominant = ref (leftHanded ? ref l : ref r);
            ref var other = ref (leftHanded ? ref r : ref l);
            SetTrigger(ref dominant, fire); SetGrip(ref dominant, grab);
            SetTrigger(ref other, offFire);

            InputState.Change(left, l);
            InputState.Change(right, r);
        }

        static void SetTrigger(ref XRSimulatedControllerState s, bool on)
        { s.trigger = on ? 1f : 0f; s = s.WithButton(ControllerButton.TriggerButton, on); }
        static void SetGrip(ref XRSimulatedControllerState s, bool on)
        { s.grip = on ? 1f : 0f; s = s.WithButton(ControllerButton.GripButton, on); }

        void UpdateCursor(Keyboard kb, Mouse mouse, bool menuOpen)
        {
            if (Pressed(kb?.escapeKey)) cursorFree = true;
            else if (cursorFree && mouse != null && mouse.leftButton.wasPressedThisFrame) cursorFree = false;
            bool free = cursorFree || !Application.isFocused;
            var want = free ? CursorLockMode.None : CursorLockMode.Locked;
            // Locking warps the cursor; skip that frame's mouse motion so the head doesn't jump.
            skipMouse = Cursor.lockState != want;
            if (skipMouse) { Cursor.lockState = want; Cursor.visible = free; }
        }

        static bool Held(UnityEngine.InputSystem.Controls.KeyControl key) => key != null && key.isPressed;
        static bool Pressed(UnityEngine.InputSystem.Controls.KeyControl key) => key != null && key.wasPressedThisFrame;
    }
}
#endif
