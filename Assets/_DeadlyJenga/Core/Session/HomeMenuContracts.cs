namespace DeadlyJenga.Core
{
    public interface IHomeMenuControls
    {
        bool Busy { get; }
        string Status { get; }
        string Region { get; }
        string Code { get; set; }
        bool CodeValid { get; }
        int NearbyCount { get; }
        string NearbyTitle(int index);
        void DetectNearby();
        void NextRegion();
        void JoinCode();
        void JoinNearby(int index);
        void Host();
        void RandomLobby();
        void Demo();
    }
}
