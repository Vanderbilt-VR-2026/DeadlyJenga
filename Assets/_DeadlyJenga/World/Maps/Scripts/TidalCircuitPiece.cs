using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Seated pieces sleep until a bearing is withdrawn. PhysX handles the remaining contacts.
    public sealed class TidalCircuitPiece : MonoBehaviour, IGrabbable
    {
        internal readonly List<TidalCircuitPiece> Supported = new();
        internal TidalCircuitMap Map;
        internal Rigidbody Body;
        internal bool Loose;
        internal bool Rotor;
        internal HingeJoint Hinge;
        internal Vector3 Rest;
        internal GameObject Holder;
        Vector3 offset, target;
        public float Weight => Body.mass;
        public bool CanGrab(GameObject hand) => hand && !Holder && Map.Authoritative && isActiveAndEnabled;
        public bool TryStartGrab(GameObject hand, Vector3 point)
        {
            if (!CanGrab(hand)) return false;
            Holder = hand;
            offset = Body.position - point;
            target = Body.position;
            Unseat();
            return true;
        }
        public void Hold(Vector3 point) => target = point + offset;
        public void Release() => Holder = null;
        internal void Unseat()
        {
            if (Loose || !Map.Authoritative) return;
            Loose = true;
            if (Hinge) { Hinge.useMotor = false; Destroy(Hinge); Hinge = null; }
            Body.isKinematic = false;
            Body.useGravity = true;
            Body.WakeUp();
            foreach (var child in Supported) child.Unseat();
        }
        void FixedUpdate()
        {
            if (Holder && Map.Authoritative)
                Body.AddForce(Vector3.ClampMagnitude((target - Body.position) * 32 - Body.linearVelocity * 8, 60), ForceMode.Acceleration);
        }
        void OnCollisionEnter(Collision hit)
        {
            if (!Loose && hit.rigidbody && !hit.rigidbody.isKinematic && hit.impulse.magnitude > Body.mass * 8)
                Unseat();
        }
    }
}
