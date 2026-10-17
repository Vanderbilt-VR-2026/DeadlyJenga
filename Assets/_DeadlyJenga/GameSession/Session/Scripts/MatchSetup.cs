using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    // Applies the lobby's choices (Core's MatchChoices, set by the lobby menus) when Game loads.
    // Direct play of Game leaves FromLobby unset, so nothing is applied.
    public static class MatchSetup
    {
        public static bool FromLobby;
        public static bool StartLocalSession;
        public static MatchRules Rules => MatchChoices.Rules;
        static int BotTarget => MatchChoices.BotTarget;

        public static void Apply(CastlePlaySession session)
        {
            if (!FromLobby || session == null || session.Player == null)
                return;

            (session.Player as ISurvivorSettings)?.ApplyMatchRules(Rules);
            if (!session.CanEditWorld) return;
            var bots = session.SurvivorBots;
            var hands = session.HandBots;
            if (bots != null)
            {
                bots.TargetPopulation = UnityEngine.Mathf.Clamp(BotTarget, 1, 20);
                bots.SelectPreset(MatchChoices.SurvivorPreset);
            }
            if (hands != null)
            {
                hands.SelectPreset(MatchChoices.HandPreset);
                for (int i = 0; i < UnityEngine.Mathf.Min(MatchChoices.HandCount, BotTarget - 1); i++)
                    hands.SpawnOne();
            }
            if (bots != null && BotTarget > 1) bots.FillToTarget();
        }
    }
}
