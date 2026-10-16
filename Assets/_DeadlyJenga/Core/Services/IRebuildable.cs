namespace DeadlyJenga.Core
{
    // A map that can be rebuilt from scratch (the "reset castle" button). The active map implements it.
    public interface IRebuildable
    {
        void Rebuild();
    }
}
