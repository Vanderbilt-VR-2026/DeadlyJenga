using FlyingHand;
using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.Core;
using DeadlyJenga.CandyPhysicsHands;
using DeadlyJenga.CandyPlayers;

namespace DeadlyJenga.CastlePrototype
{
    // Prefab boundary: swap this whole actor, or only its HandVisual children.
    public sealed class CastleHandActor : MonoBehaviour, ITeleportable, IFlyingState, IPlayableCharacter, IMenuInputState
    {
        public FlyingHandController motor;
        public ThirdPersonVRFollow follow;
        public Camera View => follow.viewCamera;
        CandyFlyingHandBridge bridge;
        CandyFlyingHandBridge Bridge => bridge ? bridge : bridge = GetComponent<CandyFlyingHandBridge>();

        // IFlyingState
        public Vector3 Velocity => motor && motor.Body ? motor.Body.linearVelocity : Vector3.zero;
        public Transform Anchor => motor ? motor.transform : transform;
        public bool Interactive => Bridge && Bridge.PhysicalHand && Bridge.PhysicalHand.IsInteractive;
        public IReadOnlyList<Rigidbody> ContactBodies => Bridge && Bridge.PhysicalHand ? Bridge.PhysicalHand.bodies : null;
        void OnEnable() => Tracked.Flyers.Add(this);
        void OnDisable() => Tracked.Flyers.Remove(this);

        // IPlayableCharacter (Place is below)
        public Camera ViewCamera => View;
        public bool IsVR => motor && motor.ActiveControlMode == ControlMode.VR;
        public bool InputSuspended { get => motor && motor.InputSuspended; set { if (motor) motor.InputSuspended = value; } }
        CandyTrackingSource handTracking;
        CandyTrackingSource HandTracking => handTracking ? handTracking : handTracking = GetComponent<CandyTrackingSource>();
        public bool HasHandTracking => HandTracking && HandTracking.HasHandTracking;
        public bool MenuPressed => HandTracking && HandTracking.LeftMenuPressed;
        public bool MenuHeld=>HandTracking&&HandTracking.LeftMenuHeld;
        public bool MovementHeld
        {
            get
            {
                if(!motor||motor.externallyDriven)return false;
                var keys=UnityEngine.InputSystem.Keyboard.current;
                if(keys!=null&&(keys.wKey.isPressed||keys.aKey.isPressed||keys.sKey.isPressed||
                    keys.dKey.isPressed||keys.qKey.isPressed||keys.eKey.isPressed))return true;
                if(!IsVR)return false;
                bool dominantLeft=PlayerPrefs.GetInt("CandyBlaster.LeftHanded",0)!=0;
                var dominant=HybridXRInput.Controller(dominantLeft);
                var support=HybridXRInput.Controller(!dominantLeft);
                var controller=dominant??support;
                float Axis(string name)=>controller?.TryGetChildControl<UnityEngine.InputSystem.Controls.AxisControl>(name)?.ReadValue()??0;
                if(dominant!=null&&support!=null)
                {
                    var look=HybridXRInput.Stick(dominant);
                    return HybridXRInput.Stick(support).magnitude>motor.stickDeadzone||
                        Mathf.Abs(look.x)>motor.stickDeadzone||Mathf.Abs(look.y)>motor.stickDeadzone||
                        Axis("trigger")!=Axis("grip");
                }
                if(controller!=null)
                {
                    if(motor.leftControllerFlight)
                    {
                        var stick=HybridXRInput.Stick(controller);
                        bool turning=controller.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("primaryButton")?.isPressed==true;
                        bool stickHeld=turning?Mathf.Abs(stick.x)>motor.stickDeadzone||Mathf.Abs(stick.y)>motor.stickDeadzone:
                            stick.magnitude>motor.stickDeadzone;
                        return stickHeld||Axis("trigger")!=Axis("grip");
                    }
                    return motor.move.ReadValue<Vector2>().magnitude>motor.stickDeadzone||
                        Mathf.Abs(motor.turn.ReadValue<Vector2>().x)>motor.stickDeadzone||
                        motor.ascend.ReadValue<float>()!=motor.descend.ReadValue<float>();
                }
                if(!HandTracking)return false;
                return FlightPinchHeld(HandTracking.WorldHand(true))||FlightPinchHeld(HandTracking.WorldHand(false));
            }
        }
        static bool FlightPinchHeld(CandyHandPose hand)=>hand.tracked&&hand.skeletonTracked&&
            CandyHandTransition.IsValidPose(hand)&&Vector3.Distance(hand.skeleton.Joint(3),hand.skeleton.Joint(7))<.032f;
        public bool SelectPressed => HandTracking && HandTracking.MenuSelectPressed;
        public bool SelectHeld => HandTracking && HandTracking.MenuSelectHeld;
        public string StatusText => "";
        public void PrepareForSession()
        {
            if (View) View.clearFlags = CameraClearFlags.Skybox;
            if (motor) motor.captureDesktopCursor = false;
        }

        public void Teleport(Vector3 position, Quaternion rotation) => Place(position, rotation);
        public void Place(Vector3 position, Quaternion rotation)
        {
            motor.Body.position = position; motor.Body.rotation = rotation;
            motor.Body.linearVelocity = Vector3.zero; motor.Body.angularVelocity = Vector3.zero;
            motor.transform.SetPositionAndRotation(position, rotation);
            if(Bridge)Bridge.ResetTrackingRig();
            follow.SnapToTarget();
        }
    }
}
