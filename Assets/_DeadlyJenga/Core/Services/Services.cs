namespace DeadlyJenga.Core
{
    // Where to find the shared services. Game Session sets Session, the active map sets World.
    public static class Services
    {
        public static ISessionInfo Session;
        public static IWorldQuery World;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Session = null; World = null; }
    }
}
