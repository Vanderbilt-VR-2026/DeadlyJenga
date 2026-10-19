using UnityEngine;
using DeadlyJenga.CandyPlayers;

namespace DeadlyJenga.CandyPhysicsHands
{
    public enum DesktopHandPose { Pointer, Cup, Fist, Flat, Peace, Spread }

    public static class DesktopHandPoses
    {
        public static CandyHandSkeleton Build(bool left, DesktopHandPose pose, float clench = 0)
        {
            var rest = CandyHandSkeleton.Rest(left);
            var result = rest;
            float sign = left ? 1 : -1;
            for (int f = 0; f < 5; f++)
            {
                float curl = pose switch
                {
                    DesktopHandPose.Pointer => f == 1 ? 0 : 1,
                    DesktopHandPose.Cup => f == 0 ? .1f : .5f,
                    DesktopHandPose.Fist => 1,
                    DesktopHandPose.Peace => f == 1 || f == 2 ? 0 : 1,
                    _ => 0
                };
                curl = Mathf.Lerp(curl, 1, Mathf.Clamp01(clench));
                Vector3 at = rest.Joint(f * 4);
                for (int j = 0; j < 3; j++)
                {
                    Vector3 bone = rest.Joint(f * 4 + j + 1) - rest.Joint(f * 4 + j);
                    Quaternion bend = f == 0
                        ? Quaternion.AngleAxis(sign * curl * (j + 1) * 25, Vector3.forward)
                        : Quaternion.AngleAxis(curl * (j + 1) * 32, Vector3.right);
                    float fan = pose == DesktopHandPose.Spread ? (f - 2.3f) * 10 : pose == DesktopHandPose.Peace ? (f == 1 ? -8 : f == 2 ? 8 : 0) : 0;
                    if (f > 0) bend = Quaternion.AngleAxis(sign * fan * (1 - clench), Vector3.up) * bend;
                    if (pose == DesktopHandPose.Cup && f == 0) bend = Quaternion.AngleAxis(sign * 65 * (1 - clench), Vector3.forward) * bend;
                    at += bend * bone;
                    result.SetJoint(f * 4 + j + 1, at);
                }
            }
            return result;
        }

        public static CandyHandSkeleton Blend(CandyHandSkeleton from, CandyHandSkeleton to, float t)
        {
            var result = to;
            for (int f = 0; f < 5; f++)
            {
                Vector3 at = Vector3.Lerp(from.Joint(f * 4), to.Joint(f * 4), t);
                result.SetJoint(f * 4, at);
                for (int j = 0; j < 3; j++)
                {
                    Vector3 a = from.Joint(f * 4 + j + 1) - from.Joint(f * 4 + j);
                    Vector3 b = to.Joint(f * 4 + j + 1) - to.Joint(f * 4 + j);
                    at += Vector3.Slerp(a, b, t);
                    result.SetJoint(f * 4 + j + 1, at);
                }
            }
            return result;
        }
    }
}
