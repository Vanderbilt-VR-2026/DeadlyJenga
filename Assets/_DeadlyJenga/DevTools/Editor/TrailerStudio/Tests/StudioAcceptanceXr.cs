using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CastlePrototype;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // Performing in VR without a headset: an emulated headset and two controllers feed the game's normal
    // VR rig, and the Studio's VR paths (real-time clock, wrist panel, handheld camera, playback in the body) run on it.
    //   -executeMethod DeadlyJenga.DevTools.Editor.TrailerStudio.StudioAcceptance.RunXr
    // What is simulated is said in every result. Nothing here proves comfort, tracking quality or frame rate on a Quest.
    public static partial class StudioAcceptance
    {
        // The emulated devices: the same Input System device types as the game's PC-simulated XR (PcSimulatedVR),
        // but posed by the test instead of the keyboard and mouse. Poses are in tracking space, like a real headset's.
        static class XrDevices
        {
            public static Pose head = new Pose(Vector3.zero, Quaternion.identity);
            public static Pose left = new Pose(new Vector3(-.2f, -.35f, .4f), Quaternion.identity);
            public static Pose right = new Pose(new Vector3(.2f, -.35f, .4f), Quaternion.identity);
            public static Vector2 rightStick;
            public static string Layouts = "";

            // What the devices said in the last game-side input update (reading them from the editor loop gives empty values).
            public static Pose seenHead, seenLeft, seenRight;
            public static bool seenTracked;
            public static int seenUpdates;

            static InputDevice hmd, leftDevice, rightDevice;
            static InputSettings saved, copy;
            static bool on;
            static HideFlags savedFlags;

            public static void Start()
            {
                if (on) return;
                on = true;
                // A batch editor never has window focus; keep input flowing as it would in a focused game.
                // The project's settings asset must stay loaded while it is swapped out, or it cannot be put back.
                saved = InputSystem.settings;
                savedFlags = saved.hideFlags; saved.hideFlags |= HideFlags.DontUnloadUnusedAsset;
                copy = Object.Instantiate(saved);
                copy.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                copy.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings = copy;
                string hmdLayout = Layout("XRSimulatedHMD", @"{""name"":""StudioTestHMD"",""extend"":""XRHMD""}");
                string handLayout = Layout("XRSimulatedController", @"{""name"":""StudioTestController"",""extend"":""XRController"",""controls"":[{""name"":""primary2DAxis"",""layout"":""Vector2""},{""name"":""trigger"",""layout"":""Axis""},{""name"":""grip"",""layout"":""Axis""}]}");
                Layouts = hmdLayout + " + 2 x " + handLayout;
                hmd = InputSystem.AddDevice(hmdLayout, "Studio test headset");
                leftDevice = InputSystem.AddDevice(handLayout, "Studio test left controller");
                rightDevice = InputSystem.AddDevice(handLayout, "Studio test right controller");
                InputSystem.SetDeviceUsage(leftDevice, UnityEngine.InputSystem.CommonUsages.LeftHand);
                InputSystem.SetDeviceUsage(rightDevice, UnityEngine.InputSystem.CommonUsages.RightHand);
                InputSystem.onBeforeUpdate += Feed;
                InputSystem.onAfterUpdate += Watch;
            }

            // The XR Interaction Toolkit's simulated device types when they can be found, otherwise a plain XR layout.
            static string Layout(string simulated, string fallbackJson)
            {
                var type = Type.GetType("UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation." + simulated + ", Unity.XR.Interaction.Toolkit");
                if (type != null)
                {
                    if (!InputSystem.ListLayouts().Contains(simulated)) InputSystem.RegisterLayout(type, simulated);
                    return simulated;
                }
                InputSystem.RegisterLayout(fallbackJson);
                return fallbackJson.Split('"')[3];
            }

            public static void Stop()
            {
                if (!on) return;
                on = false;
                InputSystem.onBeforeUpdate -= Feed;
                InputSystem.onAfterUpdate -= Watch;
                foreach (var device in new[] { hmd, leftDevice, rightDevice })
                    if (device != null && device.added) InputSystem.RemoveDevice(device);
                hmd = leftDevice = rightDevice = null;
                if (saved) { InputSystem.settings = saved; saved.hideFlags = savedFlags; }
                if (copy) Object.Destroy(copy);
            }

            // Queued before every input update; the game-side update applies them (editor updates leave game input alone in Play Mode).
            static void Feed()
            {
                if (hmd == null) return;
                Send(hmd, head, Vector2.zero, true);
                Send(leftDevice, left, Vector2.zero, false);
                Send(rightDevice, right, rightStick, false);
            }

            static void Send(InputDevice device, Pose pose, Vector2 stick, bool isHead)
            {
                using (StateEvent.From(device, out var e))
                {
                    Set(device, e, "devicePosition", pose.position);
                    Set(device, e, "deviceRotation", pose.rotation);
                    if (isHead) { Set(device, e, "centerEyePosition", pose.position); Set(device, e, "centerEyeRotation", pose.rotation); }
                    Set(device, e, "isTracked", 1f);
                    Set(device, e, "trackingState", 3);
                    if (!isHead) Set(device, e, "primary2DAxis", stick);
                    InputSystem.QueueEvent(e);
                }
            }

            static void Set<T>(InputDevice device, InputEventPtr e, string name, T value) where T : struct
            {
                if (device.TryGetChildControl(name) is InputControl<T> control) control.WriteValueIntoEvent(value, e);
            }

            // Read the devices now. Only valid inside the game loop (frame callbacks), not from the editor loop.
            public static Pose HeadNow => Read(hmd);
            public static Pose LeftNow => Read(leftDevice);
            public static Pose RightNow => Read(rightDevice);

            static Pose Read(InputDevice device)
            {
                var p = device?.TryGetChildControl("devicePosition") as InputControl<Vector3>;
                var r = device?.TryGetChildControl("deviceRotation") as InputControl<Quaternion>;
                return new Pose(p?.ReadValue() ?? Vector3.zero, r?.ReadValue() ?? Quaternion.identity);
            }

            static void Watch()
            {
                if (InputState.currentUpdateType == InputUpdateType.Editor || hmd == null) return;
                seenHead = Read(hmd); seenLeft = Read(leftDevice); seenRight = Read(rightDevice);
                seenTracked = (rightDevice.TryGetChildControl("isTracked") as InputControl<float>)?.ReadValue() > .5f;
                seenUpdates++;
            }
        }

        // Where the game should put a tracked thing in the world: tracking space through the player's rig.
        static Vector3 PalmWorld(CandyPlayerAvatar avatar, CandyTrackingSource tracking, Pose device) =>
            avatar.trackingOrigin.TransformPoint(device.position + device.rotation * (tracking.style ? tracking.style.controllerPalmOffset : Vector3.zero));

        static string P(Vector3 v) => $"({v.x:F3}, {v.y:F3}, {v.z:F3})";

        sealed class XrSample
        {
            public Vector3 head, left, right;              // where the devices say they are, in the world, on this frame
            public Matrix4x4 toTracking;                   // world to tracking space on this frame
            public Pose deviceHead, deviceLeft, deviceRight;
            public Vector3 camera, cameraLocal;           // the player's view camera (moved by the game's TrackedPoseDriver)
            public bool focused, rebasing;                // the survivor's own "headset is on and settled" flags
        }

        static IEnumerator<int> XrTests()
        {
            var stage = StudioRun.Stage;
            var set = stage.set;
            var tape = stage.tape;
            int fps = set.fps;
            StudioCamera.Resize(640, 360);
            foreach (var mark in set.marks)
                if (StudioStage.SurfaceBelow(stage.ToWorld(mark.position), out var surface)) mark.position = stage.ToStage(surface);
            StudioLibrary.Save(set);
            var ada = set.marks[0];
            var session = stage.session;
            var motor = session.Survivor();
            var avatar = motor.GetComponent<CandyPlayerAvatar>();
            var tracking = motor.GetComponent<CandyTrackingSource>();
            var focus = typeof(CastleSurvivor).GetField("vrInputFocused", BindingFlags.Instance | BindingFlags.NonPublic);
            var rebase = typeof(CastleSurvivor).GetField("trackingRebase", BindingFlags.Instance | BindingFlags.NonPublic);
            int preference = PlayerPrefs.GetInt("DeadlyJenga.PreferredControlMode", 0);
            bool desktopForced = preference == 1 || preference == 4;
            var oldMode = motor.controlMode;
            int forcedFocus = 0;
            bool KeepFocus()
            {
                // The batch editor is never the focused app, and the game treats an unfocused app as "headset taken off".
                if (focus == null || (bool)focus.GetValue(motor)) return false;
                focus.SetValue(motor, true); forcedFocus++;
                return true;
            }

            // ---- a. An emulated XR session: the Studio must switch to the real-time clock ----
            bool vrBefore = session.IsVR;
            int captureBefore = Time.captureFramerate;
            XrDevices.Start();
            motor.controlMode = FlyingHand.ControlMode.VR;
            motor.RefreshMode();
            bool focusedAtStart = focus != null && (bool)focus.GetValue(motor);
            KeepFocus();
            StudioRun.Refresh();    // the Studio's own Hold reads IsVR again and sets the clock
            for (int n = 0; n < 20; n++) { KeepFocus(); yield return 0; }
            bool vrNow = session.IsVR;
            int captureHeld = Time.captureFramerate;
            string simulated = $"Simulated: {XrDevices.Layouts} Input System devices posed by the test (the device types the home menu's PC-simulated XR, PcSimulatedVR, creates; " +
                "PcSimulatedVR itself is keyboard-and-mouse driven and cannot follow a scripted path on this branch). VR mode chosen with the survivor's public controlMode = VR, " +
                $"as the game's own XR checks do (saved control preference is {preference}). No OpenXR runtime, no headset display.";

            // ---- b. Perform a short take in VR: head bob, left hand raised, right hand forward ----
            Vector3 HeadPath(float t) => new Vector3(0, .05f * Mathf.Sin(2 * Mathf.PI * t), 0);
            Quaternion HeadTurn(float t) => Quaternion.Euler(0, 8 * Mathf.Sin(Mathf.PI * t), 0);
            Vector3 LeftPath(float t) => new Vector3(-.2f, -.35f + .45f * Mathf.Clamp01(t / 2), .4f);
            Vector3 RightPath(float t) => new Vector3(.2f, -.35f, .4f + .3f * Mathf.Clamp01(t / 2));
            void PoseAt(float t)
            {
                XrDevices.head = new Pose(HeadPath(t), HeadTurn(t));
                XrDevices.left = new Pose(LeftPath(t), Quaternion.identity);
                XrDevices.right = new Pose(RightPath(t), Quaternion.identity);
            }
            PoseAt(0);
            var samples = new Dictionary<int, XrSample>();
            string driverNote = "";
            int lastSampled = -1, rendered = 0, captureWhileRecording = 0, capturedSamples = 0;
            float scaleWhileRecording = -1;
            void Sample(int frame)
            {
                if (!StudioRun.Recording || StudioRun.LeadIn || StudioRun.Pass == null || StudioRun.Pass.label != "VR take") return;
                Pose h = XrDevices.HeadNow, l = XrDevices.LeftNow, r = XrDevices.RightNow;
                var s = new XrSample
                {
                    deviceHead = h, deviceLeft = l, deviceRight = r,
                    toTracking = avatar.trackingOrigin.worldToLocalMatrix,
                    head = avatar.trackingOrigin.TransformPoint(h.position),
                    left = PalmWorld(avatar, tracking, l), right = PalmWorld(avatar, tracking, r),
                    camera = motor.viewCamera.transform.position, cameraLocal = motor.viewCamera.transform.localPosition,
                    focused = focus != null && (bool)focus.GetValue(motor), rebasing = rebase != null && (bool)rebase.GetValue(motor)
                };
                // In real time one shown frame can stand for several tape frames; they all hold this frame's pose.
                if (driverNote.Length == 0)
                {
                    // What the game's head tracker (TrackedPoseDriver on the view camera) sees on the first recorded frame.
                    var driver = motor.viewCamera.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
                    var position = driver ? driver.positionInput.action : null;
                    var state = driver ? driver.trackingStateInput.action : null;
                    driverNote = driver == null ? "no TrackedPoseDriver on the view camera" :
                        $"TrackedPoseDriver enabled {driver.isActiveAndEnabled}, tracking type {driver.trackingType}, update {driver.updateType}, ignore tracking state {driver.ignoreTrackingState}; " +
                        $"position action enabled {position?.enabled}, bound to {position?.controls.Count ?? 0} controls ({string.Join(", ", position?.controls.Select(c => c.path) ?? new string[0])}), value {P(position != null && position.controls.Count > 0 ? position.ReadValue<Vector3>() : Vector3.zero)}; " +
                        $"tracking state action enabled {state?.enabled}, controls {state?.controls.Count ?? 0}, value {(state != null && state.controls.Count > 0 ? state.ReadValue<int>() : -1)}; headset device position now {P(h.position)}";
                }
                for (int f = Mathf.Max(lastSampled + 1, StudioRun.FirstFrame); f <= frame; f++) samples[f] = s;
                lastSampled = frame; rendered++;
                captureWhileRecording = Mathf.Max(captureWhileRecording, Time.captureFramerate); capturedSamples++;
                scaleWhileRecording = Time.timeScale;
            }
            var pass = new StudioPass { label = "VR take", performer = ada.id, fixedClock = true, countdown = 3, preRoll = 0, to = 3 * fps };
            pass.record.Add(ada.id);
            var countdownTexts = new List<string>();
            float panelToWrist = -1, panelToController = -1, countdownDistance = -1, scaleInCountdown = -1;
            bool panelBuilt = false; float pathStart = -1;
            StudioRun.FrameShown += Sample;
            StudioRun.Record(pass);
            float started = Time.realtimeSinceStartup;
            while (!Held || Time.realtimeSinceStartup - started < .1f)
            {
                if (StudioRun.State == StudioState.Closed) throw new InvalidOperationException("The stage closed: " + StudioRun.Status);
                if (Time.realtimeSinceStartup - started > 120) throw new TimeoutException("The VR take did not finish: " + StudioRun.Status);
                KeepFocus();
                var panel = StudioDrivers.Wrist;
                if (StudioRun.Countdown > 0)
                {
                    panelBuilt |= panel.Built && panel.Panel && panel.Panel.gameObject.activeInHierarchy;
                    string shown = panel.CountdownShown;
                    if (shown.Length > 0 && !countdownTexts.Contains(shown)) countdownTexts.Add(shown);
                    var view = session.ActiveCamera;
                    if (shown.Length > 0 && view) countdownDistance = Vector3.Distance(panel.CountdownPosition, view.transform.position);
                    if (panel.Panel && avatar.LastFrame.scale > 0)
                    {
                        panelToWrist = Vector3.Distance(panel.Panel.position, avatar.LastFrame.left.position);
                        panelToController = Vector3.Distance(panel.Panel.position, tracking.WorldHand(true).position);
                    }
                    scaleInCountdown = Time.timeScale;
                }
                if (StudioRun.Recording && !StudioRun.LeadIn && StudioRun.Countdown <= 0 && pathStart < 0) pathStart = Time.realtimeSinceStartup;
                PoseAt(pathStart < 0 ? 0 : Time.realtimeSinceStartup - pathStart);
                yield return 0;
            }
            StudioRun.FrameShown -= Sample;
            var adaTrack = tape.Find(ada.id);
            var take = adaTrack?.ActiveTake;
            Result("a. The emulated XR session switches the Studio to the real-time clock",
                !vrBefore && vrNow && !desktopForced && captureHeld == 0 && capturedSamples > 0 && captureWhileRecording == 0,
                $"IsVR before {vrBefore}, after switching on the emulated devices {vrNow}. Time.captureFramerate: {captureBefore} before, {captureHeld} while held in VR, " +
                $"at most {captureWhileRecording} over {capturedSamples} recorded frames of a pass that asked for the fixed clock (fixedClock = true), so MustBeRealTime overrode it. " +
                $"{rendered} rendered frames covered {samples.Count} tape frames (real time: a slow batch frame spans several tape frames). Time scale {scaleWhileRecording:0.##} while recording, {scaleInCountdown:0.##} in the countdown. " +
                (focusedAtStart ? "" : $"Forced: CastleSurvivor.vrInputFocused set to true {forcedFocus} times (the batch editor is never the focused app; the game treats that as the headset being off). ") + simulated);

            // Compare every recorded frame with where the devices were on that frame.
            float worstHead = 0, worstLeft = 0, worstRight = 0, worstLocal = 0; int compared = 0;
            float worstCameraToDevice = 0, worstHeadToCamera = 0, cameraLocalLow = float.MaxValue, cameraLocalHigh = float.MinValue;
            int unfocusedFrames = 0, rebasingFrames = 0;
            float headLow = float.MaxValue, headHigh = float.MinValue, leftFirst = 0, leftLast = 0, rightFirst = 0, rightLast = 0;
            if (take != null)
            {
                for (int f = take.start; f < take.End; f++)
                {
                    if (!samples.TryGetValue(f, out var s)) continue;
                    var pose = ActorFrame.Read(adaTrack.type, take.frames[f - take.start]);
                    if (!pose.visible) continue;
                    worstHead = Mathf.Max(worstHead, Vector3.Distance(pose.body.head, s.head));
                    worstCameraToDevice = Mathf.Max(worstCameraToDevice, Vector3.Distance(s.camera, s.head));
                    worstHeadToCamera = Mathf.Max(worstHeadToCamera, Vector3.Distance(pose.body.head, s.camera));
                    if (!s.focused) unfocusedFrames++;
                    if (s.rebasing) rebasingFrames++;
                    cameraLocalLow = Mathf.Min(cameraLocalLow, s.cameraLocal.y); cameraLocalHigh = Mathf.Max(cameraLocalHigh, s.cameraLocal.y);
                    worstLeft = Mathf.Max(worstLeft, Vector3.Distance(pose.body.left.position, s.left));
                    worstRight = Mathf.Max(worstRight, Vector3.Distance(pose.body.right.position, s.right));
                    // Back into tracking space: the recorded head must be where the emulated headset was.
                    Vector3 headLocal = s.toTracking.MultiplyPoint3x4(pose.body.head);
                    worstLocal = Mathf.Max(worstLocal, Vector3.Distance(headLocal, s.deviceHead.position));
                    Vector3 leftLocal = s.toTracking.MultiplyPoint3x4(pose.body.left.position), rightLocal = s.toTracking.MultiplyPoint3x4(pose.body.right.position);
                    headLow = Mathf.Min(headLow, headLocal.y); headHigh = Mathf.Max(headHigh, headLocal.y);
                    if (compared == 0) { leftFirst = leftLocal.y; rightFirst = rightLocal.z; }
                    leftLast = leftLocal.y; rightLast = rightLocal.z;
                    compared++;
                }
            }
            const float Tolerance = .002f;
            float bob = headHigh - headLow, raised = leftLast - leftFirst, forward = rightLast - rightFirst;
            float secondsRecorded = take != null ? take.frames.Count / (float)fps : 0;
            float expectedRaise = .45f * Mathf.Clamp01(secondsRecorded / 2), expectedForward = .3f * Mathf.Clamp01(secondsRecorded / 2);
            Result("b. A take performed in VR stores the tracked head and hands as they were",
                take != null && compared > fps && worstHead < Tolerance && worstLeft < Tolerance && worstRight < Tolerance && worstLocal < Tolerance &&
                bob > .06f && raised > expectedRaise * .6f && forward > expectedForward * .6f,
                $"Take {take?.frames.Count ?? 0} frames; {compared} compared with the emulated devices on the same frame. Largest difference: head {worstHead * 1000:F2} mm, " +
                $"left hand {worstLeft * 1000:F2} mm, right hand {worstRight * 1000:F2} mm; the head taken back into tracking space is {worstLocal * 1000:F2} mm from the headset's own position. " +
                $"Tolerance {Tolerance * 1000:F0} mm: the pose goes from the device to the tape as plain numbers (palm offset {P(tracking.style ? tracking.style.controllerPalmOffset : Vector3.zero)} added by the game), so anything near a centimetre would mean it was re-derived; " +
                $"the desktop arm swing sits 20 cm or more away. In tracking space the head bobbed {bob * 100:F1} cm (fed: 10 cm), the left hand rose {raised * 100:F1} cm (fed: {expectedRaise * 100:F0} cm by the end), " +
                $"the right hand went forward {forward * 100:F1} cm (fed: {expectedForward * 100:F0} cm). Emulated device updates seen: {XrDevices.seenUpdates}. " +
                $"Head diagnosis: the view camera was at most {worstCameraToDevice * 1000:F1} mm from the headset pose through the rig, and the recorded head at most {worstHeadToCamera * 1000:F1} mm from the camera; " +
                $"camera local height {cameraLocalLow:F3} to {cameraLocalHigh:F3} m; frames with the survivor's tracking-rebase flag on {rebasingFrames}, unfocused {unfocusedFrames}; player scale {motor.playerScale:F2}, eye height {motor.eyeHeight:F2}. {driverNote}.");

            // ---- c. The wrist panel: countdown, and keys pressed with the right index fingertip ----
            StudioPerform.Actor = ada.id;
            StudioPerform.Countdown = false; StudioPerform.PreRoll = 0; StudioPerform.Loop = false;
            StudioPerform.PunchIn = false; StudioPerform.LivePhysics = false; StudioPerform.Speed = 1;
            // A comfortable pose for reading the panel: left wrist in front of the chest, right hand off to the side.
            XrDevices.head = new Pose(Vector3.zero, Quaternion.identity);
            XrDevices.left = new Pose(new Vector3(-.12f, -.28f, .32f), Quaternion.Euler(0, 30, 0));
            XrDevices.right = new Pose(new Vector3(.25f, -.35f, .35f), Quaternion.identity);
            for (int n = 0; n < 20; n++) { KeepFocus(); yield return 0; }
            var touches = new List<string>();
            float tipMiss = 0, palmAtPress = 0;
            // Move the emulated right controller so its index fingertip goes to a key and back out, like a finger press.
            IEnumerable<int> Touch(int index, Func<bool> done)
            {
                var wrist = StudioDrivers.Wrist;
                float begin = Time.realtimeSinceStartup, reached = -1, nearest = float.MaxValue, palmNearest = 0;
                bool ok = false;
                while (Time.realtimeSinceStartup - begin < 4)
                {
                    KeepFocus();
                    float t = Time.realtimeSinceStartup - begin;
                    if (!wrist.Built || !wrist.Panel || !tracking.TryIndexTip(false, out var tip)) { yield return 0; continue; }
                    Vector3 key = wrist.KeyPosition(index), outward = -wrist.Panel.forward;
                    float depth = reached < 0 ? Mathf.Max(0, .12f - t * .25f) : Mathf.Min(.12f, (Time.realtimeSinceStartup - reached) * .3f);
                    Vector3 aim = key + outward * depth;
                    // Far away (the player was just put back on the mark): go straight to the hover point, so the hand
                    // never sweeps across the panel. Close by: move gently, like a finger.
                    Vector3 move = (aim - tip).magnitude > .1f ? aim - tip : Vector3.ClampMagnitude(aim - tip, .05f) * .5f;
                    var right = XrDevices.right;
                    right.position += avatar.trackingOrigin.InverseTransformVector(move);
                    XrDevices.right = right;
                    float distance = Vector3.Distance(tip, key);
                    if (distance < nearest) { nearest = distance; palmNearest = Vector3.Distance(avatar.LastFrame.right.position, key); }
                    if (!ok && done()) { ok = true; reached = Time.realtimeSinceStartup; }
                    if (reached < 0 && depth <= 0 && t > 1.5f) reached = Time.realtimeSinceStartup;   // gave up pushing; pull back
                    if (reached >= 0 && depth >= .12f) break;
                    yield return 0;
                }
                tipMiss = Mathf.Max(tipMiss, ok ? 0 : nearest); palmAtPress = Mathf.Max(palmAtPress, palmNearest);
                touches.Add($"{wrist.KeyLabel(index)}: {(ok ? "pressed" : "NOT pressed")} (fingertip came within {nearest * 100:F1} cm of the key, the palm was {palmNearest * 100:F1} cm away)");
                pressedLast = ok;
            }
            int takesBefore = adaTrack.takes.Count;
            foreach (int wait in Touch(0, () => StudioRun.State == StudioState.Running)) yield return wait;
            bool recStarted = pressedLast;
            string labelWhileRunning = StudioDrivers.Wrist.Built ? StudioDrivers.Wrist.KeyLabel(0) : "";
            float waitRolling = Time.realtimeSinceStartup;
            while (StudioRun.State == StudioState.Running && (!StudioRun.Recording || StudioRun.LeadIn || StudioRun.Frame < 5) && Time.realtimeSinceStartup - waitRolling < 10) { KeepFocus(); yield return 0; }
            float speedFirst = StudioPerform.Speed;   // 1 unless a key was pressed by accident on the way
            StudioPerform.Speed = 1;
            float speedBefore = StudioPerform.Speed;
            foreach (int wait in Touch(4, () => !Mathf.Approximately(StudioPerform.Speed, speedBefore))) yield return wait;
            bool speedChanged = pressedLast;
            string speedLabel = StudioDrivers.Wrist.Built ? StudioDrivers.Wrist.KeyLabel(4) : "";
            foreach (int wait in Touch(0, () => StudioRun.State != StudioState.Running)) yield return wait;
            bool stopped = pressedLast;
            foreach (int wait in UntilHeld()) yield return wait;
            var stoppedTake = adaTrack.ActiveTake;
            bool shortTake = adaTrack.takes.Count == takesBefore + 1 && stoppedTake != null && stoppedTake.frames.Count < set.Frames;
            Result("c. The wrist panel on the left wrist: countdown, and keys pressed with the right index fingertip",
                panelBuilt && countdownTexts.Contains("3") && countdownTexts.Contains("1") && panelToWrist > 0 && panelToWrist < .13f &&
                countdownDistance > 1.3f && countdownDistance < 1.5f && recStarted && labelWhileRunning == "STOP" && speedChanged && Mathf.Approximately(speedFirst, 1) && Mathf.Approximately(StudioPerform.Speed, .5f) && stopped && shortTake,
                $"In the VR take's 3-2-1 the panel was {(panelBuilt ? "shown" : "NOT shown")} {panelToWrist * 100:F1} cm from the avatar's left wrist ({panelToController * 100:F1} cm from the tracked left controller), " +
                $"the countdown showed {string.Join(", ", countdownTexts)} at {countdownDistance:F2} m in front of the eyes. Touches by the emulated right controller's fingertip (IFingerTips, as the game's menus use): " +
                $"{string.Join("; ", touches)}. REC started a run: {recStarted}; its label then read \"{labelWhileRunning}\". Slow motion before the speed key {speedFirst:0.##} (1 = no key fired by accident on the way), then from {speedBefore:0.##} to {StudioPerform.Speed:0.##} (label \"{speedLabel}\"). " +
                $"STOP ended the run early: {stopped}; the new take has {stoppedTake?.frames.Count ?? 0} of {set.Frames} frames. " +
                $"Before this branch's fix the panel pressed with the palm point, which was {palmAtPress * 100:F1} cm from the key at the closest moment (keys fire within 3 cm), so a fingertip press could not fire. " +
                "Simulated: the controller is moved by the test, not a hand; no physical reach or comfort is tested.");
            StudioPerform.Speed = 1;

            // ---- d. Handheld camera: the right controller is the camera ----
            var handheld = new StudioCameraTrack { name = "Handheld VR", color = 6 };
            set.cameras.Add(handheld);
            Vector3 CameraPath(float t) => new Vector3(.15f + .35f * Mathf.Sin(t * Mathf.PI / 4), -.15f + .1f * t, .35f);
            Quaternion CameraTurn(float t) => Quaternion.Euler(-5 * t, -25 * t, 0);
            var truth = new Dictionary<int, Pose>();
            int lastTruth = -1; bool viewfinderSeen = false;
            float sameFrameGap = 0, sameFrameAngle = 0;
            void Watch(int frame)
            {
                if (!StudioHandheld.Recording) return;
                var o = avatar.trackingOrigin;
                var device = XrDevices.RightNow;   // read on the same frame, as the Studio does
                var pose = new Pose(o.TransformPoint(device.position), o.rotation * device.rotation);
                for (int f = Mathf.Max(lastTruth + 1, StudioRun.FirstFrame); f <= frame; f++) truth[f] = pose;
                // This runs after the Studio's own handler on the same frame, so the director's camera holds what it just read.
                var cam = StudioCamera.Camera ? StudioCamera.Camera.transform : null;
                if (cam) { sameFrameGap = Mathf.Max(sameFrameGap, Vector3.Distance(cam.position, pose.position)); sameFrameAngle = Mathf.Max(sameFrameAngle, Quaternion.Angle(cam.rotation, pose.rotation)); }
                lastTruth = frame;
                viewfinderSeen |= GameObject.Find("Trailer Studio viewfinder");
            }
            StudioHandheld.PoseSource = null;
            float handheldStart = Time.realtimeSinceStartup;
            bool handheldDone = false;
            XrDevices.right = new Pose(CameraPath(0), CameraTurn(0));
            StudioHandheld.Start(handheld, 0, 2 * fps, _ => handheldDone = true);
            StudioRun.FrameShown += Watch;   // after Start, so it runs after the Studio's own frame handler
            while (!Held || Time.realtimeSinceStartup - handheldStart < .1f)
            {
                if (Time.realtimeSinceStartup - handheldStart > 120) throw new TimeoutException("The handheld take did not finish.");
                KeepFocus();
                float t = Time.realtimeSinceStartup - handheldStart;
                XrDevices.right = new Pose(CameraPath(t), CameraTurn(t));
                XrDevices.rightStick = t > 1 ? Vector2.up : Vector2.zero;   // push the stick to zoom in for the second half
                yield return 0;
            }
            yield return 0;
            StudioRun.FrameShown -= Watch;
            XrDevices.rightStick = Vector2.zero;
            float keyWorst = 0, angleWorst = 0; int keysCompared = 0;
            foreach (var key in handheld.keys)
            {
                int frame = Mathf.RoundToInt(key.time * fps);
                if (!truth.TryGetValue(frame, out var real)) continue;
                keyWorst = Mathf.Max(keyWorst, Vector3.Distance(stage.ToWorld(key.position), real.position));
                angleWorst = Mathf.Max(angleWorst, Quaternion.Angle(Quaternion.Euler(key.euler), real.rotation));
                keysCompared++;
            }
            float travel = handheld.keys.Count > 1 ? Vector3.Distance(handheld.keys[0].position, handheld.keys[handheld.keys.Count - 1].position) : 0;
            float focalFirst = handheld.keys.Count > 0 ? handheld.keys[0].focal : 0, focalLast = handheld.keys.Count > 0 ? handheld.keys[handheld.keys.Count - 1].focal : 0;
            bool viewfinderGone = !GameObject.Find("Trailer Studio viewfinder");
            Result("d. Handheld camera in VR follows the emulated right controller",
                handheldDone && keysCompared >= 5 && keyWorst < .001f && angleWorst < .1f && travel > .1f && viewfinderSeen && viewfinderGone && focalLast > focalFirst * 1.2f,
                $"{handheld.keys.Count} keys kept ({keysCompared} compared with the controller's pose on their frame): worst {keyWorst * 1000:F3} mm and {angleWorst:F3} degrees. " +
                $"On each frame the director's camera was at most {sameFrameGap * 1000:F3} mm and {sameFrameAngle:F3} degrees from the controller pose read on that frame. " +
                $"The camera travelled {travel:F2} m. Viewfinder shown above the controller: {viewfinderSeen}, removed afterwards: {viewfinderGone}. " +
                $"Pushing the stick zoomed from {focalFirst:F1} mm to {focalLast:F1} mm (before this branch's fix the Studio only read a control called \"thumbstick\", which the PC-simulated controllers do not have; " +
                "Quest controllers do have it, so on a headset zoom worked already). Simulated: controller pose and stick written by the test.");

            // ---- e. Playback while in VR: the performer stays in their body ----
            int played = 0, doubleShown = 0, captureInPlay = 0, firstPlayed = -1, lastPlayed = -1;
            void WhilePlaying(int frame)
            {
                played++;
                if (firstPlayed < 0) firstPlayed = frame;
                lastPlayed = frame;
                captureInPlay = Mathf.Max(captureInPlay, Time.captureFramerate);
                var body = stage.Live(ada.id);
                if (body && body.activeInHierarchy && stage.playback.TryGetValue(ada.id, out var copy) && copy && copy.activeInHierarchy) doubleShown++;
            }
            StudioRun.FrameShown += WhilePlaying;
            StudioRun.Play(0, -1, false);   // asks for the exact (fixed) clock; in VR the Studio must still play in real time
            float playStart = Time.realtimeSinceStartup;
            while (StudioRun.State == StudioState.Running && Time.realtimeSinceStartup - playStart < 1.5f) { KeepFocus(); yield return 0; }
            bool stillRunning = StudioRun.State == StudioState.Running;
            StudioRun.Stop();
            foreach (int wait in UntilHeld()) yield return wait;
            StudioRun.FrameShown -= WhilePlaying;
            bool inBody = stage.performer == ada.id && stage.Live(ada.id) && stage.Live(ada.id).activeInHierarchy && session.IsVR;
            Result("e. Playback in VR keeps the performer in their body; Stop returns to Held",
                played > 5 && lastPlayed > firstPlayed && captureInPlay == 0 && stillRunning && Held && inBody,
                $"Played {played} rendered frames (tape frames {firstPlayed} to {lastPlayed}) with Time.captureFramerate at most {captureInPlay}, although the call asked for the exact clock. " +
                $"Stopped after 1.5 s while still running: {stillRunning}; state now {StudioRun.State}; still in {ada.actor}'s body in VR: {inBody}.");
            Result("e2. The performer's own take is not shown a second time during VR playback", doubleShown == 0,
                $"On {doubleShown} of {played} played frames {ada.actor}'s recorded copy was shown while the live performer stood in the same actor. " +
                "PlayRoutine keeps the person in their body in VR but builds a player for every track, the performer's included (Hold leaves it out). " +
                "Not changed here: whether to hide the copy (Hold's rule) or keep it for handheld filming of one's own take is the builder's decision; see the report.");

            // ---- Leave VR and the emulated devices ----
            StudioRun.LeaveBody();
            motor.controlMode = oldMode;
            motor.RefreshMode();
            XrDevices.Stop();
            StudioRun.Refresh();
            yield return 0;
            Result("Runtime errors during the Studio tests", exceptions == 0, exceptions == 0 && errorLines.Count == 0 ? "None." : $"{exceptions} exceptions. First messages: {string.Join(" // ", errorLines)}");
            StudioRun.SaveTape();
        }

        static bool pressedLast;
    }
}
