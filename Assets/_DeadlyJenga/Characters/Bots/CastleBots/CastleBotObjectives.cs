using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastleBots
{
    public sealed partial class CastleBotBrain
    {
        readonly List<BotObjective> objectives=new List<BotObjective>(32);
        BotObjective selectedObjective;
        bool hasObjective;
        bool objectiveReached,wasObjectiveClimbing;
        float nextObjective,objectiveCommit;
        bool recovering;Vector3 recoveryPoint;float recoveryUntil,nextRecovery,progressHeight,objectiveProgressAt;
        readonly List<Vector3> recoveryStops=new List<Vector3>(8);
        public string ObjectiveId => hasObjective?selectedObjective.id:"";
        public int ObjectivesReached { get; private set; }
        public int RouteFailures { get; private set; }
        public int Climbs { get; private set; }

        void UpdateObjectives()
        {
            if(BotObjectives.Current==null){ClearObjective();return;}
            if(!Motor.Grounded||Motor.Climbing||Motor.Vaulting)return;
            if(Time.time<nextObjective)return;
            nextObjective=Time.time+.4f+Mathf.Repeat(Seed,.3f);
            objectives.Clear();BotObjectives.Current.Collect(gameObject,objectives);
            float best=float.NegativeInfinity;BotObjective choice=default;bool found=false;
            foreach(var candidate in objectives)
            {
                var item=candidate;
                if(item.expiresAt>0&&item.expiresAt<Time.time)continue;
                if(item.kind==BotObjectiveKind.Evade)
                {
                    var away=Vector3.ProjectOnPlane(transform.position-item.position,Vector3.up).normalized;
                    if(away.sqrMagnitude<.1f)away=WanderDirection;
                    item.position=transform.position+away*5;
                }
                float weight=item.kind switch
                {
                    BotObjectiveKind.Guard=>.7f+Personality.survivalPriority*.7f,
                    BotObjectiveKind.Escort=>.5f+Personality.sociability,
                    BotObjectiveKind.Attack=>.4f+Personality.bravery,
                    BotObjectiveKind.Evade=>.5f+Personality.survivalPriority,
                    BotObjectiveKind.Contest=>.7f+Personality.journeyDrive*.6f,
                    _=>.6f+Personality.exploration*.5f
                };
                float assigned=Population.ObjectivePressure(item.id,this);
                float score=item.priority*weight-assigned*ObjectiveCongestion(item.kind)-Mathf.Min(.45f,Vector3.Distance(transform.position,item.position)*.008f);
                score+=Mathf.PerlinNoise(Seed,item.id==null?0:(uint)item.id.GetHashCode()%1000*.01f)*Personality.goofiness;
                if(hasObjective&&item.id==selectedObjective.id)score+=Time.time<objectiveCommit?.35f:.12f;
                if(score>best){best=score;choice=item;found=true;}
            }
            if(!found){ClearObjective();return;}
            choice=CastleBotPopulation.ApplyAssignedPosition(choice,Population.AssignedPosition(choice,this));
            if(!BindExecution(choice,out bool newExecution)){ClearObjective();return;}
            bool newAction=!hasObjective||choice.id!=selectedObjective.id||newExecution;
            bool changed=newAction||!recovering&&Vector3.Distance(choice.position,tripDestination)>.75f;
            selectedObjective=choice;hasObjective=found;
            if(changed)
            {
                objectiveReached=false;
                if(newAction){recovering=false;recoveryStops.Clear();progressHeight=transform.position.y;objectiveProgressAt=Time.time;}
                objectiveCommit=Time.time+Mathf.Lerp(1.5f,4,Personality.journeyDrive);
                if(newAction)SetJourneyDestination(choice.position,Mathf.Max(5,Personality.travelSeconds));
                else {tripDestination=choice.position;tripUntil=Time.time+Mathf.Max(5,Personality.travelSeconds);if(!hasGoal)nextPlan=0;}
                Activity=choice.kind+" · "+choice.id;
            }
            if(found&&choice.kind!=BotObjectiveKind.Evade)
            {
                if(transform.position.y>progressHeight+.5f){progressHeight=transform.position.y;objectiveProgressAt=Time.time;if(Time.time<ascentCommittedUntil)ascentCommittedUntil=Time.time+30;}
                if(recovering&&(Vector3.Distance(transform.position,recoveryPoint)<1||Time.time>recoveryUntil))
                {ResumeTransit(choice.position);}
                var recoveryTarget=recovering?recoveryPoint:choice.position;
                if(CastleBotNavigation.ShouldStageAscent(transform.position,recoveryTarget,recovering&&!transitAscending)&&(Time.time-objectiveProgressAt>8||Motor.HasWalkableIsland&&Motor.StandingSurface&&Motor.StandingSurface.gameObject.layer==0)&&Time.time>=nextRecovery)
                {
                    nextRecovery=Time.time+8;
                    if(CastleBotJourneyPlanner.RecoveryStage(this,recoveryTarget,recoveryStops,out var staging))
                    {
                        if(recoveryStops.Count>=8)recoveryStops.RemoveAt(0);recoveryStops.Add(staging);
                        PushTransit(staging,120);Activity="Finding an open stair approach";
                    }
                }
            }
        }

        void ClearObjective()
        {
            ResetCombat();
            ReleaseExecution("Objective cleared");
            if(!hasObjective)return;
            bool finishStep=Motor&&Motor.Alive&&hasGoal&&goal.surface&&goal.surface.gameObject.activeInHierarchy&&(Motor.Climbing||Motor.Vaulting||!Motor.Grounded);
            hasObjective=false;recovering=false;transitReturns.Clear();planner=null;journey.Clear();
            // Revoke strategic work now; keep a valid physical step until safe footing.
            if(!finishStep)hasGoal=false;
            tripUntil=0;waitUntil=0;nextPlan=0;
        }
        static float ObjectiveCongestion(BotObjectiveKind kind) => kind switch
        {
            BotObjectiveKind.Guard=>.18f, BotObjectiveKind.Escort=>.22f,
            BotObjectiveKind.Contest=>.12f, BotObjectiveKind.Attack=>.06f, _=>.025f
        };

        bool ObjectiveDestination(out Vector3 destination)
        {
            destination=recovering?recoveryPoint:selectedObjective.position;
            if(!hasObjective)return false;
            if(Vector3.Distance(transform.position,destination)<=Mathf.Max(.25f,selectedObjective.radius))
            {
                if(!objectiveReached){ObjectivesReached++;objectiveReached=true;}
                if(selectedObjective.kind==BotObjectiveKind.Guard||selectedObjective.kind==BotObjectiveKind.Contest||selectedObjective.kind==BotObjectiveKind.Escort)
                {waitUntil=Time.time+.45f;return true;}
            }
            return true;
        }
    }
}



