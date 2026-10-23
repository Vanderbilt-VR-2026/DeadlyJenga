using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    public static class ModeBotTactics
    {
        public static float CombatUtility(float distance,bool visible,float health,bool carrier)
            => Mathf.Clamp01(1-distance/20)*.5f+(visible?.3f+Mathf.Clamp01(1-health)*.25f+(carrier?.4f:0):0);
        public static bool Retreat(float health, bool lastLife, int enemies, int allies) => enemies > 0 && (health < .35f || lastLife && health < .65f && enemies > allies + 1);
        public static float EscapeUtility(Vector3 feet, Vector3 point, IList<Vector3> threats)
        {
            float before=30,after=30;
            foreach(var threat in threats){before=Mathf.Min(before,Vector3.Distance(feet,threat));after=Mathf.Min(after,Vector3.Distance(point,threat));}
            return Mathf.Clamp((after-before)*.25f-Vector3.Distance(feet,point)*.025f,-5,5);
        }
        public static float SupportUtility(Vector3 feet,Vector3 point,float water)
            => Mathf.Clamp((point.y-feet.y)*.12f+Mathf.Clamp(point.y-water,0,24)*.025f-Vector3.Distance(feet,point)*.015f,-5,5);
        public static bool BlockRefreshed(bool grounded,int standing,int block,int owner,int team,float emptySeconds)
            => grounded&&block>=0&&team>=0&&standing==block&&owner==team&&emptySeconds<=.05f;
        public static bool MysteryHostile(MysteryRole self,bool witnessed) => self==MysteryRole.Murderer||witnessed;
    }

    public sealed partial class MatchModeRuntime
    {
        sealed partial class MatchBotFacts
        {
            struct Visit { public float entered,until,last; }
            readonly Dictionary<(string actor,string target),Visit> visits=new Dictionary<(string,string),Visit>();
            readonly List<Vector3> threats=new List<Vector3>();
            readonly List<BotObjective> knownHostiles=new List<BotObjective>();
            readonly List<BotDirectorTarget> ranked=new List<BotDirectorTarget>(16);
            readonly Dictionary<string,(BotDirectorTarget target,float until)> escapes=new Dictionary<string,(BotDirectorTarget,float)>();
            static readonly string[] tacticQueries={"hill-threats","block-threats","home-threats","escort-threats","close-threats","owned-blocks","escape-routes","regroup","patrol","mystery-refuge"};
            static readonly string[] tacticConditions={"threat-near","needs-retreat","visited","target-infected","target-revived","body-reported","sheriff-promoted","block-refreshed"};

            void PruneTacticalMemory()
            {
                if(visits.Count<4096)return;
                var old=new List<(string,string)>();foreach(var v in visits)if(Time.time-v.Value.last>60)old.Add(v.Key);
                foreach(var key in old)visits.Remove(key);
                if(visits.Count>4096)visits.Clear();
            }
            void Known(BotDirectorActor actor)
            {knownHostiles.Clear();Hostiles(actor,knownHostiles);}
            bool TestTactic(string name,BotDirectorActor actor,BotDirectorTarget target)
            {
                switch(name)
                {
                    case "threat-near": Known(actor);foreach(var h in knownHostiles)if((h.position-actor.position).sqrMagnitude<144)return true;return false;
                    case "needs-retreat":
                        Known(actor);int enemies=0,allies=0;foreach(var h in knownHostiles)if((h.position-actor.position).sqrMagnitude<100)enemies++;
                        foreach(var p in Sim.Participants)if(p.present&&p.frame.alive&&p.frame.id!=actor.id&&p.frame.team==actor.team&&(p.frame.position-actor.position).sqrMagnitude<100)allies++;
                        bool last=false;foreach(var team in owner.frame.teams)if(team.id==actor.team)last=team.tickets==0;
                        return ModeBotTactics.Retreat(Sim.Find(actor.id)?.frame.health??1,last,enemies,allies);
                    case "visited":
                        var key=(actor.id,target.id);bool remembered=visits.TryGetValue(key,out var visit);visit.last=Time.time;
                        if(Time.time<visit.until){visits[key]=visit;return true;}
                        if(Vector3.Distance(actor.position,target.position)>1.5f){visit.entered=0;if(remembered)visits[key]=visit;return false;}
                        if(visit.entered<=0)visit.entered=Time.time;
                        if(Time.time-visit.entered>=1.5f){visit.until=Time.time+12;visit.entered=0;visits[key]=visit;return true;}
                        visits[key]=visit;return false;
                    case "block-refreshed":
                        return int.TryParse(target.id,out int block)&&ModeBotTactics.BlockRefreshed(Sim.Find(actor.id).grounded,Sim.Find(actor.id).frame.standingBlock,block,Sim.OwnerOf(block),actor.team,Sim.EmptySecondsOf(block));
                    case "target-infected": return Sim.Find(target.id)?.frame.role=="infected";
                    case "target-revived": return owner.MysteryActive&&owner.mystery.Alive(TargetPerson(target.id))&&!owner.mystery.IsDowned(TargetPerson(target.id));
                    case "body-reported":
                        if(!owner.MysteryActive)return false;
                        foreach(var b in (publicMystery??owner.mystery.Public()).bodies)if(target.id=="investigate:"+b.id)return b.reported;return false;
                    case "sheriff-promoted": return owner.MysteryActive&&owner.mystery.Role(actor.id)==MysteryRole.Sheriff;
                    default: throw new ArgumentException("Unknown bot condition: "+name);
                }
            }
            static readonly string[] personPrefixes={"revive:","stalk:","witnessed:"};
            static string TargetPerson(string id)
            {
                foreach(var prefix in personPrefixes)if(id.StartsWith(prefix,StringComparison.Ordinal))return id.Substring(prefix.Length);
                return id;
            }
            void QueryTactic(string name,BotDirectorActor actor,List<BotDirectorTarget> results)
            {
                switch(name)
                {
                    case "close-threats":
                        if(!owner.RulesFor(actor.body).punchEnabled)return;
                        var self=Sim.Find(actor.id);if(self==null)return;
                        List<BotObjective> contacts;
                        if(owner.MysteryActive)
                        {
                            if(!mysteries.TryGetValue(actor.id,out contacts))
                            {contacts=new List<BotObjective>();owner.CollectMystery(actor.id,contacts,publicMystery);mysteries[actor.id]=contacts;}
                        }
                        else {Known(actor);contacts=knownHostiles;}
                        foreach(var h in contacts)
                        {
                            if(owner.MysteryActive&&h.kind!=BotObjectiveKind.Attack&&h.kind!=BotObjectiveKind.Evade)continue;
                            if(!h.subject||(h.position-actor.position).sqrMagnitude>2.2f*2.2f||!owner.actorIds.TryGetValue(h.subject,out var id))continue;
                            var other=Sim.Find(id);
                            if(other==null||!other.present||!other.frame.alive||other.frame.dying||owner.RulesFor(h.subject).downed||!owner.VisibleToActor(self.frame,other.frame))continue;
                            Add(results,h.id,h.position,h.subject,0,1.1f);
                            var item=results[results.Count-1];item.utility=h.priority;results[results.Count-1]=item;
                            if(results.Count>=4)break;
                        }
                        return;
                    case "escape-routes": case "regroup": Escape(actor,results,false);return;
                    case "mystery-refuge": Escape(actor,results,true);return;
                    case "patrol": SafeSupports(actor,results,true);return;
                    case "owned-blocks":
                        foreach(var pair in owner.blockIds)
                        {
                            if(!pair.Key||Sim.OwnerOf(pair.Value)!=actor.team)continue;
                            if(!BlockPoint(actor,pair.Key.GetComponent<Collider>(),out var point))continue;
                            Add(results,pair.Value.ToString(),point,pair.Key.gameObject,0,.25f);
                            var item=results[results.Count-1];item.utility=owner.Rules.claimDecaySeconds>0?Mathf.Clamp01(Sim.EmptySecondsOf(pair.Value)/owner.Rules.claimDecaySeconds)*1.5f:0;results[results.Count-1]=item;
                            if(results.Count>=12)break;
                        }
                        return;
                    case "hill-threats": case "block-threats": case "home-threats": case "escort-threats":
                        if(!owner.Rules.punchEnabled)return;
                        if(name=="home-threats"){var home=Own(actor.team);if(home==null||Vector3.Distance(actor.position,home.home)>=10)return;}
                        if(name=="hill-threats"&&Vector3.Distance(actor.position,Sim.Hill)>=owner.Rules.hillRadius+4)return;
                        if(name=="escort-threats")
                        {
                            bool nearby=false;
                            foreach(var flag in Sim.Flags){var carrier=Sim.Find(flag.carrier);if(carrier!=null&&carrier.frame.team==actor.team&&Vector3.Distance(actor.position,carrier.frame.position)<8){nearby=true;break;}}
                            if(!nearby)return;
                        }
                        Known(actor);
                        foreach(var h in knownHostiles)
                        {
                            bool relevant=false;
                            if((h.position-actor.position).sqrMagnitude>100)continue;
                            if(name=="hill-threats")relevant=Vector3.Distance(actor.position,Sim.Hill)<owner.Rules.hillRadius+4&&Vector3.Distance(h.position,Sim.Hill)<owner.Rules.hillRadius+2;
                            if(name=="home-threats"){var home=Own(actor.team);relevant=home!=null&&Vector3.Distance(actor.position,home.home)<10&&Vector3.Distance(h.position,home.home)<8;}
                            if(name=="block-threats")
                            {
                                var p=Sim.Find(h.id);relevant=p!=null&&p.frame.standingBlock>=0&&(Sim.OwnerOf(p.frame.standingBlock)==actor.team||Sim.Find(actor.id)?.frame.standingBlock==p.frame.standingBlock);
                            }
                            if(name=="escort-threats")foreach(var f in Sim.Flags)
                            {var p=Sim.Find(f.carrier);if(p!=null&&p.frame.team==actor.team&&Vector3.Distance(actor.position,p.frame.position)<8&&Vector3.Distance(h.position,p.frame.position)<7)relevant=true;}
                            if(relevant){Add(results,h.id,h.position,h.subject);if(results.Count>=8)break;}
                        }
                        return;
                    default: throw new ArgumentException("Unknown bot query: "+name);
                }
            }
            bool BlockPoint(BotDirectorActor actor,Collider collider,out Vector3 point)
            {
                point=default;if(!collider||!collider.enabled||(collider.bounds.ClosestPoint(actor.position)-actor.position).sqrMagnitude>1600)return false;
                var body=collider.attachedRigidbody;if(body&&body.linearVelocity.sqrMagnitude>.25f)return false;
                var bounds=collider.bounds;
                if(!collider.Raycast(new Ray(new Vector3(bounds.center.x,bounds.max.y+.5f,bounds.center.z),Vector3.down),out var hit,bounds.size.y+1)||hit.normal.y<.7f)return false;
                return Footing(actor,hit.point+.04f*Vector3.up,out point,1);
            }
            bool Footing(BotDirectorActor actor,Vector3 probe,out Vector3 point,float depth=8)
            {
                point=default;int mask=1|(1<<Layers.Block)|(1<<Layers.Surface);
                if(!Physics.Raycast(probe+Vector3.up*.4f,Vector3.down,out var hit,depth+.4f,mask,QueryTriggerInteraction.Ignore)||hit.normal.y<.7f)return false;
                point=hit.point+Vector3.up*.04f;
                if(point.y<=(Services.World?.WaterHeight(point)??-100)+.25f&&!(owner.RulesFor(actor.body).island==IslandRule.Walkable&&hit.collider.gameObject.layer==0))return false;
                var body=owner.FindActor(actor.id);float height=body==null?1.8f:Mathf.Clamp(body.ViewPose.position.y-actor.position.y+.15f,.8f,5);
                float radius=Mathf.Min(.28f,height*.2f);
                return !Physics.CheckCapsule(point+Vector3.up*(radius+.04f),point+Vector3.up*(height-radius),radius,mask,QueryTriggerInteraction.Ignore);
            }
            void SafeSupports(BotDirectorActor actor,List<BotDirectorTarget> results,bool patrol)
            {
                owner.RefreshSupportGoals();ranked.Clear();
                foreach(var point in owner.supportCandidates)
                {
                    var delta=point-actor.position;
                    if(delta.y>12||delta.y< -6||delta.sqrMagnitude>40*40)continue;
                    var item=new BotDirectorTarget{id=PointId(point),position=point,radius=patrol?.8f:.65f,
                        utility=patrol?-.025f*delta.magnitude:ModeBotTactics.SupportUtility(actor.position,point,Services.World?.WaterHeight(point)??0)};
                    if(patrol&&TestTactic("visited",actor,item))continue;
                    Rank(item,8);
                }
                if(ranked.Count==0&&Footing(actor,actor.position,out var local))Rank(new BotDirectorTarget{id=PointId(local),position=local,radius=.5f},8);
                results.AddRange(ranked);
            }
            void Escape(BotDirectorActor actor,List<BotDirectorTarget> results,bool mystery)
            {
                threats.Clear();
                if(mystery)
                {
                    if(!owner.MysteryActive||owner.mystery.Role(actor.id)!=MysteryRole.Innocent)return;
                    if(owner.witnessedMurderers.TryGetValue(actor.id,out var known))foreach(var id in known)
                    {
                        var enemy=Sim.Find(id);var self=Sim.Find(actor.id);
                        if(enemy!=null&&enemy.frame.alive&&owner.ObserveOpponent(self.frame,enemy.frame,out var point)&&(point-actor.position).sqrMagnitude<144)threats.Add(point);
                    }
                }
                else{Known(actor);foreach(var h in knownHostiles)if((h.position-actor.position).sqrMagnitude<225)threats.Add(h.position);}
                if(threats.Count==0)return;
                if(escapes.TryGetValue(actor.id,out var prior)&&Time.time<prior.until&&Vector3.Distance(actor.position,prior.target.position)>1&&ModeBotTactics.EscapeUtility(actor.position,prior.target.position,threats)>.05f&&Footing(actor,prior.target.position,out _))
                {results.Add(prior.target);return;}
                ranked.Clear();
                for(int i=0;i<16;i++)
                {
                    float angle=i*Mathf.PI*.25f;var offset=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(i<8?4:7);
                    if(!Footing(actor,actor.position+offset,out var point))continue;
                    float drop=actor.position.y-point.y;if(drop>(owner.Rules.fallDamage?owner.Rules.fallDamageStart*.8f:10))continue;
                    float score=ModeBotTactics.EscapeUtility(actor.position,point,threats);
                    if(score<=.05f)continue;
                    Rank(new BotDirectorTarget{id=PointId(point),position=point,utility=score,radius=.4f},4);
                }
                if(ranked.Count>0){escapes[actor.id]=(ranked[0],Time.time+4);results.Add(ranked[0]);}
            }
            void Rank(BotDirectorTarget item,int limit)
            {
                foreach(var existing in ranked)if(existing.id==item.id)return;
                int at=0;while(at<ranked.Count&&ranked[at].utility>=item.utility)at++;
                if(at>=limit)return;ranked.Insert(at,item);if(ranked.Count>limit)ranked.RemoveAt(limit);
            }
        }
    }
}
