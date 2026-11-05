using System.Collections.Generic;
using Unity.XR.CoreUtils;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;

namespace DeadlyJenga.Presentation
{
    // All VR menus use a tracked controller and that controller's trigger.
    public static class MenuPointer
    {
        public struct Sample { public Ray Ray; public bool Pressed, Held; }
        sealed class TriggerState { public int frame = -1; public bool held, pressed; }
        static readonly Dictionary<int, TriggerState> triggers = new Dictionary<int, TriggerState>();
        static int activeDevice = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { triggers.Clear(); activeDevice = -1; }

        public static bool TryRead(Camera camera, bool preferLeft, out Sample sample)
        {
            sample = default;
            if (!camera) return false;
            var origin = camera.GetComponentInParent<XROrigin>();
            var space = origin && origin.CameraFloorOffsetObject ? origin.CameraFloorOffsetObject.transform : null;
            if (!space) return false;
            XRController chosen = null, fallback = null, preferred = null;
            foreach (var device in InputSystem.devices)
            {
                if (!(device is XRController controller) || !controller.isTracked.isPressed ||
                    device.layout.IndexOf("HandInteraction", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                var trigger = device.TryGetChildControl<AxisControl>("trigger");
                if (trigger == null) continue;
                bool held = trigger.ReadValue() >= .65f;
                if (!triggers.TryGetValue(device.deviceId, out var state))
                    triggers[device.deviceId] = state = new TriggerState { held = held };
                if (state.frame != Time.frameCount)
                {
                    state.pressed = held && !state.held; state.held = held; state.frame = Time.frameCount;
                    if (state.pressed) activeDevice = device.deviceId;
                }
                fallback ??= controller;
                foreach (var usage in device.usages)
                    if (usage == (preferLeft ? CommonUsages.LeftHand : CommonUsages.RightHand)) preferred = controller;
                if (device.deviceId == activeDevice) chosen = controller;
            }
            chosen ??= preferred ?? fallback;
            if (chosen == null) return false;
            var position = chosen.TryGetChildControl<Vector3Control>("pointerPosition") ?? chosen.devicePosition;
            var rotation = chosen.TryGetChildControl<QuaternionControl>("pointerRotation") ?? chosen.deviceRotation;
            var direction = space.TransformDirection(rotation.ReadValue() * Vector3.forward);
            var start = space.TransformPoint(position.ReadValue());
            if (!((start - camera.transform.position).sqrMagnitude < 9) || !(direction.sqrMagnitude > .5f && direction.sqrMagnitude < 2)) return false;
            var selected = triggers[chosen.deviceId];
            sample = new Sample { Ray = new Ray(start, direction.normalized), Pressed = selected.pressed, Held = selected.held };
            Draw(camera, sample.Ray, 4);
            return true;
        }

        public static bool OnPanel(Camera camera, RectTransform panel, bool preferLeft, out Vector2 point, out bool pressed)
        {
            point = default; pressed = false;
            if (!TryRead(camera, preferLeft, out var sample)) return false;
            if (Vector3.Dot(sample.Ray.direction, panel.forward) <= .01f ||
                !new Plane(panel.forward, panel.position).Raycast(sample.Ray, out float distance) || distance > 4) return false;
            point = panel.InverseTransformPoint(sample.Ray.GetPoint(distance)); pressed = sample.Pressed;
            Draw(camera, sample.Ray, distance);
            return true;
        }

        public static void Draw(Camera camera, Ray ray, float distance)
        {
            var laser = camera.GetComponent<MenuLaser>();
            if (!laser) laser = camera.gameObject.AddComponent<MenuLaser>();
            laser.Show(ray, distance);
        }
    }

    public static class MenuAccess
    {
        public static bool HostTools(ISessionInfo session)
        {
            if(session?.CanEditWorld!=true)return false;
            if(session is ILobbyControls lobby)return lobby.IsHost;
            var room=session.Multiplayer;
            return room?.IsFusion==true?room.CanToggleSharedControls:room?.IsClient!=true;
        }
    }

    sealed class MenuLaser : MonoBehaviour
    {
        LineRenderer beam;
        Material ownedMaterial;
        int shownFrame = -1;
        public void Show(Ray ray, float distance)
        {
            if (!beam)
            {
                var go = new GameObject("Local menu controller laser"); go.transform.SetParent(transform, false);
                beam = go.AddComponent<LineRenderer>();
                var material = Resources.Load<Material>("CandyUtilityGlow");
                if (!material)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Unlit");
                    if (shader) material = ownedMaterial = new Material(shader);
                }
                beam.sharedMaterial = material; beam.useWorldSpace = true; beam.positionCount = 2;
                beam.startWidth = .004f; beam.endWidth = .002f;
                beam.startColor = beam.endColor = new Color(.45f, 1f, .8f);
                beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; beam.receiveShadows = false;
            }
            shownFrame = Time.frameCount; beam.enabled = true;
            beam.SetPosition(0, ray.GetPoint(.025f)); beam.SetPosition(1, ray.GetPoint(Mathf.Clamp(distance, .05f, 12)));
        }
        void LateUpdate() { if (beam) beam.enabled = shownFrame == Time.frameCount; }
        void OnDisable() { if (beam) beam.enabled = false; }
        void OnDestroy() { if (ownedMaterial) Destroy(ownedMaterial); }
    }
}
