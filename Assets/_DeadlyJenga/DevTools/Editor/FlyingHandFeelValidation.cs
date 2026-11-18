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
using FlyingHand;
using DeadlyJenga.Core;
using DeadlyJenga.DevTools;
using DeadlyJenga.Presentation;
using Object = UnityEngine.Object;

namespace DeadlyJenga.HandValidation
{
    // How the flying hand looks and feels: whole fingers in every input mode, firm contact, real grip.
    // Runs only when asked (menu or -executeMethod). Uses the real prefab in an empty scene; saves nothing.
    // It runs twice. First with a fixture that hands poses straight to the hand (ICandyHandPoseSource), which allows
    // exact numbers for contact and grip. Then through emulated XR: PcSimulatedVR's controllers and
    // SimulatedTrackedHands' XR Hands joints, read by the game exactly as it reads a Quest. Still not a headset.
    [InitializeOnLoad]
    public static class FlyingHandFeelValidation
    {
        const string Key = "FlyingHandFeelValidation";
        static readonly Stack<IEnumerator> work = new Stack<IEnumerator>();
        static string Report => ProjectPath.Report("FlyingHandFeel.txt");
        static FlyingHandFeelValidation() { EditorApplication.playModeStateChanged += Changed; }

        [MenuItem("Tools/Deadly Jenga/Validate Flying Hand Feel")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start in Edit Mode.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            File.WriteAllText(Report, "Flying hand look and feel checks\n");
            SessionState.SetInt(Key + "Failures", 0); SessionState.SetInt(Key + "Checks", 0);
            SessionState.SetInt(Key, 1);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
        static void Changed(PlayModeStateChange state)
        {
            if (SessionState.GetInt(Key, 0) == 0) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                Application.logMessageReceived += Log;
                work.Push(All());
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
        static void Log(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            Fail(); Line("FAIL Unity: " + message + "\n" + stack);
        }
        static void Tick()
        {
            try
            {
                if (work.Count == 0) { EditorApplication.update -= Tick; EditorApplication.ExitPlaymode(); return; }
                if (xr) Pump();
                if (watching) Sample();
                var next = work.Peek();
                if (!next.MoveNext()) { work.Pop(); return; }
                if (next.Current is IEnumerator nested) work.Push(nested);
            }
            catch (Exception e) { Fail(); Line("FAIL " + e); work.Clear(); }
        }
        // A failed check is recorded and the run continues, so one report shows everything.
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

        sealed class Poses : ICandyHandPoseSource
        {
            public CandyHandPose pose = new CandyHandPose
            {
                tracked = true, skeletonTracked = true, position = new Vector3(.2f, 1.2f, .4f),
                rotation = Quaternion.identity, skeleton = CandyHandSkeleton.Rest(L)
            };
            public bool TryGetPose(out CandyHandPose value) { value = pose; return pose.tracked; }
            // Controller squeeze, for the emulated XR controller (the fixture carries it in the skeleton).
            public float grip, trigger;
            // XR only: joints to report instead of the skeleton, however broken.
            public Vector3[] rawJoints;
        }
        // Which hand: the right one, unless the player's saved preference makes the game use the left in XR.
        static bool L, xr;
        static float Tolerance => xr ? 12 : 6;
        // A batch editor never has focus, and the Input System switches devices off without it. Emulated controllers
        // are devices, so let them work unfocused for this run only. Nothing is saved; the old values are put back.
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
        // Emulated XR: every frame, report the commanded hand through the simulated devices.
        static void Pump()
        {
            var vr = PcSimulatedVR.Instance; var hands = SimulatedTrackedHands.Instance;
            if (!vr || !hands || input == null) return;
            var p = input.pose; bool hand = p.tracked && p.skeletonTracked, controller = p.tracked && !p.skeletonTracked;
            var wrist = new Pose(p.position, p.rotation);
            if (L) { vr.TestLeftUntracked = !controller; vr.TestLeftPose = wrist; vr.TestLeftGrip = input.grip; vr.TestLeftTrigger = input.trigger; vr.TestRightUntracked = true; }
            else { vr.TestRightUntracked = !controller; vr.TestRightPose = wrist; vr.TestRightGrip = input.grip; vr.TestRightTrigger = input.trigger; vr.TestLeftUntracked = true; }
            if (input.rawJoints != null) hands.Set(L, hand, wrist, input.rawJoints); else hands.Set(L, hand, wrist, p.skeleton);
            hands.Set(!L, false, Pose.identity, CandyHandSkeleton.Rest(!L));
        }

        // ---------- emulated tracked hand ----------
        // curls: degrees each finger joint bends toward the palm (finger 0 = thumb). splay: degrees fingers fan out.
        // Proportions copy a real hand (longer than the glove, knuckles further from the wrist) on purpose.
        static CandyHandSkeleton Tracked(bool left, float[][] curls, float splay = 0, float lean = 0)
        {
            var rest = CandyHandSkeleton.Rest(left); var result = rest; float sign = left ? 1 : -1;
            for (int f = 0; f < 5; f++)
            {
                Vector3 at = rest.Joint(f * 4) * 1.15f + (f == 0 ? Vector3.zero : Vector3.forward * .04f);
                result.SetJoint(f * 4, at);
                float total = 0;
                for (int j = 0; j < 3; j++)
                {
                    Vector3 bone = rest.Joint(f * 4 + j + 1) - rest.Joint(f * 4 + j);
                    total += curls[f][j];
                    Quaternion bend = f == 0
                        ? Quaternion.AngleAxis(sign * total * .6f, Vector3.forward) * Quaternion.AngleAxis(total * .5f, Vector3.right)
                        : Quaternion.AngleAxis(sign * (f - 2.5f) * splay, Vector3.up) * Quaternion.AngleAxis(total, Vector3.right) * Quaternion.AngleAxis(lean, Vector3.right);
                    at += bend * bone * 1.3f;
                    result.SetJoint(f * 4 + j + 1, at);
                }
            }
            return result;
        }
        static float[][] Curls(Func<int, float[]> perFinger)
        { var c = new float[5][]; for (int f = 0; f < 5; f++) c[f] = perFinger(f); return c; }
        static readonly float[] Straight = { 0, 0, 0 }, Closed = { 80, 100, 60 }, Half = { 35, 45, 30 }, ThumbIn = { 30, 30, 30 };
        static CandyHandSkeleton Open(bool left) => Tracked(left, Curls(f => Straight));
        static CandyHandSkeleton Fist(bool left) => Tracked(left, Curls(f => f == 0 ? ThumbIn : Closed));
        static CandyHandSkeleton Point(bool left) => Tracked(left, Curls(f => f == 0 ? ThumbIn : f == 1 ? Straight : Closed));
        static CandyHandSkeleton Peace(bool left) => Tracked(left, Curls(f => f == 0 ? ThumbIn : f <= 2 ? Straight : Closed), 6);
        static CandyHandSkeleton Spread(bool left) => Tracked(left, Curls(f => Straight), 14);
        static CandyHandSkeleton Claw(bool left) => Tracked(left, Curls(f => f == 0 ? new float[] { 10, 20, 20 } : Half), 6);
        static CandyHandSkeleton Lerp(CandyHandSkeleton a, CandyHandSkeleton b, float t)
        { var r = a; for (int i = 0; i < 20; i++) r.SetJoint(i, Vector3.Lerp(a.Joint(i), b.Joint(i), t)); return r; }

        // Wraps a block lying under the palm: fingers down its far side, thumb down its near side.
        static CandyHandSkeleton Grasp(bool left, bool pinchOnly)
        {
            var rest = CandyHandSkeleton.Rest(left); var result = rest;
            for (int f = 0; f < 5; f++)
            {
                Vector3 at = rest.Joint(f * 4);
                for (int j = 0; j < 3; j++)
                {
                    float length = (rest.Joint(f * 4 + j + 1) - rest.Joint(f * 4 + j)).magnitude;
                    Vector3 direction = f == 0 ? new Vector3(0, -1, .45f + j * .15f)
                        : pinchOnly && f > 1 ? Vector3.forward
                        : new Vector3(0, -1, -.3f - j * .25f);
                    at += direction.normalized * length;
                    result.SetJoint(f * 4 + j + 1, at);
                }
            }
            return result;
        }

        // ---------- what the drawn hand actually looks like ----------
        static CandyPhysicalHand watched; static bool watching;
        static Mesh watchedMesh; static Vector3[] vertices; static BoneWeight[] weights; static Matrix4x4[] binds;
        static int[][] middle; static float[][] restRadius;
        static int thinnestBone;
        static float worstGirth, worstRoll, worstGap, worstBackBend, worstStep; static bool everHidden; static Vector3 lastPalm; static bool hasLastPalm;
        // Is the drawn hand a shape a hand can make? Sideways lean out of the finger's plane, joints bent backwards,
        // how fast a joint moved, and whether anything became not-a-number.
        static float worstSide, worstJointBack, worstJointRate, lowestStrength; static bool sawNaN; static readonly float[] lastBend = new float[16]; static bool hasLastBend;
        static void Watch(CandyPhysicalHand hand)
        {
            watched = hand; watching = hand; worstGirth = 1; worstRoll = worstGap = worstBackBend = worstStep = 0; everHidden = false; hasLastPalm = false;
            worstSide = worstJointBack = worstJointRate = 0; lowestStrength = 1; sawNaN = false; hasLastBend = false;
            if (!hand) return;
            var mesh = hand.skin.sharedMesh;
            if (mesh == watchedMesh) return;
            watchedMesh = mesh; vertices = mesh.vertices; weights = mesh.boneWeights; binds = mesh.bindposes;
            var bones = hand.skin.bones; middle = new int[16][]; restRadius = new float[16][];
            for (int i = 1; i < 16; i++)
            {
                int bone = Array.IndexOf(bones, hand.visualBones[i]);
                int f = (i - 1) / 3, j = (i - 1) % 3; var rest = CandyHandSkeleton.Rest(hand.left);
                float length = hand.visualReferenceLengths != null && hand.visualReferenceLengths.Length == 15 ? hand.visualReferenceLengths[i - 1] : (rest.Joint(f * 4 + j + 1) - rest.Joint(f * 4 + j)).magnitude;
                var ids = new List<int>(); var radii = new List<float>();
                for (int v = 0; v < vertices.Length; v++)
                {
                    var w = weights[v];
                    if (w.boneIndex0 != bone || w.weight0 < .5f) continue;
                    Vector3 local = binds[bone].MultiplyPoint3x4(vertices[v]);
                    float radius = new Vector2(local.x, local.z).magnitude;
                    // The middle of each finger bone. Joints are allowed to crease; the middle must stay full.
                    if (local.y < length * .3f || local.y > length * .7f || radius < .003f) continue;
                    // The thumb's first bone is the ball of the thumb. It is meant to merge into the palm, so it has no
                    // free-standing middle to measure; its skin is covered by the roll and gap checks instead.
                    if (i == 1) continue;
                    ids.Add(v); radii.Add(radius);
                }
                middle[i] = ids.ToArray(); restRadius[i] = radii.ToArray();
            }
        }
        static readonly Matrix4x4[] skinning = new Matrix4x4[64];
        static void Sample()
        {
            var hand = watched; if (!hand || !hand.gameObject.activeInHierarchy) { if (hand) everHidden = true; return; }
            if (!hand.skin.enabled) everHidden = true;
            var bones = hand.skin.bones;
            for (int b = 0; b < bones.Length && b < skinning.Length; b++) skinning[b] = bones[b] ? bones[b].localToWorldMatrix * binds[b] : Matrix4x4.identity;
            var radii = new List<float>();
            for (int i = 1; i < 16; i++)
            {
                var toBone = hand.visualBones[i].worldToLocalMatrix; radii.Clear();
                for (int n = 0; n < middle[i].Length; n++)
                {
                    int v = middle[i][n]; var w = weights[v]; Vector3 p = Vector3.zero;
                    p += skinning[w.boneIndex0].MultiplyPoint3x4(vertices[v]) * w.weight0;
                    if (w.weight1 > 0) p += skinning[w.boneIndex1].MultiplyPoint3x4(vertices[v]) * w.weight1;
                    if (w.weight2 > 0) p += skinning[w.boneIndex2].MultiplyPoint3x4(vertices[v]) * w.weight2;
                    if (w.weight3 > 0) p += skinning[w.boneIndex3].MultiplyPoint3x4(vertices[v]) * w.weight3;
                    Vector3 local = toBone.MultiplyPoint3x4(p);
                    radii.Add(new Vector2(local.x, local.z).magnitude / restRadius[i][n]);
                }
                if (radii.Count > 8) { radii.Sort(); if (radii[radii.Count / 20] < worstGirth) { worstGirth = radii[radii.Count / 20]; thinnestBone = i; } }
                // Roll between neighbouring bones pinches skin; a gap between them stretches it.
                int j = (i - 1) % 3; var parent = hand.visualBones[j == 0 ? 0 : i - 1]; var bone = hand.visualBones[i];
                if (j > 0)
                {
                    Quaternion relative = Quaternion.Inverse(parent.rotation) * bone.rotation;
                    Vector3 twistAxis = new Vector3(relative.x, relative.y, relative.z); float along = twistAxis.y;
                    float roll = Mathf.Abs(Mathf.DeltaAngle(0, 2 * Mathf.Atan2(along, relative.w) * Mathf.Rad2Deg));
                    worstRoll = Mathf.Max(worstRoll, roll);
                    float length = (parent.localScale.y / hand.GeometrySize) * ReferenceLength(hand, i - 1) * hand.GeometrySize;
                    worstGap = Mathf.Max(worstGap, Vector3.Distance(parent.position + parent.up * length, bone.position) / hand.GeometrySize);
                }
                if (i > 3)
                {
                    Vector3 direction = Quaternion.Inverse(hand.visualBones[0].rotation) * bone.up;
                    if (j == 0) worstBackBend = Mathf.Max(worstBackBend, Mathf.Atan2(direction.y, Mathf.Max(.0001f, direction.z)) * Mathf.Rad2Deg);
                }
            }
            lowestStrength = Mathf.Min(lowestStrength, hand.Strength);
            Quaternion fromPalm = Quaternion.Inverse(hand.visualBones[0].rotation);
            for (int f = 1; f < 5; f++)
            {
                Vector3 knuckle = fromPalm * hand.visualBones[1 + f * 3].up;
                float flat = new Vector2(knuckle.x, knuckle.z).magnitude;
                Vector3 along = flat > .3f ? new Vector3(knuckle.x, 0, knuckle.z) / flat : Vector3.forward;
                Vector3 across = Vector3.Cross(Vector3.up, along);
                Vector3 parent = Vector3.zero;
                for (int j = 0; j < 3; j++)
                {
                    int i = 1 + f * 3 + j; Vector3 d = fromPalm * hand.visualBones[i].up;
                    if (float.IsNaN(d.x) || float.IsNaN(hand.visualBones[i].position.x)) sawNaN = true;
                    if (flat > .3f && j > 0) worstSide = Mathf.Max(worstSide, Mathf.Asin(Mathf.Clamp(Mathf.Abs(Vector3.Dot(d, across)), 0, 1)) * Mathf.Rad2Deg);
                    float bend = j == 0 ? Mathf.Atan2(-d.y, Vector3.Dot(d, along)) * Mathf.Rad2Deg : Vector3.SignedAngle(parent, d, across);
                    if (j > 0) worstJointBack = Mathf.Max(worstJointBack, -bend);
                    if (hasLastBend && Time.deltaTime > 0) worstJointRate = Mathf.Max(worstJointRate, Mathf.Abs(Mathf.DeltaAngle(lastBend[i], bend)) / Time.deltaTime);
                    lastBend[i] = bend; parent = d;
                }
            }
            hasLastBend = true;
            Vector3 palm = hand.visualBones[0].position;
            if (hasLastPalm) worstStep = Mathf.Max(worstStep, Vector3.Distance(palm, lastPalm));
            lastPalm = palm; hasLastPalm = true;
        }
        static float ReferenceLength(CandyPhysicalHand hand, int i)
        {
            var rest = CandyHandSkeleton.Rest(hand.left); int f = (i - 1) / 3, j = (i - 1) % 3;
            return hand.visualReferenceLengths != null && hand.visualReferenceLengths.Length == 15 ? hand.visualReferenceLengths[i - 1] : (rest.Joint(f * 4 + j + 1) - rest.Joint(f * 4 + j)).magnitude;
        }
        static void Judge(string what, bool expectVisible = true)
        {
            Line($"LOOK {what}: thinnest finger middle={worstGirth:P0} of rest (finger {(thinnestBone - 1) / 3} bone {(thinnestBone - 1) % 3}), roll between bones={worstRoll:F1} deg, gap between bones={worstGap * 1000:F2} mm (hand scale 1), largest palm step={worstStep:F2} m, hidden={everHidden}");
            Check(worstGirth > .8f, what + ": finger middles keep their thickness (" + worstGirth.ToString("P0") + ")");
            Check(worstRoll < 3, what + ": no roll between neighbouring finger bones (" + worstRoll.ToString("F1") + " deg)");
            Check(worstGap < .0005f, what + ": finger bones stay joined end to end");
            if (expectVisible) Check(!everHidden, what + ": the hand never blinks out");
            Watch(watched);
        }
        static float FingerError(CandyPhysicalHand hand, CandyHandSkeleton wanted)
        {
            float worst = 0; var fitted = CandyHandPoseLimits.Fit(wanted, hand.left);
            for (int f = 0; f < 5; f++) for (int j = 0; j < 3; j++)
            {
                Vector3 want = hand.visualBones[0].rotation * (fitted.Joint(f * 4 + j + 1) - fitted.Joint(f * 4 + j)).normalized;
                worst = Mathf.Max(worst, Vector3.Angle(want, hand.visualBones[1 + f * 3 + j].up));
            }
            return worst;
        }
        static float PhysicalFingerError(CandyPhysicalHand hand, CandyHandSkeleton wanted)
        {
            float worst = 0; var fitted = CandyHandPoseLimits.Fit(wanted, hand.left);
            for (int f = 1; f < 5; f++) for (int j = 0; j < 3; j++)
            {
                Vector3 want = hand.bodies[0].rotation * (fitted.Joint(f * 4 + j + 1) - fitted.Joint(f * 4 + j)).normalized;
                worst = Mathf.Max(worst, Vector3.Angle(want, hand.bodies[1 + f * 3 + j].transform.up));
            }
            return worst;
        }

        static void Shot(CandyPhysicalHand hand, string name, Vector3 from)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) { Line("SKIP screenshot (no graphics): " + name); return; }
            float size = hand.GeometrySize; var palm = hand.visualBones[0];
            var cameraObject = new GameObject("Hand evidence camera"); var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
            Vector3 focus = palm.position + palm.rotation * new Vector3(0, 0, .05f) * size;
            camera.transform.position = focus + from * size; camera.transform.LookAt(focus);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.14f, .18f, .23f); camera.fieldOfView = 40; camera.farClipPlane = 500;
            var lightObject = new GameObject("Hand evidence light"); var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.1f; light.transform.rotation = Quaternion.LookRotation(focus - camera.transform.position + Vector3.down * size * .1f);
            var rt = new RenderTexture(900, 700, 24); var old = RenderTexture.active; Texture2D pixels = null;
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt; pixels = new Texture2D(900, 700, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 900, 700), 0, 0); pixels.Apply(); File.WriteAllBytes(ProjectPath.Report("HandFeel-" + (xr ? "xr-" : "") + name + ".png"), pixels.EncodeToPNG());
            }
            finally { RenderTexture.active = old; camera.targetTexture = null; Object.Destroy(rt); if (pixels) Object.Destroy(pixels); Object.Destroy(cameraObject); Object.Destroy(lightObject); }
        }

        // ---------- the run ----------
        static CastleHandActor actor; static FlyingHandController motor; static CandyFlyingHandBridge bridge; static Poses input;
        static IEnumerator Spawn(Vector3 at, bool throughXR = false)
        {
            if (actor) { Object.Destroy(actor.gameObject); yield return Wait(.2f); }
            var prefab = AssetDatabase.LoadAssetAtPath<CastleHandActor>(ProjectPath.Find("CastleFlyingHand.prefab"));
            actor = Object.Instantiate(prefab); motor = actor.motor;
            motor.captureDesktopCursor = false; motor.ControlModeOverride = ControlMode.VR; motor.RefreshControlMode();
            actor.Place(at, Quaternion.identity);
            bridge = actor.GetComponent<CandyFlyingHandBridge>();
            // The same default rules a solo session confirms (1200 N, friction 2).
            var profile = HandDriveProfile.For(actor.gameObject); profile.Apply(null); bridge.PhysicsProfile = profile;
            xr = throughXR; L = xr && PlayerPrefs.GetInt("CandyBlaster.LeftHanded", 0) != 0;
            input = new Poses(); input.pose.skeleton = Open(L);
            if (xr) { AllowUnfocusedInput(); PcSimulatedVR.Create(); SimulatedTrackedHands.Create(); Pump(); }
            else bridge.PoseSource = input;
            yield return Wait(1);
            Watch(bridge.PhysicalHand);
        }
        static IEnumerator All()
        {
            yield return Spawn(new Vector3(0, 20, 0)); var hand = bridge.PhysicalHand;
            input.pose.skeleton = Fist(L); yield return Wait(1.5f);
            float fist = PhysicalFingerError(hand, Fist(L));
            Check(hand && hand.IsInteractive && bridge.Mode == CandyHandMode.HandTracking, "Emulated hand tracking drives the real flying-hand prefab");
            Check(hand.skin.sharedMesh.isReadable, "Skin mesh can be measured");
            Check(fist < 25, "Physical fingers close into a full fist inside their joint limits (" + fist.ToString("F1") + " deg off)");
            input.pose.skeleton = Open(L);
            // Watch the physical index finger reopen, joint by joint.
            for (int n = 0; n < 8; n++)
            {
                yield return Wait(.1f); string bends = "";
                for (int j = 0; j < 3; j++) bends += Vector3.Angle(hand.bodies[j == 0 ? 0 : 4 + j - 1].transform.rotation * (j == 0 ? Vector3.forward : Vector3.up), hand.bodies[4 + j].transform.up).ToString("F0") + " ";
                Line($"REOPEN t={n * .1f + .1f:F1}s index joint bends {bends}(want 0 0 0), finger torque in use {hand.AppliedTorqueNewtonMetres:F0} N m, palm off target {Quaternion.Angle(hand.bodies[0].rotation, hand.Target.rotation):F1} deg");
            }
            float reopened = PhysicalFingerError(hand, Open(L));
            Check(reopened < 25, "Physical fingers open flat again after the fist (" + reopened.ToString("F1") + " deg off)");
            Watch(hand);

            yield return TrackedPoses(hand);
            yield return FastMotion(hand);
            yield return UpsideDown(hand);
            yield return ControllerMode(hand);
            yield return Switching(hand);
            yield return Chaos(hand);
            yield return WallPress(hand);
            yield return Grip(new Vector3(80, 20, 0), false);
            float fullHold = lastHold; bool fullLift = lastLift;
            yield return Grip(new Vector3(160, 20, 0), true);
            Check(fullLift, "A full-hand grasp lifts a 40 kg castle-sized block straight up by friction alone");
            Line($"GRIP hold strength: full hand={fullHold:F0} N extra pull, thumb+index pinch={lastHold:F0} N (pinch lifted={lastLift})");
            Check(fullHold > lastHold * 1.3f, "A full-hand grasp holds clearly more than a thumb-and-index pinch");
            yield return DesktopPoses();

            Line("---- Emulated XR: simulated controllers and XR Hands joints, read by the game's own XR input code ----");
            yield return Spawn(new Vector3(320, 20, 0), true); hand = bridge.PhysicalHand;
            Check(hand && hand.IsInteractive && bridge.Mode == CandyHandMode.HandTracking && bridge.PoseSource == null, "XR: an emulated tracked hand reaches the flying hand through XR Hands");
            if (hand && hand.IsInteractive)
            {
                yield return TrackedPoses(hand);
                yield return FastMotion(hand);
                yield return UpsideDown(hand);
                yield return ControllerMode(hand);
                yield return Switching(hand);
                yield return Chaos(hand);
            }
            xr = false; RestoreInput();
            if (PcSimulatedVR.Instance) Object.Destroy(PcSimulatedVR.Instance.gameObject);
            if (SimulatedTrackedHands.Instance) Object.Destroy(SimulatedTrackedHands.Instance.gameObject);
            if (actor) Object.Destroy(actor.gameObject);
            yield return Wait(.2f);
        }
        static IEnumerator TrackedPoses(CandyPhysicalHand hand)
        {
            var poses = new (string name, CandyHandSkeleton pose)[] { ("open", Open(L)), ("fist", Fist(L)), ("point", Point(L)), ("peace", Peace(L)), ("spread", Spread(L)), ("claw", Claw(L)) };
            foreach (var p in poses)
            {
                input.pose.skeleton = p.pose; yield return Wait(.9f);
                float error = FingerError(hand, p.pose);
                Check(error < Tolerance, "Tracked " + p.name + ": drawn fingers match the tracked finger directions (" + error.ToString("F1") + " deg off)");
                Shot(hand, "tracked-" + p.name, new Vector3(.16f, .2f, -.12f));
                if (p.name == "fist") Shot(hand, "tracked-fist-under", new Vector3(-.1f, -.22f, .16f));
            }
            Judge("Tracked poses and the blends between them");
            input.pose.skeleton = Open(L); yield return Wait(.5f);
        }
        static IEnumerator FastMotion(CandyPhysicalHand hand)
        {
            Vector3 home = input.pose.position; float start = Time.time; bool shot = false;
            while (Time.time - start < 3)
            {
                float t = Time.time - start;
                // A fast wave: about 1.5 m/s of real wrist speed, 30 m/s for the giant hand, while opening and closing.
                input.pose.position = home + new Vector3(Mathf.Sin(t * 9) * .16f, Mathf.Sin(t * 7) * .08f, Mathf.Cos(t * 5) * .08f);
                input.pose.rotation = Quaternion.Euler(Mathf.Sin(t * 6) * 50, Mathf.Sin(t * 4) * 40, Mathf.Sin(t * 8) * 70);
                input.pose.skeleton = Lerp(Open(L), Fist(L), .5f + .5f * Mathf.Sin(t * 11));
                if (!shot && t > 1.3f) { shot = true; Shot(hand, "fast-motion", new Vector3(.18f, .2f, -.16f)); }
                yield return null;
            }
            input.pose.position = home; input.pose.rotation = Quaternion.identity; input.pose.skeleton = Open(L);
            Judge("Fast waving while opening and closing");
            yield return Wait(.8f);
        }
        static IEnumerator UpsideDown(CandyPhysicalHand hand)
        {
            var turns = new (string name, Quaternion turn)[] { ("rolled palm-up", Quaternion.Euler(0, 0, 180)), ("pitched over backwards", Quaternion.Euler(-170, 0, 0)), ("pointing straight down", Quaternion.Euler(90, 0, 0)), ("rolled and yawed", Quaternion.Euler(20, 160, 150)) };
            input.pose.skeleton = Claw(L);
            foreach (var t in turns)
            {
                // Turn there the way a wrist does, not in one frame.
                Quaternion from = input.pose.rotation; float start = Time.time;
                while (Time.time - start < .6f) { input.pose.rotation = Quaternion.Slerp(from, t.turn, (Time.time - start) / .6f); yield return null; }
                input.pose.rotation = t.turn; yield return Wait(.9f);
                float off = Quaternion.Angle(hand.bodies[0].rotation, hand.Target.rotation);
                float asked = Quaternion.Angle(hand.Target.rotation, motor.transform.rotation * t.turn);
                Check(off < 8, "Hand turns fully to " + t.name + " (" + off.ToString("F1") + " deg off)");
                Check(asked < (xr ? 20 : 2), "Tracking asks for " + t.name + " as commanded (" + asked.ToString("F1") + " deg off)");
                Check(FingerError(hand, Claw(L)) < Tolerance, "Fingers keep the tracked pose while " + t.name);
                Shot(hand, "turned-" + t.name.Replace(' ', '-'), new Vector3(.16f, .2f, -.12f));
            }
            Judge("Turning upside down and back");
            input.pose.rotation = Quaternion.identity; input.pose.skeleton = Open(L); yield return Wait(1);
        }
        static IEnumerator ControllerMode(CandyPhysicalHand hand)
        {
            input.pose.skeletonTracked = false;
            foreach (var (grip, trigger) in new[] { (0f, 0f), (1f, 0f), (0f, 1f), (1f, 1f) })
            {
                input.pose.skeleton = CandyHandSkeleton.ControllerGrip(L, grip, trigger); input.grip = grip; input.trigger = trigger; yield return Wait(.7f);
                Shot(hand, $"controller-grip{grip:F0}-trigger{trigger:F0}", new Vector3(.16f, .2f, -.12f));
            }
            Check(bridge.Mode == CandyHandMode.Controller && hand.IsInteractive, "Controller input drives the same physical hand");
            Judge("Controller grip and trigger poses");
            input.pose.skeletonTracked = true; input.pose.skeleton = Open(L); yield return Wait(.8f);
            Watch(hand);
        }
        static IEnumerator Switching(CandyPhysicalHand hand)
        {
            var trackedPose = input.pose;
            var controller = trackedPose; controller.skeletonTracked = false; controller.skeleton = CandyHandSkeleton.ControllerGrip(L, 1, .2f); input.grip = 1; input.trigger = .2f;
            // A controller held somewhere completely different, pointing another way.
            controller.position += new Vector3(.3f, .12f, -.25f); controller.rotation = Quaternion.Euler(50, 100, -70);
            Watch(hand); Vector3 before = hand.Target.position;
            input.pose = controller; yield return null; yield return null;
            float jump = Vector3.Distance(before, hand.Target.position);
            yield return Wait(1.2f);
            Check(bridge.Mode == CandyHandMode.Controller, "Hand tracking to controller: the controller takes over");
            Check(jump < 1, "Hand tracking to controller: the hand does not leap to the controller's place (" + jump.ToString("F2") + " m)");
            Check(Quaternion.Angle(hand.bodies[0].rotation, hand.Target.rotation) < 10, "Hand tracking to controller: the hand turns to the controller's angle");
            Check(worstStep < 1.2f, "Hand tracking to controller: palm moves smoothly (largest step " + worstStep.ToString("F2") + " m)");
            Check(hand.IsInteractive, "Hand tracking to controller: contacts stay on (a held block is not dropped)");
            Judge("Switch from hand tracking to a controller held elsewhere");
            before = hand.Target.position; input.pose = trackedPose; input.pose.skeleton = Fist(L); yield return null; yield return null;
            jump = Vector3.Distance(before, hand.Target.position);
            yield return Wait(1.2f);
            Check(bridge.Mode == CandyHandMode.HandTracking && jump < 1, "Controller to hand tracking: takes over without a leap (" + jump.ToString("F2") + " m)");
            Check(FingerError(hand, Fist(L)) < Tolerance, "Controller to hand tracking: fingers arrive at the tracked pose");
            Check(worstStep < 1.2f, "Controller to hand tracking: palm moves smoothly (largest step " + worstStep.ToString("F2") + " m)");
            Judge("Switch from a controller back to hand tracking");
            // Put the controller down, pause, raise a hand: a short gap with nothing tracked.
            input.pose = controller; yield return Wait(1);
            Watch(hand);
            input.pose.tracked = false; yield return Wait(.2f);
            input.pose = trackedPose; input.pose.skeleton = Peace(L); yield return Wait(1.2f);
            Check(bridge.Mode == CandyHandMode.HandTracking && hand.IsInteractive, "Controller, short gap, hand tracking: recovers");
            Check(worstStep < 1.2f, "Controller, short gap, hand tracking: palm moves smoothly (largest step " + worstStep.ToString("F2") + " m)");
            Judge("Switch across a short tracking gap");
            input.pose = trackedPose; input.pose.skeleton = Open(L); yield return Wait(1);
        }
        // Someone trying to break the hand, and tracking that breaks by itself.
        static void JudgeShape(string what)
        {
            Line($"SHAPE {what}: sideways lean {worstSide:F1} deg, joint bent backwards {worstJointBack:F1} deg, fastest joint {worstJointRate:F0} deg/s, lowest strength {lowestStrength:F2}, NaN={sawNaN}");
            Check(!sawNaN, what + ": nothing in the drawn hand became not-a-number");
            Check(worstSide < 3, what + ": fingers only curl in their own plane (" + worstSide.ToString("F1") + " deg sideways)");
            Check(worstJointBack < 8, what + ": no finger joint bends backwards (" + worstJointBack.ToString("F1") + " deg)");
            Check(worstJointRate < 3200, what + ": no finger joint moves faster than a finger can (" + worstJointRate.ToString("F0") + " deg/s)");
        }
        static IEnumerator Chaos(CandyPhysicalHand hand)
        {
            var random = new System.Random(20261118); float R() => (float)random.NextDouble();
            var shapes = new[] { Open(L), Fist(L), Point(L), Peace(L), Spread(L), Claw(L) };
            var calm = input.pose; calm.skeleton = Open(L); calm.rotation = Quaternion.identity;

            // 1. Crazy fingers: an unrelated hand shape and a twitching wrist every single frame.
            input.pose = calm; yield return Wait(.5f); Watch(hand); float start = Time.time;
            while (Time.time - start < 3)
            {
                input.pose.skeleton = shapes[random.Next(shapes.Length)];
                input.pose.rotation = Quaternion.Euler(R() * 50 - 25, R() * 50 - 25, R() * 50 - 25);
                if (Time.time - start > 1.5f && Time.time - start < 1.6f) Shot(hand, "chaos-crazy-fingers", new Vector3(.16f, .2f, -.12f));
                yield return null;
            }
            JudgeShape("Crazy fingers (new shape every frame)"); Judge("Crazy fingers (new shape every frame)");

            // 2. Joint soup: every finger bone pointing somewhere random (backwards, sideways, through the palm),
            //    with believable bone lengths so the game cannot simply reject the frame.
            input.pose = calm; yield return Wait(.3f); Watch(hand); start = Time.time;
            while (Time.time - start < 3)
            {
                var soup = Open(L);
                for (int f = 0; f < 5; f++)
                {
                    Vector3 at = soup.Joint(f * 4) + new Vector3(R() - .5f, R() - .5f, R() - .5f) * .02f; soup.SetJoint(f * 4, at);
                    for (int j = 0; j < 3; j++) { at += UnityEngine.Random.onUnitSphere * (.02f + R() * .03f); soup.SetJoint(f * 4 + j + 1, at); }
                }
                input.pose.skeleton = soup;
                if (Time.time - start > 1.5f && Time.time - start < 1.6f) Shot(hand, "chaos-joint-soup", new Vector3(.16f, .2f, -.12f));
                yield return null;
            }
            JudgeShape("Joint soup (random joint positions every frame)"); Judge("Joint soup (random joint positions every frame)");

            // 2b. Rubbish the game can recognise (bones centimetres too long or all on one point) counts as lost tracking:
            //     the hand may fade while it lasts, and must come straight back.
            input.pose = calm; input.pose.skeleton = Fist(L); yield return Wait(.6f); Watch(hand); start = Time.time;
            while (Time.time - start < 1.5f)
            {
                var junk = Open(L);
                for (int i = 0; i < 20; i++) junk.SetJoint(i, new Vector3(R() - .5f, R() - .5f, R() - .5f) * .6f);
                input.pose.skeleton = junk; yield return null;
            }
            JudgeShape("Rejected rubbish frames"); Judge("Rejected rubbish frames", false);
            input.pose.skeleton = Fist(L); yield return Wait(xr ? 1.2f : .8f);
            Check(hand.IsInteractive && FingerError(hand, Fist(L)) < Tolerance, "After rubbish frames the hand comes back to the tracked fist");

            // 3. Poison: not-a-number, infinity, everything at the wrist, everything a metre away. XR only: a skeleton cannot hold these.
            if (xr)
            {
                input.pose = calm; input.pose.skeleton = Fist(L); yield return Wait(.6f); Watch(hand); start = Time.time; int kind = 0;
                var poison = new Vector3[20];
                while (Time.time - start < 2)
                {
                    for (int i = 0; i < 20; i++)
                        poison[i] = kind % 4 == 0 ? new Vector3(float.NaN, 0, 0) : kind % 4 == 1 ? Vector3.positiveInfinity : kind % 4 == 2 ? Vector3.zero : new Vector3(1, 1, 1) * (1 + i);
                    input.rawJoints = poison; kind++;
                    yield return null;
                }
                input.rawJoints = null;
                JudgeShape("Poisoned joints (NaN, infinity, zero, far away)"); Judge("Poisoned joints (NaN, infinity, zero, far away)", false);
                yield return Wait(.6f);
                Check(FingerError(hand, Fist(L)) < Tolerance, "After poisoned joints the hand returns to the tracked fist");
            }

            // 4. Flicker: tracking keeps cutting out for 20 to 70 ms at a time (about half the time) while holding a fist.
            input.pose = calm; input.pose.skeleton = Fist(L); yield return Wait(.8f); Watch(hand); start = Time.time;
            float until = 0; bool seen = true; float lostAt = -1, longestLoss = 0; int modeChanges = 0; var lastMode = bridge.Mode;
            var tracking = actor.GetComponent<CandyTrackingSource>();
            while (Time.time - start < 3)
            {
                if (Time.time >= until) { seen = !seen; until = Time.time + .02f + R() * .04f; }
                input.pose.tracked = seen; yield return null;
                // What the game itself saw: how long each loss really lasted once it had passed through the input path.
                bool gameSees = xr ? tracking && tracking.WorldHand(L).tracked : seen;
                if (!gameSees) { if (lostAt < 0) lostAt = Time.time; longestLoss = Mathf.Max(longestLoss, Time.time - lostAt); } else lostAt = -1;
                if (bridge.Mode != lastMode) { modeChanges++; lastMode = bridge.Mode; }
            }
            Line($"FLICKER longest loss the game saw {longestLoss * 1000:F0} ms, frame time about {Time.deltaTime * 1000:F0} ms, hand mode changed {modeChanges} times");
            input.pose.tracked = true;
            Check(lowestStrength > .9f, "Flickering tracking: the hand keeps its strength (lowest " + lowestStrength.ToString("F2") + ")");
            Check(hand.IsInteractive, "Flickering tracking: contacts stay on");
            Judge("Flickering tracking (half the frames lost)");

            // 5. Ping-pong: controller and tracked hand swap ten times a second, in different places.
            var controller = calm; controller.skeletonTracked = false; controller.skeleton = CandyHandSkeleton.ControllerGrip(L, 1, 0); input.grip = 1; input.trigger = 0;
            controller.position += new Vector3(.2f, .1f, -.15f); controller.rotation = Quaternion.Euler(30, 80, -50);
            var tracked = calm; tracked.skeleton = Peace(L);
            input.pose = tracked; yield return Wait(.6f); Watch(hand); start = Time.time;
            while (Time.time - start < 3) { input.pose = Mathf.FloorToInt((Time.time - start) * 10) % 2 == 0 ? controller : tracked; yield return null; }
            Check(worstStep < 1.2f, "Rapid source swapping: palm never jumps (largest step " + worstStep.ToString("F2") + " m)");
            JudgeShape("Rapid swapping between controller and tracked hand"); Judge("Rapid swapping between controller and tracked hand");

            // 6. After all that, is the hand still quick? Open to fist, timed.
            input.pose = calm; yield return Wait(xr ? 1.2f : .6f);
            input.pose.skeleton = Fist(L); start = Time.time; float took = -1;
            while (Time.time - start < 1) { yield return null; if (FingerError(hand, Fist(L)) < Tolerance + 4) { took = Time.time - start; break; } }
            Line("QUICK open to fist drawn in " + (took < 0 ? "more than 1.00" : took.ToString("F3")) + " s");
            Check(took >= 0 && took < .2f, "A real quick fist still arrives within a fifth of a second (" + took.ToString("F3") + " s)");
            input.pose = calm; yield return Wait(.6f); Watch(hand);
        }
        static IEnumerator WallPress(CandyPhysicalHand hand)
        {
            float size = hand.GeometrySize; Vector3 palm = hand.bodies[0].position;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "Hand feel wall";
            float face = palm.z + .13f * size; // just beyond the fingertips
            wall.transform.position = new Vector3(palm.x, palm.y, face + 1); wall.transform.localScale = new Vector3(14, 14, 2);
            Vector3 home = input.pose.position; Watch(hand);
            // Fingertips first. Push the tracked hand a little past the wall, then much further.
            float reach = 0; for (int i = 1; i < 16; i++) reach = Mathf.Max(reach, hand.bodies[i].position.z + hand.bodies[i].transform.up.z * ReferenceLength(hand, i) * size);
            float touch = (face - reach) / size;
            input.pose.position = home + Vector3.forward * (touch + .03f); yield return Wait(1.5f);
            float pushNear = hand.AppliedLinearForceNewtons, gapNear = hand.Target.position.z - hand.bodies[0].position.z;
            input.pose.position = home + Vector3.forward * (touch + .06f); yield return Wait(1.5f);
            float pushMid = hand.AppliedLinearForceNewtons, gapMid = hand.Target.position.z - hand.bodies[0].position.z;
            Shot(hand, "wall-fingertips", new Vector3(.22f, .12f, -.02f));
            input.pose.position = home + Vector3.forward * (touch + .2f);
            for (int n = 0; n < 6; n++) { yield return Wait(.25f); Line($"WALL far press t={n * .25f + .25f:F2}s target z={hand.Target.position.z:F2} palm z={hand.bodies[0].position.z:F2} carrier z={motor.Body.position.z:F2} push={hand.AppliedLinearForceNewtons:F0} N interactive={hand.IsInteractive} strength={hand.Strength:F2}"); }
            float pushFar = hand.AppliedLinearForceNewtons, gapFar = hand.Target.position.z - hand.bodies[0].position.z;
            float deepest = 0;
            for (int i = 1; i < 16; i++) deepest = Mathf.Max(deepest, hand.bodies[i].position.z + hand.bodies[i].transform.up.z * ReferenceLength(hand, i) * size - face);
            Line($"WALL fingertips: gap {gapNear:F2} m -> push {pushNear:F0} N; gap {gapMid:F2} m -> push {pushMid:F0} N; gap {gapFar:F2} m -> push {pushFar:F0} N (budget 1200); deepest finger point {deepest:F2} m past the face; back-bend {worstBackBend:F1} deg");
            Check(gapFar > .15f && gapFar >= gapMid, "Wall: the hand stops at the wall while tracking keeps going (gap " + gapFar.ToString("F2") + " m)");
            Check(deepest < .25f, "Wall: no finger goes through the wall (deepest " + deepest.ToString("F2") + " m)");
            Check(pushFar > pushNear * 1.3f && pushFar >= pushMid * .98f && pushMid >= pushNear * .9f, "Wall: the further tracking runs past the wall, the harder the hand pushes (" + pushNear.ToString("F0") + ", " + pushMid.ToString("F0") + ", " + pushFar.ToString("F0") + " N)");
            Check(pushFar <= 1210, "Wall: the push never exceeds the hand's strength rule (" + pushFar.ToString("F0") + " of 1200 N)");
            Check(worstBackBend < 25, "Wall: fingers stay firm instead of folding backwards (" + worstBackBend.ToString("F1") + " deg)");
            Judge("Open hand pressed fingertips-first into a wall");
            input.pose.position = home; yield return Wait(1);
            // Knuckles first.
            input.pose.skeleton = Fist(L); yield return Wait(.8f); Watch(hand);
            input.pose.position = home + Vector3.forward * .2f; yield return Wait(1.5f);
            Shot(hand, "wall-fist", new Vector3(.22f, .12f, -.02f));
            Judge("Fist pressed into a wall");
            input.pose.position = home; input.pose.skeleton = Open(L); yield return Wait(1);
            Object.Destroy(wall);
            // Flat hand pressed down onto a floor.
            palm = hand.bodies[0].position;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "Hand feel floor";
            floor.transform.position = palm + Vector3.down * (.03f * size + 1) + Vector3.forward * .04f * size; floor.transform.localScale = new Vector3(14, 2, 14);
            Watch(hand);
            input.pose.position = home + Vector3.down * .12f; yield return Wait(1.5f);
            float sunk = floor.transform.position.y + 1 - hand.bodies[0].position.y;
            Vector3 tilt = (Quaternion.Inverse(hand.Target.rotation) * hand.bodies[0].rotation).eulerAngles, aim = hand.Target.rotation.eulerAngles, carrierTurn = motor.Body.rotation.eulerAngles;
            Line($"FLOOR palm tilt from target (pitch, yaw, roll) {Mathf.DeltaAngle(0, tilt.x):F0} {Mathf.DeltaAngle(0, tilt.y):F0} {Mathf.DeltaAngle(0, tilt.z):F0} deg; target angle {Mathf.DeltaAngle(0, aim.x):F0} {Mathf.DeltaAngle(0, aim.y):F0} {Mathf.DeltaAngle(0, aim.z):F0}; carrier angle {Mathf.DeltaAngle(0, carrierTurn.x):F0} {Mathf.DeltaAngle(0, carrierTurn.y):F0} {Mathf.DeltaAngle(0, carrierTurn.z):F0}; palm is {hand.bodies[0].position.y - (floor.transform.position.y + 1):F2} m above the floor; push {hand.AppliedLinearForceNewtons:F0} N; gap {hand.bodies[0].position.y - hand.Target.position.y:F2} m");
            Shot(hand, "floor-flat", new Vector3(.2f, .14f, -.1f));
            Check(sunk < 0, "Floor: the palm rests on the floor instead of sinking in");
            Check(FingerError(hand, Open(L)) < 35, "Floor: fingers lie flat on the floor (" + FingerError(hand, Open(L)).ToString("F1") + " deg off the tracked open hand)");
            Judge("Flat hand pressed onto a floor");
            // Claw pressed down: fingertips land first and must hold their shape.
            input.pose.position = home; input.pose.skeleton = Claw(L); yield return Wait(1); Watch(hand);
            input.pose.position = home + Vector3.down * .12f; yield return Wait(1.5f);
            Shot(hand, "floor-claw", new Vector3(.2f, .14f, -.1f));
            Judge("Claw pressed onto a floor");
            input.pose.position = home; input.pose.skeleton = Open(L); yield return Wait(1);
            Object.Destroy(floor);
        }
        static float lastHold; static bool lastLift;
        // Fingers hang down and open wide, ready to come down around a block.
        static CandyHandSkeleton Reach(bool left, bool pinchOnly)
        {
            var rest = CandyHandSkeleton.Rest(left); var result = rest;
            for (int f = 0; f < 5; f++)
            {
                Vector3 at = rest.Joint(f * 4);
                for (int j = 0; j < 3; j++)
                {
                    float length = (rest.Joint(f * 4 + j + 1) - rest.Joint(f * 4 + j)).magnitude;
                    Vector3 direction = f == 0 ? new Vector3(0, -1, -.6f) : pinchOnly && f > 1 ? Vector3.forward : new Vector3(0, -1, .45f);
                    at += direction.normalized * length;
                    result.SetJoint(f * 4 + j + 1, at);
                }
            }
            return result;
        }
        static IEnumerator Move(Vector3 to, float seconds)
        {
            Vector3 from = input.pose.position; float start = Time.time;
            while (Time.time - start < seconds) { input.pose.position = Vector3.Lerp(from, to, (Time.time - start) / seconds); yield return null; }
            input.pose.position = to;
        }
        static IEnumerator Grip(Vector3 at, bool pinch)
        {
            yield return Spawn(at);
            var hand = bridge.PhysicalHand; string name = pinch ? "Pinch" : "Full grasp"; lastHold = 0; lastLift = false;
            Vector3 home = input.pose.position;
            // Open the hand for a grab and wait above the block's place.
            input.pose.skeleton = Reach(L, pinch); yield return Wait(1);
            Vector3 wrist = hand.bodies[0].position;
            // A castle-sized 40 kg plank on a thin stand, with castle friction, 1.5 m below the palm.
            var material = new PhysicsMaterial("Castle friction (test copy)") { staticFriction = .65f, dynamicFriction = .55f, frictionCombine = PhysicsMaterialCombine.Average };
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube); block.name = "Hand feel block"; block.layer = Layers.Block;
            block.transform.localScale = new Vector3(2.4f, .8f, .6f); block.transform.position = wrist + new Vector3(0, -.87f - 1.5f, .62f);
            block.GetComponent<Collider>().sharedMaterial = material;
            var body = block.AddComponent<Rigidbody>(); body.mass = 40;
            var stand = GameObject.CreatePrimitive(PrimitiveType.Cube); stand.name = "Hand feel stand";
            stand.transform.localScale = new Vector3(1.2f, 3, .2f); stand.transform.position = block.transform.position + Vector3.down * 1.9f;
            yield return Wait(.6f);
            Vector3 rested = body.position; Watch(hand);
            // Come down over it, close, then lift straight up.
            yield return Move(home + Vector3.down * .075f, 2); yield return Wait(.5f);
            Shot(hand, (pinch ? "pinch" : "grasp") + "-over", new Vector3(.2f, .1f, -.14f));
            var open = Reach(L, pinch); var grasp = Grasp(L, pinch); float start = Time.time;
            while (Time.time - start < .8f) { input.pose.skeleton = Lerp(open, grasp, (Time.time - start) / .8f); yield return null; }
            input.pose.skeleton = grasp; yield return Wait(.8f);
            float nudged = Vector3.Distance(body.position, rested);
            Shot(hand, (pinch ? "pinch" : "grasp") + "-closed", new Vector3(.2f, .1f, -.14f));
            Shot(hand, (pinch ? "pinch" : "grasp") + "-closed-front", new Vector3(.02f, -.05f, .3f));
            float handBefore = hand.bodies[0].position.y;
            yield return Move(home + Vector3.up * .1f, 3); yield return Wait(1.5f);
            float rise = body.position.y - rested.y, handRise = hand.bodies[0].position.y - handBefore;
            lastLift = rise > 2.5f && Mathf.Abs(rise - handRise) < .8f;
            Shot(hand, (pinch ? "pinch" : "grasp") + "-lifted", new Vector3(.3f, -.06f, -.12f));
            Line($"GRIP {name}: closing moved the block {nudged:F2} m; block rose {rise:F2} m while the hand rose {handRise:F2} m; push {hand.AppliedLinearForceNewtons:F0} N; lifted={lastLift}");
            if (lastLift)
            {
                // Pull the block down harder and harder until it slips out. That pull is the grip strength.
                float pull = 0; Vector3 offset = body.position - hand.bodies[0].position;
                var puller = block.AddComponent<ConstantForce>();
                while (pull < 6000)
                {
                    pull += 400 * Time.deltaTime; puller.force = Vector3.down * pull;
                    if ((body.position - hand.bodies[0].position - offset).magnitude > .7f) break;
                    yield return null;
                }
                lastHold = pull;
                Shot(hand, (pinch ? "pinch" : "grasp") + "-slipped", new Vector3(.2f, .1f, -.14f));
            }
            Judge(name + " on a block, lift and slip");
            Object.Destroy(block); Object.Destroy(stand); Object.Destroy(material);
        }
        static IEnumerator DesktopPoses()
        {
            if (actor) { Object.Destroy(actor.gameObject); yield return Wait(.2f); }
            var prefab = AssetDatabase.LoadAssetAtPath<CastleHandActor>(ProjectPath.Find("CastleFlyingHand.prefab"));
            actor = Object.Instantiate(prefab); motor = actor.motor;
            motor.captureDesktopCursor = false; motor.ControlModeOverride = ControlMode.Desktop; motor.RefreshControlMode();
            actor.Place(new Vector3(240, 20, 0), Quaternion.identity);
            bridge = actor.GetComponent<CandyFlyingHandBridge>();
            var profile = HandDriveProfile.For(actor.gameObject); profile.Apply(null); bridge.PhysicsProfile = profile;
            yield return Wait(1);
            var hand = bridge.PhysicalHand;
            Check(hand && bridge.Mode == CandyHandMode.Desktop, "Keyboard and mouse mode builds the same hand");
            if (!hand) yield break;
            Watch(hand);
            foreach (DesktopHandPose pose in Enum.GetValues(typeof(DesktopHandPose)))
            {
                bridge.SelectDesktopPose(pose); yield return Wait(.9f);
                Shot(hand, "desktop-" + pose.ToString().ToLowerInvariant(), new Vector3(.16f, .2f, -.12f));
            }
            Judge("Keyboard poses (pointer, cup, fist, flat, peace, spread)");
        }
    }
}
