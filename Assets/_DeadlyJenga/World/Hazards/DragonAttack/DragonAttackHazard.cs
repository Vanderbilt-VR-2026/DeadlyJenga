namespace DeadlyJenga.World.Hazards
{
    public sealed class DragonAttackHazard : ShrineDragonHazard
    {
        protected override bool Airborne => false;
    }
}
