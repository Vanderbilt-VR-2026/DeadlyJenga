using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;

namespace DeadlyJenga.CastleBots
{
    public enum BotActionKind { Explore, Travel, Guard, Escort, Attack, Evade, Contest, Recover, Disabled }
    public enum BotMovementState { Moving, Traversing, Holding, Planning, Recovering, Blocked, Disabled }

    public struct BotTraversalProgress
    {
        Vector3 sampledFeet;
        float sampledAt;
        bool sampled;
        public float StalledFor { get; private set; }
        public void Reset(){sampled=false;StalledFor=0;}
        public void Observe(Vector3 feet,bool grounded,float now,float tolerance)
        {
            if(!sampled||grounded||now<sampledAt)
            {sampled=true;sampledFeet=feet;sampledAt=now;StalledFor=0;return;}
            if(now-sampledAt<.5f)return;
            StalledFor=Vector3.Distance(feet,sampledFeet)>=tolerance?0:StalledFor+now-sampledAt;
            sampledFeet=feet;sampledAt=now;
        }
    }

    public sealed partial class CastleBotBrain
    {
        struct Transit { public Vector3 destination; public float until; public bool recovering,ascending; }
        bool transitAscending;
        BotTraversalProgress traversalProgress;
        float ascentCommittedUntil;
        readonly Stack<Transit> transitReturns=new Stack<Transit>();
        void PushTransit(Vector3 destination,float seconds)
        {
            if(transitReturns.Count>=8)transitReturns.Clear();
            transitReturns.Push(new Transit{destination=tripDestination,until=Mathf.Max(10,tripUntil-Time.time),recovering=recovering,ascending=transitAscending});
            transitAscending=destination.y>transform.position.y+.5f;
            recovering=true;recoveryPoint=destination;recoveryUntil=Time.time+seconds;
            SetRouteDestination(destination,seconds);
        }
        void ResumeTransit(Vector3 objective)
        {
            if(transitAscending&&transform.position.y>=recoveryPoint.y-.4f)ascentCommittedUntil=Time.time+30;
            if(transitReturns.Count>0)
            {
                var parent=transitReturns.Pop();recovering=parent.recovering;transitAscending=parent.ascending;recoveryPoint=parent.destination;
                recoveryUntil=Time.time+parent.until;SetRouteDestination(parent.destination,parent.until);
            }
            else {recovering=false;transitAscending=false;SetRouteDestination(objective,60);}
            nextDeparture=0;progressHeight=transform.position.y;objectiveProgressAt=Time.time;
        }
        public bool AcceptsSocialMovement => !hasObjective&&(MatchRuntime.Current==null||MatchRuntime.Current.Rules.mode==GameModeKind.Demo);
        public BotActionKind CurrentAction { get; private set; }
        public BotMovementState MovementState { get; private set; }
        public string StationaryReason { get; private set; } = "Starting";
        public string LastTransition { get; private set; } = "Spawned";
        public float PlannerAge => planner == null ? 0 : planner.Age;
        public int Recoveries { get; private set; }
        public float MovingSeconds { get; private set; }
        public float UnexplainedIdleSeconds { get; private set; }
        public float LongestIdle { get; private set; }
        public float DesiredSpeed { get; private set; }
        public float ActualSpeed { get; private set; }
        public float ActionStartedAt { get; private set; }
        internal bool PlanningPending => planner!=null&&!planner.Complete&&Motor&&Motor.Grounded&&!Motor.Vaulting&&!Motor.Climbing;
        internal float PlanningPriority => Time.time-lastPlanningService+(Motor.CarryingFlag?.5f:0)+(hasGoal?0:.25f);
        string actionKey="";
        float lastPlanningService,nextDeparture;
        internal void AdvancePlanning()
        {
            if(!PlanningPending)return;
            int before=BotPlanningBudget.EdgesUsed;
            planner.Advance(12);
            if(BotPlanningBudget.EdgesUsed!=before||planner.Complete)lastPlanningService=Time.time;
        }
        float nextFallback, idleFor, nextRecoveryAttempt, progressSampleAt, progressDistance;
        Vector3 lastFeet, steeringVelocity;
        bool observedFeet;

