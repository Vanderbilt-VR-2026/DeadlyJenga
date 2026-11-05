using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.DevTools.Editor;
using FlyingHand;
using Object = UnityEngine.Object;

namespace DeadlyJenga.XRSimulation.Editor
{
    // A temporary editor fixture. It never saves scenes, assets or XR settings.
    public sealed class SimulatorRig : IDisposable
    {
        public XRInteractionSimulator Simulator { get; private set; }
        public CastlePlaySession Session { get; private set; }
        public CastleSurvivor Player { get; private set; }
        public XRSimulatedController Supplemental { get; private set; }
        GameObject root;
        InputSettings originalSettings, temporarySettings;
        HideFlags originalSettingsFlags;
        CastleHandActor originalHand;
        readonly HashSet<UnityEngine.InputSystem.InputDevice> initialDevices = new HashSet<UnityEngine.InputSystem.InputDevice>();
        readonly HashSet<XRHandSubsystem> initialHandSubsystems = new HashSet<XRHandSubsystem>();
        ControlMode originalMode, originalHandMode;
        SurvivorVRPlayStyle originalStyle;
        SurvivorVRTurn originalTurn;
        SurvivorVRMovement originalMovement;
        bool originalRole, originalMenu, disposed;
        bool hadStyle, hadDominance;
        int stylePreference, dominancePreference;
        CursorLockMode cursorLock;
        bool cursorVisible;
        bool originalPlayerSuspended, originalHandSuspended;
        Pose originalPlayerPose, originalHandPose;

        public SimulatorRig(bool showControls)
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            Session = Object.FindFirstObjectByType<CastlePlaySession>();
            if (!Session || Session.Player == null) throw new InvalidOperationException("Open Game and wait for its real session.");
            if (Object.FindFirstObjectByType<XRInteractionSimulator>()) throw new InvalidOperationException("A simulator already owns this session.");
            foreach (var device in InputSystem.devices)
            {
                initialDevices.Add(device);
                if (device is XRHMD || device is UnityEngine.InputSystem.XR.XRController)
                    throw new InvalidOperationException("Existing XR input detected. Use a separate headset-free test session.");
            }
            var hands = new List<XRHandSubsystem>();
            SubsystemManager.GetSubsystems(hands);
            foreach (var subsystem in hands) initialHandSubsystems.Add(subsystem);
            if (hands.Any(x => x.running)) throw new InvalidOperationException("Another hand provider is running.");

