using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    public sealed partial class MatchModeRuntime
    {
        bool restoredHistory;
        public void ReconcileHumanRoster(IEnumerable<string> activeIds)
        {
            if(!Authority||activeIds==null)return;
            var active=new HashSet<string>(activeIds,StringComparer.Ordinal){LocalActorId};
            foreach(string id in simulation.ReconcileHumans(active))
            {
                UnregisterRemote(id);transitionUntil.Remove(id);delayedOutcomes.Remove(id);pendingSpawns.Remove(id);sinkStarted.Remove(id);
            }
            frame=simulation.Snapshot();PublishEnemyActions();PublishEquipment();WriteDisasterFrame();ApplyFrames();
        }
        public void ResumeAuthorityFromFrame()
        {
            if(!Authority)return;
            var saved=frame;actors.Clear();actorIds.Clear();
            RestoreAuthorityState(saved);SampleActors();wasAuthority=true;
            if(Rules.mode==GameModeKind.MurderMystery)foreach(var p in simulation.Participants)if(p.present)Respawn(p.frame.id,"survivor");
            frame=simulation.Snapshot();PublishEnemyActions();PublishEquipment();WriteDisasterFrame();ApplyFrames();
        }

        void RestoreAuthorityState(MatchFrame saved)
        {
            ResetObjectiveDirector("Authority restored");
            ruleCache.Clear();transitionUntil.Clear();delayedOutcomes.Clear();sinkStarted.Clear();pendingBotHealth.Clear();
            if(saved==null||saved.players.Length==0)
            {SampleActors();simulation.StartRound();SetFlagHomes();return;}
            RestoreEquipment(saved);RestoreEnemyActions(saved);simulation.Restore(saved);restoredHistory=true;
            if(saved.rules.mode==GameModeKind.MurderMystery)
            {
                // Secret roles were never broadcast. A new server starts a fresh mystery.
                ResetEquipment();mystery=null;ClearMysteryBodies();pendingBotHealth.Clear();simulation.StartRound();restoredHistory=false;return;
            }
            disasters.Restore(simulation.Elapsed,saved.disasterWarning,saved.disasterWarningRemaining,saved.disasterNextIn);
            foreach(var row in saved.players)
            {
                remoteHealth.Seed(row.id,row.health,row.dying||!row.alive);
                if(row.bot)pendingBotHealth[row.id]=row.health;
                if(row.sinking)sinkStarted[row.id]=Time.time-row.sinkProgress*Mathf.Max(.2f,Rules.sinkGraceSeconds);
                if(!row.dying)continue;
                if(!Enum.TryParse(row.pendingOutcome,out EliminationOutcome outcome)||outcome==EliminationOutcome.None)
                    outcome=row.role=="infected"?EliminationOutcome.Infected:row.role=="spectator"?EliminationOutcome.Spectator:EliminationOutcome.Respawn;
                float remaining=row.deathRemaining>0?row.deathRemaining:.9f*(1-row.deathProgress);
                transitionUntil[row.id]=Time.time+Mathf.Clamp(remaining,.01f,.9f);delayedOutcomes[row.id]=outcome;
            }
            ConfigureStatCollectors();analyzer=new RoundStatAnalyzer(Time.timeAsDouble);CaptureStats();
            if(saved.phase==RoundPhase.Victory||saved.phase==RoundPhase.Results)FinishReport();
        }
        void RestoreBotHealth(string id,IModeActor actor)
        {
            if(!Authority||!pendingBotHealth.TryGetValue(id,out float health))return;
            var original=RulesFor(actor.gameObject);var safe=original.Copy();safe.deathEnabled=false;
            actor.ApplyModeRules(safe);
            float deficit=actor.Health-Mathf.Clamp01(health)*actor.MaxHealth;
            if(deficit>.001f)actor.gameObject.GetComponent<IDamageable>()?.Damage(deficit,gameObject);
            actor.ApplyModeRules(original);pendingBotHealth.Remove(id);
        }
        sealed class ModeStat
        {
            public readonly string id,title,unit;
            public readonly Func<MatchPlayerFrame,double> read;
            public ModeStat(string id,string title,string unit,Func<MatchPlayerFrame,double> read)
            {this.id=id;this.title=title;this.unit=unit;this.read=read;}
        }
        ModeStat[] ReportStats()
        {
            var kills=new ModeStat("kills","Eliminations","",p=>p.kills);
            var deaths=new ModeStat("deaths","Deaths","",p=>p.deaths);
            var survival=new ModeStat("alive_seconds","Time as survivor","s",p=>p.survivalSeconds);
            switch(Rules.mode)
            {
                case GameModeKind.CaptureFlag:return new[]{new ModeStat("captures","Captures","",p=>p.captures),new ModeStat("flag_returns","Flag returns","",p=>p.flagReturns),kills,deaths};
                case GameModeKind.TeamTickets:return new[]{kills,deaths};
                case GameModeKind.KingOfHill:return new[]{new ModeStat("hill_seconds","Hill control","s",p=>p.hillSeconds),kills,deaths};
                case GameModeKind.BlockControl:return new[]{new ModeStat("claims","Claims completed","",p=>p.blocksClaimed),new ModeStat("team_points","Team points","",p=>simulation.ScoreForTeam(p.team)),kills,deaths};
                case GameModeKind.Infection:return new[]{survival,new ModeStat("infections","Survivors infected","",p=>p.infections)};
                case GameModeKind.MurderMystery:return new[]{new ModeStat("body_reports","Bodies reported","",p=>p.bodyReports),new ModeStat("revives","Revives","",p=>p.revives),kills};
                case GameModeKind.Survival:return new[]{new ModeStat("alive_seconds","Time alive","s",p=>p.survivalSeconds),new ModeStat("average_altitude","Average height","m",p=>p.averageHeight),kills};
                default:return new[]{kills,deaths};
            }
        }
        void ConfigureStatCollectors()
        {
            stats=new SessionStats();
            stats.Register(new StatDefinition("mode_score",StatKind.Number),sample=>simulation.ScoreOf(sample.Id));
            stats.DefiningMetricId="mode_score";
            foreach(var field in ReportStats())
                stats.Register(new StatDefinition(field.id,StatKind.Number,field.unit),sample=>simulation.Find(sample.Id) is ModeSimulation.Participant p?field.read(p.frame):0);
        }
        void FinishReport()
        {
            if(analyzer==null)return;
            analyzer.Finish(Time.timeAsDouble);
            var fields=new List<ModeStat>(ReportStats());
            // Keep custom host-selected scoring visible without replacing the mode's useful totals.
            if(Rules.metric!=ModeSimulation.Defaults(Rules.mode).metric)fields.Insert(0,new ModeStat("mode_score",simulation.MetricLabel,"",p=>simulation.ScoreOf(p.id)));
            var columns=new PostGameColumn[fields.Count];
            for(int i=0;i<columns.Length;i++)columns[i]=new PostGameColumn(fields[i].id,fields[i].title,fields[i].unit);
            var participants=new List<ModeSimulation.Participant>();
            foreach(var p in simulation.Participants)participants.Add(p);
            participants.Sort((a,b)=>{int score=simulation.ScoreOf(b.frame.id).CompareTo(simulation.ScoreOf(a.frame.id));return score!=0?score:string.CompareOrdinal(a.frame.id,b.frame.id);});
            var rows=new PostGameRow[participants.Count];int place=0;float previous=float.NaN;
            for(int i=0;i<rows.Length;i++)
            {
                var p=participants[i].frame;float score=simulation.ScoreOf(p.id);
                if(i==0||score!=previous)place=i+1;previous=score;
                var values=new double?[fields.Count];for(int c=0;c<values.Length;c++)values[c]=fields[c].read(p);
                rows[i]=new PostGameRow(p.id,p.name,p.bot,Rules.mode==GameModeKind.MurderMystery||Rules.mode==GameModeKind.Demo?0:place,values);
            }
            string description="Round totals from authoritative objectives and eliminations. Team outcome is separate from individual contributions.";
            if(restoredHistory)description+=" Host changed; transferred cumulative totals are preserved.";
            LatestReport=new PostGameReport(simulation.Winner=="Draw"?"Draw":simulation.Winner+" wins",description,simulation.Elapsed,columns,rows);analyzer=null;
        }
    }
}
