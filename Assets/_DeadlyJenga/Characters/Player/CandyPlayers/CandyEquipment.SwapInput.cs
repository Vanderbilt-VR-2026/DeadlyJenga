using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadlyJenga.CandyPlayers
{
    public sealed partial class CandyEquipment
    {
        const float PickupRange = 1.65f;
        const float GripReach = .45f;
        const float AimFocus = .92f;
        const float GrabBuffer = .18f;
        const int PickupOccluders = (1 << Layers.Block) | (1 << Layers.Surface) | 1;
        int pendingPickup;
        float nextPickupRequest;
        float leftGrabUntil, rightGrabUntil;

        public int PendingPickup => pendingPickup;

        void ClearPickupFocus()
        {
            pendingPickup = 0;
            leftGrabUntil = rightGrabUntil = 0;
        }

        public void ConfirmSwap(bool accept)
        {
            if (!accept) { ClearPickupFocus(); return; }
            if (Local && motor.Alive && motor.HasInputFocus && !motor.InputSuspended &&
                !motor.Climbing && !motor.Vaulting && Services.Session?.MenuOpen != true)
                TakeFocusedPickup();
        }

        bool TakeFocusedPickup()
        {
            if (pendingPickup == 0 || Time.unscaledTime < nextPickupRequest) return false;
            nextPickupRequest = Time.unscaledTime + .28f;
            // An explicit pickup also authorizes replacing the item in the single slot.
            if (Runtime?.RequestPickup(pendingPickup, true) != true) return false;
            ClearPickupFocus();
            return true;
        }

        bool UpdatePickupFocus(EquipmentState current)
        {
            int previousFocus = pendingPickup;
            pendingPickup = 0;
            var frame = MatchRuntime.Current?.Frame;
            if (frame == null || frame.phase != RoundPhase.Playing || !avatar.viewCamera)
            {
                ClearPickupFocus();
                return false;
            }

            var eye = avatar.viewCamera.transform;
            bool vr = motor.IsVR;
            bool fireHeld = vr && (trigger?.IsPressed() == true || offTrigger?.IsPressed() == true);
            var leftPoint = PickupHand(true, vr, fireHeld, ref leftGrabUntil);
            var rightPoint = PickupHand(false, vr, fireHeld, ref rightGrabUntil);
            float now = Time.unscaledTime;
            bool leftGrab = leftGrabUntil > now;
            bool rightGrab = rightGrabUntil > now;
            bool slotFree = current.kind == EquipmentKind.None || current.builtIn && current.activePower == EquipmentKind.None;
            EquipmentPickup focused = null, touchingPower = null;
            float best = float.NegativeInfinity;
            float powerDistance = .70f * .70f;
            int bestPriority = -1;
            foreach (var pickup in frame.pickups)
            {
                if (pickup == null || pickup.availableIn > 0) continue;
                float feetDistance = (transform.position - pickup.position).sqrMagnitude;
                if (feetDistance > PickupRange * PickupRange) continue;
                var delta = pickup.position - eye.position;
                float depth = delta.magnitude;
                float alignment = depth > .01f ? Vector3.Dot(eye.forward, delta / depth) : 1;
                bool autoPower = slotFree && feetDistance < powerDistance &&
                    (pickup.kind == EquipmentKind.Speed || pickup.kind == EquipmentKind.Jump);
                float leftDistance = leftPoint.HasValue ? (leftPoint.Value - pickup.position).sqrMagnitude : float.PositiveInfinity;
                float rightDistance = rightPoint.HasValue ? (rightPoint.Value - pickup.position).sqrMagnitude : float.PositiveInfinity;
                float handDistance = Mathf.Min(leftDistance, rightDistance);
                float grabDistance = Mathf.Min(leftGrab ? leftDistance : float.PositiveInfinity,
                    rightGrab ? rightDistance : float.PositiveInfinity);
                bool reachable = handDistance <= GripReach * GripReach;
                bool grabbing = grabDistance <= GripReach * GripReach;
                if (!autoPower && !reachable && alignment < AimFocus) continue;
                if (depth > .01f && Physics.Raycast(eye.position, delta / depth, depth * .92f,
                    PickupOccluders, QueryTriggerInteraction.Ignore)) continue;
                if (autoPower)
                {
                    touchingPower = pickup;
                    powerDistance = feetDistance;
                    continue;
                }

                // A squeeze chooses the closest item to that hand, even when looking elsewhere.
                int priority = grabbing ? 2 : reachable ? 1 : 0;
                float score = grabbing ? -grabDistance : reachable ? -handDistance :
                    alignment - depth * .02f + (pickup.id == previousFocus ? .011f : 0);
                if (priority < bestPriority || priority == bestPriority && score <= best) continue;
                focused = pickup;
                best = score;
                bestPriority = priority;
            }

            if (focused != null)
            {
                pendingPickup = focused.id;
                bool activate = vr ? bestPriority == 2 : Keyboard.current?.eKey.wasPressedThisFrame == true;
                if (activate) return TakeFocusedPickup();
            }
            else if (touchingPower != null && Time.unscaledTime >= nextPickupRequest)
            {
                nextPickupRequest = Time.unscaledTime + .45f;
                Runtime?.RequestPickup(touchingPower.id, false);
            }
            return false;
        }

        Vector3? PickupHand(bool left, bool vr, bool fireHeld, ref float grabUntil)
        {
            var tracking = avatar.tracking;
            var hand = vr && tracking ? tracking.WorldHand(left) : default;
            bool controller = vr && HybridXRInput.ControllerTracked(left);
            if (!hand.tracked || !controller && !hand.skeletonTracked)
            {
                grabUntil = 0;
                return null;
            }

            bool held = controller ? HybridXRInput.GripHeld(left) :
                left ? tracking.LeftPinching : tracking.RightPinching;
            bool pressed = controller ? HybridXRInput.GripPressed(left) :
                left ? tracking.LeftPinchPressed : tracking.RightPinchPressed;
            if (!held || fireHeld) grabUntil = 0;
            else if (pressed) grabUntil = Time.unscaledTime + GrabBuffer;

            return !controller && tracking.TryIndexTip(left, out var tip) ? tip : hand.position;
        }
    }
}
