namespace DeadlyJenga.Core
{
    public interface ILobbyMatchVisitControls
    {
        bool CanVisitLobby { get; }
        bool VisitingLobby { get; }
        void VisitLobby();
        void ReturnToMatch();
    }
}
