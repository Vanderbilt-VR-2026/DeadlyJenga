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
}
