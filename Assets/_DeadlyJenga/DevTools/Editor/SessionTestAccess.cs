using DeadlyJenga.CastleBots;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;

namespace DeadlyJenga.DevTools.Editor
{
    // Validators look inside the real survivor and hand. DevTools may name any box's classes;
    // gameplay code goes through Core instead.
    public static class SessionTestAccess
    {
        public static CastleSurvivor Survivor(this CastlePlaySession session) => session.Player as CastleSurvivor;
        public static CastleHandActor HandActor(this CastlePlaySession session) => session.Hand as CastleHandActor;
        public static CastleBotPopulation Bots(this CastlePlaySession session) => session.SurvivorBots as CastleBotPopulation;
        public static CastleHandBotPopulation Hands(this CastlePlaySession session) => session.HandBots as CastleHandBotPopulation;
        public static IslandLobbyView View(this IslandLobby lobby) => lobby.GetComponent<IslandLobbyView>();
        public static CastleSurvivor Survivor(this IslandLobby lobby) => lobby.Player as CastleSurvivor;
        public static CastleGenerator Generator(this CastlePlaySession session) => Services.World as CastleGenerator;
    }
}
