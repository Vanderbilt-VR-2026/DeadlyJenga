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
            frame=simulation.Snapshot();WriteDisasterFrame();ApplyFrames();
        }
        public void ResumeAuthorityFromFrame()
        {
            if(!Authority)return;
            var saved=frame;actors.Clear();actorIds.Clear();
            RestoreAuthorityState(saved);SampleActors();wasAuthority=true;
            if(Rules.mode==GameModeKind.MurderMystery)foreach(var p in simulation.Participants)if(p.present)Respawn(p.frame.id,"survivor");
            frame=simulation.Snapshot();WriteDisasterFrame();ApplyFrames();
        }

        void RestoreAuthorityState(MatchFrame saved)
        {
            ruleCache.Clear();transitionUntil.Clear();delayedOutcomes.Clear();sinkStarted.Clear();pendingBotHealth.Clear();
            if(saved==null||saved.players.Length==0)
            {SampleActors();simulation.StartRound();SetFlagHomes();return;}
            simulation.Restore(saved);restoredHistory=true;
            if(saved.rules.mode==GameModeKind.MurderMystery)
            {
                // Secret roles were never broadcast. A new server starts a fresh mystery.
                mystery=null;ClearMysteryBodies();pendingBotHealth.Clear();simulation.StartRound();restoredHistory=false;return;
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
            analyzer=new RoundStatAnalyzer(Time.timeAsDouble);CaptureStats();
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
        void FinishReport()
        {
            if(analyzer==null)return;
            var analysis=analyzer.Finish(Time.timeAsDouble);
            string description="Ranked by "+simulation.MetricLabel+". Scores, survival time and average height are authoritative round totals.";
            if(restoredHistory)description+=" Host changed: observed time and peak height include only this host's samples; earlier raw samples were not transferred.";
            var sampled=reportBuilder.Build(analysis,simulation.Winner=="Draw"?"Draw":simulation.Winner+" wins",
                order:(a,b)=>simulation.ScoreOf(b.Id).CompareTo(simulation.ScoreOf(a.Id)),name:p=>simulation.Find(p.Id)?.frame.name??p.Id,description:description);
            var columns=new PostGameColumn[sampled.Columns.Count];for(int c=0;c<columns.Length;c++)columns[c]=sampled.Columns[c];
            var rows=new PostGameRow[sampled.Rows.Count];int place=0;float previous=float.NaN;
            for(int i=0;i<rows.Length;i++)
            {
                var row=sampled.Rows[i];var state=simulation.Find(row.ParticipantId)?.frame;
                float score=simulation.ScoreOf(row.ParticipantId);if(i==0||score!=previous)place=i+1;previous=score;
                var values=new double?[columns.Length];
                for(int c=0;c<columns.Length;c++)
                {
                    values[c]=row.Values[c];
                    if(state!=null&&columns[c].Id=="alive_seconds")values[c]=state.survivalSeconds;
                    if(state!=null&&columns[c].Id=="average_altitude")values[c]=state.averageHeight;
                }
                rows[i]=new PostGameRow(row.ParticipantId,row.DisplayName,row.IsBot,place,values);
            }
            LatestReport=new PostGameReport(sampled.Title,description,simulation.Elapsed,columns,rows);analyzer=null;
        }
    }
}
