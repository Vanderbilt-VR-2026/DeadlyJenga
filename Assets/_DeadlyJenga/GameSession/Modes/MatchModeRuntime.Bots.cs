using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    public sealed partial class MatchModeRuntime
    {
        readonly List<Vector3> supportGoals=new List<Vector3>();
        readonly List<Vector3> supportCandidates=new List<Vector3>();
        readonly List<Vector3> searchStages=new List<Vector3>(8);
        float nextSupportGoals;
        IWorldQuery supportWorld;
        void RefreshSupportGoals()
        {
            if(!ReferenceEquals(supportWorld,Services.World)){supportWorld=Services.World;nextSupportGoals=0;}
            if(Time.time<nextSupportGoals)return;nextSupportGoals=Time.time+5;supportCandidates.Clear();supportGoals.Clear();
            var world=Services.World;if(world==null)return;
            foreach(var body in world.Blocks)
            {
                if(!body||!body.gameObject.activeInHierarchy||body.linearVelocity.sqrMagnitude>9)continue;
                var collider=body.GetComponent<Collider>();if(!collider)continue;
                var bounds=collider.bounds;var ray=new Ray(new Vector3(bounds.center.x,bounds.max.y+.5f,bounds.center.z),Vector3.down);
                if(!collider.Raycast(ray,out var hit,bounds.size.y+1)||hit.normal.y<=.65f||hit.point.y<=world.WaterHeight(hit.point)+.2f)continue;
                if(Physics.CheckCapsule(hit.point+Vector3.up*.38f,hit.point+Vector3.up*1.55f,.3f,(1<<Layers.Block)|(1<<Layers.Surface)|1,QueryTriggerInteraction.Ignore))continue;
                supportCandidates.Add(hit.point+Vector3.up*.05f);
            }
            supportCandidates.Sort((a,b)=>b.y.CompareTo(a.y));
            foreach(var p in supportCandidates)
            {
                bool crowded=false;foreach(var selected in supportGoals)if((selected-p).sqrMagnitude<16){crowded=true;break;}
                if(!crowded)supportGoals.Add(p);if(supportGoals.Count>=12)break;
            }
        }
        public void Collect(GameObject bot,List<BotObjective> goals)
        {
            if(!Authority||!bot||goals==null||!actorIds.TryGetValue(bot,out string id)||simulation.Phase!=RoundPhase.Playing)return;
            var self=simulation.Find(id);if(self==null||!self.frame.alive||self.frame.dying||self.frame.role=="hand"||self.frame.role=="spectator")return;
            if(CollectHazardTactics(FindActor(id),goals))return;
            if(CollectMystery(id,goals))return;
            var rules=Rules;
            if(rules.mode==GameModeKind.KingOfHill)
                AddGoal(goals,"hill",BotObjectiveKind.Contest,simulation.Hill,null,rules.hillRadius,.95f);
            if(rules.mode==GameModeKind.BlockControl)
            {
                int n=0;
                foreach(var pair in blockIds)
                {
                    if(!pair.Key||simulation.OwnerOf(pair.Value)==self.frame.team)continue;
                    var collider=pair.Key.GetComponent<Collider>();if(!collider)continue;
                    Vector3 point=collider.bounds.center+Vector3.up*(collider.bounds.extents.y+.05f);
                    if((point-bot.transform.position).sqrMagnitude>40*40)continue;
                    AddGoal(goals,"claim:"+pair.Value,BotObjectiveKind.Contest,point,pair.Key.gameObject,1,.75f);
                    if(++n>=24)break;
                }
            }
            if(rules.mode==GameModeKind.CaptureFlag)
            {
                bool carrying=simulation.CarriesFlag(id);
                if(carrying)
                {
                    foreach(var flag in simulation.Flags)if(flag.team==self.frame.team)AddGoal(goals,"return",BotObjectiveKind.Travel,flag.home,null,1.2f,1.5f);
                    return;
                }
                int hash=0;foreach(char c in id)hash=unchecked(hash*31+c);
                bool defender=(hash&int.MaxValue)%3==0;
                string firstRecoveryBot=null;
                foreach(var member in simulation.Participants)
                    if(member.present&&member.frame.bot&&member.frame.alive&&!member.frame.dying&&member.frame.role!="hand"&&member.frame.team==self.frame.team&&!simulation.CarriesFlag(member.frame.id)&&(firstRecoveryBot==null||string.CompareOrdinal(member.frame.id,firstRecoveryBot)<0))firstRecoveryBot=member.frame.id;
                if(defender||id==firstRecoveryBot)
                {
                    foreach(var ownFlag in simulation.Flags)
                    {
                        if(ownFlag.team!=self.frame.team||ownFlag.atHome||ownFlag.carrier==null)continue;
                        var thief=simulation.Find(ownFlag.carrier);if(thief==null||thief.frame.team==self.frame.team)continue;
                        var thiefActor=FindActor(ownFlag.carrier);
                        bool seen=thiefActor!=null&&!thief.frame.dying&&VisibleTo(self.frame.position,thief.frame.position);
                        if(seen){AddGoal(goals,"recover-visible:"+ownFlag.team,BotObjectiveKind.Attack,thief.frame.position,thiefActor.gameObject,1,2);return;}
                        ModeCtfRecovery.PublicSearch(self.frame.team,true,false,simulation.Flags,searchStages);
                        for(int i=0;i<searchStages.Count;i++)AddGoal(goals,"recover-base:"+i,BotObjectiveKind.Travel,searchStages[i],null,2,1.3f);
                        if(searchStages.Count>0)return;
                    }
                }
                foreach(var flag in simulation.Flags)
                {
                    if(flag.team==self.frame.team)AddGoal(goals,"guard-home",BotObjectiveKind.Guard,flag.home,null,2.5f,defender?.8f:.35f);
                    if(flag.carrier==null && (flag.team!=self.frame.team||!flag.atHome))AddGoal(goals,"flag:"+flag.team,BotObjectiveKind.Travel,flag.position,null,.9f,flag.team==self.frame.team?1.3f:defender?.65f:1.05f);
                    else if(flag.carrier!=null&&flag.carrier!=id)
                    {
                        var carrier=simulation.Find(flag.carrier);var actor=FindActor(flag.carrier);
                        if(carrier==null||carrier.frame.dying||actor==null)continue;
                        bool friendly=carrier.frame.team==self.frame.team;
                        if(!friendly&&!VisibleTo(self.frame.position,carrier.frame.position))continue;
                        AddGoal(goals,"carrier:"+flag.team,friendly?BotObjectiveKind.Escort:BotObjectiveKind.Attack,carrier.frame.position,actor.gameObject,friendly?2.5f:1,friendly?defender?.5f:.8f:1.4f);
                    }
                }
                ModeCtfRecovery.FilterRoleGoals(defender,goals);
            }
            if(rules.mode==GameModeKind.TeamTickets||rules.mode==GameModeKind.Infection)
            {
                bool visibleAttack=false;
                int survivors=0;foreach(var p in simulation.Participants)if(p.frame.alive&&p.frame.role!="infected")survivors++;
                foreach(var p in simulation.Participants)
                {
                    if(p==self||!p.present||!p.frame.alive||p.frame.dying||p.frame.role=="hand"||p.frame.role=="spectator")continue;
                    var other=FindActor(p.frame.id);if(other==null)continue;
                    bool infected=p.frame.role=="infected",selfInfected=self.frame.role=="infected";
                    bool hostile=rules.mode==GameModeKind.Infection?infected!=selfInfected:p.frame.team!=self.frame.team;
                    if(!hostile)continue;
                    bool publicBlip=rules.mode==GameModeKind.Infection&&(!selfInfected||survivors<=2);
                    if(!publicBlip&&!VisibleTo(self.frame.position,p.frame.position))continue;
                    AddGoal(goals,"enemy:"+p.frame.id,!selfInfected&&rules.mode==GameModeKind.Infection?BotObjectiveKind.Evade:BotObjectiveKind.Attack,p.frame.position,other.gameObject,1.3f,.85f);
                    if(rules.mode==GameModeKind.TeamTickets||selfInfected)visibleAttack=true;
                }
                if(rules.mode==GameModeKind.TeamTickets&&!visibleAttack&&Services.World!=null)
                {
                    RefreshSupportGoals();ModeSearchGeometry.Midfield(self.frame.position,Services.World.Center,supportCandidates,false,searchStages);
                    for(int i=0;i<searchStages.Count;i++)AddGoal(goals,"midfield:"+i,BotObjectiveKind.Contest,searchStages[i],null,2.5f,.65f);
                }
                if(rules.mode==GameModeKind.Infection&&self.frame.role=="infected"&&!visibleAttack)
                {
                    RefreshSupportGoals();ModeSearchGeometry.Select(self.frame.position,supportCandidates,false,searchStages);
                    var actor=FindActor(id);float height=actor==null?1.8f:Mathf.Clamp(actor.ViewPose.position.y-self.frame.position.y+.2f,.8f,6);
                    float radius=Mathf.Min(.3f,height*.2f);
                    for(int i=0;i<searchStages.Count;i++)
                    {
                        var point=searchStages[i];
                        if(Physics.CheckCapsule(point+Vector3.up*(radius+.06f),point+Vector3.up*(height-radius),radius,(1<<Layers.Block)|(1<<Layers.Surface)|1,QueryTriggerInteraction.Ignore))continue;
                        AddGoal(goals,"search:"+Mathf.RoundToInt(point.x)+":"+Mathf.RoundToInt(point.y)+":"+Mathf.RoundToInt(point.z),BotObjectiveKind.Travel,point,null,1,.65f);
                    }
                }
            }
            if(rules.mode==GameModeKind.Survival||rules.mode==GameModeKind.Infection&&self.frame.role!="infected")
            {
                RefreshSupportGoals();
                for(int i=0;i<supportGoals.Count;i++)AddGoal(goals,"high-ground:"+i,BotObjectiveKind.Travel,supportGoals[i],null,1.5f,.75f-i*.015f);
                if(supportGoals.Count==0)AddGoal(goals,"high-ground",BotObjectiveKind.Travel,simulation.Hill,null,2,.6f);
            }
        }
        static bool VisibleTo(Vector3 from,Vector3 target)
        {
            Vector3 delta=target+Vector3.up*.6f-(from+Vector3.up*.6f);
            return delta.sqrMagnitude<30*30&&!Physics.Raycast(from+Vector3.up*.6f,delta.normalized,delta.magnitude,(1<<Layers.Block)|(1<<Layers.Surface),QueryTriggerInteraction.Ignore);
        }
        static void AddGoal(List<BotObjective> goals,string id,BotObjectiveKind kind,Vector3 point,GameObject subject,float radius,float priority)
        {goals.Add(new BotObjective{id=id,kind=kind,position=point,subject=subject,radius=radius,priority=priority,expiresAt=Time.time+1});}
    }
}
