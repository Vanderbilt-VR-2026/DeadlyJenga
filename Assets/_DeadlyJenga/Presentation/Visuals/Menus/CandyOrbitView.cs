using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadlyJenga.CastlePrototype
{
    // Shared by the gameplay inspection view and wardrobe mirror.
    public sealed class CandyOrbitView
    {
        public float Yaw, Pitch = 12, Distance = 2.8f;
        float yaw, pitch, distance;
        bool initialized;
        public void Reset(float heading, float range = 2.8f)
        { Yaw = yaw = heading; Pitch = pitch = 12; Distance = distance = range; initialized = true; }
        public void ReadDesktop(bool dragAllowed = true)
        {
            var mouse = Mouse.current; var keys = Keyboard.current;
            if (dragAllowed && mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed))
            { var delta = mouse.delta.ReadValue(); Yaw += delta.x * .2f; Pitch -= delta.y * .2f; }
            if (dragAllowed) Distance -= (mouse?.scroll.ReadValue().y ?? 0) * .0015f;
            float step = Time.unscaledDeltaTime;
            if (keys == null) return;
            Yaw += ((keys.rightArrowKey.isPressed ? 1 : 0) - (keys.leftArrowKey.isPressed ? 1 : 0)) * 70 * step;
            Pitch += ((keys.downArrowKey.isPressed ? 1 : 0) - (keys.upArrowKey.isPressed ? 1 : 0)) * 45 * step;
            Distance += ((keys.minusKey.isPressed ? 1 : 0) - (keys.equalsKey.isPressed ? 1 : 0)) * step;
        }
        public Vector3 Position(Vector3 target, float height, out Quaternion rotation)
        {
            Pitch = Mathf.Clamp(Pitch, -30, 70); Distance = Mathf.Clamp(Distance, 1.1f, 7);
            if (!initialized) Reset(Yaw, Distance);
            float blend = 1 - Mathf.Exp(-18 * Time.unscaledDeltaTime);
            yaw = Mathf.LerpAngle(yaw, Yaw, blend); pitch = Mathf.Lerp(pitch, Pitch, blend); distance = Mathf.Lerp(distance, Distance, blend);
            var direction = Quaternion.Euler(pitch, yaw, 0) * Vector3.back;
            rotation = Quaternion.LookRotation(-direction);
            return target + direction * height * distance;
        }
    }
}
