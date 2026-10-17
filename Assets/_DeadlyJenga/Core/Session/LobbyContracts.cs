using UnityEngine;

namespace DeadlyJenga.Core
{
    // The lobby's connection controls. GameSession provides it as Services.Lobby while the Lobby scene is open;
    // the lobby menus in Presentation call it.
    public interface ILobbyControls
    {
        string RoomLabel { get; }          // "Room Code: ABCD", the countdown, or "Room Code: —"
        bool Connected { get; }            // in a room
        bool Connecting { get; }           // hosting or joining is in progress
        bool CanEdit { get; }              // not in a room, or this player is the host
        string Status { get; }             // last connection message, "" when none
        bool StatusIsError { get; }
        void ShowMessage(string text, bool error);

        // The shared lobby screen: the host publishes its page and placement, everyone else follows.
        bool HasSharedScreen { get; }      // connected and following the host
        int SharedPage { get; }
        Pose SharedScreenPose { get; }
        void PublishScreen(int page, Pose pose);   // ignored unless CanEdit

        void Host();
        void Join(string code);
        void Leave();                      // leave or close the room
        void StartMatch();                 // host: start the countdown to the game
        void CancelMatch();                // host: stop the countdown
        bool TryStartSolo(out string problem);
    }

    // Choices made in the lobby menus, read by the session when the game starts.
    // Kept here so the menus (Presentation) and the session (GameSession) share them without knowing each other.
    public static class MatchChoices
    {
        public static MatchRules Rules = new MatchRules();
        public static int BotTarget = 1;       // population, including the local player
        public static int HandCount;           // AI hands
        public static int SurvivorPreset, HandPreset;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Rules = new MatchRules(); BotTarget = 1; HandCount = 0; SurvivorPreset = 0; HandPreset = 0; }
    }
}
