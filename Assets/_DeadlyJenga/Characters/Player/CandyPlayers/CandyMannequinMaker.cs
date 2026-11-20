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
                // Switched off on its hanger stick: legs straight, head level, arms dangling, fingers loose.
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
            if (pose == MannequinPose.Display) Chrome(rig);
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

        static CandyHandPose Hand(Vector3 position, Quaternion rotation, bool left, float curl) =>
            new CandyHandPose { position = position, rotation = rotation, skeletonTracked = true, skeleton = CandyHandSkeleton.Rest(left, curl) };
    }
}
