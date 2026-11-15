using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    internal enum GiftPieceRole { Base, Wall, Lid, Rail }

    // Wrapping holds the shell in place until damage reaches a structural piece.
    internal sealed class UnderneathTreeGiftShell
    {
        internal readonly List<UnderneathTreeGiftPanel> Pieces = new List<UnderneathTreeGiftPanel>();

        internal void OnPanelReleased(UnderneathTreeGiftPanel hit, Vector3 impulse)
        {
            if (hit.Role == GiftPieceRole.Base || impulse.magnitude >= 5.6f)
            {
                Vector3 center = hit.transform.parent.position;
                foreach (var piece in Pieces)
                {
                    if (piece == hit || piece.IsLoose)
                        continue;
                    Vector3 outward = Vector3.ProjectOnPlane(piece.Body.worldCenterOfMass - center, Vector3.up).normalized;
                    Vector3 shared = impulse * .16f;
                    if (piece.Role == GiftPieceRole.Wall)
                    {
                        // Tipping the panel outward keeps it from balancing upright on its thin bottom edge.
                        if (outward.sqrMagnitude < .01f)
                            outward = piece.transform.right;
                        piece.Release(shared + outward * 3.4f + Vector3.up * .55f,
                            Vector3.Cross(Vector3.up, outward) * 2.3f);
                    }
                    else if (piece.Role == GiftPieceRole.Lid)
                        piece.Release(shared + piece.transform.parent.forward * 2.28f + Vector3.up * 1.1f,
                            piece.transform.parent.right * .88f);
                    else
                        piece.Release(shared);
                }
                return;
            }

            if (hit.Role != GiftPieceRole.Wall)
                return;
            int walls = 0;
            int held = 0;
            foreach (var piece in Pieces)
            {
                if (piece.Role != GiftPieceRole.Wall)
                    continue;
                walls++;
                if (!piece.IsLoose)
                    held++;
            }
            if (held * 2 > walls)
                return;
            foreach (var piece in Pieces)
                if (piece.Role == GiftPieceRole.Lid)
                    piece.Release(Vector3.zero);
        }
    }

    public sealed class UnderneathTreeGiftPanel : MonoBehaviour, IClimbable, IPushable, IHazardMotion
    {
        public bool CanClimb => isActiveAndEnabled;
        public float Grip => 1f;
        public Transform Anchor => transform;
        public Rigidbody Body { get; private set; }
        public Transform Visual { get; private set; }
        public Vector3 HazardVelocity => Body ? Body.linearVelocity : Vector3.zero;
        public bool IsLoose { get; private set; }

        internal GiftPieceRole Role { get; private set; }
        UnderneathTreeGiftShell shell;
        Vector3 wrappedPosition;
        Quaternion wrappedRotation;
        bool replica;

        internal bool IsAtWrappedPose()
        {
            var parent = transform.parent;
            var localPosition = parent.InverseTransformPoint(Body.position);
            var localRotation = Quaternion.Inverse(parent.rotation) * Body.rotation;
            return (localPosition - wrappedPosition).sqrMagnitude <= .035f * .035f &&
                Quaternion.Angle(localRotation, wrappedRotation) <= .85f;
        }

        internal void SetUp(Rigidbody body, Transform visual, UnderneathTreeGiftShell owner, GiftPieceRole role)
        {
            Body = body;
            Visual = visual;
            shell = owner;
            Role = role;
            wrappedPosition = transform.localPosition;
            wrappedRotation = transform.localRotation;
            shell.Pieces.Add(this);
        }

        public void SetReplica(bool value)
        {
            // Snapshots carry body poses. Pieces displaced by the former host stay loose when authority returns.
            if (replica && !value && gameObject.activeSelf)
                IsLoose |= !IsAtWrappedPose();
            replica = value;
            if (value)
                Body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            Body.isKinematic = value || !IsLoose;
            if (!Body.isKinematic)
                Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        public void Push(Vector3 force, GameObject source)
        {
            if (replica || !Body || !isActiveAndEnabled ||
                (Services.Session is IWorldAuthority authority && !authority.HasWorldAuthority))
                return;
            if (!IsLoose)
            {
                if (force.sqrMagnitude < 1.4f * 1.4f)
                    return;
                Release(force);
                shell.OnPanelReleased(this, force);
            }
            else
                Body.AddForce(force, ForceMode.VelocityChange);
            GameEvents.RaiseBlockHit(transform.position, force.magnitude);
        }

        internal void Release(Vector3 impulse, Vector3 angularVelocity = default)
        {
            if (IsLoose || replica)
                return;
            IsLoose = true;
            SetReplica(false);
            Body.AddForce(impulse, ForceMode.VelocityChange);
            if (angularVelocity.sqrMagnitude > 0f)
                Body.AddTorque(angularVelocity, ForceMode.VelocityChange);
        }

        void OnCollisionEnter(Collision collision)
        {
            if (IsLoose || !collision.rigidbody || collision.rigidbody.isKinematic ||
                collision.contactCount == 0 || collision.relativeVelocity.sqrMagnitude < 16f)
                return;
            if (collision.rigidbody.TryGetComponent<UnderneathTreeGiftPanel>(out var panel) && panel.shell == shell)
                return;
            float speed = Mathf.Min(8f, collision.relativeVelocity.magnitude * .36f);
            Push(collision.GetContact(0).normal * speed, collision.gameObject);
        }
    }
}
