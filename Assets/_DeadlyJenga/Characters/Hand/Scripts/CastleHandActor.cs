using FlyingHand;
using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.Core;
using DeadlyJenga.CandyPhysicsHands;

namespace DeadlyJenga.CastlePrototype
{
    // Prefab boundary: swap this whole actor, or only its HandVisual children.
    public sealed class CastleHandActor : MonoBehaviour, ITeleportable, IFlyingState
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
