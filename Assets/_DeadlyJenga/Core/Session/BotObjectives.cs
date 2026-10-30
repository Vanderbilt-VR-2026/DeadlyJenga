using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.Core
{
    public enum BotObjectiveKind { Travel, Guard, Escort, Contest, Attack, Evade }

    public struct BotObjective
    {
        public string id;
        public Vector3 position;
        public GameObject subject;
        public float radius, priority, expiresAt;
        public BotObjectiveKind kind;
    }

    public interface IBotObjectives
    {
        void Collect(GameObject actor, List<BotObjective> results);
    }

    public static class BotObjectives
    {
        public static IBotObjectives Current;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => Current = null;
    }
}
