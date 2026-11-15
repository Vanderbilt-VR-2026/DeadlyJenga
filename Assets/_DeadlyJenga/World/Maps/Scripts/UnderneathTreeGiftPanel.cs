using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    internal enum GiftPieceRole { Base, Wall, Lid }

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
                    Vector3 outward = Vector3.ProjectOnPlane(piece.Body.position - center, Vector3.up).normalized;
                    // Release the wrapping and give the panels a modest kick away from the shell.
                    piece.Release(impulse * .18f + outward * .65f + Vector3.up * .28f);
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
            {
                var parent = transform.parent;
                var localPosition = parent.InverseTransformPoint(Body.position);
                var localRotation = Quaternion.Inverse(parent.rotation) * Body.rotation;
                IsLoose |= (localPosition - wrappedPosition).sqrMagnitude > .035f * .035f ||
                    Quaternion.Angle(localRotation, wrappedRotation) > .85f;
            }
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

        internal void Release(Vector3 impulse)
        {
            if (IsLoose || replica)
                return;
            IsLoose = true;
            SetReplica(false);
            Body.AddForce(impulse, ForceMode.VelocityChange);
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
