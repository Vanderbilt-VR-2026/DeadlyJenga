using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace DeadlyJenga.CandyPlayers
{
    public static class DesktopGamepad
    {
        // OpenXR 1.16 registers an XR device named DPad over the standard control.
        // Give gamepads their own control name without touching XR layouts or packages.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        static void RegisterLayouts()
        {
            InputSystem.RegisterLayout<UnityEngine.InputSystem.Controls.DpadControl>("CandyGamepadDpad");
            InputSystem.RegisterLayoutOverride(@"{""name"":""CandyGamepadControls"",""extend"":""Gamepad"",""controls"":[{""name"":""dpad"",""layout"":""CandyGamepadDpad""}]}");
        }
        public static Gamepad Current
        {
            get
            {
                int mode=PlayerPrefs.GetInt("DeadlyJenga.PreferredControlMode",0);
                if(mode!=4&&(mode!=0||XRSettings.isDeviceActive))return null;
                var pad=Gamepad.current;
                if(pad!=null&&pad.added&&pad.enabled)return pad;
                foreach(var device in Gamepad.all)if(device.added&&device.enabled)return device;
                return null;
            }
        }
        public static Vector2 Move=>Current?.leftStick.ReadValue()??Vector2.zero;
        public static Vector2 Look=>Current?.rightStick.ReadValue()??Vector2.zero;
        public static bool JumpPressed=>Current?.buttonSouth.wasPressedThisFrame==true;
        public static bool JumpHeld=>Current?.buttonSouth.isPressed==true;
        public static bool SprintPressed=>Current?.leftStickButton.wasPressedThisFrame==true;
        public static bool SprintHeld=>Current?.leftStickButton.isPressed==true;
        public static bool FirePressed=>Current?.rightTrigger.wasPressedThisFrame==true;
        public static bool FireHeld=>Current?.rightTrigger.isPressed==true;
        public static bool AimHeld=>Current?.leftTrigger.isPressed==true;
        public static bool InteractPressed=>Current?.buttonWest.wasPressedThisFrame==true;
        public static bool HolsterPressed=>Current?.buttonNorth.wasPressedThisFrame==true;
        public static bool DropPressed=>Current?.leftShoulder.wasPressedThisFrame==true;
        public static bool ModePressed=>Current?.rightShoulder.wasPressedThisFrame==true;
        public static bool BashPressed=>Current?.rightStickButton.wasPressedThisFrame==true;
        public static bool MenuPressed=>Current?.startButton.wasPressedThisFrame==true;
        public static bool MenuHeld=>Current?.startButton.isPressed==true;
        public static bool SelectPressed=>InteractPressed||JumpPressed;
        public static bool SelectHeld=>Current?.buttonWest.isPressed==true||JumpHeld;
        public static float Lift=>(JumpHeld?1:0)-(Current?.buttonEast.isPressed==true?1:0);
        public static bool Moving=>Move.sqrMagnitude>.001f||JumpHeld||SprintHeld;
    }
}
