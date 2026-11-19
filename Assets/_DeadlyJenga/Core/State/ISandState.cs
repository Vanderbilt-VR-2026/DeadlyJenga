namespace DeadlyJenga.Core
{
    // A character caught in sinking sand, read-only. Looks and sound read this
    // (for example a sand coating after escaping) instead of the character's own scripts.
    public interface ISandState
    {
        bool InSand { get; }
        float SandDepth { get; }    // 0..1, how far the character has sunk
    }
}
