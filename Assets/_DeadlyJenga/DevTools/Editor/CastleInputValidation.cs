using DeadlyJenga.DevTools.Editor;
using System.Collections;
using System.IO;
using System.Text;
using FlyingHand;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;

namespace DeadlyJenga.CastlePrototype.Editor
{
    [InputControlLayout(commonUsages = new[] { "LeftHand", "RightHand" })]
    public class CastleTestXRController : XRController
    {
        [InputControl] public Vector2Control primary2DAxis { get; private set; }
        [InputControl(format = "FLT")] public ButtonControl primaryButton { get; private set; }
        [InputControl(format = "FLT")] public ButtonControl secondaryButton { get; private set; }
        [InputControl] public AxisControl trigger { get; private set; }
        protected override void FinishSetup()
        {
            base.FinishSetup(); primary2DAxis = GetChildControl<Vector2Control>("primary2DAxis");
            primaryButton = GetChildControl<ButtonControl>("primaryButton"); secondaryButton = GetChildControl<ButtonControl>("secondaryButton");
            trigger = GetChildControl<AxisControl>("trigger");
        }
    }
    [InitializeOnLoad]
    public static class CastleInputValidation
    {
        static CastleInputValidation()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("CastleInputTest", false))
                { SessionState.SetBool("CastleInputTest", false); EditorApplication.delayCall += Run; }
            };
        }
        [MenuItem("Tools/Castle Prototype/Validate Input Bindings (Play Mode) %#F9")]
        public static void Run()
        {
            if (!Application.isPlaying) { SessionState.SetBool("CastleInputTest", true); EditorApplication.EnterPlaymode(); return; }
            var session = Object.FindFirstObjectByType<CastlePlaySession>(); session.StartCoroutine(Validate(session));
        }
        public static IEnumerator Validate(CastlePlaySession session,bool xrOnly=false)
        {
            var report = new StringBuilder();
            var survivor=session.Survivor();var savedMode=survivor.controlMode;
            bool savedRole=session.PlayingHand,savedMenu=session.MenuOpen;
            bool savedPlayerSuspended=survivor.InputSuspended,savedHandSuspended=session.Hand.InputSuspended;
            var savedPlayerPosition=survivor.transform.position;var savedPlayerRotation=survivor.transform.rotation;
            var savedHandPosition=session.Hand.transform.position;var savedHandRotation=session.Hand.transform.rotation;
            float savedCaptureDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60; // Stable simulated frame duration despite editor import stalls.
            void Check(string name, bool success, string detail = "")
            {
                report.AppendLine((success ? "PASS " : "FAIL ") + name + " " + detail);
                File.WriteAllText(ProjectPath.Report("InputValidation.txt"), report.ToString());
            }
            var saved = InputSystem.settings;
            var savedFlags = saved.hideFlags;
            saved.hideFlags = HideFlags.DontUnloadUnusedAsset; // Avoid InputManager destroying its temporary default on replacement.
            var settings = Object.Instantiate(saved); settings.hideFlags = HideFlags.HideAndDontSave;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = settings;
            InputSystem.RegisterLayout<CastleTestXRController>();
            var keys = InputSystem.AddDevice<Keyboard>("Castle test keyboard");
            var mouse = InputSystem.AddDevice<Mouse>("Castle test mouse");
            var hmd = InputSystem.AddDevice<XRHMD>("Input-only test HMD");
            var left = InputSystem.AddDevice<CastleTestXRController>(); InputSystem.SetDeviceUsage(left, "LeftHand");
            var right = InputSystem.AddDevice<CastleTestXRController>(); InputSystem.SetDeviceUsage(right, "RightHand");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "Temporary input floor";
            floor.transform.position = new Vector3(100, 9.5f, 100); floor.transform.localScale = new Vector3(30, 1, 30);
            var ledge = GameObject.CreatePrimitive(PrimitiveType.Cube); ledge.name = "Temporary input ledge";
            ledge.transform.position = new Vector3(100, 11f, 102); ledge.transform.localScale = new Vector3(6, 2, 2);
            try
            {
                session.SendMessage("OnApplicationFocus", true); session.SetRole(false); session.SetMenu(false);
                var p = survivor;p.controlMode=ControlMode.Auto;p.RefreshMode();p.InputSuspended=false; p.Teleport(new Vector3(100, 10.04f, 96), Quaternion.identity); Physics.SyncTransforms();
                yield return new WaitForSeconds(.2f);
                Check("Input-only HMD keeps desktop mode", !p.IsVR);
                Vector3 before = p.transform.position;
                // A batch editor cannot acquire an OS mouse lock; desktop input is checked in an interactive editor.
                if(!Application.isBatchMode&&!xrOnly)
                {
                InputSystem.QueueStateEvent(keys, new KeyboardState(Key.W));
                yield return new WaitForSeconds(.2f);
                InputSystem.QueueStateEvent(keys, new KeyboardState());
                Check("W walks forward", p.transform.position.z - before.z > .5f, (p.transform.position - before).ToString());
                InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(80, 40) });
                yield return null; yield return null;
                Check("Mouse look rotates yaw and pitch", Mathf.Abs(Mathf.DeltaAngle(0, p.transform.eulerAngles.y)) > 1
                    && Mathf.Abs(Mathf.DeltaAngle(0, p.viewCamera.transform.localEulerAngles.x)) > 1);
                InputSystem.QueueStateEvent(mouse, new MouseState());
                before = p.transform.position;
                InputSystem.QueueStateEvent(keys, new KeyboardState(Key.Space)); yield return new WaitForSeconds(.12f);
                Check("Space jumps", p.transform.position.y - before.y > .25f);
                InputSystem.QueueStateEvent(keys, new KeyboardState()); yield return null; yield return null;
                float speed = p.VerticalSpeed;
                InputSystem.QueueStateEvent(keys, new KeyboardState(Key.Space)); yield return null; yield return null;
                Check("Second space in open air gives no jump", p.VerticalSpeed < speed && !p.Vaulting);
                InputSystem.QueueStateEvent(keys, new KeyboardState());
                p.Teleport(new Vector3(100, 11.05f, 100.45f), Quaternion.identity); Physics.SyncTransforms();
                yield return new WaitForSeconds(.08f);
                int vaults = p.VaultCount;
                InputSystem.QueueStateEvent(keys, new KeyboardState(Key.Space)); yield return new WaitForSeconds(p.vaultDuration+.15f);
                Check("Airborne space vaults nearby crate", p.VaultCount > vaults && p.transform.position.y > 11.98f, p.transform.position.ToString());
                InputSystem.QueueStateEvent(keys, new KeyboardState()); yield return null; yield return null;
                InputSystem.QueueStateEvent(keys, new KeyboardState(Key.Tab)); yield return null; yield return null;
                Check("Tab possesses hand", session.PlayingHand);
                InputSystem.QueueStateEvent(keys, new KeyboardState(Key.W, Key.E));
                session.HandActor().motor.SendMessage("OnApplicationFocus", true); before = session.HandActor().motor.Body.position;
                yield return new WaitForSeconds(.6f);
                Check("Hand W/E moves physics body", session.HandActor().motor.Body.position.z > before.z + .3f && session.HandActor().motor.Body.position.y > before.y + .2f);
                InputSystem.QueueStateEvent(keys, new KeyboardState()); yield return null; yield return null;
                }
                else report.AppendLine("SKIP Desktop keyboard/mouse and hand flight: batch mode cannot acquire mouse capture. Run this suite interactively for those checks.");
                session.SetRole(false); p.controlMode = ControlMode.VR; p.RefreshMode(); session.SetMenu(false);
                p.Teleport(new Vector3(100, 10.04f, 96), Quaternion.identity); Physics.SyncTransforms();
                yield return new WaitForSeconds(.2f); before = p.transform.position;
                InputSystem.QueueDeltaStateEvent(left.primary2DAxis, Vector2.up);
                yield return new WaitForSeconds(.2f);
                Check("XR left stick walks", p.transform.position.z > before.z + p.vrWalkSpeed*.12f);
                InputSystem.QueueDeltaStateEvent(left.primary2DAxis, Vector2.zero);
                before = p.transform.position;
                InputSystem.QueueDeltaStateEvent(right.primaryButton, 1f); yield return new WaitForSeconds(.12f);
                Check("XR A jumps", p.transform.position.y > before.y + .25f);
                InputSystem.QueueDeltaStateEvent(right.primaryButton, 0f);
                // Finish the jump fixture before aiming at a menu that follows the head.
                // Otherwise its old collider pose aims one row low as the rising camera recentres it.
                p.Teleport(new Vector3(100,10.04f,96),Quaternion.identity);Physics.SyncTransforms();
                yield return new WaitForSeconds(.25f);
                InputSystem.QueueDeltaStateEvent(left.secondaryButton, 1f); yield return null; yield return null;
                Check("XR Y opens world menu", session.MenuOpen && Object.FindFirstObjectByType<CastleWorldMenu>());
                InputSystem.QueueDeltaStateEvent(left.secondaryButton, 0f);
                var menu = Object.FindFirstObjectByType<CastleWorldMenu>();
                if (menu)
                {
                    var resume=menu.transform.Find("Resume").GetComponent<Collider>();
                    bool gazeHit=false;float angle=180;
                    InputSystem.QueueDeltaStateEvent(right.trigger,0f);
                    // TPD owns head pose. Queue a head turn, allow the real tracking / menu loop
                    // to settle, and only press once the actual gaze ray reaches the button.
                    for(int attempt=0;attempt<8&&!gazeHit;attempt++)
                    {
                        var camera=p.viewCamera.transform;
                        var desired=Quaternion.LookRotation(resume.bounds.center-camera.position,Vector3.up);
                        var local=Quaternion.Inverse(camera.parent.rotation)*desired;
                        InputSystem.QueueDeltaStateEvent(hmd.deviceRotation,local);
                        InputSystem.QueueDeltaStateEvent(hmd.centerEyeRotation,local);
                        InputSystem.QueueDeltaStateEvent(hmd.trackingState,2);
                        yield return null;yield return null;Physics.SyncTransforms();
                        angle=Vector3.Angle(camera.forward,resume.bounds.center-camera.position);
                        gazeHit=resume.Raycast(new Ray(camera.position,camera.forward),out _,4);
                    }
                    Check("Tracked gaze reaches the real Resume button before trigger",gazeHit,"angle="+angle);
                    InputSystem.QueueDeltaStateEvent(right.trigger, 1f);yield return null;yield return null;
                    Check("Gaze plus trigger activates Resume without changing role",gazeHit&&!session.MenuOpen&&!session.PlayingHand,
                        "trigger="+right.trigger.ReadValue()+" gazeBeforePress="+gazeHit+" menuOpen="+session.MenuOpen+" hand="+session.PlayingHand);
                    InputSystem.QueueDeltaStateEvent(right.trigger, 0f);
                }
                session.SetMenu(false);
                InputSystem.QueueDeltaStateEvent(right.secondaryButton, 1f); yield return null; yield return null;
                Check("XR B switches role", session.PlayingHand);
                if(xrOnly)
                {
                    InputSystem.QueueDeltaStateEvent(right.secondaryButton,0f);session.SetRole(false);p.RefreshMode();session.SetMenu(false);
                    var inspection=CandyThirdPersonInspection.For(session);var originalCamera=p.ViewCamera;inspection.Begin();yield return null;
                    Check("VR inspection preserves the tracked camera and suspends player input",inspection.Inspecting&&originalCamera.enabled&&p.InputSuspended);inspection.End();
                    yield return ModeCriteriaChecks.CheckClosedMenuInputPlay();
                    Check("Closed-menu controller context actions remain interactive",true);
                }
            }
            finally
            {
                foreach (var device in new InputDevice[] { keys, mouse, hmd, left, right }) if (device.added) InputSystem.RemoveDevice(device);
                InputSystem.settings = saved; saved.hideFlags = savedFlags;
                Time.captureDeltaTime = savedCaptureDelta;
                Object.Destroy(settings); Object.Destroy(floor); Object.Destroy(ledge);
                survivor.controlMode=savedMode;session.SetRole(savedRole);survivor.RefreshMode();
                survivor.Place(savedPlayerPosition,savedPlayerRotation);session.Hand.Place(savedHandPosition,savedHandRotation);
                session.SetMenu(savedMenu);survivor.InputSuspended=savedPlayerSuspended;session.Hand.InputSuspended=savedHandSuspended;
                report.AppendLine("Uses synthetic Input System devices in the real Unity player loop; no physical headset was available.");
                File.WriteAllText(ProjectPath.Report("InputValidation.txt"), report.ToString()); Debug.Log(report.ToString());
            }
        }
    }
}




