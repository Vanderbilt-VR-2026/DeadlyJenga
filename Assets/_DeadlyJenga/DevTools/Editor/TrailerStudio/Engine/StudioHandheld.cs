using System;
using System.Collections.Generic;
using System.Linq;
using DeadlyJenga.CandyPlayers;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Film the tape by hand, like a camera operator. While the tape plays back in real time, a pose is
    // read every tape frame and becomes keys on a camera track, which can then be edited like any other.
    //   Desktop: fly the director's camera in the Game view. Hold the right mouse button to look,
    //            W A S D to move, Q and E to go down and up, the mouse wheel to zoom, Shift to go faster.
    //   VR:      the right controller is the camera. The thumbstick zooms. A viewfinder sits above it.
    public static class StudioHandheld
    {
        public static bool Recording { get; private set; }
        public static StudioCameraTrack Target { get; private set; }

        // Tests (and other tools) can supply the camera pose instead of a person: tape time in, world pose and focal length out.
        public static Func<float, (Vector3 position, Quaternion rotation, float focal)> PoseSource;

        static readonly List<StudioCameraKey> captured = new List<StudioCameraKey>();
        static Action<StudioCameraTrack> finished;
        static float focal = 35;
        static GameObject viewfinder;
        static bool hooked;

        public static void Start(StudioCameraTrack camera, int from, int to, Action<StudioCameraTrack> done = null)
        {
            if (StudioRun.State != StudioState.Held) throw new InvalidOperationException("Open the stage, and wait for any run to finish.");
            if (Recording) throw new InvalidOperationException("A handheld take is already recording.");
            Target = camera; finished = done;
            captured.Clear();
            focal = camera.keys.Count > 0 ? camera.keys[0].focal : 35;
            if (!hooked) { StudioRun.FrameShown += Each; StudioRun.Changed += Ended; hooked = true; }
            Recording = true;
            StudioRun.Play(from, to, true);
        }

        static void Each(int frame)
        {
            if (!Recording) return;
            var stage = StudioRun.Stage;
            float time = frame / (float)stage.Fps;
            Vector3 position; Quaternion rotation;
            if (PoseSource != null) (position, rotation, focal) = PoseSource(time);
            else if (stage.session.IsVR && TryController(stage, out position, out rotation)) ShowViewfinder(position, rotation);
            else { Fly(); position = StudioCamera.Camera.transform.position; rotation = StudioCamera.Camera.transform.rotation; }
            // What is being filmed is what the director's camera shows.
            StudioCamera.Camera.transform.SetPositionAndRotation(position, rotation);
            StudioCamera.Camera.fieldOfView = StudioCameras.FieldOfView(focal, Target.sensorHeight);
            var key = new StudioCameraKey { time = time, position = stage.ToStage(position), euler = rotation.eulerAngles, focal = focal,
                focus = Target.keys.Count > 0 ? Target.keys[0].focus : 6, ease = 0 };
            // A headset draws 72-120 frames a second, faster than the tape's frames: keep one pose per tape frame (the latest),
            // so no two keys share a time.
            if (captured.Count > 0 && Mathf.Approximately(captured[captured.Count - 1].time, time)) captured[captured.Count - 1] = key;
            else captured.Add(key);
        }

        // Desktop fly camera, read straight from the keyboard and mouse.
        static void Fly()
        {
            var keys = Keyboard.current; var mouse = Mouse.current;
            if (keys == null || mouse == null) return;
            var view = StudioCamera.Camera.transform;
            float dt = Time.unscaledDeltaTime;
            if (mouse.rightButton.isPressed)
            {
                Vector2 look = mouse.delta.ReadValue() * .12f;
                Vector3 euler = view.rotation.eulerAngles;
                float pitch = euler.x > 180 ? euler.x - 360 : euler.x;
                view.rotation = Quaternion.Euler(Mathf.Clamp(pitch - look.y, -88, 88), euler.y + look.x, 0);
            }
            Vector3 move = Vector3.zero;
            if (keys.wKey.isPressed) move += view.forward;
            if (keys.sKey.isPressed) move -= view.forward;
            if (keys.dKey.isPressed) move += view.right;
            if (keys.aKey.isPressed) move -= view.right;
            if (keys.eKey.isPressed) move += Vector3.up;
            if (keys.qKey.isPressed) move -= Vector3.up;
            view.position += move * (keys.leftShiftKey.isPressed ? 12 : 4) * dt;
            focal = Mathf.Clamp(focal * (1 + mouse.scroll.ReadValue().y * .0008f), 10, 200);
        }

        // VR: the right controller's pose, moved from tracking space into the world through the player's rig.
        static bool TryController(StudioStage stage, out Vector3 position, out Quaternion rotation)
        {
            position = default; rotation = Quaternion.identity;
            var controller = XRController.rightHand;
            var session = stage.session;
            var rig = DeadlyJenga.Core.NetworkActors.Alive(session.Player) ? session.Player.gameObject.GetComponent<CandyPlayerAvatar>() : null;
            if (controller == null || !rig || !rig.trackingOrigin) return false;
            Vector3 local = controller.devicePosition.ReadValue();
            Quaternion turn = controller.deviceRotation.ReadValue();
            position = rig.trackingOrigin.TransformPoint(local);
            rotation = rig.trackingOrigin.rotation * turn;
            // Quest controllers call the stick "thumbstick"; the PC-simulated controllers call it "primary2DAxis".
            var stick = controller.TryGetChildControl<UnityEngine.InputSystem.Controls.Vector2Control>("thumbstick") ??
                controller.TryGetChildControl<UnityEngine.InputSystem.Controls.Vector2Control>("primary2DAxis");
            if (stick != null) focal = Mathf.Clamp(focal * (1 + stick.ReadValue().y * Time.unscaledDeltaTime * .9f), 10, 200);
            return true;
        }

        // A small screen above the controller showing what the camera sees.
        static void ShowViewfinder(Vector3 position, Quaternion rotation)
        {
            if (!viewfinder)
            {
                viewfinder = GameObject.CreatePrimitive(PrimitiveType.Quad);
                viewfinder.name = "Trailer Studio viewfinder";
                UnityEngine.Object.Destroy(viewfinder.GetComponent<Collider>());
                var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                material.SetTexture("_BaseMap", StudioCamera.Image);
                viewfinder.GetComponent<Renderer>().sharedMaterial = material;
                viewfinder.transform.localScale = new Vector3(.16f, .09f, 1);
            }
            viewfinder.transform.SetPositionAndRotation(position + rotation * new Vector3(0, .09f, -.02f), rotation);
        }

        static void Ended()
        {
            if (!Recording || StudioRun.State != StudioState.Held) return;
            Recording = false;
            if (viewfinder) UnityEngine.Object.Destroy(viewfinder);
            var camera = Target;
            if (captured.Count > 1)
            {
                // Keep a key every fifth of a second and let the spline fill in between: smooth, and easy to edit.
                var stage = StudioRun.Stage;
                int step = Mathf.Max(1, Mathf.RoundToInt(stage.Fps * .2f));
                camera.keys.Clear();
                for (int i = 0; i < captured.Count; i += step) camera.keys.Add(captured[i]);
                if ((captured.Count - 1) % step != 0) camera.keys.Add(captured[captured.Count - 1]);
                camera.spline = true; camera.behavior = "";
                camera.start = captured[0].time; camera.end = captured[captured.Count - 1].time;
            }
            StudioCamera.Place();
            var callback = finished; finished = null;
            callback?.Invoke(camera);
        }
    }
}
