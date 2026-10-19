using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    public sealed partial class ModeSimulation
    {
        public float ScoreOf(string id){var p=Find(id);return p==null?0:Score(p);}
        float Score(Participant p)
        {
            var f=p.frame;
            switch(Rules.metric)
            {
                case ScoreMetric.AverageHeight:return f.averageHeight;
                case ScoreMetric.CurrentHeight:return f.height;
                case ScoreMetric.HillTime:return f.metric;
                case ScoreMetric.BlocksOwned:return f.team>=0?teams[f.team].ownedBlocks:0;
                case ScoreMetric.TeamPoints:return f.team>=0?TeamScore(teams[f.team]):0;
                case ScoreMetric.InfectedKills:return f.kills;
                default:return f.survivalSeconds;
            }
        }
        int Compare(Participant a,Participant b)
        {
            int score=Score(b).CompareTo(Score(a));
            return score!=0?score:string.CompareOrdinal(a.frame.id,b.frame.id);
        }
        public MatchPlayerFrame[] TopTen()
        {
            var top=new List<Participant>(11);
            foreach(var p in players.Values)
            {
                if(!p.present)continue;
                int i=0;while(i<top.Count&&Compare(top[i],p)<=0)i++;
                if(i<10){top.Insert(i,p);if(top.Count>10)top.RemoveAt(10);}
            }
            var result=new MatchPlayerFrame[top.Count];
            for(int i=0;i<result.Length;i++){result[i]=top[i].frame.Copy();result[i].metric=Score(top[i]);result[i].rank=i+1;}
            return result;
        }
        MatchPlayerFrame[] InfectedLeaders()
        {
            if(Rules.mode!=GameModeKind.Infection)return Array.Empty<MatchPlayerFrame>();
            var list=new List<Participant>();foreach(var p in players.Values)if(p.present&&p.frame.role=="infected")list.Add(p);
            list.Sort((a,b)=>{int kills=b.frame.kills.CompareTo(a.frame.kills);return kills!=0?kills:string.CompareOrdinal(a.frame.id,b.frame.id);});
            var result=new MatchPlayerFrame[Mathf.Min(5,list.Count)];
            for(int i=0;i<result.Length;i++){result[i]=list[i].frame.Copy();result[i].metric=list[i].frame.kills;result[i].rank=i+1;}return result;
        }
        public string MetricLabel
        {
            get
            {
                switch(Rules.metric){case ScoreMetric.AverageHeight:return "Average height";case ScoreMetric.CurrentHeight:return "Height";case ScoreMetric.HillTime:return "Hill time";case ScoreMetric.BlocksOwned:return "Blocks owned";case ScoreMetric.TeamPoints:return Rules.mode==GameModeKind.CaptureFlag?"Captures":"Team points";case ScoreMetric.InfectedKills:return "Infected kills";default:return "Time alive";}
            }
        }
        public MatchFrame Snapshot()
        {
            foreach(var participant in players.Values)if(!participant.frame.name.StartsWith(ModeNames.NameFor(participant.frame.color,participant.noun),StringComparison.Ordinal))Rename(participant);
            var rows=new List<MatchPlayerFrame>();int alive=0;
            foreach(var p in players.Values)if(p.present){var f=p.frame.Copy();f.handPowerRemaining=Mathf.Max(0,p.powerUntil-Elapsed);f.handReturnRole=p.previousRole;f.heightObservedSeconds=p.observedSeconds;f.metric=Score(p);f.rank=0;rows.Add(f);if(f.alive&&f.role!="infected")alive++;}
            var top=TopTen();if(Rules.mode==GameModeKind.Infection&&top.Length>5)Array.Resize(ref top,5);
            foreach(var f in top)foreach(var row in rows)if(row.id==f.id){row.rank=f.rank;break;}
            var claimFrames=new List<BlockClaimFrame>();foreach(var c in claims.Values)claimFrames.Add(new BlockClaimFrame{block=c.frame.block,owner=c.frame.owner,claimant=c.frame.claimant,progress=c.frame.progress});
            var teamFrames=new MatchTeamFrame[teams.Count];for(int i=0;i<teams.Count;i++){var t=teams[i];teamFrames[i]=new MatchTeamFrame{id=t.id,name=t.name,color=t.color,points=t.points,ownedBlocks=t.ownedBlocks,captures=t.captures,tickets=t.tickets};}
            var flagFrames=new FlagFrame[flags.Count];for(int i=0;i<flags.Count;i++){var f=flags[i];flagFrames[i]=new FlagFrame{team=f.team,home=f.home,position=f.position,carrier=f.carrier,atHome=f.atHome};}
            return new MatchFrame{revision=++sequence,phase=Phase,rules=Rules.Copy(),metricLabel=MetricLabel,winner=Winner,winnerTeam=WinnerTeam,
                elapsed=Elapsed,remaining=Phase==RoundPhase.Countdown?Mathf.Max(0,Rules.countdownSeconds-phaseTime):Rules.roundSeconds<=0?-1:Mathf.Max(0,Rules.roundSeconds-Elapsed),hill=Hill,survivorsLeft=alive,
                players=rows.ToArray(),topTen=top,secondaryLeaders=InfectedLeaders(),secondaryMetricLabel=Rules.mode==GameModeKind.Infection?"Infected kills":"",teams=teamFrames,claims=claimFrames.ToArray(),flags=flagFrames};
        }
        void TickSpectators()
        {
            foreach(var p in players.Values)p.frame.watching=0;
            foreach(var p in players.Values)
            {
                if(!p.present||p.frame.alive)continue;
                var target=Find(p.frame.spectating);
                if(target==null||!CanSpectate(p,target)){if(p.frame.bot)SelectBotSpectator(p);else NextSpectator(p.frame.id,1);}
                target=Find(p.frame.spectating);if(target!=null)target.frame.watching++;
            }
        }
        void SelectBotSpectator(Participant viewer)
        {
            var options=new List<Participant>();foreach(var target in players.Values)if(CanSpectate(viewer,target))options.Add(target);
            options.Sort((a,b)=>string.CompareOrdinal(a.frame.id,b.frame.id));
            uint salt=2166136261;foreach(char c in viewer.frame.id)salt=unchecked((salt^c)*16777619);
            viewer.frame.spectating=options.Count==0?null:options[(int)(salt%(uint)options.Count)].frame.id;
        }
        bool CanSpectate(Participant viewer,Participant target)
        {
            if(!target.present||!target.frame.alive||target==viewer)return false;
            if(!TeamMode(Rules.mode)||viewer.frame.team==target.frame.team)return true;
            foreach(var p in players.Values)if(p.present&&p.frame.alive&&p.frame.team==viewer.frame.team)return false;
            return true;
        }
        public void NextSpectator(string id,int step)
        {
            var p=Find(id);if(p==null)return;
            var options=new List<Participant>();foreach(var target in players.Values)if(CanSpectate(p,target))options.Add(target);
            options.Sort((a,b)=>string.CompareOrdinal(a.frame.id,b.frame.id));
            if(options.Count==0){p.frame.spectating=null;return;}
            int current=options.FindIndex(x=>x.frame.id==p.frame.spectating);
            p.frame.spectating=options[(current+(step>=0?1:-1)+options.Count)%options.Count].frame.id;
        }
    }
}
