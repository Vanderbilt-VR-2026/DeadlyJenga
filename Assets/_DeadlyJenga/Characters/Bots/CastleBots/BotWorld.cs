using System;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastleBots
{
    // What bots need from the running game. Game Session fills this in.
    public sealed class BotWorld
    {
        public IWorldQuery world => Services.World;   // whichever map is active
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
