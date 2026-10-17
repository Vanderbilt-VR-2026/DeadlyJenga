using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    // Choices made in the lobby before Game loads. Direct play of Game leaves these unset.
    // Option names (movement, turn, curves) come from Core's MatchOptions, filled by Characters.
    public static class MatchSetup
    {
        public static bool FromLobby;
        public static bool StartLocalSession;
        public static string Movement = "HeadRelative";
        public static string Turn = "Snap";
        public static float SnapAngle = 30f;
        public static float SmoothDegrees = 60f;
        public static float GripSeconds = 4f;
        public static int BotTarget = 1;
        public static int SurvivorPreset;
        public static int HandPreset;
        public static int HandCount;
        public static float DrainRate = 1f, RecoveryRate = .65f, RecoveryDelay = .45f;
        public static float DrainDegree = 2f, RecoveryDegree = 2f;
        public static string DrainCurve = "Linear", RecoveryCurve = "Linear";
        public static float MasterVolume = .8f, MusicVolume = .22f;

        public static MatchRules Rules => new MatchRules
        {
            movement = Movement, turn = Turn, snapAngle = SnapAngle, smoothDegrees = SmoothDegrees,
            gripSeconds = GripSeconds, drainRate = DrainRate, recoveryRate = RecoveryRate, recoveryDelay = RecoveryDelay,
            drainDegree = DrainDegree, recoveryDegree = RecoveryDegree, drainCurve = DrainCurve, recoveryCurve = RecoveryCurve
        };

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
                bots.SelectPreset(SurvivorPreset);
            }
            if (hands != null)
            {
                hands.SelectPreset(HandPreset);
                for (int i = 0; i < UnityEngine.Mathf.Min(HandCount, BotTarget - 1); i++)
                    hands.SpawnOne();
            }
            if (bots != null && BotTarget > 1) bots.FillToTarget();
        }
    }
}
