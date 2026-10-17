using DeadlyJenga.CastlePrototype;

namespace DeadlyJenga.DevTools.Editor
{
    // Validators look inside the real survivor and hand. DevTools may name any box's classes;
    // gameplay code goes through Core instead.
    static class SessionTestAccess
    {
        public static CastleSurvivor Survivor(this CastlePlaySession session) => session.Player as CastleSurvivor;
        public static CastleHandActor HandActor(this CastlePlaySession session) => session.Hand as CastleHandActor;
    }
}
