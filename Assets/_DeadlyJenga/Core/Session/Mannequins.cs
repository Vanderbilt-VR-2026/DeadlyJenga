using UnityEngine;

namespace DeadlyJenga.Core
{
    // How a display figure stands: upright on a stand, or hanging limp from a rail (head down, arms loose).
    public enum MannequinPose { Display, Hanging }

    // Makes a still, non-networked player figure wearing a chosen outfit, for shop and lobby art.
    // Characters provides the maker. Presentation asks for figures without naming avatar classes.
    public interface IMannequinMaker
    {
        GameObject Create(Transform parent, Vector3 feet, Quaternion facing, AvatarAppearance look, int color, MannequinPose pose);
    }

    public static class Mannequins
    {
        public static IMannequinMaker Maker { get; set; }

        // Returns null when no maker is registered (for example, Characters is not in the build).
        public static GameObject Create(Transform parent, Vector3 feet, Quaternion facing, AvatarAppearance look, int color, MannequinPose pose)
            => Maker?.Create(parent, feet, facing, look, color, pose);
    }
}
