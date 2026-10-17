namespace DeadlyJenga.Core
{
    // Where to find the shared services. Game Session sets Session, the active map sets World,
    // the Lobby scene's connection (GameSession) sets Lobby.
    public static class Services
    {
        public static ISessionInfo Session;
        public static IWorldQuery World;
        public static ILobbyControls Lobby;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Session = null; World = null; Lobby = null; }
    }
}
