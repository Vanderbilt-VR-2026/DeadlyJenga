using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadlyJenga.CandyPlayers
{
    public sealed partial class CandyEquipment
    {
        const float PickupRange = 1.65f;
        const float GripReach = .34f;
        const int PickupOccluders = (1 << Layers.Block) | (1 << Layers.Surface) | 1;
        int pendingPickup;
        float nextPickupRequest;

        public int PendingPickup => pendingPickup;

        void ClearPickupFocus() => pendingPickup = 0;

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
            ClearPickupFocus();
            var frame = MatchRuntime.Current?.Frame;
            if (frame == null || frame.phase != RoundPhase.Playing || !avatar.viewCamera) return false;

            var eye = avatar.viewCamera.transform;
            bool vr = motor.IsVR;
            bool leftController = vr && HybridXRInput.ControllerTracked(true);
            bool rightController = vr && HybridXRInput.ControllerTracked(false);
            var tracking = avatar.tracking;
            var leftHand = vr && tracking ? tracking.WorldHand(true) : default;
            var rightHand = vr && tracking ? tracking.WorldHand(false) : default;
            bool leftGrab = leftHand.tracked && (leftController ? HybridXRInput.GripPressed(true) :
                leftHand.skeletonTracked && tracking.LeftPinching && tracking.MenuSelectPressed);
            bool rightGrab = rightHand.tracked && (rightController ? HybridXRInput.GripPressed(false) :
                rightHand.skeletonTracked && tracking.RightPinchPressed);
            bool slotFree = current.kind == EquipmentKind.None || current.builtIn && current.activePower == EquipmentKind.None;
            EquipmentPickup focused = null, touchingPower = null;
            float best = float.NegativeInfinity;
            float powerDistance = .70f * .70f;
            bool gripOnFocus = false;
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
                float leftDistance = leftHand.tracked && (leftController || leftHand.skeletonTracked) ? (leftHand.position - pickup.position).sqrMagnitude : float.PositiveInfinity;
                float rightDistance = rightHand.tracked && (rightController || rightHand.skeletonTracked) ? (rightHand.position - pickup.position).sqrMagnitude : float.PositiveInfinity;
                float handDistance = Mathf.Min(leftDistance, rightDistance);
                bool reachable = vr && handDistance < GripReach * GripReach;
                if (!autoPower && alignment < (reachable ? .83f : .95f)) continue;
                if (depth > .01f && Physics.Raycast(eye.position, delta / depth, depth * .92f,
                    PickupOccluders, QueryTriggerInteraction.Ignore)) continue;
                if (autoPower)
                {
                    touchingPower = pickup;
                    powerDistance = feetDistance;
                    continue;
                }

                float score = alignment * 5 - depth * .10f + (pickup.id == previousFocus ? .055f : 0);
                if (reachable) score += 1.1f - handDistance * 4;
                if (score <= best) continue;
                focused = pickup;
                best = score;
                gripOnFocus = leftGrab && leftDistance < GripReach * GripReach ||
                    rightGrab && rightDistance < GripReach * GripReach;
            }

            if (focused != null)
            {
                pendingPickup = focused.id;
                bool fireHeld = trigger?.IsPressed() == true || offTrigger?.IsPressed() == true;
                bool activate = vr ? gripOnFocus && !fireHeld : Keyboard.current?.eKey.wasPressedThisFrame == true;
                if (activate) return TakeFocusedPickup();
            }
            else if (touchingPower != null && Time.unscaledTime >= nextPickupRequest)
            {
                nextPickupRequest = Time.unscaledTime + .45f;
                Runtime?.RequestPickup(touchingPower.id, false);
            }
            return false;
        }
    }
}
