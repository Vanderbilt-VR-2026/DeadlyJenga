using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastleBots
{
    public sealed partial class CastleBotBrain
    {
        IBotObjectiveExecution execution;
        BotExecutionPolicy executionPolicy;
        string executionObjective;
        float nextExecutionFeedback, nextObjectiveInteraction;
        public long ObjectiveAssignment => executionPolicy.assignment;

        bool BindExecution(BotObjective objective,out bool changed)
        {
            changed=false;
            var provider = BotObjectives.Current as IBotObjectiveExecution;
            BotExecutionPolicy policy = default;
            if (provider != null && !provider.TryGetPolicy(gameObject, objective.id, out policy)) return false;
            changed = !ReferenceEquals(execution, provider) || executionPolicy.assignment != policy.assignment || executionObjective != objective.id;
            if (changed) ReleaseExecution("Assignment replaced");
            execution = provider; executionPolicy = policy; executionObjective = objective.id;
            return true;
        }

        void ReleaseExecution(string reason)
        {
            if (execution != null) execution.Report(gameObject, Feedback(BotExecutionStatus.Interrupted, reason));
            execution = null; executionPolicy = default; executionObjective = null; nextExecutionFeedback = nextObjectiveInteraction = 0;
        }

        BotExecutionFeedback Feedback(BotExecutionStatus status, string reason) => new BotExecutionFeedback
        { objectiveId = executionObjective, assignment = executionPolicy.assignment, status = status, position = transform.position, reason = reason };

        void ValidateExecutionLease()
        {
            if (execution == null) return;
            if (!ReferenceEquals(execution, BotObjectives.Current) || !execution.TryGetPolicy(gameObject, executionObjective, out var policy) || policy.assignment != executionPolicy.assignment)
                ClearObjective();
        }

        void PublishExecution()
        {
            if (execution == null || !hasObjective || Time.time < nextExecutionFeedback) return;
            nextExecutionFeedback = Time.time + .5f;
            bool arrived = !recovering && traversalProgress.StalledFor<2.5f && Vector3.Distance(transform.position, selectedObjective.position) <= selectedObjective.radius;
            var status = arrived ? BotExecutionStatus.Arrived : idleFor > 3 ? BotExecutionStatus.Blocked : BotExecutionStatus.Running;
            execution.Report(gameObject, Feedback(status, StationaryReason));
            if (!arrived || string.IsNullOrEmpty(executionPolicy.interaction) || Time.time < nextObjectiveInteraction) return;
            nextObjectiveInteraction = Time.time + 1;
            IBotObjectiveInteraction interaction = null;
            if (selectedObjective.subject)
                foreach (var behaviour in selectedObjective.subject.GetComponentsInParent<MonoBehaviour>())
                    if (behaviour is IBotObjectiveInteraction action && action.InteractionId == executionPolicy.interaction) { interaction = action; break; }
            if (interaction == null)
            { execution.Report(gameObject, Feedback(BotExecutionStatus.Unavailable, "Required target interaction missing: " + executionPolicy.interaction)); return; }
            // A successful request is not proof of objective completion. Mode facts decide that.
            if (interaction.CanInteract(gameObject)) interaction.TryInteract(gameObject);
        }
    }
}
