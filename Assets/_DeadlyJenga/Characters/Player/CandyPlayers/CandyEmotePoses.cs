using UnityEngine;

namespace DeadlyJenga.CandyPlayers
{
    // Emote body poses for any candy player frame (local player, bots seen over the network, remote players).
    // Same poses the bots use. Facing comes from the head's yaw so it works without a body transform.
    public static class CandyEmotePoses
    {
        public static bool Loops(string emote) => emote is "TinyVictoryDance" or "Breakdance" or "IrishDance";

        public static float Duration(string emote) => emote switch
        {
            "FriendlyWave" => 1.6f,
            "NopeNopeNope" => 1.2f,
            "Breakdance" => 3.2f,
            "IrishDance" => 2.4f,
            _ => 2f
        };

        public static CandyAvatarFrame Pose(CandyAvatarFrame frame, string emote, float t, float duration)
        {
            float weight = Mathf.SmoothStep(0, 1, Mathf.Min(Mathf.Clamp01(t / .2f), Mathf.Clamp01((duration - t) / .25f)));
            if (weight <= 0) return frame;
            var facing = Quaternion.Euler(0, frame.headRotation.eulerAngles.y, 0);
            Vector3 l = new Vector3(-.27f, .83f, .06f), r = new Vector3(.27f, .83f, .06f);
            Vector3 lr = new Vector3(80, 0, 10), rr = new Vector3(80, 0, -10);
            float lc = .12f, rc = .12f, pitch = 0, roll = 0;
            float wave = Mathf.Sin(t * 12), bounce = Mathf.Sin(t * 8);
            switch (emote)
            {
                case "FriendlyWave":
                    r = new Vector3(.34f + .07f * wave, 1.65f, .12f); rr = new Vector3(-70, 0, -15 + wave * 22); pitch = -3; break;
                case "TotallyMeantThat":
                    l = new Vector3(-.28f, 1.45f + .09f * bounce, .15f); r = new Vector3(.28f, 1.45f + .09f * bounce, .15f); lr = rr = new Vector3(-65, 0, 0); lc = rc = .95f; pitch = -7; break;
                case "Phew":
                    r = new Vector3(Mathf.Lerp(.2f, -.1f, Mathf.PingPong(t * .7f, 1)), 1.53f, .22f); rr = new Vector3(0, -60, 70); pitch = 8; break;
                case "WhatWasThat":
                    l = new Vector3(-.43f, 1.16f, .18f); r = new Vector3(.43f, 1.16f, .18f); lr = new Vector3(-10, -35, -25); rr = new Vector3(-10, 35, 25); roll = 9 * Mathf.Sin(t * 3); break;
                case "GolfClap":
                    float gap = .035f + .055f * (.5f + .5f * Mathf.Cos(t * 13)); l = new Vector3(-gap, 1.12f, .32f); r = new Vector3(gap, 1.12f, .32f);
                    lr = new Vector3(-15, 60, -70); rr = new Vector3(-15, -60, 70); pitch = 4 * Mathf.Sin(t * 6); break;
                case "JustChilling":
                    l = new Vector3(-.31f, 1.73f, .02f); r = new Vector3(.31f, 1.73f, .02f); lr = new Vector3(-80, -15, 0); rr = new Vector3(-80, 15, 0); pitch = -10; lc = rc = .25f; break;
                case "NopeNopeNope":
                    l = new Vector3(-.4f, 1.27f + bounce * .12f, .08f); r = new Vector3(.4f, 1.27f - bounce * .12f, .08f); lr = new Vector3(-35, 0, -35); rr = new Vector3(-35, 0, 35); roll = wave * 7; break;
                case "Breakdance":
                    float sweep = t * (2 * Mathf.PI / 1.6f);
                    float side = Mathf.Sin(sweep);
                    float sweepHeight = Mathf.Cos(sweep);
                    frame.danceWeight = weight;
                    frame.danceTorsoEuler = new Vector3(48 + 10 * sweepHeight, 22 * side, -14);
                    frame.danceLeftFoot = new Vector3(-.32f + .37f * side, .085f + .23f * Mathf.Max(0, sweepHeight), -.21f + .32f * sweepHeight);
                    frame.danceRightFoot = new Vector3(.31f + .37f * side, .085f + .23f * Mathf.Max(0, -sweepHeight), -.21f - .32f * sweepHeight);
                    frame.head = Vector3.Lerp(frame.head, frame.feet + facing * (new Vector3(-.11f, .85f, .34f) * frame.scale), weight);
                    l = new Vector3(-.41f, .13f, .41f);
                    r = new Vector3(.52f, .57f + .13f * side, .42f);
                    lr = new Vector3(75, 0, 0); rr = new Vector3(15, 45, -50);
                    pitch = 12; roll = -12;
                    break;
                case "IrishDance":
                    float beat = t * (2 * Mathf.PI / .6f);
                    float leftLift = Mathf.Max(0, Mathf.Sin(beat));
                    float rightLift = Mathf.Max(0, -Mathf.Sin(beat));
                    frame.danceWeight = weight;
                    frame.danceLeftFoot = new Vector3(-.10f + leftLift * .16f, .075f + leftLift * .27f, .05f + leftLift * .25f);
                    frame.danceRightFoot = new Vector3(.10f - rightLift * .16f, .075f + rightLift * .27f, .05f + rightLift * .25f);
                    frame.head = Vector3.Lerp(frame.head, frame.feet + Vector3.up * ((1.59f + .055f * Mathf.Abs(Mathf.Sin(beat))) * frame.scale), weight);
                    l = new Vector3(-.24f, .79f, .01f); r = new Vector3(.24f, .79f, .01f);
                    break;
                default:
                    l = new Vector3(-.35f, 1.12f + bounce * .15f, .15f); r = new Vector3(.35f, 1.12f - bounce * .15f, .15f); lr = new Vector3(-30, 0, -20); rr = new Vector3(-30, 0, 20); roll = bounce * 8; lc = rc = .7f; break;
            }
            if (emote == "TinyVictoryDance")
            {
                frame.danceWeight = weight;
                frame.danceLeftFoot = new Vector3(-.13f, .075f + Mathf.Max(0, bounce) * .12f, .03f);
                frame.danceRightFoot = new Vector3(.13f, .075f + Mathf.Max(0, -bounce) * .12f, .03f);
            }
            frame.left = Hand(frame.left, l, lr, lc, true, frame, facing, weight);
            frame.right = Hand(frame.right, r, rr, rc, false, frame, facing, weight);
            frame.headRotation = Quaternion.Slerp(frame.headRotation, frame.headRotation * Quaternion.Euler(pitch, 0, roll), weight);
            return frame;
        }