            Player = Session.Survivor();
            originalHand = Session.HandActor();
            if (!Player || !originalHand || !originalHand.motor || !originalHand.motor.Body)
                throw new InvalidOperationException("The real survivor and physical hand must be initialized.");
            originalPlayerPose = new Pose(Player.transform.position, Player.transform.rotation);
            originalHandPose = new Pose(originalHand.motor.Body.position, originalHand.motor.Body.rotation);
            originalPlayerSuspended = Player.InputSuspended; originalHandSuspended = originalHand.InputSuspended;
            originalRole = Session.PlayingHand; originalMenu = Session.MenuOpen;
            originalMode = Player.controlMode;
            originalStyle = Player.vrPlayStyle; originalTurn = Player.vrTurn; originalMovement = Player.vrMovement;
            originalHandMode = originalHand.motor.ControlModeOverride;
            hadStyle = PlayerPrefs.HasKey("SurvivorVRPlayStyle"); stylePreference = PlayerPrefs.GetInt("SurvivorVRPlayStyle");
            hadDominance = PlayerPrefs.HasKey("CandyBlaster.LeftHanded"); dominancePreference = PlayerPrefs.GetInt("CandyBlaster.LeftHanded");
            cursorLock = Cursor.lockState; cursorVisible = Cursor.visible;
            originalSettings = InputSystem.settings;
            originalSettingsFlags = originalSettings.hideFlags;
            try
            {
                originalSettings.hideFlags = HideFlags.DontUnloadUnusedAsset;
                temporarySettings = Object.Instantiate(originalSettings);
                temporarySettings.hideFlags = HideFlags.DontUnloadUnusedAsset;
                temporarySettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                temporarySettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings = temporarySettings;
                if (Session.PlayingHand) Session.SetRole(false);
                Player.controlMode = ControlMode.VR; Player.RefreshMode();
                Session.HandActor().motor.ControlModeOverride = ControlMode.VR;
                root = new GameObject("SIMULATED VR - editor fixture, not Quest validation");
                root.SetActive(false);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPath.Find("XR Interaction Simulator.prefab"));
                if (!prefab) throw new InvalidOperationException("Import the installed XRI Interaction Simulator sample first.");
                var instance = Object.Instantiate(prefab, root.transform);
                Simulator = instance.GetComponent<XRInteractionSimulator>();
                if (!Simulator) throw new InvalidOperationException("The selected sample is not XR Interaction Simulator.");
                Simulator.deviceLifecycleManager.removeOtherHMDDevices = false;
                Simulator.cameraTransform = Player.viewCamera.transform;
                if (!showControls) Simulator.interactionSimulatorUI = null;
                root.SetActive(true);
                Session.SetMenu(true);
            }
            catch (Exception setupFailure)
            {
                try { Dispose(); }
                catch (Exception cleanupFailure)
                { throw new AggregateException("Simulator setup and cleanup both failed.", setupFailure, cleanupFailure); }
                throw;
            }
        }

        public void SelectCamera()
        {
            if (Simulator && Session.ActiveCamera) Simulator.cameraTransform = Session.ActiveCamera.transform;
        }

        public void AddSupplemental(bool left)
        {
            if (Simulator.deviceLifecycleManager.deviceMode != SimulatedDeviceLifecycleManager.DeviceMode.Hand)
                throw new InvalidOperationException("Select stock Hand mode before adding a mixed-input fixture.");
            RemoveSupplemental();
            Supplemental = InputSystem.AddDevice<XRSimulatedController>("Mixed input supplemental test controller");
            InputSystem.SetDeviceUsage(Supplemental, left ? UnityEngine.InputSystem.CommonUsages.LeftHand : UnityEngine.InputSystem.CommonUsages.RightHand);
            WriteSupplemental(left, Vector2.zero);
        }

        public void WriteSupplemental(bool left, Vector2 stick, bool grip = false, bool trigger = false)
        {
            if (Supplemental == null || !Supplemental.added) return;
            var state = new XRSimulatedControllerState
            {
                isTracked = true, trackingState = (int)(InputTrackingState.Position | InputTrackingState.Rotation),
                devicePosition = new Vector3(left ? -.24f : .24f, 1.3f, .4f), deviceRotation = Quaternion.identity,
                primary2DAxis = stick, grip = grip ? 1 : 0, trigger = trigger ? 1 : 0
            };
            state.WithButton(ControllerButton.GripButton, grip);
            state.WithButton(ControllerButton.TriggerButton, trigger);
            InputSystem.QueueStateEvent(Supplemental, state);
        }

        public void RemoveSupplemental()
        {
            if (Supplemental != null && Supplemental.added) InputSystem.RemoveDevice(Supplemental);
            Supplemental = null;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            var failures = new List<Exception>();
            void Attempt(string operation, Action action)
            {
                try { action(); }
                catch (Exception error)
                { failures.Add(new InvalidOperationException(operation + ": " + error.Message, error)); }
            }
            // Independent attempts keep actor restoration failures from stranding editor state.
            Attempt("Remove supplemental controller", RemoveSupplemental);
            Attempt("Destroy simulator fixture", () => { if (root) Object.DestroyImmediate(root); });
            Attempt("Remove remaining fixture devices", () =>
            {
                foreach (var device in InputSystem.devices.ToArray())
                    if (!initialDevices.Contains(device) && (device is XRSimulatedHMD || device is XRSimulatedController))
                        Attempt("Remove " + device.name, () => { if (device.added) InputSystem.RemoveDevice(device); });
            });
            Attempt("Stop remaining fixture hand providers", () =>
            {
                var subsystems = new List<XRHandSubsystem>();
                SubsystemManager.GetSubsystems(subsystems);
                foreach (var subsystem in subsystems)
                    if (!initialHandSubsystems.Contains(subsystem) && subsystem.GetType().Name.Contains("Simulator"))
                    {
                        Attempt("Stop simulator hand provider", subsystem.Stop);
                        Attempt("Destroy simulator hand provider", subsystem.Destroy);
                    }
            });
            Attempt("Restore player control mode", () =>
            {
                if (!Player) throw new InvalidOperationException("Original survivor no longer exists.");
                Player.controlMode = originalMode;
            });
            Attempt("Restore hand control mode", () =>
            {
                if (!originalHand || !originalHand.motor) throw new InvalidOperationException("Original hand no longer exists.");
                originalHand.motor.ControlModeOverride = originalHandMode;
            });
            Attempt("Restore session role", () =>
            {
                if (!Session) throw new InvalidOperationException("Original session no longer exists.");
                if (Session.PlayingHand != originalRole) Session.SetRole(originalRole);
                if (Session.PlayingHand != originalRole) throw new InvalidOperationException("Session refused the original role.");
            });
            Attempt("Restore survivor pose", () =>
            {
                if (!Player) throw new InvalidOperationException("Original survivor no longer exists.");
                // Teleport clears traversal without reviving or placing the other actor.
                Player.Teleport(originalPlayerPose.position, originalPlayerPose.rotation);
                if (Vector3.Distance(Player.transform.position, originalPlayerPose.position) > .001f ||
                    Quaternion.Angle(Player.transform.rotation, originalPlayerPose.rotation) > .05f)
                    throw new InvalidOperationException("Survivor pose did not restore.");
            });
            Attempt("Restore survivor play style", () =>
            {
                if (!Player) throw new InvalidOperationException("Original survivor no longer exists.");
                Player.SetVRPlayStyle(originalStyle, false);
                if (Player.vrPlayStyle != originalStyle)
                    throw new InvalidOperationException("Play style restoration was refused (climbing=" + Player.Climbing + ", vaulting=" + Player.Vaulting + ").");
            });
            Attempt("Restore survivor comfort settings", () =>
            {
                if (!Player) throw new InvalidOperationException("Original survivor no longer exists.");
                Player.vrTurn = originalTurn; Player.vrMovement = originalMovement; Player.RefreshMode();
            });
            Attempt("Restore physical hand pose", () =>
            {
                if (!originalHand || !originalHand.motor || !originalHand.motor.Body)
                    throw new InvalidOperationException("Original physical hand no longer exists.");
                originalHand.Place(originalHandPose.position, originalHandPose.rotation);
                if (Vector3.Distance(originalHand.motor.Body.position, originalHandPose.position) > .001f ||
                    Quaternion.Angle(originalHand.motor.Body.rotation, originalHandPose.rotation) > .05f ||
                    Vector3.Distance(originalHand.motor.transform.position, originalHandPose.position) > .001f ||
                    Quaternion.Angle(originalHand.motor.transform.rotation, originalHandPose.rotation) > .05f)
                    throw new InvalidOperationException("Hand body/transform pose did not restore.");
            });
            Attempt("Restore session menu", () =>
            {
                if (!Session) throw new InvalidOperationException("Original session no longer exists.");
                Session.SetMenu(originalMenu);
                if (Session.MenuOpen != originalMenu) throw new InvalidOperationException("Session refused the original menu state.");
            });
            Attempt("Restore survivor input suspension", () => { if (Player) Player.InputSuspended = originalPlayerSuspended; });
            Attempt("Restore hand input suspension", () => { if (originalHand) originalHand.InputSuspended = originalHandSuspended; });
            Attempt("Restore play style preference", () =>
            { if (hadStyle) PlayerPrefs.SetInt("SurvivorVRPlayStyle", stylePreference); else PlayerPrefs.DeleteKey("SurvivorVRPlayStyle"); });
            Attempt("Restore dominant hand preference", () =>
            { if (hadDominance) PlayerPrefs.SetInt("CandyBlaster.LeftHanded", dominancePreference); else PlayerPrefs.DeleteKey("CandyBlaster.LeftHanded"); });
            Attempt("Restore original InputSettings", () =>
            {
                if (!originalSettings) throw new InvalidOperationException("Original InputSettings was destroyed.");
                InputSystem.settings = originalSettings;
                if (InputSystem.settings != originalSettings) throw new InvalidOperationException("InputSettings replacement did not restore.");
            });
            Attempt("Restore original InputSettings hide flags", () =>
            { if (originalSettings) originalSettings.hideFlags = originalSettingsFlags; });
            Attempt("Destroy temporary InputSettings", () =>
            {
                if (!temporarySettings) return;
                if (InputSystem.settings == temporarySettings) throw new InvalidOperationException("Temporary settings remains active; kept alive to avoid a destroyed active settings object.");
                Object.DestroyImmediate(temporarySettings);
            });
            Attempt("Restore cursor lock", () => Cursor.lockState = cursorLock);
            Attempt("Restore cursor visibility", () => Cursor.visible = cursorVisible);
            if (failures.Count > 0)
            {
                var error = new AggregateException("Simulator cleanup could not restore all state; inspect each failure.", failures);
                Debug.LogError(error.ToString());
                throw error;
            }
        }
    }
}
