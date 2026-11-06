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
        public const float LobbyRange = 10f;
        public const float PanelRange = 4f;

        public struct Sample
        {
            public Ray Ray;
            public bool Pressed, Held;
            internal int Device;
            internal bool Valid => Device != 0;
        }

        sealed class TriggerState { public int frame = -1; public bool held, pressed; }
        static readonly Dictionary<int, TriggerState> triggers = new();
        static int activeDevice;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { triggers.Clear(); activeDevice = 0; }

        static void Read(Camera camera, out Sample left, out Sample right)
        {
            left = right = default;
            if (!camera) return;
            var origin = camera.GetComponentInParent<XROrigin>();
            if (!origin || !origin.CameraFloorOffsetObject) return;
            var space = origin.CameraFloorOffsetObject.transform;
            foreach (var device in InputSystem.devices)
            {
                if (!(device is XRController controller) ||
                    device.layout.IndexOf("HandInteraction", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (!controller.isTracked.isPressed)
                {
                    triggers.Remove(device.deviceId);
                    continue;
                }
                var trigger = device.TryGetChildControl<AxisControl>("trigger");
                if (trigger == null) continue;
                bool isLeft = false, isRight = false;
                foreach (var usage in device.usages)
                {
                    isLeft |= usage == CommonUsages.LeftHand;
                    isRight |= usage == CommonUsages.RightHand;
                }
                if (!isLeft && !isRight) continue;
                var position = controller.TryGetChildControl<Vector3Control>("pointerPosition") ?? controller.devicePosition;
                var rotation = controller.TryGetChildControl<QuaternionControl>("pointerRotation") ?? controller.deviceRotation;
                var direction = space.TransformDirection(rotation.ReadValue() * Vector3.forward);
                var start = space.TransformPoint(position.ReadValue());
                if (!((start - camera.transform.position).sqrMagnitude < 9) ||
                    !(direction.sqrMagnitude > .5f && direction.sqrMagnitude < 2)) continue;
                bool held = trigger.ReadValue() >= .65f;
                if (!triggers.TryGetValue(device.deviceId, out var state))
                    triggers[device.deviceId] = state = new TriggerState { held = held };
                if (state.frame != Time.frameCount)
                {
                    state.pressed = held && !state.held;
                    state.held = held;
                    state.frame = Time.frameCount;
                    if (state.pressed) activeDevice = device.deviceId;
                }
                var sample = new Sample { Ray = new Ray(start, direction.normalized), Pressed = state.pressed, Held = state.held, Device = device.deviceId };
                if (isLeft) left = sample;
                else right = sample;
            }
        }

        public static bool TryRead(Camera camera, bool preferLeft, out Sample sample)
        {
            Read(camera, out var left, out var right);
            if (left.Valid && left.Device == activeDevice) sample = left;
            else if (right.Valid && right.Device == activeDevice) sample = right;
            else sample = Choose(left, right, preferLeft);
            return sample.Valid;
        }

        static Sample Choose(Sample left, Sample right, bool preferLeft)
        {
            if (!left.Valid) return right;
            if (!right.Valid) return left;
            if (left.Pressed != right.Pressed) return left.Pressed ? left : right;
            if (left.Held != right.Held) return left.Held ? left : right;
            return preferLeft ? left : right;
        }

        public static bool AimAt(Camera camera, bool preferLeft, System.Func<Ray, float> hitDistance, out Sample sample)
        {
            Read(camera, out var left, out var right);
            if (!ShowHit(camera, left, 0, hitDistance)) left = default;
            if (!ShowHit(camera, right, 1, hitDistance)) right = default;
            sample = Choose(left, right, preferLeft);
            return sample.Valid;
        }

        static bool ShowHit(Camera camera, Sample sample, int hand, System.Func<Ray, float> hitDistance)
        {
            if (!sample.Valid) return false;
            float distance = hitDistance(sample.Ray);
            if (!(distance > 0 && distance <= LobbyRange)) return false;
            var laser = camera.GetComponent<MenuLaser>();
            if (!laser) laser = camera.gameObject.AddComponent<MenuLaser>();
            laser.Show(hand, sample.Ray, distance);
            return true;
        }

        public static float PanelHit(Ray ray, RectTransform panel, float range = PanelRange)
        {
            if (!panel || !panel.gameObject.activeInHierarchy || Vector3.Dot(ray.direction, panel.forward) <= .01f ||
                !new Plane(panel.forward, panel.position).Raycast(ray, out float distance) || distance > range)
                return 0;
            return panel.rect.Contains(panel.InverseTransformPoint(ray.GetPoint(distance))) ? distance : 0;
        }

        public static bool OnPanel(Camera camera, RectTransform panel, bool preferLeft, out Vector2 point, out bool pressed)
        {
            point = default;
            pressed = false;
            if (!AimAt(camera, preferLeft, ray => PanelHit(ray, panel), out var sample)) return false;
            point = panel.InverseTransformPoint(sample.Ray.GetPoint(PanelHit(sample.Ray, panel)));
            pressed = sample.Pressed;
            return true;
        }
    }

    public static class MenuAccess
    {
        public static bool GuestPause(ISessionInfo session)
        {
            var room=session?.Multiplayer;
            if(room==null)return false;
            if(room.IsFusion&&!string.IsNullOrEmpty(room.RoomInfo))
            {
                if(room.CanToggleSharedControls||GameExperiencePanel.Resolve(session)?.CanConfigureExperience==true)return false;
                if(MatchRuntime.Current?.Rules?.mode==GameModeKind.Demo)return true;
            }
            return room.IsClient;
        }

        public static void ExitGuestMatch(ISessionInfo session)
        {
            var room=session.Multiplayer;
            if(room?.CanLeave!=true||!GuestPause(session))return;
            room.LeaveSession();
            if(!room.IsFusion)MatchRuntime.Current?.ReturnToLobby();
        }

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
        readonly LineRenderer[] beams = new LineRenderer[2];
        readonly int[] shownFrames = { -1, -1 };
        Material material, ownedMaterial;

        public void Show(int hand, Ray ray, float distance)
        {
            var beam = beams[hand];
            if (!beam)
            {
                var go = new GameObject(hand == 0 ? "Left menu laser" : "Right menu laser");
                go.transform.SetParent(transform, false);
                beams[hand] = beam = go.AddComponent<LineRenderer>();
                if (!material)
                {
                    material = Resources.Load<Material>("CandyUtilityGlow");
                    if (!material)
                    {
                        var shader = Shader.Find("Universal Render Pipeline/Unlit");
                        if (shader) material = ownedMaterial = new Material(shader);
                    }
                }
                beam.sharedMaterial = material;
                beam.useWorldSpace = true;
                beam.positionCount = 2;
                beam.startWidth = .004f;
                beam.endWidth = .002f;
                beam.startColor = beam.endColor = new Color(.45f, 1f, .8f);
                beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                beam.receiveShadows = false;
            }
            shownFrames[hand] = Time.frameCount;
            beam.enabled = true;
            beam.SetPosition(0, ray.origin);
            beam.SetPosition(1, ray.GetPoint(distance));
        }

        void LateUpdate()
        {
            for (int hand = 0; hand < beams.Length; hand++)
                if (beams[hand]) beams[hand].enabled = shownFrames[hand] == Time.frameCount;
        }

        void OnDisable()
        {
            foreach (var beam in beams)
                if (beam) beam.enabled = false;
        }

        void OnDestroy() { if (ownedMaterial) Destroy(ownedMaterial); }
    }
}
