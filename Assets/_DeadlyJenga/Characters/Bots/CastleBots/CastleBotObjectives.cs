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
            if(BotObjectives.Current==null){hasObjective=false;return;}
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
                float score=item.priority*weight-Mathf.Min(.45f,Vector3.Distance(transform.position,item.position)*.008f);
                score+=Mathf.PerlinNoise(Seed,item.id==null?0:(uint)item.id.GetHashCode()%1000*.01f)*Personality.goofiness;
                if(hasObjective&&item.id==selectedObjective.id)score+=Time.time<objectiveCommit?1.5f:.25f;
                if(score>best){best=score;choice=item;found=true;}
            }
            bool changed=found&&(!hasObjective||choice.id!=selectedObjective.id||Vector3.Distance(choice.position,selectedObjective.position)>2);
            selectedObjective=choice;hasObjective=found;
            if(changed)
            {
                objectiveReached=false;recovering=false;recoveryStops.Clear();progressHeight=transform.position.y;objectiveProgressAt=Time.time;
                objectiveCommit=Time.time+Mathf.Lerp(1.5f,4,Personality.journeyDrive);
                SetJourneyDestination(choice.position,Mathf.Max(5,Personality.travelSeconds));
                Activity=choice.kind+" · "+choice.id;
            }
            if(found&&Motor.CarryingFlag&&choice.kind==BotObjectiveKind.Travel)
            {
                if(transform.position.y>progressHeight+.5f){progressHeight=transform.position.y;objectiveProgressAt=Time.time;}
                if(recovering&&(Vector3.Distance(transform.position,recoveryPoint)<1||Time.time>recoveryUntil))
                {recovering=false;SetJourneyDestination(choice.position,60);objectiveProgressAt=Time.time;}
                if(!recovering&&choice.position.y>transform.position.y+3&&(Time.time-objectiveProgressAt>8||Motor.HasWalkableIsland&&Motor.StandingSurface&&Motor.StandingSurface.gameObject.layer==0)&&Time.time>=nextRecovery)
                {
                    nextRecovery=Time.time+8;
                    if(CastleBotJourneyPlanner.RecoveryStage(this,choice.position,recoveryStops,out var staging))
                    {
                        if(recoveryStops.Count>=8)recoveryStops.RemoveAt(0);recoveryStops.Add(staging);
                        recoveryPoint=staging;recoveryUntil=Time.time+120;recovering=true;SetJourneyDestination(staging,120);Activity="Finding an open stair approach";
                    }
                }
            }
            if(found&&choice.kind==BotObjectiveKind.Attack&&choice.subject)
            {
                Vector3 delta=choice.position+Vector3.up*Motor.StandingHeight*.5f-Motor.ViewPose.position;
                if(delta.magnitude<Motor.StandingHeight*1.8f)
                    Motor.RequestPunch(Motor.ViewPose.position,delta,2+Personality.bravery);
            }
        }

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



