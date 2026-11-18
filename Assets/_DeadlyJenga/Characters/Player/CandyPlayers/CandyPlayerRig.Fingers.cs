using UnityEngine;

namespace DeadlyJenga.CandyPlayers
{
    public sealed partial class CandyPlayerRig
    {
        readonly CandyHandSkeleton[] fingerPoses = new CandyHandSkeleton[2];
        readonly Transform[] fingerPalms = new Transform[2];
        readonly float[] fingerScales = new float[2];
        readonly bool[] fingerPoseReady = new bool[2];
        static readonly Unity.Profiling.ProfilerMarker FingerPoseMarker = new("DJ.Avatar.Fingers");

        void PoseFingers(int side, CandyHandPose hand, float scale)
        {
            using var measurement = FingerPoseMarker.Auto();
            var palm = bones[side == 0 ? 4 : 7];
            if (fingerPalms[side] != palm)
            {
                for (int i = 0; i < 15; i++) bones[14 + side * 15 + i].SetParent(palm, true);
                fingerPalms[side] = palm;
                fingerPoseReady[side] = false;
            }
            var skeleton = hand.skeletonTracked ? hand.skeleton : CandyHandSkeleton.Rest(side == 0, .12f);
            if (fingerPoseReady[side] && fingerScales[side] == scale && fingerPoses[side].SamePose(skeleton)) return;
            fingerScales[side] = scale;
            fingerPoses[side] = skeleton;
            fingerPoseReady[side] = true;
            for (int finger = 0; finger < 5; finger++)
            for (int joint = 0; joint < 3; joint++)
            {
                int index = 14 + side * 15 + finger * 3 + joint;
                Vector3 start = hand.position + hand.rotation * (skeleton.Joint(finger * 4 + joint) * scale);
                Vector3 end = hand.position + hand.rotation * (skeleton.Joint(finger * 4 + joint + 1) * scale);
                // Each bone only bends from its rest pose and keeps its parent's roll (see FingerRotation). Taking roll from
                // the palm direction flips it half a turn once a finger curls past 90 degrees, which pinches the skin flat.
                Vector3 delta = end - start;
                bones[index].SetPositionAndRotation(start, delta.sqrMagnitude > .0000001f
                    ? hand.rotation * DeadlyJenga.CandyPhysicsHands.CandyPhysicalHand.FingerRotation(skeleton, side == 0, finger, joint)
                    : transform.rotation);
                float rigScale = Mathf.Max(.001f, transform.lossyScale.y);
                bones[index].localScale = new Vector3(1, Mathf.Max(.001f, delta.magnitude) / (Mathf.Max(.001f, lengths[index]) * rigScale), 1);
                // Degenerate tracked joints use the body orientation, which can change independently.
                if ((end - start).sqrMagnitude <= .0000001f) fingerPoseReady[side] = false;
            }
        }
    }
}
