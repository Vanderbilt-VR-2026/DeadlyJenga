namespace DeadlyJenga.Core
{
    // Choices made in the lobby menus, read by the session when the game starts.
    // Kept here so the menus (Presentation) and the session (GameSession) share them without knowing each other.
    public static class MatchChoices
    {
        public static MatchRules Rules = new MatchRules();
        public static string ModeId = "demo", MapId = "castle";   // ids from the lobby catalog
        public static int BotTarget = 10;      // population, including the local player
        public static int HandCount;           // AI hands
        public static int SurvivorPreset, HandPreset;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Rules = new MatchRules(); ModeId = "demo"; MapId = "castle"; BotTarget = 10; HandCount = 0; SurvivorPreset = 0; HandPreset = 0; }
    }
}
