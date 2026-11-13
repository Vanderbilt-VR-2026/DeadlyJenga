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
        static readonly Vector3 PalmOffset = new Vector3(0, 0, .025f);   // matches CandyPlayerStyle.controllerPalmOffset

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
                // Aim direction comes from the pointer pose, but the ray starts at the visible hand (grip + palm),
                // the same origin weapons fire from. Close menus (pause, item pages) then line up with where the hand points.
                var rotation = controller.TryGetChildControl<QuaternionControl>("pointerRotation") ?? controller.deviceRotation;
                var direction = space.TransformDirection(rotation.ReadValue() * Vector3.forward);
                var grip = controller.deviceRotation.ReadValue();
                if (grip == default) grip = Quaternion.identity;
                var start = space.TransformPoint(controller.devicePosition.ReadValue() + grip * PalmOffset);
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

        internal static bool TryReadDevice(Camera camera, int device, out Sample sample)
        {
            Read(camera, out var left, out var right);
            sample = left.Device == device ? left : right.Device == device ? right : default;
            return sample.Valid;
        }

        internal static Camera TrackingCamera(ISessionInfo session, Camera view)
        {
            var characterCamera = (session.PlayingHand ? session.Hand : session.Player)?.ViewCamera;
            return view.GetComponentInParent<XROrigin>() || !characterCamera ? view : characterCamera;
        }

        internal static bool TriggerHeld(Camera trackingCamera)
        {
            Read(trackingCamera, out var left, out var right);
            return left.Held || right.Held;
        }

        public static bool AimAt(Camera camera, bool preferLeft, System.Func<Ray, float> hitDistance, out Sample sample)
            => AimAt(camera, camera, preferLeft, hitDistance, out sample);

        internal static bool AimAt(Camera camera, Camera trackingCamera, bool preferLeft, System.Func<Ray, float> hitDistance, out Sample sample)
        {
            Read(trackingCamera, out var left, out var right);
            if (camera && trackingCamera && camera != trackingCamera)
            {
                var rotation = camera.transform.rotation * Quaternion.Inverse(trackingCamera.transform.rotation);
                var origin = camera.transform.position - rotation * trackingCamera.transform.position;
                if (left.Valid) left.Ray = new Ray(origin + rotation * left.Ray.origin, rotation * left.Ray.direction);
                if (right.Valid) right.Ray = new Ray(origin + rotation * right.Ray.origin, rotation * right.Ray.direction);
            }
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
            => OnPanel(camera, camera, panel, preferLeft, out point, out pressed);

        internal static bool OnPanel(Camera camera, Camera trackingCamera, RectTransform panel, bool preferLeft, out Vector2 point, out bool pressed)
        {
            point = default;
            pressed = false;
            if (!AimAt(camera, trackingCamera, preferLeft, ray => PanelHit(ray, panel), out var sample)) return false;
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
                return !room.CanToggleSharedControls&&GameExperiencePanel.Resolve(session)?.CanConfigureExperience!=true;
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

    // Laser beam plus an exact hit cursor for every VR menu. The cursor sits on the ray's hit point,
    // faces the eye and keeps the same angular size at any distance.
    sealed class MenuLaser : MonoBehaviour
    {
        readonly LineRenderer[] beams = new LineRenderer[2];
        readonly Transform[] cores = new Transform[2], rims = new Transform[2];
        readonly int[] shownFrames = { -1, -1 };
        Material material, ownedMaterial, coreMaterial, rimMaterial;
        static Mesh disc;
        static int hoverFrame = -1, pressFrame = -1;
        int lastHand;

        // Called by menus that know the ray is over a clickable control this frame.
        internal static void MarkHover(bool pressed)
        {
            hoverFrame = Time.frameCount;
            if (pressed) pressFrame = Time.frameCount;
        }

        static Mesh Disc()
        {
            if (disc) return disc;
            const int n = 28;
            var v = new Vector3[n + 1];
            var tris = new int[n * 3];
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2 / n;
                v[i + 1] = new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * .5f;
                tris[i * 3] = 0; tris[i * 3 + 1] = i + 1; tris[i * 3 + 2] = i == n - 1 ? 1 : i + 2;
            }
            disc = new Mesh { name = "Menu cursor disc", vertices = v, triangles = tris };
            disc.RecalculateBounds();
            return disc;
        }

        Transform Dot(string name, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = Disc();
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        Material DotMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader) return null;
            var m = new Material(shader) { renderQueue = 3150, color = color };
            m.SetFloat("_Cull", 0);
            return m;
        }

        void ShowDot(int hand, Ray ray, float distance)
        {
            if (!coreMaterial)
            {
                coreMaterial = DotMaterial(Color.white);
                rimMaterial = DotMaterial(new Color(.05f, .06f, .12f));
            }
            if (!cores[hand])
            {
                rims[hand] = Dot(hand == 0 ? "Left menu cursor rim" : "Right menu cursor rim", rimMaterial);
                cores[hand] = Dot(hand == 0 ? "Left menu cursor" : "Right menu cursor", coreMaterial);
            }
            var eye = transform.position;
            var point = ray.GetPoint(distance);
            var toPoint = point - eye;
            float range = toPoint.magnitude;
            if (range < .05f) return;
            var dir = toPoint / range;
            bool hover = hoverFrame >= Time.frameCount - 1 && hand == lastHand;
            bool press = pressFrame >= Time.frameCount - 6 && hand == lastHand;
            float size = range * (hover ? .017f : .011f) * (press ? .8f : 1);
            var facing = Quaternion.LookRotation(dir);
            cores[hand].SetPositionAndRotation(point - dir * Mathf.Min(.012f, distance * .5f), facing);
            cores[hand].localScale = Vector3.one * size;
            rims[hand].SetPositionAndRotation(point - dir * Mathf.Min(.008f, distance * .4f), facing);
            rims[hand].localScale = Vector3.one * size * 1.5f;
            coreMaterial.color = press ? new Color(1f, .72f, .55f) : hover ? new Color(.45f, 1f, .8f) : Color.white;
            cores[hand].gameObject.SetActive(true);
            rims[hand].gameObject.SetActive(true);
        }

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
                beam.startWidth = .005f;
                beam.endWidth = .0025f;
                beam.startColor = beam.endColor = new Color(.45f, 1f, .8f);
                beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                beam.receiveShadows = false;
            }
            shownFrames[hand] = Time.frameCount;
            lastHand = hand;
            beam.enabled = true;
            ShowDot(hand, ray, distance);
            beam.SetPosition(0, ray.origin);
            beam.SetPosition(1, ray.GetPoint(distance));
        }

        void LateUpdate()
        {
            for (int hand = 0; hand < beams.Length; hand++)
            {
                bool on = shownFrames[hand] == Time.frameCount;
                if (beams[hand]) beams[hand].enabled = on;
                if (cores[hand] && cores[hand].gameObject.activeSelf != on) { cores[hand].gameObject.SetActive(on); rims[hand].gameObject.SetActive(on); }
            }
        }

        void OnDisable()
        {
            foreach (var beam in beams)
                if (beam) beam.enabled = false;
            for (int hand = 0; hand < 2; hand++)
                if (cores[hand]) { cores[hand].gameObject.SetActive(false); rims[hand].gameObject.SetActive(false); }
        }

        void OnDestroy()
        {
            if (ownedMaterial) Destroy(ownedMaterial);
            if (coreMaterial) Destroy(coreMaterial);
            if (rimMaterial) Destroy(rimMaterial);
        }
    }
}
