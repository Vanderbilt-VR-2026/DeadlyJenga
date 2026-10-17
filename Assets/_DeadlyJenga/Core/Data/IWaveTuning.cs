namespace DeadlyJenga.Core
{
    public interface IWaveTuning
    {
        // Configure an inactive instance before Begin. Speed is m/s; force is a multiplier.
        void Configure(float speed, float forceMultiplier);
    }
}
