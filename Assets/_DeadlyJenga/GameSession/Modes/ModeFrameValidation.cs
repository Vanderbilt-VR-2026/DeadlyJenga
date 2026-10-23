using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    public static class ModeFrameValidation
    {
        static bool Number(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value)&&Mathf.Abs(value)<100000000;
        static bool Position(Vector3 p)=>Number(p.x)&&Number(p.y)&&Number(p.z);
        static bool Rotation(Quaternion q)=>Number(q.x)&&Number(q.y)&&Number(q.z)&&Number(q.w)&&q.x*q.x+q.y*q.y+q.z*q.z+q.w*q.w>.01f;
        static bool Text(string value,int max,bool optional=false)=>optional&&value==null||value!=null&&value.Length<=max&&(optional||value.Length>0);
        public static bool Rules(ModeRules rules)
        {
            if(rules==null||!Enum.IsDefined(typeof(GameModeKind),rules.mode)||!Enum.IsDefined(typeof(IslandRule),rules.island)||!Enum.IsDefined(typeof(SpawnRule),rules.spawn)||!Enum.IsDefined(typeof(ScoreMetric),rules.metric)||rules.teamCount<2||rules.teamCount>8)return false;
            foreach(var field in typeof(ModeRules).GetFields())if(field.FieldType==typeof(float)&&!Number((float)field.GetValue(rules)))return false;
            return Text(rules.environmentSkin,40,true)&&MatchModeRuntime.ValidMysterySettings(rules.mystery);
        }
        public static bool EquipmentActions(MatchFrame frame)
        {
            if(frame==null||frame.equipmentActions==null||frame.equipmentActions.Length>32||frame.equipmentSequence<0||!Text(frame.equipmentEpoch,32,true))return false;
            if(frame.equipmentEpoch!=null&&frame.equipmentEpoch.Length>0&&!Guid.TryParseExact(frame.equipmentEpoch,"N",out _))return false;
            if(frame.equipmentSequence>0&&string.IsNullOrEmpty(frame.equipmentEpoch))return false;
            int last=0;
            foreach(var item in frame.equipmentActions)
            {
                if(item==null||item.id<=last||item.id>frame.equipmentSequence||!Text(item.actor,128)||item.kind<EquipmentKind.Speed||item.kind>EquipmentKind.PoweredArmor||!Enum.IsDefined(typeof(EquipmentAction),item.action)||!Position(item.position)||!Number(item.remaining)||item.remaining<=0||item.remaining>.801f||item.endpoints==null||item.endpoints.Length>7)return false;
                if(item.shotgun&&item.kind!=EquipmentKind.DualRifle)return false;
                if(item.action==EquipmentAction.Shot)
                {
                    if(item.kind<EquipmentKind.Blaster)return false;
                    if(item.kind==EquipmentKind.DualRifle&&item.endpoints.Length!=(item.shotgun?7:1))return false;
                    if(item.kind!=EquipmentKind.DualRifle&&item.endpoints.Length>1)return false;
                }
                else if(item.endpoints.Length!=0)return false;
                if((item.action==EquipmentAction.Draw||item.action==EquipmentAction.Holster)&&item.kind<EquipmentKind.Blaster)return false;
                if(item.action==EquipmentAction.Impact&&item.kind!=EquipmentKind.Blaster)return false;
                if(item.action==EquipmentAction.ModeSwitch&&item.kind!=EquipmentKind.DualRifle)return false;
                float range=item.kind==EquipmentKind.DualRifle?(item.shotgun?14:65):1.4f;
                foreach(var end in item.endpoints)if(!Position(end)||(end-item.position).sqrMagnitude>(range+.05f)*(range+.05f))return false;
                last=item.id;
            }
            return true;
        }
        public static bool Frame(MatchFrame frame,int blockCount)
        {
            if(frame==null||!Rules(frame.rules)||!Enum.IsDefined(typeof(RoundPhase),frame.phase)||frame.revision<1||!Number(frame.elapsed)||frame.elapsed<0||!Number(frame.remaining)||!Position(frame.hill)||!Text(frame.winner,128,true)||!Text(frame.metricLabel,64))return false;
            if(!EquipmentActions(frame)||!EnemyActionValidation.Valid(frame))return false;
            if(frame.equipment==null||frame.equipment.Length>256||frame.pickups==null||frame.pickups.Length>128)return false;
            var equipmentIds=new HashSet<string>();
            foreach(var item in frame.equipment)if(item==null||!Text(item.actor,128)||!equipmentIds.Add(item.actor)||item.kind<EquipmentKind.Speed||item.kind>EquipmentKind.PoweredArmor||!Number(item.remaining)||item.remaining<0||item.remaining>20||item.drawn&&item.kind<EquipmentKind.Blaster||item.roleIssued&&!item.drawn||item.shotgun&&item.kind!=EquipmentKind.DualRifle||item.thrust&&(item.kind!=EquipmentKind.PoweredArmor||!item.drawn))return false;
            var pickupIds=new HashSet<int>();
            foreach(var pickup in frame.pickups)if(pickup==null||pickup.id<1||!pickupIds.Add(pickup.id)||pickup.kind<EquipmentKind.Speed||pickup.kind>EquipmentKind.PoweredArmor||!Position(pickup.position)||!Number(pickup.remaining)||pickup.remaining<=0||pickup.remaining>20||!Number(pickup.availableIn)||pickup.availableIn<0||pickup.availableIn>15)return false;
            if(frame.blaster!=null)
            {
                if(frame.blaster.shots==null||frame.blaster.impacts==null||frame.blaster.shots.Length>64||frame.blaster.impacts.Length>64)return false;
                var projectiles=new HashSet<int>();foreach(var shot in frame.blaster.shots)if(shot==null||shot.id<1||!projectiles.Add(shot.id)||!Position(shot.position)||!Position(shot.velocity)||shot.velocity.sqrMagnitude>1600)return false;
                projectiles.Clear();foreach(var impact in frame.blaster.impacts)if(impact==null||impact.id<1||!projectiles.Add(impact.id)||!Position(impact.position)||!Number(impact.remaining)||impact.remaining<0||impact.remaining>.5f)return false;
            }
            if(frame.players==null||frame.players.Length>256||frame.topTen==null||frame.topTen.Length>10||frame.secondaryLeaders==null||frame.secondaryLeaders.Length>5||frame.teams==null||frame.teams.Length>8||frame.flags==null||frame.flags.Length>8||frame.claims==null||frame.claims.Length>Mathf.Max(0,blockCount))return false;
            var ids=new HashSet<string>();
            foreach(var p in frame.players)
            {
                if(p==null||!Text(p.id,128)||!ids.Add(p.id)||!Text(p.name,64)||!Text(p.role,32)||!Text(p.spectating,128,true)||!Position(p.position)||!Position(p.viewPosition)||!Rotation(p.viewRotation)||!Number(p.health)||p.health<0||p.health>1||!Number(p.metric)||!Number(p.survivalSeconds)||!Number(p.averageHeight)||!Number(p.height)||!Number(p.claimProgress)||!Number(p.handPowerRemaining)||p.handPowerRemaining<0||p.handPowerRemaining>120||!Number(p.heightObservedSeconds)||p.heightObservedSeconds<0||!Number(p.deathRemaining)||p.deathRemaining<0||p.deathRemaining>10||!Text(p.pendingOutcome,32,true)||!Text(p.handReturnRole,32,true)||!Number(p.deathProgress)||!Number(p.sinkProgress)||p.sinkProgress<0||p.sinkProgress>1)return false;
                if(p.deaths<0||p.captures<0||p.flagReturns<0||p.blocksClaimed<0||p.infections<0||p.bodyReports<0||p.revives<0||!Number(p.hillSeconds)||p.hillSeconds<0)return false;
                if(p.team<-2||p.team>=frame.teams.Length&&p.team>=0)return false;
            }
            var ranks=new HashSet<string>();foreach(var p in frame.topTen)if(p==null||!ids.Contains(p.id)||!ranks.Add(p.id)||p.rank<1||p.rank>10)return false;
            ranks.Clear();foreach(var p in frame.secondaryLeaders)if(p==null||!ids.Contains(p.id)||!ranks.Add(p.id)||p.rank<1||p.rank>5||!Number(p.metric))return false;
            var teamIds=new HashSet<int>();foreach(var t in frame.teams)if(t==null||t.id<0||t.id>=frame.teams.Length||!teamIds.Add(t.id)||!Number(t.points)||t.captures<0||t.tickets<0||t.ownedBlocks<0)return false;
            var claimIds=new HashSet<int>();foreach(var c in frame.claims)if(c==null||c.block<0||c.block>=blockCount||!claimIds.Add(c.block)||c.owner<-1||c.owner>=frame.teams.Length||c.claimant<-1||c.claimant>=frame.teams.Length||!Number(c.progress)||c.progress<0||c.progress>1||!Number(c.emptySeconds)||c.emptySeconds<0)return false;
            var flagIds=new HashSet<int>();foreach(var f in frame.flags)if(f==null||f.team<0||f.team>=frame.teams.Length||!flagIds.Add(f.team)||!Position(f.home)||!Position(f.position)||f.carrier!=null&&!ids.Contains(f.carrier))return false;
            if(frame.mystery==null||frame.mystery.bodies==null||frame.mystery.bodies.Length>256||frame.mystery.downed==null||frame.mystery.downed.Length>256)return false;
            foreach(var b in frame.mystery.bodies)if(b==null||!Text(b.id,128)||!Text(b.name,64)||!Position(b.position)||!Position(b.supportPoint)||!Number(b.reportedAge)||b.suspected==null||b.suspected.Length>3)return false;
            return true;
        }
    }
}
