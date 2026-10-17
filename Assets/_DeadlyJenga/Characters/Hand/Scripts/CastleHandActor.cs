using FlyingHand;
using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.Core;
using DeadlyJenga.CandyPhysicsHands;
using DeadlyJenga.CandyPlayers;

namespace DeadlyJenga.CastlePrototype
{
    // Prefab boundary: swap this whole actor, or only its HandVisual children.
    public sealed class CastleHandActor : MonoBehaviour, ITeleportable, IFlyingState, IPlayableCharacter
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
        public bool SelectPressed => HandTracking && HandTracking.RightPinchPressed;
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
            follow.SnapToTarget();
        }
    }
}
