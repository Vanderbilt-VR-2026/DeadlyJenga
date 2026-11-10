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
        const float SurvivalDisasterDuration=60;
        readonly List<(IHazard hazard,float end)> survivalDisasters=new List<(IHazard,float)>();
        string lastDisaster="";

        static bool SupportsSurvivalDuration(IDemoHazard hazard)
        {
            if(hazard==null)return false;
            foreach(var parameter in hazard.Parameters)
                if(parameter.id=="duration")return parameter.min<=SurvivalDisasterDuration&&parameter.max>=SurvivalDisasterDuration;
            return false;
        }
        bool PlayDisasters=>RoomGameExperience.ForManagedMatch(session)?.PlayerPlaylist==true;
        static bool StructuralDisaster(string key)=>key.Contains("earthquake")||key.Contains("tornado")||key.Contains("tsunami");
        bool EligibleSurvivalDisaster(GameObject prefab)=>SupportsSurvivalDuration(prefab.GetComponent<IDemoHazard>())||
            (PlayDisasters&&prefab.GetComponent<IWaveState>()!=null);

        string ChooseDisaster()
        {
            var choices=new List<GameObject>();
            var structural=new List<GameObject>();
            bool play=PlayDisasters;
            foreach(var prefab in DemoDisasters.Prefabs)
            {
                if(!prefab||prefab.name==lastDisaster)continue;
                string key=prefab.name.ToLowerInvariant();
                var options=prefab.GetComponent<IDemoHazard>();
                if(options?.Toggle==true||key.Contains("planet")||key.Contains("vampire"))continue;
                if(Rules.mode==GameModeKind.Survival&&!EligibleSurvivalDisaster(prefab))continue;
                bool team=ModeSimulation.TeamMode(Rules.mode);
                if(team&&(key.Contains("fire")||key.Contains("meteor")||key.Contains("acid")||key.Contains("antigravity"))&&simulation.Elapsed<180)continue;
                if(!(prefab.GetComponent<IHazard>() is IMapHazard definition)||!definition.TryValidate(Services.World,out _))continue;
                bool running=false;
                foreach(var component in session.GetComponentsInChildren<MonoBehaviour>())if(component is IHazard active&&active.Active&&active.Type==definition.Type){running=true;break;}
                if(running)continue;
                choices.Add(prefab);
                if(StructuralDisaster(key))structural.Add(prefab);
                if(StructuralDisaster(key)||(!play&&key.Contains("outage")))choices.Add(prefab);
            }
            // Give both public presets a physical opener. Keep variety after it.
            var pool=play&&simulation.Elapsed<60&&structural.Count>0?structural:choices;
            return pool.Count==0?null:pool[disasterRandom.Next(pool.Count)].name;
        }
        bool LaunchScheduled(string id)
        {
            GameObject prefab=null;foreach(var candidate in DemoDisasters.Prefabs)if(candidate&&candidate.name==id){prefab=candidate;break;}
            if(!prefab||Rules.mode==GameModeKind.Survival&&!EligibleSurvivalDisaster(prefab))return false;
            bool play=PlayDisasters;
            bool structural=StructuralDisaster(prefab.name.ToLowerInvariant());
            var parameters=DemoDisasters.Parameters(prefab);var values=new float[parameters.Count];
            for(int i=0;i<values.Length;i++)
            {
                values[i]=parameters[i].initial;
                if(parameters[i].id=="warning")values[i]=parameters[i].min;
                if(Rules.mode==GameModeKind.Survival&&parameters[i].id=="duration")values[i]=SurvivalDisasterDuration;
                if(ModeSimulation.TeamMode(Rules.mode))
                {
                    string parameter=parameters[i].id.ToLowerInvariant();
                    float scale=play&&structural&&!parameter.Contains("damage")?.85f:.55f;
                    if(parameter.Contains("force")||parameter.Contains("pull")||parameter.Contains("damage"))values[i]=parameters[i].Clamp(values[i]*scale);
                    if(parameter=="duration")values[i]=parameters[i].Clamp(Mathf.Min(values[i],18));
                }
            }
            bool started=DemoDisasters.ExecuteForMode(session,id,values,out var hazard);
            if(started&&Rules.mode==GameModeKind.Survival)
            {
                if(hazard==null)return false;
                survivalDisasters.Add((hazard,simulation.Elapsed+SurvivalDisasterDuration));
            }
            if(started)lastDisaster=id;
            return started;
        }
        int ActiveDisasters()
        {int count=0;foreach(var component in session.GetComponentsInChildren<MonoBehaviour>())if(component is IHazard hazard&&hazard.Active)count++;return count;}
        void TickDisasters()
        {
            if(!Authority||simulation.Phase!=RoundPhase.Playing||Rules.mode==GameModeKind.Demo)return;
            // Retire the oldest hazard before checking the overlap cap for the next activation.
            for(int i=survivalDisasters.Count-1;i>=0;i--)
            {
                var scheduled=survivalDisasters[i];
                if(!(scheduled.hazard is Component owner)||!owner||!scheduled.hazard.Active)
                    survivalDisasters.RemoveAt(i);
                else if(simulation.Elapsed>=scheduled.end)
                {
                    scheduled.hazard.End();
                    survivalDisasters.RemoveAt(i);
                }
            }
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
