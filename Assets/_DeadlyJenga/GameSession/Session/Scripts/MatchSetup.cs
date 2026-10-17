namespace DeadlyJenga.CastlePrototype
{
    // Choices made in the lobby before Game loads. Direct play of Game leaves these unset.
    public static class MatchSetup
    {
        public static bool FromLobby;
        public static bool StartLocalSession;
        public static string ModeId="demo", MapId="castle";
        public static SurvivorVRPlayStyle PlayStyle=(SurvivorVRPlayStyle)UnityEngine.Mathf.Clamp(UnityEngine.PlayerPrefs.GetInt("SurvivorVRPlayStyle",0),0,1);
        public static SurvivorVRMovement Movement = PlayStyle==SurvivorVRPlayStyle.Standing?SurvivorVRMovement.HeadRelative:SurvivorVRMovement.BodyRelative;
        public static SurvivorVRTurn Turn = PlayStyle==SurvivorVRPlayStyle.Standing?SurvivorVRTurn.PhysicalOnly:SurvivorVRTurn.Smooth;
        public static void TogglePlayStyle()
        {
            PlayStyle=PlayStyle==SurvivorVRPlayStyle.Seated?SurvivorVRPlayStyle.Standing:SurvivorVRPlayStyle.Seated;
            Movement=PlayStyle==SurvivorVRPlayStyle.Standing?SurvivorVRMovement.HeadRelative:SurvivorVRMovement.BodyRelative;
            Turn=PlayStyle==SurvivorVRPlayStyle.Standing?SurvivorVRTurn.PhysicalOnly:SurvivorVRTurn.Smooth;
            UnityEngine.PlayerPrefs.SetInt("SurvivorVRPlayStyle",(int)PlayStyle);
        }
        public static float SnapAngle = 30f;
        public static float SmoothDegrees = 60f;
        public static float GripSeconds = 4f;
        public static int BotTarget = 10;
        public static int SurvivorPreset;
        public static int HandPreset;
        public static int HandCount;
        public static float DrainRate = 1f, RecoveryRate = .65f, RecoveryDelay = .45f;
        public static float DrainDegree = 2f, RecoveryDegree = 2f;
        public static StaminaCurve DrainCurve, RecoveryCurve;
        public static float MasterVolume = .8f, MusicVolume = .22f;

        [System.Serializable]
        public sealed class StaminaRules
        {
            public float capacity, drain, recovery, delay, drainDegree, recoveryDegree;
            public StaminaCurve drainCurve, recoveryCurve;
        }
        public static string CaptureRules(CastleSurvivor player) => UnityEngine.JsonUtility.ToJson(new StaminaRules
        {
            capacity = player.climbGripSeconds, drain = player.climbDrainPerSecond,
            recovery = player.climbRecoveryPerSecond, delay = player.climbRecoveryDelay,
            drainDegree = player.climbDrainDegree, recoveryDegree = player.climbRecoveryDegree,
            drainCurve = player.climbDrainCurve, recoveryCurve = player.climbRecoveryCurve
        });
        public static void ApplyRules(CastleSurvivor player, string json)
        {
            var rules = UnityEngine.JsonUtility.FromJson<StaminaRules>(json);
            if (rules == null) throw new System.IO.InvalidDataException("Missing host stamina rules");
            float[] numbers={rules.capacity,rules.drain,rules.recovery,rules.delay,rules.drainDegree,rules.recoveryDegree};
            foreach(float number in numbers)if(float.IsNaN(number)||float.IsInfinity(number))throw new System.IO.InvalidDataException("Invalid stamina number");
            if(!System.Enum.IsDefined(typeof(StaminaCurve),rules.drainCurve)||!System.Enum.IsDefined(typeof(StaminaCurve),rules.recoveryCurve))throw new System.IO.InvalidDataException("Invalid stamina curve");
            player.climbGripSeconds = UnityEngine.Mathf.Clamp(rules.capacity, .5f, 20);
            player.climbDrainPerSecond = UnityEngine.Mathf.Clamp(rules.drain, 0, 4);
            player.climbRecoveryPerSecond = UnityEngine.Mathf.Clamp(rules.recovery, 0, 2);
            player.climbRecoveryDelay = UnityEngine.Mathf.Clamp(rules.delay, 0, 5);
            player.climbDrainDegree = UnityEngine.Mathf.Clamp(rules.drainDegree, 1, 6);
            player.climbRecoveryDegree = UnityEngine.Mathf.Clamp(rules.recoveryDegree, 1, 6);
            player.climbDrainCurve = rules.drainCurve;
            player.climbRecoveryCurve = rules.recoveryCurve;
        }

        public static void RefreshLocalControls()
        {
            var savedStyle=(SurvivorVRPlayStyle)UnityEngine.Mathf.Clamp(UnityEngine.PlayerPrefs.GetInt("SurvivorVRPlayStyle",(int)PlayStyle),0,1);
            if(savedStyle!=PlayStyle)
            {
                PlayStyle=savedStyle;
                Movement=PlayStyle==SurvivorVRPlayStyle.Standing?SurvivorVRMovement.HeadRelative:SurvivorVRMovement.BodyRelative;
                Turn=PlayStyle==SurvivorVRPlayStyle.Standing?SurvivorVRTurn.PhysicalOnly:SurvivorVRTurn.Smooth;
            }
        }
        public static void Apply(CastlePlaySession session)
        {
            if (!FromLobby || session == null || session.Player == null)return;
            var player=session.Player;
            RefreshLocalControls();
            player.SetVRPlayStyle(PlayStyle,false);
            player.vrMovement = Movement;
            player.vrTurn = Turn;
            player.vrSnapAngle = SnapAngle;
            player.vrSmoothDegreesPerSecond = SmoothDegrees;
            player.climbGripSeconds = GripSeconds;
            player.climbDrainPerSecond = DrainRate;
            player.climbRecoveryPerSecond = RecoveryRate;
            player.climbRecoveryDelay = RecoveryDelay;
            player.climbDrainCurve = DrainCurve;
            player.climbRecoveryCurve = RecoveryCurve;
            player.climbDrainDegree = DrainDegree;
            player.climbRecoveryDegree = RecoveryDegree;
            if (session.Network != null && session.Network.IsClient) return;
            if (session.Bots != null)
            {
                session.Bots.targetPopulation = UnityEngine.Mathf.Max(1,BotTarget);
                session.Bots.SelectPreset(SurvivorPreset);

            }
            if (session.HandBots != null)
            {
                session.HandBots.SelectPreset(HandPreset);
                for (int i = 0; i < UnityEngine.Mathf.Max(0,HandCount); i++)
                    session.HandBots.SpawnHand();
            }
            if (ModeId!="demo" && session.Bots != null && BotTarget > 1) session.Bots.FillToTarget();
        }
    }
}
