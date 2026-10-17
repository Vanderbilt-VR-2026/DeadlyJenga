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
        void NextNearby();
        void RequestSelected();
        void InviteSelected();
        void AcceptPending();
        void DeclinePending();
        void LeaveSession();
        void DrawMenu();   // desktop menu (IMGUI)
    }

    // Sound controls for menus and the lobby. Presentation provides it as Services.Audio.
    public interface IAudioControls
    {
        float MasterVolume { get; set; }
        float MusicVolume { get; set; }
        void PlayUiClick(Vector3 at);
        void OpenStudio();
    }

    // Marks a full menu/HUD on the session object, so the session skips its plain fallback text.
    public interface ISessionHud { }
}
