using System;
using UnityEngine;

namespace DeadlyJenga.Core
{
    // A group of AI characters the menus can manage.
    public interface IBotPopulation
    {
        int Count { get; }
        int Capacity { get; }
        string Status { get; }
        string SelectedName { get; }   // personality used for the next one
        int SelectedPreset { get; }    // index of that personality
        bool Running { get; set; }
        GameObject SpawnOne();         // the new bot, or null when there was no room for it
        void Clear();
        void CyclePersonality();
        void SelectPreset(int index);
        void MapRebuilt();
    }

    public interface ISurvivorBotPopulation : IBotPopulation
    {
        int TargetPopulation { get; set; }
        void FillToTarget();
        void SetHumanPlayerCount(int humans);
        System.Collections.Generic.IEnumerable<GameObject> Members { get; }   // the survivor bots alive now
        bool AutoFill { get; set; }    // keep topping up to TargetPopulation
        float ThreatCost(Vector3 point);   // how dangerous a spot is right now (0 = safe)
    }

    // What the session hands to the bot system when a game starts.
    public struct BotSetup
    {
        public IPlayableCharacter player, hand;
        public MonoBehaviour survivorPrefab, handPrefab;
        public Func<bool> isPlayingHand;
    }

    // Characters registers how to create bots; the session asks for them without naming bot classes.
    public static class BotSystems
    {
        public delegate void Maker(GameObject host, BotSetup setup, out ISurvivorBotPopulation survivors, out IBotPopulation hands);
        static Maker maker;
        public static void Register(Maker make) => maker = make;
        public static void Create(GameObject host, BotSetup setup, out ISurvivorBotPopulation survivors, out IBotPopulation hands)
        {
            survivors = null; hands = null;
            maker?.Invoke(host, setup, out survivors, out hands);
        }
    }
}
