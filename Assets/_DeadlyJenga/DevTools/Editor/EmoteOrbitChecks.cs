using System;
using System.Collections;
using System.IO;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using FlyingHand;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static class EmoteOrbitChecks
    {
        const string Key = "EmoteOrbitChecks";
        static string Report => ProjectPath.Report("EmoteOrbit.txt");
        static int errors, failures;

        static EmoteOrbitChecks()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Key, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    errors = failures = 0;
                    Application.logMessageReceived += Log;
                    EditorApplication.delayCall += () =>
                    {
                        var runner = new GameObject("Emote validation driver").AddComponent<EmoteOrbitCheckDriver>();
                        runner.StartCoroutine(Run());
                    };
                }
                else if (state == PlayModeStateChange.EnteredEditMode)
                {
                    Application.logMessageReceived -= Log;
                    SessionState.SetBool(Key, false);
                    EditorApplication.Exit(SessionState.GetBool(Key + "Passed", false) ? 0 : 1);
                }
            };
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use the isolated batch editor.");
            File.WriteAllText(Report, "Game.unity solo emote validation with virtual Input System devices.\n");
            SessionState.SetBool(Key, true);
            SessionState.SetBool(Key + "Passed", false);
            MatchSetup.FromLobby = false;
            MatchSetup.StartLocalSession = false;
            MatchSetup.ModeId = "demo";
            MatchSetup.MapId = "castle";
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            EditorApplication.EnterPlaymode();
        }

        static void Log(string message, string trace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            errors++;
            File.AppendAllText(Report, "ERROR " + message + "\n" + trace + "\n");
        }

        static bool Check(bool ok, string label)
        {
            if (!ok) failures++;
            File.AppendAllText(Report, (ok ? "PASS " : "FAIL ") + label + "\n");
            return ok;
        }

        static Camera Orbit()
        {
            var go = GameObject.Find("Local emote orbit");
            return go ? go.GetComponent<Camera>() : null;
        }

        static void Tracked(InputDevice device)
        {
            using var state = StateEvent.From(device, out var e);
            device.GetChildControl<ButtonControl>("isTracked").WriteValueIntoEvent(1f, e);
            InputSystem.QueueEvent(e);
        }

        static IEnumerator Run()
        {
            var checks = CheckScene();
            while (true)
            {
                object yielded;
                try
                {
                    if (!checks.MoveNext()) break;
                    yielded = checks.Current;
                }
                catch (Exception error)
                {
                    Check(false, error.ToString());
                    break;
                }
                yield return yielded;
            }

            File.AppendAllText(Report, $"Failures {failures}, runtime errors {errors}\n");
            SessionState.SetBool(Key + "Passed", failures == 0 && errors == 0);
            EditorApplication.ExitPlaymode();
        }

        static IEnumerator CheckScene()
        {
            yield return null;
            yield return null;
            var session = Services.Session as CastlePlaySession;
            if (!Check(session && session.Player != null && Services.World != null && session.Modes,
                "Production Game scene initialized with survivor, world and match runtime.")) yield break;
            if (session.Social != null) session.Social.VoiceEnabled = false;
            var motor = session.Survivor();
            var avatar = motor.GetComponent<CandyPlayerAvatar>();
            var view = (ILocalEmoteView)motor;
            var eye = motor.ViewCamera;

            // Keep the desktop session from opening its menu when batch mode cannot lock the OS cursor.
            session.enabled = false;
            session.SetRole(false);
            session.SetMenu(false);
            motor.controlMode = ControlMode.Desktop;
            motor.RefreshMode();
            motor.SendMessage("OnApplicationFocus", true);
            var fixture = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fixture.name = "Emote validation floor";
            fixture.layer = Layers.Surface;
            fixture.transform.SetPositionAndRotation(new Vector3(120, 9.5f, 120), Quaternion.identity);
            fixture.transform.localScale = new Vector3(20, 1, 20);
            motor.Teleport(new Vector3(120, 10.04f, 120), Quaternion.identity);
            Physics.SyncTransforms();
            yield return new WaitForSeconds(.4f);
            Check(motor.Grounded && !motor.IsVR && eye.enabled, "Desktop survivor is grounded with its normal camera.");

            var equipment = motor.GetComponent<CandyEquipment>();
            bool hasRifle = Check(session.Modes.GiveEquipment(EquipmentKind.DualRifle), "Solo authority can equip a drawn rifle.");
            yield return null;
            yield return null;
            var rifle = motor.transform.Find("Visual - DualRifle");
            var drawState = equipment.Equipment;
            Check(hasRifle && rifle && drawState.drawn, "Rifle starts drawn in the survivor's hand.");

            int breakdance = Array.IndexOf(Emotes.Survivor, "Breakdance");
            int irish = Array.IndexOf(Emotes.Survivor, "IrishDance");
            Emotes.Request(breakdance);
            yield return null;
            yield return null;
            Check(view.EmoteActive && Orbit() && Orbit().enabled && !eye.enabled, "Breakdance switches desktop rendering to a separate orbit camera.");
            var rifleState = equipment.Equipment;
            Vector3 toRifle = rifle ? rifle.position - motor.transform.position : Vector3.zero;
            Check(rifle && Vector3.Dot(motor.transform.forward, toRifle) < -.08f &&
                rifleState.kind == drawState.kind && rifleState.drawn == drawState.drawn,
                "The drawn rifle slings behind the survivor while authority still reports it drawn.");
            Check(!equipment.FireOnce(), "A visually slung two-handed rifle cannot fire.");
            Transform foot = avatar.Rig.transform.Find("Left foot");
            Vector3 before = foot.position;
            yield return new WaitForSeconds(.25f);
            Check(Vector3.Distance(before, foot.position) > .025f, "Breakdance moves a rendered rig foot.");
            yield return new WaitForSeconds(3.2f);
            Check(view.EmoteActive && Orbit(), "Breakdance continues beyond one cycle.");
            Emotes.Request(breakdance);
            yield return null;
            yield return null;
            Check(!view.EmoteActive && !Orbit() && eye.enabled, "Selecting the active dance restores the desktop view.");
            Vector3 RifleHand()
            {
                var frame = avatar.LastFrame;
                return equipment.Equipment.leftHanded ? frame.left.position : frame.right.position;
            }
            Check(rifle && equipment.Equipment.drawn && Vector3.Distance(rifle.position, RifleHand()) < .03f,
                "Cancel restores the same drawn rifle to the gripping hand.");
            Emotes.Request(0);
            yield return new WaitForSeconds(1.8f);
            Check(!view.EmoteActive && !Orbit() && eye.enabled, "Wave finishes and restores the desktop view automatically.");
            Check(rifle && equipment.Equipment.drawn && Vector3.Distance(rifle.position, RifleHand()) < .03f,
                "Natural completion restores the rifle.");
            equipment.SetEquipped(false);
            Emotes.Request(0);
            yield return new WaitForSeconds(1.8f);
            Check(rifle && !equipment.Equipment.drawn && Vector3.Distance(rifle.position, RifleHand()) > .12f,
                "A rifle that starts holstered stays holstered after the emote.");

            Check(session.Modes.GiveEquipment(EquipmentKind.Blaster), "Solo authority can equip a one-handed blaster.");
            Emotes.Request(0);
            yield return new WaitForSeconds(.45f);
            var blaster = motor.transform.Find("Visual - Blaster");
            var palm = avatar.Rig.transform.Find(equipment.Equipment.leftHanded ? "Left palm" : "Right palm");
            Check(view.EmoteActive && blaster && palm && equipment.Equipment.drawn &&
                Vector3.Distance(blaster.position, palm.position) < .03f, "A drawn one-handed blaster follows the emote's animated palm.");
            Emotes.Request(0);

            var inputSettings = Object.Instantiate(InputSystem.settings);
            inputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            inputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = inputSettings;
            InputSystem.RegisterLayout(@"{""name"":""EmoteCheckController"",""extend"":""XRController"",""controls"":[{""name"":""primary2DAxis"",""layout"":""Vector2""},{""name"":""primary2DAxisClick"",""layout"":""Button""},{""name"":""primaryButton"",""layout"":""Button""},{""name"":""secondaryButton"",""layout"":""Button""}]}");
            var left = InputSystem.AddDevice("EmoteCheckController");
            var right = InputSystem.AddDevice("EmoteCheckController");
            InputSystem.SetDeviceUsage(left, CommonUsages.LeftHand);
            InputSystem.SetDeviceUsage(right, CommonUsages.RightHand);
            Tracked(left);
            Tracked(right);
            var hmd = InputSystem.AddDevice<XRHMD>("Emote test HMD");
            InputSystem.QueueDeltaStateEvent(hmd.centerEyeRotation, Quaternion.identity);
            InputSystem.QueueDeltaStateEvent(hmd.deviceRotation, Quaternion.identity);
            InputSystem.QueueDeltaStateEvent(hmd.trackingState, 2);
            motor.controlMode = ControlMode.VR;
            motor.RefreshMode();
            motor.UseStationaryVR();
            session.enabled = true;
            session.SetMenu(false);
            yield return new WaitForSeconds(.4f);
            Check(motor.IsVR && session.IsVR && HybridXRInput.ControllerTracked(true) && HybridXRInput.ControllerTracked(false),
                "Forced VR uses two tracked virtual controllers.");

            var click = left.GetChildControl<ButtonControl>("primary2DAxisClick");
            InputSystem.QueueDeltaStateEvent(click, 1f);
            yield return null;
            yield return null;
            var wheel = GameObject.Find("Emote wheel panel");
            if (!Check(wheel && !session.MenuOpen, "Holding left stick click opens the quick wheel independently of the session menu.")) yield break;
            var slot = (RectTransform)wheel.transform.Find("Slot " + irish);
            var icon = slot.Find("Pose icon").GetComponent<Image>().sprite;
            var breakIcon = wheel.transform.Find("Slot " + breakdance + "/Pose icon").GetComponent<Image>().sprite;
            Check(icon && breakIcon && icon != breakIcon && wheel.GetComponentsInChildren<Image>().Length >= 21,
                "Both dance choices have distinct pose icons on the ten-slot wheel.");
            Vector3 target = slot.position;
            Quaternion gaze = Quaternion.Inverse(eye.transform.parent.rotation) *
                Quaternion.LookRotation(target - eye.transform.position, Vector3.up);
            InputSystem.QueueDeltaStateEvent(hmd.centerEyeRotation, gaze);
            InputSystem.QueueDeltaStateEvent(hmd.deviceRotation, gaze);
            yield return null;
            yield return null;
            InputSystem.QueueDeltaStateEvent(click, 0f);
            yield return null;
            yield return null;
            var vrOrbit = Orbit();
            var mirror = GameObject.Find("Local emote VR mirror");
            if (!Check(view.EmoteActive && vrOrbit && mirror && vrOrbit.targetTexture && eye.enabled,
                "Gaze and release selects Irish Dance and starts the VR mirror with the original camera enabled.")) yield break;
            before = foot.position;
            yield return new WaitForSeconds(.15f);
            Check(Vector3.Distance(before, foot.position) > .02f, "Irish Dance animates the rig footwork.");
            var orbitRotation = vrOrbit.transform.rotation;
            var eyeRotation = eye.transform.rotation;
            bool walksWithLeft = PlayerPrefs.GetInt("CandyBlaster.LeftHanded", 0) != 0;
            var stick = (walksWithLeft ? right : left).GetChildControl<Vector2Control>("primary2DAxis");
            var moveStick = (walksWithLeft ? left : right).GetChildControl<Vector2Control>("primary2DAxis");
            InputSystem.QueueDeltaStateEvent(stick, new Vector2(.9f, .35f));
            yield return new WaitForSeconds(.4f);
            Check(Quaternion.Angle(orbitRotation, vrOrbit.transform.rotation) > 10 &&
                Quaternion.Angle(eyeRotation, eye.transform.rotation) < 1,
                "Look stick orbits the mirror camera without turning the tracked camera.");
            InputSystem.QueueDeltaStateEvent(stick, Vector2.zero);
            yield return new WaitForSeconds(2.5f);
            Check(view.EmoteActive && Orbit(), "Irish Dance continues beyond one cycle.");
            Capture(vrOrbit.targetTexture);
            InputSystem.QueueDeltaStateEvent(moveStick, Vector2.up);
            yield return new WaitForSeconds(.15f);
            Check(!view.EmoteActive && !Orbit() && eye.enabled && !GameObject.Find("Local emote VR mirror"),
                "The movement stick cancels the dance and removes the VR mirror.");
            InputSystem.QueueDeltaStateEvent(moveStick, Vector2.zero);
            yield return null;
            Emotes.Request(7);
            yield return null;
            yield return null;
            Check(view.EmoteActive && Orbit(), "Existing victory dance still plays.");
            session.SetRole(true);
            yield return null;
            yield return null;
            Check(!view.EmoteActive && !Orbit() && session.PlayingHand, "Role switch clears the orbit and hands camera ownership to the session.");
        }

        static void Capture(RenderTexture target)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            pixels.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(ProjectPath.Report("EmoteOrbit-IrishDance.png"), pixels.EncodeToPNG());
            Object.Destroy(pixels);
        }
    }

    sealed class EmoteOrbitCheckDriver : MonoBehaviour { }
}
