using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CastleBots;

namespace DeadlyJenga.CharacterNetwork
{
    // Plugs Characters into Core when the game loads: lobby options, lobby input and bot creation.
    // The session and lobby use these through Core and never name a Characters class.
    static class CharacterServices
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Register()
        {
            MatchOptions.Movements = new[] { nameof(SurvivorVRMovement.HeadRelative), nameof(SurvivorVRMovement.BodyRelative) };
            MatchOptions.Turns = new[] { nameof(SurvivorVRTurn.Snap), nameof(SurvivorVRTurn.Smooth) };   // the two the lobby offers
            MatchOptions.Curves = Enum.GetNames(typeof(StaminaCurve));
            MatchOptions.SurvivorPersonalities = () => Names(Resources.LoadAll<CastleBotPersonality>("CastleBots").Select(p => p.displayName), "Default");
            MatchOptions.HandPersonalities = () => Names(Resources.LoadAll<CastleHandBotPersonality>("CastleHandBots").Select(p => p.displayName), "Playful");
            LocalPlayerInput.FindOrCreate = () => LocalHandInput.FindOrCreate();
            BotSystems.Register(MakeBots);
        }

        static string[] Names(IEnumerable<string> names, string fallback)
        {
            var sorted = names.OrderBy(n => n).ToArray();
            return sorted.Length == 0 ? new[] { fallback } : sorted;
        }

        static void MakeBots(GameObject host, BotSetup setup, out ISurvivorBotPopulation survivors, out IBotPopulation hands)
        {
            var bots = host.AddComponent<CastleBotPopulation>();
            var handBots = host.AddComponent<CastleHandBotPopulation>();
            var world = new BotWorld
            {
                Player = setup.player as CastleSurvivor, Hand = setup.hand as CastleHandActor,
                survivorPrefab = setup.survivorPrefab as CastleSurvivor, handPrefab = setup.handPrefab as CastleHandActor,
                Bots = bots, HandBots = handBots, isPlayingHand = setup.isPlayingHand
            };
            bots.Initialize(world); handBots.Initialize(world);
            survivors = bots; hands = handBots;
        }
    }
}
