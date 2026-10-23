using System;
using UnityEngine;
using UnityEngine.InputSystem;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CastleSurvivor : IModeActor
    {
        public bool Sinking { get; private set; }
        public float SinkProgress { get; private set; }
        public bool CarryingFlag { get; private set; }
        public bool HasWalkableIsland => modeRules!=null&&modeRules.island==IslandRule.Walkable;
        public bool PunchEnabled { get; private set; }
        public bool FallDamageEnabled { get; set; }
        public bool IgnoreFatalDamage { get; set; }
        public float punchCooldown = .85f;
        public float punchMinimumSpeed = 1.4f;
        public float fallSafeSpeed = 8, fallDamagePerSpeed = 12;
        public event Action<CastleSurvivor, Vector3, Vector3, float> PunchRequested;
        float movementMultiplier = 1, vaultMultiplier = 1, nextPunch;
        public float EffectiveWalkSpeed => walkSpeed*movementMultiplier*PowerupSpeedMultiplier;
        float peakFallSpeed;
        ModeRules modeRules;
        string actorId, displayName;
        int team = -1;
        Color actorColor = UnityEngine.Color.white;
        bool identityAssigned;
        public string ActorId => string.IsNullOrEmpty(actorId) ? "local-" + GetInstanceID() : actorId;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? gameObject.name : displayName;
        public int Team => team;
        public Color Color { get { var avatar=GetComponent<CandyPlayerAvatar>();return identityAssigned?actorColor:avatar&&avatar.Rig&&avatar.Rig.style?avatar.Rig.style.ColorAt(avatar.ColorIndex):actorColor; } }
        public bool Alive => !eliminated;
        public bool IsBot => externallyDriven;
        bool IModeActor.IsLocal => !externallyDriven;
        public Transform Visual { get { var avatar=GetComponent<CandyPlayerAvatar>();return avatar&&avatar.Rig?avatar.Rig.transform:transform.Find("Visual"); } }
        public Pose ViewPose => viewCamera ? new Pose(viewCamera.transform.position,viewCamera.transform.rotation) : new Pose(transform.position+Vector3.up*StandingHeight,transform.rotation);
        public Pose HandPose(bool left)
        {
            var avatar=GetComponent<CandyPlayerAvatar>();
            if(avatar&&avatar.Rig){var f=avatar.ReadFrame();var hand=left?f.left:f.right;return new Pose(hand.position,hand.rotation);}
            var view=ViewPose;return new Pose(view.position+view.rotation*new Vector3(left?-.25f:.25f,-.4f,.35f),view.rotation);
        }
        public Rigidbody StandingBody => Grounded&&StandingSurface ? Climbables.TryGet(StandingSurface,out var owner)?owner.Body:StandingSurface.attachedRigidbody : null;
        public Vector3 StandingNormal => Grounded ? GroundNormal : Vector3.zero;
        public void SetIdentity(string id,string label,int assignedTeam,Color color)
        {
            actorId=id;displayName=label;team=assignedTeam;actorColor=color;identityAssigned=true;
            var avatar=GetComponent<CandyPlayerAvatar>();if(avatar)avatar.SetExactColor(color);
        }
        public void ApplyModeRules(ModeRules rules)
        {
            if(rules==null)return;
            modeRules=rules;
            ConfigureActions(rules.punchEnabled,rules.carryingFlag,rules.moveScale,rules.vaultScale);
            SetSinking(rules.sinking,rules.sinkProgress);
            punchCooldown=rules.punchCooldown;FallDamageEnabled=rules.fallDamage;IgnoreFatalDamage=!rules.deathEnabled;
            fallSafeSpeed=Mathf.Sqrt(2*Mathf.Max(.1f,gravity)*Mathf.Max(0,rules.fallDamageStart));
            fallDamagePerSpeed=Mathf.Max(0,rules.fallDamagePerMeter)*fallSafeSpeed/Mathf.Max(.1f,gravity);
        }
        Vector3 lastPunchHand;
        bool punchHandReady, punchArmed = true;
        public Func<DeadlyJenga.Core.IClimbable, bool> ClimbPermission { get; set; }

        public void ConfigureActions(bool punch, bool flagCarrier, float walkMultiplier = 1, float vaultSpeedMultiplier = 1)
        {
            PunchEnabled = punch;
            CarryingFlag = flagCarrier;
            movementMultiplier = Mathf.Clamp(walkMultiplier, 0, 3);
            vaultMultiplier = Mathf.Clamp(vaultSpeedMultiplier, .1f, 3);
            if (flagCarrier && Climbing) ReleaseClimb("Flag carrier cannot climb", false);
        }

        public void SetSinking(bool value, float progress = 0)
        {
            Sinking = value; SinkProgress = value ? Mathf.Clamp01(progress) : 0;
            if (value && Climbing) ReleaseClimb("Sinking — find a ledge", false);
        }
        void ApplyFallImpact()
        {
            if(!Grounded)return;
            if(FallDamageEnabled && peakFallSpeed>fallSafeSpeed)
            {
                float distance=(peakFallSpeed*peakFallSpeed-fallSafeSpeed*fallSafeSpeed)/(2*Mathf.Max(.1f,gravity));
                Damage(distance*Mathf.Max(0,modeRules?.fallDamagePerMeter??12),null);
            }
            peakFallSpeed=0;
        }

        public bool RequestPunch(Vector3 origin, Vector3 direction, float speed)
        {
            if (!PunchEnabled || CarryingFlag || InputSuspended || eliminated || Time.time < nextPunch || direction.sqrMagnitude < .001f) return false;
            nextPunch = Time.time + Mathf.Max(.2f, punchCooldown);
            MatchRuntime.Current?.RequestPunch(gameObject,origin,direction.normalized,Mathf.Clamp(speed,0,12));
            PunchRequested?.Invoke(this, origin, direction.normalized, Mathf.Clamp(speed, 0, 12));
            return true;
        }

        void UpdatePunch()
        {
            if(MatchRuntime.Current!=null)ApplyModeRules(MatchRuntime.Current.RulesFor(gameObject));
            var weapon=GetComponent<CandyEquipment>();if(weapon?.Equipment.drawn==true){punchHandReady=false;return;}
            if (externallyDriven || !PunchEnabled || CarryingFlag || InputSuspended || !vrInputFocused)
            { punchHandReady = false; return; }
            if (!IsVR)
            {
                if (Cursor.lockState == CursorLockMode.Locked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                    RequestPunch(viewCamera.transform.position, viewCamera.transform.forward, 2);
                return;
            }
            if (!candyTracking) candyTracking = GetComponent<CandyTrackingSource>();
            var hand = candyTracking ? candyTracking.WorldHand(false) : default;
            if (!hand.tracked) { punchHandReady = false; return; }
            Vector3 relative = hand.position-viewCamera.transform.position;
            if (!punchHandReady) { lastPunchHand = relative; punchHandReady = true; return; }
            Vector3 delta = relative - lastPunchHand; lastPunchHand = relative;
            float speed = delta.magnitude / Mathf.Max(.001f, Time.deltaTime) / Mathf.Max(.1f, playerScale);
            if (speed < .45f) punchArmed = true;
            bool fist = !hand.skeletonTracked;
            if (hand.skeletonTracked)
            {
                float closed = 0;
                for (int f = 1; f < 5; f++) closed += Vector3.Distance(hand.skeleton.Joint(f * 4 + 3), hand.skeleton.Joint(f * 4));
                fist = closed < .18f;
            }
            if (punchArmed && fist && speed >= punchMinimumSpeed && speed < 12 && Vector3.Dot(delta.normalized, viewCamera.transform.forward) > .25f)
            { if (RequestPunch(hand.position, delta, speed)) punchArmed = false; }
        }
    }
}
