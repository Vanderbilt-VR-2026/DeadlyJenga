using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CandyPlayers
{
    public sealed partial class CandyPlayerAvatar : IAvatarShowcase
    {
        bool showcasing;
        float showcaseStarted, showcaseHeading;
        public bool Showcasing
        {
            get => showcasing;
            set
            {
                if (showcasing == value) return;
                showcasing = value;
                if (!value) return;
                showcaseStarted = Time.unscaledTime;
                showcaseHeading = viewCamera ? viewCamera.transform.eulerAngles.y : transform.eulerAngles.y;
            }
        }
        CandyAvatarFrame ReadShowcaseFrame()
        {
            float scale = Mathf.Max(.1f, survivor ? survivor.playerScale : 1);
            var facing = Quaternion.Euler(0, showcaseHeading, 0);
            var feet = transform.position;
            if(viewCamera) feet = new Vector3(viewCamera.transform.position.x, feet.y, viewCamera.transform.position.z);
            var frame = new CandyAvatarFrame { feet = feet, head = feet + Vector3.up * 1.6f * scale, headRotation = facing, scale = scale };
            float age = Time.unscaledTime - showcaseStarted;
            // A brief wave every six seconds, with a full relaxed pause between gestures.
            float gesture = age % 6 - 3.5f;
            float wave = gesture >= 0 && gesture < 1.8f ? Mathf.Sin(Mathf.PI * gesture / 1.8f) : 0;
            CandyHandPose Hand(bool left)
            {
                float sign = left ? -1 : 1;
                Vector3 rest = new Vector3(sign * .29f, .77f, .025f);
                Vector3 raised = new Vector3(.43f + Mathf.Sin(gesture * 12) * .075f, 1.67f, .08f);
                var position = left ? rest : Vector3.Lerp(rest, raised, wave);
                var rotation = facing * Quaternion.Euler(left ? 0 : -25 * wave, sign * 12, sign * 85 * (1 - wave));
                return new CandyHandPose { tracked = true, skeletonTracked = true, position = feet + facing * position * scale, rotation = rotation, skeleton = CandyHandSkeleton.Rest(left, .04f) };
            }
            frame.left = Hand(true); frame.right = Hand(false);
            return frame;
        }
    }
}
