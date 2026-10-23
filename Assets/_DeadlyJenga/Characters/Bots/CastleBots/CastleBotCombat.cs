using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastleBots
{
    public enum BotCombatState { None, Approach, Flank, Face, Windup, Recover }

    public static class BotCombatPolicy
    {
        // MatchModeRuntime's ordinary punch uses this fixed world-space reach and cone.
        public static bool InReach(Vector3 origin,Vector3 contact) => (contact-origin).sqrMagnitude<2.05f*2.05f;
        public static bool Facing(Vector3 forward,Vector3 direction)
        {
            forward=Vector3.ProjectOnPlane(forward,Vector3.up);direction=Vector3.ProjectOnPlane(direction,Vector3.up);
            return direction.sqrMagnitude>.0001f&&Vector3.Dot(forward.normalized,direction.normalized)>.94f;
        }
        public static bool ShieldFront(Vector3 attacker,Vector3 target,Vector3 forward)
            => Vector3.Dot(Vector3.ProjectOnPlane(attacker-target,Vector3.up).normalized,Vector3.ProjectOnPlane(forward,Vector3.up).normalized)>=.45f;
        public static Vector3 Lead(Vector3 velocity) => Vector3.ClampMagnitude(Vector3.ProjectOnPlane(velocity,Vector3.up)*.16f,.6f);
    }

    public sealed partial class CastleBotBrain
    {
        const int CombatMask=(1<<Layers.Block)|(1<<Layers.Surface);
        static readonly Unity.Profiling.ProfilerMarker CombatMarker=new Unity.Profiling.ProfilerMarker("DeadlyJenga.Bots.Combat");
        public BotCombatState CombatState { get; private set; }
        public int PunchAttempts { get; private set; }
        public int PunchesAccepted { get; private set; }
        public int PunchesWithheld { get; private set; }
        public string CombatReason { get; private set; }="No attack assignment";
        GameObject combatSubject;
        long combatToken;
        float combatReady,combatWindup=-1,nextCombatSample,nextCombatStep,combatSeenAt,combatRepositionUntil;
        Vector3 combatSeen,combatVelocity,combatStep;
        bool hasCombatStep;
        readonly Collider[] combatBodies=new Collider[64];

        void ResetCombat()
        {
            combatSubject=null;combatToken=0;combatWindup=-1;combatSeenAt=0;hasCombatStep=false;combatRepositionUntil=0;
            combatVelocity=Vector3.zero;CombatState=BotCombatState.None;CombatReason="No valid visible attack target";
        }
        bool ReadCombatCommand(ref CastleBotCommand command)
        {
            using(CombatMarker.Auto())return BuildCombatCommand(ref command);
        }
        bool BuildCombatCommand(ref CastleBotCommand command)
        {
            var rules=MatchRuntime.Current?.RulesFor(gameObject);
            if(!hasObjective||selectedObjective.kind!=BotObjectiveKind.Attack||!selectedObjective.subject||!Motor.PunchEnabled||rules?.punchEnabled==false||Motor.CarryingFlag||Motor.Climbing||Motor.Vaulting||!Motor.Grounded)
            {ResetCombat();return false;}
            var subject=selectedObjective.subject;
            var target=subject.GetComponent<IModeActor>();
            var targetRules=MatchRuntime.Current?.RulesFor(subject);
            if(target==null||!subject.activeInHierarchy||!target.Alive||targetRules?.downed==true||targetRules!=null&&targetRules.moveScale<=0||Motor.Team>=0&&target.Team==Motor.Team)
            {ResetCombat();return false;}
            Vector3 feet=target.transform.position,contact=feet+Vector3.up*.6f,origin=Motor.ViewPose.position;
            Vector3 aim=contact-origin;
            if(Vector3.Distance(transform.position,feet)>7||Physics.Raycast(origin,aim.normalized,aim.magnitude,CombatMask,QueryTriggerInteraction.Ignore))
            {ResetCombat();return false;}
            if(combatSubject!=subject||combatToken!=ObjectiveAssignment)
            {
                ResetCombat();combatSubject=subject;combatToken=ObjectiveAssignment;
                combatReady=Time.time+Mathf.Lerp(.16f,.3f,Mathf.Repeat(Seed,.97f));
                nextCombatSample=0;nextCombatStep=Time.time+Mathf.Repeat(Seed,.12f);combatSeen=feet;combatSeenAt=Time.time;
            }
            if(Time.time>=nextCombatSample)
            {
                float elapsed=Time.time-combatSeenAt;
                if(elapsed>.01f)combatVelocity=Vector3.ClampMagnitude((feet-combatSeen)/elapsed,8);
                combatSeen=feet;combatSeenAt=Time.time;nextCombatSample=Time.time+.12f;
            }
            bool shield=targetRules?.carryingFlag==true&&BotCombatPolicy.ShieldFront(transform.position,feet,target.ViewPose.rotation*Vector3.forward);
            if(Time.time>=nextCombatStep)
            {
                nextCombatStep=Time.time+.24f;
                hasCombatStep=FindCombatStep(feet,target.ViewPose.rotation*Vector3.forward,targetRules?.carryingFlag==true,out combatStep);
            }
            bool range=BotCombatPolicy.InReach(origin,contact);
            // Reaching an opponent does not require walking to their floor or jumping after them.
            if((!range||shield)&&(!hasCombatStep||Mathf.Abs(feet.y-transform.position.y)>.65f))
            {combatWindup=-1;CombatState=BotCombatState.Approach;CombatReason="Need a physical route";return false;}
            command.jump=false;command.climbHeld=false;command.look=aim;
            float flat=Vector3.ProjectOnPlane(feet-transform.position,Vector3.up).magnitude;
            bool aligned=BotCombatPolicy.Facing(Motor.ViewPose.rotation*Vector3.forward,aim);
            Vector3 step=Vector3.ProjectOnPlane(combatStep-transform.position,Vector3.up);
            bool reposition=hasCombatStep&&(shield||!range||flat<.75f||Time.time<combatRepositionUntil);
            command.move=reposition?Vector3.ClampMagnitude(step*2,1):Vector3.zero;
            if(reposition)
            {combatWindup=-1;CombatState=shield?BotCombatState.Flank:BotCombatState.Approach;CombatReason=shield?"Move around carrier shield":"Establish striking distance";return true;}
            if(!aligned)
            {combatWindup=-1;CombatState=BotCombatState.Face;CombatReason="Align actual view with target";return true;}
            if(Time.time<combatReady)
            {combatWindup=-1;CombatState=BotCombatState.Recover;CombatReason="Reaction or punch recovery";return true;}
            if((combatWindup<0||Time.time>=combatWindup)&&!ClearPunch(target,origin,aim))
            {combatWindup=-1;PunchesWithheld++;CombatState=BotCombatState.Face;CombatReason="Another actor intercepts the punch";nextCombatStep=Time.time+.1f;combatReady=Time.time+.2f;combatRepositionUntil=Time.time+.6f;return true;}
            if(combatWindup<0)combatWindup=Time.time+Mathf.Lerp(.12f,.2f,Mathf.Repeat(Seed,.83f));
            CombatState=BotCombatState.Windup;CombatReason="Wind up, rechecking range and line of sight";
            if(Time.time<combatWindup)return true;
            PunchAttempts++;
            if(Motor.RequestPunch(origin,aim,2+Personality.bravery))PunchesAccepted++;
            combatReady=Time.time+Mathf.Max(.2f,Motor.punchCooldown)+Mathf.Lerp(.04f,.16f,Mathf.Repeat(Seed,.71f));
            combatWindup=-1;CombatState=BotCombatState.Recover;CombatReason="Strike requested through ordinary motor";
            return true;
        }

        bool ClearPunch(IModeActor desired,Vector3 origin,Vector3 aim)
        {
            int count=Physics.OverlapSphereNonAlloc(origin,2.2f,combatBodies,1<<Layers.Survivor,QueryTriggerInteraction.Collide);
            if(count==combatBodies.Length)return false;
            float nearest=(desired.transform.position+Vector3.up*.6f-origin).magnitude;
            for(int i=0;i<count;i++)
            {
                var actor=combatBodies[i].GetComponentInParent<IModeActor>();
                if(actor==null||actor.gameObject==gameObject||actor.gameObject==desired.gameObject||!actor.Alive||MatchRuntime.Current?.RulesFor(actor.gameObject)?.downed==true)continue;
                var delta=actor.transform.position+Vector3.up*.6f-origin;float distance=delta.magnitude;
                if(distance>=nearest||distance<.01f||Vector3.Dot(delta/distance,aim.normalized)<.45f)continue;
                if(!Physics.Raycast(origin,delta/distance,distance,CombatMask,QueryTriggerInteraction.Ignore))return false;
            }
            return true;
        }
        bool FindCombatStep(Vector3 feet,Vector3 facing,bool carrier,out Vector3 point)
        {
            point=default;float best=float.PositiveInfinity;
            Vector3 radial=Vector3.ProjectOnPlane(transform.position-feet,Vector3.up).normalized;
            if(radial.sqrMagnitude<.1f)radial=-Vector3.ProjectOnPlane(facing,Vector3.up).normalized;
            if(radial.sqrMagnitude<.1f)radial=WanderDirection;
            Vector3 lead=BotCombatPolicy.Lead(combatVelocity);
            for(int i=0;i<8;i++)
            {
                float angle=i==0?0:((i+1)/2)*45*(i%2==1?1:-1);
                Vector3 candidate=feet+lead+Quaternion.Euler(0,angle,0)*radial*1.2f;
                Vector3 segment=Vector3.ProjectOnPlane(candidate-transform.position,Vector3.up);
                float along=segment.sqrMagnitude>.001f?Mathf.Clamp01(Vector3.Dot(Vector3.ProjectOnPlane(feet-transform.position,Vector3.up),segment)/segment.sqrMagnitude):0;
                if(Vector3.ProjectOnPlane(transform.position-feet,Vector3.up).sqrMagnitude>.75f*.75f&&Vector3.ProjectOnPlane(transform.position+segment*along-feet,Vector3.up).sqrMagnitude<.65f*.65f)continue;
                if(!CastleBotNavigation.Ground(candidate+Vector3.up*.5f,1,out var ground)||Mathf.Abs(ground.point.y-transform.position.y)>.45f||!CastleBotNavigation.Room(Motor,ground.point+Vector3.up*.04f)||!CastleBotNavigation.Walkable(Motor,transform.position,ground.point))continue;
                if(Services.World!=null&&ground.point.y<=Services.World.WaterHeight(ground.point)+.15f&&!(Motor.HasWalkableIsland&&ground.collider.gameObject.layer==0))continue;
                float cost=Vector3.Distance(transform.position,ground.point)+Population.Crowding(ground.point,this)*2;
                if(Time.time<combatRepositionUntil&&i==0)cost+=4;
                if(carrier&&BotCombatPolicy.ShieldFront(ground.point,feet,facing))cost+=5;
                if(cost<best){best=cost;point=ground.point;}
            }
            return best<float.PositiveInfinity;
        }
    }
}