        void Transition(BotActionKind action, string reason)
        {
            if(CurrentAction==action&&actionKey==reason)return;
            actionKey=reason;
            LastTransition = CurrentAction + " -> " + action + ": " + reason;
            CurrentAction = action; ActionStartedAt = Time.time;
        }
        bool TacticalHold => hasObjective && !recovering &&
            (selectedObjective.kind == BotObjectiveKind.Travel || selectedObjective.kind == BotObjectiveKind.Guard || selectedObjective.kind == BotObjectiveKind.Contest || selectedObjective.kind == BotObjectiveKind.Escort) &&
            Vector3.Distance(transform.position, selectedObjective.position) <= Mathf.Max(.35f, selectedObjective.radius * .75f);

        public CastleBotCommand ReadCommand(CastleSurvivor motor, float dt)
        {
            ValidateExecutionLease();
            if(observedFeet)ActualSpeed=Vector3.Distance(transform.position,lastFeet)/Mathf.Max(.001f,dt);
            if(observedFeet&&Vector3.Distance(transform.position,lastFeet)>Mathf.Max(3,motor.EffectiveWalkSpeed*dt*4))
            {planner=null;journey.Clear();hasGoal=false;waitUntil=0;nextPlan=0;steeringVelocity=Vector3.zero;idleFor=0;}
            lastFeet=transform.position;observedFeet=true;
            var rules=MatchRuntime.Current?.RulesFor(gameObject);
            if(!motor.Alive || rules!=null&&rules.moveScale<=0 || !isActiveAndEnabled || Respawning)
            {
                ResetCombat();
                ReleaseExecution("Round or life state prevents execution");
                Transition(BotActionKind.Disabled,"Round or life state prevents movement");
                MovementState=BotMovementState.Disabled;StationaryReason="Round or life state";idleFor=0;DesiredSpeed=0;return default;
            }
            traversalProgress.Observe(transform.position,motor.Grounded,Time.time,Mathf.Clamp(motor.EffectiveWalkSpeed*.04f,.03f,.12f));
            var command=ReadMovementCommand(motor,dt);
            bool fighting=ReadCombatCommand(ref command);
            var action=BotActionKind.Explore;
            // The Core enum order is not a behavior contract.
            if(hasObjective)action=selectedObjective.kind switch
            { BotObjectiveKind.Guard=>BotActionKind.Guard, BotObjectiveKind.Escort=>BotActionKind.Escort,
              BotObjectiveKind.Attack=>BotActionKind.Attack, BotObjectiveKind.Evade=>BotActionKind.Evade,
              BotObjectiveKind.Contest=>BotActionKind.Contest, _=>BotActionKind.Travel };
            if(recovering)action=BotActionKind.Recover;
            Transition(action,ObjectiveId);
            if(TacticalHold && motor.Grounded && Population.ThreatCost(transform.position)<1)
            {command.move=Vector3.zero;command.jump=false;command.look=(Services.World?.Center??selectedObjective.position)-transform.position;StationaryReason="Holding assigned objective area";MovementState=BotMovementState.Holding;idleFor=0;DesiredSpeed=0;PublishExecution();return command;}
            if(motor.Grounded&&!motor.Vaulting&&!motor.Climbing&&!command.jump&&(!PrecisionTraversal||fighting))
                command.move=Steer(command.move,dt);
            DesiredSpeed=command.move.magnitude*motor.EffectiveWalkSpeed;
            if(fighting&&command.move.sqrMagnitude<.001f)
            {MovementState=BotMovementState.Holding;StationaryReason=CombatReason;idleFor=0;}
            else if(motor.Vaulting||motor.Climbing||!motor.Grounded)
            {
                if(traversalProgress.StalledFor>=2.5f)
                {
                    idleFor+=dt;LongestIdle=Mathf.Max(LongestIdle,idleFor);
                    MovementState=BotMovementState.Blocked;StationaryReason="Traversal has no physical progress";
                    if(idleFor>2)UnexplainedIdleSeconds+=dt;
                    if(Time.time>=nextRecoveryAttempt)RecoverMovement();
                }
                else {MovementState=BotMovementState.Traversing;StationaryReason="Traversal in progress";idleFor=0;}
            }
            else if(ActualSpeed>.15f)
            {MovementState=BotMovementState.Moving;StationaryReason="";MovingSeconds+=dt;idleFor=0;}
            else if(Time.time<waitUntil&&!hasObjective)
            {MovementState=BotMovementState.Holding;StationaryReason="Exploration rest";idleFor=0;}
            else
            {
                idleFor+=dt;LongestIdle=Mathf.Max(LongestIdle,idleFor);
                MovementState=planner!=null?BotMovementState.Planning:BotMovementState.Blocked;
                StationaryReason=planner!=null?"Route request pending":"No usable local route";
                if(idleFor>2)UnexplainedIdleSeconds+=dt;
                if(idleFor>1.5f&&(planner==null||planner.Age>2.2f)&&Time.time>=nextRecoveryAttempt)RecoverMovement();
            }
            // Detect pacing as well as immobility. Traversals get their own completion deadline.
            if(Time.time>=progressSampleAt && motor.Grounded && !TacticalHold)
            {
                float distance=Vector3.Distance(transform.position,tripDestination);
                if(progressSampleAt>0&&hasObjective&&distance>3&&distance>=progressDistance-.25f&&Time.time>=nextRecoveryAttempt)RecoverMovement();
                progressDistance=distance;progressSampleAt=Time.time+8;
            }
            PublishExecution();
            return command;
        }