        static CandyHandPose Hand(CandyHandPose hand, Vector3 local, Vector3 euler, float curl, bool left, CandyAvatarFrame frame, Quaternion facing, float weight)
        {
            hand.position = Vector3.Lerp(hand.position, frame.feet + facing * (local * frame.scale), weight);
            hand.rotation = Quaternion.Slerp(hand.rotation, facing * Quaternion.Euler(euler), weight);
            var skeleton = Flex(left, curl);
            var previous = hand.skeletonTracked ? hand.skeleton : CandyHandSkeleton.Rest(left, .12f);
            for (int i = 0; i < 20; i++) previous.SetJoint(i, Vector3.Lerp(previous.Joint(i), skeleton.Joint(i), weight));
            hand.skeleton = previous; hand.skeletonTracked = true; hand.tracked = true;
            return hand;
        }

        static CandyHandSkeleton Flex(bool left, float curl)
        {
            var rest = CandyHandSkeleton.Rest(left); var result = rest;
            for (int f = 0; f < 5; f++)
            {
                Vector3 p = rest.Joint(f * 4);
                for (int j = 0; j < 3; j++)
                {
                    var rotation = f == 0 ? Quaternion.AngleAxis((left ? -1 : 1) * curl * (j + 1) * 15, Vector3.up) : Quaternion.AngleAxis(curl * (j + 1) * 32, Vector3.right);
                    p += rotation * (rest.Joint(f * 4 + j + 1) - rest.Joint(f * 4 + j)); result.SetJoint(f * 4 + j + 1, p);
                }
            }
            return result;
        }
    }
}
