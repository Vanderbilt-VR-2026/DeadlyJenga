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
        // Online room (island lobby connection); RoomInfo is "" when not in one.
        string RoomInfo { get; }            // "Room: US-ABC234 · 3 players"
        string DiscoveryStatus { get; }     // nearby-room search status on this Wi-Fi
        bool CanToggleSharedControls { get; }
        bool SharedControls { get; }        // demo controls for everyone, not just the host
        void ToggleSharedControls();
        bool CanInviteNearby { get; }
        void InviteNearby();
        string InvitationFrom { get; }      // name of a nearby room inviting this player, "" when none
        void DismissInvitation();
        void NextNearby();
        void RequestSelected();
        void InviteSelected();
        void AcceptPending();
        void DeclinePending();
        void LeaveSession();
    }
}
