using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Applies the lobby's choices (Core's MatchChoices) when Game loads. Direct play of Game leaves
    // FromLobby unset, so nothing is applied. The named properties below are the lobby's view of
    // MatchChoices; they all read and write the same Core data.
    public static class MatchSetup
    {
        public static bool FromLobby;
        public static bool StartLocalSession;
        public static MatchRules Rules => MatchChoices.Rules;

        public static string ModeId { get => MatchChoices.ModeId; set => MatchChoices.ModeId = value; }
        public static string MapId { get => MatchChoices.MapId; set => MatchChoices.MapId = value; }
        public static int BotTarget { get => MatchChoices.BotTarget; set => MatchChoices.BotTarget = value; }
        public static int HandCount { get => MatchChoices.HandCount; set => MatchChoices.HandCount = value; }
        public static int SurvivorPreset { get => MatchChoices.SurvivorPreset; set => MatchChoices.SurvivorPreset = value; }
        public static int HandPreset { get => MatchChoices.HandPreset; set => MatchChoices.HandPreset = value; }
        public static float SnapAngle { get => Rules.snapAngle; set => Rules.snapAngle = value; }
        public static float SmoothDegrees { get => Rules.smoothDegrees; set => Rules.smoothDegrees = value; }
        public static float GripSeconds { get => Rules.gripSeconds; set => Rules.gripSeconds = value; }
        public static float DrainRate { get => Rules.drainRate; set => Rules.drainRate = value; }
        public static float RecoveryRate { get => Rules.recoveryRate; set => Rules.recoveryRate = value; }
        public static float RecoveryDelay { get => Rules.recoveryDelay; set => Rules.recoveryDelay = value; }
        public static float DrainDegree { get => Rules.drainDegree; set => Rules.drainDegree = value; }
        public static float RecoveryDegree { get => Rules.recoveryDegree; set => Rules.recoveryDegree = value; }
        public static string DrainCurve { get => Rules.drainCurve; set => Rules.drainCurve = value; }
        public static string RecoveryCurve { get => Rules.recoveryCurve; set => Rules.recoveryCurve = value; }
        public static string Movement => Rules.movement;
        public static string Turn => Rules.turn;
        public static float MasterVolume = .8f, MusicVolume = .22f;

        // Seated (body-directed, smooth turn) or Standing (head-directed, turn physically). Saved per device.
        public static string PlayStyle
        {
            get { RefreshLocalControls(); return Rules.playStyle; }
        }
        public static void TogglePlayStyle()
        {
            RefreshLocalControls();
            SetPlayStyle(Rules.playStyle == "Standing" ? "Seated" : "Standing");
            PlayerPrefs.SetInt("SurvivorVRPlayStyle", Rules.playStyle == "Standing" ? 1 : 0);
        }
        public static void RefreshLocalControls()
        {
            SetPlayStyle(PlayerPrefs.GetInt("SurvivorVRPlayStyle", 0) == 1 ? "Standing" : "Seated");
        }
        static void SetPlayStyle(string style)
        {
            Rules.playStyle = style;
            Rules.movement = style == "Standing" ? "HeadRelative" : "BodyRelative";
            Rules.turn = style == "Standing" ? "PhysicalOnly" : "Smooth";
        }

        // The host's stamina rules for joining players, through Core's ISurvivorSettings.
        public static string CaptureRules(object player) => (player as ISurvivorSettings)?.SaveStaminaRules();
        public static void ApplyRules(object player, string json) => (player as ISurvivorSettings)?.LoadStaminaRules(json);

        public static void Apply(CastlePlaySession session)
        {
            if (!FromLobby || session == null || session.Player == null) return;
            RefreshLocalControls();
            (session.Player as ISurvivorSettings)?.ApplyMatchRules(Rules);
            if (session.Network != null && session.Network.IsClient) return;
            var bots = session.SurvivorBots;
            var hands = ((ISessionInfo)session).HandBots;
            if (bots != null)
            {
                bots.TargetPopulation = Mathf.Max(1, BotTarget);
                bots.SelectPreset(SurvivorPreset);
            }
            if (hands != null)
            {
                hands.SelectPreset(HandPreset);
                for (int i = 0; i < Mathf.Max(0, HandCount); i++)
                    hands.SpawnOne();
            }
            if (ModeId != "demo" && bots != null && BotTarget > 1) bots.FillToTarget();
        }
    }
}
