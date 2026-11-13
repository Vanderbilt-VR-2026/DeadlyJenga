using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    public sealed partial class MatchModeRuntime
    {
        readonly List<DisasterFrame> tacticalHazards=new List<DisasterFrame>();
        readonly List<WaveFrame> tacticalWaves=new List<WaveFrame>();
        readonly List<Vector3> tacticalPoints=new List<Vector3>();
        readonly HashSet<Vector3Int> tacticalCells = new HashSet<Vector3Int>();
        float nextTacticalSample;
        bool CollectHazardTactics(IModeActor actor,List<BotObjective> goals)
        {
            if(actor==null||Services.World==null)return false;
            if(Time.time>=nextTacticalSample)
            {
                nextTacticalSample=Time.time+.5f;tacticalHazards.Clear();tacticalWaves.Clear();
                foreach(var source in DisasterStates.Active){var f=source.Capture();if(f!=null&&!f.residualOnly)tacticalHazards.Add(f);}
                if(disasters.Pending=="AcidRain")
                    tacticalHazards.Add(new DisasterFrame{kind="AcidRain",warning=disasters.WarningRemaining(simulation.Elapsed)});
                foreach(var source in Tracked.Waves)tacticalWaves.Add(source.Wave);
            }
            if(tacticalHazards.Count==0&&tacticalWaves.Count==0)return false;
            RefreshSupportGoals();float height=Mathf.Clamp(actor.ViewPose.position.y-actor.transform.position.y,.8f,5);
            ModeHazardTactics.Select(actor.transform.position,height,supportCandidates,tacticalHazards,tacticalWaves,Services.World.IsSheltered,tacticalPoints);
            tacticalCells.Clear();
            foreach (var point in tacticalPoints)
            {
                var cell = Vector3Int.RoundToInt(point);
                if (!tacticalCells.Add(cell)) continue;
                AddGoal(goals, $"hazard-safe:{cell.x}:{cell.y}:{cell.z}", BotObjectiveKind.Guard, point, null, .7f, 3);
            }
            return tacticalPoints.Count>0;
        }
    }
}
