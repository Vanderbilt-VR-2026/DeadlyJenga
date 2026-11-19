#if UNITY_EDITOR || UNITY_STANDALONE
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.ProviderImplementation;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CandyPhysicsHands;
using DeadlyJenga.Presentation;

namespace DeadlyJenga.DevTools
{
    // Emulated headset hand tracking. It is a real XR Hands provider, so the game reads these hands exactly
    // the way it reads a Quest's: through XRHandSubsystem joints. Nothing in the game knows it is emulated.
    // Together with PcSimulatedVR (emulated headset + controllers) this lets a person or a test play "in VR" on a PC.
    //
    // By hand (with XR Simulation chosen in the home menu):
    //   H  put the dominant controller down and use a tracked hand instead (H again picks the controller back up)
    //   1-6  hand shape: pointer, cup, fist, flat, peace, spread      hold left mouse  clench
    //   Hold E (or Q) + mouse  move that hand, exactly as for the controller
    // By script: Create(), then Set(left, tracked, wrist, joints) every frame. Any joint positions are allowed,
    // including impossible ones; that is the point.
    public sealed class SimulatedTrackedHands : MonoBehaviour
    {
        public static SimulatedTrackedHands Instance { get; private set; }
        // True once a script calls Set: the keyboard then leaves the hands alone.
        public bool ScriptControlled { get; private set; }

        const string ProviderId = "Deadly Jenga simulated tracked hands";
        static bool registered;
        // What the provider reports. Index 0 = left, 1 = right.
        static readonly bool[] tracked = new bool[2];
        static readonly Pose[] wrists = { Pose.identity, Pose.identity };
        static readonly Vector3[][] joints = { new Vector3[20], new Vector3[20] };

        // Same twenty joints, in the same order, that CandyTrackingSource reads from the headset.
        static readonly XRHandJointID[] JointIds =
        {
            XRHandJointID.ThumbMetacarpal, XRHandJointID.ThumbProximal, XRHandJointID.ThumbDistal, XRHandJointID.ThumbTip,
            XRHandJointID.IndexProximal, XRHandJointID.IndexIntermediate, XRHandJointID.IndexDistal, XRHandJointID.IndexTip,
            XRHandJointID.MiddleProximal, XRHandJointID.MiddleIntermediate, XRHandJointID.MiddleDistal, XRHandJointID.MiddleTip,
            XRHandJointID.RingProximal, XRHandJointID.RingIntermediate, XRHandJointID.RingDistal, XRHandJointID.RingTip,
            XRHandJointID.LittleProximal, XRHandJointID.LittleIntermediate, XRHandJointID.LittleDistal, XRHandJointID.LittleTip
        };
        static readonly XRHandJointID[] Metacarpals =
            { XRHandJointID.IndexMetacarpal, XRHandJointID.MiddleMetacarpal, XRHandJointID.RingMetacarpal, XRHandJointID.LittleMetacarpal };

        sealed class Subsystem : XRHandSubsystem { }
        sealed class Provider : XRHandSubsystemProvider
        {
            public override void Start() { }
            public override void Stop() { }
            public override void Destroy() { }
            public override void GetHandLayout(NativeArray<bool> layout)
            {
                layout[XRHandJointID.Wrist.ToIndex()] = true; layout[XRHandJointID.Palm.ToIndex()] = true;
                foreach (var id in JointIds) layout[id.ToIndex()] = true;
                foreach (var id in Metacarpals) layout[id.ToIndex()] = true;
            }
            public override XRHandSubsystem.UpdateSuccessFlags TryUpdateHands(XRHandSubsystem.UpdateType updateType,
                ref Pose leftRoot, NativeArray<XRHandJoint> leftJoints, ref Pose rightRoot, NativeArray<XRHandJoint> rightJoints)
            {
                var flags = XRHandSubsystem.UpdateSuccessFlags.None;
                if (tracked[0]) { Fill(Handedness.Left, 0, ref leftRoot, leftJoints); flags |= XRHandSubsystem.UpdateSuccessFlags.LeftHandRootPose | XRHandSubsystem.UpdateSuccessFlags.LeftHandJoints; }
                if (tracked[1]) { Fill(Handedness.Right, 1, ref rightRoot, rightJoints); flags |= XRHandSubsystem.UpdateSuccessFlags.RightHandRootPose | XRHandSubsystem.UpdateSuccessFlags.RightHandJoints; }
                return flags;
            }
            static void Fill(Handedness side, int hand, ref Pose root, NativeArray<XRHandJoint> into)
            {
                var wrist = wrists[hand]; root = wrist;
                void Put(XRHandJointID id, Vector3 local) => into[id.ToIndex()] = XRHandProviderUtility.CreateJoint(
                    side, XRHandJointTrackingState.Pose, id, new Pose(wrist.position + wrist.rotation * local, wrist.rotation));
                Put(XRHandJointID.Wrist, Vector3.zero);
                Put(XRHandJointID.Palm, joints[hand][8] * .5f);
                for (int i = 0; i < 20; i++) Put(JointIds[i], joints[hand][i]);
                for (int f = 0; f < 4; f++) Put(Metacarpals[f], joints[hand][4 + f * 4] * .3f);
            }
        }

        XRHandSubsystem subsystem;
        XRHandProviderUtility.SubsystemUpdater updater;

