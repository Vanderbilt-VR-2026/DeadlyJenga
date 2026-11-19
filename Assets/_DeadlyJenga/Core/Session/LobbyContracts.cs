using UnityEngine;

namespace DeadlyJenga.Core
{
    // Choices made in the lobby menus, read by the session when the game starts.
    // Kept here so the menus (Presentation) and the session (GameSession) share them without knowing each other.
    public static class MatchChoices
    {
        public static MatchRules Rules = new MatchRules();
        public static string ModeId = "demo", MapId = "castle";   // ids from the lobby catalog
        public static int BotTarget = 10;      // population, including the local player
        public static bool SameHumanTeam;
        public static int HandCount;           // AI hands
        public static int SurvivorPreset, HandPreset;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Rules = new MatchRules(); ModeId = "demo"; MapId = "castle"; BotTarget = 10; SameHumanTeam = false; HandCount = 0; SurvivorPreset = 0; HandPreset = 0; }
    }

    // The island lobby's three terminals (join, host, map + mode) as Presentation draws them.
    // Game Session provides it and does the work; the terminals only show these and call them.
    public interface ILobbyControls
    {
        MonoBehaviour EnvironmentPrefab { get; }   // the island's ocean, sand and sky
        ScriptableObject Art { get; }              // art profile for the island (may be null: use the prefab's)
        // Collision stays with the lobby: Presentation hands over shapes, the lobby makes the colliders.
        void SetFloor(Mesh mesh, Transform pose);  // the island's sand surface (replaces the flat gray-box floor)
        void AddBlocker(Transform pose, Vector3 center, Vector3 size);   // something solid to walk around, in pose's local space

        // Join terminal
        bool JoinEnabled { get; }                  // false while hosting, joined or connecting
        bool ShowingNearby { get; }                // listing nearby rooms instead of the code box
        bool CodeFocused { get; }                  // typed letters go into the code
        string EnteredCode { get; set; }
        bool CodeValid { get; }
        void FocusCode();
        void UnfocusCode();
        void TypeCode(char c);
        void EraseCode();
        void Join();                               // join the entered code
        void DetectNearby();
        void ShowCodeEntry();
        string NearbyTitle(int slot);              // "" when that slot is empty (3 slots per page)
        bool HasNearby(int slot);
        void JoinNearby(int slot);
        bool HasMoreNearby { get; }
        void NextNearbyPage();
        string InvitationFrom { get; }             // null when no invitation
        void AcceptInvitation();
        bool CanLeave { get; }
        bool Busy { get; }
        void Leave();
        string JoinHint { get; }

        // Host terminal
        bool HostActive { get; }
        bool CanHost { get; }
        void Host();
        string HostCode { get; }
        string Region { get; }
        void NextRegion();
        bool CanInvite { get; }
        void InviteNearby();
        bool DemoSelected { get; }
        bool SharedControls { get; }
        void ToggleSharedControls();
        string HostHint { get; }

        // Map + mode terminal
        bool IsHost { get; }
        bool Connected { get; }
        bool CanConfigure { get; }
        string MapTitle { get; }
        string ModeTitle { get; }
        void CycleMap(int step);
        void CycleMode(int step);
        bool SelectMap(string id);                 // false when unknown or not allowed
        bool SelectMode(string id);
        bool CanStart { get; }
        bool StartPending { get; }
        void StartOrCancel();
        string SetupHint { get; }
    }
}
