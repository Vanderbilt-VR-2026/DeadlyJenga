using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace DeadlyJenga.XRSimulation.Editor
{
    public sealed class SimulatorWindow : EditorWindow
    {
        SimulatorRig rig;
        string status = "Open Game, enter Play Mode, then attach. Use an isolated headset-free checkout.";
        bool supplementalLeft;

        [MenuItem("Deadly Jenga/Testing/Simulated VR Controls")]
        static void Open() => GetWindow<SimulatorWindow>("Simulated VR");

        void OnEnable()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += Changed;
        }
        void OnDisable()
        {
            try { Detach(); }
            finally
            {
                EditorApplication.update -= Tick;
                EditorApplication.playModeStateChanged -= Changed;
            }
        }
        void Changed(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode) Detach();
        }
        void Detach()
        {
            var previous = rig; rig = null;
            try
            {
                previous?.Dispose();
                status = "Detached; temporary input settings and preferences restored.";
            }
            catch (Exception e) { status = "Cleanup needs inspection: " + e.Message; throw; }
        }
        void Tick()
        {
            if (rig == null || !EditorApplication.isPlaying) return;
            rig.SelectCamera();
            if (rig.Supplemental != null)
            {
                if (rig.Simulator.deviceLifecycleManager.deviceMode != SimulatedDeviceLifecycleManager.DeviceMode.Hand)
                {
                    rig.RemoveSupplemental();
                    status = "Returned to stock controller mode; supplemental fixture removed.";
                }
                else
                {
                    var k = Keyboard.current;
                    if (k != null) rig.WriteSupplemental(supplementalLeft,
                        new Vector2((k.lKey.isPressed ? 1 : 0) - (k.jKey.isPressed ? 1 : 0), (k.iKey.isPressed ? 1 : 0) - (k.kKey.isPressed ? 1 : 0)),
                        k.gKey.isPressed, k.tKey.isPressed);
                }
            }
            Repaint();
        }
        void OnGUI()
        {
            EditorGUILayout.HelpBox("SIMULATED INPUT ONLY. This does not run the Quest APK or validate physical tracking, comfort or Quest performance.", MessageType.Info);
            EditorGUILayout.LabelField(status, EditorStyles.wordWrappedLabel);
            if (rig == null)
            {
                using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
                    if (GUILayout.Button("Attach existing XRI simulator to Game"))
                        try { rig = new SimulatorRig(true); status = "Attached. Input-only fixture forces the real survivor and flying hand into VR mode."; }
                        catch (Exception e) { status = e.Message; }
                return;
            }
            EditorGUILayout.LabelField("Stock mode", rig.Simulator.deviceLifecycleManager.deviceMode.ToString());
            EditorGUILayout.LabelField("Mixed fixture", rig.Supplemental == null ? "None" : supplementalLeft ? "Left controller + XRI hands" : "Right controller + XRI hands");
            EditorGUILayout.HelpBox("Focus Game view. WASD/QE moves the selected device; arrow keys rotate. H selects head; [ and ] select hands/controllers; repeat a selected bracket to switch stock controller/hand mode. IJKL = stick, G = grip, T = trigger. Hold Shift for left-controller actions. Hand gestures: M pinch, N poke, K grab, P fist, O open; Shift targets left hand.", MessageType.None);
            EditorGUILayout.HelpBox("Mixed tests require stock HAND mode. The extra controller is a fixture; optical joints still come from the XRI hand subsystem. In mixed mode K/L/M also have stock hand bindings—use the production tests to isolate conflicts.", MessageType.Warning);
            using (new EditorGUI.DisabledScope(rig.Simulator.deviceLifecycleManager.deviceMode != SimulatedDeviceLifecycleManager.DeviceMode.Hand))
            {
                if (GUILayout.Button("Mixed fixture: left controller")) { supplementalLeft = true; rig.AddSupplemental(true); }
                if (GUILayout.Button("Mixed fixture: right controller")) { supplementalLeft = false; rig.AddSupplemental(false); }
            }
            if (GUILayout.Button("Remove mixed controller")) rig.RemoveSupplemental();
            if (GUILayout.Button("Use left-dominant controls")) PlayerPrefs.SetInt("CandyBlaster.LeftHanded", 1);
            if (GUILayout.Button("Use right-dominant controls")) PlayerPrefs.SetInt("CandyBlaster.LeftHanded", 0);
            if (GUILayout.Button("Drop all simulated tracking")) SetTracking(false);
            if (GUILayout.Button("Restore simulated tracking")) SetTracking(true);
            if (GUILayout.Button("Detach and restore")) Detach();
        }
        void SetTracking(bool tracked)
        {
            var sim = rig.Simulator;
            sim.hmdIsTracked = tracked;
            sim.hmdTrackingState = tracked ? UnityEngine.XR.InputTrackingState.Position | UnityEngine.XR.InputTrackingState.Rotation : UnityEngine.XR.InputTrackingState.None;
            sim.leftControllerIsTracked = sim.rightControllerIsTracked = tracked;
            sim.leftHandIsTracked = sim.rightHandIsTracked = tracked;
            if (!tracked) rig.RemoveSupplemental();
        }
    }
}
