namespace DeadlyJenga.Core
{
    // The world can be rewound a few seconds. The active world timeline sets Services.Rewind.
    public interface IRewindable
    {
        float Recorded { get; }        // seconds of history available
        bool Rewinding { get; }
        bool Rewind(float seconds);    // false if it can't (not the host, nothing recorded, already rewinding)
    }
}
