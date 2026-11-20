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
                // Switched off: legs straight, head dropped onto the chest (tipped a little to one side),
                // arms dangling straight down, fingers loose.
                // The body is posed standing tall first; the head is then bent down at the neck below.
                frame.head = feet + Vector3.up * 1.6f;
                frame.headRotation = facing;
                frame.left = Hand(feet + facing * new Vector3(-.22f, .6f, -.02f), facing * Quaternion.Euler(0, 0, 4), true, .4f);
                frame.right = Hand(feet + facing * new Vector3(.22f, .6f, -.02f), facing * Quaternion.Euler(0, 0, -4), false, .4f);
            }
            else
            {
                // On display: standing tall, one hand on the hip, the other relaxed.
                frame.head = feet + Vector3.up * 1.6f;
                frame.headRotation = facing * Quaternion.Euler(-4, 0, 0);
                frame.left = Hand(feet + facing * new Vector3(-.3f, 1.02f, -.02f), facing * Quaternion.Euler(0, 40, 70), true, .55f);
                frame.right = Hand(feet + facing * new Vector3(.36f, .98f, .1f), facing * Quaternion.Euler(-10, 0, -12), false, .15f);
            }
            rig.ApplyFrame(frame, true, 0);
            if (pose == MannequinPose.Hanging) DropHead(rig.HeadAnchor, facing, (color % 3 - 1) * 14f);
            return rig.gameObject;
        }

        // Swings the head forward around the neck, so it stays attached to the body.
        static void DropHead(Transform head, Quaternion facing, float tip)
        {
            if (!head) return;
            Vector3 neck = head.position + Vector3.down * .15f;
            Quaternion bend = facing * Quaternion.Euler(70, 0, tip) * Quaternion.Inverse(facing);
            head.SetPositionAndRotation(neck + bend * (head.position - neck), bend * head.rotation);
        }

        static CandyHandPose Hand(Vector3 position, Quaternion rotation, bool left, float curl) =>
            new CandyHandPose { position = position, rotation = rotation, skeletonTracked = true, skeleton = CandyHandSkeleton.Rest(left, curl) };
    }
}
