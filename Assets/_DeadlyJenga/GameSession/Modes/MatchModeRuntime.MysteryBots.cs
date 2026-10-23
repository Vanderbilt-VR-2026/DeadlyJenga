using System.Collections.Generic;
using System.Linq;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    public sealed partial class MatchModeRuntime
    {
        readonly Dictionary<string,HashSet<string>> witnessedMurderers=new Dictionary<string,HashSet<string>>();
        readonly HashSet<string> unavailableBadges=new HashSet<string>();
        float nextMysteryBots;
        void TickMysteryBots()
        {
            if(Time.time<nextMysteryBots)return;nextMysteryBots=Time.time+.25f;
            var clues=mystery.Public();
            foreach(var p in simulation.Participants)
            {
                if(!p.present||!p.frame.bot||!mystery.Alive(p.frame.id)||mystery.IsDowned(p.frame.id))continue;
                string id=p.frame.id;
                var role=mystery.Role(id);
                if(!witnessedMurderers.TryGetValue(id,out var known)){known=new HashSet<string>();witnessedMurderers[id]=known;}
                foreach(var strike in mysteryStrikes)if(strike.Value>Time.time&&mystery.Role(strike.Key)==MysteryRole.Murderer&&strike.Key!=id)
                {var attacker=simulation.Find(strike.Key);if(attacker!=null&&(attacker.frame.position-p.frame.position).sqrMagnitude<225&&VisibleTo(p.frame.position,attacker.frame.position))known.Add(strike.Key);}
                foreach(var body in clues.bodies)if(Close(id,body.position))
                {
                    mystery.Report(id,body.id,p.frame.color);
                    if(body.sheriff&&role==MysteryRole.Innocent&&!mystery.RecoverSheriff(id,body.id))unavailableBadges.Add(id+"/"+body.id);
                    if(body.reported&&role!=MysteryRole.Murderer)
                        for(int t=trailFrames.Count-1;t>=Mathf.Max(0,trailFrames.Count-128);t--)
                        {
                            var trail=trailFrames[t];var blocks=Services.World?.Blocks;
                            if(blocks==null||trail.block<0||trail.block>=blocks.Count||!blocks[trail.block]||trail.color==p.frame.color)continue;
                            var point=blocks[trail.block].transform.TransformPoint(trail.point);
                            if((point-body.position).sqrMagnitude<=Rules.mystery.trailRadius*Rules.mystery.trailRadius&&Close(id,point))
                            {ReceiveMysteryCommand(id,"suspect:"+trail.sequence+":"+body.id);break;}
                        }
                }
                if(role!=MysteryRole.Murderer)foreach(var target in simulation.Participants)if(mystery.IsDowned(target.frame.id)&&Close(id,target.frame.position))ReceiveMysteryCommand(id,"revive:"+target.frame.id);
                bool attacking=botDirector!=null&&botDirector.TryGet(id,out var assignment,out _)&&assignment.kind==BotObjectiveKind.Attack;
                mystery.SetSpecial(id,attacking);
                var personal=mystery.Personal(id);
                if(personal.disasterChoices.Length>0&&personal.choiceRemaining>0)
                {
                    string selected=personal.disasterChoices[0];
                    foreach(var choice in personal.disasterChoices)if(HazardPreference(choice)>HazardPreference(selected))selected=choice;
                    ReceiveMysteryCommand(id,"disaster:"+selected);
                }
            }
        }
        bool CollectMystery(string id,List<BotObjective> goals,MysteryPublicFrame publicFacts=null)
        {
            if(!MysteryActive)return false;
            var self=simulation.Find(id);if(self==null||mystery.IsDowned(id))return true;
            var role=mystery.Role(id);
            witnessedMurderers.TryGetValue(id,out var known);
            bool attackReady=mystery.Personal(id).attackReadyIn<=0;
            foreach(var p in simulation.Participants)
            {
                if(p==self||!p.present||!mystery.Alive(p.frame.id))continue;
                var actor=FindActor(p.frame.id);if(actor==null)continue;
                if(mystery.IsDowned(p.frame.id))
                {if(role!=MysteryRole.Murderer&&VisibleToActor(self.frame,p.frame))AddGoal(goals,"revive:"+p.frame.id,BotObjectiveKind.Guard,p.frame.position,actor.gameObject,.65f,1.3f);continue;}
                if(!ObserveOpponent(self.frame,p.frame,out var observed))continue;
                bool visible=VisibleToActor(self.frame,p.frame);
                if(role==MysteryRole.Murderer)
                {
                    if(!attackReady)continue;
                    int witnesses=0;foreach(var observer in simulation.Participants)if(observer!=self&&observer!=p&&observer.present&&mystery.Alive(observer.frame.id)&&VisibleToActor(self.frame,observer.frame)&&(observer.frame.position-p.frame.position).sqrMagnitude<64&&VisibleTo(observer.frame.position,p.frame.position))witnesses++;
                    AddGoal(goals,"stalk:"+p.frame.id,BotObjectiveKind.Attack,observed,visible?actor.gameObject:null,1,.9f/(1+witnesses*.4f));
                }
                else if(ModeBotTactics.MysteryHostile(role,known?.Contains(p.frame.id)==true))
                {AddGoal(goals,"witnessed:"+p.frame.id,role==MysteryRole.Sheriff?BotObjectiveKind.Attack:BotObjectiveKind.Evade,observed,visible?actor.gameObject:null,role==MysteryRole.Sheriff?1:7,1.6f);}
            }
            foreach(var body in (publicFacts??mystery.Public()).bodies)
            {
                if(body.lost||(body.position-self.frame.position).sqrMagnitude>225||!VisibleTo(self.frame.position,body.position))continue;
                if(!body.reported&&mystery.ReportCooldown(id,body.id)<=0)AddGoal(goals,"investigate:"+body.id,BotObjectiveKind.Guard,body.position,null,.65f,role==MysteryRole.Murderer?.3f:.9f);
                if(body.sheriff&&role==MysteryRole.Innocent&&!unavailableBadges.Contains(id+"/"+body.id))AddGoal(goals,"badge:"+body.id,BotObjectiveKind.Guard,body.position,null,.65f,1.5f);
            }
            RefreshSupportGoals();
            for(int i=0;i<supportGoals.Count;i++){int hash=(id.GetHashCode()&int.MaxValue)%Mathf.Max(1,supportGoals.Count);int index=(i+hash)%supportGoals.Count;AddGoal(goals,"explore:"+index,BotObjectiveKind.Travel,supportGoals[index],null,1.5f,.38f-i*.01f);}
            return true;
        }
        static int HazardPreference(string id)=>id.Contains("Planet")?4:id.Contains("Tsunami")?3:id.Contains("Acid")?2:1;
    }
}
