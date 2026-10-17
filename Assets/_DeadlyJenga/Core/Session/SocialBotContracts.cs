using System;
using UnityEngine;

namespace DeadlyJenga.Core
{
    // How a bot's personality is set when the social system gives it an identity (all 0..1 unless noted).
    public struct SocialBotTuning
    {
        public float bravery, goofiness, sociability, exploration, survivalPriority;
        public float pace;               // 0.5..1.2 walking speed factor
        public float decisionInterval;   // seconds between route decisions
        public float journeyDrive, hidePreference;
        public float restSeconds;
    }

    public enum SocialEmote { Friendly, Scared }

    // A survivor bot as the social system sees it. Characters provides it; Game Session's social
    // layer tunes it, nudges how it walks and reads what happened to it. The bot does the moving.
    public interface ISocialBot
    {
        GameObject gameObject { get; }
        Transform transform { get; }
        string NetworkId { get; }              // same id the network uses for this bot
        bool Respawning { get; }
        int Respawns { get; }                  // goes up by one every respawn
        bool Grounded { get; }
        float VerticalSpeed { get; }
        int VaultCount { get; }                // goes up by one every vault
        void Tune(SocialBotTuning tuning);     // replaces this bot's personality until Release
        void SetBravery(float bravery);
        float Skill { get; set; }              // 0..1: how crisply it walks on safe ground
        float HesitateUntil { get; set; }      // Time.time until which safe walking slows down
        Func<Vector3?> LookTarget { get; set; } // where to look while standing still (null = nowhere)
        void SeekPoint(Vector3 point, float seconds);
        float EmoteFrequency { get; set; }
        void PlayEmote(SocialEmote emote);
        void Release();                        // back to the plain bot
    }

    // Characters registers how to turn a spawned survivor bot into an ISocialBot.
    public static class SocialBots
    {
        static Func<GameObject, ISocialBot> attach;
        public static void Register(Func<GameObject, ISocialBot> make) => attach = make;
        public static ISocialBot Attach(GameObject bot) => bot ? attach?.Invoke(bot) : null;
        // What blocks a bot's line of sight (everything except survivors and blocks).
        public const int SightMask = ~((1 << Layers.Survivor) | (1 << Layers.Block));
    }
}
