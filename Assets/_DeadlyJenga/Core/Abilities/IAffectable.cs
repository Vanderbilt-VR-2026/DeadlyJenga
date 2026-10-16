namespace DeadlyJenga.Core
{
    // Can be put under a timed effect. Ignore effects you don't understand.
    public interface IAffectable
    {
        void Apply(Effect effect);
        void Remove(EffectType type);
        bool Has(EffectType type);
    }
}
