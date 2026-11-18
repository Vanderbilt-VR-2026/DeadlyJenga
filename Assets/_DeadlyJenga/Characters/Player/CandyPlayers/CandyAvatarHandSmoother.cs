using UnityEngine;
using DeadlyJenga.CandyPhysicsHands;

namespace DeadlyJenga.CandyPlayers
{
    // Keeps the local VR player's own hands looking like hands whatever tracking does.
    // - Fingers are read as joint angles (CandyHandShape), so impossible shapes cannot be drawn, jitter is smoothed and
    //   no joint moves faster than a finger can.
    // - When the source changes (controller, tracked hand, or nothing) the hand glides to the new place over a quarter
    //   of a second instead of jumping, then follows it exactly again: no lag is added while a source is steady.
    // - A hand that drops out for a moment stays where it was, relative to the head, before fading to the idle pose.
    sealed class CandyAvatarHandSmoother
    {
        public const int None = 0, Controller = 1, TrackedHand = 2;
        const float GlideSeconds = .25f, HoldSeconds = .2f;
        CandyHandShape shape; bool ready; int kind = -1, frame = -1; float glide = 1, lostAt = -1;
        CandyHandPose shown, cached; Vector3 heldPosition; Quaternion heldRotation = Quaternion.identity; CandyHandSkeleton heldSkeleton;

        public void Reset() { ready = false; kind = -1; frame = -1; glide = 1; lostAt = -1; }

        // target: the hand the avatar would show this frame. sourceKind: where it came from.
        public CandyHandPose Step(CandyHandPose target, int sourceKind, bool left, Vector3 head, Quaternion headTurn, float dt)
        {
            // The pose is read more than once a frame (drawing and network); smooth once.
            if (frame == Time.frameCount) return cached;
            frame = Time.frameCount;
            Quaternion fromHead = Quaternion.Inverse(headTurn);
            if (sourceKind != None)
            { lostAt = -1; heldPosition = fromHead * (target.position - head); heldRotation = fromHead * target.rotation; heldSkeleton = target.skeleton; }
            else if (ready && kind != None)
            {
                // Just lost. Keep the hand where it was beside the head for a moment; most losses are only a few frames.
                if (lostAt < 0) lostAt = Time.unscaledTime;
                if (Time.unscaledTime - lostAt < HoldSeconds)
                { target.position = head + headTurn * heldPosition; target.rotation = headTurn * heldRotation; target.skeleton = heldSkeleton; sourceKind = kind; }
            }
            var wanted = CandyHandShape.From(target.skeleton, left);
            bool far = ready && (shown.position - target.position).sqrMagnitude > 9;   // respawn or teleport: do not glide across the map
            if (!ready || far) { shape = wanted; shown = target; ready = true; kind = sourceKind; glide = 1; }
            if (sourceKind != kind) { kind = sourceKind; glide = 0; }
            glide = Mathf.MoveTowards(glide, 1, dt / GlideSeconds);
            shape.Follow(wanted, dt, .03f);
            var result = target; result.skeleton = shape.ToSkeleton(left);
            if (glide < 1)
            {
                float catchUp = Mathf.Lerp(1 - Mathf.Exp(-dt / .05f), 1, glide * glide);
                result.position = Vector3.Lerp(shown.position, target.position, catchUp);
                result.rotation = Quaternion.Slerp(shown.rotation, target.rotation, catchUp);
            }
            shown = result; cached = result;
            return result;
        }
    }
}
