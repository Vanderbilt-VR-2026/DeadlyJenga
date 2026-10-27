using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.Core
{
    public enum GameModeKind { Demo, Survival, KingOfHill, BlockControl, CaptureFlag, Infection, TeamTickets, MurderMystery }
    public enum IslandRule { Sink, Lethal, Walkable }
    public enum SpawnRule { Top, Rim, TeamPoles }
    public enum RoundPhase { Waiting, Countdown, Playing, Victory, Results }
    public enum ScoreMetric { SurvivalTime, AverageHeight, CurrentHeight, HillTime, BlocksOwned, TeamPoints, InfectedKills, Eliminations, FlagCaptures, BlocksClaimed, AliveStatus }

    [Serializable]
    public sealed class ModeRules
    {
        public GameModeKind mode;
        public IslandRule island = IslandRule.Sink;
        public SpawnRule spawn;
        public ScoreMetric metric;
        public int teamCount = 2, tickets = 50, flagTarget = 3;
        public bool defaultDagger;
        public bool sameHumanTeam, punchEnabled, healthEnabled, deathEnabled = true, fallDamage;
        public bool friendlyFire;
        public bool flagWinByTwo = true, starterInfected, enemyClimbBlocked;
        public float punchDistance = 3, punchDamage = 18, punchCooldown = .8f;
        public float hillInterval = 15, hillRadius = 3, claimSeconds = 3, reclaimSeconds = 5;
        public float claimDecaySeconds = 30, pointsPerBlockSecond = 1, handPowerSeconds = 15;
        public float roundSeconds = 300, countdownSeconds = 3, victorySeconds = 5;
        public float sinkGraceSeconds = 4, sinkSpeed = .2f, sinkMoveScale = .16f;
        public float fallDamageStart = 4, fallDamagePerMeter = 12;
        public bool carryingFlag, sinking;
        public float sinkProgress;
        public bool canJump = true, canClimb = true, canVault = true;
        public float moveScale = 1, vaultScale = 1;
        public string environmentSkin = "tropical";
        public bool automaticDisasters = true;
        public float disasterMinInterval = 90, disasterMaxInterval = 120, disasterLateInterval = 60, disasterWarningSeconds = 8;
        public int maxConcurrentDisasters = 2;
        public MysterySettings mystery = new MysterySettings();
        public bool downed;
        public ModeRules Copy() => (ModeRules)MemberwiseClone();
    }

    public interface IModeActor
    {
        GameObject gameObject { get; }
        Transform transform { get; }
        Transform Visual { get; }
        string ActorId { get; }
        string DisplayName { get; }
        int Team { get; }
        Color Color { get; }
        bool Alive { get; }
        bool IsBot { get; }
        bool IsLocal { get; }
        bool Grounded { get; }
        float Health { get; }
        float MaxHealth { get; }
        Pose ViewPose { get; }
        Pose HandPose(bool left);
        Rigidbody StandingBody { get; }
        Vector3 StandingNormal { get; }
        void SetIdentity(string id, string displayName, int team, Color color);
        void ApplyModeRules(ModeRules rules);
    }

    [Serializable]
    public sealed class MatchPlayerFrame
    {
        public string id, name, role = "survivor", spectating;
        public int team = -1, rank, kills, watching;
        public int deaths, captures, flagReturns, blocksClaimed, infections, bodyReports, revives;
        public float hillSeconds;
        public Color color = Color.white;
        public Color survivorColor;
        public bool alive, bot, local, dying;
        public float deathProgress;
        public float deathRemaining, handPowerRemaining, heightObservedSeconds;
        public string pendingOutcome, handReturnRole;
        public bool sinking;
        public float sinkProgress;
        public float health = 1, metric, survivalSeconds, averageHeight, height, claimProgress;
        public int standingBlock = -1, blockOwner = -1;
        public Vector3 position, viewPosition;
        public Quaternion viewRotation = Quaternion.identity;
        public MatchPlayerFrame Copy() => (MatchPlayerFrame)MemberwiseClone();
    }
    [Serializable]
    public sealed class MatchTeamFrame
    {
        public int id, ownedBlocks, captures, tickets;
        public string name;
        public Color color;
        public float points;
    }
    [Serializable]
    public sealed class BlockClaimFrame
    {
        public int block, owner = -1, claimant = -1;
        public float progress;
        public float emptySeconds;
    }
    [Serializable]
    public sealed class FlagFrame
    {
        public int team;
        public Vector3 home, position;
        public string carrier;
        public bool atHome = true;
    }
    [Serializable] public sealed class ExpressionFrame {public int id;public string actor,name;public Vector3 position;public float remaining;}
    [Serializable]
    public sealed class MatchFrame
    {
        public int expressionSequence;
        public ExpressionFrame[] expressions=Array.Empty<ExpressionFrame>();
        public EquipmentKind demoRespawnEquipment;
        public EquipmentProjectileFrame[] weaponProjectiles=Array.Empty<EquipmentProjectileFrame>();
        public MysteryReplayCue mysteryReplay;
        public PowerupSpawnSettings powerupSpawns=new PowerupSpawnSettings();
        public int randomPickupCount;
        public float nextPowerupIn;
        public string enemyActionEpoch="";
        public int enemyActionSequence;
        public EnemyActionFrame[] enemyActions=Array.Empty<EnemyActionFrame>();
        public string equipmentEpoch="";
        public int equipmentSequence;
        public EquipmentActionFrame[] equipmentActions=Array.Empty<EquipmentActionFrame>();
        public EquipmentState[] equipment=Array.Empty<EquipmentState>();
        public EquipmentPickup[] pickups=Array.Empty<EquipmentPickup>();
        public BlasterFrame blaster=new BlasterFrame();
        public int revision, winnerTeam = -1;
        public RoundPhase phase;
        public ModeRules rules = new ModeRules();
        public string metricLabel = "Time alive", winner = "";
        public string secondaryMetricLabel = "";
        public float remaining, elapsed;
        public string disasterWarning = "";
        public float disasterWarningRemaining, disasterNextIn;
        public int survivorsLeft;
        public Vector3 hill;
        public MatchPlayerFrame[] players = Array.Empty<MatchPlayerFrame>();
        public MatchPlayerFrame[] topTen = Array.Empty<MatchPlayerFrame>();
        public MatchPlayerFrame[] secondaryLeaders = Array.Empty<MatchPlayerFrame>();
        public MatchTeamFrame[] teams = Array.Empty<MatchTeamFrame>();
        public BlockClaimFrame[] claims = Array.Empty<BlockClaimFrame>();
        public FlagFrame[] flags = Array.Empty<FlagFrame>();
        public MysteryPublicFrame mystery = new MysteryPublicFrame();
    }

    public interface IMatchRuntime
    {
        ModeRules Rules { get; }
        MatchFrame Frame { get; }
        string LocalActorId { get; }
        bool Authority { get; }
        IModeActor FindActor(string id);
        Transform FindParticipantRoot(string id);
        ModeRules RulesFor(GameObject actor);
        bool CanClimb(GameObject actor, IClimbable climbable);
        bool RequestPunch(GameObject source, Vector3 origin, Vector3 direction, float speed);
        void Configure(ModeRules rules);
        void StartRound();
        void ReturnToLobby();
        void NextSpectator(int step);
        void ActivateHandPower(float seconds);
    }
    // Temporary development review tools. Normal host permission must never enable these.
    public interface IMatchReviewControls
    {
        bool DevelopmentReviewEnabled { get; }
        bool CanEndGame { get; }
        string PlayerInRole(MysteryRole role);
        void WatchPlayer(string actorId);
        void EndGame();
    }
    public static class MatchRuntime
    {
        public static IMatchRuntime Current;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => Current = null;
    }
    public static class ModeDefaults
    {
        public static Func<GameModeKind,ModeRules> Create;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => Create = null;
    }
}
