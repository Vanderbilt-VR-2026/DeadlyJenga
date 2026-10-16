using FlyingHand;
using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    // Prefab boundary: swap this whole actor, or only its HandVisual children.
    public sealed class CastleHandActor : MonoBehaviour, ITeleportable
    {
        public FlyingHandController motor;
        public ThirdPersonVRFollow follow;
        public Camera View => follow.viewCamera;
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
