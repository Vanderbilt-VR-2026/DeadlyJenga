using System;
using UnityEngine;
using DeadlyJenga.Core;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CastleBots;

namespace DeadlyJenga.CastlePrototype
{
    // What the session, menus and lobby use, through Core (IPlayableCharacter, ISurvivorSettings).
    public sealed partial class CastleSurvivor : IPlayableCharacter, ISurvivorSettings
    {
        // IPlayableCharacter (ViewCamera, IsVR, InputSuspended already exist)
        CandyTrackingSource handTracking;
        CandyTrackingSource HandTracking => handTracking ? handTracking : handTracking = GetComponent<CandyTrackingSource>();
        public bool HasHandTracking => HandTracking && HandTracking.HasHandTracking;
        public bool MenuPressed => HandTracking && HandTracking.LeftMenuPressed;
        public bool SelectPressed => HandTracking && HandTracking.RightPinchPressed;
        public bool SelectHeld => HandTracking && HandTracking.RightPinching;
        public string StatusText => ActionStatus;
        public void PrepareForSession() { if (viewCamera) viewCamera.clearFlags = CameraClearFlags.Skybox; }
        public void Place(Vector3 position, Quaternion rotation) { Teleport(position, rotation); Revive(); }

        // ISurvivorSettings
        public string MovementLabel => vrMovement.ToString();
        public string TurnLabel => vrTurn.ToString();
        public float SnapAngle => vrSnapAngle;
        public float SmoothTurnSpeed => vrSmoothDegreesPerSecond;
        public void CycleMovement() => CycleVRMovement();
        public void CycleTurn() => CycleVRTurn();
        public void CycleSnapAngle() => vrSnapAngle = vrSnapAngle < 30 ? 30 : vrSnapAngle < 45 ? 45 : vrSnapAngle < 60 ? 60 : 15;
        public void CycleSmoothTurnSpeed() => vrSmoothDegreesPerSecond = vrSmoothDegreesPerSecond < 60 ? 60 : vrSmoothDegreesPerSecond < 90 ? 90 : 45;
        public void UseStationaryPreset() => UseStationaryVR();
        public void UseFluidPreset() => UseFluidVR();
        public bool StandingPlay => vrPlayStyle == SurvivorVRPlayStyle.Standing;
        public void UseStandingPreset() => UseStandingVR();
        public string JumpGestureLabel => jumpGesture.ToString();
        public void RecalibrateSeatedHeight() => CalibrateSeatedHeight();
        public string ControlDescription => VRControlDescription;
        public MatchRules CurrentRules => new MatchRules
        {
            playStyle = vrPlayStyle.ToString(), movement = vrMovement.ToString(), turn = vrTurn.ToString(), snapAngle = vrSnapAngle, smoothDegrees = vrSmoothDegreesPerSecond,
            gripSeconds = climbGripSeconds, drainRate = climbDrainPerSecond, recoveryRate = climbRecoveryPerSecond, recoveryDelay = climbRecoveryDelay,
            drainCurve = climbDrainCurve.ToString(), recoveryCurve = climbRecoveryCurve.ToString(), drainDegree = climbDrainDegree, recoveryDegree = climbRecoveryDegree
        };

        public void FitJump(float stepHeight, float shortfall)
        {
            if (!Capsule) return;
            float margin = Mathf.Max(shortfall, Capsule.radius + Capsule.stepOffset + Capsule.skinWidth + .02f);
            // Small pavement steps must not shrink the character's normal jump.
            jumpHeight = Mathf.Max(authoredJumpHeight, stepHeight - margin);
        }

        [Serializable]
        sealed class StaminaRules
        {
            public float capacity, drain, recovery, delay, drainDegree, recoveryDegree;
            public StaminaCurve drainCurve, recoveryCurve;
        }
        public string SaveStaminaRules() => JsonUtility.ToJson(new StaminaRules
        {
            capacity = climbGripSeconds, drain = climbDrainPerSecond, recovery = climbRecoveryPerSecond, delay = climbRecoveryDelay,
            drainDegree = climbDrainDegree, recoveryDegree = climbRecoveryDegree, drainCurve = climbDrainCurve, recoveryCurve = climbRecoveryCurve
        });
        public void LoadStaminaRules(string json)
        {
            var rules = JsonUtility.FromJson<StaminaRules>(json);
            if (rules == null) throw new System.IO.InvalidDataException("Missing host stamina rules");
            foreach (float number in new[] { rules.capacity, rules.drain, rules.recovery, rules.delay, rules.drainDegree, rules.recoveryDegree })
                if (float.IsNaN(number) || float.IsInfinity(number)) throw new System.IO.InvalidDataException("Invalid stamina number");
            if (!Enum.IsDefined(typeof(StaminaCurve), rules.drainCurve) || !Enum.IsDefined(typeof(StaminaCurve), rules.recoveryCurve))
                throw new System.IO.InvalidDataException("Invalid stamina curve");
            climbGripSeconds = Mathf.Clamp(rules.capacity, .5f, 20);
            climbDrainPerSecond = Mathf.Clamp(rules.drain, 0, 4);
            climbRecoveryPerSecond = Mathf.Clamp(rules.recovery, 0, 2);
            climbRecoveryDelay = Mathf.Clamp(rules.delay, 0, 5);
            climbDrainDegree = Mathf.Clamp(rules.drainDegree, 1, 6);
            climbRecoveryDegree = Mathf.Clamp(rules.recoveryDegree, 1, 6);
            climbDrainCurve = rules.drainCurve;
            climbRecoveryCurve = rules.recoveryCurve;
        }

        public void ApplyMatchRules(MatchRules rules)
        {
            if (rules == null) return;
            if (Enum.TryParse(rules.playStyle, out SurvivorVRPlayStyle style)) SetVRPlayStyle(style, false);
            if (Enum.TryParse(rules.movement, out SurvivorVRMovement movement)) vrMovement = movement;
            if (Enum.TryParse(rules.turn, out SurvivorVRTurn turn)) vrTurn = turn;
            vrSnapAngle = rules.snapAngle;
            vrSmoothDegreesPerSecond = rules.smoothDegrees;
            climbGripSeconds = rules.gripSeconds;
            climbDrainPerSecond = rules.drainRate;
            climbRecoveryPerSecond = rules.recoveryRate;
            climbRecoveryDelay = rules.recoveryDelay;
            if (Enum.TryParse(rules.drainCurve, out StaminaCurve drainCurve)) climbDrainCurve = drainCurve;
            if (Enum.TryParse(rules.recoveryCurve, out StaminaCurve recoveryCurve)) climbRecoveryCurve = recoveryCurve;
            climbDrainDegree = rules.drainDegree;
            climbRecoveryDegree = rules.recoveryDegree;
        }

        public bool TryStandOn(Rigidbody block, float sideways, out Vector3 spot)
        {
            spot = default;
            var box = block ? block.GetComponent<BoxCollider>() : null;
            if (!box) return false;
            var top = CastleBotNavigation.TopPoint(block, box, sideways);
            if (!CastleBotNavigation.Landing(this, top, out var hit)) return false;
            spot = hit.point + Vector3.up * .06f;
            return true;
        }
    }
}
