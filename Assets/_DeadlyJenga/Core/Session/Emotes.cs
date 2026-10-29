using System;
using UnityEngine;

namespace DeadlyJenga.Core
{
    // Emote names shared by everyone, plus the local player's "play this emote" request.
    // Menus and wheels call Request; Characters plays it on whatever the player is (survivor or hand)
    // and raises GameEvents.Emoted so sounds and bubbles can react.
    public static class Emotes
    {
        public static readonly string[] Survivor =
            { "FriendlyWave", "TotallyMeantThat", "Phew", "WhatWasThat", "GolfClap", "JustChilling", "NopeNopeNope", "TinyVictoryDance" };
        public static readonly string[] Hand = { "FingerWag", "ThumbsDown", "SlowClap", "PointAndLaugh" };

        public static event Action<int> LocalRequested;   // index into Survivor or Hand, whichever the local player is
        public static void Request(int index) => LocalRequested?.Invoke(index);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { LocalRequested = null; }
    }
}
