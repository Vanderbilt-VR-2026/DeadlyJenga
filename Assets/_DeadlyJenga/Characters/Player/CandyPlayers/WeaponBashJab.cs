using UnityEngine;

namespace DeadlyJenga.CandyPlayers
{
    internal sealed class WeaponBashJab
    {
        bool sampled, armed, leftHand;
        int device;
        float trackedAt, lastAt, quietAt = -1, strokeAt = -1;
        Vector3 previousHand, previousRelative, previousForward;
        Vector3 restHand, restRelative, restForward;

        public bool Stable { get; private set; }

        public void Reset()
        {
            sampled = Stable = false;
            Consume();
        }

        public void Consume()
        {
            armed = false;
            quietAt = strokeAt = -1;
        }

        public bool Sample(int deviceId, bool left, Vector3 hand, Vector3 head, Vector3 forward,
            float scale, float now, float dt, out Vector3 direction, out float speed)
        {
            direction = Vector3.zero;
            speed = 0;
            if (!Finite(hand) || !Finite(head) || !Finite(forward) || !Finite(scale) ||
                !Finite(now) || !Finite(dt) || scale < .1f || dt < .001f || dt > .1f ||
                !Finite(forward.sqrMagnitude) || forward.sqrMagnitude < .01f)
            {
                Reset();
                return false;
            }

            forward = forward.normalized;
            var relative = hand - head;
            if (!sampled || device != deviceId || leftHand != left || now <= lastAt || now - lastAt > .1001f)
            {
                Reset();
                sampled = true;
                device = deviceId;
                leftHand = left;
                trackedAt = lastAt = now;
                previousHand = hand;
                previousRelative = relative;
                previousForward = forward;
                return false;
            }

            var travel = (hand - previousHand) / scale;
            var relativeTravel = (relative - previousRelative) / scale;
            float worldSpeed = travel.magnitude / dt;
            float relativeSpeed = relativeTravel.magnitude / dt;
            bool turning = Vector3.Dot(previousForward, forward) < .98f ||
                armed && Vector3.Dot(restForward, forward) < .95f;
            previousHand = hand;
            previousRelative = relative;
            previousForward = forward;
            lastAt = now;

            if (travel.magnitude > .22f || relativeTravel.magnitude > .22f ||
                worldSpeed > 8 || relativeSpeed > 8)
            {
                Reset();
                return false;
            }

            Stable = now - trackedAt >= .25f;
            if (!Stable || turning)
            {
                Consume();
                return false;
            }

            if (worldSpeed < .4f && relativeSpeed < .4f)
            {
                if (quietAt < 0) quietAt = now;
                if (now - quietAt >= .16f) armed = true;
                restHand = hand;
                restRelative = relative;
                restForward = forward;
                strokeAt = -1;
                return false;
            }

            quietAt = -1;
            if (!armed) return false;
            if (strokeAt < 0) strokeAt = now;
            if (now - strokeAt > .4f)
            {
                Consume();
                return false;
            }

            float worldForward = Vector3.Dot((hand - restHand) / scale, restForward);
            float relativeForward = Vector3.Dot((relative - restRelative) / scale, restForward);
            float forwardSpeed = Vector3.Dot(travel, restForward) / dt;
            if (worldForward <= .15f || relativeForward <= .15f || forwardSpeed < 1.8f) return false;

            direction = travel.normalized;
            speed = forwardSpeed;
            Consume();
            return true;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    }
}
