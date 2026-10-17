namespace DeadlyJenga.Core
{
    public interface IMapHazard : IHazard
    {
        bool TryValidate(IWorldQuery world, out string error);
    }
}
