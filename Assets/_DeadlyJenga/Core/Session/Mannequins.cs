using UnityEngine;

namespace DeadlyJenga.Core
{
    // How a display figure stands: upright on a stand, hanging limp from a rail, or just a head
    // (with what is worn on it). For Head, "feet" is the point the bottom of the head rests on.
    public enum MannequinPose { Display, Hanging, Head }

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
