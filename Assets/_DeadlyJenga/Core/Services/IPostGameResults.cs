namespace DeadlyJenga.Core
{
    // An optional capability of Services.Session. Null means no results are ready.
    public interface IPostGameResults
    {
        PostGameReport LatestReport { get; }
    }
}