        void RecoverMovement()
        {
            Recoveries++;RouteFailures++;nextRecoveryAttempt=Time.time+2.5f;
            LastTransition="Movement recovery: "+StationaryReason;
            MovementState=BotMovementState.Recovering;
            planner=null;journey.Clear();hasGoal=false;waitUntil=0;nextPlan=0;nextFallback=0;
            WanderDirection=Quaternion.Euler(0,73+Recoveries*17%90,0)*WanderDirection;
            if(recentStops.Count>=16){recentStops.RemoveAt(0);stopTimes.RemoveAt(0);}
            recentStops.Add(transform.position);stopTimes.Add(Time.time);
            if(CastleBotNavigation.GroundStep(this,out goal))
            {hasGoal=true;goalSince=Time.time;Intent="Recovering a safe approach";}
            else LocalFallback();
        }

        Vector3 Steer(Vector3 desired,float dt)
        {
            if(desired.sqrMagnitude<.001f){steeringVelocity=Vector3.zero;return desired;}
            Vector3 separation=Vector3.zero;int considered=0;
            foreach(var other in Population.Bots)
            {
                if(!other||other==this||other.Respawning||Mathf.Abs(other.transform.position.y-transform.position.y)>.45f)continue;
                var delta=Vector3.ProjectOnPlane(transform.position-other.transform.position,Vector3.up);
                float distance=delta.magnitude;
                if(distance>.9f)continue;
                if(++considered>12)break;
                if(distance>.02f)
                {
                    separation+=delta/distance*(.9f-distance);
                    if(Vector3.Dot(other.transform.forward,desired.normalized)<-.5f&&Mathf.Abs(Vector3.Dot(delta/distance,desired.normalized))>.7f)
                        separation+=Vector3.Cross(Vector3.up,desired.normalized)*.4f;
                }
            }
            var candidate=Vector3.ClampMagnitude(desired+Vector3.ClampMagnitude(separation,.5f),desired.magnitude);
            float step=Mathf.Max(.2f,Motor.EffectiveWalkSpeed*dt*1.5f);
            var end=transform.position+candidate.normalized*step;
            if(CastleBotNavigation.Walkable(Motor,transform.position,end))desired=candidate;
            steeringVelocity=Vector3.MoveTowards(steeringVelocity,desired,dt*8);
            end=transform.position+steeringVelocity.normalized*step;
            if(!CastleBotNavigation.Walkable(Motor,transform.position,end))steeringVelocity=desired;
            return steeringVelocity;
        }
        void SmoothWalkingRoute()
        {
            if(journey.Count==0)return;
            var next=journey.Peek();
            if(next.route!=CastleBotRoute.Walk||!next.surface||Vector3.Distance(transform.position,next.Point)>4)return;
            if(CastleBotNavigation.Landing(Motor,next.Point,out _) && CastleBotNavigation.Walkable(Motor,transform.position,next.Point))
            {goal=journey.Dequeue();goalSince=Time.time;}
        }
    }
}
