namespace DeadlyJenga.Core
{
    // A disaster that game modes can start and stop without knowing what it is.
    public interface IHazard
    {
        HazardType Type { get; }
        bool Active { get; }
        void Begin();
        void End();
    }

    // Optional: a hazard that arrives from a side of the map can be told which side, before Begin().
    // Heading is in degrees around the map centre (0 = +X, 90 = +Z). Null keeps the hazard's own random choice.
    public interface IDirectedHazard
    {
        float? Heading { get; set; }
    }
}
