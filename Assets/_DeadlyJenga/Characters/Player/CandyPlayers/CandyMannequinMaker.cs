using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CandyPlayers
{
    // Builds the figures behind Mannequins.Create: the real player rig in the requested outfit,
    // posed once and left still. No colliders, no networking, no input.
    sealed class CandyMannequinMaker : IMannequinMaker
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => Mannequins.Maker = new CandyMannequinMaker();

        public GameObject Create(Transform parent, Vector3 feet, Quaternion facing, AvatarAppearance look, int color, MannequinPose pose)
        {
            var prefab = Resources.Load<CandyPlayerRig>("CandyPlayerVisual");
            if (!prefab) return null;
            var rig = Object.Instantiate(prefab, parent);
            rig.name = pose == MannequinPose.Hanging ? "Hanging display avatar" : "Display mannequin";
            rig.ownerCamera = null;
            if (rig.style) rig.SetColor(color);
            rig.SetAppearance(CandyPlayerAvatar.SanitizeAppearance(look));

            var frame = new CandyAvatarFrame { feet = feet, scale = 1 };
            if (pose == MannequinPose.Hanging)
            {
                // Limp, held up only by a stick through the middle of the head: the body hangs straight
                // below it, arms fall straight down from the shoulders, feet dangle off the floor.
                frame.head = feet + Vector3.up * 1.6f;
                frame.headRotation = facing;
                frame.left = Hand(feet + facing * new Vector3(-.21f, .5f, 0), facing * Quaternion.Euler(0, 0, 2), true, .35f);
                frame.right = Hand(feet + facing * new Vector3(.21f, .5f, 0), facing * Quaternion.Euler(0, 0, -2), false, .35f);
            }
            else
            {
                // On display: standing tall, one hand on the hip, the other relaxed.
                frame.head = feet + Vector3.up * 1.6f;
                frame.headRotation = facing * Quaternion.Euler(-4, 0, 0);
                frame.left = Hand(feet + facing * new Vector3(-.3f, 1.02f, -.02f), facing * Quaternion.Euler(0, 40, 70), true, .55f);
                frame.right = Hand(feet + facing * new Vector3(.36f, .98f, .1f), facing * Quaternion.Euler(-10, 0, -12), false, .15f);
            }
            // A hanging body is "in the air", so its feet don't reach for the floor.
            rig.ApplyFrame(frame, pose != MannequinPose.Hanging, 0);
            if (pose == MannequinPose.Display) Chrome(rig);
            else Dangle(rig, feet, facing, (color % 3 - 1) * 4f);
            return rig.gameObject;
        }

        static Material chrome;

        // Display mannequins get a polished chrome skin; their clothes keep their colors.
        static void Chrome(CandyPlayerRig rig)
        {
            if (!chrome)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (!shader) return;
                chrome = new Material(shader) { name = "Mannequin chrome", enableInstancing = true };
                chrome.SetColor("_BaseColor", new Color(.82f, .85f, .89f));
                chrome.SetFloat("_Metallic", 1f);
                chrome.SetFloat("_Smoothness", .94f);
            }
            foreach (var skin in new Renderer[] { rig.bodyRenderer, rig.headRenderer })
            {
                if (!skin) continue;
                skin.SetPropertyBlock(null);
                skin.sharedMaterial = chrome;
            }
        }

        // Gravity on a body hung by the head: the head tips a little on its stick, the toes droop, and
        // the whole body settles so the middle of the head sits straight above the given point.
        static void Dangle(CandyPlayerRig rig, Vector3 feet, Quaternion facing, float tip)
        {
            var head = rig.HeadAnchor;
            if (!head) return;
            head.rotation = facing * Quaternion.Euler(6, 0, tip) * Quaternion.Inverse(facing) * head.rotation;
            var right = facing * Vector3.right;
            var forward = facing * Vector3.forward;
            // Nothing holds the legs up, so each one hangs straight down from the hip and the toes droop.
            if (rig.BoneCount > 13)
                foreach (int thigh in new[] { 8, 11 })
                {
                    Transform upper = rig.AppearanceBone(thigh), lower = rig.AppearanceBone(thigh + 1), foot = rig.AppearanceBone(thigh + 2);
                    var straight = Quaternion.LookRotation(forward, Vector3.down);
                    Vector3 hip = upper.position;
                    upper.rotation = straight;
                    lower.SetPositionAndRotation(hip + Vector3.down * .31f, straight);
                    foot.position = hip + Vector3.down * .62f;
                    foot.rotation = Quaternion.AngleAxis(45, right) * facing;
                }
            var offset = head.position - feet;
            rig.transform.position -= new Vector3(offset.x, 0, offset.z);
        }

        static CandyHandPose Hand(Vector3 position, Quaternion rotation, bool left, float curl) =>
            new CandyHandPose { position = position, rotation = rotation, skeletonTracked = true, skeleton = CandyHandSkeleton.Rest(left, curl) };
    }
}
