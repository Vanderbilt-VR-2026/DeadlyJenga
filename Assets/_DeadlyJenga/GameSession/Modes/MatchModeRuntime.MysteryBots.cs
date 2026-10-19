using System.Collections.Generic;
using System.Linq;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    public sealed partial class MatchModeRuntime
    {
        readonly Dictionary<string,HashSet<string>> witnessedMurderers=new Dictionary<string,HashSet<string>>();
        float nextMysteryBots;
        void TickMysteryBots()
        {
            if(Time.time<nextMysteryBots)return;nextMysteryBots=Time.time+.25f;
            var clues=mystery.Public();
            foreach(var p in simulation.Participants)
            {
                if(!p.present||!p.frame.bot||!mystery.Alive(p.frame.id)||mystery.IsDowned(p.frame.id))continue;
                string id=p.frame.id;
                if(!witnessedMurderers.TryGetValue(id,out var known)){known=new HashSet<string>();witnessedMurderers[id]=known;}
                foreach(var strike in mysteryStrikes)if(strike.Value>Time.time&&mystery.Role(strike.Key)==MysteryRole.Murderer&&strike.Key!=id)
                {var attacker=simulation.Find(strike.Key);if(attacker!=null&&(attacker.frame.position-p.frame.position).sqrMagnitude<225&&VisibleTo(p.frame.position,attacker.frame.position))known.Add(strike.Key);}
                foreach(var body in clues.bodies)if(Close(id,body.position))
                {mystery.Report(id,body.id,p.frame.color);if(body.sheriff)mystery.RecoverSheriff(id,body.id);}
                foreach(var target in simulation.Participants)if(mystery.IsDowned(target.frame.id)&&Close(id,target.frame.position))ReceiveMysteryCommand(id,"revive:"+target.frame.id);
            }
        }
        bool CollectMystery(string id,List<BotObjective> goals)
        {
            if(!MysteryActive)return false;
            var self=simulation.Find(id);if(self==null||mystery.IsDowned(id))return true;
            var role=mystery.Role(id);bool attacking=false;
            witnessedMurderers.TryGetValue(id,out var known);
            foreach(var p in simulation.Participants)
            {
                if(p==self||!p.present||!mystery.Alive(p.frame.id))continue;
                var actor=FindActor(p.frame.id);if(actor==null)continue;
                if(mystery.IsDowned(p.frame.id))
                {if(role!=MysteryRole.Murderer)AddGoal(goals,"revive:"+p.frame.id,BotObjectiveKind.Guard,p.frame.position,actor.gameObject,1,1.15f);continue;}
                if(!VisibleTo(self.frame.position,p.frame.position))continue;
                if(role==MysteryRole.Murderer)
                {
                    int witnesses=0;foreach(var observer in simulation.Participants)if(observer!=self&&observer!=p&&observer.present&&mystery.Alive(observer.frame.id)&&(observer.frame.position-p.frame.position).sqrMagnitude<64&&VisibleTo(observer.frame.position,p.frame.position))witnesses++;
                    AddGoal(goals,"stalk:"+p.frame.id,BotObjectiveKind.Attack,p.frame.position,actor.gameObject,1,.9f/(1+witnesses*.4f));attacking=true;
                }
                else if(known?.Contains(p.frame.id)==true)
                {AddGoal(goals,"witnessed:"+p.frame.id,role==MysteryRole.Sheriff?BotObjectiveKind.Attack:BotObjectiveKind.Evade,p.frame.position,actor.gameObject,role==MysteryRole.Sheriff?1:7,1.6f);attacking=role==MysteryRole.Sheriff;}
            }
            mystery.SetSpecial(id,attacking);
            foreach(var body in mystery.Public().bodies)if(!body.lost&&!body.reported&&(body.position-self.frame.position).sqrMagnitude<225&&VisibleTo(self.frame.position,body.position))AddGoal(goals,"investigate:"+body.id,BotObjectiveKind.Guard,body.position,null,1.3f,role==MysteryRole.Murderer?.3f:.75f);
            RefreshSupportGoals();
            for(int i=0;i<supportGoals.Count;i++){int hash=(id.GetHashCode()&int.MaxValue)%Mathf.Max(1,supportGoals.Count);int index=(i+hash)%supportGoals.Count;AddGoal(goals,"explore:"+index,BotObjectiveKind.Travel,supportGoals[index],null,1.5f,.38f-i*.01f);}
            return true;
        }
    }
}
