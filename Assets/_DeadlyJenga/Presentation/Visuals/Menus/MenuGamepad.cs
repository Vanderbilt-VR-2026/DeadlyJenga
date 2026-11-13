using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace DeadlyJenga.Presentation
{
    // A virtual pointer stays inside the game; it never moves the operating-system cursor.
    public static class MenuGamepad
    {
        static Vector2 position;
        static int frame=-1,deviceId;
        static bool active;
        public static Gamepad Connected
        {
            get
            {
                var pad=Gamepad.current;
                if(pad!=null&&pad.added&&pad.enabled)return pad;
                foreach(var device in Gamepad.all)if(device.added&&device.enabled)return device;
                return null;
            }
        }
        public static Gamepad Current
        {
            get
            {
                int mode=PlayerPrefs.GetInt("DeadlyJenga.PreferredControlMode",0);
                return mode==4||mode==0&&!XRSettings.isDeviceActive?Connected:null;
            }
        }
        public static string DeviceName=>Connected==null?"No controller connected":
            string.IsNullOrEmpty(Connected.description.product)?Connected.displayName:Connected.description.product;
        public static bool Pressed=>Current?.buttonSouth.wasPressedThisFrame==true;
        public static bool Held=>Current?.buttonSouth.isPressed==true;
        public static bool Cancel=>Current?.buttonEast.wasPressedThisFrame==true;
        public static bool UsingPointer { get { Update();return active; } }
        public static Vector2 Position { get { Update();return position; } }
        public static void PresentPointer(UnityEngine.UI.Image cursor,Vector2 localPoint)
        {
            if(!cursor)return;
            cursor.gameObject.SetActive(UsingPointer);
            if(!UsingPointer)return;
            cursor.rectTransform.anchoredPosition=localPoint;
            cursor.rectTransform.localScale=Vector3.one;
            cursor.color=new Color(.45f,1,.8f,1);
            cursor.transform.SetAsLastSibling();
        }
        static void Update()
        {
            if(frame==Time.frameCount)return;
            frame=Time.frameCount;
            var pad=Current;
            if(pad==null){active=false;deviceId=0;return;}
            if(deviceId!=pad.deviceId){deviceId=pad.deviceId;position=new Vector2(Screen.width*.5f,Screen.height*.5f);}
            var stick=pad.leftStick.ReadValue()+pad.rightStick.ReadValue()+pad.dpad.ReadValue();
            if(stick.sqrMagnitude>.001f||Pressed||Held)active=true;
            var mouse=Mouse.current;
            if(mouse!=null&&(mouse.delta.ReadValue().sqrMagnitude>4||mouse.leftButton.wasPressedThisFrame))active=false;
            position+=Vector2.ClampMagnitude(stick,1)*Mathf.Min(Screen.width,Screen.height)*.75f*Mathf.Min(Time.unscaledDeltaTime,.05f);
            position=new Vector2(Mathf.Clamp(position.x,0,Screen.width),Mathf.Clamp(position.y,0,Screen.height));
        }
    }
}
