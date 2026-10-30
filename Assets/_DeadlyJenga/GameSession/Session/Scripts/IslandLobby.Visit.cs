using DeadlyJenga.Core;
namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class IslandLobby : ILobbyMatchVisitControls
    {
        internal CastlePlaySession VisitOwner;
        public bool CanVisitLobby=>false;
        public bool VisitingLobby=>VisitOwner;
        public void VisitLobby() { }
        public void ReturnToMatch(){if(VisitOwner)VisitOwner.ReturnToMatch();}
    }
}
