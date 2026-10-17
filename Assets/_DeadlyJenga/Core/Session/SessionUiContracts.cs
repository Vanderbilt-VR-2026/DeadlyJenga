using UnityEngine;

namespace DeadlyJenga.Core
{
    // Multiplayer controls the menus show. GameSession's networking provides it.
    public interface IMultiplayerControls
    {
        string Status { get; }
        bool IsFusion { get; }
        bool IsClient { get; }
        bool CanEditWorld { get; }
        int HumanCount { get; }
        string SelectedName { get; }
        string PendingLabel { get; }
        bool HasPending { get; }
        bool CanLeave { get; }      // in, joining or hosting someone: a leave / end button makes sense
        void NextNearby();
        void RequestSelected();
        void InviteSelected();
        void AcceptPending();
        void DeclinePending();
        void LeaveSession();
    }
}
