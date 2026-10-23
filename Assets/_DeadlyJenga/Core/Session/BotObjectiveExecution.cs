using UnityEngine;

namespace DeadlyJenga.Core
{
    public enum BotExecutionStatus { Running, Arrived, Blocked, Failed, Interrupted, Unavailable }

    public struct BotExecutionPolicy
    {
        public long assignment;
        public string interaction;
    }

    public struct BotExecutionFeedback
    {
        public string objectiveId, reason;
        public long assignment;
        public BotExecutionStatus status;
        public Vector3 position;
    }

    // Optional extension to IBotObjectives; older providers remain valid.
    public interface IBotObjectiveExecution
    {
        bool TryGetPolicy(GameObject actor, string objectiveId, out BotExecutionPolicy policy);
        void Report(GameObject actor, BotExecutionFeedback feedback);
    }

    // The target owns permission, range, authority and the actual gameplay action.
    public interface IBotObjectiveInteraction
    {
        string InteractionId { get; }
        bool CanInteract(GameObject actor);
        bool TryInteract(GameObject actor);
    }
}
