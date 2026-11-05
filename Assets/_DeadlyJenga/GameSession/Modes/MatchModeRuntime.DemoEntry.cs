using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;

namespace DeadlyJenga.Modes
{
    public sealed partial class MatchModeRuntime
    {
        internal void ReleaseCuratedDeferralsForDemo()
        {
            var experience = RoomGameExperience.Current;
            if (!Authority || simulation == null || !experience || experience.Experience != GameExperience.Demo) return;
            ruleCache.Clear();
            foreach (var participant in simulation.Participants)
            {
                if (!participant.present || participant.frame.bot || !participant.deferredParticipation) continue;
                string id = participant.frame.id;
                simulation.SetDeferredParticipation(id, false);
                participant.frame.alive = true;
                participant.frame.role = "survivor";
                participant.frame.health = 1;
                pendingSpawns.Remove(id); transitionUntil.Remove(id); delayedOutcomes.Remove(id); sinkStarted.Remove(id);
                Respawn(id, "survivor");
            }
        }
    }
}
