using System;
using DeadlyJenga.CastlePrototype;

namespace DeadlyJenga.CastleBots
{
    // What bots need from the running game. Game Session fills this in.
    public sealed class BotWorld
    {
        public CastleGenerator generator;
        public CastleSurvivor Player;
        public CastleHandActor Hand;
        public CastleSurvivor survivorPrefab;
        public CastleHandActor handPrefab;
        public CastleBotPopulation Bots;
        public CastleHandBotPopulation HandBots;
        public Func<bool> isPlayingHand;
        public bool PlayingHand => isPlayingHand != null && isPlayingHand();
    }
}
