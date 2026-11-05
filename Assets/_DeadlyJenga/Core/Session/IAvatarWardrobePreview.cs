namespace DeadlyJenga.Core
{
    public struct AvatarWardrobeResolution
    {
        public AvatarAppearance effective;
        public AvatarAccessory hiddenAccessories;
        public AvatarClothing hiddenClothing;
        public bool hiddenHair;
        public string reason;
    }

    // Optional read-only assessment of a proposed wardrobe choice.
    public interface IAvatarWardrobePreview
    {
        AvatarWardrobeResolution EvaluateAppearance(AvatarAppearance requested);
    }
}
