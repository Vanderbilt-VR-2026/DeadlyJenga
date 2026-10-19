using DeadlyJenga.Core;
using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    public sealed partial class ModeSimulation
    {
        public string[] ReconcileHumans(IEnumerable<string> activeIds)
        {
            var active=new HashSet<string>(activeIds,System.StringComparer.Ordinal);
            var removed=new List<string>();
            foreach(var p in players.Values)if(p.present&&!p.frame.bot&&!active.Contains(p.frame.id))removed.Add(p.frame.id);
            foreach(string id in removed){Remove(id);Find(id).frame.spectating=null;Find(id).frame.watching=0;}
            TickSpectators();
            return removed.ToArray();
        }
        public void Restore(MatchFrame frame)
        {
            if(frame==null||frame.rules==null)return;
            restoring=true;try{Configure(frame.rules);}finally{restoring=false;}players.Clear();
            foreach(var source in frame.players)names.Reserve(source.name);
            foreach(var source in frame.players)
            {
                if(string.IsNullOrEmpty(source.id)||players.ContainsKey(source.id)||players.Count>=256)continue;
                var p=new Participant();players.Add(source.id,p);p.frame.id=source.id;p.frame.bot=source.bot;p.frame.local=source.local;
                var f=p.frame;f.name=source.name;p.noun=ModeNames.NounOf(source.name);f.team=source.team;f.color=source.color;f.alive=source.alive;f.role=source.role;
                f.survivorColor=source.survivorColor.a>0?source.survivorColor:source.role=="infected"?ModeNames.Colors[1]:source.color;
                f.position=source.position;f.viewPosition=source.viewPosition;f.viewRotation=source.viewRotation;
                f.health=source.health;f.metric=source.metric;f.survivalSeconds=source.survivalSeconds;f.averageHeight=source.averageHeight;f.height=source.height;f.kills=source.kills;f.spectating=source.spectating;
                f.dying=source.dying;f.deathProgress=source.deathProgress;f.deathRemaining=source.deathRemaining;f.pendingOutcome=source.pendingOutcome;
                f.sinking=source.sinking;f.sinkProgress=source.sinkProgress;
                p.previousRole=source.handReturnRole;p.powerUntil=source.handPowerRemaining>0?frame.elapsed+source.handPowerRemaining:0;
                p.observedSeconds=source.heightObservedSeconds;p.heightIntegral=source.averageHeight*p.observedSeconds;
            }
            foreach(var t in frame.teams)if(t.id>=0&&t.id<teams.Count){var target=teams[t.id];target.points=t.points;target.captures=t.captures;target.tickets=t.tickets;target.ownedBlocks=t.ownedBlocks;}
            flags.Clear();foreach(var f in frame.flags)flags.Add(new FlagFrame{team=f.team,home=f.home,position=f.position,carrier=f.carrier,atHome=f.atHome});
            claims.Clear();foreach(var f in frame.claims){var c=new Claim();c.frame.block=f.block;c.frame.owner=f.owner;c.frame.claimant=f.claimant;c.frame.progress=f.progress;claims[f.block]=c;}
            Elapsed=frame.elapsed;Hill=frame.hill;Winner=frame.winner;WinnerTeam=frame.winnerTeam;Phase=frame.phase;
            phaseTime=Phase==RoundPhase.Countdown?Mathf.Max(0,Rules.countdownSeconds-frame.remaining):0;sequence=frame.revision;
        }
        public void UpdateFlagSupport(int team,float height)
        {
            if(team<0||team>=flags.Count)return;
            var flag=flags[team];flag.home=new Vector3(flag.home.x,height,flag.home.z);
            if(flag.atHome)flag.position=flag.home;
        }
    }
}
