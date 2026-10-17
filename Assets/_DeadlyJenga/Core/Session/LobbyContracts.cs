namespace DeadlyJenga.Core
{
    // The lobby's connection controls. GameSession provides it as Services.Lobby while the Lobby scene is open;
    // the lobby menus in Presentation call it.
    public interface ILobbyControls
    {
        string RoomLabel { get; }          // "Room Code: ABCD", the countdown, or "Room Code: —"
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