        public static SimulatedTrackedHands Create()
        {
            if (Instance) return Instance;
            var go = new GameObject("Simulated tracked hands");
            DontDestroyOnLoad(go);
            return go.AddComponent<SimulatedTrackedHands>();
        }

        // Starts with XR Simulation, so a person only has to press H.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartWithXRSimulation()
        {
            if (XRSimulationManager.CurrentMode == XRSimulationManager.XRSimulation) Create();
        }

        void OnEnable()
        {
            Instance = this;
            tracked[0] = tracked[1] = false;
            if (!registered)
            {
                XRHandSubsystemDescriptor.Register(new XRHandSubsystemDescriptor.Cinfo
                { id = ProviderId, providerType = typeof(Provider), subsystemTypeOverride = typeof(Subsystem) });
                registered = true;
            }
            var descriptors = new List<XRHandSubsystemDescriptor>();
            SubsystemManager.GetSubsystemDescriptors(descriptors);
            foreach (var descriptor in descriptors)
                if (descriptor.id == ProviderId) { subsystem = descriptor.Create(); break; }
            if (subsystem == null) { Debug.LogError("Simulated tracked hands could not start their XR Hands subsystem."); return; }
            subsystem.Start();
            updater = new XRHandProviderUtility.SubsystemUpdater(subsystem);
            updater.Start();
        }

        void OnDisable()
        {
            tracked[0] = tracked[1] = false;
            updater?.Stop(); updater?.Destroy(); updater = null;
            if (subsystem != null) { subsystem.Stop(); subsystem.Destroy(); subsystem = null; }
            if (PcSimulatedVR.Instance && !ScriptControlled) PcSimulatedVR.Instance.TestLeftUntracked = PcSimulatedVR.Instance.TestRightUntracked = false;
            if (Instance == this) Instance = null;
        }

        // wrist: the wrist in tracking space (the space controllers report in).
        // wristLocalJoints: the twenty finger joints relative to the wrist, in metres, as a headset reports them.
        public void Set(bool left, bool isTracked, Pose wrist, CandyHandSkeleton wristLocalJoints)
        {
            ScriptControlled = true;
            Apply(left, isTracked, wrist, wristLocalJoints);
        }
        // Raw joints, for feeding deliberately broken tracking.
        public void Set(bool left, bool isTracked, Pose wrist, Vector3[] wristLocalJoints)
        {
            ScriptControlled = true;
            int hand = left ? 0 : 1; tracked[hand] = isTracked; wrists[hand] = wrist;
            if (wristLocalJoints != null && wristLocalJoints.Length == 20) System.Array.Copy(wristLocalJoints, joints[hand], 20);
        }
        static void Apply(bool left, bool isTracked, Pose wrist, CandyHandSkeleton skeleton)
        {
            int hand = left ? 0 : 1; tracked[hand] = isTracked; wrists[hand] = wrist;
            for (int i = 0; i < 20; i++) joints[hand][i] = skeleton.Joint(i);
        }

        // A glove-shaped pose stretched to a real hand's proportions: knuckles further from the wrist, longer fingers.
        public static CandyHandSkeleton RealHand(bool left, CandyHandSkeleton gloveShape)
        {
            var rest = CandyHandSkeleton.Rest(left); var result = gloveShape;
            for (int f = 0; f < 5; f++)
            {
                Vector3 at = rest.Joint(f * 4) * 1.15f + (f == 0 ? Vector3.zero : Vector3.forward * .04f);
                result.SetJoint(f * 4, at);
                for (int j = 0; j < 3; j++)
                {
                    at += (gloveShape.Joint(f * 4 + j + 1) - gloveShape.Joint(f * 4 + j)) * 1.3f;
                    result.SetJoint(f * 4 + j + 1, at);
                }
            }
            return result;
        }

        // ---------- by hand ----------
        bool handOut; DesktopHandPose shape = DesktopHandPose.Flat; float clench;
        void Update()
        {
            var vr = PcSimulatedVR.Instance; var keys = Keyboard.current;
            if (ScriptControlled || !vr || keys == null) return;
            bool left = PlayerPrefs.GetInt("CandyBlaster.LeftHanded", 0) != 0;
            if (keys.hKey.wasPressedThisFrame) handOut = !handOut;
            if (left) vr.TestLeftUntracked = handOut; else vr.TestRightUntracked = handOut;
            if (!handOut) { tracked[0] = tracked[1] = false; return; }
            if (keys.digit1Key.wasPressedThisFrame) shape = DesktopHandPose.Pointer;
            if (keys.digit2Key.wasPressedThisFrame) shape = DesktopHandPose.Cup;
            if (keys.digit3Key.wasPressedThisFrame) shape = DesktopHandPose.Fist;
            if (keys.digit4Key.wasPressedThisFrame) shape = DesktopHandPose.Flat;
            if (keys.digit5Key.wasPressedThisFrame) shape = DesktopHandPose.Peace;
            if (keys.digit6Key.wasPressedThisFrame) shape = DesktopHandPose.Spread;
            bool squeeze = Mouse.current != null && Mouse.current.leftButton.isPressed;
            clench = Mathf.MoveTowards(clench, squeeze ? 1 : 0, Time.unscaledDeltaTime * 6);
            Apply(left, true, vr.SimulatedControllerPose(left), RealHand(left, DesktopHandPoses.Build(left, shape, clench)));
            tracked[left ? 1 : 0] = false;
        }
    }
}
#endif
