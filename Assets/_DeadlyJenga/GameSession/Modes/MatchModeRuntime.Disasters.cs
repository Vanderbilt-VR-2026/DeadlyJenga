using System.Collections.Generic;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    public sealed partial class MatchModeRuntime
    {
        readonly ModeDisasterClock disasters=new ModeDisasterClock();
        readonly System.Random disasterRandom=new System.Random();
        string lastDisaster="";
        string ChooseDisaster()
        {
            var choices=new List<GameObject>();
            foreach(var prefab in DemoDisasters.Prefabs)
            {
                if(!prefab||prefab.name==lastDisaster)continue;
                string key=prefab.name.ToLowerInvariant();
                if(prefab.GetComponent<IDemoHazard>()?.Toggle==true||key.Contains("planet")||key.Contains("vampire"))continue;
                bool team=ModeSimulation.TeamMode(Rules.mode);
                if(team&&(key.Contains("fire")||key.Contains("meteor")||key.Contains("acid")||key.Contains("antigravity"))&&simulation.Elapsed<180)continue;
                if(!(prefab.GetComponent<IHazard>() is IMapHazard definition)||!definition.TryValidate(Services.World,out _))continue;
                bool running=false;
                foreach(var component in session.GetComponentsInChildren<MonoBehaviour>())if(component is IHazard active&&active.Active&&active.Type==definition.Type){running=true;break;}
                if(!running){choices.Add(prefab);if(key.Contains("earthquake")||key.Contains("outage")||key.Contains("tornado"))choices.Add(prefab);}
            }
            return choices.Count==0?null:choices[disasterRandom.Next(choices.Count)].name;
        }
        bool LaunchScheduled(string id)
        {
            GameObject prefab=null;foreach(var candidate in DemoDisasters.Prefabs)if(candidate&&candidate.name==id){prefab=candidate;break;}
            if(!prefab)return false;
            var parameters=DemoDisasters.Parameters(prefab);var values=new float[parameters.Count];
            for(int i=0;i<values.Length;i++)
            {
                values[i]=parameters[i].initial;
                if(parameters[i].id=="warning")values[i]=parameters[i].min;
                if(ModeSimulation.TeamMode(Rules.mode))
                {
                    string parameter=parameters[i].id.ToLowerInvariant();
                    if(parameter.Contains("force")||parameter.Contains("pull")||parameter.Contains("damage"))values[i]=parameters[i].Clamp(values[i]*.55f);
                    if(parameter=="duration")values[i]=parameters[i].Clamp(Mathf.Min(values[i],18));
                }
            }
            bool started=DemoDisasters.ExecuteForMode(session,id,values);if(started)lastDisaster=id;
            return started;
        }
        int ActiveDisasters()
        {int count=0;foreach(var component in session.GetComponentsInChildren<MonoBehaviour>())if(component is IHazard hazard&&hazard.Active)count++;return count;}
        void TickDisasters()
        {
            if(!Authority||simulation.Phase!=RoundPhase.Playing||Rules.mode==GameModeKind.Demo)return;
            string before=disasters.Pending;
            disasters.Tick(Rules,simulation.Elapsed,ActiveDisasters(),ChooseDisaster,LaunchScheduled);
            if(before!=disasters.Pending&&disasters.Pending.Length>0)GameEvents.RaiseEmoted(gameObject,"DisasterWarning");
        }
        void WriteDisasterFrame()
        {
            if(simulation.Phase!=RoundPhase.Playing){frame.disasterWarning="";frame.disasterWarningRemaining=frame.disasterNextIn=0;return;}
            frame.disasterWarning=disasters.Pending;
            frame.disasterWarningRemaining=disasters.WarningRemaining(simulation.Elapsed);
            frame.disasterNextIn=disasters.NextIn(simulation.Elapsed);
        }
    }
}
