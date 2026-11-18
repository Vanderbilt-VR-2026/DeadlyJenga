using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CandyPhysicsHands;
using DeadlyJenga.Core;
using DeadlyJenga.DevTools;
using DeadlyJenga.Presentation;
using FlyingHand;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    // The player's own hands in VR, driven only through emulated XR devices (PcSimulatedVR controllers and
    // SimulatedTrackedHands XR Hands joints) in the real Game.unity. It tries to break them: wild fingers, broken
    // joints, flickering tracking, putting a controller down, losing the bare hand while the controller sits on a
    // desk (looking up on a Quest), and swapping sources ten times a second.
    // Runs only when asked. Saves nothing. Emulated devices are not a headset.
    [InitializeOnLoad]
    public static class PlayerHandSyncCheck
    {
        const string Key = "PlayerHandSyncCheck";
        static readonly Stack<IEnumerator> work = new Stack<IEnumerator>();
        static string Report => ProjectPath.Report("PlayerHandSync.txt");
        static PlayerHandSyncCheck() { EditorApplication.playModeStateChanged += Changed; }

        [MenuItem("Tools/Deadly Jenga/Validate Player Hand Sync")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start in Edit Mode.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            File.WriteAllText(Report, "Player hand sync checks (Game.unity, emulated XR controllers and tracked hands)\n");
            SessionState.SetInt(Key + "Failures", 0); SessionState.SetInt(Key + "Checks", 0); SessionState.SetInt(Key, 1);
            MatchSetup.FromLobby = false; MatchSetup.StartLocalSession = false;
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            EditorApplication.EnterPlaymode();
        }
        static void Changed(PlayModeStateChange state)
        {
            if (SessionState.GetInt(Key, 0) == 0) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                Application.logMessageReceived += Log;
                work.Push(Play());
                EditorApplication.update += Tick;
            }
            if (state != PlayModeStateChange.EnteredEditMode) return;
            Application.logMessageReceived -= Log; RestoreInput();
            int failures = SessionState.GetInt(Key + "Failures", 0);
            if (SessionState.GetInt(Key + "Checks", 0) == 0) { failures++; Line("FAIL: Play Mode ended before any checks ran."); }
            SessionState.SetInt(Key, 0);
            Line(failures > 0 ? "FAILED (" + failures + ")" : "COMPLETE: editor checks passed; Quest hardware not tested.");
            if (Application.isBatchMode) EditorApplication.Exit(failures > 0 ? 1 : 0);
        }
        static void Line(string text) => File.AppendAllText(Report, text + "\n");
        static void Fail() => SessionState.SetInt(Key + "Failures", SessionState.GetInt(Key + "Failures", 0) + 1);
        static int errors;
        static void Log(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (errors++ < 5) { Fail(); Line("FAIL Unity: " + message + "\n" + stack); }
        }
        static void Tick()
        {
            try
            {
                if (work.Count == 0) { EditorApplication.update -= Tick; EditorApplication.ExitPlaymode(); return; }
                Pump();
                if (watching) Sample();
                var next = work.Peek();
                if (!next.MoveNext()) { work.Pop(); return; }
                if (next.Current is IEnumerator nested) work.Push(nested);
            }
            catch (Exception e) { Fail(); Line("FAIL " + e); work.Clear(); }
        }
        static void Check(bool condition, string message)
        {
            SessionState.SetInt(Key + "Checks", SessionState.GetInt(Key + "Checks", 0) + 1);
            if (!condition) Fail();
            Line((condition ? "PASS " : "FAIL ") + message);
        }
        static IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
        }

        static bool focusChanged; static UnityEngine.InputSystem.InputSettings.BackgroundBehavior oldBackground; static UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
        static void AllowUnfocusedInput()
        {
            if (focusChanged) return;
            var settings = UnityEngine.InputSystem.InputSystem.settings;
            oldBackground = settings.backgroundBehavior; oldEditorInput = settings.editorInputBehaviorInPlayMode; focusChanged = true;
            settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        }
        static void RestoreInput()
        {
            if (!focusChanged) return;
            var settings = UnityEngine.InputSystem.InputSystem.settings;
            settings.backgroundBehavior = oldBackground; settings.editorInputBehaviorInPlayMode = oldEditorInput; focusChanged = false;
        }

        // ---------- what each emulated hand is doing ----------
        sealed class Side
        {
            public bool controllerTracked, handTracked;
            public Pose controller, wrist;          // tracking space
            public float grip, trigger;
            public CandyHandSkeleton joints;        // wrist-local, real-hand proportions
            public Vector3[] rawJoints;             // broken tracking, when set
        }
        static readonly Side leftSide = new Side(), rightSide = new Side();
        static readonly Vector3 LeftHome = new Vector3(-.2f, -.3f, .4f), RightHome = new Vector3(.2f, -.3f, .4f);
        static void Pump()
        {
            var vr = PcSimulatedVR.Instance; var hands = SimulatedTrackedHands.Instance;
            if (!vr || !hands) return;
            vr.TestLeftUntracked = !leftSide.controllerTracked; vr.TestLeftPose = leftSide.controller; vr.TestLeftGrip = leftSide.grip; vr.TestLeftTrigger = leftSide.trigger;
            vr.TestRightUntracked = !rightSide.controllerTracked; vr.TestRightPose = rightSide.controller; vr.TestRightGrip = rightSide.grip; vr.TestRightTrigger = rightSide.trigger;
            if (leftSide.rawJoints != null) hands.Set(true, leftSide.handTracked, leftSide.wrist, leftSide.rawJoints); else hands.Set(true, leftSide.handTracked, leftSide.wrist, leftSide.joints);
            if (rightSide.rawJoints != null) hands.Set(false, rightSide.handTracked, rightSide.wrist, rightSide.rawJoints); else hands.Set(false, rightSide.handTracked, rightSide.wrist, rightSide.joints);
        }
        // Every XR controller the Input System has, and what the game makes of each side.
        static void Devices(string when)
        {
            string text = "";
            foreach (var device in UnityEngine.InputSystem.InputSystem.devices)
                if (device is UnityEngine.InputSystem.XR.XRController controller)
                    text += $" [{controller.name} usages={string.Join("/", controller.usages)} enabled={controller.enabled} tracked={controller.isTracked.isPressed} at={controller.devicePosition.ReadValue():F2}]";
            Line($"DEVICES {when}:{text}; game sees left controller={HybridXRInput.ControllerTracked(true)} right controller={HybridXRInput.ControllerTracked(false)}; playing bare-handed left={HybridXRInput.PlayingBareHanded(true)} right={HybridXRInput.PlayingBareHanded(false)}");
        }
        // Two controllers in use, as the game sees it.
        static bool Both() => HybridXRInput.ControllerTracked(true) && HybridXRInput.ControllerTracked(false);
        static CandyHandSkeleton Shape(bool left, DesktopHandPose pose, float clench = 0) =>
            SimulatedTrackedHands.RealHand(left, DesktopHandPoses.Build(left, pose, clench));

        // ---------- what the drawn hands look like ----------
        static CandyPlayerAvatar avatar; static Transform[] bones; static Camera view; static bool watching;
        static float worstRoll, worstSide, worstBack, worstRate, worstHandStep, worstViewStep; static bool sawNaN;
        static readonly float[] lastBend = new float[45]; static bool hasLast; static Vector3 lastHand, lastView; static bool hasLastHand;
        const int WatchedSide = 1;   // right hand: the one that changes source in these tests
        static void Watch()
        {
            watching = true; worstRoll = worstSide = worstBack = worstRate = worstHandStep = worstViewStep = 0; sawNaN = false; hasLast = hasLastHand = false;
        }
        static void Sample()
        {
            if (!avatar || bones == null || !view) return;
            for (int side = 0; side < 2; side++)
            {
                var palm = bones[side == 0 ? 4 : 7]; Quaternion fromPalm = Quaternion.Inverse(palm.rotation);
                for (int f = 1; f < 5; f++)
                {
                    Vector3 knuckle = fromPalm * bones[14 + side * 15 + f * 3].up;
                    float flat = new Vector2(knuckle.x, knuckle.z).magnitude;
                    Vector3 along = flat > .3f ? new Vector3(knuckle.x, 0, knuckle.z) / flat : Vector3.forward, across = Vector3.Cross(Vector3.up, along), parent = Vector3.zero;
                    for (int j = 0; j < 3; j++)
                    {
                        int i = 14 + side * 15 + f * 3 + j; var bone = bones[i]; Vector3 d = fromPalm * bone.up;
                        if (float.IsNaN(d.x) || float.IsNaN(bone.position.x)) sawNaN = true;
                        if (j > 0)
                        {
                            var before = bones[i - 1]; Quaternion relative = Quaternion.Inverse(before.rotation) * bone.rotation;
                            worstRoll = Mathf.Max(worstRoll, Mathf.Abs(Mathf.DeltaAngle(0, 2 * Mathf.Atan2(relative.y, relative.w) * Mathf.Rad2Deg)));
                            if (flat > .3f) worstSide = Mathf.Max(worstSide, Mathf.Asin(Mathf.Clamp01(Mathf.Abs(Vector3.Dot(d, across)))) * Mathf.Rad2Deg);
                        }
                        float bend = j == 0 ? Mathf.Atan2(-d.y, Vector3.Dot(d, along)) * Mathf.Rad2Deg : Vector3.SignedAngle(parent, d, across);
                        if (j > 0) worstBack = Mathf.Max(worstBack, -bend);
                        if (hasLast && Time.deltaTime > 0) worstRate = Mathf.Max(worstRate, Mathf.Abs(Mathf.DeltaAngle(lastBend[i], bend)) / Time.deltaTime);
                        lastBend[i] = bend; parent = d;
                    }
                }
            }
            hasLast = true;
            // Hand and view movement per frame, measured against the player's own body so walking does not count.
            Vector3 hand = view.transform.InverseTransformPoint(bones[WatchedSide == 0 ? 4 : 7].position), eye = avatar.transform.InverseTransformPoint(view.transform.position);
            if (hasLastHand) { worstHandStep = Mathf.Max(worstHandStep, Vector3.Distance(hand, lastHand)); worstViewStep = Mathf.Max(worstViewStep, Vector3.Distance(eye, lastView)); }
            lastHand = hand; lastView = eye; hasLastHand = true;
        }
        static void Judge(string what, float handStepLimit = .12f)
        {
            Line($"HANDS {what}: roll between finger bones {worstRoll:F1} deg, sideways lean {worstSide:F1} deg, joint bent backwards {worstBack:F1} deg, fastest joint {worstRate:F0} deg/s, largest hand step in one frame {worstHandStep * 100:F1} cm, largest view step {worstViewStep * 100:F1} cm, NaN={sawNaN}");
            Check(!sawNaN, what + ": nothing in the drawn hands became not-a-number");
            Check(worstRoll < 5, what + ": no roll between neighbouring finger bones (" + worstRoll.ToString("F1") + " deg)");
            Check(worstSide < 4, what + ": fingers only curl in their own plane (" + worstSide.ToString("F1") + " deg sideways)");
            Check(worstBack < 9, what + ": no finger joint bends backwards (" + worstBack.ToString("F1") + " deg)");
            Check(worstRate < 3400, what + ": no finger joint moves faster than a finger can (" + worstRate.ToString("F0") + " deg/s)");
            Check(worstHandStep < handStepLimit, what + ": the hand never jumps (largest step " + (worstHandStep * 100).ToString("F1") + " cm in a frame)");
            Check(worstViewStep < .05f, what + ": the view never jumps (largest step " + (worstViewStep * 100).ToString("F1") + " cm in a frame)");
            Watch();
        }
        static void Shot(string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null || !view) return;
            var cameraObject = new GameObject("Hand sync evidence camera"); var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
            Vector3 focus = (bones[4].position + bones[7].position) * .5f;
            camera.transform.position = focus + view.transform.rotation * new Vector3(.25f, .35f, .75f); camera.transform.LookAt(focus);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.14f, .18f, .23f); camera.fieldOfView = 38; camera.nearClipPlane = .02f;
            var lightObject = new GameObject("Hand sync evidence light"); var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.1f; light.transform.rotation = Quaternion.LookRotation(focus - camera.transform.position);
            var rt = new RenderTexture(900, 700, 24); var old = RenderTexture.active; Texture2D pixels = null;
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt; pixels = new Texture2D(900, 700, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 900, 700), 0, 0); pixels.Apply(); File.WriteAllBytes(ProjectPath.Report("PlayerHands-" + name + ".png"), pixels.EncodeToPNG());
            }
            finally { RenderTexture.active = old; camera.targetTexture = null; Object.Destroy(rt); if (pixels) Object.Destroy(pixels); Object.Destroy(cameraObject); Object.Destroy(lightObject); }
        }

        static IEnumerator Play()
        {
            float until = Time.realtimeSinceStartup + 60;
            while (!(Services.Session is CastlePlaySession) && Time.realtimeSinceStartup < until) yield return null;
            var session = Services.Session as CastlePlaySession;
            Check(session, "Game.unity started a play session");
            if (!session) yield break;
            if (session.Social != null) session.Social.VoiceEnabled = false;
            MatchSetup.ModeId = "demo"; MatchSetup.FromLobby = false; MatchSetup.ApplyMap();
            session.SurvivorBots?.Clear(); session.HandBots?.Clear();
            yield return Wait(1);

            var survivor = session.Survivor();
            int preference = PlayerPrefs.GetInt("DeadlyJenga.PreferredControlMode", 0);
            Line("SETUP saved control mode preference is " + preference + (preference == 1 || preference == 4 ? " (desktop): this run cannot force VR and will fail its first check" : ""));
            AllowUnfocusedInput(); PcSimulatedVR.Create(); SimulatedTrackedHands.Create();
            // Both controllers in hand to begin with.
            leftSide.controllerTracked = rightSide.controllerTracked = true;
            leftSide.controller = new Pose(LeftHome, Quaternion.identity); rightSide.controller = new Pose(RightHome, Quaternion.identity);
            leftSide.joints = Shape(true, DesktopHandPose.Flat); rightSide.joints = Shape(false, DesktopHandPose.Flat);
            Pump();
            survivor.controlMode = ControlMode.VR; survivor.RefreshMode();
            yield return Wait(2);
            avatar = survivor.GetComponent<CandyPlayerAvatar>(); view = session.ActiveCamera;
            Check(survivor.IsVR && avatar && avatar.Rig && avatar.Rig.bodyRenderer, "The survivor is in VR with its avatar");
            if (!survivor.IsVR || !avatar || !avatar.Rig) yield break;
            // The rig's own bone list (palms at 4 and 7, fingers from 14). The renderer's list is not in that order.
            bones = (Transform[])typeof(CandyPlayerRig).GetField("bones", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(avatar.Rig);
            Line($"SETUP rig bones {bones.Length}; palm bones '{bones[4].name}' and '{bones[7].name}'; first right finger bone '{bones[14 + 15].name}'");
            Devices("at start");
            Check(bones.Length >= 44, "The avatar rig has its hand bones");
            Check(Both(), "Both emulated controllers are seen (dual joystick mode)");
            var tracking = survivor.GetComponent<CandyTrackingSource>();
            var random = new System.Random(20261118); float R() => (float)random.NextDouble();

            // 1. Two controllers: squeeze and release.
            Watch();
            for (int n = 0; n < 6; n++) { rightSide.grip = n % 2; rightSide.trigger = (n / 2) % 2; leftSide.grip = (n + 1) % 2; yield return Wait(.35f); }
            Shot("1-two-controllers");
            Judge("Two controllers, squeezing");

            // 2. Put the right controller down and use the bare hand: dual joystick becomes joystick plus hand.
            Vector3 desk = RightHome + new Vector3(.15f, -.35f, .1f);
            rightSide.grip = rightSide.trigger = 0; rightSide.controller = new Pose(desk, Quaternion.Euler(0, 40, 90));
            yield return Wait(.3f);
            Watch();
            rightSide.controllerTracked = false; rightSide.handTracked = true;
            rightSide.wrist = new Pose(RightHome + new Vector3(.05f, .12f, -.05f), Quaternion.Euler(-20, -30, 15)); rightSide.joints = Shape(false, DesktopHandPose.Spread);
            float changed = Time.time, shown = -1;
            while (Time.time - changed < 1.6f)
            {
                yield return null;
                if (shown < 0 && tracking.WorldHand(false).skeletonTracked) shown = Time.time - changed;
            }
            Line($"SWITCH controller put down: bare hand shown after {shown:F2} s; single controller {HybridXRInput.SingleController}; hybrid {tracking.IsHybrid}");
            Check(shown >= 0 && shown < .3f, "Controller put down: the bare hand takes over at once (" + shown.ToString("F2") + " s), not after a freeze");
            Check(HybridXRInput.SingleController && tracking.IsHybrid, "Controller put down: the game is in joystick plus hand mode");
            Shot("2-joystick-plus-hand");
            Judge("Dual joystick to joystick plus hand");

            // 3. Someone trying to break the bare hand.
            var shapes = new[] { Shape(false, DesktopHandPose.Flat), Shape(false, DesktopHandPose.Fist), Shape(false, DesktopHandPose.Pointer), Shape(false, DesktopHandPose.Peace), Shape(false, DesktopHandPose.Spread), Shape(false, DesktopHandPose.Cup) };
            Watch(); float start = Time.time;
            while (Time.time - start < 3) { rightSide.joints = shapes[random.Next(shapes.Length)]; if (Time.time - start > 1.5f && Time.time - start < 1.6f) Shot("3-crazy-fingers"); yield return null; }
            Judge("Crazy fingers (new shape every frame)");
            start = Time.time; var soup = new Vector3[20]; var realFlat = Shape(false, DesktopHandPose.Flat);
            while (Time.time - start < 3)
            {
                for (int f = 0; f < 5; f++)
                {
                    Vector3 at = realFlat.Joint(f * 4) + new Vector3(R() - .5f, R() - .5f, R() - .5f) * .02f; soup[f * 4] = at;
                    for (int j = 0; j < 3; j++) { at += UnityEngine.Random.onUnitSphere * (.02f + R() * .03f); soup[f * 4 + j + 1] = at; }
                }
                rightSide.rawJoints = soup; yield return null;
            }
            Judge("Joint soup (every finger bone pointing somewhere random)");
            start = Time.time; int kind = 0;
            while (Time.time - start < 2)
            {
                for (int i = 0; i < 20; i++) soup[i] = kind % 4 == 0 ? new Vector3(float.NaN, 0, 0) : kind % 4 == 1 ? Vector3.positiveInfinity : kind % 4 == 2 ? Vector3.zero : new Vector3(1, 1, 1) * (1 + i);
                kind++; yield return null;
            }
            rightSide.rawJoints = null; rightSide.joints = Shape(false, DesktopHandPose.Fist);
            Judge("Poisoned joints (NaN, infinity, zero, far away)");
            yield return Wait(.6f);
            start = Time.time; float flickerUntil = 0; bool seen = true;
            while (Time.time - start < 3) { if (Time.time >= flickerUntil) { seen = !seen; flickerUntil = Time.time + .02f + R() * .04f; } rightSide.handTracked = seen; yield return null; }
            rightSide.handTracked = true;
            Check(HybridXRInput.SingleController, "Flickering hand tracking: still joystick plus hand");
            Judge("Flickering hand tracking (20 to 60 ms losses)");

            // 4. Look up: the headset loses the bare hand, and the controller lying on the desk shows up as tracked again.
            yield return Wait(.6f); Watch();
            rightSide.handTracked = false; rightSide.controllerTracked = true;   // controller still at 'desk', not moving, nothing pressed
            float lost = Time.time; bool flipped = false; float farthest = 0; Vector3 before = view.transform.InverseTransformPoint(bones[7].position);
            while (Time.time - lost < 2.5f)
            {
                yield return null;
                flipped |= Both();
                farthest = Mathf.Max(farthest, Vector3.Distance(view.transform.InverseTransformPoint(bones[7].position), view.transform.InverseTransformPoint(view.transform.parent ? view.transform.parent.TransformPoint(desk) : desk)) < .12f ? 1 : 0);
            }
            Line($"LOOK UP hand lost for 2.5 s with the controller resting in view: switched to two controllers={flipped}; drawn hand went to the resting controller={(farthest > 0)}");
            Check(!flipped, "Hand lost while the controller rests on a desk: the game does not flip to two-controller mode");
            Check(farthest == 0, "Hand lost while the controller rests on a desk: the drawn hand does not jump to the controller");
            Shot("4-hand-lost");
            Judge("Looking up and losing the bare hand");
            rightSide.controllerTracked = false; rightSide.handTracked = true; rightSide.joints = Shape(false, DesktopHandPose.Peace);
            yield return Wait(1);
            Check(tracking.WorldHand(false).skeletonTracked && HybridXRInput.SingleController, "Hand found again: back to the bare hand, still joystick plus hand");
            Judge("Bare hand found again");

            // 5. Really pick the controller back up: it moves in the hand.
            rightSide.handTracked = false; rightSide.controllerTracked = true; yield return Wait(.2f);
            start = Time.time;
            while (Time.time - start < .6f) { rightSide.controller = new Pose(Vector3.Lerp(desk, RightHome, (Time.time - start) / .6f), Quaternion.Slerp(Quaternion.Euler(0, 40, 90), Quaternion.identity, (Time.time - start) / .6f)); yield return null; }
            rightSide.controller = new Pose(RightHome, Quaternion.identity);
            yield return Wait(1.2f);
            Devices("after picking the controller back up");
            Check(Both(), "Controller picked back up (moved): two-controller mode returns");
            Judge("Picking the controller back up", .25f);

            // 6. Ping-pong: controller in hand and bare hand swap ten times a second, in different places.
            var wristAway = new Pose(RightHome + new Vector3(-.1f, .15f, .05f), Quaternion.Euler(30, -40, 60));
            start = Time.time;
            while (Time.time - start < 3)
            {
                bool useController = Mathf.FloorToInt((Time.time - start) * 10) % 2 == 0;
                rightSide.controllerTracked = useController; rightSide.handTracked = !useController; rightSide.wrist = wristAway; rightSide.joints = Shape(false, DesktopHandPose.Peace);
                // A controller in the hand is never perfectly still.
                rightSide.controller = new Pose(RightHome + UnityEngine.Random.insideUnitSphere * .08f, Quaternion.identity); rightSide.grip = 1;
                if (Time.time - start > 1.5f && Time.time - start < 1.6f) Shot("6-ping-pong");
                yield return null;
            }
            Judge("Swapping controller and bare hand ten times a second", .2f);

            // 7. Still quick afterwards? Flat to fist on the bare hand, timed by the drawn index finger.
            rightSide.controllerTracked = false; rightSide.handTracked = true; rightSide.wrist = new Pose(RightHome, Quaternion.identity); rightSide.joints = Shape(false, DesktopHandPose.Flat); rightSide.grip = 0;
            yield return Wait(1.5f);
            rightSide.joints = Shape(false, DesktopHandPose.Fist); start = Time.time; float took = -1, most = 0;
            while (Time.time - start < 1)
            {
                yield return null;
                float curl = Vector3.Angle(bones[7].rotation * Vector3.forward, bones[14 + 15 + 3 + 2].up);
                most = Mathf.Max(most, curl);
                if (curl > 150) { took = Time.time - start; break; }
            }
            Line("QUICK flat to fist drawn in " + (took < 0 ? "more than 1.00" : took.ToString("F3")) + " s; index fingertip reached " + most.ToString("F0") + " deg from the palm's forward");
            Check(took >= 0 && took < .25f, "A real quick fist still arrives within a quarter of a second (" + took.ToString("F3") + " s)");
            Shot("7-fist");
            watching = false;
            if (PcSimulatedVR.Instance) Object.Destroy(PcSimulatedVR.Instance.gameObject);
            if (SimulatedTrackedHands.Instance) Object.Destroy(SimulatedTrackedHands.Instance.gameObject);
        }
    }
}
