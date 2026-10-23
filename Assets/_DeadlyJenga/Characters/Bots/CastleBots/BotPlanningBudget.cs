using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace DeadlyJenga.CastleBots
{
    // Simulation-time credits preserve each bot's share across rendering rates.
    public static class BotPlanningBudget
    {
        public static int MaximumEdgesPerFrame = 160, MaximumSensingPerFrame = 24;
        public static float EdgesPerSecond = 2400, SensesPerSecond = 300;
        public static double MaximumMillisecondsPerFrame = 4;
        public static int EdgesUsed { get; private set; }
        public static int SensingUsed { get; private set; }
        sealed class Account { public float at, edges = 4, senses = 1; }
        static readonly Dictionary<int, Account> owners = new Dictionary<int, Account>();
        static readonly Stopwatch watch = new Stopwatch();
        static int frame = -1;
        static float remainingEdges, remainingSenses;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            owners.Clear(); frame = -1; remainingEdges = 40; remainingSenses = 8;
            EdgesUsed = SensingUsed = 0; watch.Reset();
        }
        static Account Begin(int owner, int population)
        {
            if (frame != Time.frameCount)
            {
                frame = Time.frameCount; EdgesUsed = SensingUsed = 0;
                bool running=watch.IsRunning; watch.Reset(); if(running)watch.Start();
                float dt = Mathf.Min(.1f, Time.deltaTime);
                remainingEdges = Mathf.Min(MaximumEdgesPerFrame, remainingEdges + dt * EdgesPerSecond);
                remainingSenses = Mathf.Min(MaximumSensingPerFrame, remainingSenses + dt * SensesPerSecond);
                if (owners.Count > Mathf.Max(256, population * 4)) owners.Clear();
            }
            if (!owners.TryGetValue(owner, out var a)) owners[owner] = a = new Account { at = Time.time };
            float elapsed = Mathf.Clamp(Time.time - a.at, 0, 1); a.at = Time.time;
            a.edges = Mathf.Min(64, a.edges + elapsed * EdgesPerSecond / Mathf.Max(1, population));
            a.senses = Mathf.Min(3, a.senses + elapsed * SensesPerSecond / Mathf.Max(1, population));
            return a;
        }
        public static bool TryEdge(int owner = 0, int population = 1)
        {
            var a = Begin(owner, population);
            if (remainingEdges < 1 || a.edges < 1 || watch.Elapsed.TotalMilliseconds >= MaximumMillisecondsPerFrame) return false;
            remainingEdges--; a.edges--; EdgesUsed++; return true;
        }
        public static bool TrySense() => TrySense(0, 1);
        public static bool TrySense(int owner, int population)
        {
            var a = Begin(owner, population);
            if (remainingSenses < 1 || a.senses < 1 || watch.Elapsed.TotalMilliseconds >= MaximumMillisecondsPerFrame) return false;
            remainingSenses--; a.senses--; SensingUsed++; return true;
        }
        public static void BeginWork() => watch.Start();
        public static void EndWork() => watch.Stop();
    }
}
