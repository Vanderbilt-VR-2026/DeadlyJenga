using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    internal static class LocalSpectatorParticipation
    {
        public static bool TryRead(GameObject actor,out bool spectator)
        {
            spectator=false;
            var session=Services.Session;
            if(session==null)return true;
            bool survivor=session.Player?.gameObject==actor;
            bool hand=session.Hand?.gameObject==actor;
            if(!survivor&&!hand)return true;
            if(session is ILobbyControls)return true;
            bool initialHold=session is IGameExperienceControls experience&&experience.Experience==GameExperience.Game;
            var runtime=MatchRuntime.Current;
            var players=runtime?.Frame?.players;
            bool currentLocalRoot=false;
            if(runtime!=null&&!string.IsNullOrEmpty(runtime.LocalActorId))
            {
                currentLocalRoot=survivor?runtime.FindActor(runtime.LocalActorId)?.gameObject==actor:
                    runtime.FindParticipantRoot(runtime.LocalActorId)==actor.transform;
            }
            if(players==null||!currentLocalRoot)
            {
                spectator=initialHold;
                return initialHold;
            }
            foreach(var row in players)
                if(row!=null&&row.id==runtime.LocalActorId)
                {
                    spectator=row.role=="spectator"&&!row.dying;
                    return true;
                }
            spectator=initialHold;
            return initialHold;
        }
    }

    public sealed partial class CastleSurvivor
    {
        bool spectatorPhysicsSuspended,spectatorCapsuleWasEnabled,spectatorMoverWasTracked;
        bool ParticipationBlocked
        {
            get
            {
                if(externallyDriven)return false;
                return LocalSpectatorParticipation.TryRead(gameObject,out bool spectator)?spectator:spectatorPhysicsSuspended;
            }
        }
        bool UpdateSpectatorParticipation()
        {
            if(externallyDriven)return false;
            if(LocalSpectatorParticipation.TryRead(gameObject,out bool spectator))
            {
                if(spectator&&!spectatorPhysicsSuspended)
                {
                    spectatorCapsuleWasEnabled=Capsule.enabled;
                    spectatorPhysicsSuspended=true;
                    spectatorMoverWasTracked=Tracked.Movers.Remove(this);
                    ClearSprint();ClearMovementPowerups();ResetClimb(false);ResetVRJump();
                    support=null;Vaulting=false;Grounded=false;StandingSurface=null;
                    verticalSpeed=0;slopeVelocity=iceVelocity=Vector3.zero;
                    hoverUntil=0;hoverVertical=0;hoverVelocity=Vector3.zero;
                    jumped=false;peakFallSpeed=0;
                }
                else if(!spectator)RestoreSpectatorParticipation();
            }
            if(spectatorPhysicsSuspended){Capsule.enabled=false;Tracked.Movers.Remove(this);}
            return spectatorPhysicsSuspended;
        }
        void RestoreSpectatorParticipation()
        {
            if(!spectatorPhysicsSuspended)return;
            spectatorPhysicsSuspended=false;
            if(Capsule)Capsule.enabled=spectatorCapsuleWasEnabled;
            if(spectatorMoverWasTracked&&isActiveAndEnabled&&!Tracked.Movers.Contains(this))Tracked.Movers.Add(this);
            spectatorMoverWasTracked=false;
        }
    }
}
